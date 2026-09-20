namespace PlutoFrameworkCore.Solana
{
    /// <summary>
    /// Tells whether a signing or submission failure is the blockhash having expired -
    /// the one failure a retry with a freshly fetched blockhash can fix. A blockhash
    /// lives about 150 slots (roughly a minute), which a slow approval in the wallet
    /// app can outlive; anything else (declined, insufficient funds, program errors)
    /// must not prompt again.
    /// </summary>
    public static class SolanaBlockhashExpiry
    {
        public static bool IsExpiredError(string? message) =>
            message?.Contains("blockhash", StringComparison.OrdinalIgnoreCase) == true;
    }
}
