namespace PlutoFramework.Components.XcavateProperty;

public partial class DeveloperListPropertyPopupView : ContentView
{
    public DeveloperListPropertyPopupView()
    {
        InitializeComponent();

        BindingContext = DependencyService.Get<DeveloperListPropertyPopupViewModel>();
    }
}
