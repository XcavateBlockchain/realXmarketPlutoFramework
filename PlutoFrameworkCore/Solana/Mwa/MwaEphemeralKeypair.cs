using System.Security.Cryptography;

namespace PlutoFrameworkCore.Solana.Mwa
{
    /// <summary>
    /// A per-session P-256 ECDH keypair. The public keypoint is exchanged in
    /// HELLO_REQ/HELLO_RSP and the private half is used once to derive the session key.
    /// </summary>
    public sealed class MwaEphemeralKeypair : IDisposable
    {
        private readonly ECDiffieHellman ecdh;

        private MwaEphemeralKeypair(ECDiffieHellman ecdh)
        {
            this.ecdh = ecdh;

            PublicKeyPoint = MwaKeyPoint.Encode(ecdh.PublicKey.ExportParameters());
        }

        public static MwaEphemeralKeypair Generate() =>
            new(ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256));

        /// <summary>
        /// X9.62 uncompressed encoding of the public keypoint: 0x04 || x || y, 65 bytes.
        /// </summary>
        public byte[] PublicKeyPoint { get; }

        /// <summary>
        /// The raw 32-byte ECDH shared secret with the peer, before any key derivation.
        /// </summary>
        public byte[] DeriveSharedSecret(byte[] peerPublicKeyPoint)
        {
            using var peer = ECDiffieHellman.Create(MwaKeyPoint.Decode(peerPublicKeyPoint));

            return ecdh.DeriveRawSecretAgreement(peer.PublicKey);
        }

        public void Dispose() => ecdh.Dispose();
    }
}
