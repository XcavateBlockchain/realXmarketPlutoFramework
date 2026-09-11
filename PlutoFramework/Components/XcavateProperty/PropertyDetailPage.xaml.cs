using PlutoFramework.Templates.PageTemplate;

namespace PlutoFramework.Components.XcavateProperty;

public partial class PropertyDetailPage : PageTemplate
{
    public PropertyDetailPage(PropertyDetailViewModel viewModel)
    {
        InitializeComponent();

        // The details may still be loading, so the metadata (and with it the map) is bound
        // rather than captured here.
        BindingContext = viewModel;
    }
}