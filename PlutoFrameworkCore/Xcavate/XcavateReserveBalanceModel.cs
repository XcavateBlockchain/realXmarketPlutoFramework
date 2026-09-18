using PlutoFrameworkCore.Solana;
using System.Globalization;

namespace PlutoFramework.Model.Xcavate
{
    /// <summary>
    /// The client-side guard behind the reserve popup: the user's balance in a listing's
    /// payment token and whether a new reservation would push it below zero once what is
    /// already reserved is subtracted.
    /// </summary>
    /// <remarks>
    /// reserve_shares moves no funds: the money stays in the investor's own payment
    /// account, bound by the reservation until claim_shares pays for it. The wallet must
    /// therefore still hold enough of the payment token for everything reserved so far,
    /// and this check is the client-side view of that: balance minus the value of the
    /// shares the user has already reserved must still cover the cost of the new
    /// reservation, or the balance would effectively go negative.
    /// <para>
    /// Every value is grouped by payment token symbol: all listings sell for tGBP today,
    /// but the marketplace config already accepts a list of payment mints, so a listing
    /// priced in USDC or SOL must never be netted out of the tGBP row. Keep the buckets
    /// apart end to end - summing across symbols would mix currencies.
    /// </para>
    /// </remarks>
    public static class XcavateReserveBalanceModel
    {
        /// <summary>The one payment token every current listing prices its shares in.</summary>
        public const string TgBpSymbol = "tGBP";

        /// <summary>
        /// The page size every investor-properties query in this model uses. 100 positions
        /// per page matches the Substrate-owned-properties limit; an investor holding more
        /// listings than that on the devnet marketplace is beyond what the app's other
        /// pages list either, so the cap is an accepted, known limitation.
        /// </summary>
        public const int InvestorPropertiesPageSize = 100;

        /// <summary>The whitelist entry for one payment token symbol on one cluster, when configured.</summary>
        public static SolanaTokenWhitelistEntry? FindPaymentTokenEntry(SolanaCluster cluster, string symbol) =>
            SolanaTokenWhitelist.ForCluster(cluster)
                .FirstOrDefault(entry => string.Equals(entry.Symbol, symbol, StringComparison.OrdinalIgnoreCase));

        /// <summary>The tGBP whitelist entry for one cluster, when the app configured it.</summary>
        public static SolanaTokenWhitelistEntry? FindTgBpEntry(SolanaCluster cluster) =>
            FindPaymentTokenEntry(cluster, TgBpSymbol);

        /// <summary>
        /// The <paramref name="address"/>'s balance of one whitelisted token on
        /// <paramref name="cluster"/>, in display units - zero when the wallet holds no
        /// token account for the mint.
        /// </summary>
        public static async Task<decimal> GetTokenBalanceAsync(
            SolanaCluster cluster, string address, SolanaTokenWhitelistEntry entry, CancellationToken token)
        {
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
        /// The <paramref name="address"/>'s balance of the payment token
        /// <paramref name="symbol"/>, in display units. Throws when the token is not
        /// configured for the cluster - a balance that cannot be read must never pass
        /// for zero.
        /// </summary>
        public static Task<decimal> GetPaymentTokenBalanceAsync(
            SolanaCluster cluster, string address, string symbol, CancellationToken token) =>
            GetTokenBalanceAsync(cluster, address,
                FindPaymentTokenEntry(cluster, symbol)
                    ?? throw new InvalidOperationException($"{symbol} is not configured for {cluster.GetName()}."),
                token);

        /// <summary>
        /// The value still sitting in the wallet, bound by every position in
        /// <paramref name="positions"/>, grouped by payment token symbol:
        /// reserved-but-not-yet-bought shares at each listing's price per token, summed
        /// per symbol. Bought shares are excluded - claim_shares already paid for them,
        /// so that money has left the wallet. The 1% reservation fee is excluded as
        /// well: it was charged when the reservation was made.
        /// </summary>
        /// <remarks>
        /// Pure on purpose: the balances and detail pages call it with the indexer's
        /// result, and the unit tests call it with hand-built positions.
        /// </remarks>
        public static IReadOnlyDictionary<string, decimal> ComputeReservedValues(
            IReadOnlyList<XcavateSolanaInvestorProperty> positions) =>
            SumByPaymentToken(positions, position => position.ReservedShares);

        /// <summary>
        /// The value of every share the investor has committed to, at each listing's
        /// price per token, grouped by payment token symbol: bought shares (already
        /// paid for) plus reserved shares (still bound in the wallet until claim).
        /// Reserved properties count - before a claim moves the money, the reserved
        /// shares are the investor's assets in that property.
        /// </summary>
        public static IReadOnlyDictionary<string, decimal> ComputeTotalAssetValues(
            IReadOnlyList<XcavateSolanaInvestorProperty> positions) =>
            SumByPaymentToken(positions, position => position.CommittedShares);

        private static IReadOnlyDictionary<string, decimal> SumByPaymentToken(
            IReadOnlyList<XcavateSolanaInvestorProperty> positions,
            Func<XcavateSolanaInvestorProperty, uint> shares)
        {
            var totals = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

            foreach (var position in positions)
            {
                var value = (decimal)shares(position)
                    * (position.Listing.XcavateMetadata?.Financials.PricePerToken ?? 0);

                totals[position.PaymentTokenSymbol] =
                    totals.TryGetValue(position.PaymentTokenSymbol, out var running) ? running + value : value;
            }

            return totals;
        }

        /// <summary>One payment token's bucket out of a grouped result - zero when absent.</summary>
        public static decimal ValueFor(IReadOnlyDictionary<string, decimal> values, string symbol) =>
            values.TryGetValue(symbol, out var value) ? value : 0m;

        /// <summary>
        /// The wallet-wide value <paramref name="address"/> has bound through
        /// reservations, grouped by payment token symbol, in display units. The Xcavate
        /// devnet marketplace is the only cluster the reservation flow knows, so the
        /// cluster is not a parameter.
        /// </summary>
        public static async Task<IReadOnlyDictionary<string, decimal>> GetReservedValuesAsync(
            string address, CancellationToken token)
        {
            var positions = await XcavateMarketplaceIndexerModel
                .GetInvestorPropertiesAsync(address, null, null, null, null, null, InvestorPropertiesPageSize, 0, token)
                .ConfigureAwait(false);

            return ComputeReservedValues(positions);
        }

        /// <summary>
        /// The wallet-wide value of every property the investor has bought or reserved
        /// shares in, grouped by payment token symbol, in display units. Same query and
        /// page cap as <see cref="GetReservedValuesAsync"/>: no filters, so both reserved
        /// and purchased positions are in the total.
        /// </summary>
        public static async Task<IReadOnlyDictionary<string, decimal>> GetTotalAssetValuesAsync(
            string address, CancellationToken token)
        {
            var positions = await XcavateMarketplaceIndexerModel
                .GetInvestorPropertiesAsync(address, null, null, null, null, null, InvestorPropertiesPageSize, 0, token)
                .ConfigureAwait(false);

            return ComputeTotalAssetValues(positions);
        }

        /// <summary>
        /// The positions that actually bind <paramref name="paymentTokenSymbol"/> value in
        /// the wallet: reserved-but-not-yet-bought shares priced in that token. Bought-only
        /// positions are out (claim_shares already moved that money), and so is every other
        /// payment token - each token's reserved section lists only its own positions.
        /// </summary>
        /// <remarks>
        /// Pure on purpose: the tGBP detail page lists the result, and the unit tests call
        /// it with hand-built positions.
        /// </remarks>
        public static IReadOnlyList<XcavateSolanaInvestorProperty> ReservedPositions(
            IReadOnlyList<XcavateSolanaInvestorProperty> positions,
            string paymentTokenSymbol) =>
            positions
                .Where(position => position.ReservedShares > 0
                    && string.Equals(position.PaymentTokenSymbol, paymentTokenSymbol, StringComparison.OrdinalIgnoreCase))
                .ToList();

        /// <summary>
        /// One position's reserved value: its reserved shares at the listing's price per
        /// token, in the position's payment token. A listing whose offchain metadata has
        /// not been attached yet counts as zero rather than throwing.
        /// </summary>
        public static decimal ComputePositionReservedValue(XcavateSolanaInvestorProperty position) =>
            (decimal)position.ReservedShares
                * (position.Listing.XcavateMetadata?.Financials.PricePerToken ?? 0);

        /// <summary>
        /// The rows with one payment token's reserved value netted out of its row: the
        /// amount and USD value both drop by the reserved figure (the amount clamped at
        /// zero), and the row is marked <see cref="SolanaTokenBalance.IsAmountNetted"/>
        /// so a page that nets on its own - the token detail page does - does not
        /// subtract the value a second time. Every other row passes through unchanged.
        /// </summary>
        /// <remarks>
        /// Returns null when there is nothing to net - a non-positive reserved value or
        /// no unnetted row for the mint - so a caller can tell a no-op from a netted
        /// list.
        /// </remarks>
        public static IReadOnlyList<SolanaTokenBalance>? NetReservedValue(
            IReadOnlyList<SolanaTokenBalance> rows,
            SolanaTokenWhitelistEntry entry,
            decimal reserved)
        {
            if (reserved <= 0m)
            {
                return null;
            }

            return NetReservedByMint(rows, new Dictionary<string, decimal>(StringComparer.Ordinal)
            {
                [entry.Mint] = reserved,
            });
        }

        /// <summary>
        /// The rows with every payment token's reserved value netted out of its own row
        /// at once - the multi-token counterpart of <see cref="NetReservedValue"/>, and
        /// the one the balances page and the main page's balance cell net through, so the
        /// two displayed totals cannot drift apart. Symbols with no whitelist entry on
        /// the cluster are skipped rather than netted against the wrong row. Returns
        /// null when nothing was netted.
        /// </summary>
        public static IReadOnlyList<SolanaTokenBalance>? NetReservedValues(
            IReadOnlyList<SolanaTokenBalance> rows,
            SolanaCluster cluster,
            IReadOnlyDictionary<string, decimal> reservedValues)
        {
            var reservedByMint = new Dictionary<string, decimal>(StringComparer.Ordinal);

            foreach (var (symbol, reserved) in reservedValues)
            {
                if (reserved <= 0m)
                {
                    continue;
                }

                var entry = FindPaymentTokenEntry(cluster, symbol);

                if (entry is null)
                {
                    continue;
                }

                reservedByMint[entry.Mint] = reservedByMint.TryGetValue(entry.Mint, out var running)
                    ? running + reserved
                    : reserved;
            }

            if (reservedByMint.Count == 0)
            {
                return null;
            }

            return NetReservedByMint(rows, reservedByMint);
        }

        private static IReadOnlyList<SolanaTokenBalance>? NetReservedByMint(
            IReadOnlyList<SolanaTokenBalance> rows,
            IReadOnlyDictionary<string, decimal> reservedByMint)
        {
            var nettedRows = new List<SolanaTokenBalance>(rows.Count);
            var nettedAny = false;

            foreach (var row in rows)
            {
                if (row.IsAmountNetted || !reservedByMint.TryGetValue(row.Mint, out var reserved))
                {
                    nettedRows.Add(row);

                    continue;
                }

                var net = Math.Max(row.Amount - reserved, 0m);
                var netUsd = row.UsdValue is double usd && row.Amount > 0m
                    ? (double)net * (usd / (double)row.Amount)
                    : row.UsdValue;

                nettedRows.Add(row with { Amount = net, UsdValue = netUsd, IsAmountNetted = true });
                nettedAny = true;
            }

            return nettedAny ? nettedRows : null;
        }

        /// <summary>
        /// The total cost of reserving <paramref name="shares"/>: the funds plus the 1%
        /// investor-side fee - the same figure the popup prints as its total price.
        /// Priced in the listing's payment token.
        /// </summary>
        public static decimal ComputeReservationTotalCost(uint shares, decimal pricePerShare) =>
            (decimal)1.01 * shares * pricePerShare;

        /// <summary>
        /// Whether the wallet's payment token balance stays at or above zero after the
        /// reservation: subtract the value of what is already reserved (bound until
        /// claim) from the balance, and the remainder must still cover the new
        /// reservation's total cost. All three figures are in the same token - the
        /// listing's payment token.
        /// </summary>
        public static bool CanAfford(
            decimal balance,
            decimal alreadyReservedValue,
            decimal reservationTotalCost) =>
            balance - alreadyReservedValue - reservationTotalCost >= 0m;

        public static string InsufficientBalanceMessage(
            string symbol,
            decimal balance,
            decimal alreadyReservedValue,
            decimal reservationTotalCost) =>
            $"Insufficient {symbol} balance: your {Format(balance)} {symbol}, minus the {Format(alreadyReservedValue)} {symbol} already reserved, cannot cover the {Format(reservationTotalCost)} {symbol} total cost of this reservation.";

        /// <summary>Four fixed decimal places, the way token balances print elsewhere in the app.</summary>
        public static string Format(decimal amount) =>
            amount.ToString("0.0000", CultureInfo.InvariantCulture);
    }
}
