using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Model;

namespace PlutoFramework.Components.Solana
{
    /// <summary>
    /// Shows a freshly generated Solana seed phrase for backup, then saves it once the user
    /// confirms they have written it down.
    /// </summary>
    /// <remarks>
    /// One instance is shared through <see cref="DependencyService"/>, so the phrase is
    /// generated when the popup opens instead of in the constructor - otherwise every caller
    /// after the first would be handed the phrase generated for the first.
    /// </remarks>
    public partial class CreateSolanaMnemonicsPopupViewModel : ObservableObject, IPopup, ISetToDefault
    {
        [ObservableProperty]
        private bool isVisible = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Address))]
        private string mnemonics = "";

        [ObservableProperty]
        private bool isSaving = false;

        /// <summary>
        /// Runs after the key is saved. The popup closes itself first, so callers only have to
        /// refresh whatever they show for the new account.
        /// </summary>
        public Func<Task> Completed { get; set; } = () => Task.CompletedTask;

        public string Address =>
            string.IsNullOrEmpty(Mnemonics) ? "Loading" : SolanaMnemonicsModel.GetAddressFromMnemonics(Mnemonics);

        partial void OnIsVisibleChanged(bool value)
        {
            if (value)
            {
                Mnemonics = SolanaMnemonicsModel.GenerateMnemonics();
            }
        }

        /// <summary>
        /// Runs when the card finishes closing, whichever way it was closed - including a
        /// swipe down, which abandons the phrase before it was ever saved.
        /// </summary>
        public void SetToDefault()
        {
            IsVisible = false;
            IsSaving = false;
            Mnemonics = "";
            Completed = () => Task.CompletedTask;
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

                // Captured before the reset below clears it.
                var completed = Completed;

                IsVisible = false;

                // Reset here rather than leaving it to the card's close animation: a card torn
                // down mid-animation never reaches SetToDefault, which would leave the phrase
                // sitting in this shared instance for whoever opens the popup next.
                SetToDefault();

                await completed.Invoke();
            }
            finally
            {
                IsSaving = false;
            }
        }
    }
}
