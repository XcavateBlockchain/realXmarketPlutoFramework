namespace PlutoFramework.Components.XcavateProperty
{
    public partial class MakeOfferPopupView : ContentView
    {
        public MakeOfferPopupView()
        {
            InitializeComponent();

            BindingContext = DependencyService.Get<MakeOfferPopupViewModel>();
        }
    }
}
