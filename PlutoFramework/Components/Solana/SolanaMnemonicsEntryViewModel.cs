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
        public const string INVALID_PHRASE_MESSAGE = "That is not a valid seed phrase.";

        /// <summary>
        /// BIP39 phrases are 12, 15, 18, 21 or 24 words - 12 is the shortest length at which a
        /// phrase can first validate.
        /// </summary>
        private const int SHORTEST_VALID_PHRASE_WORDS = 12;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AddressPreview))]
        [NotifyPropertyChangedFor(nameof(AddressPreviewIsVisible))]
        [NotifyPropertyChangedFor(nameof(IsValid))]
        [NotifyPropertyChangedFor(nameof(InvalidPhraseIsVisible))]
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

        /// <summary>
        /// True once the phrase is long enough to be a complete BIP39 phrase but still does not
        /// validate. Gated on the word count so the message does not accuse the user of a bad
        /// phrase while they are still typing the first one.
        /// </summary>
        public bool InvalidPhraseIsVisible => WordCount >= SHORTEST_VALID_PHRASE_WORDS && !IsValid;

        /// <summary>
        /// Splits on any whitespace, not just spaces: the phrase arrives in a multiline editor
        /// and is often pasted with newlines from a text file or password manager.
        /// </summary>
        private int WordCount => Mnemonics.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

        public string InvalidPhraseMessage => INVALID_PHRASE_MESSAGE;

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
