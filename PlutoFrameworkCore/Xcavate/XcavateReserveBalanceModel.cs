using PlutoFrameworkCore.Solana;
using System.Globalization;

namespace PlutoFramework.Model.Xcavate
{
    /// <summary>
    /// The client-side guard behind the reserve popup: the user's tGBP balance and whether
    /// a new reservation would push it below zero once what is already reserved is
    /// subtracted.
    /// </summary>
    /// <remarks>
    /// reserve_shares moves no funds: the money stays in the investor's own payment
    /// account, bound by the reservation until claim_shares pays for it. The wallet must
    /// therefore still hold enough tGBP for everything reserved so far, and this check is
    /// the client-side view of that: balance minus the value of the shares the user has
    /// already reserved must still cover the cost of the new reservation, or the balance
    /// would effectively go negative.
    /// </remarks>
    public static class XcavateReserveBalanceModel
    {
        public const string TgBpSymbol = "tGBP";

        /// <summary>The tGBP whitelist entry for one cluster, when the app configured it.</summary>
        public static SolanaTokenWhitelistEntry? FindTgBpEntry(SolanaCluster cluster) =>
            SolanaTokenWhitelist.ForCluster(cluster)
                .FirstOrDefault(entry => string.Equals(entry.Symbol, TgBpSymbol, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// The <paramref name="address"/>'s tGBP balance on <paramref name="cluster"/>, in
        /// display units - zero when the wallet holds no token account for the mint.
        /// </summary>
        public static async Task<decimal> GetTgBpBalanceAsync(
            SolanaCluster cluster, string address, CancellationToken token)
        {
            var entry = FindTgBpEntry(cluster)
                ?? throw new InvalidOperationException($"tGBP is not configured for {cluster.GetName()}.");

            var accounts = await SolanaRpcModel.GetTokenAccountsAsync(cluster, address, entry.ProgramId, token)
                .ConfigureAwait(false);

            decimal balance = 0m;

            foreach (var account in accounts)
            {
                var info = account.Account?.Data?.Parsed?.Info;

                if (info?.TokenAmount is null || info.Mint != entry.Mint)
                {
                    continue;
                }

                balance += SolanaAmount.FromBaseUnits(info.TokenAmount.Amount, entry.Decimals);
            }

            return balance;
        }

        /// <summary>
        /// The tGBP value still sitting in the wallet, bound by every position in
        /// <paramref name="positions"/>: reserved-but-not-yet-bought shares at each listing's
        /// price per token, summed. Bought shares are excluded - claim_shares already paid
        /// for them, so that money has left the wallet. The 1% reservation fee is excluded
        /// as well: it was charged when the reservation was made.
        /// </summary>
        /// <remarks>
        /// Pure on purpose: the balances and detail pages call it with the indexer's result,
        /// and the unit tests call it with hand-built positions.
        /// </remarks>
        public static decimal ComputeReservedValue(IReadOnlyList<XcavateSolanaInvestorProperty> positions) =>
            positions.Sum(position => (decimal)position.ReservedShares
                * (position.Listing.XcavateMetadata?.Financials.PricePerToken ?? 0));

        /// <summary>
        /// The wallet-wide tGBP value <paramref name="address"/> has bound through reservations,
        /// in display units. The Xcavate devnet marketplace is the only cluster the reservation
        /// flow knows, so the cluster is not a parameter.
        /// </summary>
        public static async Task<decimal> GetReservedTgBpValueAsync(string address, CancellationToken token)
        {
            // 100 positions per page matches the Substrate-owned-properties limit; an investor
            // holding more listings than that on the devnet marketplace is beyond what the
            // app's other pages list either, so the cap is an accepted, known limitation.
            var positions = await XcavateMarketplaceIndexerModel
                .GetInvestorPropertiesAsync(address, null, null, null, null, null, 100, 0, token)
                .ConfigureAwait(false);

            return ComputeReservedValue(positions);
        }

        /// <summary>
        /// The total cost of reserving <paramref name="shares"/>: the funds plus the 1%
        /// investor-side fee - the same figure the popup prints as its total price.
        /// </summary>
        public static decimal ComputeReservationTotalCost(uint shares, decimal pricePerShare) =>
            (decimal)1.01 * shares * pricePerShare;

        /// <summary>
        /// Whether the wallet's tGBP balance stays at or above zero after the reservation:
        /// subtract the value of what is already reserved (bound until claim) from the
        /// balance, and the remainder must still cover the new reservation's total cost.
        /// </summary>
        public static bool CanAfford(
            decimal tGbpBalance,
            decimal alreadyReservedValue,
            decimal reservationTotalCost) =>
            tGbpBalance - alreadyReservedValue - reservationTotalCost >= 0m;

        public static string InsufficientBalanceMessage(
            decimal tGbpBalance,
            decimal alreadyReservedValue,
            decimal reservationTotalCost) =>
            $"Insufficient tGBP balance: your {Format(tGbpBalance)} tGBP, minus the {Format(alreadyReservedValue)} tGBP already reserved, cannot cover the {Format(reservationTotalCost)} tGBP total cost of this reservation.";

        /// <summary>Four fixed decimal places, the way tGBP balances print elsewhere in the app.</summary>
        public static string Format(decimal amount) =>
            amount.ToString("0.0000", CultureInfo.InvariantCulture);
    }
}
