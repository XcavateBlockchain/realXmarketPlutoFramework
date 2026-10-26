namespace PlutoFramework.Components.XcavateProperty;

public partial class SelectListingRegionPopupView : ContentView
{
    public SelectListingRegionPopupView()
    {
        InitializeComponent();

        BindingContext = DependencyService.Get<SelectListingRegionPopupViewModel>();
    }
}
