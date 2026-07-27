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
    /// <see cref="Completed"/> and then <see cref="IsVisible"/>. Only usable where a password
    /// is already stored - <see cref="KeysModel.SaveSolanaMnemonicKeyAsync"/> reads it to
    /// encrypt the phrase. Onboarding, which has no password yet, uses
    /// <c>ImportSolanaWalletPage</c> instead.
    /// </remarks>
    public partial class EnterSolanaMnemonicsPopupViewModel : ObservableObject, IPopup, ISetToDefault
    {
        [ObservableProperty]
        private bool isVisible = false;

        /// <summary>
        /// The phrase, its validity and its address preview. Not shared with any other
        /// surface, so a phrase typed here cannot surface anywhere else.
        /// </summary>
        public SolanaMnemonicsEntryViewModel Entry { get; } = new();

        /// <summary>
        /// Runs after the key is saved, with the phrase that was imported. The popup closes
        /// itself first.
        /// </summary>
        public Func<string, Task> Completed { get; set; } = (string mnemonics) => Task.CompletedTask;

        /// <summary>
        /// Runs when the card finishes closing. Clearing the phrase matters here beyond the
        /// usual reset: a shared instance would otherwise keep somebody's seed phrase in
        /// memory, and show it to whoever opens the popup next.
        /// </summary>
        public void SetToDefault()
        {
            IsVisible = false;
            Entry.Reset();
            Completed = (string mnemonics) => Task.CompletedTask;
        }

        [RelayCommand]
        public async Task ContinueWithMnemonicsAsync()
        {
            if (!Entry.IsValid)
            {
                Entry.ShowError("That is not a valid seed phrase.");

                return;
            }

            try
            {
                await KeysModel.SaveSolanaMnemonicKeyAsync(Entry.Mnemonics);
            }
            catch
            {
                // Only the save is guarded. Letting this also cover the callback would report
                // a phrase that imported perfectly well as a failure, on a popup that has
                // already closed - and, on a shared instance, the error would still be
                // showing the next time somebody opens it.
                //
                // The phrase was validated above, so this is not an invalid-phrase failure
                // and must not claim to be one.
                Entry.ShowError("Could not save your wallet. Please try again.");

                return;
            }

            // Captured before the reset below clears them.
            var completed = Completed;
            var mnemonics = Entry.Mnemonics;

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
