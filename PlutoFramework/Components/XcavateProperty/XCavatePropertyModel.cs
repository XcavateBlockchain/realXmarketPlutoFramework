using Amazon;
using Amazon.S3;
using CommunityToolkit.Maui.Alerts;
using Microsoft.Extensions.Configuration;
using PlutoFramework.Constants;
using PlutoFramework.Model;
using PlutoFramework.Model.SQLite;
using PlutoFramework.Model.Xcavate;
using PlutoFrameworkCore.Xcavate;
using Substrate.NetApi.Model.Types.Primitive;
using UniqueryPlus.Nfts;

namespace PlutoFramework.Components.XcavateProperty
{
    public class XcavatePropertyModel
    {
        public static async Task<XcavateNftWrapper> ToXcavateNftWrapperAsync(INftXcavateBase nft, CancellationToken token)
        {
            try
            {
                var configuration = MauiAppBuilderExtensions.Services.GetService<IConfiguration>();

                RegionEndpoint region = RegionEndpoint.EUWest1;

                IAmazonS3 s3Client = new AmazonS3Client(
                    configuration.GetValue<string>("DYNAMO_ACCESS_KEY"),
                    configuration.GetValue<string>("DYNAMO_SECRET_KEY"),
                    region);

                // Handle S3
                if (nft?.XcavateMetadata?.Files is not null)
                {
                    var images = new List<string>();

                    foreach (var file in nft.XcavateMetadata.Files.Where(file =>
                        file != null && file.Length > 5 && (file.Contains(".jpg") || file.Contains(".jpeg") || file.Contains(".png")) && file[0] == '5'
                    ))
                    {
                        const string bucketName = "real-marketplace-properties";

                        var presignedUrl = await S3Model.GeneratePresignedURLAsync(s3Client, bucketName, file);

                        images.Add(presignedUrl);
                    }
                    nft.XcavateMetadata.Files = images;
                }
                else
                {
                    Console.WriteLine("Nft was null: " + nft is null);
                }

                Console.WriteLine("Nft: ");

                Console.WriteLine(nft);

                if (nft.Metadata is not null && nft.Metadata.Image is null)
                {
                    nft.Metadata.Image = "noimage.png";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("To Xcavate nft wrapper error:");
                Console.WriteLine(ex);
            }

            var substrateClient = await SubstrateClientModel.GetOrAddSubstrateClientAsync(PlutoFrameworkCore.NftModel.GetEndpointKey(nft.Type), token);

            uint blockNumber = (uint)await BlockModel.GetCachedBlockNumberAsync(substrateClient, token).ConfigureAwait(false);

            uint listingExpiry = ((INftXcavateOngoingObjectListing)nft).OngoingObjectListingDetails?.ListingExpiry ?? 0;
            uint claimExpiry = ((INftXcavateOngoingObjectListing)nft).OngoingObjectListingDetails?.ClaimExpiry ?? 0;

            return new XcavateNftWrapper
            {
                TokensBought = 0,
                TokensOwned = 0,
                Favourite = await XcavatePropertyDatabase.IsPropertyFavouriteAsync(nft.Type, nft.CollectionId, nft.Id).ConfigureAwait(false),
                NftBase = nft,
                Region = ((INftXcavateNftMarketplace)nft).NftMarketplaceDetails != null ? await RegionModel.GetCachedRegionAsync(substrateClient, ((INftXcavateNftMarketplace)nft).NftMarketplaceDetails!.Region, token) : null,
                ListingHasExpired = blockNumber > listingExpiry,
                TimeLeftToBuy = blockNumber <= listingExpiry ? TimeSpan.FromSeconds(6 * (listingExpiry - blockNumber)) : null,
                TimeLeftToClaim = blockNumber <= claimExpiry ? TimeSpan.FromSeconds(6 * (claimExpiry - blockNumber)) : null,
                SpvCreated = ((INftXcavateRealWorldAssetDetails)nft).RealWorldAssetDetails?.SpvCreated ?? true,
                Endpoint = Endpoints.GetEndpointDictionary[PlutoFrameworkCore.NftModel.GetEndpointKey(nft.Type)]
            };
        }

        public static async Task NavigateToPropertyDetailPageAsync(XcavateNftWrapper nft, CancellationToken token)
        {
            if (nft.NftBase is SavedXcavatePropertyBase)
            {
                nft.NftBase = await nft.NftBase.GetFullAsync(token);
            }

            if (nft.NftBase is not INftXcavateMetadata || ((INftXcavateMetadata)nft.NftBase).XcavateMetadata is null || nft.NftBase is not INftXcavateNftMarketplace)
            {
                var toast = Toast.Make($"Could not navigate to property id: {nft.Key.Item3.ToString() ?? "Unknown"}");
                await toast.Show();

                return;
            }

            var viewModel = new PropertyDetailViewModel
            {
                Endpoint = nft.Endpoint!,
                Favourite = nft.Favourite,
                NftBase = nft.NftBase,
                Metadata = ((INftXcavateMetadata)nft.NftBase).XcavateMetadata,
                ListingDetails = ((INftXcavateOngoingObjectListing)nft.NftBase).OngoingObjectListingDetails,
                Region = nft.Region,
                ListingHasExpired = nft.ListingHasExpired,
                TimeLeftToBuy = nft.TimeLeftToBuy,
            };

            if (XcavateOwnedPropertiesModel.ItemsDict.TryGetValue(nft.Key, out PropertyOwnership? tokenInfo))
            {
                viewModel.TokensOwned = tokenInfo?.TokensOwned ?? 0;
                viewModel.TokensBought = tokenInfo?.TokensBought ?? 0;
            }

            await NavigationModel.PushAsync(new PropertyDetailPage(viewModel));

            // Loads after the push
            var substrateClient = await SubstrateClientModel.GetOrAddSubstrateClientAsync(nft.Endpoint.Key, token);
            var tokensOwned = await RealWorldAssetsModel.GetRealWorldAssetTokensOwnedAsync((XcavatePaseo.NetApi.Generated.SubstrateClientExt)substrateClient.SubstrateClient, new U32((uint)nft.Key.Item3), KeysModel.GetSubstrateKey(), token);

            viewModel.TokensOwned = tokensOwned;
        }
    }
}
