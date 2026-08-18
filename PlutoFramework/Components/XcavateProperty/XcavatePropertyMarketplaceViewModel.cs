using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Components.Nft;
using PlutoFramework.Constants;
using PlutoFramework.Model;
using PlutoFramework.Model.SQLite;
using PlutoFramework.Model.Xcavate;
using PlutoFrameworkCore.Xcavate;
using System.Collections.ObjectModel;
using UniqueryPlus.Nfts;
using XcavatePaseo.NetApi.Generated;
using NftKey = (UniqueryPlus.NftTypeEnum, System.Numerics.BigInteger, System.Numerics.BigInteger);

namespace PlutoFramework.Components.XcavateProperty
{
    public partial class XcavatePropertyMarketplaceViewModel : BaseListViewModel<NftKey, XcavateNftWrapper>
    {
        [ObservableProperty]
        private bool isRefreshing = false;

        public override string Title => "Property Marketplace";

        //private List<Task<PlutoFrameworkSubstrateClient>> clientTasks;

        private IAsyncEnumerator<INftXcavateBase>? uniqueryNftEnumerator = null;

        public void UpdateFavourite(INftXcavateBase nftBase, bool newValue)
        {
            if (ItemsDict.ContainsKey((nftBase.Type, nftBase.CollectionId, nftBase.Id)))
            {
                ItemsDict[(nftBase.Type, nftBase.CollectionId, nftBase.Id)].Favourite = newValue;
            }
        }

        public override async Task LoadMoreAsync(CancellationToken token)
        {
            if (Loading)
            {
                return;
            }

            if (uniqueryNftEnumerator == null)
            {
                return;
            }

            Loading = true;

            try
            {
                for (uint i = 0; i < LIMIT; i++)
                {

                    if (uniqueryNftEnumerator != null && await uniqueryNftEnumerator.MoveNextAsync().ConfigureAwait(false))
                    {
                        XcavateNftWrapper newNft = await XcavatePropertyModel.ToXcavateNftWrapperAsync((INftXcavateBase)uniqueryNftEnumerator.Current, token);

                        if (!ItemsDict.ContainsKey(newNft.Key))
                        {
                            ItemsDict.Add(newNft.Key, newNft);

                            // Save to DB
                            try
                            {
                                await XcavatePropertyDatabase.SavePropertyAsync(newNft).ConfigureAwait(false);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Error saving to DB: ");
                                Console.WriteLine(ex);

                                await XcavatePropertyDatabase.DropAsync().ConfigureAwait(false);
                            }

                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                Items.Add(newNft);
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Nft list error: ");
                Console.WriteLine(ex);
            }

            Loading = false;

        }

        public override async Task InitialLoadAsync(CancellationToken token)
        {
            Loading = true;

            var uniqueryNftEnumerable = PropertyMarketplaceModel.GetPropertiesAsync(
                            (SubstrateClientExt)(await SubstrateClientModel.GetOrAddSubstrateClientAsync(EndpointEnum.XcavatePaseo, token).ConfigureAwait(false)).SubstrateClient,
                            limit: LIMIT
                        );

            uniqueryNftEnumerator = uniqueryNftEnumerable.GetAsyncEnumerator(token);

            //await LoadSavedPropertiesAsync().ConfigureAwait(false);

            Loading = false;

            await LoadMoreAsync(token).ConfigureAwait(false);



            Console.WriteLine("initial load done");
        }

        [RelayCommand]
        public async Task RefreshAsync()
        {
            IsRefreshing = true;

            Clear();

            await InitialLoadAsync(CancellationToken.None);

            IsRefreshing = false;
        }

        private void Clear()
        {
            uniqueryNftEnumerator = null;

            ItemsDict = new Dictionary<NftKey, XcavateNftWrapper>();

            Items = new ObservableCollection<XcavateNftWrapper>();
        }

        /*private async Task LoadSavedPropertiesAsync()
        {
            foreach (var savedNft in await XcavatePropertyDatabase.GetPropertiesAsync().ConfigureAwait(false))
            {
                if (!ItemsDict.ContainsKey(savedNft.Key))
                {
                    ItemsDict.Add(savedNft.Key, savedNft);

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        Items.Add(savedNft);
                    });
                }
            }
        }*/
    }
}
