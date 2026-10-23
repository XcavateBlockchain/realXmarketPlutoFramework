using PlutoFramework.Model.Xcavate;
using PlutoFramework.Templates.PageTemplate;

namespace PlutoFramework.Components.XcavateProperty;

/// <summary>
/// Pushed from the pending properties list with the tapped record. Everything the page shows
/// is already on the record, so there is nothing to load on appearing.
/// </summary>
public partial class PendingPropertyDetailPage : PageTemplate
{
    public PendingPropertyDetailPage(DeveloperProperty property)
    {
        InitializeComponent();

        BindingContext = new PendingPropertyDetailViewModel(property);
    }
}
