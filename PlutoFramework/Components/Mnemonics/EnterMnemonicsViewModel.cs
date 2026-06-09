using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Model;

namespace PlutoFramework.Components.Mnemonics
{
    public partial class EnterMnemonicsViewModel : ObservableObject
    {
        public Func<string, Task> Navigation { get; set; } = (string mnemonics) => Task.CompletedTask;

        [ObservableProperty]
        private bool incorrectMnemonicsEntered = false;

        [ObservableProperty]
        private string mnemonics = "";

        [ObservableProperty]
        private string privateKey = "";

        [RelayCommand]
        public async Task ContinueWithMnemonicsAsync()
        {
            try
            {
                await Navigation.Invoke(Mnemonics);
            }
            catch
            {
                IncorrectMnemonicsEntered = true;
            }
        }

        [RelayCommand]
        public async Task ImportJsonAsync()
        {
            await KeysModel.ImportJsonKeyAsync();

            string mnemonics = MnemonicsModel.GenerateMnemonics();

            await Navigation.Invoke(mnemonics);
        }
    }
}
