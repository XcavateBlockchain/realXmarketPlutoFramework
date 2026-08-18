namespace PlutoFrameworkCore.Solana
{
    /// <summary>
    /// SOL. Not an SPL token and never a whitelist entry: it pays every fee, so it must be
    /// visible even when the whitelist is empty or misconfigured.
    /// </summary>
    public static class SolanaNativeToken
    {
        public const string Symbol = "SOL";

        /// <summary>
        /// The wrapped-SOL mint. SOL itself has no mint; this is the address price feeds
        /// key it by.
        /// </summary>
        public const string Mint = "So11111111111111111111111111111111111111112";

        public const int Decimals = 9;

        public const ulong LamportsPerSol = 1_000_000_000;
    }
}
