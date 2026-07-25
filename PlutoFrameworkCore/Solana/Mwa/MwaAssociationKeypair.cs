using Microsoft.AspNetCore.WebUtilities;
using System.Security.Cryptography;

namespace PlutoFrameworkCore.Solana.Mwa
{
    /// <summary>
    /// The ephemeral P-256 association keypair. Its public keypoint becomes the
    /// association token in the URI handed to the wallet, and doubles as the HKDF salt,
    /// which is what binds a session to the association the user actually approved.
    ///
    /// The private half signs HELLO_REQ, proving to the wallet that whoever opened the
    /// WebSocket is the same party that issued the intent.
    /// </summary>
    public sealed class MwaAssociationKeypair : IDisposable
    {
        private readonly ECDsa ecdsa;

        private MwaAssociationKeypair(ECDsa ecdsa)
        {
            this.ecdsa = ecdsa;

            PublicKeyPoint = MwaKeyPoint.Encode(ecdsa.ExportParameters(includePrivateParameters: false));
        }

        public static MwaAssociationKeypair Generate() => new(ECDsa.Create(ECCurve.NamedCurves.nistP256));

        /// <summary>
        /// X9.62 uncompressed encoding of the public keypoint: 0x04 || x || y, 65 bytes.
        /// </summary>
        public byte[] PublicKeyPoint { get; }

        /// <summary>
        /// The association token carried in the URI, base64url with no padding.
        /// </summary>
        public string AssociationToken => WebEncoders.Base64UrlEncode(PublicKeyPoint);

        /// <summary>
        /// ECDSA-SHA256 over the payload, in the fixed-width P1363 form (r || s) that the
        /// protocol requires. The .NET default is DER, which is variable-length and would
        /// be rejected by the wallet.
        /// </summary>
        public byte[] SignPayload(byte[] payload) =>
            ecdsa.SignData(payload, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        public static bool VerifyPayload(byte[] publicKeyPoint, byte[] payload, byte[] signature)
        {
            using var verifier = ECDsa.Create(MwaKeyPoint.Decode(publicKeyPoint));

            return verifier.VerifyData(
                payload,
                signature,
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }

        public void Dispose() => ecdsa.Dispose();
    }
}
