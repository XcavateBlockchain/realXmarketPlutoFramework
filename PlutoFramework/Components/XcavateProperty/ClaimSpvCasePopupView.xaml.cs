namespace PlutoFramework.Components.XcavateProperty
{
    public partial class ClaimSpvCasePopupView : ContentView
    {
        public ClaimSpvCasePopupView()
        {
            InitializeComponent();

            BindingContext = DependencyService.Get<ClaimSpvCasePopupViewModel>();
        }
    }
}
