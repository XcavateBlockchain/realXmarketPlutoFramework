using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Components.Nft;
using PlutoFramework.Constants;
using PlutoFramework.Model;
using PlutoFramework.Model.SQLite;
using PlutoFramework.Model.Xcavate;
using PlutoFrameworkCore.Xcavate;
using System.Collections.ObjectModel;
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
        private bool clientLoaded;
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

        /// <summary>True while the list is still empty and loading (initial load / refresh).</summary>
        public bool ShowSkeleton => Loading && Items.Count == 0;

        /// <summary>True when a search text or a town/type filter is currently applied.</summary>
        public bool HasActiveFilter =>
            !string.IsNullOrEmpty(includesPropertyName)
            || !string.IsNullOrEmpty(includesTownCity)
            || !string.IsNullOrEmpty(includesPropertyType);

        /// <summary>
        /// The empty-state caption. The filter-specific wording is only used when a filter is
        /// actually applied; otherwise the generic wording is shown.
        /// </summary>
        public string NoItemsMessage => HasActiveFilter
            ? "No properties were found for this filter. Try to search for something different."
            : "No properties were found";

        public XcavateIndexedPropertyMarketplaceViewModel()
        {
            filterPopupViewModel = DependencyService.Get<PropertyMarketplaceFilterPopupViewModel>();
            filterPopupViewModel.ApplyRequested = ApplyFiltersAsync;
            searchText = filterPopupViewModel.SearchText;

            // ShowSkeleton depends on the base-class Loading flag and the Items count, neither of
            // which auto-notifies this derived property. Re-raise it (on the main thread) whenever
            // either changes so the skeleton shows only while the list is empty and loading.
            PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(Loading))
                {
                    MainThread.BeginInvokeOnMainThread(() => OnPropertyChanged(nameof(ShowSkeleton)));
                }
            };
            Items.CollectionChanged += (sender, e) =>
                MainThread.BeginInvokeOnMainThread(() => OnPropertyChanged(nameof(ShowSkeleton)));
        }

        public override async Task LoadMoreAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            if (!hasMore || !clientLoaded)
            {
                return;
            }

            await loadMoreSemaphore.WaitAsync(token).ConfigureAwait(false);

            try
            {
                // Do not gate on Loading here: a refresh sets Loading=true up front (before the
                // list is cleared) to keep the empty-state caption suppressed, and that must not
                // block this load. The semaphore already serializes concurrent loads.
                if (!hasMore || !clientLoaded)
                {
                    return;
                }

                Loading = true;

                // The devnet indexer has no server-side text filters, so the old
                // town/type/name filters apply here, to each fetched page. A raw page can
                // filter down to nothing while deeper pages still match, and a scroll that
                // appends nothing never re-fires the CollectionView's remaining-items
                // threshold - so this keeps fetching raw pages until something passes or
                // the feed ends.
                var newItems = new List<XcavateNftWrapper>();

                while (newItems.Count == 0 && hasMore)
                {
                    var results = await XcavateMarketplaceIndexerModel.GetMarketplaceListedPropertiesAsync(
                            first: (int)LIMIT,
                            offset: offset,
                            token)
                        .ConfigureAwait(false);

                    token.ThrowIfCancellationRequested();

                    if (results.Count == 0)
                    {
                        hasMore = false;
                        break;
                    }

                    offset += results.Count;

                    if (results.Count < LIMIT)
                    {
                        hasMore = false;
                    }

                    // Open listings show; so does a torn-down listing that still holds
                    // someone's reservation - hiding it would hide the refund path.
                    // Pre-sale (PENDING_ASSETS) rows and fully settled dead listings stay
                    // out of the feed.
                    var matchingResults = results
                        .Where(result => (result.OpenForSale
                                || (result.OngoingObjectListingDetails?.UnclaimedTokens ?? 0) > 0)
                            && XcavateMarketplaceIndexerModel.MatchesFilter(
                                result,
                                includesTownCity,
                                includesPropertyType,
                                includesPropertyName))
                        .ToList();

                    var wrappedResults = await Task.WhenAll(
                        matchingResults.Select(result => XcavatePropertyModel.ToXcavateNftWrapperAsync(result, token)))
                        .ConfigureAwait(false);

                    token.ThrowIfCancellationRequested();

                    foreach (var newNft in wrappedResults)
                    {
                        if (!ItemsDict.ContainsKey(newNft.Key))
                        {
                            ItemsDict.Add(newNft.Key, newNft);
                            newItems.Add(newNft);
                        }
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
                clientLoaded = true;

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
                // Keep the empty-state caption suppressed while the list is cleared and reloaded;
                // otherwise NoItems flips on for the moment the list is empty, showing the
                // "no properties" message even though items are about to be (re)loaded.
                Loading = true;
                Clear();
                await InitialLoadAsync(CancellationToken.None).ConfigureAwait(false);

                if (!IsActiveLoadingCanceled())
                {
                    RememberLoadedQuery();
                }

                // Refresh the empty-state caption now that the query has settled (this method can
                // run off the main thread via the debounced search path, so marshal it).
                MainThread.BeginInvokeOnMainThread(() => OnPropertyChanged(nameof(NoItemsMessage)));
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
