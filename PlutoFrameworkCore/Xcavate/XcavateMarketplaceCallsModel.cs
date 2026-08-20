using PlutoFrameworkCore.Solana;
using Solnet.Rpc.Models;
using Solnet.Wallet;
using StrawberryShake;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using XcavateDevnetIndexer;

namespace PlutoFramework.Model.Xcavate
{
    /// <summary>
    /// Builds the Solana marketplace transactions behind the property pages - the
    /// replacement for the Substrate-era <c>MarketplaceCalls</c> factories. Pure
    /// instruction encoding lives in <see cref="XcavateMarketplaceProgram"/>; this layer
    /// resolves what the encoding needs from live state: the listing's current price and
    /// fees, the investor's recorded position, the config's rent collector and accepted
    /// payment mints.
    /// </summary>
    /// <remarks>
    /// On the sponsor: buy_property_shares and claim_shares take a rent-fronting
    /// <c>payer</c> that the program pins to the config's rent collector, and a signer at
    /// that. This app holds only the investor's key, so those two transactions are built
    /// correctly but cannot carry the sponsor's signature until a co-signing service
    /// exists - they are rejected at submission with a signature-verification error
    /// today. The seam for that service is exactly here, where the payer is resolved.
    /// </remarks>
    public static class XcavateMarketplaceCallsModel
    {
        /// <summary>
        /// The cluster whose marketplace is transacted with - the same deployment the
        /// listing feed reads from, deliberately not the user's selected network (see
        /// <see cref="XcavateMarketplaceIndexerModel.MarketplaceCluster"/>).
        /// </summary>
        public const SolanaCluster MarketplaceCluster = XcavateMarketplaceIndexerModel.MarketplaceCluster;

        /// <summary>
        /// buy_property_shares for <paramref name="amount"/> shares of listing
        /// <paramref name="listingId"/>, paying with the first accepted payment mint the
        /// app knows. Price, fees and tax are read fresh from the indexer so the
        /// max-total-cost cap reflects the listing as it is now, not as the page loaded it.
        /// </summary>
        public static async Task<List<TransactionInstruction>> BuyPropertySharesAsync(
            string investor,
            long listingId,
            uint amount,
            CancellationToken token = default)
        {
            var programs = XcavateProgramAddresses.Require(MarketplaceCluster);
            var client = XcavateWhitelistIndexer.GetClient(MarketplaceCluster);

            var listing = await GetListingAsync(client, listingId, token).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Listing {listingId} does not exist or is closed.");

            var config = await GetConfigAsync(client, token).ConfigureAwait(false);

            var paymentMint = PickPaymentMint(config.AcceptedPaymentMints);
            var paymentTokenProgram = await ResolveTokenProgramAsync(paymentMint, token).ConfigureAwait(false);

            var sharePrice = ParseUInt64(listing.SharePrice);
            var maxTotalCost = ComputeMaxTotalCost(sharePrice, amount, listing.InvestorFeeBps, listing.TaxBps, listing.TaxPaidByDeveloper);

            var investorKey = new PublicKey(investor);

            return
            [
                XcavateMarketplaceProgram.BuyPropertyShares(
                    programs,
                    investorKey,
                    payer: new PublicKey(config.RentCollector),
                    (ulong)listingId,
                    amount,
                    maxTotalCost,
                    paymentMint,
                    investorPaymentAccount: SolanaAssociatedTokenAccount.Derive(investorKey, paymentMint, paymentTokenProgram),
                    paymentTokenProgram),
            ];
        }

        /// <summary>
        /// claim_shares for listing <paramref name="listingId"/>, paying from the exact
        /// account the investor's reservation was made with (the reservation PDA is keyed
        /// by it).
        /// </summary>
        public static async Task<List<TransactionInstruction>> ClaimSharesAsync(
            string investor,
            long listingId,
            CancellationToken token = default)
        {
            var programs = XcavateProgramAddresses.Require(MarketplaceCluster);
            var client = XcavateWhitelistIndexer.GetClient(MarketplaceCluster);

            var position = await GetPositionAsync(client, listingId, investor, token).ConfigureAwait(false);
            var config = await GetConfigAsync(client, token).ConfigureAwait(false);

            var paymentMint = new PublicKey(position.PaymentMint);
            var paymentTokenProgram = await ResolveTokenProgramAsync(paymentMint, token).ConfigureAwait(false);

            return
            [
                XcavateMarketplaceProgram.ClaimShares(
                    programs,
                    new PublicKey(investor),
                    payer: new PublicKey(config.RentCollector),
                    (ulong)listingId,
                    paymentMint,
                    recordedPaymentAccount: new PublicKey(position.PaymentAccount),
                    paymentTokenProgram),
            ];
        }

        /// <summary>
        /// create_spv for listing <paramref name="listingId"/>, called by an
        /// SpvConfirmation role holder. Pure - nothing has to be looked up.
        /// </summary>
        public static Task<List<TransactionInstruction>> CreateSpvAsync(
            string confirmer,
            long listingId,
            CancellationToken token = default)
        {
            var programs = XcavateProgramAddresses.Require(MarketplaceCluster);

            return Task.FromResult<List<TransactionInstruction>>(
            [
                XcavateMarketplaceProgram.CreateSpv(programs, new PublicKey(confirmer), (ulong)listingId),
            ]);
        }

        /// <summary>
        /// unreserve_shares for listing <paramref name="listingId"/> - cancels the
        /// investor's reservation, the successor to the pallet's cancel_property_purchase.
        /// </summary>
        public static async Task<List<TransactionInstruction>> CancelReservationAsync(
            string investor,
            long listingId,
            CancellationToken token = default)
        {
            var programs = XcavateProgramAddresses.Require(MarketplaceCluster);
            var client = XcavateWhitelistIndexer.GetClient(MarketplaceCluster);

            var position = await GetPositionAsync(client, listingId, investor, token).ConfigureAwait(false);

            return
            [
                XcavateMarketplaceProgram.UnreserveShares(
                    programs,
                    new PublicKey(investor),
                    (ulong)listingId,
                    recordedPaymentAccount: new PublicKey(position.PaymentAccount)),
            ];
        }

        /// <summary>withdraw_expired - refund after the listing expired unsold.</summary>
        public static Task<List<TransactionInstruction>> WithdrawExpiredAsync(
            string investor, long listingId, CancellationToken token = default) =>
            WithdrawAsync(XcavateMarketplaceProgram.WithdrawExpired, investor, listingId, token);

        /// <summary>withdraw_cancelled - refund from a cancelled or refunding listing.</summary>
        public static Task<List<TransactionInstruction>> WithdrawCancelledAsync(
            string investor, long listingId, CancellationToken token = default) =>
            WithdrawAsync(XcavateMarketplaceProgram.WithdrawCancelled, investor, listingId, token);

        /// <summary>withdraw_legal_process_expired - refund after the legal phase blew its deadline.</summary>
        public static Task<List<TransactionInstruction>> WithdrawLegalProcessExpiredAsync(
            string investor, long listingId, CancellationToken token = default) =>
            WithdrawAsync(XcavateMarketplaceProgram.WithdrawLegalProcessExpired, investor, listingId, token);

        private static async Task<List<TransactionInstruction>> WithdrawAsync(
            Func<XcavateProgramSet, PublicKey, ulong, PublicKey, PublicKey, PublicKey, PublicKey, TransactionInstruction> build,
            string investor,
            long listingId,
            CancellationToken token)
        {
            var programs = XcavateProgramAddresses.Require(MarketplaceCluster);
            var client = XcavateWhitelistIndexer.GetClient(MarketplaceCluster);

            var position = await GetPositionAsync(client, listingId, investor, token).ConfigureAwait(false);
            var config = await GetConfigAsync(client, token).ConfigureAwait(false);

            var paymentMint = new PublicKey(position.PaymentMint);
            var paymentTokenProgram = await ResolveTokenProgramAsync(paymentMint, token).ConfigureAwait(false);
            var investorKey = new PublicKey(investor);

            // The refund handlers deliberately do not pin the destination to the recorded
            // payment account (it may be closed by now); the investor's associated
            // account for the mint is the canonical place to refund into.
            return
            [
                build(
                    programs,
                    investorKey,
                    (ulong)listingId,
                    new PublicKey(config.RentCollector),
                    paymentMint,
                    SolanaAssociatedTokenAccount.Derive(investorKey, paymentMint, paymentTokenProgram),
                    paymentTokenProgram),
            ];
        }

        /// <summary>
        /// The most the program may charge for a buy: funds plus the investor-side fee
        /// plus tax when the developer is not covering it, each rounded up so a program
        /// that rounds either way still lands at or under the cap.
        /// </summary>
        public static ulong ComputeMaxTotalCost(
            ulong sharePrice,
            uint amount,
            int investorFeeBps,
            int taxBps,
            bool taxPaidByDeveloper)
        {
            var funds = (BigInteger)sharePrice * amount;
            var fee = CeilingBps(funds, investorFeeBps);
            var tax = taxPaidByDeveloper ? BigInteger.Zero : CeilingBps(funds, taxBps);

            var total = funds + fee + tax;

            return total <= ulong.MaxValue
                ? (ulong)total
                : throw new OverflowException("The total cost does not fit the program's u64 argument.");
        }

        private static BigInteger CeilingBps(BigInteger value, int bps) =>
            (value * bps + 9_999) / 10_000;

        /// <summary>
        /// The payment mint for a buy: the first accepted mint the app's token whitelist
        /// knows (so the user can actually see and hold it), else the first accepted mint.
        /// <paramref name="acceptedPaymentMintsJson"/> is the config's raw JSON list.
        /// </summary>
        public static PublicKey PickPaymentMint(string acceptedPaymentMintsJson)
        {
            var accepted = JsonSerializer.Deserialize<List<string>>(acceptedPaymentMintsJson) ?? [];

            if (accepted.Count == 0)
            {
                throw new InvalidOperationException("The marketplace config accepts no payment mints.");
            }

            var known = SolanaTokenWhitelist.ForCluster(MarketplaceCluster);

            var mint = accepted.FirstOrDefault(candidate => known.Any(entry => entry.Mint == candidate))
                ?? accepted[0];

            return new PublicKey(mint);
        }

        /// <summary>
        /// The token program owning <paramref name="mint"/> (classic or Token-2022) -
        /// from the app's token whitelist when the mint is configured there, else from
        /// the mint account's owner on chain.
        /// </summary>
        private static async Task<PublicKey> ResolveTokenProgramAsync(PublicKey mint, CancellationToken token)
        {
            var entry = SolanaTokenWhitelist.ForCluster(MarketplaceCluster)
                .FirstOrDefault(entry => entry.Mint == mint.Key);

            if (entry is not null)
            {
                return new PublicKey(entry.ProgramId);
            }

            var accountInfo = await SolanaRpcModel.GetAccountInfoAsync(MarketplaceCluster, mint.Key, token).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Payment mint {mint.Key} does not exist on {MarketplaceCluster.GetName()}.");

            return new PublicKey(accountInfo.Owner);
        }

        private static async Task<IListingParts?> GetListingAsync(
            IXcavateDevnetIndexerClient client, long listingId, CancellationToken token)
        {
            var result = await client.MarketplaceListing
                .ExecuteAsync(listingId.ToString(CultureInfo.InvariantCulture), token)
                .ConfigureAwait(false);

            result.EnsureNoErrors();

            return result.Data?.Listings.Nodes.FirstOrDefault();
        }

        private static async Task<IMarketplaceInvestorPositions_InvestorPositions_Nodes> GetPositionAsync(
            IXcavateDevnetIndexerClient client, long listingId, string investor, CancellationToken token)
        {
            var result = await client.MarketplaceInvestorPositions
                .ExecuteAsync(listingId.ToString(CultureInfo.InvariantCulture), investor, first: 1, offset: 0, token)
                .ConfigureAwait(false);

            result.EnsureNoErrors();

            return result.Data?.InvestorPositions.Nodes.FirstOrDefault()
                ?? throw new InvalidOperationException(
                    $"This wallet has no open position on listing {listingId}.");
        }

        private static async Task<IMarketplaceConfigInfo_MarketplaceConfig> GetConfigAsync(
            IXcavateDevnetIndexerClient client, CancellationToken token)
        {
            var result = await client.MarketplaceConfigInfo
                .ExecuteAsync(token)
                .ConfigureAwait(false);

            result.EnsureNoErrors();

            return result.Data?.MarketplaceConfig
                ?? throw new InvalidOperationException(
                    $"The marketplace is not initialized on {MarketplaceCluster.GetName()}.");
        }

        private static ulong ParseUInt64(string? value) =>
            ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }
}
