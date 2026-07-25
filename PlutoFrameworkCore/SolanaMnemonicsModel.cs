using Solnet.Wallet.Bip39;
using SeedMode = Solnet.Wallet.SeedMode;
using SolanaAccount = Solnet.Wallet.Account;
using SolanaMnemonic = Solnet.Wallet.Bip39.Mnemonic;
using SolanaWallet = Solnet.Wallet.Wallet;

namespace PlutoFramework.Model
{
    /// <summary>
    /// The only place in the codebase that touches Solnet types.
    ///
    /// Solnet and Substrate collide on Account, Wallet and Mnemonic, all three of which
    /// are used pervasively elsewhere. Keeping Solnet behind this class means no other
    /// file has to disambiguate them.
    /// </summary>
    public static class SolanaMnemonicsModel
    {
        /// <summary>
        /// Solnet derives <see cref="SolanaWallet.Account"/> at m/44'/501'/0'/0' under
        /// <see cref="SeedMode.Ed25519Bip32"/>, which is what Phantom, Solflare and
        /// Backpack show by default.
        ///
        /// Do not switch this to <see cref="SeedMode.Bip39"/>: besides changing every
        /// derived address, it makes Solnet's GetAccount(index) throw outright.
        /// </summary>
        private const SeedMode DEFAULT_SEED_MODE = SeedMode.Ed25519Bip32;

        public static string GenerateMnemonics()
        {
            var mnemonic = new SolanaMnemonic(WordList.English, WordCount.Twelve);

            return string.Join(" ", mnemonic.Words);
        }

        public static SolanaAccount GetAccountFromMnemonics(string mnemonics)
        {
            var wallet = new SolanaWallet(mnemonics.Trim(), WordList.English, seedMode: DEFAULT_SEED_MODE);

            return wallet.Account;
        }

        public static string GetAddressFromMnemonics(string mnemonics) =>
            GetAccountFromMnemonics(mnemonics).PublicKey.Key;

        /// <summary>
        /// True only when the phrase has a supported word count, contains solely
        /// wordlist entries, and carries a correct BIP39 checksum.
        /// </summary>
        public static bool ValidateMnemonics(string mnemonics)
        {
            if (string.IsNullOrWhiteSpace(mnemonics))
            {
                return false;
            }

            try
            {
                return new SolanaMnemonic(mnemonics.Trim(), WordList.English).IsValidChecksum;
            }
            catch
            {
                // Solnet throws FormatException for a bad word count and for words
                // outside the wordlist. Both mean "not importable".
                return false;
            }
        }
    }
}
