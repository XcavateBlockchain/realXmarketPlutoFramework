using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Components.Nft;
using PlutoFramework.Constants;
using PlutoFramework.Model;
using PlutoFramework.Model.SQLite;
using PlutoFramework.Model.Xcavate;
using PlutoFrameworkCore.Xcavate;
using System;
using System.Collections.ObjectModel;
using UniqueryPlus.Nfts;
using XcavatePaseo.NetApi.Generated;
using NftKey = (UniqueryPlus.NftTypeEnum, System.Numerics.BigInteger, System.Numerics.BigInteger);

namespace PlutoFramework.Components.XcavateProperty
{
    public partial class XcavateIndexedPropertyMarketplaceViewModel : BaseListViewModel<NftKey, XcavateNftWrapper>
    {
        [ObservableProperty]
        private bool isRefreshing = false;

        private readonly PropertyMarketplaceFilterPopupViewModel filterPopupViewModel;
        private SubstrateClientExt? substrateClient;
        private int offset = 0;
        private bool hasMore = true;

        private string includesTownCity = string.Empty;
        private string includesPropertyType = string.Empty;
        private string includesPropertyName = string.Empty;

        public override string Title => "Property Marketplace";

        public XcavateIndexedPropertyMarketplaceViewModel()
        {
            filterPopupViewModel = DependencyService.Get<PropertyMarketplaceFilterPopupViewModel>();
            filterPopupViewModel.ApplyRequested = ApplyFiltersAsync;
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
                var results = await XcavateIndexerModel.GetMarketplaceListedPropertiesAsync(
                        substrateClient,
                        first: (int)LIMIT,
                        offset: offset,
                        includesTownCity: includesTownCity,
                        includesPropertyType: includesPropertyType,
                        includesPropertyName: includesPropertyName)
                    .ConfigureAwait(false);

                if (results.Count == 0)
                {
                    hasMore = false;
                    Loading = false;
                    return;
                }

                offset += results.Count;

                foreach (var result in results)
                {
                    XcavateNftWrapper newNft = await XcavatePropertyModel.ToXcavateNftWrapperAsync(result, token);

                    if (!ItemsDict.ContainsKey(newNft.Key))
                    {
                        ItemsDict.Add(newNft.Key, newNft);

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
            catch (Exception ex)
            {
                Console.WriteLine("Indexed nft list error: ");
                Console.WriteLine(ex);
            }

            Loading = false;
        }

        public override async Task InitialLoadAsync(CancellationToken token)
        {
            Loading = true;

            if (substrateClient is null)
            {
                substrateClient = (SubstrateClientExt)(await SubstrateClientModel.GetOrAddSubstrateClientAsync(EndpointEnum.XcavatePaseo, token).ConfigureAwait(false)).SubstrateClient;
            }

            offset = 0;
            hasMore = true;

            Loading = false;

            await LoadMoreAsync(token).ConfigureAwait(false);
        }

        [RelayCommand]
        public async Task RefreshAsync()
        {
            IsRefreshing = true;

            Clear();

            await InitialLoadAsync(CancellationToken.None);

            IsRefreshing = false;
        }

        [RelayCommand]
        private void OpenFilter()
        {
            filterPopupViewModel.IsVisible = true;
        }

        private async Task ApplyFiltersAsync()
        {
            includesTownCity = NormalizeFilterValue(filterPopupViewModel.SelectedTownCity);
            includesPropertyType = NormalizeFilterValue(filterPopupViewModel.SelectedPropertyType);
            includesPropertyName = filterPopupViewModel.SearchText?.Trim() ?? string.Empty;

            await RefreshAsync().ConfigureAwait(false);

            filterPopupViewModel.IsVisible = false;
        }

        private static string NormalizeFilterValue(string value)
        {
            return string.Equals(value, "All", StringComparison.OrdinalIgnoreCase) ? string.Empty : value;
        }

        private void Clear()
        {
            ItemsDict = new Dictionary<NftKey, XcavateNftWrapper>();
            Items = new ObservableCollection<XcavateNftWrapper>();
            offset = 0;
            hasMore = true;
        }
    }
}
