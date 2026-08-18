using PlutoFramework.Templates.PageTemplate;

namespace PlutoFramework.Components.Solana;

public partial class SolanaMnemonicKeyDetailPage : PageTemplate
{
    public SolanaMnemonicKeyDetailPage(SolanaMnemonicKeyDetailPageViewModel viewModel)
    {
        InitializeComponent();

        BindingContext = viewModel;
    }
}
