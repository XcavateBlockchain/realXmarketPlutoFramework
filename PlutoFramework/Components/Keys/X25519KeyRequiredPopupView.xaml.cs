namespace PlutoFramework.Components.Keys;

public partial class X25519KeyRequiredPopupView : ContentView
{
    public X25519KeyRequiredPopupView()
    {
        InitializeComponent();

        BindingContext = DependencyService.Get<X25519KeyRequiredPopupViewModel>();
    }
}
