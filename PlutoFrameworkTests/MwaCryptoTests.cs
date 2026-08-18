using Microsoft.AspNetCore.WebUtilities;
using PlutoFrameworkCore.Solana.Mwa;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace PlutoFrameworkTests
{
    public class MwaAssociationKeypairTests
    {
        [Test]
        public void PublicKeyPointIsX962Uncompressed()
        {
            using var keypair = MwaAssociationKeypair.Generate();

            Assert.Multiple(() =>
            {
                // 0x04 || x(32) || y(32) per X9.62 uncompressed encoding on P-256.
                Assert.That(keypair.PublicKeyPoint, Has.Length.EqualTo(65));
                Assert.That(keypair.PublicKeyPoint[0], Is.EqualTo(0x04));
            });
        }

        [Test]
        public void AssociationTokenIsBase64UrlOfPublicKeyPoint()
        {
            using var keypair = MwaAssociationKeypair.Generate();

            var decoded = WebEncoders.Base64UrlDecode(keypair.AssociationToken);

            Assert.That(decoded, Is.EqualTo(keypair.PublicKeyPoint));
        }

        [Test]
        public void AssociationTokenCarriesNoBase64Padding()
        {
            using var keypair = MwaAssociationKeypair.Generate();

            Assert.Multiple(() =>
            {
                Assert.That(keypair.AssociationToken, Does.Not.Contain("="));
                Assert.That(keypair.AssociationToken, Does.Not.Contain("+"));
                Assert.That(keypair.AssociationToken, Does.Not.Contain("/"));
            });
        }

        [Test]
        public void EachGeneratedKeypairIsDistinct()
        {
            using var first = MwaAssociationKeypair.Generate();
            using var second = MwaAssociationKeypair.Generate();

            Assert.That(first.AssociationToken, Is.Not.EqualTo(second.AssociationToken));
        }

        [Test]
        public void SignatureIsP1363AndVerifiesAgainstPublicKey()
        {
            using var keypair = MwaAssociationKeypair.Generate();
            var payload = new byte[65];
            RandomNumberGenerator.Fill(payload);

            var signature = keypair.SignPayload(payload);

            Assert.Multiple(() =>
            {
                // P1363 on P-256 is r||s, 32 bytes each. DER would be variable-length.
                Assert.That(signature, Has.Length.EqualTo(64));
                Assert.That(MwaAssociationKeypair.VerifyPayload(keypair.PublicKeyPoint, payload, signature), Is.True);
            });
        }

        [Test]
        public void SignatureFailsVerificationForTamperedPayload()
        {
            using var keypair = MwaAssociationKeypair.Generate();
            var payload = new byte[65];
            RandomNumberGenerator.Fill(payload);
            var signature = keypair.SignPayload(payload);

            payload[10] ^= 0xFF;

            Assert.That(MwaAssociationKeypair.VerifyPayload(keypair.PublicKeyPoint, payload, signature), Is.False);
        }
    }

    public class MwaHelloRequestTests
    {
        [Test]
        public void HelloRequestIsPublicKeyPointFollowedBySignature()
        {
            using var association = MwaAssociationKeypair.Generate();
            using var ephemeral = MwaEphemeralKeypair.Generate();

            var helloReq = MwaSession.BuildHelloRequest(association, ephemeral);

            Assert.Multiple(() =>
            {
                // <Qd (65)><Sa (64)>
                Assert.That(helloReq, Has.Length.EqualTo(129));
                Assert.That(helloReq[..65], Is.EqualTo(ephemeral.PublicKeyPoint));
                Assert.That(
                    MwaAssociationKeypair.VerifyPayload(association.PublicKeyPoint, helloReq[..65], helloReq[65..]),
                    Is.True);
            });
        }
    }

    public class MwaAssociationUriTests
    {
        [Test]
        public void LocalAssociationUriCarriesTokenPortAndVersion()
        {
            using var keypair = MwaAssociationKeypair.Generate();

            var uri = MwaAssociationUri.BuildLocal(keypair.AssociationToken, 49999);

            Assert.Multiple(() =>
            {
                Assert.That(uri, Does.StartWith("solana-wallet:/v1/associate/local?"));
                Assert.That(uri, Does.Contain($"association={keypair.AssociationToken}"));
                Assert.That(uri, Does.Contain("port=49999"));
                Assert.That(uri, Does.Contain("v=2"));
            });
        }

        [Test]
        public void GeneratedPortIsInEphemeralRange()
        {
            for (int i = 0; i < 200; i++)
            {
                var port = MwaAssociationUri.GeneratePort();

                Assert.That(port, Is.InRange(49152, 65535));
            }
        }
    }

    public class MwaSessionCipherTests
    {
        /// <summary>
        /// Builds the two halves of a real ECDH exchange, so the cipher pair under test is
        /// keyed exactly as a dapp and wallet would be.
        /// </summary>
        private static (MwaSessionCipher Dapp, MwaSessionCipher Wallet) EstablishedPair()
        {
            using var association = MwaAssociationKeypair.Generate();
            using var dappEphemeral = MwaEphemeralKeypair.Generate();
            using var walletEphemeral = MwaEphemeralKeypair.Generate();

            var dapp = MwaSessionCipher.Derive(dappEphemeral, walletEphemeral.PublicKeyPoint, association.PublicKeyPoint);
            var wallet = MwaSessionCipher.Derive(walletEphemeral, dappEphemeral.PublicKeyPoint, association.PublicKeyPoint);

            return (dapp, wallet);
        }

        [Test]
        public void DappDecryptsWhatWalletEncrypts()
        {
            var (dapp, wallet) = EstablishedPair();
            var plaintext = System.Text.Encoding.UTF8.GetBytes("""{"jsonrpc":"2.0","result":{}}""");

            var decrypted = dapp.Decrypt(wallet.Encrypt(plaintext));

            Assert.That(decrypted, Is.EqualTo(plaintext));
        }

        [Test]
        public void CipherKeyedFromADifferentAssociationCannotDecrypt()
        {
            var (dapp, _) = EstablishedPair();

            // A cipher from an unrelated exchange must not read this session's traffic,
            // which is what pins the association keypoint into the HKDF salt.
            var (_, unrelatedWallet) = EstablishedPair();

            Assert.Throws<AuthenticationTagMismatchException>(() => unrelatedWallet.Decrypt(dapp.Encrypt(new byte[8])));
        }

        [Test]
        public void WalletDecryptsWhatDappEncrypts()
        {
            var (dapp, wallet) = EstablishedPair();
            var plaintext = System.Text.Encoding.UTF8.GetBytes("""{"jsonrpc":"2.0","method":"authorize"}""");

            var decrypted = wallet.Decrypt(dapp.Encrypt(plaintext));

            Assert.That(decrypted, Is.EqualTo(plaintext));
        }

        [Test]
        public void FrameLayoutIsSequenceThenIvThenCiphertextAndTag()
        {
            var (dapp, _) = EstablishedPair();
            var plaintext = new byte[10];

            var frame = dapp.Encrypt(plaintext);

            // 4-byte sequence + 12-byte IV + ciphertext + 16-byte tag
            Assert.That(frame, Has.Length.EqualTo(4 + 12 + plaintext.Length + 16));
        }

        [Test]
        public void SequenceNumberStartsAtOneAndIsBigEndian()
        {
            var (dapp, _) = EstablishedPair();

            var frame = dapp.Encrypt(new byte[1]);

            Assert.That(frame[..4], Is.EqualTo(new byte[] { 0x00, 0x00, 0x00, 0x01 }));
        }

        [Test]
        public void SequenceNumberIncrementsPerMessage()
        {
            var (dapp, _) = EstablishedPair();

            dapp.Encrypt(new byte[1]);
            var second = dapp.Encrypt(new byte[1]);

            Assert.That(second[..4], Is.EqualTo(new byte[] { 0x00, 0x00, 0x00, 0x02 }));
        }

        [Test]
        public void EachFrameUsesAFreshIv()
        {
            var (dapp, _) = EstablishedPair();

            var first = dapp.Encrypt(new byte[1]);
            var second = dapp.Encrypt(new byte[1]);

            Assert.That(first[4..16], Is.Not.EqualTo(second[4..16]));
        }

        [Test]
        public void DecryptRejectsReplayedFrame()
        {
            var (dapp, wallet) = EstablishedPair();
            var frame = dapp.Encrypt(new byte[8]);

            wallet.Decrypt(frame);

            Assert.Throws<MwaProtocolException>(() => wallet.Decrypt(frame));
        }

        [Test]
        public void DecryptRejectsOutOfOrderFrame()
        {
            var (dapp, wallet) = EstablishedPair();
            var first = dapp.Encrypt(new byte[8]);
            var second = dapp.Encrypt(new byte[8]);

            // Skipping the first frame must be refused, not silently accepted.
            Assert.Throws<MwaProtocolException>(() => wallet.Decrypt(second));

            // And the sequence state must be intact enough to accept the real next frame.
            Assert.That(wallet.Decrypt(first), Has.Length.EqualTo(8));
        }

        [Test]
        public void DecryptRejectsTamperedCiphertext()
        {
            var (dapp, wallet) = EstablishedPair();
            var frame = dapp.Encrypt(new byte[8]);

            frame[^1] ^= 0xFF;

            Assert.Throws<AuthenticationTagMismatchException>(() => wallet.Decrypt(frame));
        }

        [Test]
        public void DecryptRejectsTamperedSequenceNumber()
        {
            var (dapp, wallet) = EstablishedPair();
            var frame = dapp.Encrypt(new byte[8]);

            frame[3] = 0x09;

            Assert.Throws<MwaProtocolException>(() => wallet.Decrypt(frame));
        }

        [Test]
        public void DecryptRejectsTruncatedFrame()
        {
            var (_, wallet) = EstablishedPair();

            Assert.Throws<MwaProtocolException>(() => wallet.Decrypt(new byte[15]));
        }

        [Test]
        public void DerivationRejectsMalformedPeerKeyPoint()
        {
            using var association = MwaAssociationKeypair.Generate();
            using var ephemeral = MwaEphemeralKeypair.Generate();

            Assert.Throws<MwaProtocolException>(() =>
                MwaSessionCipher.Derive(ephemeral, new byte[10], association.PublicKeyPoint));
        }
    }

    /// <summary>
    /// Pins our cipher to the wire format of the reference walletlib, which round-trip
    /// tests between two of our own ciphers cannot do: a deviation both sides share
    /// still round-trips, but no real wallet would accept the frames.
    ///
    /// The reference side here re-derives the session key independently and, like the
    /// walletlib, authenticates the 4-byte sequence number as AES-GCM associated data.
    /// </summary>
    public class MwaSessionCipherWireFormatTests
    {
        private const int SEQUENCE_LENGTH = 4;
        private const int IV_LENGTH = 12;
        private const int TAG_LENGTH = 16;

        private static (MwaSessionCipher Dapp, byte[] WalletKey) EstablishedAgainstReference()
        {
            using var association = MwaAssociationKeypair.Generate();
            using var dappEphemeral = MwaEphemeralKeypair.Generate();
            using var walletEphemeral = MwaEphemeralKeypair.Generate();

            var dapp = MwaSessionCipher.Derive(
                dappEphemeral, walletEphemeral.PublicKeyPoint, association.PublicKeyPoint);

            // ECDH + HKDF-SHA256(salt: association keypoint, L: 16), as the walletlib does.
            var sharedSecret = walletEphemeral.DeriveSharedSecret(dappEphemeral.PublicKeyPoint);

            var walletKey = HKDF.DeriveKey(
                HashAlgorithmName.SHA256,
                ikm: sharedSecret,
                outputLength: 16,
                salt: association.PublicKeyPoint,
                info: null);

            return (dapp, walletKey);
        }

        private static byte[] ReferenceEncrypt(byte[] key, uint sequence, byte[] plaintext)
        {
            var frame = new byte[SEQUENCE_LENGTH + IV_LENGTH + plaintext.Length + TAG_LENGTH];

            BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(0, SEQUENCE_LENGTH), sequence);
            RandomNumberGenerator.Fill(frame.AsSpan(SEQUENCE_LENGTH, IV_LENGTH));

            using var aes = new AesGcm(key, TAG_LENGTH);

            aes.Encrypt(
                nonce: frame.AsSpan(SEQUENCE_LENGTH, IV_LENGTH),
                plaintext: plaintext,
                ciphertext: frame.AsSpan(SEQUENCE_LENGTH + IV_LENGTH, plaintext.Length),
                tag: frame.AsSpan(SEQUENCE_LENGTH + IV_LENGTH + plaintext.Length, TAG_LENGTH),
                associatedData: frame.AsSpan(0, SEQUENCE_LENGTH));

            return frame;
        }

        private static byte[] ReferenceDecrypt(byte[] key, byte[] frame)
        {
            var plaintext = new byte[frame.Length - SEQUENCE_LENGTH - IV_LENGTH - TAG_LENGTH];

            using var aes = new AesGcm(key, TAG_LENGTH);

            aes.Decrypt(
                nonce: frame.AsSpan(SEQUENCE_LENGTH, IV_LENGTH),
                ciphertext: frame.AsSpan(SEQUENCE_LENGTH + IV_LENGTH, plaintext.Length),
                tag: frame.AsSpan(SEQUENCE_LENGTH + IV_LENGTH + plaintext.Length, TAG_LENGTH),
                plaintext: plaintext,
                associatedData: frame.AsSpan(0, SEQUENCE_LENGTH));

            return plaintext;
        }

        [Test]
        public void ReferenceWalletAcceptsDappFrame()
        {
            var (dapp, walletKey) = EstablishedAgainstReference();
            var plaintext = System.Text.Encoding.UTF8.GetBytes("""{"jsonrpc":"2.0","method":"authorize"}""");

            var frame = dapp.Encrypt(plaintext);

            Assert.That(ReferenceDecrypt(walletKey, frame), Is.EqualTo(plaintext));
        }

        [Test]
        public void DappAcceptsReferenceWalletFrame()
        {
            var (dapp, walletKey) = EstablishedAgainstReference();
            var plaintext = System.Text.Encoding.UTF8.GetBytes("""{"jsonrpc":"2.0","result":{}}""");

            var frame = ReferenceEncrypt(walletKey, sequence: 1, plaintext);

            Assert.That(dapp.Decrypt(frame), Is.EqualTo(plaintext));
        }
    }
}
