using PlutoFramework.Templates.PageTemplate;

namespace PlutoFramework.Components.XcavateProperty;

/// <summary>
/// Pushed from the main menu's Pending Properties button. Loads the first page on appearing;
/// every later page is the scroll handler's doing, the same pattern the lawyer main page
/// follows.
/// </summary>
public partial class PendingPropertiesPage : PageTemplate
{
    private readonly PendingPropertiesViewModel viewModel = new();

    public PendingPropertiesPage()
    {
        InitializeComponent();

        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        _ = viewModel.RefreshAsync();
    }

    private async void OnMainScrollViewScrolled(object? sender, ScrolledEventArgs e)
    {
        if (sender is not ScrollView scrollView)
        {
            return;
        }

        var remainingHeight = scrollView.ContentSize.Height - (scrollView.ScrollY + scrollView.Height);

        if (remainingHeight > 280)
        {
            return;
        }

        await viewModel.TryLoadMoreAsync(CancellationToken.None);
    }
}
