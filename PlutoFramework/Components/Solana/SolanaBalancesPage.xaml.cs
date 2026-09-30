using PlutoFramework.Templates.PageTemplate;

namespace PlutoFramework.Components.Solana;

public partial class SolanaBalancesPage : PageTemplate
{
    private readonly SolanaBalancesPageViewModel viewModel = new();

    public SolanaBalancesPage()
    {
        InitializeComponent();

        BindingContext = viewModel;

        // The band starts below the top navigation bar. Its height is an app-level resource
        // (the same lookup PageTemplate.ApplyScrollViewPadding makes), not a value this
        // library can hardcode in XAML the way InvestorMainPage does in the app project.
        var topNavigationBarHeight = (double)Application.Current!.Resources["TopNavigationBarHeight"];

        AbsoluteLayout.SetLayoutBounds(particleStreamView, new Rect(0, topNavigationBarHeight, 1, 100));
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
}
