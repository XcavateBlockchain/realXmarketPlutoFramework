using PlutoFramework.Model;

namespace PlutoFrameworkTests
{
    public class SolanaMnemonics
    {
        /// <summary>
        /// Vector taken from Solnet's own WalletTest.cs, where the expected keys are
        /// documented as the output of sollet.io for this mnemonic.
        /// </summary>
        private const string TestMnemonics =
            "lens scheme misery search address destroy shallow police picture gown apart rural cotton vivid cage disagree enrich govern history kit early near cloth alarm";

        /// <summary>
        /// m/44'/501'/0'/0' under SeedMode.Ed25519Bip32 - the Phantom/Solflare default.
        /// </summary>
        private const string ExpectedEd25519Bip32Address = "ALSzrjtGi8MZGmAZa6ZhtUZq3rwurWuJqWFdgcj9MMFL";

        /// <summary>
        /// The same mnemonic under SeedMode.Bip39 (solana-keygen). Asserted as NOT equal,
        /// so that switching seed modes fails this fixture loudly.
        /// </summary>
        private const string SolanaKeygenAddress = "4n8BE7DHH4NudifUBrwPbvNPs2F86XcagT7C2JKdrWrR";

        [Test]
        public void GetAccountFromMnemonicsDerivesEd25519Bip32Index0()
        {
            var account = SolanaMnemonicsModel.GetAccountFromMnemonics(TestMnemonics);

            Assert.That(account.PublicKey.Key, Is.EqualTo(ExpectedEd25519Bip32Address));
        }

        [Test]
        public void GetAccountFromMnemonicsDoesNotUseBip39SeedMode()
        {
            var account = SolanaMnemonicsModel.GetAccountFromMnemonics(TestMnemonics);

            Assert.That(account.PublicKey.Key, Is.Not.EqualTo(SolanaKeygenAddress));
        }

        [Test]
        public void GetAddressFromMnemonicsMatchesAccountPublicKey()
        {
            var address = SolanaMnemonicsModel.GetAddressFromMnemonics(TestMnemonics);

            Assert.That(address, Is.EqualTo(ExpectedEd25519Bip32Address));
        }

        [Test]
        public void GenerateMnemonicsProducesTwelveWords()
        {
            var mnemonics = SolanaMnemonicsModel.GenerateMnemonics();

            Assert.That(mnemonics.Split(' ', StringSplitOptions.RemoveEmptyEntries), Has.Length.EqualTo(12));
        }

        [Test]
        public void GenerateMnemonicsProducesDistinctAccounts()
        {
            var first = SolanaMnemonicsModel.GetAddressFromMnemonics(SolanaMnemonicsModel.GenerateMnemonics());
            var second = SolanaMnemonicsModel.GetAddressFromMnemonics(SolanaMnemonicsModel.GenerateMnemonics());

            Assert.That(first, Is.Not.EqualTo(second));
        }

        [Test]
        public void GenerateMnemonicsProducesImportableMnemonics()
        {
            var mnemonics = SolanaMnemonicsModel.GenerateMnemonics();

            Assert.That(SolanaMnemonicsModel.ValidateMnemonics(mnemonics), Is.True);
        }

        [Test]
        public void ValidateMnemonicsAcceptsKnownGoodMnemonics()
        {
            Assert.That(SolanaMnemonicsModel.ValidateMnemonics(TestMnemonics), Is.True);
        }

        /// <summary>
        /// The canonical BIP39 all-zeros-entropy vector. "abandon" x11 + "about" is the
        /// valid phrase; replacing the final word breaks only the checksum, leaving every
        /// word a legitimate wordlist entry. This pair isolates checksum validation from
        /// wordlist validation.
        ///
        /// Hand-picking a "wrong looking" phrase does not work here: a 12-word mnemonic
        /// carries just 4 checksum bits, so an arbitrary phrase validates 1 time in 16.
        /// </summary>
        private const string CanonicalValidMnemonics =
            "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";

        private const string CanonicalBadChecksumMnemonics =
            "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon";

        [Test]
        public void ValidateMnemonicsAcceptsCanonicalAllZerosVector()
        {
            Assert.That(SolanaMnemonicsModel.ValidateMnemonics(CanonicalValidMnemonics), Is.True);
        }

        [Test]
        public void ValidateMnemonicsRejectsBadChecksum()
        {
            Assert.That(SolanaMnemonicsModel.ValidateMnemonics(CanonicalBadChecksumMnemonics), Is.False);
        }

        [Test]
        public void ValidateMnemonicsRejectsWordsOutsideWordlist()
        {
            var notInWordlist = "zzzzz scheme misery search address destroy shallow police picture gown apart rural";

            Assert.That(SolanaMnemonicsModel.ValidateMnemonics(notInWordlist), Is.False);
        }

        [Test]
        public void ValidateMnemonicsRejectsEmptyInput()
        {
            Assert.That(SolanaMnemonicsModel.ValidateMnemonics("   "), Is.False);
        }

        [Test]
        public void TryGetAddressPreviewReturnsTheAddressForAValidPhrase()
        {
            Assert.That(
                SolanaMnemonicsModel.TryGetAddressPreview(TestMnemonics),
                Is.EqualTo(ExpectedEd25519Bip32Address));
        }

        [Test]
        public void TryGetAddressPreviewIsEmptyForAHalfTypedPhrase()
        {
            Assert.That(SolanaMnemonicsModel.TryGetAddressPreview("lens scheme misery"), Is.Empty);
        }

        [Test]
        public void TryGetAddressPreviewIsEmptyForABadChecksum()
        {
            // Twelve wordlist words in a combination BIP39's checksum rejects. The canonical
            // valid all-abandon phrase ends in "about".
            var badChecksum = string.Join(" ", Enumerable.Repeat("abandon", 12));

            Assert.That(SolanaMnemonicsModel.ValidateMnemonics(badChecksum), Is.False);
            Assert.That(SolanaMnemonicsModel.TryGetAddressPreview(badChecksum), Is.Empty);
        }

        [Test]
        public void TryGetAddressPreviewIsEmptyForNullAndEmptyInput()
        {
            Assert.That(SolanaMnemonicsModel.TryGetAddressPreview(null), Is.Empty);
            Assert.That(SolanaMnemonicsModel.TryGetAddressPreview(""), Is.Empty);
            Assert.That(SolanaMnemonicsModel.TryGetAddressPreview("   "), Is.Empty);
        }
    }
}
