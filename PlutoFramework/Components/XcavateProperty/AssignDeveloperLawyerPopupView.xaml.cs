namespace PlutoFramework.Components.XcavateProperty
{
    public partial class AssignDeveloperLawyerPopupView : ContentView
    {
        public AssignDeveloperLawyerPopupView()
        {
            InitializeComponent();

            BindingContext = DependencyService.Get<AssignDeveloperLawyerPopupViewModel>();
        }
    }
}
