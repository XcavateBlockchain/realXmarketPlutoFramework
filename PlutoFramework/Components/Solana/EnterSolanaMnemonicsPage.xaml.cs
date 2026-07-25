using PlutoFramework.Templates.PageTemplate;

namespace PlutoFramework.Components.Solana;

public partial class EnterSolanaMnemonicsPage : PageTemplate
{
    public EnterSolanaMnemonicsPage(EnterSolanaMnemonicsViewModel viewModel)
    {
        InitializeComponent();

        BindingContext = viewModel;
    }
}
