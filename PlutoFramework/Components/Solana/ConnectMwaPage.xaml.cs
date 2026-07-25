using PlutoFramework.Templates.PageTemplate;

namespace PlutoFramework.Components.Solana;

public partial class ConnectMwaPage : PageTemplate
{
    public ConnectMwaPage(ConnectMwaPageViewModel viewModel)
    {
        InitializeComponent();

        BindingContext = viewModel;
    }
}
