using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Model;

namespace PlutoFramework.Components.Solana
{
    public partial class EnterSolanaMnemonicsViewModel : ObservableObject
    {
        public Func<string, Task> Navigation { get; set; } = (string mnemonics) => Task.CompletedTask;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AddressPreview))]
        [NotifyPropertyChangedFor(nameof(AddressPreviewIsVisible))]
        [NotifyPropertyChangedFor(nameof(IsValid))]
        private string mnemonics = "";

        [ObservableProperty]
        private bool incorrectMnemonicsEntered = false;

        public bool IsValid => SolanaMnemonicsModel.ValidateMnemonics(Mnemonics);

        /// <summary>
        /// Shown live so the user can confirm the derived address matches the one their
        /// existing wallet displays before committing to the import. A mnemonic imported
        /// under the wrong derivation yields a valid but empty account, which is otherwise
        /// only discoverable after the fact.
        /// </summary>
        public string AddressPreview
        {
            get
            {
                if (!IsValid)
                {
                    return "";
                }

                try
                {
                    return SolanaMnemonicsModel.GetAddressFromMnemonics(Mnemonics);
                }
                catch
                {
                    return "";
                }
            }
        }

        public bool AddressPreviewIsVisible => !string.IsNullOrEmpty(AddressPreview);

        partial void OnMnemonicsChanged(string value)
        {
            // Clear a stale error as soon as the user edits the phrase.
            IncorrectMnemonicsEntered = false;
        }

        [RelayCommand]
        public async Task ContinueWithMnemonicsAsync()
        {
            if (!IsValid)
            {
                IncorrectMnemonicsEntered = true;

                return;
            }

            try
            {
                await KeysModel.SaveSolanaMnemonicKeyAsync(Mnemonics);

                await Navigation.Invoke(Mnemonics);
            }
            catch
            {
                IncorrectMnemonicsEntered = true;
            }
        }
    }
}
