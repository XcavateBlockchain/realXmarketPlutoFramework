namespace PlutoFrameworkCore.Solana
{
    /// <summary>
    /// Whether a string is a Solana address.
    /// </summary>
    /// <remarks>
    /// Deliberately not a length check. The Substrate transfer popup validates with
    /// <c>Address.Length == 48</c>, and an SS58 address is exactly 48 base58 characters — so
    /// that rule waves one straight through into a Solana transfer. Solana addresses are
    /// 32 to 44 characters depending on how many leading zero bytes they carry, which makes
    /// any length rule both too permissive and too strict.
    /// </remarks>
    public static class SolanaAddressValidator
    {
        /// <summary>An ed25519 public key, and therefore every Solana address.</summary>
        public const int PublicKeyLength = 32;

        /// <summary>
        /// Off-curve addresses are accepted. A program-derived address is a legitimate
        /// recipient — every associated token account is one — so requiring a point on the
        /// ed25519 curve would reject the commonest destination on the network.
        /// </summary>
        public static bool IsValidAddress(string? address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return false;
            }

            try
            {
                return SolanaBase58.Decode(address).Length == PublicKeyLength;
            }
            catch (FormatException)
            {
                // A pasted or scanned address is untrusted input. The caller asked a
                // question and must get an answer, not an exception.
                return false;
            }
        }
    }
}
