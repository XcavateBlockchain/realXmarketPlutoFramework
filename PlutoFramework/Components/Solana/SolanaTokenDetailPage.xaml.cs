using PlutoFramework.Templates.PageTemplate;
using PlutoFrameworkCore.Solana;

namespace PlutoFramework.Components.Solana;

public partial class SolanaTokenDetailPage : PageTemplate
{
    private readonly SolanaTokenDetailPageViewModel viewModel;

    public SolanaTokenDetailPage(SolanaTokenBalance balance)
    {
        InitializeComponent();

        viewModel = new SolanaTokenDetailPageViewModel(balance);

        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // A stablecoin's LoadAsync returns immediately - its page is built entirely from the
        // row it was constructed with.
        _ = viewModel.LoadAsync(CancellationToken.None);

        // The reserved tGBP section is an indexer query, not part of the row, so it loads
        // on its own path - it is worth showing even on a stablecoin where LoadAsync bails
        // early.
        _ = viewModel.LoadReservedAsync();
    }

    protected override void OnDisappearing()
    {
        viewModel.Unsubscribe();

        base.OnDisappearing();
    }
}
