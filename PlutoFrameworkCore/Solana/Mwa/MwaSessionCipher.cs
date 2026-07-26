using System.Buffers.Binary;
using System.Security.Cryptography;

namespace PlutoFrameworkCore.Solana.Mwa
{
    /// <summary>
    /// The encrypted Mobile Wallet Adapter session channel.
    ///
    /// Both endpoints derive one shared AES-128 key via ECDH plus HKDF-SHA256, salted with
    /// the association keypoint. Each frame is
    /// <c>[4-byte big-endian sequence][12-byte IV][ciphertext][16-byte tag]</c>, with the
    /// sequence bytes authenticated as AES-GCM associated data — a frame sealed without
    /// them is rejected by real wallets.
    ///
    /// Outbound and inbound sequences are counted independently, since each direction
    /// numbers its own messages from 1.
    /// </summary>
    public sealed class MwaSessionCipher : IDisposable
    {
        private const int SEQUENCE_LENGTH = 4;
        private const int IV_LENGTH = 12;
        private const int TAG_LENGTH = 16;
        private const int AES_128_KEY_LENGTH = 16;

        private const int MINIMUM_FRAME_LENGTH = SEQUENCE_LENGTH + IV_LENGTH + TAG_LENGTH;

        private readonly AesGcm aes;

        private uint outboundSequence;
        private uint inboundSequence;

        private MwaSessionCipher(byte[] key)
        {
            aes = new AesGcm(key, TAG_LENGTH);
        }

        /// <summary>
        /// Derives the session key from our ephemeral private key and the peer's keypoint.
        /// The association keypoint is the HKDF salt, so a session cannot be relabelled
        /// onto a different association.
        /// </summary>
        public static MwaSessionCipher Derive(
            MwaEphemeralKeypair ephemeral,
            byte[] peerPublicKeyPoint,
            byte[] associationPublicKeyPoint)
        {
            var sharedSecret = ephemeral.DeriveSharedSecret(peerPublicKeyPoint);

            try
            {
                var key = HKDF.DeriveKey(
                    HashAlgorithmName.SHA256,
                    ikm: sharedSecret,
                    outputLength: AES_128_KEY_LENGTH,
                    salt: associationPublicKeyPoint,
                    info: null);

                return new MwaSessionCipher(key);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(sharedSecret);
            }
        }

        public byte[] Encrypt(byte[] plaintext)
        {
            var sequence = ++outboundSequence;

            var frame = new byte[SEQUENCE_LENGTH + IV_LENGTH + plaintext.Length + TAG_LENGTH];

            BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(0, SEQUENCE_LENGTH), sequence);

            var iv = frame.AsSpan(SEQUENCE_LENGTH, IV_LENGTH);
            RandomNumberGenerator.Fill(iv);

            aes.Encrypt(
                nonce: iv,
                plaintext: plaintext,
                ciphertext: frame.AsSpan(SEQUENCE_LENGTH + IV_LENGTH, plaintext.Length),
                tag: frame.AsSpan(SEQUENCE_LENGTH + IV_LENGTH + plaintext.Length, TAG_LENGTH),
                associatedData: frame.AsSpan(0, SEQUENCE_LENGTH));

            return frame;
        }

        /// <summary>
        /// Decrypts a frame, requiring its sequence number to be exactly one past the last
        /// accepted one. Replays and reordering are refused before any decryption is
        /// attempted, and a rejected frame leaves the inbound counter untouched so the
        /// genuine next frame still verifies.
        /// </summary>
        public byte[] Decrypt(byte[] frame)
        {
            if (frame.Length < MINIMUM_FRAME_LENGTH)
            {
                throw new MwaProtocolException(
                    $"Frame is {frame.Length} bytes, shorter than the {MINIMUM_FRAME_LENGTH}-byte minimum");
            }

            var sequence = BinaryPrimitives.ReadUInt32BigEndian(frame.AsSpan(0, SEQUENCE_LENGTH));

            var expected = inboundSequence + 1;

            if (sequence != expected)
            {
                throw new MwaProtocolException(
                    $"Expected frame sequence {expected} but received {sequence}");
            }

            var ciphertextLength = frame.Length - MINIMUM_FRAME_LENGTH;
            var plaintext = new byte[ciphertextLength];

            // Throws AuthenticationTagMismatchException if the frame was altered.
            aes.Decrypt(
                nonce: frame.AsSpan(SEQUENCE_LENGTH, IV_LENGTH),
                ciphertext: frame.AsSpan(SEQUENCE_LENGTH + IV_LENGTH, ciphertextLength),
                tag: frame.AsSpan(SEQUENCE_LENGTH + IV_LENGTH + ciphertextLength, TAG_LENGTH),
                plaintext: plaintext,
                associatedData: frame.AsSpan(0, SEQUENCE_LENGTH));

            // Only advance once the frame has authenticated.
            inboundSequence = sequence;

            return plaintext;
        }

        public void Dispose() => aes.Dispose();
    }
}
