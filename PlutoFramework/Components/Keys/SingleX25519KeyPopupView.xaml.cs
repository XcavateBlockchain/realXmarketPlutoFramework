namespace PlutoFramework.Components.Keys
{
    public partial class SingleX25519KeyPopupView : ContentView
    {
        public SingleX25519KeyPopupView()
        {
            InitializeComponent();

            BindingContext = DependencyService.Get<SingleX25519KeyPopupViewModel>();
        }
    }
}
