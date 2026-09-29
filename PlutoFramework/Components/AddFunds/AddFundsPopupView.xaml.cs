namespace PlutoFramework.Components.AddFunds;

public partial class AddFundsPopupView : ContentView
{
    public AddFundsPopupView()
    {
        InitializeComponent();

        BindingContext = DependencyService.Get<AddFundsPopupViewModel>();
    }
}
