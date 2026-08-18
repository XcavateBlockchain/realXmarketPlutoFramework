using PlutoFramework.Model;
using Substrate.NetApi;

namespace PlutoFrameworkTests
{
    /// <summary>
    /// Onboarding derives the Substrate account and DID that KYC, the questionnaire and the
    /// XcavatePaseo whitelist are keyed to from the Solana account's own seed phrase, so both
    /// come off the same backup. That runs a Solana phrase through Substrate's keyring, which
    /// is only safe because both chains use plain BIP39 - the assumption these tests hold in
    /// place, alongside <see cref="SolanaX25519DerivationTests"/> for the third key of the set.
    /// </summary>
    public class SolanaSubstrateIdentityDerivationTests
    {
        /// <summary>
        /// The suffix KeysModel.EnsureSubstrateIdentityAsync appends for the DID key, matching
        /// what the Polkadot onboarding flow has always written.
        /// </summary>
        private const string DID_SUFFIX = "//did";

        /// <summary>
        /// The load-bearing case. If Substrate's keyring rejected a Solana-generated phrase,
        /// every account onboarded through the Solana flow would dead-end at the questionnaire
        /// with no address to submit.
        /// </summary>
        [Test]
        public void DerivesASubstrateAccountFromASolanaGeneratedPhrase()
        {
            var mnemonics = SolanaMnemonicsModel.GenerateMnemonics();

            var account = MnemonicsModel.GetAccountFromMnemonics(mnemonics);

            Assert.That(account.Value, Is.Not.Empty);

            // What KeysModel.GetPublicKey() and GetSubstrateKey(ss58prefix) do to the stored
            // address. This is the call that throws today on the "Substrate key does not
            // exist" placeholder, so a real address has to survive it.
            Assert.DoesNotThrow(() => Utils.GetPublicKeyFrom(account.Value));
            Assert.That(Utils.GetPublicKeyFrom(account.Value), Has.Length.EqualTo(32));
        }

        /// <summary>
        /// The DID must be its own key, not the account key over again. A keyring that ignored
        /// the derivation suffix would hand Sumsub an ExternalUserId identical to the UserId,
        /// silently and without failing anywhere.
        /// </summary>
        [Test]
        public void DerivesADidDistinctFromTheAccount()
        {
            var mnemonics = SolanaMnemonicsModel.GenerateMnemonics();

            var account = MnemonicsModel.GetAccountFromMnemonics(mnemonics);
            var did = MnemonicsModel.GetAccountFromMnemonics($"{mnemonics}{DID_SUFFIX}");

            Assert.That(did.Value, Is.Not.Empty);
            Assert.That(did.Value, Is.Not.EqualTo(account.Value));
        }

        /// <summary>
        /// Deterministic, which is the point of deriving rather than generating: the user
        /// restores the phrase their wallet was backed up under and their verified identity
        /// comes back with it.
        /// </summary>
        [Test]
        public void IsDeterministicForTheSamePhrase()
        {
            var mnemonics = SolanaMnemonicsModel.GenerateMnemonics();

            Assert.Multiple(() =>
            {
                Assert.That(
                    MnemonicsModel.GetAccountFromMnemonics(mnemonics).Value,
                    Is.EqualTo(MnemonicsModel.GetAccountFromMnemonics(mnemonics).Value));

                Assert.That(
                    MnemonicsModel.GetAccountFromMnemonics($"{mnemonics}{DID_SUFFIX}").Value,
                    Is.EqualTo(MnemonicsModel.GetAccountFromMnemonics($"{mnemonics}{DID_SUFFIX}").Value));
            });
        }

        /// <summary>
        /// Two accounts must not collide on one identity - the profile, the Sumsub applicant
        /// and the whitelist entry are all keyed by this address.
        /// </summary>
        [Test]
        public void DiffersBetweenPhrases()
        {
            var first = MnemonicsModel.GetAccountFromMnemonics(SolanaMnemonicsModel.GenerateMnemonics());
            var second = MnemonicsModel.GetAccountFromMnemonics(SolanaMnemonicsModel.GenerateMnemonics());

            Assert.That(first.Value, Is.Not.EqualTo(second.Value));
        }

        /// <summary>
        /// The Mobile Wallet Adapter path has no phrase to derive from and generates its own.
        /// It goes through the same keyring, so the identity it produces has to be just as
        /// usable as a derived one.
        /// </summary>
        [Test]
        public void DerivesAnIdentityFromAGeneratedPhraseForWalletConnections()
        {
            var mnemonics = MnemonicsModel.GenerateMnemonics();

            var account = MnemonicsModel.GetAccountFromMnemonics(mnemonics);
            var did = MnemonicsModel.GetAccountFromMnemonics($"{mnemonics}{DID_SUFFIX}");

            Assert.DoesNotThrow(() => Utils.GetPublicKeyFrom(account.Value));
            Assert.That(did.Value, Is.Not.EqualTo(account.Value));
        }
    }
}
