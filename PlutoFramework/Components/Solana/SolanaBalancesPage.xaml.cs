using PlutoFramework.Templates.PageTemplate;

namespace PlutoFramework.Components.Solana;

public partial class SolanaBalancesPage : PageTemplate
{
    private readonly SolanaBalancesPageViewModel viewModel = new();

    public SolanaBalancesPage()
    {
        InitializeComponent();

        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Reloaded on every appearance, not just construction: the user may have changed
        // network, or created an account, while this page sat on the stack.
        _ = viewModel.LoadAsync(CancellationToken.None);
    }

    protected override void OnDisappearing()
    {
        viewModel.Unsubscribe();

        base.OnDisappearing();
    }

    private void OnTransferClicked(object sender, EventArgs e) =>
        DependencyService.Get<Transfer.SolanaTransferViewModel>().Appear();
}
