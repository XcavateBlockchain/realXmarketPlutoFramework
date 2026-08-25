using PlutoFrameworkCore.Solana;
using StrawberryShake;
using Substrate.NetApi.Model.Types.Primitive;
using System.Globalization;
using System.Numerics;
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
    /// The chain carries a <c>metadataUri</c> per property asset pointing at the webapp's
    /// property document (name, address, images, finances), fetched and mapped through
    /// <see cref="XcavateSolanaPropertyMetadataClient"/>; when the URI is still empty or the
    /// fetch fails, the mapped record degrades to a minimal <see cref="PropertyMetadata"/>
    /// synthesized from chain data so the existing views still render. There is no
    /// server-side text filtering either way - the old town/type/name filters are answered
    /// client-side by <see cref="MatchesFilter"/>.
    /// </para>
    /// </summary>
    public static class XcavateMarketplaceIndexerModel
    {
        /// <summary>
        /// The cluster whose marketplace program listings are read. Devnet for the same
        /// reason as <see cref="WhitelistModel.WhitelistCluster"/>: the Xcavate programs
        /// are only deployed there today.
        /// </summary>
        public const SolanaCluster MarketplaceCluster = SolanaCluster.Devnet;

        /// <summary>
        /// Decimals of the listing's share price. The marketplace config's accepted payment
        /// mints are USD stablecoins (tUSDC / USDC) with 6 decimals, and sharePrice is
        /// denominated in their base units.
        /// </summary>
        public const int SharePriceDecimals = 6;

        /// <summary>
        /// Share holdings are read per property; one page of this size per request. Holder
        /// counts are unbounded in principle, so the reader pages until short.
        /// </summary>
        private const int HoldingsPageSize = 100;

        public static async Task<IReadOnlyList<XcavateSolanaListingNft>> GetMarketplaceListedPropertiesAsync(
            int first,
            int offset,
            CancellationToken token = default)
        {
            var client = XcavateWhitelistIndexer.GetClient(MarketplaceCluster);

            var result = await client.MarketplaceListings
                .ExecuteAsync(first, offset, token)
                .ConfigureAwait(false);

            result.EnsureNoErrors();

            var listings = result.Data?.Listings.Nodes;
            if (listings is null || listings.Count == 0)
            {
                return [];
            }

            // The flat QueryRoot has no listing -> asset join, so the asset behind each
            // listing is a second lookup (plus its metadata document); the listings are
            // independent, so all run at once.
            var mapped = await Task.WhenAll(
                listings.Select(async listing =>
                {
                    var asset = await GetPropertyAssetAsync(client, listing.AssetId, token).ConfigureAwait(false);
                    var metadata = await XcavateSolanaPropertyMetadataClient.GetAsync(asset?.MetadataUri, token).ConfigureAwait(false);
                    return MapListing(listing, asset, metadata);
                }))
                .ConfigureAwait(false);

            return mapped;
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
            long listingId,
            string? investor,
            CancellationToken token = default)
        {
            var client = XcavateWhitelistIndexer.GetClient(MarketplaceCluster);

            var result = await client.MarketplaceListing
                .ExecuteAsync(listingId.ToString(CultureInfo.InvariantCulture), token)
                .ConfigureAwait(false);

            result.EnsureNoErrors();

            var listing = result.Data?.Listings.Nodes.FirstOrDefault();
            if (listing is null)
            {
                return null;
            }

            var asset = await GetPropertyAssetAsync(client, listing.AssetId, token).ConfigureAwait(false);
            var metadata = await XcavateSolanaPropertyMetadataClient.GetAsync(asset?.MetadataUri, token).ConfigureAwait(false);

            var nft = MapListing(listing, asset, metadata);

            if (investor is not null && nft.OngoingObjectListingDetails is not null)
            {
                var positionsResult = await client.MarketplaceInvestorPositions
                    .ExecuteAsync(listing.ListingId, investor, first: 1, offset: 0, token)
                    .ConfigureAwait(false);

                positionsResult.EnsureNoErrors();

                var position = positionsResult.Data?.InvestorPositions.Nodes.FirstOrDefault();
                if (position is not null)
                {
                    // Bought plus reserved: both are shares the investor has committed
                    // money to, and the reserved part is what the cancel-reservation UI
                    // (gated on this figure downstream) exists for.
                    var committedShares = ParseInt64(position.ShareAmount) + ParseInt64(position.ReservedShareAmount);

                    nft.OngoingObjectListingDetails.ShareOwners[position.Investor] = new ShareOwner
                    {
                        Account = position.Investor,
                        ShareAmount = (uint)Math.Clamp(committedShares, 0, uint.MaxValue),
                    };
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

        private static async Task<IMarketplacePropertyAsset_PropertyAssets_Nodes?> GetPropertyAssetAsync(
            IXcavateDevnetIndexerClient client,
            string assetId,
            CancellationToken token)
        {
            var result = await client.MarketplacePropertyAsset
                .ExecuteAsync(assetId, token)
                .ConfigureAwait(false);

            result.EnsureNoErrors();

            return result.Data?.PropertyAssets.Nodes.FirstOrDefault();
        }

        private static XcavateSolanaListingNft MapListing(
            IListingParts listing,
            IMarketplacePropertyAsset_PropertyAssets_Nodes? asset,
            XcavateSolanaPropertyMetadata? offchainMetadata)
        {
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

            var metadata = new MetadataBase
            {
                Name = propertyName,
                Description = offchainMetadata?.PropertyDescription ?? string.Empty,
                Image = offchainMetadata?.PropertyImages.FirstOrDefault() ?? string.Empty,
            };

            // The webapp document when the asset carries one, degraded to a minimal
            // record synthesized from chain state when not - the views need a non-null
            // PropertyMetadata to render at all.
            var propertyMetadata = offchainMetadata?.ToPropertyMetadata() ?? new PropertyMetadata
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
    }
}
