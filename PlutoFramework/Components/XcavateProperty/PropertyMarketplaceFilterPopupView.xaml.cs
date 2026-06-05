namespace PlutoFramework.Components.XcavateProperty;

public partial class PropertyMarketplaceFilterPopupView : ContentView
{
    public PropertyMarketplaceFilterPopupView()
    {
        InitializeComponent();

        BindingContext = DependencyService.Get<PropertyMarketplaceFilterPopupViewModel>();
    }
}