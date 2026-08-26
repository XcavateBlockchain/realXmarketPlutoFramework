using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Model;

namespace PlutoFramework.Templates.TopNavigationBarTemplate
{
    public partial class TopNavigationBarViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool extra1IsVisible = false;

        [ObservableProperty]
        private bool extra2IsVisible = false;

        public Func<Task>? BackFunc { get; set; }

        [RelayCommand]
        public async Task BackAsync()
        {
            // A visible popup is dismissed before the page goes back.
            if (PopupManager.TryCloseTopPopup())
            {
                return;
            }

            await (BackFunc?.Invoke() ?? NavigationModel.PopAsync());
        }
    }
}
