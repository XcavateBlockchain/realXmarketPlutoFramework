using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Model;

namespace PlutoFramework.Components.Solana
{
    public partial class CreateSolanaMnemonicsViewModel : ObservableObject
    {
        public Func<Task> Navigation { get; set; } = () => Task.CompletedTask;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Address))]
        private string mnemonics = "";

        [ObservableProperty]
        private bool isSaving = false;

        public string Address =>
            string.IsNullOrEmpty(Mnemonics) ? "Loading" : SolanaMnemonicsModel.GetAddressFromMnemonics(Mnemonics);

        public CreateSolanaMnemonicsViewModel()
        {
            Mnemonics = SolanaMnemonicsModel.GenerateMnemonics();
        }

        [RelayCommand]
        public async Task ContinueAsync()
        {
            if (IsSaving)
            {
                return;
            }

            IsSaving = true;

            try
            {
                await KeysModel.SaveSolanaMnemonicKeyAsync(Mnemonics);

                await Navigation.Invoke();
            }
            finally
            {
                IsSaving = false;
            }
        }
    }
}
