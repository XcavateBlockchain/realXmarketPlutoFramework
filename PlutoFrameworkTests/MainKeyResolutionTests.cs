using PlutoFrameworkCore.Keys;

namespace PlutoFrameworkTests
{
    /// <summary>
    /// The stored preference is not what the app acts on - which key exists gets a veto.
    /// Two groups of users depend on that: the Solana-only accounts every new user gets, and
    /// the Substrate-only ones from before the Solana switch, neither of whom ever opens
    /// Settings.
    /// </summary>
    public class MainKeyResolutionTests
    {
        /// <summary>
        /// The case that decides what a brand new user gets. Every account created since the
        /// Solana switch is Solana-only, and they have not touched the setting.
        /// </summary>
        [Test]
        public void DefaultsToSolana()
        {
            Assert.That(MainKeyOptions.Default, Is.EqualTo(MainKeyChain.Solana));

            Assert.That(
                MainKeyOptions.Resolve(MainKeyOptions.Default, hasSolana: true, hasSubstrate: false),
                Is.EqualTo(MainKeyChain.Solana));
        }

        [TestCase(MainKeyChain.Solana)]
        [TestCase(MainKeyChain.Polkadot)]
        public void HonoursThePreferenceWhenBothKeysExist(MainKeyChain preferred)
        {
            Assert.That(
                MainKeyOptions.Resolve(preferred, hasSolana: true, hasSubstrate: true),
                Is.EqualTo(preferred));
        }

        /// <summary>
        /// The migration case. A user onboarded before Solana holds only a Substrate key and
        /// has never opened Settings, so their preference is the Solana default. Stranding
        /// them on a chain they have no key for would hide their profile and their address.
        /// </summary>
        [Test]
        public void FallsBackToSubstrateWhenTheSolanaPreferenceHasNoKey()
        {
            Assert.That(
                MainKeyOptions.Resolve(MainKeyChain.Solana, hasSolana: false, hasSubstrate: true),
                Is.EqualTo(MainKeyChain.Polkadot));
        }

        /// <summary>
        /// The mirror case: a Polkadot preference outliving the Substrate key it was chosen
        /// for, which is what a log out and a Solana-only re-onboard leaves behind.
        /// </summary>
        [Test]
        public void FallsBackToSolanaWhenThePolkadotPreferenceHasNoKey()
        {
            Assert.That(
                MainKeyOptions.Resolve(MainKeyChain.Polkadot, hasSolana: true, hasSubstrate: false),
                Is.EqualTo(MainKeyChain.Solana));
        }

        /// <summary>
        /// Null rather than a default, so callers show a logged-out state instead of querying
        /// a profile for an address that does not exist.
        /// </summary>
        [TestCase(MainKeyChain.Solana)]
        [TestCase(MainKeyChain.Polkadot)]
        public void ResolvesToNothingWhenThereAreNoKeys(MainKeyChain preferred)
        {
            Assert.That(
                MainKeyOptions.Resolve(preferred, hasSolana: false, hasSubstrate: false),
                Is.Null);
        }
    }
}
