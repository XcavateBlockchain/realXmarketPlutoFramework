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
using Substrate.NetApi.Model.Types.Primitive;
using UniqueryPlus.Nfts;
using XcavatePaseo.NetApi.Generated;

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

        public static async Task NavigateToPropertyDetailPageAsync(XcavateNftWrapper nft, CancellationToken token)
        {
            var loadingViewModel = await MainThread.InvokeOnMainThreadAsync(() =>
            {
                var viewModel = DependencyService.Get<FullPageLoadingViewModel>();

                viewModel.IsVisible = true;
                viewModel.Message = "Gathering property details";

                return viewModel;
            });

            var ownerAddress = KeysModel.GetSubstrateKey(ss58prefix: 0);

            var substrateClient = await SubstrateClientModel.GetOrAddSubstrateClientAsync(nft.Endpoint.Key, token);

            var indexedProperty = await XcavateIndexerModel.GetPropertyFullInfoAsync(
                    (SubstrateClientExt)substrateClient.SubstrateClient,
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

            // Did not work so far...
            // Tried hybrid data loading
            //await LoadPropertyDetailFromOnChainAsync(nft, viewModel, token);
        }


        public static async Task LoadPropertyDetailFromOnChainAsync(XcavateNftWrapper nft, PropertyDetailViewModel viewModel, CancellationToken token)
        {
            Console.WriteLine("Getting full property from onchain");
            try
            {
                var substrateClient = await SubstrateClientModel.GetOrAddSubstrateClientAsync(nft.Endpoint.Key, token);

                var nftBase = await XcavatePaseoNftModel.GetNftNftsPalletByIdAsync((XcavatePaseo.NetApi.Generated.SubstrateClientExt)substrateClient.SubstrateClient, (uint)nft.NftBase.CollectionId, (uint)nft.NftBase.Id, token);
                nft.NftBase = await nftBase!.GetFullAsync(token);

                Console.WriteLine("Got full");

                viewModel.SpvCreated = (((INftXcavateRealWorldAssetDetails)nft).RealWorldAssetDetails)?.SpvCreated ?? false;

                if (nft.NftBase is not INftXcavateMetadata || ((INftXcavateMetadata)nft.NftBase).XcavateMetadata is null || nft.NftBase is not INftXcavateNftMarketplace)
                {
                    Console.WriteLine("It was null probably?");

                    return;
                }

                viewModel.Endpoint = nft.Endpoint!;
                viewModel.NftWrapper = nft;
                viewModel.Metadata = ((INftXcavateMetadata)nft.NftBase).XcavateMetadata;
                viewModel.ListingDetails = ((INftXcavateOngoingObjectListing)nft.NftBase).OngoingObjectListingDetails;
                viewModel.Region = nft.Region;
                viewModel.SpvCreated = (((INftXcavateRealWorldAssetDetails)nft).RealWorldAssetDetails)?.SpvCreated ?? true;

                if (XcavateOwnedPropertiesModel.ItemsDict.TryGetValue(nft.Key, out PropertyOwnership? tokenInfo))
                {
                    viewModel.TokensOwned = tokenInfo?.TokensOwned ?? 0;
                    viewModel.TokensBought = tokenInfo?.TokensBought ?? 0;
                }

                var tokensOwned = await RealWorldAssetsModel.GetRealWorldAssetTokensOwnedAsync((XcavatePaseo.NetApi.Generated.SubstrateClientExt)substrateClient.SubstrateClient, new U32((uint)nft.Key.Item3), KeysModel.GetSubstrateKey(), token);

                viewModel.TokensOwned = tokensOwned;

                var roles = await WhitelistModel.GetRolesAsync((XcavatePaseo.NetApi.Generated.SubstrateClientExt)substrateClient.SubstrateClient, KeysModel.GetSubstrateKey(), token);

                viewModel.Roles = roles;

                Console.WriteLine("Getting Full On-Chain property finished");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}
