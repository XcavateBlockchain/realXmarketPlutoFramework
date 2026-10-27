using PlutoFrameworkCore.Solana;
using Solnet.Rpc.Models;
using Solnet.Wallet;
using StrawberryShake;
using System.Buffers.Binary;
using System.Globalization;
using System.Numerics;
using System.Text;
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
        /// Lamports the developer's wallet must hold to open a listing: the listing and
        /// property accounts' rent (2,291,080 + 3,662,880, measured from a devnet
        /// list_property), the share mint / property vault / vault share account's
        /// (3,398,520, from a devnet init_property_assets) and the fee - 9,357,480 exact,
        /// rounded up for margin. The chain reports a shortfall as a bare
        /// "custom program error: 0x1" from a CPI; the pre-flight check says it in words.
        /// </summary>
        private const ulong ListRentBudgetLamports = 10_000_000;

        /// <summary>
        /// The same for init_property_assets alone (the Complete Listing recovery path):
        /// 3,398,520 of rent measured on devnet plus the fee, rounded up.
        /// </summary>
        private const ulong AssetSetupRentBudgetLamports = 3_600_000;

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
        /// The candidacy a revote moves power away from: the voter's recorded choice when
        /// it differs from <paramref name="candidate"/>, else null. On-chain,
        /// previous_candidacy must be Some exactly when the vote record backs a DIFFERENT
        /// candidacy (needs_previous == record.power &gt; 0 &amp;&amp; record.choice !=
        /// choice) - a same-target revote passing the account fails CandidacyMismatch.
        /// </summary>
        public static PublicKey? ResolvePreviousCandidate(string? currentVoteChoice, string candidate) =>
            currentVoteChoice is not null && currentVoteChoice != candidate
                ? new PublicKey(currentVoteChoice)
                : null;

        /// <summary>
        /// The same pick over a voter's full vote rows: the indexer schema exposes no
        /// round filter, so <paramref name="votes"/> spans every round - but only the
        /// round BEING VOTED keys the vote record this transaction revises. A stale
        /// round's row must never leak its candidacy into the account list.
        /// </summary>
        public static PublicKey? ResolvePreviousCandidate(
            long round,
            IReadOnlyList<XcavateMarketplaceIndexerModel.AgentElectionState.VoteRow> votes,
            string candidate) =>
            ResolvePreviousCandidate(
                votes.Where(vote => vote.Round == round)
                    .Select(vote => (string?)vote.Candidate)
                    .FirstOrDefault(),
                candidate);

        /// <summary>
        /// The pending-finalization arrow a crank wrapper was invoked for, or a
        /// user-presentable rejection when the indexer sees nothing to settle (the window
        /// is still open, or someone else already finalized it).
        /// </summary>
        public static T RequireFinalization<T>(T? finalization, string what) where T : class =>
            finalization ?? throw new InvalidOperationException(
                $"There is no {what} to finalize: its window is still open or it was already settled.");

        /// <summary>
        /// vote_on_spv_lawyer for <paramref name="amount"/> shares: the investor's
        /// share-weighted vote backing one candidacy in the listing's SPV-lawyer election -
        /// the pre-settlement vote the property page surfaces as the "terms" approval. The
        /// vote record's rent goes through the config's rent collector like every purchase
        /// (the deployed program accepts any paying wallet, so the two-signer flow the
        /// transaction model already runs for purchases covers this unchanged);
        /// <paramref name="round"/> is the election's current round, read from the
        /// indexer's listing row first — a stale round fails the record PDA's seed check.
        /// Revotes resolve the previous candidacy from the indexer's lawyer votes of that
        /// same round: a switch to a different candidacy carries it along, a same-target
        /// revote or a first vote passes None.
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

            var votes = await GetLawyerVotesAsync(client, listingId, voter, token).ConfigureAwait(false);

            return
            [
                XcavateMarketplaceProgram.VoteOnSpvLawyer(
                    programs,
                    new PublicKey(voter),
                    new PublicKey(config.RentCollector),
                    (ulong)listingId,
                    round,
                    new PublicKey(candidate),
                    ResolvePreviousCandidate((long)round, votes, candidate),
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
        /// The costs counterpart of <see cref="QuoteFromDecimal"/>: a lawyer's legal-costs
        /// quote may legitimately be zero (claim_spv_case only caps it at the collected
        /// fee quote), while a share price may not. Everything else - the nine-decimal
        /// scale, the decimal-places and u64-overflow guards - is shared.
        /// </summary>
        public static ulong QuoteCostsFromDecimal(decimal costs) =>
            costs == 0 ? 0UL : QuoteFromDecimal(costs);

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
        /// assign_developer_lawyer for listing <paramref name="listingId"/>, called by the
        /// listing's developer to engage <paramref name="lawyer"/> for the developer side
        /// of the legal phase. Pure - every account is derived; the program enforces the
        /// developer's ownership of the listing, the vacant seat, the lawyer's region and
        /// compliance.
        /// </summary>
        public static Task<List<TransactionInstruction>> AssignDeveloperLawyerAsync(
            SolanaCluster cluster,
            string developer,
            long listingId,
            string lawyer,
            CancellationToken token = default)
        {
            var programs = XcavateProgramAddresses.Require(cluster);

            return Task.FromResult<List<TransactionInstruction>>(
            [
                XcavateMarketplaceProgram.AssignDeveloperLawyer(
                    programs, new PublicKey(developer), new PublicKey(lawyer), (ulong)listingId),
            ]);
        }

        /// <summary>
        /// list_property for the developer's verified property: opens the sale of
        /// <paramref name="numberOfShares"/> shares at <paramref name="propertyPrice"/> /
        /// shares each (9-decimal price scale, via <see cref="QuoteFromDecimal"/>) in the
        /// on-chain location the developer picked (<paramref name="regionId"/> +
        /// <paramref name="postcode"/> - the picker only offers registered locations
        /// (<see cref="GetRegisteredLocationsAsync"/>), so nothing is resolved here.
        /// Returns the instruction and the listing id it initializes under - the config's
        /// next_listing_id, read ON CHAIN (<see cref="XcavateMarketplaceConfigModel"/>),
        /// because the Listing/Property PDAs are seeded by that counter and the indexer
        /// can lag it. The deposit cap is the config's listing_deposit, fresh from the
        /// same read, so the charge cannot slip between builds; the tax is left to the
        /// buyer side (tax_paid_by_developer = false), matching the web flow.
        /// <para>
        /// list_property alone leaves the listing PENDING_ASSETS - invisible to the
        /// marketplace feed - until init_property_assets creates the share mint and
        /// property vault. Both are the developer's alone to sign, so the second
        /// instruction is bundled into the same transaction: one atomic submission opens
        /// the listing fully, and a failed half leaves nothing behind. The chain stores
        /// <paramref name="name"/> and <paramref name="uri"/> as the share mint's
        /// Metaplex metadata (capped at 40/200 bytes, truncated here to fit); an empty
        /// uri is rejected - the property needs a published metadata document before it can be listed.
        /// </para>
        /// </summary>
        public static async Task<(List<TransactionInstruction> Instructions, ulong ListingId)> ListPropertyAsync(
            SolanaCluster cluster,
            string developer,
            ushort regionId,
            string postcode,
            decimal propertyPrice,
            long numberOfShares,
            string name,
            string uri,
            CancellationToken token = default)
        {
            var programs = XcavateProgramAddresses.Require(cluster);

            var config = await XcavateMarketplaceConfigModel.GetConfigAsync(cluster, token).ConfigureAwait(false);

            var postcodeBytes = NormalizePostcode(postcode);

            if (numberOfShares < config.MinPropertyShares || numberOfShares > config.MaxPropertyShares)
            {
                throw new InvalidOperationException(
                    $"The share count must be between {config.MinPropertyShares} and {config.MaxPropertyShares} - " +
                    $"this property declares {numberOfShares}.");
            }

            var sharePrice = QuoteFromDecimal(propertyPrice / numberOfShares);

            if (sharePrice < MinSharePriceQuote)
            {
                throw new InvalidOperationException(
                    $"The share price must be at least {MinSharePriceQuote} quote units (9-decimal scale).");
            }

            var developerKey = new PublicKey(developer);

            await EnsureRentBudgetAsync(
                cluster, developer, ListRentBudgetLamports,
                "Listing a property opens its on-chain accounts", token).ConfigureAwait(false);

            var (xcavTokenProgram, xcavDecimals) = await ResolveMintAsync(cluster, config.XcavMint, token).ConfigureAwait(false);

            var developerToken = SolanaAssociatedTokenAccount.Derive(developerKey, config.XcavMint, xcavTokenProgram);

            // Fail here with the real reason rather than on chain with a raw account error:
            // list_property PULLS the deposit from the developer's XCAV account - it does not
            // create one - so a wallet without XCAV dies on chain with Anchor's
            // AccountNotInitialized on developer_token.
            var depositDisplay = SolanaAmount.FromBaseUnits(config.ListingDeposit.ToString(CultureInfo.InvariantCulture), xcavDecimals);

            var developerTokenInfo = await SolanaRpcModel.GetAccountInfoAsync(cluster, developerToken.Key, token).ConfigureAwait(false);

            if (developerTokenInfo is null)
            {
                throw new InvalidOperationException(
                    $"This wallet holds no XCAV. Listing takes a {depositDisplay} XCAV deposit - fund the wallet first.");
            }

            // SPL token account layout: the amount sits at offset 64. Anything that is not
            // a parseable token account lets the chain decide - the deposit check is a
            // diagnosis, not a gate.
            var developerTokenData = Convert.FromBase64String(developerTokenInfo.Data[0]);

            if (developerTokenData.Length >= 72)
            {
                var developerXcav = BinaryPrimitives.ReadUInt64LittleEndian(developerTokenData.AsSpan(64));

                if (developerXcav < config.ListingDeposit)
                {
                    throw new InvalidOperationException(
                        $"The listing deposit is {depositDisplay} XCAV; this wallet holds " +
                        $"{SolanaAmount.FromBaseUnits(developerXcav.ToString(CultureInfo.InvariantCulture), xcavDecimals)} XCAV.");
                }
            }

            var listingId = config.NextListingId;

            var (assetName, assetUri) = AssetMetadata(name, uri);

            return
            (
                [
                    XcavateMarketplaceProgram.ListProperty(
                        programs,
                        developerKey,
                        config.XcavMint,
                        developerToken,
                        xcavTokenProgram,
                        regionId,
                        postcodeBytes,
                        sharePrice,
                        (uint)numberOfShares,
                        taxPaidByDeveloper: false,
                        maxDeposit: config.ListingDeposit,
                        listingId),
                    XcavateMarketplaceProgram.InitPropertyAssets(
                        programs,
                        developerKey,
                        listingId,
                        assetName,
                        assetUri),
                ],
                listingId
            );
        }

        /// <summary>
        /// init_property_assets on its own, for a listing whose list_property landed
        /// without it (a PENDING_ASSETS listing - the marketplace feed never shows one,
        /// and the sale cannot open until the share mint exists). Normally bundled into
        /// <see cref="ListPropertyAsync"/>'s transaction; this is the recovery path. Pure
        /// - every account is derived; the program enforces the developer's role, its
        /// ownership of the listing and the PENDING_ASSETS state. The metadata caps are
        /// the same as the bundled path's.
        /// </summary>
        public static async Task<List<TransactionInstruction>> InitPropertyAssetsAsync(
            SolanaCluster cluster,
            string developer,
            long listingId,
            string name,
            string uri,
            CancellationToken token = default)
        {
            var programs = XcavateProgramAddresses.Require(cluster);

            await EnsureRentBudgetAsync(
                cluster, developer, AssetSetupRentBudgetLamports,
                "Completing the listing creates the share mint and vault accounts", token).ConfigureAwait(false);

            var (assetName, assetUri) = AssetMetadata(name, uri);

            return
            [
                XcavateMarketplaceProgram.InitPropertyAssets(
                    programs, new PublicKey(developer), (ulong)listingId, assetName, assetUri),
            ];
        }

        /// <summary>
        /// Fails the build with the real reason when the wallet cannot pay the new
        /// accounts' rent: the chain would otherwise say it as a bare
        /// "custom program error: 0x1" bubbled up from a system-program CPI.
        /// <paramref name="budgetLamports"/> covers the accounts' rent and the fee.
        /// </summary>
        private static async Task EnsureRentBudgetAsync(
            SolanaCluster cluster,
            string developer,
            ulong budgetLamports,
            string what,
            CancellationToken token)
        {
            var lamports = await SolanaRpcModel.GetLamportBalanceAsync(cluster, developer, token).ConfigureAwait(false);

            if (lamports >= budgetLamports)
            {
                return;
            }

            static string Sol(ulong lamports) =>
                SolanaAmount.FromBaseUnits(lamports.ToString(CultureInfo.InvariantCulture), 9)
                    .ToString(CultureInfo.InvariantCulture);

            throw new InvalidOperationException(
                $"{what} - about {Sol(budgetLamports)} SOL of rent and fees; " +
                $"this wallet holds {Sol(lamports)} SOL. Top up the wallet and try again.");
        }

        /// <summary>
        /// The share mint's on-chain metadata, fitted to the program's bounds: name at 40
        /// and uri at 200 bytes, both non-empty - truncate to fit and fail here with the
        /// reason when nothing is left.
        /// </summary>
        private static (string Name, string Uri) AssetMetadata(string name, string uri)
        {
            var assetName = TruncateToBytes(name.Trim(), 40);
            var assetUri = TruncateToBytes(uri.Trim(), 200);

            if (assetName.Length == 0)
            {
                throw new InvalidOperationException("The property needs a name before it can be listed.");
            }

            if (assetUri.Length == 0)
            {
                throw new InvalidOperationException("The property needs a metadata document before it can be listed.");
            }

            return (assetName, assetUri);
        }

        /// <summary>
        /// Trims <paramref name="value"/> to at most <paramref name="maxBytes"/> of UTF-8,
        /// cutting on a character boundary so no multi-byte sequence is split.
        /// </summary>
        private static string TruncateToBytes(string value, int maxBytes)
        {
            if (Encoding.UTF8.GetByteCount(value) <= maxBytes)
            {
                return value;
            }

            var bytes = Encoding.UTF8.GetBytes(value);
            var cut = maxBytes;

            // A continuation byte (10xxxxxx) means the cut falls inside a multi-byte
            // character - step back to its lead byte.
            while (cut > 0 && (bytes[cut] & 0xC0) == 0x80)
            {
                cut--;
            }

            return Encoding.UTF8.GetString(bytes, 0, cut);
        }

        /// <summary>
        /// The postcode in the form the regions program seeds Location PDAs with: spaces
        /// out, upper case, UTF-8 bytes. The program caps the postcode at 10 bytes, so a
        /// longer one can never have been registered - reject it here with the reason.
        /// </summary>
        private static byte[] NormalizePostcode(string postcode)
        {
            var normalized = postcode.Replace(" ", string.Empty).ToUpperInvariant();

            if (normalized.Length == 0)
            {
                throw new InvalidOperationException("The property has no postcode to list under.");
            }

            var bytes = Encoding.UTF8.GetBytes(normalized);

            if (bytes.Length > 10)
            {
                throw new InvalidOperationException(
                    $"The postcode '{postcode}' is longer than the 10 bytes the regions program accepts.");
            }

            return bytes;
        }

        /// <summary>
        /// Every location registered on chain, in the regions program's own postcode form -
        /// what the developer picks the listing's region/location from. Locations are few,
        /// so one page almost always answers; the loop covers the day they are not.
        /// </summary>
        public static async Task<IReadOnlyList<(ushort RegionId, string Postcode)>> GetRegisteredLocationsAsync(
            SolanaCluster cluster, CancellationToken token = default)
        {
            var client = XcavateWhitelistIndexer.GetClient(cluster);

            const int pageSize = 100;
            var found = new List<(ushort RegionId, string Postcode)>();

            for (var offset = 0; ; offset += pageSize)
            {
                var result = await client.Locations
                    .ExecuteAsync(first: pageSize, offset: offset, token)
                    .ConfigureAwait(false);

                result.EnsureNoErrors();

                var locations = result.Data?.Locations;

                if (locations is null)
                {
                    break;
                }

                foreach (var node in locations.Nodes)
                {
                    found.Add(((ushort)node.RegionId, node.Postcode));
                }

                if (offset + locations.Nodes.Count >= locations.TotalCount)
                {
                    break;
                }
            }

            return [.. found.OrderBy(location => location.Postcode, StringComparer.Ordinal)];
        }

        public static IReadOnlyList<ushort> NormalizeRegionIds(IEnumerable<int> regionIds)
        {
            return [.. regionIds
                .Select(regionId => regionId is < 0 or > ushort.MaxValue
                    ? throw new InvalidOperationException($"Region id {regionId} is outside the supported u16 range.")
                    : (ushort)regionId)
                .Distinct()
                .Order()];
        }

        public static async Task<IReadOnlyList<ushort>> GetActiveRegionIdsAsync(
            SolanaCluster cluster, CancellationToken token = default)
        {
            var client = XcavateWhitelistIndexer.GetClient(cluster);

            const int pageSize = 100;
            var found = new List<int>();

            for (var offset = 0; ; offset += pageSize)
            {
                var result = await client.ActiveRegions
                    .ExecuteAsync(first: pageSize, offset: offset, token)
                    .ConfigureAwait(false);

                result.EnsureNoErrors();

                var regions = result.Data?.Regions;

                if (regions is null)
                {
                    break;
                }

                found.AddRange(regions.Nodes.Select(node => node.RegionId));

                if (offset + regions.Nodes.Count >= regions.TotalCount)
                {
                    break;
                }
            }

            return NormalizeRegionIds(found);
        }

        public static async Task<List<TransactionInstruction>> RegisterLawyerAsync(
            SolanaCluster cluster,
            string lawyer,
            ushort regionId,
            CancellationToken token = default)
        {
            var programs = XcavateProgramAddresses.Require(cluster);
            var config = await XcavateMarketplaceConfigModel.GetConfigAsync(cluster, token).ConfigureAwait(false);
            var lawyerKey = new PublicKey(lawyer);
            var (xcavTokenProgram, xcavDecimals) = await ResolveMintAsync(cluster, config.XcavMint, token).ConfigureAwait(false);
            var lawyerToken = SolanaAssociatedTokenAccount.Derive(lawyerKey, config.XcavMint, xcavTokenProgram);
            var depositDisplay = SolanaAmount.FromBaseUnits(
                config.LawyerDeposit.ToString(CultureInfo.InvariantCulture), xcavDecimals);
            var lawyerTokenInfo = await SolanaRpcModel.GetAccountInfoAsync(cluster, lawyerToken.Key, token).ConfigureAwait(false);

            if (lawyerTokenInfo is null)
            {
                throw new InvalidOperationException(
                    $"This wallet holds no XCAV. Lawyer registration takes a {depositDisplay} XCAV deposit - fund the wallet first.");
            }

            var lawyerTokenData = Convert.FromBase64String(lawyerTokenInfo.Data[0]);

            if (lawyerTokenData.Length >= 72)
            {
                var lawyerXcav = BinaryPrimitives.ReadUInt64LittleEndian(lawyerTokenData.AsSpan(64));

                if (lawyerXcav < config.LawyerDeposit)
                {
                    throw new InvalidOperationException(
                        $"The lawyer registration deposit is {depositDisplay} XCAV; this wallet holds " +
                        $"{SolanaAmount.FromBaseUnits(lawyerXcav.ToString(CultureInfo.InvariantCulture), xcavDecimals)} XCAV.");
                }
            }

            return
            [
                XcavateMarketplaceProgram.RegisterLawyer(
                    programs,
                    lawyerKey,
                    config.RentCollector,
                    config.XcavMint,
                    lawyerToken,
                    xcavTokenProgram,
                    regionId,
                    maxDeposit: config.LawyerDeposit),
            ];
        }

        /// <summary>
        /// claim_spv_case for listing <paramref name="listingId"/>: the calling lawyer's
        /// candidacy for the SPV-lawyer seat in election <paramref name="round"/>, quoting
        /// <paramref name="costsQuote"/> (9-decimal price scale) for the job. Pure - the
        /// round comes from the listing's election state (read it fresh: a stale round
        /// fails the candidacy PDA's seed check) and the lawyer wallet fronts the
        /// candidacy's rent itself, so nothing has to be looked up.
        /// </summary>
        public static Task<List<TransactionInstruction>> ClaimSpvCaseAsync(
            SolanaCluster cluster,
            string lawyer,
            long listingId,
            ulong round,
            ulong costsQuote,
            CancellationToken token = default)
        {
            var programs = XcavateProgramAddresses.Require(cluster);

            return Task.FromResult<List<TransactionInstruction>>(
            [
                XcavateMarketplaceProgram.ClaimSpvCase(
                    programs, new PublicKey(lawyer), (ulong)listingId, round, costsQuote),
            ]);
        }

        /// <summary>
        /// The transfer the send flow must clear: <paramref name="amount"/> fits the
        /// sender's transferable holding (amount minus listed minus the largest vote
        /// lock) and lands the receiver below the listing's ownership cap - both
        /// re-read fresh from the indexer at build time, so a holding that changed
        /// since the sheet opened rejects here with a user-presentable reason instead
        /// of failing on chain.
        /// </summary>
        public static void ValidateShareSend(uint amount, ulong senderTransferable, ulong receiverOwned, ulong maxShares)
        {
            if (amount == 0 || amount > senderTransferable)
            {
                throw new InvalidOperationException(
                    $"This wallet's transferable holding is {senderTransferable} shares; cannot send {amount}.");
            }

            // The program requires owned_after < max_shares - an exact hit rejects too.
            if (receiverOwned + amount >= maxShares)
            {
                throw new InvalidOperationException(
                    "The receiver already holds too much of this property; this send would take them over its ownership cap.");
            }
        }

        /// <summary>Shares free to sell or send, mirroring the on-chain transferable(): amount - max(vote locks) - listed, saturating.</summary>
        public static ulong TransferableShares(IMarketplaceShareHoldingOf_ShareHoldings_Nodes? holding)
        {
            if (holding is null)
            {
                return 0;
            }

            var amount = ParseUInt64(holding.Amount);
            var listed = ParseUInt64(holding.Listed);
            var locked = Math.Max(
                Math.Max(ParseUInt64(holding.LockLawyerElection), ParseUInt64(holding.LockAgentElection)),
                Math.Max(ParseUInt64(holding.LockProposal), ParseUInt64(holding.LockChallenge)));

            var free = amount > locked ? amount - locked : 0UL;
            return free > listed ? free - listed : 0UL;
        }

        /// <summary>
        /// send_property_shares for <paramref name="amount"/> shares of asset
        /// <paramref name="assetId"/>: a direct investor-to-investor transfer behind the
        /// send sheet - no money moves, so nothing has to be priced; the program settles
        /// both sides' property income to their checkpoints as part of the transfer.
        /// Rent for accounts created along the way (the receiver's holding is
        /// init_if_needed) is pinned to the config's rent collector exactly like on the
        /// purchase flow, so this rides the same two-signer path. The send is re-validated
        /// against fresh state (the sender's transferable holding, the receiver's
        /// ownership-cap headroom) so a stale sheet rejects here, where the popup can
        /// re-open the form, rather than on chain.
        /// </summary>
        public static async Task<List<TransactionInstruction>> SendPropertySharesAsync(
            SolanaCluster cluster,
            string sender,
            long assetId,
            string receiver,
            uint amount,
            CancellationToken token = default)
        {
            var programs = XcavateProgramAddresses.Require(cluster);
            var client = XcavateWhitelistIndexer.GetClient(cluster);

            var assetIdArg = assetId.ToString(CultureInfo.InvariantCulture);

            var listing = await GetListingAsync(client, assetId, token).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Listing {assetId} does not exist or is closed.");

            var senderHolding = await GetShareHoldingAsync(client, assetIdArg, sender, token).ConfigureAwait(false);
            var receiverHolding = await GetShareHoldingAsync(client, assetIdArg, receiver, token).ConfigureAwait(false);

            ValidateShareSend(
                amount,
                TransferableShares(senderHolding),
                ParseUInt64(receiverHolding?.Amount),
                (ulong)listing.MaxOwnershipBps * ParseUInt64(listing.PropertyAsset?.ShareAmount) / 10_000);

            var config = await GetConfigAsync(cluster, client, token).ConfigureAwait(false);

            return
            [
                XcavateMarketplaceProgram.SendPropertyShares(
                    programs,
                    new PublicKey(sender),
                    new PublicKey(config.RentCollector),
                    (ulong)assetId,
                    new PublicKey(receiver),
                    amount),
            ];
        }

        /// <summary>
        /// close_share_holding for asset <paramref name="assetId"/>: reclaims the emptied
        /// holding's rent (the program rejects HoldingNotEmpty - call only once nothing
        /// remains). Permissionless; the app signs with the owner themselves - the sole
        /// signer. The refund target is pinned on-chain to the config's rent collector
        /// (read fresh), the wallet that fronted the holding's rent.
        /// </summary>
        public static async Task<List<TransactionInstruction>> CloseShareHoldingAsync(
            SolanaCluster cluster,
            string owner,
            long assetId,
            CancellationToken token = default)
        {
            var programs = XcavateProgramAddresses.Require(cluster);
            var config = await GetMarketplaceConfigAsync(cluster, token).ConfigureAwait(false);

            var ownerKey = new PublicKey(owner);

            return
            [
                XcavateMarketplaceProgram.CloseShareHolding(
                    programs,
                    ownerKey,
                    new PublicKey(config.RentCollector),
                    (ulong)assetId,
                    ownerKey),
            ];
        }

        /// <summary>
        /// finalize_spv_election for listing <paramref name="listingId"/>: the
        /// permissionless crank settling the SPV-lawyer election once its window closed.
        /// The indexer answers the round, the plurality winner and the round's candidacy
        /// lawyers as one pending-finalization arrow; the candidacy accounts trail the
        /// instruction (the handler pins their count), so a listing whose election is
        /// still open or already settled rejects here. Single signer - the cranker; the
        /// listing id doubles as the property space's asset id on this deployment (the
        /// property-side arrows are unused here).
        /// </summary>
        public static async Task<List<TransactionInstruction>> FinalizeSpvElectionAsync(
            SolanaCluster cluster,
            string cranker,
            long listingId,
            CancellationToken token = default)
        {
            var programs = XcavateProgramAddresses.Require(cluster);

            var pending = await XcavateMarketplaceIndexerModel
                .GetPendingFinalizationsAsync(cluster, listingId, listingId, cranker, token)
                .ConfigureAwait(false);

            return [ BuildFinalizeSpvElection(programs, cranker, listingId, pending.SpvElection) ];
        }

        /// <summary>The finalize_spv_election instruction for a pending election arrow, or a user-presentable rejection when the arrow is absent.</summary>
        public static TransactionInstruction BuildFinalizeSpvElection(
            XcavateProgramSet programs,
            string cranker,
            long listingId,
            XcavateMarketplaceIndexerModel.PendingFinalizations.ElectionFinalization? finalization)
        {
            var arrow = RequireFinalization(finalization, "SPV-lawyer election finalization");

            return XcavateMarketplaceProgram.FinalizeSpvElection(
                programs,
                new PublicKey(cranker),
                (ulong)listingId,
                (ulong)arrow.Round,
                arrow.Winner is null ? null : new PublicKey(arrow.Winner),
                [.. arrow.Candidates.Select(candidate => new PublicKey(candidate))]);
        }

        /// <summary>
        /// resolve_silent_verdict for listing <paramref name="listingId"/>: the
        /// permissionless crank ruling on the developer's lawyer who let the review
        /// window run into its final stretch without a verdict. The indexer's pending
        /// finalizations say whether the on-chain windows line up (SPV ruled, developer
        /// silent, inside the final stretch) - outside them the crank would revert, so a
        /// miss rejects here. Single signer - the cranker.
        /// </summary>
        public static async Task<List<TransactionInstruction>> ResolveSilentVerdictAsync(
            SolanaCluster cluster,
            string cranker,
            long listingId,
            CancellationToken token = default)
        {
            var programs = XcavateProgramAddresses.Require(cluster);

            var pending = await XcavateMarketplaceIndexerModel
                .GetPendingFinalizationsAsync(cluster, listingId, listingId, cranker, token)
                .ConfigureAwait(false);

            return
            [
                BuildResolveSilentVerdict(programs, cranker, listingId, pending.SilentVerdictOverdue),
            ];
        }

        /// <summary>
        /// The resolve_silent_verdict instruction, or a user-presentable rejection when
        /// the indexer says the crank cannot land: the gate
        /// (<see cref="XcavateMarketplaceIndexerModel.PendingFinalizations.SilentVerdictOverdue"/>)
        /// is the whole decision - outside the closing stretch, or the lawyer already
        /// ruled, the instruction's own window constraints would revert.
        /// </summary>
        public static TransactionInstruction BuildResolveSilentVerdict(
            XcavateProgramSet programs,
            string cranker,
            long listingId,
            bool silentVerdictOverdue)
        {
            if (!silentVerdictOverdue)
            {
                throw new InvalidOperationException(
                    "The developer's lawyer can still rule - or already ruled - so there is no silent verdict to resolve.");
            }

            return XcavateMarketplaceProgram.ResolveSilentVerdict(
                programs,
                new PublicKey(cranker),
                (ulong)listingId);
        }

        /// <summary>
        /// execute_deal for listing <paramref name="listingId"/>: the permissionless
        /// settlement crank once both lawyers approved and the legal window is still
        /// open. The indexer answers the parties, the region and the collected payment
        /// mints (the handler walks one payout account group per mint, so nothing can be
        /// invented client-side); mint token programs come from the chain, the region's
        /// operator from the indexer. Single signer - the cranker.
        /// </summary>
        public static async Task<List<TransactionInstruction>> ExecuteDealAsync(
            SolanaCluster cluster,
            string cranker,
            long listingId,
            CancellationToken token = default)
        {
            var programs = XcavateProgramAddresses.Require(cluster);
            var client = XcavateWhitelistIndexer.GetClient(cluster);

            var pending = await XcavateMarketplaceIndexerModel
                .GetPendingFinalizationsAsync(cluster, listingId, listingId, cranker, token)
                .ConfigureAwait(false);

            var deal = RequireFinalization(pending.ExecuteDeal, "deal to settle");
            var config = pending.Config
                ?? throw new InvalidOperationException(
                    $"The marketplace is not initialized on {cluster.GetName()}.");

            var regionOwner = await GetRegionOwnerAsync(client, deal.RegionId, token).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Region {deal.RegionId} was not found in the indexer.");

            var (xcavTokenProgram, _) = await ResolveMintAsync(cluster, new PublicKey(deal.XcavMint), token)
                .ConfigureAwait(false);

            var tokenPrograms = new Dictionary<string, PublicKey>
            {
                [deal.XcavMint] = xcavTokenProgram,
            };
            foreach (var mint in deal.CollectedMints)
            {
                var (mintTokenProgram, _) = await ResolveMintAsync(cluster, new PublicKey(mint), token)
                    .ConfigureAwait(false);
                tokenPrograms[mint] = mintTokenProgram;
            }

            return
            [
                BuildExecuteDeal(programs, cranker, listingId, deal, regionOwner, config.Treasury, tokenPrograms),
            ];
        }

        /// <summary>
        /// The execute_deal instruction for a deal arrow: the parties, the region payee,
        /// the treasury and the collected-mint payout loop (one 7-account group per
        /// collected mint, in arrow order, each redeeming its own token program from
        /// <paramref name="tokenProgramByMint"/>). The payment-token-program slot is the
        /// FIRST collected mint's program - the program the handler consults against the
        /// payout groups; with nothing collected it is the XCAV mint's, an unused
        /// pass-through (settlement.rs:141 checks remaining == collected.len() * 7).
        /// </summary>
        public static TransactionInstruction BuildExecuteDeal(
            XcavateProgramSet programs,
            string cranker,
            long listingId,
            XcavateMarketplaceIndexerModel.PendingFinalizations.ExecuteDealFinalization finalization,
            string regionOwner,
            string treasury,
            IReadOnlyDictionary<string, PublicKey> tokenProgramByMint)
        {
            if (!tokenProgramByMint.TryGetValue(finalization.XcavMint, out var xcavTokenProgram))
            {
                throw new InvalidOperationException($"No token program resolved for the deal mint {finalization.XcavMint}.");
            }

            var collectedMints = new List<XcavateMarketplaceProgram.CollectedMint>();
            foreach (var mint in finalization.CollectedMints)
            {
                if (!tokenProgramByMint.TryGetValue(mint, out var tokenProgram))
                {
                    throw new InvalidOperationException($"No token program resolved for the deal mint {mint}.");
                }

                collectedMints.Add(new XcavateMarketplaceProgram.CollectedMint(new PublicKey(mint), tokenProgram));
            }

            // The payment-token-program account is only consulted against the collected
            // mints' own programs; with nothing collected it is an unused pass-through.
            var paymentTokenProgram = collectedMints.Count > 0 ? collectedMints[0].TokenProgram : xcavTokenProgram;

            return XcavateMarketplaceProgram.ExecuteDeal(
                programs,
                new PublicKey(cranker),
                (ulong)listingId,
                finalization.RegionId,
                new PublicKey(finalization.DeveloperLawyer),
                new PublicKey(finalization.SpvLawyer),
                new PublicKey(finalization.XcavMint),
                new PublicKey(finalization.Developer),
                xcavTokenProgram,
                paymentTokenProgram,
                new PublicKey(regionOwner),
                new PublicKey(treasury),
                collectedMints);
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

        /// <summary>
        /// withdraw_expired - refund after the listing expired unsold. A position that only
        /// ever reserved (nothing paid in, nothing to refund) takes the release_reservation
        /// sweep instead - see <see cref="WithdrawAsync"/>.
        /// </summary>
        public static Task<List<TransactionInstruction>> WithdrawExpiredAsync(
            SolanaCluster cluster, string investor, long listingId, CancellationToken token = default) =>
            WithdrawAsync(XcavateMarketplaceProgram.WithdrawExpired, cluster, investor, listingId, token);

        /// <summary>
        /// withdraw_cancelled - refund from a cancelled listing (the only status it accepts).
        /// Routes a reservation-only position to release_reservation, like the other withdraws.
        /// </summary>
        public static Task<List<TransactionInstruction>> WithdrawCancelledAsync(
            SolanaCluster cluster, string investor, long listingId, CancellationToken token = default) =>
            WithdrawAsync(XcavateMarketplaceProgram.WithdrawCancelled, cluster, investor, listingId, token);

        /// <summary>
        /// withdraw_legal_process_expired - refund after the legal phase blew its deadline.
        /// Routes a reservation-only position to release_reservation, like the other withdraws.
        /// </summary>
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

            var investorKey = new PublicKey(investor);

            // A position that only ever reserved has nothing to refund: the payment never
            // left the wallet, and the share holding the withdraw instructions deserialize
            // is created only when shares are delivered (claim or direct buy), so the
            // program refuses them with AccountNotInitialized (3012). The dead
            // reservation's exit is the release crank - permissionless once claiming is
            // impossible - which the investor signs here themselves.
            if (ParseUInt64(position.ShareAmount) == 0)
            {
                return
                [
                    XcavateMarketplaceProgram.ReleaseReservation(
                        programs,
                        investorKey,
                        (ulong)listingId,
                        new PublicKey(config.RentCollector),
                        investorKey,
                        new PublicKey(position.PaymentAccount)),
                ];
            }

            var paymentMint = new PublicKey(position.PaymentMint);
            var (paymentTokenProgram, _) = await ResolveMintAsync(cluster, paymentMint, token).ConfigureAwait(false);

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
        /// at offset 44 of the mint layout). Shared by every wrapper that addresses a
        /// payment mint's program: buys, claims, share settlements, income claims.
        /// </summary>
        public static async Task<(PublicKey TokenProgram, int Decimals)> ResolveMintAsync(
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

        /// <summary>
        /// One voter's SPV-lawyer votes on a listing, every round the indexer still sees
        /// open: the vote_on_spv_lawyer revote keeps the vote of the round it votes in
        /// (same shape as the agent election's rows - the schema exposes no round filter).
        /// One page covers every round a voter realistically leaves open.
        /// </summary>
        private static async Task<IReadOnlyList<XcavateMarketplaceIndexerModel.AgentElectionState.VoteRow>> GetLawyerVotesAsync(
            IXcavateDevnetIndexerClient client, long listingId, string voter, CancellationToken token)
        {
            var result = await client.MarketplaceLawyerVotes
                .ExecuteAsync(listingId.ToString(CultureInfo.InvariantCulture), voter, first: 50, offset: 0, token)
                .ConfigureAwait(false);

            result.EnsureNoErrors();

            return result.Data?.LawyerVotes.Nodes
                .Select(node => new XcavateMarketplaceIndexerModel.AgentElectionState.VoteRow(
                    (long)ParseUInt64(node.Round), node.Choice, (long)ParseUInt64(node.Power)))
                .ToList() ?? [];
        }

        /// <summary>One region's current operator wallet - execute_deal's region-share payee - or null when the indexer does not know the region.</summary>
        private static async Task<string?> GetRegionOwnerAsync(
            IXcavateDevnetIndexerClient client, ushort regionId, CancellationToken token)
        {
            var result = await client.Regions
                .ExecuteAsync(regionId, first: 1, offset: 0, token)
                .ConfigureAwait(false);

            result.EnsureNoErrors();

            return result.Data?.Regions.Nodes.FirstOrDefault()?.Owner;
        }

        /// <summary>One wallet's share holding in one asset, or null when the wallet holds none.</summary>
        private static async Task<IMarketplaceShareHoldingOf_ShareHoldings_Nodes?> GetShareHoldingAsync(
            IXcavateDevnetIndexerClient client, string assetIdArg, string owner, CancellationToken token)
        {
            var result = await client.MarketplaceShareHoldingOf
                .ExecuteAsync(assetIdArg, owner, token)
                .ConfigureAwait(false);

            result.EnsureNoErrors();

            return result.Data?.ShareHoldings.Nodes.FirstOrDefault();
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
