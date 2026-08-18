using System.Security.Cryptography;

namespace PlutoFrameworkCore.Solana.Mwa
{
    /// <summary>
    /// X9.62 uncompressed keypoint encoding for P-256, which is how Mobile Wallet Adapter
    /// carries every public key on the wire.
    /// </summary>
    internal static class MwaKeyPoint
    {
        private const int COORDINATE_LENGTH = 32;

        internal const int ENCODED_LENGTH = 1 + (COORDINATE_LENGTH * 2);

        private const byte UNCOMPRESSED_TAG = 0x04;

        internal static byte[] Encode(ECParameters parameters)
        {
            var encoded = new byte[ENCODED_LENGTH];

            encoded[0] = UNCOMPRESSED_TAG;

            // X and Y are already fixed-width for a named curve, but copy right-aligned so
            // a leading-zero coordinate cannot shift the layout.
            var x = parameters.Q.X!;
            var y = parameters.Q.Y!;

            x.CopyTo(encoded, 1 + (COORDINATE_LENGTH - x.Length));
            y.CopyTo(encoded, 1 + COORDINATE_LENGTH + (COORDINATE_LENGTH - y.Length));

            return encoded;
        }

        internal static ECParameters Decode(byte[] keyPoint)
        {
            if (keyPoint.Length != ENCODED_LENGTH || keyPoint[0] != UNCOMPRESSED_TAG)
            {
                throw new MwaProtocolException(
                    $"Expected a {ENCODED_LENGTH}-byte X9.62 uncompressed P-256 keypoint, got {keyPoint.Length} bytes");
            }

            return new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint
                {
                    X = keyPoint[1..(1 + COORDINATE_LENGTH)],
                    Y = keyPoint[(1 + COORDINATE_LENGTH)..],
                },
            };
        }
    }
}
