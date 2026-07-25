using System.Security.Cryptography;

namespace PlutoFrameworkCore.Solana.Mwa
{
    /// <summary>
    /// Builds the <c>solana-wallet:</c> association URI that wallet apps register to handle.
    /// </summary>
    public static class MwaAssociationUri
    {
        /// <summary>
        /// The protocol version advertised in the URI. 2 selects Mobile Wallet Adapter 2.0.
        /// </summary>
        private const int PROTOCOL_VERSION = 2;

        /// <summary>
        /// The IANA dynamic/private port range, which the specification requires the
        /// port to be drawn from.
        /// </summary>
        private const int MINIMUM_PORT = 49152;
        private const int MAXIMUM_PORT = 65535;

        public static int GeneratePort() => RandomNumberGenerator.GetInt32(MINIMUM_PORT, MAXIMUM_PORT + 1);

        /// <summary>
        /// A local association URI, for a wallet installed on this same device.
        /// </summary>
        /// <remarks>
        /// The association token is already base64url, which is URL-safe, so it is
        /// interpolated rather than escaped. Escaping it would corrupt the token.
        /// </remarks>
        public static string BuildLocal(string associationToken, int port) =>
            $"solana-wallet:/v1/associate/local?association={associationToken}&port={port}&v={PROTOCOL_VERSION}";

        /// <summary>
        /// The WebSocket endpoint the wallet is expected to serve for a local association.
        /// The wallet is the server here; we connect to it as a client.
        /// </summary>
        public static Uri BuildLocalWebSocket(int port) => new($"ws://127.0.0.1:{port}/solana-wallet");
    }
}
