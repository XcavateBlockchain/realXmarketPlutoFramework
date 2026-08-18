namespace PlutoFramework.Components.XcavateProperty;

public partial class CancelReservationPopup : ContentView
{
    public CancelReservationPopup()
    {
        InitializeComponent();

        BindingContext = DependencyService.Get<CancelReservationPopupViewModel>();
    }
}