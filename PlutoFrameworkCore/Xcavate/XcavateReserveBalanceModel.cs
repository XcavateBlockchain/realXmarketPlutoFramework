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
