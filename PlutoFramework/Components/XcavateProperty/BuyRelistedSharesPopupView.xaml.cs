namespace PlutoFramework.Components.XcavateProperty
{
    public partial class BuyRelistedSharesPopupView : ContentView
    {
        public BuyRelistedSharesPopupView()
        {
            InitializeComponent();

            BindingContext = DependencyService.Get<BuyRelistedSharesPopupViewModel>();
        }
    }
}
