using Amazon;
using Amazon.S3;
using CommunityToolkit.Maui.Alerts;
using Microsoft.Extensions.Configuration;
using PlutoFramework.Components.Loading;
using PlutoFramework.Constants;
using PlutoFramework.Model;
using PlutoFramework.Model.SQLite;
using PlutoFramework.Model.Xcavate;
using PlutoFrameworkCore.Xcavate;
using UniqueryPlus.Nfts;

namespace PlutoFramework.Components.XcavateProperty
{
    public class XcavatePropertyModel
    {
        private static readonly object S3ClientLock = new();
        private static IAmazonS3? cachedS3Client;
        private static bool s3ClientInitialized;

        private static IAmazonS3? GetOrCreateS3Client()
        {
            lock (S3ClientLock)
            {
                if (s3ClientInitialized)
                {
                    return cachedS3Client;
                }

                s3ClientInitialized = true;

                try
                {
                    var configuration = MauiAppBuilderExtensions.Services.GetService<IConfiguration>();
                    var accessKey = configuration?.GetValue<string>("DYNAMO_ACCESS_KEY");
                    var secretKey = configuration?.GetValue<string>("DYNAMO_SECRET_KEY");

                    if (string.IsNullOrWhiteSpace(accessKey) || string.IsNullOrWhiteSpace(secretKey))
                    {
                        return null;
                    }

                    cachedS3Client = new AmazonS3Client(accessKey, secretKey, RegionEndpoint.EUWest1);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }

                return cachedS3Client;
            }
        }

        public static async Task<XcavateNftWrapper> ToXcavateNftWrapperAsync(INftXcavateBase nft, CancellationToken token)
        {
            try
            {
                var s3Client = GetOrCreateS3Client();

                // Handle S3
                if (s3Client is not null && nft?.XcavateMetadata?.Files is not null)
                {
                    var images = new List<string>();

                    foreach (var file in nft.XcavateMetadata.Files.Where(file =>
                        !string.IsNullOrWhiteSpace(file)
                        && file.Length > 5
                        && (file.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                            || file.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                            || file.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                        && file[0] == '5'
                    ))
                    {
                        const string bucketName = "real-marketplace-properties";

                        var presignedUrl = await S3Model.GeneratePresignedURLAsync(s3Client, bucketName, file);

                        images.Add(presignedUrl);
                    }
                    nft.XcavateMetadata.Files = images;
                }

                if (nft.Metadata is not null && string.IsNullOrWhiteSpace(nft.Metadata.Image))
                {
                    nft.Metadata.Image = "noimage.png";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("To Xcavate nft wrapper error:");
                Console.WriteLine(ex);
            }

            var endpointKey = PlutoFrameworkCore.NftModel.GetEndpointKey(nft.Type);
            var substrateClient = await SubstrateClientModel.GetOrAddSubstrateClientAsync(endpointKey, token);

            uint blockNumber = (uint)await BlockModel.GetCachedBlockNumberAsync(substrateClient, token).ConfigureAwait(false);

            var ongoingObjectListing = ((INftXcavateOngoingObjectListing)nft).OngoingObjectListingDetails;

            uint listingExpiry = ongoingObjectListing?.ListingExpiry ?? 0;
            uint claimExpiry = ongoingObjectListing?.ClaimExpiry ?? 0;

            var tokensBought = ongoingObjectListing?.ShareOwners?.Count() == 1 ? ongoingObjectListing.ShareOwners.First().Value.ShareAmount : 0u;
            var tokensOwned = ((INftXcavateRealWorldAssetDetails)nft).RealWorldAssetDetails?.ShareOwners.Count() == 1 ? ((INftXcavateRealWorldAssetDetails)nft).RealWorldAssetDetails?.ShareOwners.First().Value.ShareAmount ?? 0u : 0u;

            return new XcavateNftWrapper
            {
                TokensBought = tokensBought,
                TokensOwned = tokensOwned,
                Favourite = await XcavatePropertyDatabase.IsPropertyFavouriteAsync(nft.Type, nft.CollectionId, nft.Id).ConfigureAwait(false),
                NftBase = nft,
                Region = ((INftXcavateNftMarketplace)nft).NftMarketplaceDetails != null ? await RegionModel.GetCachedRegionAsync(substrateClient, ((INftXcavateNftMarketplace)nft).NftMarketplaceDetails!.Region, token) : null,
                ListingHasExpired = blockNumber > listingExpiry,
                TimeLeftToBuy = blockNumber <= listingExpiry ? TimeSpan.FromSeconds(6 * (listingExpiry - blockNumber)) : null,
                ClaimHasExpired = blockNumber > claimExpiry,
                TimeLeftToClaim = blockNumber <= claimExpiry ? TimeSpan.FromSeconds(6 * (claimExpiry - blockNumber)) : null,
                SpvCreated = ((INftXcavateRealWorldAssetDetails)nft).RealWorldAssetDetails?.SpvCreated ?? true,
                Endpoint = Endpoints.GetEndpointDictionary[endpointKey]
            };
        }

        /// <summary>
        /// Wraps a listing read from the Xcavate Solana indexer. No S3 (there are no
        /// off-chain files yet), no Substrate client and no region cache: Solana deadlines
        /// are unix timestamps, so expiry is plain wall-clock arithmetic.
        /// </summary>
        public static async Task<XcavateNftWrapper> ToXcavateNftWrapperAsync(XcavateSolanaListingNft nft, CancellationToken token)
        {
            if (nft.Metadata is not null && string.IsNullOrWhiteSpace(nft.Metadata.Image))
            {
                nft.Metadata.Image = "noimage.png";
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // A listing that is not open for sale (pending assets, cancelled, refunding)
            // counts as expired outright: no countdown chip, nothing offering to buy.
            var listingHasExpired = !nft.OpenForSale || now > nft.ListingExpiryTimestamp;
            var claimHasExpired = nft.ClaimDeadlineTimestamp != 0 && now > nft.ClaimDeadlineTimestamp;

            var solanaAddress = KeysModel.GetSolanaAddress();

            var tokensBought = solanaAddress is not null && nft.OngoingObjectListingDetails?.ShareOwners?.TryGetValue(solanaAddress, out var shareBuyer) == true
                ? shareBuyer.ShareAmount
                : 0u;
            var tokensOwned = solanaAddress is not null && nft.RealWorldAssetDetails?.ShareOwners?.TryGetValue(solanaAddress, out var shareOwner) == true
                ? shareOwner.ShareAmount
                : 0u;

            return new XcavateNftWrapper
            {
                TokensBought = tokensBought,
                TokensOwned = tokensOwned,
                Favourite = await XcavatePropertyDatabase.IsPropertyFavouriteAsync(nft.Type, nft.CollectionId, nft.Id).ConfigureAwait(false),
                NftBase = nft,
                // Regions live on Solana now; the Substrate region cache has nothing for them.
                Region = null,
                ListingHasExpired = listingHasExpired,
                TimeLeftToBuy = listingHasExpired ? null : TimeSpan.FromSeconds(nft.ListingExpiryTimestamp - now),
                ClaimHasExpired = claimHasExpired,
                TimeLeftToClaim = nft.ClaimDeadlineTimestamp == 0 || claimHasExpired ? null : TimeSpan.FromSeconds(nft.ClaimDeadlineTimestamp - now),
                SpvCreated = nft.RealWorldAssetDetails?.SpvCreated ?? true,
                // Still the Substrate endpoint: the detail page's buy flow is unmigrated.
                Endpoint = Endpoints.GetEndpointDictionary[EndpointEnum.XcavatePaseo],
            };
        }

        public static async Task NavigateToPropertyDetailPageAsync(XcavateNftWrapper nft, CancellationToken token)
        {
            var loadingViewModel = await MainThread.InvokeOnMainThreadAsync(() =>
            {
                var viewModel = DependencyService.Get<FullPageLoadingViewModel>();

                viewModel.IsVisible = true;
                viewModel.Message = "Gathering property details";

                return viewModel;
            });

            if (nft.NftBase is XcavateSolanaListingNft solanaListing)
            {
                // Solana-sourced items refresh from the Xcavate devnet indexer; the SubQuery
                // indexer below knows nothing about them. On failure the list-time data is
                // simply kept - stale beats no detail page.
                try
                {
                    var solanaAddress = KeysModel.GetSolanaAddress();

                    var freshListing = await XcavateMarketplaceIndexerModel.GetListingFullInfoAsync(
                            solanaListing.ListingId,
                            solanaAddress,
                            token)
                        .ConfigureAwait(false);

                    if (freshListing is not null)
                    {
                        nft.NftBase = freshListing;
                        nft.TokensBought = solanaAddress is not null && freshListing.OngoingObjectListingDetails?.ShareOwners?.TryGetValue(solanaAddress, out var shareBuyer) == true
                            ? shareBuyer.ShareAmount
                            : 0u;
                        nft.TokensOwned = solanaAddress is not null && freshListing.RealWorldAssetDetails?.ShareOwners?.TryGetValue(solanaAddress, out var shareOwner) == true
                            ? shareOwner.ShareAmount
                            : 0u;
                        nft.SpvCreated = freshListing.RealWorldAssetDetails?.SpvCreated ?? true;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to refresh the Solana listing: ");
                    Console.WriteLine(ex);
                }
            }
            else
            {
                var ownerAddress = KeysModel.GetSubstrateKey(ss58prefix: 0);

                var indexedProperty = await XcavateIndexerModel.GetPropertyFullInfoAsync(
                        checked((int)nft.Key.Item3),
                        ownerAddress)
                    .ConfigureAwait(false);

                if (indexedProperty is not null)
                {
                    nft.NftBase = indexedProperty;
                    nft.TokensBought = indexedProperty.OngoingObjectListingDetails?.ShareOwners?.TryGetValue(ownerAddress, out var shareBuyers) == true
                        ? shareBuyers.ShareAmount
                        : 0u;
                    nft.TokensOwned = indexedProperty.RealWorldAssetDetails?.ShareOwners?.TryGetValue(ownerAddress, out var shareOwner) == true
                        ? shareOwner.ShareAmount
                        : 0u;
                    nft.SpvCreated = indexedProperty.RealWorldAssetDetails?.SpvCreated ?? true;

                    var s3Client = GetOrCreateS3Client();

                    // Handle S3
                    if (s3Client is not null && indexedProperty.XcavateMetadata?.Files is not null)
                    {
                        var images = new List<string>();

                        foreach (var file in indexedProperty.XcavateMetadata.Files.Where(file =>
                            !string.IsNullOrWhiteSpace(file)
                            && file.Length > 5
                            && (file.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                                || file.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                                || file.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                            && file[0] == '5'
                        ))
                        {
                            const string bucketName = "real-marketplace-properties";

                            var presignedUrl = await S3Model.GeneratePresignedURLAsync(s3Client, bucketName, file);

                            images.Add(presignedUrl);
                        }
                        ((INftXcavateMetadata)nft.NftBase).XcavateMetadata?.Files = images;
                    }
                }
            }

            if (nft.NftBase is not INftXcavateMetadata || ((INftXcavateMetadata)nft.NftBase).XcavateMetadata is null || nft.NftBase is not INftXcavateNftMarketplace)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    var toast = Toast.Make($"Could not navigate to property id: {nft.Key.Item3.ToString() ?? "Unknown"}");
                    await toast.Show(token);

                    loadingViewModel.IsVisible = false;
                });

                return;
            }

            var viewModel = new PropertyDetailViewModel
            {
                Endpoint = nft.Endpoint!,
                Favourite = nft.Favourite,
                NftWrapper = nft,
                Metadata = ((INftXcavateMetadata)nft.NftBase).XcavateMetadata,
                ListingDetails = ((INftXcavateOngoingObjectListing)nft.NftBase).OngoingObjectListingDetails,
                Region = nft.Region,
                TokensBought = nft.TokensBought,
                TokensOwned = nft.TokensOwned,
            };

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                loadingViewModel.IsVisible = false;

                await NavigationModel.PushAsync(new PropertyDetailPage(viewModel));
            });
        }
    }
}
