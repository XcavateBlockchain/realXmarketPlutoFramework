namespace PlutoFrameworkCore.Solana
{
    /// <summary>
    /// Turns a lamport balance and a bag of token accounts into the rows the page shows.
    /// Pure: every rule the page depends on is decided here, where it can be tested without
    /// a network.
    /// </summary>
    public static class SolanaBalanceAssembler
    {
        public static IReadOnlyList<SolanaTokenBalance> Assemble(
            ulong lamports,
            IReadOnlyList<SolanaTokenAccountAmount> tokenAccounts,
            IReadOnlyList<SolanaTokenWhitelistEntry> whitelist,
            IReadOnlyDictionary<string, double> usdPrices)
        {
            var rows = new List<SolanaTokenBalance>(whitelist.Count + 1);

            var solAmount = SolanaAmount.FromLamports(lamports);

            rows.Add(new SolanaTokenBalance
            {
                Symbol = SolanaNativeToken.Symbol,
                Mint = SolanaNativeToken.Mint,
                Amount = solAmount,
                Decimals = SolanaNativeToken.Decimals,
                IsNative = true,
                // Not configurable, unlike a whitelist row: SOL is the one token here whose
                // price moves, and no configuration mistake should be able to hide that.
                ShowPriceChart = true,
                UsdValue = ToUsdValue(solAmount, SolanaNativeToken.Mint, usdPrices),
            });

            // One wallet can hold several accounts for the same mint; the balance is the sum.
            var amountByMint = new Dictionary<string, decimal>(StringComparer.Ordinal);

            var whitelistedMints = new HashSet<string>(
                whitelist.Select(entry => entry.Mint), StringComparer.Ordinal);

            foreach (var account in tokenAccounts)
            {
                // Unlisted mints are dropped before conversion, not just before display: a
                // spam or dust token can report an absurd decimals value (mint decimals is an
                // on-chain u8, freely settable to 255), and converting it would throw before
                // any row — SOL included — gets built.
                if (!whitelistedMints.Contains(account.Mint))
                {
                    continue;
                }

                var amount = SolanaAmount.FromBaseUnits(account.RawAmount, account.Decimals);

                amountByMint[account.Mint] = amountByMint.TryGetValue(account.Mint, out var running)
                    ? running + amount
                    : amount;
            }

            foreach (var entry in whitelist)
            {
                // Absent means the user has no account for this mint, which is a zero balance
                // rather than a reason to omit the row.
                var amount = amountByMint.TryGetValue(entry.Mint, out var held) ? held : 0m;

                rows.Add(new SolanaTokenBalance
                {
                    Symbol = entry.Symbol,
                    Mint = entry.Mint,
                    Amount = amount,
                    Decimals = entry.Decimals,
                    IsNative = false,
                    ShowPriceChart = entry.ShowPriceChart,
                    UsdValue = ToUsdValue(amount, entry.Mint, usdPrices),
                });
            }

            return rows;
        }

        /// <summary>
        /// Sums the rows that have a price. An unpriced row contributes nothing rather than
        /// dragging the total to zero.
        /// </summary>
        public static double TotalUsd(IEnumerable<SolanaTokenBalance> rows) =>
            rows.Sum(row => row.UsdValue ?? 0d);

        private static double? ToUsdValue(
            decimal amount, string mint, IReadOnlyDictionary<string, double> usdPrices) =>
            usdPrices.TryGetValue(mint, out var price) ? (double)amount * price : null;
    }
}
