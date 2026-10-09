namespace PlutoFramework.Components.XcavateProperty;

public partial class CastVotePopupView : ContentView
{
    public CastVotePopupView()
    {
        InitializeComponent();

        BindingContext = DependencyService.Get<CastVotePopupViewModel>();
    }
}
