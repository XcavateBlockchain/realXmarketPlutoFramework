namespace PlutoFramework.Components.XcavateProperty;

public partial class PropertyMarketplaceSelectionPopupView : ContentView
{
    public PropertyMarketplaceSelectionPopupView()
    {
        InitializeComponent();

        BindingContext = DependencyService.Get<PropertyMarketplaceSelectionPopupViewModel>();
    }
}
