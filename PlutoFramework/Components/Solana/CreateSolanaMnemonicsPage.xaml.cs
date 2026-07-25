using PlutoFramework.Templates.PageTemplate;

namespace PlutoFramework.Components.Solana;

public partial class CreateSolanaMnemonicsPage : PageTemplate
{
    public CreateSolanaMnemonicsPage(CreateSolanaMnemonicsViewModel viewModel)
    {
        InitializeComponent();

        BindingContext = viewModel;
    }
}
