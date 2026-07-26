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
    }

    protected override void OnDisappearing()
    {
        viewModel.Unsubscribe();

        base.OnDisappearing();
    }
}
