using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Components.Nft;
using PlutoFramework.Model;
using PlutoFramework.Model.Currency;
using PlutoFramework.Model.SQLite;
using PlutoFramework.Model.Xcavate;
using PlutoFrameworkCore.Solana;
using PlutoFrameworkCore.Xcavate;
using System.Collections.ObjectModel;
using System.Globalization;
using NftKey = (UniqueryPlus.NftTypeEnum, System.Numerics.BigInteger, System.Numerics.BigInteger);

namespace PlutoFramework.Components.XcavateProperty
{
    /// <summary>
    /// The notice board: the secondary market's share listings, grouped into one card per
    /// property. Modeled on <see cref="XcavateIndexedPropertyMarketplaceViewModel"/> - the
    /// same BaseListViewModel paging, skeleton and empty-state slots, and the same refresh
    /// triggers (a confirmed marketplace transaction, a cluster switch).
    /// <para>
    /// The indexer's <c>shareListings</c> feed is one row per share listing, so a page is
    /// folded into per-asset aggregates first: the badge prints the totals (how many
    /// tokens are on offer, from what lowest price) and the card's tap context is the
    /// cheapest active listing. Each distinct asset's card model is the SAME record the
    /// marketplace builds - <see cref="XcavateMarketplaceIndexerModel.GetListingFullInfoAsync"/>
    /// with the asset id standing in for the listing id (they coincide on chain) - so the
    /// detail page behind a noticeboard card is the ordinary property page, only carrying
    /// a share-listing context.
    /// </para>
    /// </summary>
    public partial class XcavateIndexedPropertyNoticeboardViewModel : BaseListViewModel<NftKey, XcavateNftWrapper>
    {
        [ObservableProperty]
        private bool isRefreshing = false;

        /// <summary>
        /// What the CollectionView actually shows: the skeleton slot, the property cards
        /// (with their secondary-market badges) and the empty-state slot. Items stays the
        /// source of truth for the card list (ItemsDict, ShowSkeleton, NoItems); this only
        /// mirrors it - the same contract the marketplace view model documents for its
        /// feed, minus the scrolling header (this page's toggle is fixed chrome).
        /// </summary>
        public ObservableCollection<object> FeedItems { get; } = new ObservableCollection<object>();

        private readonly NoticeboardFeedSkeletonItem skeletonFeedItem;
        private readonly NoticeboardFeedEmptyStateItem emptyStateFeedItem;

        private bool clientLoaded;
        private int offset = 0;
        private bool hasMore = true;

        /// <summary>The running per-asset totals across every share listing folded in so far.</summary>
        private readonly Dictionary<long, ShareListingAggregate> aggregatesByAsset = new();

        /// <summary>
        /// The share listings already folded, by their on-chain id. Offset paging can
        /// hand the same row back twice when a new listing lands between pages (slot-DESC
        /// ordering shifts every later row down one), and folding one twice would
        /// double-count its tokens in the badge.
        /// </summary>
        private readonly HashSet<long> foldedShareListingIds = new();

        /// <summary>An asset's card key, so later pages can find and refresh its card.</summary>
        private readonly Dictionary<long, NftKey> keysByAsset = new();

        private readonly object loadingCancellationLock = new();
        private readonly SemaphoreSlim loadMoreSemaphore = new(1, 1);
        private CancellationTokenSource? activeLoadingCts;
        private bool isBackgroundHydrationRunning;

        public override string Title => "Property Notice Board";

        /// <summary>True while the list is still empty and loading (initial load / refresh).</summary>
        public bool ShowSkeleton => Loading && Items.Count == 0;

        /// <summary>
        /// The empty-state caption. A cluster with no marketplace deployment explains
        /// itself instead of pretending the secondary market is quiet.
        /// </summary>
        public string NoItemsMessage =>
            !XcavateDeploymentModel.IsDeployed(SolanaNetworkModel.SelectedCluster)
                ? XcavateDeploymentModel.NotDeployedMessage(SolanaNetworkModel.SelectedCluster)
                : "No tokens are listed on the secondary market yet";

        public XcavateIndexedPropertyNoticeboardViewModel()
        {
            skeletonFeedItem = new NoticeboardFeedSkeletonItem(this);
            emptyStateFeedItem = new NoticeboardFeedEmptyStateItem(this);

            ResetFeedItems();

            // ShowSkeleton depends on the base-class Loading flag and the Items count, neither of
            // which auto-notifies this derived property; NoItems/AnyItems only re-evaluate when
            // Items or Loading is reassigned, not on in-place collection mutations. Re-raise them
            // on the main thread whenever either changes - the same wiring the marketplace view
            // model documents.
            PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(Loading))
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        OnPropertyChanged(nameof(ShowSkeleton));
                        SyncFeedSlots();
                    });
                }
            };
            Items.CollectionChanged += (sender, e) =>
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    OnPropertyChanged(nameof(ShowSkeleton));
                    OnPropertyChanged(nameof(NoItems));
                    OnPropertyChanged(nameof(AnyItems));
                    SyncFeedSlots();
                });

            // A confirmed marketplace transaction (a relist added shares to the secondary
            // market, a delist or secondary buy took them off) changes the feed, so it must
            // be re-read. This view model is a session-wide singleton with no disposal hook,
            // so it never unsubscribes - the static event can only keep a singleton alive.
            XcavateMarketplaceTransactionModel.TransactionConfirmed += OnMarketplaceTransactionConfirmed;

            // Switching the Solana network swaps which deployment the feed reads. Same
            // singleton lifetime, same no-unsubscribe convention as the subscription above.
            SolanaNetworkModel.ClusterChanged += OnSolanaClusterChanged;
        }

        /// <summary>
        /// Runs on the main thread - the event already is - and fire-and-forget: the
        /// re-fetch is pure async I/O, so nothing blocks the UI while it runs.
        /// </summary>
        private void OnMarketplaceTransactionConfirmed(object? sender, EventArgs e) =>
            MainThread.BeginInvokeOnMainThread(() => _ = RefreshInBackgroundAsync());

        /// <summary>
        /// Runs on the main thread - the event already is - and fire-and-forget: the
        /// re-fetch is pure async I/O, so nothing blocks the UI while it runs.
        /// </summary>
        private void OnSolanaClusterChanged(object? sender, SolanaCluster cluster) =>
            MainThread.BeginInvokeOnMainThread(() => _ = RefreshInBackgroundAsync());

        /// <summary>
        /// Re-fetches the feed without the pull-to-refresh indicator, so a confirmed
        /// marketplace transaction updates the list quietly instead of spinning the header
        /// on a refresh the user did not ask for.
        /// </summary>
        public Task RefreshInBackgroundAsync() => RefreshInternalAsync(showRefreshIndicator: false);

        public override async Task LoadMoreAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            if (!hasMore || !clientLoaded)
            {
                return;
            }

            // The secondary market exists only where the Xcavate programs are deployed;
            // anywhere else the feed is the placeholder caption, not an error.
            if (!XcavateDeploymentModel.IsDeployed(SolanaNetworkModel.SelectedCluster))
            {
                hasMore = false;
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

                // A raw share-listing page can fold down to nothing new (listings of assets
                // already shown, or assets whose primary listing is gone), and a scroll that
                // appends nothing never re-fires the CollectionView's remaining-items
                // threshold - so this keeps fetching raw pages until a card is touched or the
                // feed ends, exactly how the marketplace feed survives its client-side filters.
                var cardsTouched = 0;

                while (cardsTouched == 0 && hasMore)
                {
                    var (shareListings, totalCount) = await XcavateMarketplaceIndexerModel
                        .GetMarketplaceShareListingsAsync(
                            SolanaNetworkModel.SelectedCluster,
                            first: (int)LIMIT,
                            offset: offset,
                            token)
                        .ConfigureAwait(false);

                    token.ThrowIfCancellationRequested();

                    if (shareListings.Count == 0)
                    {
                        hasMore = false;
                        break;
                    }

                    offset += shareListings.Count;
                    hasMore = offset < totalCount;

                    cardsTouched = await FoldPageIntoCardsAsync(shareListings, token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when a refresh/navigation starts a newer load operation.
            }
            catch (Exception ex)
            {
                Console.WriteLine("Noticeboard share listing error: ");
                Console.WriteLine(ex);
            }
            finally
            {
                loadMoreSemaphore.Release();

                // Clear the loading flag on the main thread, and only after the pending Items
                // mutation (posted earlier in the try block) has run - the same ordering the
                // marketplace feed relies on so the empty caption never flashes.
                MainThread.BeginInvokeOnMainThread(() => Loading = false);
            }
        }

        /// <summary>
        /// Folds one raw share-listing page into the per-asset aggregates, builds the card
        /// for every asset first seen on this page and rewrites the badge of every card
        /// the page moved. Returns how many cards were added or updated.
        /// </summary>
        private async Task<int> FoldPageIntoCardsAsync(
            IReadOnlyList<XcavateShareListingInfo> shareListings,
            CancellationToken token)
        {
            foreach (var shareListing in shareListings)
            {
                FoldIntoAggregate(shareListing);
            }

            var assetIds = shareListings
                .Select(shareListing => shareListing.AssetId)
                .Distinct()
                .ToList();

            var newAssetIds = assetIds.Where(assetId => !keysByAsset.ContainsKey(assetId)).ToList();

            // The card models are the marketplace's own: the asset's primary listing read
            // fresh (asset id == listing id on chain), wrapped the same way. One lookup per
            // distinct new asset, in parallel like the marketplace feed's wrapping.
            var investor = KeysModel.GetSolanaAddress();

            var newCards = await Task.WhenAll(newAssetIds.Select(async assetId =>
            {
                var listing = await XcavateMarketplaceIndexerModel.GetListingFullInfoAsync(
                        SolanaNetworkModel.SelectedCluster, assetId, investor, token)
                    .ConfigureAwait(false);

                if (listing is null)
                {
                    // The share listings outlived the primary listing's account (it was
                    // wound down); without the primary record there is no card to show.
                    Console.WriteLine($"Noticeboard: no primary listing for asset {assetId}; its share listings are skipped.");

                    return (assetId, wrapper: (XcavateNftWrapper?)null);
                }

                var wrapper = await XcavatePropertyModel.ToXcavateNftWrapperAsync(listing, token).ConfigureAwait(false);

                return (assetId, wrapper: (XcavateNftWrapper?)wrapper);
            })).ConfigureAwait(false);

            token.ThrowIfCancellationRequested();

            // The dictionaries mutate synchronously, off the UI thread - exactly how the
            // marketplace view model guards its ItemsDict - so the next page's fold (the
            // load semaphore only serializes the loads, not the posted UI work) already
            // sees this page's assets and cannot double-add them. The card items are
            // built synchronously too: reading the aggregates from the posted UI work
            // below would race the next page's fold.
            var cardsToAdd = new List<(NoticeboardPropertyItem Item, XcavateNftWrapper Wrapper)>();

            foreach (var (assetId, wrapper) in newCards)
            {
                if (wrapper is null || ItemsDict.ContainsKey(wrapper.Key))
                {
                    continue;
                }

                ItemsDict.Add(wrapper.Key, wrapper);
                keysByAsset[assetId] = wrapper.Key;
                cardsToAdd.Add((BuildCardItem(assetId, wrapper), wrapper));
            }

            // Cards already on screen may have moved: a later page can carry a cheaper
            // listing or more tokens for the same asset, so their badges and tap contexts
            // are rebuilt from the aggregates and swapped in place below.
            var cardsToUpdate = assetIds
                .Where(assetId => keysByAsset.ContainsKey(assetId)
                    && cardsToAdd.All(card => card.Item.AssetId != assetId))
                .Select(assetId => BuildCardItem(assetId, ItemsDict[keysByAsset[assetId]]))
                .ToList();

            var cardsTouched = cardsToAdd.Count + cardsToUpdate.Count;

            if (cardsTouched == 0)
            {
                return 0;
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                foreach (var (item, wrapper) in cardsToAdd)
                {
                    Items.Add(wrapper);
                    FeedItems.Add(item);
                }

                foreach (var updated in cardsToUpdate)
                {
                    for (var i = 0; i < FeedItems.Count; i++)
                    {
                        if (FeedItems[i] is NoticeboardPropertyItem card && card.AssetId == updated.AssetId)
                        {
                            FeedItems[i] = updated;
                            break;
                        }
                    }
                }
            });

            if (cardsToAdd.Count > 0)
            {
                _ = PersistPropertiesAsync(cardsToAdd.Select(card => card.Wrapper).ToList());
            }

            return cardsTouched;
        }

        /// <summary>
        /// One share listing folded into its asset's running totals: the total amount on
        /// offer, the listing count and the cheapest listing so far (the representative
        /// the card's tap context points at). A row already folded - paging handed it
        /// back twice - is skipped, or the badge would double-count its tokens.
        /// </summary>
        private void FoldIntoAggregate(XcavateShareListingInfo shareListing)
        {
            if (!foldedShareListingIds.Add(shareListing.ShareListingId))
            {
                return;
            }

            if (!aggregatesByAsset.TryGetValue(shareListing.AssetId, out var aggregate))
            {
                aggregate = new ShareListingAggregate();
                aggregatesByAsset[shareListing.AssetId] = aggregate;
            }

            aggregate.TotalAmount += shareListing.Amount;
            aggregate.ListingCount++;

            if (aggregate.Cheapest is null || shareListing.SharePriceQuote < aggregate.Cheapest.SharePriceQuote)
            {
                aggregate.Cheapest = shareListing;
            }
        }

        /// <summary>
        /// The card item for one asset: the marketplace card plus the secondary-market
        /// badge ("N tokens from £X/token" off the cheapest listing) and the cheapest
        /// active listing as the tap's share-listing context. Reads the aggregates, so it
        /// runs synchronously while a page is being folded in - never from posted UI work,
        /// which could race the next page's fold.
        /// </summary>
        private NoticeboardPropertyItem BuildCardItem(long assetId, XcavateNftWrapper wrapper)
        {
            var aggregate = aggregatesByAsset[assetId];
            var cheapest = aggregate.Cheapest!;

            var lowestPrice = SolanaAmount.FromBaseUnits(
                cheapest.SharePriceQuote.ToString(CultureInfo.InvariantCulture),
                XcavateMarketplaceIndexerModel.SharePriceDecimals);

            var badgeText = aggregate.TotalAmount == 1
                ? $"1 token from {lowestPrice.ToCurrencyString()}/token"
                : $"{aggregate.TotalAmount} tokens from {lowestPrice.ToCurrencyString()}/token";

            return new NoticeboardPropertyItem
            {
                AssetId = assetId,
                Wrapper = wrapper,
                BadgeText = badgeText,
                ShareListingContext = new XcavateShareListingContext(
                    cheapest.ShareListingId,
                    cheapest.AssetId,
                    cheapest.Seller,
                    cheapest.SharePriceQuote,
                    cheapest.Amount),
            };
        }

        public override async Task InitialLoadAsync(CancellationToken token)
        {
            token = StartNewLoadingOperation(token);

            if (Items.Count > 0)
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
                // "no tokens" message even though items are about to be (re)loaded.
                Loading = true;
                Clear();
                await InitialLoadAsync(CancellationToken.None).ConfigureAwait(false);

                // Refresh the empty-state caption now that the cluster has settled (this method
                // can run off the main thread, so marshal it).
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

        public void CancelPendingOperations()
        {
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

        private void Clear()
        {
            ItemsDict.Clear();
            Items.Clear();
            aggregatesByAsset.Clear();
            foldedShareListingIds.Clear();
            keysByAsset.Clear();

            // Clear can run off the main thread; the feed mirror is reset on the main thread,
            // where it is also queued ahead of the item adds the reload posts afterwards.
            MainThread.BeginInvokeOnMainThread(ResetFeedItems);

            offset = 0;
            hasMore = true;
            isBackgroundHydrationRunning = false;
        }

        private void ResetFeedItems()
        {
            FeedItems.Clear();
            SyncFeedSlots();
        }

        /// <summary>
        /// Keeps the skeleton and empty-state markers in the feed only while they apply. They are
        /// added/removed rather than hidden because an IsVisible=false item can keep its cell's
        /// measured space inside the CollectionView, showing up as a gap. Runs on the main thread
        /// (all callers marshal there).
        /// </summary>
        private void SyncFeedSlots()
        {
            if (ShowSkeleton)
            {
                if (!FeedItems.Contains(skeletonFeedItem))
                {
                    FeedItems.Insert(0, skeletonFeedItem);
                }
            }
            else
            {
                FeedItems.Remove(skeletonFeedItem);
            }

            if (NoItems)
            {
                if (!FeedItems.Contains(emptyStateFeedItem))
                {
                    FeedItems.Add(emptyStateFeedItem);
                }
            }
            else
            {
                FeedItems.Remove(emptyStateFeedItem);
            }
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
                // Expected when a new refresh starts.
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

        /// <summary>
        /// One asset's running totals across the share listings folded in so far: the
        /// figures the badge prints and the cheapest listing the card's tap context
        /// points at - the offer a buyer would take first.
        /// </summary>
        private sealed class ShareListingAggregate
        {
            public uint TotalAmount { get; set; }

            public int ListingCount { get; set; }

            /// <summary>The lowest-priced listing seen for the asset; null before the first one.</summary>
            public XcavateShareListingInfo? Cheapest { get; set; }
        }
    }
}
