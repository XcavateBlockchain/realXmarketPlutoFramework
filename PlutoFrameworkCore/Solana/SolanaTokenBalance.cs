namespace PlutoFrameworkCore.Solana
{
    /// <summary>One row on the balances page.</summary>
    public sealed record SolanaTokenBalance
    {
        public required string Symbol { get; init; }

        public required string Mint { get; init; }

        /// <summary>Display units, already scaled by decimals.</summary>
        public required decimal Amount { get; init; }

        public required int Decimals { get; init; }

        public required bool IsNative { get; init; }

        /// <summary>
        /// Whether the detail page draws a price chart for this row. Carried on the row, the
        /// way <see cref="Symbol"/> and <see cref="Decimals"/> are, so the detail page needs
        /// neither the whitelist nor the cluster to decide.
        /// </summary>
        public required bool ShowPriceChart { get; init; }

        /// <summary>Null when no price is known. Not zero — those mean different things.</summary>
        public double? UsdValue { get; init; }
    }

    /// <summary>
    /// One token account's amount, free of Solnet types so the assembler stays testable.
    /// </summary>
    public sealed record SolanaTokenAccountAmount
    {
        public required string Mint { get; init; }

        /// <summary>Raw base units, as the RPC returns them.</summary>
        public required string RawAmount { get; init; }

        public required int Decimals { get; init; }
    }
}
