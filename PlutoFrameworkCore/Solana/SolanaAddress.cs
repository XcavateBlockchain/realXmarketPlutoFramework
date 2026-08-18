using SolanaPublicKey = Solnet.Wallet.PublicKey;

namespace PlutoFrameworkCore.Solana
{
    /// <summary>
    /// Translates Solana addresses between the base64 form Mobile Wallet Adapter uses on
    /// the wire and the base58 form every Solana interface displays.
    ///
    /// This is the second and only other place that touches Solnet. Unlike Account,
    /// Wallet and Mnemonic, <c>PublicKey</c> collides with nothing in Substrate, so it
    /// carries none of the ambiguity that keeps the rest of Solnet behind
    /// <see cref="PlutoFramework.Model.SolanaMnemonicsModel"/>.
    /// </summary>
    public static class SolanaAddress
    {
        /// <summary>
        /// Length of an Ed25519 public key, and therefore of every Solana address.
        /// </summary>
        private const int PUBLIC_KEY_LENGTH = 32;

        /// <summary>
        /// Converts a base64 wire address into the base58 form shown to users.
        /// </summary>
        /// <exception cref="FormatException">
        /// The input is not base64, or does not decode to a 32-byte key.
        /// </exception>
        public static string FromBase64(string base64Address)
        {
            byte[] keyBytes;

            try
            {
                keyBytes = Convert.FromBase64String(base64Address);
            }
            catch (FormatException ex)
            {
                throw new FormatException($"Address is not valid base64: {ex.Message}", ex);
            }

            if (keyBytes.Length != PUBLIC_KEY_LENGTH)
            {
                throw new FormatException(
                    $"Address decoded to {keyBytes.Length} bytes, expected {PUBLIC_KEY_LENGTH}");
            }

            return new SolanaPublicKey(keyBytes).Key;
        }

        /// <summary>
        /// Converts a base58 address into the base64 form the protocol expects.
        /// </summary>
        public static string ToBase64(string base58Address) =>
            Convert.ToBase64String(new SolanaPublicKey(base58Address).KeyBytes);
    }
}
