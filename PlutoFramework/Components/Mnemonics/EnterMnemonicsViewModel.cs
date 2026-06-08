using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Model;

namespace PlutoFramework.Components.Mnemonics
{
    public partial class EnterMnemonicsViewModel : ObservableObject
    {
        public Func<Task> Navigation { get; set; } = () => Task.CompletedTask;

        [ObservableProperty]
        private bool incorrectMnemonicsEntered = false;

        [ObservableProperty]
        private string mnemonics = "";

        [ObservableProperty]
        private string privateKey = "";

        public bool UsePrivateKeyIsVisible => Preferences.Get(PreferencesModel.SETTINGS_ALLOW_PRIVATE_KEY, false);

        [RelayCommand]
        public async Task ContinueWithMnemonicsAsync()
        {
            try
            {
                string mnemonics = Mnemonics;
                string didMnemonics = $"{mnemonics}//did";
                string x25519Mnemonics = $"{mnemonics}//x25519";

                await KeysModel.SaveSr25519KeyAsync(mnemonics);
                await KeysModel.SaveDidKeyAsync(didMnemonics);
                await KeysModel.SaveEncryptionX25519KeyAsync(x25519Mnemonics);

                await Navigation.Invoke();
            }
            catch
            {
                IncorrectMnemonicsEntered = true;
            }
        }

        [RelayCommand]
        public async Task ContinueWithPrivateKeyAsync()
        {
            await Model.KeysModel.GenerateNewAccountFromPrivateKeyAsync(PrivateKey);

            await Navigation.Invoke();
        }

        [RelayCommand]
        public async Task ImportJsonAsync()
        {
            await KeysModel.ImportJsonKeyAsync();

            string mnemonics = MnemonicsModel.GenerateMnemonics();
            string didMnemonics = $"{mnemonics}//did";
            string x25519Mnemonics = $"{mnemonics}//x25519";

            await Task.WhenAll(
                KeysModel.SaveDidKeyAsync(didMnemonics),
                KeysModel.SaveEncryptionX25519KeyAsync(x25519Mnemonics)
            );

            await Navigation.Invoke();
        }
    }
}
