namespace PlutoFrameworkCore.Solana
{
    /// <summary>
    /// The <c>solana:</c> URI scheme, as the scanner reads it.
    /// </summary>
    /// <remarks>
    /// One implementation for both scan entry points — the global scanner and the address
    /// field inside the transfer popup — so the two cannot drift. The Substrate side has this
    /// same parsing written out twice, in <c>NavigationModel.OnScanned</c> and in
    /// <c>IdentityAddressView</c>, with subtly different handling of a missing suffix.
    /// </remarks>
    public static class SolanaUri
    {
        public const string Scheme = "solana:";

        /// <summary>
        /// The recipient address in a scanned string, or null if there is not one.
        /// </summary>
        /// <remarks>
        /// Accepts the app's own <c>solana:{address}</c>, a Solana Pay URI with parameters,
        /// and a bare address. Solana Pay's <c>amount</c> and <c>spl-token</c> are discarded:
        /// prefilling a token would mean resolving an arbitrary mint against the whitelist and
        /// deciding what to do when it is absent, which is a feature of its own. The recipient
        /// is still filled in, so scanning such a code is useful rather than rejected.
        ///
        /// The result is always validated, so a Substrate address — valid base58, wrong length
        /// — cannot come back from here and be aimed at the Solana network.
        /// </remarks>
        public static string? TryParseRecipient(string? scanned)
        {
            if (string.IsNullOrWhiteSpace(scanned))
            {
                return null;
            }

            var value = scanned.Trim();

            if (value.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase))
            {
                value = value[Scheme.Length..];
            }

            // Solana Pay parameters. Everything this app needs is before the '?'.
            var queryStart = value.IndexOf('?');

            if (queryStart >= 0)
            {
                value = value[..queryStart];
            }

            return SolanaAddressValidator.IsValidAddress(value) ? value : null;
        }
    }
}
