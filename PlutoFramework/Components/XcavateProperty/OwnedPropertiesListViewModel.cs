using PlutoFramework.Components.Nft;
using PlutoFramework.Constants;
using PlutoFramework.Model;
using PlutoFramework.Model.Xcavate;
using PlutoFrameworkCore.Xcavate;
using System.Collections.ObjectModel;
using UniqueryPlus.Nfts;
using XcavatePaseo.NetApi.Generated;
using NftKey = (UniqueryPlus.NftTypeEnum, System.Numerics.BigInteger, System.Numerics.BigInteger);

namespace PlutoFramework.Components.XcavateProperty
{
    public partial class OwnedPropertiesListViewModel : BaseListViewModel<NftKey, XcavateNftWrapper>
    {
        private SubstrateClientExt? substrateClient;
        private string ownerAddress = string.Empty;
        private int offset;
        private bool hasMore;

        public void UpdateFavourite(INftXcavateBase nftBase, bool newValue)
        {
            if (ItemsDict.ContainsKey((nftBase.Type, nftBase.CollectionId, nftBase.Id)))
            {
                ItemsDict[(nftBase.Type, nftBase.CollectionId, nftBase.Id)].Favourite = newValue;
            }
        }

        public override string Title => "Owned properties";

        public override async Task InitialLoadAsync(CancellationToken token)
        {
            if (substrateClient is null || string.IsNullOrWhiteSpace(ownerAddress))
            {
                Loading = false;
                return;
            }

            Clear();

            await LoadMoreAsync(token).ConfigureAwait(false);
        }

        public override async Task LoadMoreAsync(CancellationToken token)
        {
            if (Loading || !hasMore || substrateClient is null)
            {
                return;
            }

            Loading = true;

            try
            {
                var page = await XcavateIndexerModel.GetOwnedAndBoughtPropertiesAsync(
                        substrateClient,
                        first: (int)LIMIT,
                        offset: offset,
                        tokenOwner: ownerAddress)
                    .ConfigureAwait(false);

                if (page.Count == 0)
                {
                    hasMore = false;
                    return;
                }

                offset += page.Count;

                foreach (var property in page)
                {
                    var newProperty = await XcavatePropertyModel.ToXcavateNftWrapperAsync(property, token);

                    if (!ItemsDict.ContainsKey(newProperty.Key))
                    {
                        ItemsDict.Add(newProperty.Key, newProperty);

                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            Items.Add(newProperty);
                        });
                    }
                }

                if (page.Count < LIMIT)
                {
                    hasMore = false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Owned indexed properties list error:");
                Console.WriteLine(ex);
                hasMore = false;
            }
            finally
            {
                Loading = false;
            }
        }

        public async Task LoadAsync(PlutoFrameworkSubstrateClient client, CancellationToken token)
        {
            if (client.Endpoint.Key != EndpointEnum.XcavatePaseo)
            {
                return;
            }

            if (!KeysModel.HasSubstrateKey())
            {
                Loading = false;
                return;
            }

            var loadedAddress = KeysModel.GetSubstrateKey();
            var newClient = (SubstrateClientExt)client.SubstrateClient;
            var shouldReload = substrateClient is null || ownerAddress != loadedAddress || !ReferenceEquals(substrateClient, newClient);

            substrateClient = newClient;
            ownerAddress = loadedAddress;

            if (shouldReload)
            {
                await InitialLoadAsync(token).ConfigureAwait(false);
            }
        }

        private void Clear()
        {
            offset = 0;
            hasMore = true;
            ItemsDict = new Dictionary<NftKey, XcavateNftWrapper>();
            Items = new ObservableCollection<XcavateNftWrapper>();
        }
    }
}