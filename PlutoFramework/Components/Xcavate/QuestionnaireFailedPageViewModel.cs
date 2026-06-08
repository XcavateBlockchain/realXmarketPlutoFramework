using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
namespace PlutoFramework.Components.Xcavate
{
    public partial class QuestionnaireFailedPageViewModel : ObservableObject
    {
        [ObservableProperty]
        private string text = "";

        [RelayCommand]
        public Task CancelAsync()
        {
            return Shell.Current.Navigation.PopToRootAsync();
        }
    }
}
