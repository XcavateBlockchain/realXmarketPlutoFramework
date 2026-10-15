namespace PlutoFramework.Components.XcavateProperty
{
    public partial class SendSharesPopupView : ContentView
    {
        public SendSharesPopupView()
        {
            InitializeComponent();

            BindingContext = DependencyService.Get<SendSharesPopupViewModel>();
        }
    }
}
