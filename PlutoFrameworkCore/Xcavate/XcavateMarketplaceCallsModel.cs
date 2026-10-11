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
    /// <para>
    /// Every method takes the cluster explicitly - the app's selected Solana network - so a
    /// transaction is always built against the deployment its listing came from. Callers
    /// gate on <see cref="XcavateDeploymentModel.IsDeployed"/> first; where that is false,
    /// <see cref="XcavateProgramAddresses.Require"/> throws the real reason.
    /// </para>
    /// </summary>
    /// <remarks>
    /// On the payer: reserve_shares, buy_property_shares and claim_shares
    /// all pin the <c>payer</c> to the config's rent collector and require
    /// its signature (devnet-verified against the deployed program). So each
    /// completes end-to-end only when the signing wallet IS that rent
    /// collector - the two required signatures collapse into one. With any
    /// other investor wallet the rent collector's half now comes from the
    /// profile API's rent-collector-signature endpoint: the key lives in
    /// that service's environment, and it signs the exact compiled message
    /// the investor then signs and submits. The transaction model detects
    /// the two-signer case and routes to that endpoint before the investor's
    /// wallet is ever opened.
    /// </remarks>
    public static class XcavateMarketplaceCallsModel
    {
        /// <summary>
        /// The smallest asking price relist_shares accepts, at the 9-decimal price scale -
        /// the program rejects anything below it.
        /// </summary>
        public const ulong MinSharePriceQuote = 1_000;

        /// <summary>
        /// reserve_shares for <paramref name="amount"/> shares of listing
        /// <paramref name="listingId"/> - the sale-phase purchase behind the Reserve button.
        /// The deployed program pins the rent-fronting payer to the config's rent
        /// collector and requires its signature, so the transaction needs the investor's
        /// and the rent collector's signatures (see the class remarks). Money stays in
        /// the investor's payment account, bound by a reservation, until claim_shares
        /// pays for it.
        /// Price, fees and tax are read fresh from the indexer so the max-total-cost cap
        /// reflects the listing as it is now, not as the page loaded it.
        /// </summary>
        public static Task<List<TransactionInstruction>> ReserveSharesAsync(
            SolanaCluster cluster,
            string investor,
            long listingId,
            uint amount,
            CancellationToken token = default) =>
            PurchaseAsync(XcavateMarketplaceProgram.ReserveShares, cluster, investor, listingId, amount, token, sponsorFrontsRent: true);

        /// <summary>
        /// buy_property_shares for <paramref name="amount"/> shares - the direct purchase
        /// the program only opens after the claim window closes. The buy popup submits
        /// this once <see cref="XcavateSolanaListingNft.DirectBuyIsOpen"/> says the window
        /// closed; while the listing sells it goes through <see cref="ReserveSharesAsync"/>.
        /// </summary>
        public static Task<List<TransactionInstruction>> BuyPropertySharesAsync(
            SolanaCluster cluster,
            string investor,
            long listingId,
            uint amount,
            CancellationToken token = default) =>
            PurchaseAsync(XcavateMarketplaceProgram.BuyPropertyShares, cluster, investor, listingId, amount, token, sponsorFrontsRent: true);

        private static async Task<List<TransactionInstruction>> PurchaseAsync(
            Func<XcavateProgramSet, PublicKey, PublicKey, ulong, uint, ulong, PublicKey, PublicKey, PublicKey, TransactionInstruction> build,
            SolanaCluster cluster,
            string investor,
            long listingId,
            uint amount,
            CancellationToken token,
            bool sponsorFrontsRent)
        {
            var programs = XcavateProgramAddresses.Require(cluster);
            var client = XcavateWhitelistIndexer.GetClient(cluster);

            var listing = await GetListingAsync(client, listingId, token).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Listing {listingId} does not exist or is closed.");

            var config = await GetConfigAsync(cluster, client, token).ConfigureAwait(false);

            var payment = await ResolvePaymentAsync(cluster, client, investor, listingId, config, token).ConfigureAwait(false);

            var sharePrice = ParseUInt64(listing.SharePrice);
            var maxTotalCost = ScaleToMintDecimals(
                ComputeMaxTotalCost(sharePrice, amount, listing.InvestorFeeBps, listing.TaxBps, listing.TaxPaidByDeveloper),
                payment.Decimals);

            // The deployed program pins the rent-fronting payer to the config's rent
            // collector on every purchase (reserve included) and requires its
            // signature, so the rent collector's key always fronts rent here.
            var payer = sponsorFrontsRent ? new PublicKey(config.RentCollector) : new PublicKey(investor);

            return
            [
                build(
                    programs,
                    new PublicKey(investor),
                    payer,
                    (ulong)listingId,
                    amount,
                    maxTotalCost,
                    payment.Mint,
                    payment.Account,
                    payment.TokenProgram),
            ];
        }

        /// <summary>
        /// How a purchase pays: the mint, the paying token account, its token program and
        /// its decimals.
        /// </summary>
        private readonly record struct PaymentRoute(
            PublicKey Mint,
            PublicKey Account,
            PublicKey TokenProgram,
            int Decimals);

        /// <summary>
        /// vote_on_spv_lawyer for <paramref name="amount"/> shares: the investor's
        /// share-weighted vote backing one candidacy in the listing's SPV-lawyer election -
        /// the pre-settlement vote the property page surfaces as the "terms" approval. The
        /// vote record's rent goes through the config's rent collector like every purchase
        /// (the deployed program accepts any paying wallet, so the two-signer flow the
        /// transaction model already runs for purchases covers this unchanged);
        /// <paramref name="round"/> is the election's current round, read from the
        /// indexer's listing row first — a stale round fails the record PDA's seed check.
        /// First votes only: switching an existing vote to a different candidacy needs the
        /// previous candidacy account, which the property page does not offer.
        /// </summary>
        public static async Task<List<TransactionInstruction>> VoteOnSpvLawyerAsync(
            SolanaCluster cluster,
            string voter,
            long listingId,
            ulong round,
            string candidate,
            uint amount,
            CancellationToken token = default)
        {
            var programs = XcavateProgramAddresses.Require(cluster);
            var client = XcavateWhitelistIndexer.GetClient(cluster);

            var config = await GetConfigAsync(cluster, client, token).ConfigureAwait(false);

            return
            [
                XcavateMarketplaceProgram.VoteOnSpvLawyer(
                    programs,
                    new PublicKey(voter),
                    new PublicKey(config.RentCollector),
                    (ulong)listingId,
                    round,
                    new PublicKey(candidate),
                    previousCandidate: null,
                    amount),
            ];
        }

        /// <summary>
        /// relist_shares: the seller lists <paramref name="amount"/> of their shares of
        /// asset <paramref name="assetId"/> on the secondary market at
        /// <paramref name="sharePriceQuote"/> per share (9-decimal price scale - build it
        /// with <see cref="QuoteFromDecimal"/>). The new ShareListing account's id is the
        /// config's next_share_listing_id, read fresh from the chain so the PDA seed check
        /// lands. Rent for the new account goes through the config's rent collector, like
        /// every purchase, so the transaction needs its signature next to the seller's
        /// (see the class remarks).
        /// </summary>
        public static async Task<List<TransactionInstruction>> RelistSharesAsync(
            string address,
            SolanaCluster cluster,
            ulong assetId,
            uint amount,
            ulong sharePriceQuote,
            CancellationToken token = default)
        {
            ValidateRelistArgs(amount, sharePriceQuote);

            var programs = XcavateProgramAddresses.Require(cluster);
            var config = await XcavateMarketplaceConfigModel.GetConfigAsync(cluster, token).ConfigureAwait(false);

            return
            [
                XcavateMarketplaceProgram.RelistShares(
                    programs,
                    new PublicKey(address),
                    payer: config.RentCollector,
                    assetId,
                    amount,
                    sharePriceQuote,
                    config.NextShareListingId),
            ];
        }

        /// <summary>
        /// Reprices an open secondary-market listing: delist_shares + relist_shares as ONE
        /// instruction list, so the caller frames them in a single transaction and the
        /// listing never sits unlisted between the two. The ShareListing account is read
        /// first - delist needs its rent_payer for the rent refund - and the relist takes
        /// the config's next_share_listing_id (monotonic, so the delisted id cannot
        /// collide with the new one).
        /// </summary>
        public static async Task<List<TransactionInstruction>> DelistAndRelistAsync(
            string address,
            SolanaCluster cluster,
            ulong shareListingId,
            ulong assetId,
            ulong newSharePriceQuote,
            uint newAmount,
            CancellationToken token = default)
        {
            ValidateRelistArgs(newAmount, newSharePriceQuote);

            var programs = XcavateProgramAddresses.Require(cluster);

            var shareListing = await XcavateShareListingModel.GetShareListingAsync(cluster, shareListingId, token)
                .ConfigureAwait(false);

            if (shareListing.AssetId != assetId)
            {
                throw new InvalidOperationException(
                    $"Share listing {shareListingId} is for asset {shareListing.AssetId}, not {assetId}.");
            }

            if (shareListing.Seller.Key != address)
            {
                throw new InvalidOperationException(
                    $"Share listing {shareListingId} belongs to a different seller.");
            }

            var config = await XcavateMarketplaceConfigModel.GetConfigAsync(cluster, token).ConfigureAwait(false);

            var seller = new PublicKey(address);

            return
            [
                XcavateMarketplaceProgram.DelistShares(
                    programs,
                    seller,
                    shareListing.RentPayer,
                    shareListingId,
                    assetId),
                XcavateMarketplaceProgram.RelistShares(
                    programs,
                    seller,
                    payer: config.RentCollector,
                    assetId,
                    newAmount,
                    newSharePriceQuote,
                    config.NextShareListingId),
            ];
        }

        /// <summary>
        /// buy_relisted_shares: buys <paramref name="amount"/> shares off the open
        /// secondary-market listing <paramref name="shareListingId"/>. Seller, ask price
        /// and rent payer come from the ShareListing account itself (fresh, so the cap
        /// reflects the listing as it is now); the payment mint is
        /// <paramref name="paymentMintPreference"/> when given - and must be one the
        /// config accepts - else the first whitelisted-and-accepted mint, exactly how the
        /// primary purchase picks. The buyer pays exactly share_price x amount (the fee
        /// comes out of the seller's proceeds), scaled to the payment mint's decimals by
        /// decimal count alone, plus one base unit of headroom for the program's floor
        /// division.
        /// </summary>
        public static async Task<List<TransactionInstruction>> BuyRelistedSharesAsync(
            string address,
            SolanaCluster cluster,
            ulong assetId,
            ulong shareListingId,
            uint amount,
            string? paymentMintPreference = null,
            CancellationToken token = default)
        {
            var programs = XcavateProgramAddresses.Require(cluster);

            var shareListing = await XcavateShareListingModel.GetShareListingAsync(cluster, shareListingId, token)
                .ConfigureAwait(false);

            if (shareListing.AssetId != assetId)
            {
                throw new InvalidOperationException(
                    $"Share listing {shareListingId} is for asset {shareListing.AssetId}, not {assetId}.");
            }

            if (amount == 0 || amount > shareListing.Amount)
            {
                throw new InvalidOperationException(
                    $"Share listing {shareListingId} has {shareListing.Amount} shares for sale; cannot buy {amount}.");
            }

            if (shareListing.Seller.Key == address)
            {
                throw new InvalidOperationException("A wallet cannot buy its own share listing.");
            }

            var config = await XcavateMarketplaceConfigModel.GetConfigAsync(cluster, token).ConfigureAwait(false);

            var paymentMint = paymentMintPreference is not null
                ? new PublicKey(paymentMintPreference)
                : PickPaymentMint(cluster, [.. config.AcceptedPaymentMints.Select(mint => mint.Key)]);

            if (!config.AcceptedPaymentMints.Any(mint => mint.Key == paymentMint.Key))
            {
                throw new InvalidOperationException(
                    $"The marketplace config does not accept {paymentMint.Key} as a payment mint.");
            }

            var (paymentTokenProgram, decimals) = await ResolveMintAsync(cluster, paymentMint, token).ConfigureAwait(false);

            var buyer = new PublicKey(address);
            var buyerPaymentAccount = SolanaAssociatedTokenAccount.Derive(buyer, paymentMint, paymentTokenProgram);

            // Fail here with the real reason rather than on chain with a raw account error.
            if (await SolanaRpcModel.GetAccountInfoAsync(cluster, buyerPaymentAccount.Key, token).ConfigureAwait(false) is null)
            {
                throw new InvalidOperationException(
                    $"This wallet holds no {DescribeMint(cluster, paymentMint)} to pay with.");
            }

            var funds = (BigInteger)shareListing.SharePrice * amount;

            if (funds > ulong.MaxValue)
            {
                throw new OverflowException("The total cost does not fit the program's u64 argument.");
            }

            // One extra base unit covers the program's floor division when the price
            // scale converts down to fewer mint decimals.
            var maxTotalCost = ScaleQuoteToMint((ulong)funds, decimals) + 1;

            return
            [
                XcavateMarketplaceProgram.BuyRelistedShares(
                    programs,
                    buyer,
                    payer: config.RentCollector,
                    assetId,
                    shareListingId,
                    amount,
                    maxTotalCost,
                    seller: shareListing.Seller,
                    rentPayer: shareListing.RentPayer,
                    paymentMint,
                    buyerPaymentAccount,
                    treasury: config.Treasury,
                    paymentTokenProgram),
            ];
        }

        /// <summary>
        /// The guards every relist path shares: a nonzero amount, an asking price the
        /// program accepts (>= <see cref="MinSharePriceQuote"/>), and a total that fits
        /// the program's u64 arithmetic.
        /// </summary>
        private static void ValidateRelistArgs(uint amount, ulong sharePriceQuote)
        {
            if (amount == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Cannot relist zero shares.");
            }

            if (sharePriceQuote < MinSharePriceQuote)
            {
                throw new ArgumentOutOfRangeException(nameof(sharePriceQuote), sharePriceQuote,
                    $"The share price must be at least {MinSharePriceQuote} quote units (9-decimal scale).");
            }

            if ((BigInteger)sharePriceQuote * amount > ulong.MaxValue)
            {
                throw new OverflowException("The relist total (share price x amount) does not fit the program's u64.");
            }
        }

        /// <summary>
        /// A human-denominated price per share as the program's 9-decimal quote units.
        /// Rejects non-positive prices, prices with more than nine decimal places (a
        /// silent truncation would misquote the listing) and prices that overflow u64.
        /// </summary>
        public static ulong QuoteFromDecimal(decimal price)
        {
            if (price <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(price), price, "The share price must be positive.");
            }

            // Guard before multiplying: a decimal this large overflows decimal itself at
            // the 9-decimal scaling, let alone u64.
            if (price > ulong.MaxValue / 1_000_000_000m)
            {
                throw new ArgumentOutOfRangeException(nameof(price), price,
                    "The share price does not fit the program's u64 argument.");
            }

            var quote = price * 1_000_000_000m;

            if (quote != decimal.Floor(quote))
            {
                throw new ArgumentOutOfRangeException(nameof(price), price,
                    "The share price has more than nine decimal places.");
            }

            return (ulong)quote;
        }

        /// <summary>
        /// Rescales a quote from the program's 9-decimal price scale to the payment
        /// mint's base units by decimal count alone, rounding DOWN (the program's own
        /// floor semantics) - callers capping a cost add their own headroom. This is the
        /// relisted-shares counterpart to <see cref="ScaleToMintDecimals"/>, which rounds
        /// up for the primary purchase's conservative cap.
        /// </summary>
        public static ulong ScaleQuoteToMint(ulong quote, int mintDecimals)
        {
            if (mintDecimals < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(mintDecimals), mintDecimals,
                    "A mint cannot have a negative decimal count.");
            }

            var shift = mintDecimals - XcavateMarketplaceIndexerModel.SharePriceDecimals;

            if (shift == 0)
            {
                return quote;
            }

            if (shift > 0)
            {
                var scaled = (BigInteger)quote * BigInteger.Pow(10, shift);

                return scaled <= ulong.MaxValue
                    ? (ulong)scaled
                    : throw new OverflowException("The scaled quote does not fit the program's u64 argument.");
            }

            var divisor = (ulong)BigInteger.Pow(10, -shift);

            return quote / divisor;
        }

        /// <summary>
        /// The marketplace program's config row (the rent collector's home), shared with
        /// the property program's calls model: its votes pin their payer the same way.
        /// </summary>
        public static Task<IMarketplaceConfigInfo_MarketplaceConfig> GetMarketplaceConfigAsync(
            SolanaCluster cluster,
            CancellationToken token = default)
        {
            var client = XcavateWhitelistIndexer.GetClient(cluster);
            return GetConfigAsync(cluster, client, token);
        }

        /// <summary>
        /// The program pins one payment mint and account per position ("later buys must
        /// use the same one, so every refund is a single transfer"), so an existing
        /// position dictates the route; only a first purchase gets to pick a mint.
        /// </summary>
        private static async Task<PaymentRoute> ResolvePaymentAsync(
            SolanaCluster cluster,
            IXcavateDevnetIndexerClient client,
            string investor,
            long listingId,
            IMarketplaceConfigInfo_MarketplaceConfig config,
            CancellationToken token)
        {
            var position = await FindPositionAsync(client, listingId, investor, token).ConfigureAwait(false);

            if (position is not null)
            {
                var recordedMint = new PublicKey(position.PaymentMint);
                var (recordedTokenProgram, recordedDecimals) = await ResolveMintAsync(cluster, recordedMint, token).ConfigureAwait(false);

                return new PaymentRoute(recordedMint, new PublicKey(position.PaymentAccount), recordedTokenProgram, recordedDecimals);
            }

            var mint = PickPaymentMint(cluster, config.AcceptedPaymentMints);
            var (tokenProgram, decimals) = await ResolveMintAsync(cluster, mint, token).ConfigureAwait(false);

            var investorKey = new PublicKey(investor);
            var paymentAccount = SolanaAssociatedTokenAccount.Derive(investorKey, mint, tokenProgram);

            // A first reservation binds this account, so it has to exist and hold funds -
            // fail here with the real reason rather than on chain with a raw account error.
            var accountInfo = await SolanaRpcModel.GetAccountInfoAsync(cluster, paymentAccount.Key, token).ConfigureAwait(false);

            if (accountInfo is null)
            {
                throw new InvalidOperationException(
                    $"This wallet holds no {DescribeMint(cluster, mint)} to pay with.");
            }

            return new PaymentRoute(mint, paymentAccount, tokenProgram, decimals);
        }

        /// <summary>
        /// claim_shares for listing <paramref name="listingId"/>, paying from the exact
        /// account the investor's reservation was made with (the reservation PDA is keyed
        /// by it).
        /// </summary>
        public static async Task<List<TransactionInstruction>> ClaimSharesAsync(
            SolanaCluster cluster,
            string investor,
            long listingId,
            CancellationToken token = default)
        {
            var programs = XcavateProgramAddresses.Require(cluster);
            var client = XcavateWhitelistIndexer.GetClient(cluster);

            var position = await GetPositionAsync(client, listingId, investor, token).ConfigureAwait(false);
            var config = await GetConfigAsync(cluster, client, token).ConfigureAwait(false);

            var paymentMint = new PublicKey(position.PaymentMint);
            var (paymentTokenProgram, _) = await ResolveMintAsync(cluster, paymentMint, token).ConfigureAwait(false);

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
            SolanaCluster cluster,
            string confirmer,
            long listingId,
            CancellationToken token = default)
        {
            var programs = XcavateProgramAddresses.Require(cluster);

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
            SolanaCluster cluster,
            string investor,
            long listingId,
            CancellationToken token = default)
        {
            var programs = XcavateProgramAddresses.Require(cluster);
            var client = XcavateWhitelistIndexer.GetClient(cluster);

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
            SolanaCluster cluster, string investor, long listingId, CancellationToken token = default) =>
            WithdrawAsync(XcavateMarketplaceProgram.WithdrawExpired, cluster, investor, listingId, token);

        /// <summary>withdraw_cancelled - refund from a cancelled listing (the only status it accepts).</summary>
        public static Task<List<TransactionInstruction>> WithdrawCancelledAsync(
            SolanaCluster cluster, string investor, long listingId, CancellationToken token = default) =>
            WithdrawAsync(XcavateMarketplaceProgram.WithdrawCancelled, cluster, investor, listingId, token);

        /// <summary>withdraw_legal_process_expired - refund after the legal phase blew its deadline.</summary>
        public static Task<List<TransactionInstruction>> WithdrawLegalProcessExpiredAsync(
            SolanaCluster cluster, string investor, long listingId, CancellationToken token = default) =>
            WithdrawAsync(XcavateMarketplaceProgram.WithdrawLegalProcessExpired, cluster, investor, listingId, token);

        private static async Task<List<TransactionInstruction>> WithdrawAsync(
            Func<XcavateProgramSet, PublicKey, ulong, PublicKey, PublicKey, PublicKey, PublicKey, TransactionInstruction> build,
            SolanaCluster cluster,
            string investor,
            long listingId,
            CancellationToken token)
        {
            var programs = XcavateProgramAddresses.Require(cluster);
            var client = XcavateWhitelistIndexer.GetClient(cluster);

            var position = await GetPositionAsync(client, listingId, investor, token).ConfigureAwait(false);
            var config = await GetConfigAsync(cluster, client, token).ConfigureAwait(false);

            var paymentMint = new PublicKey(position.PaymentMint);
            var (paymentTokenProgram, _) = await ResolveMintAsync(cluster, paymentMint, token).ConfigureAwait(false);
            var investorKey = new PublicKey(investor);

            // The refund handlers deliberately do not pin the destination to the recorded
            // payment account (it may be closed by now); the investor's associated
            // account for the mint is the canonical place to refund into.
            var refundAccount = SolanaAssociatedTokenAccount.Derive(investorKey, paymentMint, paymentTokenProgram);

            var instructions = new List<TransactionInstruction>();

            // The withdraw instructions carry neither the associated-token nor the system
            // program, so unlike buy/claim the marketplace cannot create missing token
            // accounts itself. An investor who closed theirs (rent cleanup while a dead
            // listing lingered) would be unrefundable, so any missing account is created
            // first - idempotently, in case it lands twice.
            var shareMint = XcavateMarketplaceProgram.DeriveShareMint(programs, (ulong)listingId);
            var shareTokenProgram = new PublicKey(SolanaTokenProgram.Token2022);
            var shareAccount = SolanaAssociatedTokenAccount.Derive(investorKey, shareMint, shareTokenProgram);

            if (await SolanaRpcModel.GetAccountInfoAsync(cluster, refundAccount.Key, token).ConfigureAwait(false) is null)
            {
                instructions.Add(SolanaAssociatedTokenAccount.CreateIdempotentInstruction(
                    investorKey, investorKey, paymentMint, paymentTokenProgram));
            }

            if (await SolanaRpcModel.GetAccountInfoAsync(cluster, shareAccount.Key, token).ConfigureAwait(false) is null)
            {
                instructions.Add(SolanaAssociatedTokenAccount.CreateIdempotentInstruction(
                    investorKey, investorKey, shareMint, shareTokenProgram));
            }

            instructions.Add(build(
                programs,
                investorKey,
                (ulong)listingId,
                new PublicKey(config.RentCollector),
                paymentMint,
                refundAccount,
                paymentTokenProgram));

            return instructions;
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
        public static PublicKey PickPaymentMint(SolanaCluster cluster, string acceptedPaymentMintsJson) =>
            PickPaymentMint(cluster, JsonSerializer.Deserialize<List<string>>(acceptedPaymentMintsJson) ?? []);

        /// <summary>
        /// The same pick over an already-parsed mint list - what the RPC-read config
        /// (<see cref="XcavateMarketplaceConfigModel"/>) hands the secondary-market calls.
        /// </summary>
        public static PublicKey PickPaymentMint(SolanaCluster cluster, IReadOnlyList<string> acceptedPaymentMints)
        {
            if (acceptedPaymentMints.Count == 0)
            {
                throw new InvalidOperationException("The marketplace config accepts no payment mints.");
            }

            var known = SolanaTokenWhitelist.ForCluster(cluster);

            var mint = acceptedPaymentMints.FirstOrDefault(candidate => known.Any(entry => entry.Mint == candidate))
                ?? acceptedPaymentMints[0];

            return new PublicKey(mint);
        }

        /// <summary>
        /// The token program owning <paramref name="mint"/> (classic or Token-2022) and
        /// its decimals - from the app's token whitelist when the mint is configured
        /// there, else from the mint account on chain (its owner, and the decimals byte
        /// at offset 44 of the mint layout).
        /// </summary>
        private static async Task<(PublicKey TokenProgram, int Decimals)> ResolveMintAsync(
            SolanaCluster cluster,
            PublicKey mint, CancellationToken token)
        {
            var entry = SolanaTokenWhitelist.ForCluster(cluster)
                .FirstOrDefault(entry => entry.Mint == mint.Key);

            if (entry is not null)
            {
                return (new PublicKey(entry.ProgramId), entry.Decimals);
            }

            var accountInfo = await SolanaRpcModel.GetAccountInfoAsync(cluster, mint.Key, token).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Payment mint {mint.Key} does not exist on {cluster.GetName()}.");

            var data = Convert.FromBase64String(accountInfo.Data[0]);

            if (data.Length < 45)
            {
                throw new InvalidOperationException($"{mint.Key} is not a token mint.");
            }

            return (new PublicKey(accountInfo.Owner), data[44]);
        }

        /// <summary>
        /// Rescales a cost computed at the listing's price scale
        /// (<see cref="XcavateMarketplaceIndexerModel.SharePriceDecimals"/>) to the chosen
        /// payment mint's base units - the accepted mints do not all share one decimal
        /// count, and the program converts prices between them by decimal count alone.
        /// Rounds up when scaling down, so the cap never lands under the program's charge.
        /// </summary>
        public static ulong ScaleToMintDecimals(ulong totalAtPriceScale, int mintDecimals)
        {
            var shift = mintDecimals - XcavateMarketplaceIndexerModel.SharePriceDecimals;

            if (shift == 0)
            {
                return totalAtPriceScale;
            }

            if (shift > 0)
            {
                var scaled = (BigInteger)totalAtPriceScale * BigInteger.Pow(10, shift);

                return scaled <= ulong.MaxValue
                    ? (ulong)scaled
                    : throw new OverflowException("The total cost does not fit the program's u64 argument.");
            }

            var divisor = (ulong)BigInteger.Pow(10, -shift);

            return (totalAtPriceScale + divisor - 1) / divisor;
        }

        /// <summary>The mint's whitelist symbol when the app knows it, else its address.</summary>
        private static string DescribeMint(SolanaCluster cluster, PublicKey mint) =>
            SolanaTokenWhitelist.ForCluster(cluster)
                .FirstOrDefault(entry => entry.Mint == mint.Key)?.Symbol ?? mint.Key;

        private static async Task<IListingParts?> GetListingAsync(
            IXcavateDevnetIndexerClient client, long listingId, CancellationToken token)
        {
            var result = await client.MarketplaceListing
                .ExecuteAsync(listingId.ToString(CultureInfo.InvariantCulture), token)
                .ConfigureAwait(false);

            result.EnsureNoErrors();

            return result.Data?.Listings.Nodes.FirstOrDefault();
        }

        private static async Task<IMarketplaceInvestorPositions_InvestorPositions_Nodes?> FindPositionAsync(
            IXcavateDevnetIndexerClient client, long listingId, string investor, CancellationToken token)
        {
            var result = await client.MarketplaceInvestorPositions
                .ExecuteAsync(listingId.ToString(CultureInfo.InvariantCulture), investor, first: 1, offset: 0, token)
                .ConfigureAwait(false);

            result.EnsureNoErrors();

            return result.Data?.InvestorPositions.Nodes.FirstOrDefault();
        }

        private static async Task<IMarketplaceInvestorPositions_InvestorPositions_Nodes> GetPositionAsync(
            IXcavateDevnetIndexerClient client, long listingId, string investor, CancellationToken token) =>
            await FindPositionAsync(client, listingId, investor, token).ConfigureAwait(false)
                ?? throw new InvalidOperationException(
                    $"This wallet has no open position on listing {listingId}.");

        private static async Task<IMarketplaceConfigInfo_MarketplaceConfig> GetConfigAsync(
            SolanaCluster cluster,
            IXcavateDevnetIndexerClient client,
            CancellationToken token)
        {
            var result = await client.MarketplaceConfigInfo
                .ExecuteAsync(token)
                .ConfigureAwait(false);

            result.EnsureNoErrors();

            return result.Data?.MarketplaceConfig
                ?? throw new InvalidOperationException(
                    $"The marketplace is not initialized on {cluster.GetName()}.");
        }

        private static ulong ParseUInt64(string? value) =>
            ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }
}
