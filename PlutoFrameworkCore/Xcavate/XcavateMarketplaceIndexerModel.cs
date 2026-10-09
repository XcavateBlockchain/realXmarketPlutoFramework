using PlutoFrameworkCore.Solana;
using StrawberryShake;
using Substrate.NetApi.Model.Types.Primitive;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using UniqueryPlus.Metadata;
using UniqueryPlus.Nfts;
using XcavateDevnetIndexer;

namespace PlutoFramework.Model.Xcavate
{
    /// <summary>
    /// Reads marketplace property listings from the Xcavate Solana programs, through the
    /// Xcavate indexer - the replacement for the SubQuery-backed
    /// <c>UniqueryPlus.Nfts.XcavateIndexerModel</c> marketplace feed.
    /// <para>
    /// Each property asset nests the webapp's property document (name, address, images,
    /// finances) as <c>metadata</c>: the indexer's background enricher fetches and
    /// decomposes the <c>metadataUri</c> document server-side (ADR-27), so the app reads
    /// it from the same query instead of downloading and parsing the JSON itself. When
    /// the enricher has no snapshot for an asset yet (<c>metadata</c> is null), the mapped
    /// record degrades to a minimal <see cref="PropertyMetadata"/> synthesized from chain
    /// data so the existing views still render. There is no server-side text filtering
    /// either way - the old town/type/name filters are answered client-side by
    /// <see cref="MatchesFilter"/>.
    /// </para>
    /// <para>
    /// The cluster is the caller's - the app's selected Solana network. Callers gate on
    /// <see cref="XcavateDeploymentModel.IsDeployed"/> first: on a cluster with no deployment
    /// these readers throw <see cref="NotSupportedException"/> from the indexer client, and a
    /// marketplace that is not there must look like an empty placeholder, not an error.
    /// </para>
    /// </summary>
    public static class XcavateMarketplaceIndexerModel
    {
        /// <summary>
        /// Decimals of the listing's share price: sharePrice is denominated at 9 decimal
        /// places, whatever payment mint a purchase settles in - the program converts
        /// prices between mints by decimal count alone (see
        /// <see cref="XcavateMarketplaceCallsModel.ScaleToMintDecimals"/>).
        /// </summary>
        public const int SharePriceDecimals = 9;

        /// <summary>
        /// Share holdings are read per property; one page of this size per request. Holder
        /// counts are unbounded in principle, so the reader pages until short.
        /// </summary>
        private const int HoldingsPageSize = 100;

        /// <summary>
        /// A listing's full governance surface for the property detail page: the
        /// pre-settlement SPV-lawyer election (the "terms" vote; marketplace id space,
        /// keyed by the marketplace listing id) and the letting seat's post-settlement
        /// votes (property id space, keyed by the asset id).
        /// </summary>
        public sealed record XcavateGovernanceState
        {
            /// <summary>
            /// The SPV-lawyer election the listing is running (the candidacy investors
            /// vote on - the "terms" flow). Rounds repeat until a lawyer wins, so the
            /// listing's own counters are the live round. Null while no election has
            /// opened (round 0, e.g. the listing is still selling).
            /// </summary>
            public sealed record SpvElectionState(
                ulong Round,
                long Expiry,
                long CandidateCount,
                long SoldShareAmount,
                long TotalVotePower,
                SpvCandidacy? LeadingCandidacy);

            /// <summary>One lawyer's candidacy with the votes backing it.</summary>
            public sealed record SpvCandidacy(string Lawyer, long VotePower, long Costs);

            /// <summary>The seat's live spending proposal with its running tallies.</summary>
            public sealed record ProposalState(
                long ProposalId,
                long Expiry,
                long TallyYes,
                long TallyNo,
                long TallyAbstain,
                int QuorumBps,
                int ThresholdBps);

            /// <summary>The live challenge against the sitting agent with its running tallies.</summary>
            public sealed record ChallengeState(
                long ChallengeId,
                long Expiry,
                long TallyYes,
                long TallyNo,
                long TallyAbstain,
                int QuorumBps);

            /// <summary>
            /// The connected wallet's share ledger and its free voting power: on-chain the
            /// effective lock is the LARGEST single reason's counter (locks overlap across
            /// reasons), and listed shares count in amount but not in votes.
            /// </summary>
            public sealed record HoldingState(uint Amount, uint Listed, uint MaxLock)
            {
                public uint Votable => Amount - Math.Min(Listed + MaxLock, Amount);
            }

            public SpvElectionState? SpvElection { get; init; }

            /// <summary>The letting seat's election counters; zeroes while no election runs.</summary>
            public long ElectionRound { get; init; }
            public long ElectionExpiry { get; init; }
            public long ElectionCandidateCount { get; init; }
            public int ElectionQuorumBps { get; init; }

            public ProposalState? ActiveProposal { get; init; }
            public ChallengeState? ActiveChallenge { get; init; }

            /// <summary>The connected wallet's ledger; null for wallets holding nothing.</summary>
            public HoldingState? Holding { get; init; }
        }

        /// <summary>
        /// One listing's governance state for the property detail page's voting sections.
        /// The terms vote lives in the marketplace id space (<paramref name="listingId"/>);
        /// the letting seat, proposals and challenges in the property program's
        /// (<paramref name="assetId"/>). The caller's own ledger joins in when
        /// <paramref name="voter"/> is known. A missing piece (no vote opened yet, no
        /// active proposal, no holding) reads as null rather than an error.
        /// </summary>
        public static async Task<XcavateGovernanceState> GetGovernanceStateAsync(
            SolanaCluster cluster,
            long listingId,
            long assetId,
            string? voter,
            CancellationToken token = default)
        {
            var client = XcavateWhitelistIndexer.GetClient(cluster);

            var listingIdArg = listingId.ToString(CultureInfo.InvariantCulture);
            var assetIdArg = assetId.ToString(CultureInfo.InvariantCulture);

            var listingResult = await client.MarketplaceListing
                .ExecuteAsync(listingIdArg, token)
                .ConfigureAwait(false);
            listingResult.EnsureNoErrors();
            var listingNode = listingResult.Data?.Listings.Nodes.FirstOrDefault();

            var candidaciesResult = await client.MarketplaceLawyerCandidacies
                .ExecuteAsync(listingIdArg, first: 5, offset: 0, token)
                .ConfigureAwait(false);
            candidaciesResult.EnsureNoErrors();
            var candidacies = candidaciesResult.Data?.LawyerCandidacies.Nodes;

            var lettingResult = await client.PropertyLettingGovernance
                .ExecuteAsync(assetIdArg, token)
                .ConfigureAwait(false);
            lettingResult.EnsureNoErrors();
            var lettingNode = lettingResult.Data?.PropertyLettings.Nodes.FirstOrDefault();

            var proposalsResult = await client.PropertyProposals
                .ExecuteAsync(assetIdArg, first: 1, offset: 0, token)
                .ConfigureAwait(false);
            proposalsResult.EnsureNoErrors();
            var proposalNode = proposalsResult.Data?.Proposals.Nodes.FirstOrDefault();

            var challengesResult = await client.PropertyChallenges
                .ExecuteAsync(assetIdArg, first: 1, offset: 0, token)
                .ConfigureAwait(false);
            challengesResult.EnsureNoErrors();
            var challengeNode = challengesResult.Data?.Challenges.Nodes.FirstOrDefault();

            XcavateGovernanceState.HoldingState? holding = null;
            if (voter is not null)
            {
                var holdingResult = await client.MarketplaceShareHoldingOf
                    .ExecuteAsync(assetIdArg, voter, token)
                    .ConfigureAwait(false);
                holdingResult.EnsureNoErrors();
                var holdingNode = holdingResult.Data?.ShareHoldings.Nodes.FirstOrDefault();
                if (holdingNode is not null)
                {
                    var maxLock = Math.Max(
                        Math.Max(ToUInt32(holdingNode.LockLawyerElection), ToUInt32(holdingNode.LockAgentElection)),
                        Math.Max(ToUInt32(holdingNode.LockProposal), ToUInt32(holdingNode.LockChallenge)));
                    holding = new XcavateGovernanceState.HoldingState(
                        ToUInt32(holdingNode.Amount),
                        ToUInt32(holdingNode.Listed),
                        maxLock);
                }
            }

            return new XcavateGovernanceState
            {
                SpvElection = BuildSpvElection(listingNode, candidacies),
                ElectionRound = ParseInt64(lettingNode?.ElectionRound),
                ElectionExpiry = ParseInt64(lettingNode?.ElectionExpiry),
                ElectionCandidateCount = ParseInt64(lettingNode?.ElectionCandidateCount),
                ElectionQuorumBps = lettingNode?.ElectionQuorumBps ?? 0,
                ActiveProposal = proposalNode is null
                    ? null
                    : new XcavateGovernanceState.ProposalState(
                        ParseInt64(proposalNode.ProposalId),
                        ParseInt64(proposalNode.Expiry),
                        ParseInt64(proposalNode.TallyYes),
                        ParseInt64(proposalNode.TallyNo),
                        ParseInt64(proposalNode.TallyAbstain),
                        proposalNode.QuorumBps,
                        proposalNode.ThresholdBps),
                ActiveChallenge = challengeNode is null
                    ? null
                    : new XcavateGovernanceState.ChallengeState(
                        ParseInt64(challengeNode.ChallengeId),
                        ParseInt64(challengeNode.Expiry),
                        ParseInt64(challengeNode.TallyYes),
                        ParseInt64(challengeNode.TallyNo),
                        ParseInt64(challengeNode.TallyAbstain),
                        challengeNode.QuorumBps),
                Holding = holding,
            };
        }

        /// <summary>
        /// The listing's live SPV-lawyer election assembled from its own counters (the
        /// fragment's spvElection* fields) plus the round's active candidacies: the summed
        /// vote power and the strongest candidacy - the one the page's Approve action
        /// backs. Null while no election has opened (round 0).
        /// </summary>
        private static XcavateGovernanceState.SpvElectionState? BuildSpvElection(
            IMarketplaceListing_Listings_Nodes? listing,
            IReadOnlyList<IMarketplaceLawyerCandidacies_LawyerCandidacies_Nodes>? candidacies)
        {
            if (listing is null)
            {
                return null;
            }

            var round = (ulong)Math.Max(0, ParseInt64(listing.SpvElectionRound));
            if (round == 0)
            {
                return null;
            }

            long totalPower = 0;
            XcavateGovernanceState.SpvCandidacy? leading = null;
            if (candidacies is not null)
            {
                foreach (var node in candidacies)
                {
                    if (ParseInt64(node.Round) != (long)round)
                    {
                        continue;
                    }

                    var power = ParseInt64(node.VotePower);
                    totalPower += power;
                    if (leading is null || power > leading.VotePower)
                    {
                        leading = new XcavateGovernanceState.SpvCandidacy(
                            node.Lawyer, power, ParseInt64(node.Costs));
                    }
                }
            }

            return new XcavateGovernanceState.SpvElectionState(
                round,
                ParseInt64(listing.SpvElectionExpiry),
                ParseInt64(listing.SpvElectionCandidateCount),
                ParseInt64(listing.SoldShareAmount),
                totalPower,
                leading);
        }

        public static async Task<IReadOnlyList<XcavateSolanaListingNft>> GetMarketplaceListedPropertiesAsync(
            SolanaCluster cluster,
            int first,
            int offset,
            CancellationToken token = default)
        {
            var client = XcavateWhitelistIndexer.GetClient(cluster);

            var result = await client.MarketplaceListings
                .ExecuteAsync(first, offset, token)
                .ConfigureAwait(false);

            result.EnsureNoErrors();

            var listings = result.Data?.Listings.Nodes;
            if (listings is null || listings.Count == 0)
            {
                return [];
            }

            // The asset (and its nested metadata document) arrives joined on each listing,
            // so one query answers the whole page - no second per-asset lookup.
            return listings
                .Select(listing => MapListing(listing, listing.PropertyAsset))
                .ToList();
        }

        /// <summary>
        /// One listing refetched fresh for its detail page, with the share-owner
        /// dictionaries populated: the caller's open position under
        /// <c>OngoingObjectListingDetails.ShareOwners</c> (when
        /// <paramref name="investor"/> is known) and every holder of the underlying asset
        /// under <c>RealWorldAssetDetails.ShareOwners</c>. Null when the listing does not
        /// exist or its account has been closed.
        /// </summary>
        public static async Task<XcavateSolanaListingNft?> GetListingFullInfoAsync(
            SolanaCluster cluster,
            long listingId,
            string? investor,
            CancellationToken token = default)
        {
            var client = XcavateWhitelistIndexer.GetClient(cluster);

            var result = await client.MarketplaceListing
                .ExecuteAsync(listingId.ToString(CultureInfo.InvariantCulture), token)
                .ConfigureAwait(false);

            result.EnsureNoErrors();

            var listing = result.Data?.Listings.Nodes.FirstOrDefault();
            if (listing is null)
            {
                return null;
            }

            // The joined asset arrives with the listing - no second lookup.
            var asset = listing.PropertyAsset;

            var nft = MapListing(listing, asset);

            if (investor is not null && nft.OngoingObjectListingDetails is not null)
            {
                var positionsResult = await client.MarketplaceInvestorPositions
                    .ExecuteAsync(listing.ListingId, investor, first: 1, offset: 0, token)
                    .ConfigureAwait(false);

                positionsResult.EnsureNoErrors();

                var position = positionsResult.Data?.InvestorPositions.Nodes.FirstOrDefault();
                if (position is not null)
                {
                    // Reserved only: the ongoing listing's ShareOwners feeds the wrapper's
                    // TokensBought, which the detail page prints as "You reserved" and the
                    // claim / cancel-reservation / refund states gate on. claim_shares
                    // moves the reserved shares into share_amount and zeroes
                    // reserved_share_amount, so adding the bought part here would show a
                    // claimed position as still reserved. The owned part arrives through
                    // the asset's share holdings below.
                    var reservedShares = ParseInt64(position.ReservedShareAmount);

                    if (reservedShares > 0)
                    {
                        nft.OngoingObjectListingDetails.ShareOwners[position.Investor] = new ShareOwner
                        {
                            Account = position.Investor,
                            ShareAmount = (uint)Math.Clamp(reservedShares, 0, uint.MaxValue),
                        };
                    }
                }
            }

            if (asset is not null && nft.RealWorldAssetDetails is not null)
            {
                var holdingsOffset = 0;

                while (true)
                {
                    var holdingsResult = await client.MarketplaceShareHoldings
                        .ExecuteAsync(asset.AssetId, HoldingsPageSize, holdingsOffset, token)
                        .ConfigureAwait(false);

                    holdingsResult.EnsureNoErrors();

                    var holdings = holdingsResult.Data?.ShareHoldings.Nodes ?? [];

                    foreach (var holding in holdings)
                    {
                        nft.RealWorldAssetDetails.ShareOwners[holding.Owner] = new ShareOwner
                        {
                            Account = holding.Owner,
                            ShareAmount = ToUInt32(holding.Amount),
                        };
                    }

                    if (holdings.Count < HoldingsPageSize)
                    {
                        break;
                    }

                    holdingsOffset += holdings.Count;
                }
            }

            return nft;
        }

        /// <summary>
        /// The properties one investor has bought or reserved shares in, one page at a
        /// time. A single <c>investorProperties</c> query answers the whole page
        /// (ADR-34): the indexer joins the investor's open positions to their listings
        /// and the listings' <c>propertyAsset</c> metadata server-side, so no per-listing
        /// lookups.
        /// <para>
        /// The filters run in SQL, not in the app: <paramref name="owned"/> keeps only
        /// positions that hold bought shares (the page's "purchased" toggle),
        /// <paramref name="reserved"/> only positions with reserved shares (the
        /// "reserved" toggle), <paramref name="name"/> case-insensitive-substring-matches
        /// the property name or postcode (the search bar), and
        /// <paramref name="townCity"/> / <paramref name="propertyType"/> match the
        /// filter popup's dropdowns. A null argument switches a filter off; the caller
        /// converts its empty/All values to null.
        /// </para>
        /// <para>
        /// A position with no bought and no reserved shares carries nothing to show and
        /// is dropped. On the nested listing the two counts are kept apart the way the
        /// views read them: the reserved count sits under the investor's address in
        /// <c>OngoingObjectListingDetails.ShareOwners</c> (the wrapper's
        /// <c>TokensBought</c> - "You reserved", the claim and cancel-reservation gates),
        /// the bought count in <c>RealWorldAssetDetails.ShareOwners</c> (the wrapper's
        /// <c>TokensOwned</c> - "You own"), the same split the detail page's
        /// <see cref="GetListingFullInfoAsync"/> produces, so a record wrapped from
        /// <see cref="Listing"/> agrees with it.
        /// </para>
        /// </summary>
        public static async Task<IReadOnlyList<XcavateSolanaInvestorProperty>> GetInvestorPropertiesAsync(
            SolanaCluster cluster,
            string investor,
            bool? owned,
            bool? reserved,
            string? name,
            string? townCity,
            string? propertyType,
            int first,
            int offset,
            CancellationToken token = default)
        {
            var client = XcavateWhitelistIndexer.GetClient(cluster);

            var result = await client.InvestorProperties
                .ExecuteAsync(investor, owned, reserved, name, townCity, propertyType, first, offset, token)
                .ConfigureAwait(false);

            result.EnsureNoErrors();

            var nodes = result.Data?.InvestorProperties.Nodes ?? [];

            // Zero/zero positions hold nothing to show; drop them.
            var nodesToShow = nodes
                .Where(node => ParseInt64(node.ShareAmount) + ParseInt64(node.ReservedShareAmount) > 0)
                .ToList();

            var properties = new List<XcavateSolanaInvestorProperty>(nodesToShow.Count);

            foreach (var node in nodesToShow)
            {
                // The listing (with its asset and metadata) arrives joined with the
                // position - no second lookup.
                var listing = MapListing(node.Listing, node.Listing.PropertyAsset);

                var boughtShares = ParseInt64(node.ShareAmount);
                var reservedShares = ParseInt64(node.ReservedShareAmount);

                // The two figures feed different views: the ongoing listing's
                // ShareOwners becomes the wrapper's TokensBought ("You reserved", the
                // claim and cancel-reservation gates), the asset's ShareOwners becomes
                // TokensOwned ("You own"). claim_shares moves reserved_share_amount into
                // share_amount, so summing them into one bucket would show a claimed
                // position as still reserved. Zero entries are simply absent - every
                // reader falls back to 0 on a missing key.
                if (reservedShares > 0 && listing.OngoingObjectListingDetails is not null)
                {
                    listing.OngoingObjectListingDetails.ShareOwners[investor] = new ShareOwner
                    {
                        Account = investor,
                        ShareAmount = (uint)Math.Clamp(reservedShares, 0, uint.MaxValue),
                    };
                }

                if (boughtShares > 0 && listing.RealWorldAssetDetails is not null)
                {
                    // This path does not join share holdings; the position's share_amount
                    // stands in for the owned count. The two diverge only after
                    // secondary-market moves, which this position-scoped list (filtered
                    // on the same figure server-side) does not track either.
                    listing.RealWorldAssetDetails.ShareOwners[investor] = new ShareOwner
                    {
                        Account = investor,
                        ShareAmount = (uint)Math.Clamp(boughtShares, 0, uint.MaxValue),
                    };
                }

                properties.Add(new XcavateSolanaInvestorProperty
                {
                    Listing = listing,
                    BoughtShares = (uint)Math.Clamp(boughtShares, 0, uint.MaxValue),
                    ReservedShares = (uint)Math.Clamp(reservedShares, 0, uint.MaxValue),
                });
            }

            return properties;
        }

        /// <summary>
        /// The client-side stand-in for the old SubQuery <c>includesInsensitive</c> filters:
        /// an empty filter matches everything, a non-empty one is a case-insensitive
        /// substring match. The search text additionally matches the postcode, which is the
        /// only address component the chain knows.
        /// </summary>
        public static bool MatchesFilter(
            XcavateSolanaListingNft nft,
            string includesTownCity,
            string includesPropertyType,
            string includesPropertyName)
        {
            return MatchesAny(includesTownCity, nft.XcavateMetadata?.Address.TownCity)
                && MatchesAny(includesPropertyType, nft.XcavateMetadata?.PropertyType)
                && MatchesAny(includesPropertyName, nft.XcavateMetadata?.PropertyName, nft.XcavateMetadata?.Address.PostCode);
        }

        private static bool MatchesAny(string filter, params string?[] values)
        {
            if (string.IsNullOrEmpty(filter))
            {
                return true;
            }

            return values.Any(value => value?.Contains(filter, StringComparison.OrdinalIgnoreCase) == true);
        }

        private static XcavateSolanaListingNft MapListing(
            IListingParts listing,
            IMarketplaceListings_Listings_Nodes_PropertyAsset? asset)
        {
            // The indexer's background enricher has already fetched and decomposed the
            // document `metadataUri` points at (ADR-27), nested on the asset itself; null
            // while the enricher has no snapshot for this PDA (fetch pending or failing).
            var offchainMetadata = asset?.Metadata;
            var listingId = ParseInt64(listing.ListingId);
            var assetId = ParseInt64(listing.AssetId);
            var listed = ParseInt64(listing.ListedShareAmount);
            var sold = ParseInt64(listing.SoldShareAmount);
            var reserved = ParseInt64(listing.ReservedShareAmount);

            // Shares are only actually for sale while the listing is LISTED; EXPIRED keeps
            // its real remainder so the "Listing expired" chip works (the wall clock already
            // blocks buying there). Everything else - PENDING_ASSETS before the asset
            // exists, CANCELLED, REFUNDING, and the sold-out/claim statuses - must not
            // present a buyable remainder, whatever the raw counters say.
            var purchasable = listing.Status is ListingStatus.Listed or ListingStatus.Expired;
            var availableShares = purchasable ? Math.Max(0, listed - sold - reserved) : 0;

            // The asset account is the authority on the property's total share supply; a
            // PENDING_ASSETS listing has no asset yet, and there the listed amount is the
            // whole offer.
            var totalShares = asset is null ? listed : ParseInt64(asset.ShareAmount);

            var pricePerShare = SolanaAmount.FromBaseUnits(listing.SharePrice, SharePriceDecimals);

            // Best name available: the webapp document's, then the on-chain asset name
            // (empty until init_property_assets attaches it), then chain-synthesized.
            var propertyName = FirstNonEmpty(
                offchainMetadata?.PropertyName,
                asset?.Name,
                string.IsNullOrWhiteSpace(asset?.Location) ? null : $"Property {asset!.Location}",
                $"Listing #{listingId}")!;

            // The indexer carries the document's image list as a raw JSON string (juniper
            // has no JSON scalar), so it is decoded here once and shared with the view.
            var images = ParseStringArray(offchainMetadata?.PropertyImages);

            // The mirror's compressed 720x720 copies of `images` (same order; null until
            // the mirror's first upload for the asset) arrive as a real list, unlike the
            // raw-JSON `propertyImages`. Every surface except the full-screen image page
            // shows these; an absent list falls back to the full-resolution originals.
            var thumbnails = offchainMetadata?.PropertyImageThumbnails?.Select(NormalizeMirrorUrl).ToList();

            var metadata = new MetadataBase
            {
                Name = propertyName,
                Description = offchainMetadata?.PropertyDescription ?? string.Empty,
                Image = thumbnails?.FirstOrDefault() ?? images.FirstOrDefault() ?? string.Empty,
            };

            // The indexer's decomposed document when the asset carries one, degraded to a
            // minimal record synthesized from chain state when not - the views need a
            // non-null PropertyMetadata to render at all.
            var propertyMetadata = offchainMetadata is not null
                ? MapPropertyMetadata(offchainMetadata)
                : new PropertyMetadata
                {
                    Financials = new PropertyFinancials(),
                    Files = [],
                    Address = new PropertyAddress(),
                    Attributes = new PropertyAttributes(),
                };

            // Chain-authoritative fields win over whatever the document said: the buy flow
            // prices and counts shares with these, and the sale's status and developer are
            // on-chain facts.
            propertyMetadata.Status = listing.Status.ToString();
            propertyMetadata.PropertyName = propertyName;
            propertyMetadata.DeveloperAddress = listing.Developer;
            propertyMetadata.AccountAddress = listing.Developer;
            propertyMetadata.PropertyId ??= listing.Id;
            propertyMetadata.Address.PostCode ??= asset?.Location;
            propertyMetadata.Financials.PricePerToken = pricePerShare;
            propertyMetadata.Financials.NumberOfTokens = (int)Math.Clamp(totalShares, 0, int.MaxValue);
            propertyMetadata.Financials.PropertyPrice = pricePerShare * totalShares;

            return new XcavateSolanaListingNft
            {
                CollectionId = BigInteger.Zero,
                Id = listingId,
                Owner = listing.Developer,
                ListingId = listingId,
                AssetId = assetId,
                ListingExpiryTimestamp = ParseInt64(listing.ListingExpiry),
                ClaimDeadlineTimestamp = ParseInt64(listing.ClaimDeadline),
                ListingStatus = listing.Status.ToString(),
                OpenForSale = listing.Status is not (ListingStatus.PendingAssets or ListingStatus.Cancelled or ListingStatus.Refunding),
                IsTornDown = listing.Status is ListingStatus.Cancelled or ListingStatus.Refunding,
                Metadata = metadata,
                XcavateMetadata = propertyMetadata,
                OngoingObjectListingDetails = new XcavateOngoingObjectListingDetails
                {
                    RealEstateDeveloper = listing.Developer,
                    TaxPaidByDeveloper = listing.TaxPaidByDeveloper,
                    // Unix seconds, not block numbers: Solana deadlines are wall-clock.
                    // XcavateSolanaListingNft carries the untruncated values; these exist
                    // for the Substrate-era record shape.
                    ListingExpiry = ToUInt32(listing.ListingExpiry),
                    ClaimExpiry = ParseInt64(listing.ClaimDeadline) > 0 ? ToUInt32(listing.ClaimDeadline) : null,
                    ListedTokens = (uint)Math.Clamp(availableShares, 0, uint.MaxValue),
                    // Reserved-but-not-claimed shares are the Solana counterpart of the
                    // pallet's unclaimed tokens: the claim/refund states downstream key
                    // off this being non-zero.
                    UnclaimedTokens = (uint)Math.Clamp(reserved, 0, uint.MaxValue),
                    AssetId = new U32(ToUInt32(listing.AssetId)),
                    CollectionId = new U32(0),
                    ItemId = new U32(ToUInt32(listing.ListingId)),
                    ShareOwners = new(),
                },
                NftMarketplaceDetails = asset is null
                    ? null
                    : new NftMarketplaceDetails
                    {
                        SpvCreated = asset.SpvCreated,
                        AssetId = ToUInt32(asset.AssetId),
                        Region = (uint)Math.Clamp(asset.RegionId, 0, int.MaxValue),
                        Location = asset.Location,
                        Tokens = ToUInt32(asset.ShareAmount),
                    },
                RealWorldAssetDetails = asset is null
                    ? null
                    : new XcavateRealWorldAssetDetails
                    {
                        Tokens = ToUInt32(asset.ShareAmount),
                        Price = ParseBigInteger(listing.SharePrice),
                        SpvCreated = asset.SpvCreated,
                        Finalized = asset.Finalized,
                        ShareOwners = new(),
                    },
            };
        }

        private static string? FirstNonEmpty(params string?[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        }

        private static long ParseInt64(string? value)
        {
            return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
        }

        private static uint ToUInt32(string? value)
        {
            var parsed = ParseInt64(value);

            return (uint)Math.Clamp(parsed, 0, uint.MaxValue);
        }

        private static BigInteger ParseBigInteger(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return BigInteger.Zero;
            }

            return BigInteger.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : BigInteger.Zero;
        }

        private static decimal ParseDecimal(string? value)
        {
            return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0m;
        }

        private static int? ParseInt32(string? value)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
        }

        /// <summary>
        /// The mirror currently prefixes its public thumbnail URLs with a doubled scheme
        /// (<c>https://://host/...</c>), which no image loader can parse; collapsing it
        /// here keeps the views working until the mirror is fixed. Correct URLs contain
        /// no "://://" and pass through unchanged.
        /// </summary>
        private static string NormalizeMirrorUrl(string url) => url.Replace("://://", "://", StringComparison.Ordinal);

        /// <summary>
        /// The indexer's enricher stores the document's URL arrays (<c>propertyImages</c>,
        /// <c>otherDocuments</c>) as raw JSON strings - juniper has no JSON scalar - so the
        /// array is decoded here. Malformed or empty input degrades to no URLs.
        /// </summary>
        private static List<string> ParseStringArray(string? rawJson)
        {
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                return [];
            }

            try
            {
                return JsonSerializer.Deserialize<List<string>>(rawJson) ?? [];
            }
            catch (JsonException)
            {
                return [];
            }
        }

        /// <summary>
        /// The <see cref="PropertyMetadata"/> the property views bind to, built from the
        /// indexer's decomposed document. The chain-authoritative overwrite that
        /// <see cref="MapListing"/> applies afterwards is unchanged; only the source of
        /// the document data moved from an app-side HTTP fetch to the indexer.
        /// </summary>
        private static PropertyMetadata MapPropertyMetadata(IMarketplaceListings_Listings_Nodes_PropertyAsset_Metadata metadata)
        {
            var address = metadata.Address;
            var attributes = metadata.Attributes;
            var finances = metadata.Finances;

            var documentPropertyPrice = ParseDecimal(finances?.PropertyPrice);
            var documentSharePrice = ParseDecimal(finances?.SharePrice);
            var documentShares = (int)Math.Clamp(ParseInt64(finances?.NumberOfShares), 0, int.MaxValue);

            return new PropertyMetadata
            {
                Status = metadata.Status,
                PropertyName = metadata.PropertyName,
                Financials = new PropertyFinancials
                {
                    PropertyPrice = documentPropertyPrice,
                    NumberOfTokens = documentShares,
                    PricePerToken = documentSharePrice,
                    // MapListing overwrites the three live fields above with chain-derived
                    // values; yield math reads these document-scale copies so the ratio
                    // never mixes the document's scale with the chain's price decimals.
                    DocumentPropertyPrice = documentPropertyPrice,
                    DocumentPricePerToken = documentSharePrice,
                    DocumentNumberOfShares = documentShares,
                    EstimatedRentalIncome = ParseDecimal(finances?.EstimatedRentalIncome),
                    AnnualServiceCharge = ParseDecimal(finances?.AnnualServiceCharge),
                    StampDutyTax = ParseDecimal(finances?.StampDutyTax),
                    IsStampDutyPaid = finances?.IsStampDutyPaid ?? false,
                    IsAnnualServiceChargePaid = finances?.IsAnnualServiceChargePaid ?? false,
                },
                // DisplayImages is what every view treats as the image list: the mirror's
                // compressed thumbnails when the indexer supplied them, the full-resolution
                // Files otherwise. Files stays full-res for the full-screen image page.
                Files = ParseStringArray(metadata.PropertyImages),
                ThumbnailFiles = metadata.PropertyImageThumbnails?.Select(NormalizeMirrorUrl).ToList() ?? [],
                CreatedAt = metadata.CreatedAt ?? default,
                UpdatedAt = metadata.UpdatedAt ?? default,
                Address = address is null
                    ? new PropertyAddress()
                    : new PropertyAddress
                    {
                        Street = address.Street,
                        TownCity = address.TownCity,
                        FlatOrUnit = address.FlatOrUnit,
                        PostCode = address.PostCode,
                        LocalAuthority = address.LocalAuthority,
                    },
                Company = metadata.CompanyName is null && metadata.CompanyLogo is null
                    ? null
                    : new PropertyCompany
                    {
                        Name = metadata.CompanyName,
                        Logo = metadata.CompanyLogo,
                    },
                PropertyDescription = metadata.PropertyDescription,
                PropertyType = metadata.PropertyType,
                Map = metadata.MapUrl,
                PlanningCode = metadata.PlanningCode,
                PropertyId = metadata.PropertyId,
                // The developer identity the document knows; the listing's on-chain
                // developer overwrites it in MapListing.
                DeveloperAddress = metadata.CompanyWalletAddress ?? metadata.User,
                AccountAddress = metadata.User,
                Attributes = attributes is null
                    ? null
                    : new PropertyAttributes
                    {
                        Area = attributes.Area,
                        Quality = attributes.Quality,
                        OutdoorSpace = attributes.OutdoorSpace,
                        NumberOfBedrooms = ParseInt32(attributes.NumberOfBedrooms),
                        NumberOfBathrooms = ParseInt32(attributes.NumberOfBathrooms),
                        ConstructionDate = attributes.ConstructionDate,
                        OffStreetParking = attributes.OffStreetParking,
                    },
            };
        }
    }
}
