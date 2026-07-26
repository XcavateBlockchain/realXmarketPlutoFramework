using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Model;

namespace PlutoFramework.Components.Solana
{
    /// <summary>
    /// Takes an existing Solana seed phrase and saves it as the app's Solana key.
    /// </summary>
    /// <remarks>
    /// One instance is shared through <see cref="DependencyService"/>. Callers set
    /// <see cref="Completed"/> and then <see cref="IsVisible"/>.
    /// </remarks>
    public partial class EnterSolanaMnemonicsPopupViewModel : ObservableObject, IPopup, ISetToDefault
    {
        [ObservableProperty]
        private bool isVisible = false;

        /// <summary>
        /// Runs after the key is saved, with the phrase that was imported. The popup closes
        /// itself first.
        /// </summary>
        public Func<string, Task> Completed { get; set; } = (string mnemonics) => Task.CompletedTask;

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

        /// <summary>
        /// Runs when the card finishes closing. Clearing the phrase matters here beyond the
        /// usual reset: a shared instance would otherwise keep somebody's seed phrase in
        /// memory, and show it to whoever opens the popup next.
        /// </summary>
        public void SetToDefault()
        {
            IsVisible = false;
            Mnemonics = "";
            IncorrectMnemonicsEntered = false;
            Completed = (string mnemonics) => Task.CompletedTask;
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
            }
            catch
            {
                // Only the save is guarded. Letting this also cover the callback would report
                // a phrase that imported perfectly well as invalid, on a popup that has
                // already closed - and, on a shared instance, the error would still be
                // showing the next time somebody opens it.
                IncorrectMnemonicsEntered = true;

                return;
            }

            // Captured before the reset below clears them.
            var completed = Completed;
            var mnemonics = Mnemonics;

            IsVisible = false;

            // Reset here rather than leaving it to the card's close animation: onboarding's
            // callback replaces the whole page, and a card torn down mid-animation never
            // reaches SetToDefault - which would leave the phrase sitting in this shared
            // instance for whoever opens the popup next.
            SetToDefault();

            await completed.Invoke(mnemonics);
        }
    }
}
