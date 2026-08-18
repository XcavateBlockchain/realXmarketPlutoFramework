using PlutoFramework.Templates.PageTemplate;

namespace PlutoFramework.Components.Solana;

public partial class SolanaMwaKeyDetailPage : PageTemplate
{
    public SolanaMwaKeyDetailPage(SolanaMwaKeyDetailPageViewModel viewModel)
    {
        InitializeComponent();

        BindingContext = viewModel;
    }
}
