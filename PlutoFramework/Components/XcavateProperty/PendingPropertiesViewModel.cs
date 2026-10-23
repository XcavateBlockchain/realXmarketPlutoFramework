using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Model;
using PlutoFramework.Model.Xcavate;
using PlutoFramework.Model.Xcavate.Profile;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace PlutoFramework.Components.XcavateProperty;

/// <summary>
/// The pending-properties page: every property record the developer has submitted that is
/// still waiting for verification - the store's Submitted status, which is what "pending
/// approval" means there (the web enum's literal Pending is never written) - as one list
/// that pages in more records as it is scrolled.
/// </summary>
/// <remarks>
/// The records come from the same DynamoDB store the web developer page reads, keyed on the
/// developer's selected company exactly the way <c>DeveloperMainPageViewModel</c> resolves it.
/// Paging follows the store's continuation token (<see cref="DeveloperPropertyClient.GetPropertiesPageAsync"/>),
/// so the list grows a page at a time instead of reading the whole store up front.
///
/// A tap pushes the record's detail page, where the admin decides it; the refresh on
/// appearing then drops it from this list, since it is no longer submitted.
/// </remarks>
public partial class PendingPropertiesViewModel : ObservableObject
{
    private const int PageSize = 20;

    private readonly XcavateCompanyService companyService = new();
    private readonly SemaphoreSlim loadMoreSemaphore = new(1, 1);
    private readonly object loadingLock = new();
    private CancellationTokenSource? loadingCts;

    private string? selectedCompanyId;
    private string? pageToken;
    private bool hasMore = true;
    private string storeAddress = string.Empty;
    private bool listLoadFailed;
    private bool storeUnavailable;
    private bool isRefreshInProgress;

    public PendingPropertiesViewModel()
    {
        Properties.CollectionChanged += OnPropertiesCollectionChanged;
    }

    [ObservableProperty]
    private bool isRefreshing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoProperties))]
    private ObservableCollection<DeveloperProperty> properties = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoProperties))]
    [NotifyPropertyChangedFor(nameof(ShowSkeleton))]
    private bool propertiesLoading;

    /// <summary>True while a page beyond the first is being fetched.</summary>
    [ObservableProperty]
    private bool loadingMore;

    public bool NoProperties => !PropertiesLoading && Properties.Count == 0;

    /// <summary>True while the list is still empty and loading (initial load / refresh).</summary>
    public bool ShowSkeleton => PropertiesLoading && Properties.Count == 0;

    /// <summary>
    /// The empty-list caption. Same rule as the developer main page: an unconfigured store or
    /// a signed-out wallet each get their own sentence, so neither reads as "you have no
    /// properties".
    /// </summary>
    public string EmptyListMessage =>
        listLoadFailed
            ? "The properties could not be loaded. Pull down to try again."
            : storeUnavailable
                ? "The property store is not reachable with the credentials this build was signed with."
                : string.IsNullOrWhiteSpace(storeAddress)
                    ? "Sign in with your wallet to see the properties you created."
                    : "Properties waiting for verification will appear here.";

    /// <summary>
    /// The tapped property's detail page, where the admin's decision (approve / reject)
    /// lives.
    /// </summary>
    [RelayCommand]
    private Task OpenPropertyAsync(DeveloperProperty? property)
    {
        if (property is null)
        {
            return Task.CompletedTask;
        }

        return NavigationModel.PushAsync(new PendingPropertyDetailPage(property));
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        // RefreshView sets IsRefreshing to true (TwoWay binding) before executing
        // RefreshCommand, so IsRefreshing cannot be used as a re-entrancy guard here.
        if (isRefreshInProgress)
        {
            return;
        }

        isRefreshInProgress = true;
        IsRefreshing = true;

        try
        {
            var token = ReplaceLoadingToken(CancellationToken.None);

            // The company first: the store keys properties on the selected company, so the
            // list cannot be read before it is resolved.
            await LoadSelectedCompanyAsync(token).ConfigureAwait(false);

            ResetProperties();
            await LoadNextPageAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected when a newer refresh starts.
        }
        finally
        {
            IsRefreshing = false;
            isRefreshInProgress = false;
        }
    }

    /// <summary>
    /// The scroll handler's entry point: fetch the next page when the end of the list comes
    /// into reach. No-ops once the store said there are no more pages, while a refresh owns
    /// the list, and against itself - the semaphore collapses the burst of scrolled events a
    /// fast fling raises.
    /// </summary>
    public async Task TryLoadMoreAsync(CancellationToken token)
    {
        if (!hasMore || isRefreshInProgress)
        {
            return;
        }

        try
        {
            await loadMoreSemaphore.WaitAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        try
        {
            if (!hasMore || isRefreshInProgress)
            {
                return;
            }

            LoadingMore = true;

            await LoadNextPageAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected when a refresh starts over the top.
        }
        finally
        {
            LoadingMore = false;
            loadMoreSemaphore.Release();
        }
    }

    public void CancelOngoingLoading()
    {
        lock (loadingLock)
        {
            loadingCts?.Cancel();
        }
    }

    private CancellationToken ReplaceLoadingToken(CancellationToken externalToken)
    {
        CancellationTokenSource? previousCts;
        CancellationTokenSource newCts;

        lock (loadingLock)
        {
            previousCts = loadingCts;
            newCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
            loadingCts = newCts;
        }

        previousCts?.Cancel();
        previousCts?.Dispose();

        return newCts.Token;
    }

    /// <summary>
    /// The developer's selected company, resolved the same way the developer main page
    /// resolves it: the stored selection when it still exists, the first company otherwise,
    /// persisted so every screen settles on the same one. A wallet may simply own none - the
    /// store then falls back to the developer's own record, keyed on the wallet itself.
    /// </summary>
    private async Task LoadSelectedCompanyAsync(CancellationToken token)
    {
        var address = MainKeyModel.GetAddress();

        if (string.IsNullOrWhiteSpace(address))
        {
            selectedCompanyId = null;
            return;
        }

        try
        {
            var companies = await companyService.GetMyCompaniesAsync(token).ConfigureAwait(false);

            if (companies.Count == 0)
            {
                selectedCompanyId = null;
                return;
            }

            var storedId = SelectedCompanyModel.GetSelectedCompanyId(address);
            var selected = companies.FirstOrDefault(company => company.CompanyId == storedId) ?? companies[0];

            if (selected.CompanyId != storedId)
            {
                SelectedCompanyModel.SetSelectedCompanyId(address, selected.CompanyId);
            }

            selectedCompanyId = selected.CompanyId;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // A company list that cannot be read is not fatal: the store's by-user index
            // still answers for the wallet on its own.
            Console.WriteLine("Company list load error:");
            Console.WriteLine(ex);
            selectedCompanyId = null;
        }
    }

    /// <summary>
    /// The next page of submitted records. The store's failures surface as the empty state,
    /// never as a crash - the store is optional from the app's position.
    /// </summary>
    private async Task LoadNextPageAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        // Where there is no company to key on, the store falls back to the developer's own
        // record, and that key is the Solana wallet the properties were created from - the
        // same convention the developer main page follows.
        var address = KeysModel.GetSolanaAddress();

        if (string.IsNullOrWhiteSpace(address))
        {
            storeAddress = string.Empty;
            ResetProperties();
            return;
        }

        if (!string.Equals(storeAddress, address, StringComparison.Ordinal))
        {
            // A different wallet inherits nothing from the previous one's list.
            storeAddress = address;
            ResetProperties();
        }

        try
        {
            if (Properties.Count == 0)
            {
                PropertiesLoading = true;
            }

            var page = await DeveloperPropertyClient.GetPropertiesPageAsync(
                    selectedCompanyId, address, DeveloperPropertyStatus.Submitted,
                    PageSize, pageToken, token)
                .ConfigureAwait(false);

            token.ThrowIfCancellationRequested();

            storeUnavailable = Properties.Count == 0 && page.Items.Count == 0
                && !DeveloperPropertyClient.IsConfigured;
            listLoadFailed = false;
            pageToken = page.NextPageToken;
            hasMore = page.NextPageToken is not null;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                foreach (var record in page.Items)
                {
                    Properties.Add(record);
                }
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Developer property store error:");
            Console.WriteLine(ex);
            listLoadFailed = true;
            storeUnavailable = !DeveloperPropertyClient.IsConfigured;
            hasMore = false;
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                PropertiesLoading = false;
                OnPropertyChanged(nameof(NoProperties));
                OnPropertyChanged(nameof(EmptyListMessage));
            });
        }
    }

    private void ResetProperties()
    {
        listLoadFailed = false;
        storeUnavailable = false;
        pageToken = null;
        hasMore = true;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            Properties.Clear();
            OnPropertyChanged(nameof(NoProperties));
            OnPropertyChanged(nameof(EmptyListMessage));
        });
    }

    private void OnPropertiesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(NoProperties));
        OnPropertyChanged(nameof(ShowSkeleton));
    }
}
