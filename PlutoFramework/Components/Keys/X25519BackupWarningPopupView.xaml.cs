namespace PlutoFramework.Components.Keys
{
    public partial class X25519BackupWarningPopupView : ContentView
    {
        public X25519BackupWarningPopupView()
        {
            InitializeComponent();

            BindingContext = DependencyService.Get<X25519BackupWarningPopupViewModel>();
        }
    }
}
