using CommunityToolkit.Mvvm.ComponentModel;
using PlutoFramework.Model;

namespace PlutoFramework.Components.Solana
{
    /// <summary>
    /// The seed-phrase half of any import surface: the phrase, whether it is importable, the
    /// address it unlocks, and one error line.
    /// </summary>
    /// <remarks>
    /// Held as a property by whatever hosts it, never registered with
    /// <see cref="DependencyService"/>. A shared instance would carry one user's phrase into
    /// the next screen that showed it.
    /// </remarks>
    public partial class SolanaMnemonicsEntryViewModel : ObservableObject
    {
        private const string INVALID_PHRASE_MESSAGE = "That is not a valid seed phrase.";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AddressPreview))]
        [NotifyPropertyChangedFor(nameof(AddressPreviewIsVisible))]
        [NotifyPropertyChangedFor(nameof(IsValid))]
        private string mnemonics = "";

        [ObservableProperty]
        private bool errorIsVisible = false;

        [ObservableProperty]
        private string errorMessage = INVALID_PHRASE_MESSAGE;

        public bool IsValid => SolanaMnemonicsModel.ValidateMnemonics(Mnemonics);

        /// <summary>
        /// Lets the user confirm the derived address matches the one their existing wallet
        /// shows, before committing to the import.
        /// </summary>
        public string AddressPreview => SolanaMnemonicsModel.TryGetAddressPreview(Mnemonics);

        public bool AddressPreviewIsVisible => !string.IsNullOrEmpty(AddressPreview);

        partial void OnMnemonicsChanged(string value)
        {
            // Clear a stale error as soon as the user edits the phrase.
            ErrorIsVisible = false;
        }

        public void ShowError(string message)
        {
            ErrorMessage = message;
            ErrorIsVisible = true;
        }

        /// <summary>
        /// Clears the phrase along with the error. Hosts must call this once the phrase has
        /// served its purpose - it is somebody's seed phrase sitting in memory.
        /// </summary>
        public void Reset()
        {
            Mnemonics = "";
            ErrorIsVisible = false;
            ErrorMessage = INVALID_PHRASE_MESSAGE;
        }
    }
}
