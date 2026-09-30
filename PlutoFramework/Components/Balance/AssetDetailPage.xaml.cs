using PlutoFramework.Templates.PageTemplate;

namespace PlutoFramework.Components.Balance
{
    public partial class AssetDetailPage : PageTemplate
    {
        public AssetDetailPage(AssetDetailViewModel viewModel)
        {
            InitializeComponent();

            BindingContext = viewModel;

            // The bar is declared for the tGBP page's three buttons; every other asset drops
            // Redeem together with its column, restoring the two-button layout.
            if (!viewModel.RedeemIsVisible)
            {
                bottomBar.Children.Remove(redeemButton);
                bottomBar.ColumnDefinitions.RemoveAt(2);
                transferButton.Margin = new Thickness(0, 10, 10, 10);
            }
        }
    }
}