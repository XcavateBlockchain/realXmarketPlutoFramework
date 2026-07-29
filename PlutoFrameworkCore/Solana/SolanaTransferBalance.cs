using System.Numerics;

namespace PlutoFrameworkCore.Solana
{
    /// <summary>
    /// One row of the transfer token picker: a token the user may send, and how much of it.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="SolanaTokenBalance"/>, which the balances list and detail page
    /// use, because the two answer different questions. That one reports what the wallet
    /// holds, summing every token account for a mint. This one reports what a transfer can
    /// spend, which is the derived associated token account alone — an SPL transfer draws on
    /// one account, so the sum would let Max fill an amount that cannot send.
    /// </remarks>
    public sealed record SolanaTransferBalance
    {
        public required string Symbol { get; init; }

        public required string Mint { get; init; }

        public required int Decimals { get; init; }

        /// <summary>The token program owning the mint. Governs how the account is derived.</summary>
        public required string ProgramId { get; init; }

        public required bool IsNative { get; init; }

        /// <summary>Base units in the derived associated token account, or lamports for SOL.</summary>
        public required BigInteger SpendableBaseUnits { get; init; }
    }
}
