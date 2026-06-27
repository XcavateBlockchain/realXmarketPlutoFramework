using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Components.Nft;
using PlutoFramework.Constants;
using PlutoFramework.Model;
using PlutoFramework.Model.SQLite;
using PlutoFrameworkCore.Xcavate;
using System.Collections.ObjectModel;
using UniqueryPlus.Nfts;
using XcavatePaseo.NetApi.Generated;
using NftKey = (UniqueryPlus.NftTypeEnum, System.Numerics.BigInteger, System.Numerics.BigInteger);

namespace PlutoFramework.Components.XcavateProperty
{
    public partial class XcavateIndexedPropertyMarketplaceViewModel : BaseListViewModel<NftKey, XcavateNftWrapper>
    {
        public event Action? AutoSearchCompleted;

        [ObservableProperty]
        private bool isRefreshing = false;

        [ObservableProperty]
        private string searchText = string.Empty;

        private readonly PropertyMarketplaceFilterPopupViewModel filterPopupViewModel;
        private SubstrateClientExt? substrateClient;
        private int offset = 0;
        private bool hasMore = true;

        private string includesTownCity = string.Empty;
        private string includesPropertyType = string.Empty;
        private string includesPropertyName = string.Empty;
        private string lastLoadedSearchText = string.Empty;
        private string lastLoadedTownCity = string.Empty;
        private string lastLoadedPropertyType = string.Empty;
        private bool hasLoadedQuery;
        private readonly object searchDebounceLock = new();
        private readonly object loadingCancellationLock = new();
        private readonly SemaphoreSlim searchExecutionSemaphore = new(1, 1);
        private readonly SemaphoreSlim loadMoreSemaphore = new(1, 1);
        private CancellationTokenSource? searchDebounceCts;
        private CancellationTokenSource? activeLoadingCts;
        private bool isBackgroundHydrationRunning;

        public override string Title => "Property Marketplace";

        public XcavateIndexedPropertyMarketplaceViewModel()
        {
            filterPopupViewModel = DependencyService.Get<PropertyMarketplaceFilterPopupViewModel>();
            filterPopupViewModel.ApplyRequested = ApplyFiltersAsync;
            searchText = filterPopupViewModel.SearchText;
        }

        public override async Task LoadMoreAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            if (!hasMore || substrateClient is null)
            {
                return;
            }

            await loadMoreSemaphore.WaitAsync(token).ConfigureAwait(false);

            try
            {
                if (Loading || !hasMore || substrateClient is null)
                {
                    return;
                }

                Loading = true;

                var results = await XcavateIndexerModel.GetMarketplaceListedPropertiesAsync(
                        substrateClient,
                        first: (int)LIMIT,
                        offset: offset,
                        includesTownCity: includesTownCity,
                        includesPropertyType: includesPropertyType,
                        includesPropertyName: includesPropertyName)
                    .ConfigureAwait(false);

                token.ThrowIfCancellationRequested();

                if (results.Count == 0)
                {
                    hasMore = false;
                    Loading = false;
                    return;
                }

                offset += results.Count;

                var wrappedResults = await Task.WhenAll(
                    results.Select(result => XcavatePropertyModel.ToXcavateNftWrapperAsync(result, token)))
                    .ConfigureAwait(false);

                token.ThrowIfCancellationRequested();

                var newItems = new List<XcavateNftWrapper>(wrappedResults.Length);
                foreach (var newNft in wrappedResults)
                {
                    if (!ItemsDict.ContainsKey(newNft.Key))
                    {
                        ItemsDict.Add(newNft.Key, newNft);
                        newItems.Add(newNft);
                    }
                }

                if (newItems.Count > 0)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (token.IsCancellationRequested)
                        {
                            return;
                        }

                        foreach (var newNft in newItems)
                        {
                            Items.Add(newNft);
                        }
                    });

                    _ = PersistPropertiesAsync(newItems);
                }

                if (results.Count < LIMIT)
                {
                    hasMore = false;
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when a refresh/navigation starts a newer load operation.
            }
            catch (Exception ex)
            {
                Console.WriteLine("Indexed nft list error: ");
                Console.WriteLine(ex);
            }
            finally
            {
                Loading = false;
                loadMoreSemaphore.Release();
            }
        }

        public override async Task InitialLoadAsync(CancellationToken token)
        {
            token = StartNewLoadingOperation(token);

            if (Items.Count > 0 && IsSameLoadedQuery(includesPropertyName, includesTownCity, includesPropertyType))
            {
                return;
            }

            try
            {
                if (substrateClient is null)
                {
                    substrateClient = (SubstrateClientExt)(await SubstrateClientModel.GetOrAddSubstrateClientAsync(EndpointEnum.XcavatePaseo, token).ConfigureAwait(false)).SubstrateClient;
                }

                token.ThrowIfCancellationRequested();

                offset = 0;
                hasMore = true;

                await LoadMoreAsync(token).ConfigureAwait(false);
                _ = HydrateRemainingAsync(token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        [RelayCommand]
        public async Task RefreshAsync()
        {
            await RefreshInternalAsync(showRefreshIndicator: true).ConfigureAwait(false);
        }

        [RelayCommand]
        private void OpenFilter()
        {
            filterPopupViewModel.IsVisible = true;
        }

        [RelayCommand]
        private async Task SearchAsync()
        {
            CancelPendingDebouncedSearch();
            await ExecuteSearchAsync(SearchText, CancellationToken.None, force: false).ConfigureAwait(false);
        }

        private async Task ApplyFiltersAsync()
        {
            includesTownCity = NormalizeFilterValue(filterPopupViewModel.SelectedTownCity);
            includesPropertyType = NormalizeFilterValue(filterPopupViewModel.SelectedPropertyType);
            SearchText = filterPopupViewModel.SearchText?.Trim() ?? string.Empty;
            includesPropertyName = SearchText;

            if (!IsSameLoadedQuery(includesPropertyName, includesTownCity, includesPropertyType))
            {
                await RefreshAsync().ConfigureAwait(false);
            }

            filterPopupViewModel.IsVisible = false;
        }

        partial void OnSearchTextChanged(string value)
        {
            filterPopupViewModel.SearchText = value ?? string.Empty;
            _ = DebouncedSearchAsync(value ?? string.Empty);
        }

        private async Task DebouncedSearchAsync(string currentSearchText)
        {
            var token = CreateDebounceToken();

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1), token).ConfigureAwait(false);
                await ExecuteSearchAsync(currentSearchText, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected when the user keeps typing.
            }
        }

        private async Task ExecuteSearchAsync(string currentSearchText, CancellationToken token, bool force = false)
        {
            var normalizedSearchText = currentSearchText?.Trim() ?? string.Empty;

            if (IsSameLoadedQuery(normalizedSearchText, includesTownCity, includesPropertyType))
            {
                return;
            }

            await searchExecutionSemaphore.WaitAsync(token).ConfigureAwait(false);

            try
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                includesPropertyName = normalizedSearchText;
                filterPopupViewModel.SearchText = normalizedSearchText;

                await RefreshInternalAsync(showRefreshIndicator: false).ConfigureAwait(false);

                if (!force)
                {
                    AutoSearchCompleted?.Invoke();
                }
            }
            finally
            {
                searchExecutionSemaphore.Release();
            }
        }

        private async Task RefreshInternalAsync(bool showRefreshIndicator)
        {
            if (showRefreshIndicator)
            {
                IsRefreshing = true;
            }

            try
            {
                Clear();
                await InitialLoadAsync(CancellationToken.None).ConfigureAwait(false);

                if (!IsActiveLoadingCanceled())
                {
                    RememberLoadedQuery();
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when replaced by a newer refresh or when page disappears.
            }
            finally
            {
                if (showRefreshIndicator)
                {
                    IsRefreshing = false;
                }
            }
        }

        private CancellationToken CreateDebounceToken()
        {
            CancellationTokenSource newDebounceCts;

            lock (searchDebounceLock)
            {
                searchDebounceCts?.Cancel();
                searchDebounceCts?.Dispose();

                newDebounceCts = new CancellationTokenSource();
                searchDebounceCts = newDebounceCts;
            }

            return newDebounceCts.Token;
        }

        private void CancelPendingDebouncedSearch()
        {
            lock (searchDebounceLock)
            {
                searchDebounceCts?.Cancel();
                searchDebounceCts?.Dispose();
                searchDebounceCts = null;
            }
        }

        public void CancelPendingOperations()
        {
            CancelPendingDebouncedSearch();
            CancelActiveLoading();
        }

        private CancellationToken StartNewLoadingOperation(CancellationToken token)
        {
            lock (loadingCancellationLock)
            {
                activeLoadingCts?.Cancel();
                activeLoadingCts?.Dispose();

                activeLoadingCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                return activeLoadingCts.Token;
            }
        }

        private void CancelActiveLoading()
        {
            lock (loadingCancellationLock)
            {
                activeLoadingCts?.Cancel();
                activeLoadingCts?.Dispose();
                activeLoadingCts = null;
            }

            isBackgroundHydrationRunning = false;
        }

        private bool IsActiveLoadingCanceled()
        {
            lock (loadingCancellationLock)
            {
                return activeLoadingCts?.IsCancellationRequested ?? false;
            }
        }

        private static string NormalizeFilterValue(string value)
        {
            return string.Equals(value, "All", StringComparison.OrdinalIgnoreCase) ? string.Empty : value;
        }

        private bool IsSameLoadedQuery(string searchText, string townCity, string propertyType)
        {
            return hasLoadedQuery
                && string.Equals(lastLoadedSearchText, searchText ?? string.Empty, StringComparison.Ordinal)
                && string.Equals(lastLoadedTownCity, townCity ?? string.Empty, StringComparison.Ordinal)
                && string.Equals(lastLoadedPropertyType, propertyType ?? string.Empty, StringComparison.Ordinal);
        }

        private void RememberLoadedQuery()
        {
            lastLoadedSearchText = includesPropertyName;
            lastLoadedTownCity = includesTownCity;
            lastLoadedPropertyType = includesPropertyType;
            hasLoadedQuery = true;
        }

        private void Clear()
        {
            ItemsDict.Clear();
            Items.Clear();
            offset = 0;
            hasMore = true;
            isBackgroundHydrationRunning = false;
        }

        private async Task HydrateRemainingAsync(CancellationToken token)
        {
            if (isBackgroundHydrationRunning)
            {
                return;
            }

            isBackgroundHydrationRunning = true;

            try
            {
                while (hasMore)
                {
                    token.ThrowIfCancellationRequested();
                    await LoadMoreAsync(token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when a new search/refresh starts.
            }
            finally
            {
                isBackgroundHydrationRunning = false;
            }
        }

        private static async Task PersistPropertiesAsync(IReadOnlyList<XcavateNftWrapper> items)
        {
            try
            {
                foreach (var item in items)
                {
                    await XcavatePropertyDatabase.SavePropertyAsync(item).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error saving to DB: ");
                Console.WriteLine(ex);

                await XcavatePropertyDatabase.DropAsync().ConfigureAwait(false);
            }
        }
    }
}
