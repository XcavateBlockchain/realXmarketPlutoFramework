using PlutoFrameworkCore.Keys;
using PlutoFrameworkCore.Solana;
using System.Text.Json;

namespace PlutoFrameworkTests
{
    public class SolanaClusterTests
    {
        [Test]
        public void ToChainIdProducesMwaChainIdentifiers()
        {
            Assert.Multiple(() =>
            {
                Assert.That(SolanaCluster.Devnet.ToChainId(), Is.EqualTo("solana:devnet"));
                Assert.That(SolanaCluster.Testnet.ToChainId(), Is.EqualTo("solana:testnet"));
                Assert.That(SolanaCluster.Mainnet.ToChainId(), Is.EqualTo("solana:mainnet"));
            });
        }

        [Test]
        public void FromChainIdRoundTripsEveryCluster()
        {
            foreach (SolanaCluster cluster in Enum.GetValues<SolanaCluster>())
            {
                Assert.That(SolanaClusterExtensions.FromChainId(cluster.ToChainId()), Is.EqualTo(cluster));
            }
        }

        [Test]
        public void FromChainIdFallsBackToMainnetForUnknownInput()
        {
            // MWA defaults to solana:mainnet when a chain is unspecified, so an
            // unrecognised or empty stored value must not silently become devnet.
            Assert.Multiple(() =>
            {
                Assert.That(SolanaClusterExtensions.FromChainId(""), Is.EqualTo(SolanaCluster.Mainnet));
                Assert.That(SolanaClusterExtensions.FromChainId("solana:nonsense"), Is.EqualTo(SolanaCluster.Mainnet));
            });
        }

        [Test]
        public void GetNameProducesDisplayableLabels()
        {
            Assert.That(SolanaCluster.Devnet.GetName(), Is.EqualTo("Devnet"));
        }
    }

    public class SolanaNetworkOptionsTests
    {
        [Test]
        public void DefaultIsMainnet()
        {
            // A user who never opens Settings must be on the network real funds live on.
            Assert.That(SolanaNetworkOptions.Default, Is.EqualTo(SolanaCluster.Mainnet));
        }

        [Test]
        public void SelectableOffersMainnetAndDevnetInThatOrder()
        {
            Assert.That(SolanaNetworkOptions.Selectable,
                Is.EqualTo(new[] { SolanaCluster.Mainnet, SolanaCluster.Devnet }));
        }

        [Test]
        public void SelectableContainsTheDefault()
        {
            // Otherwise the settings selector would open with nothing highlighted.
            Assert.That(SolanaNetworkOptions.Selectable, Does.Contain(SolanaNetworkOptions.Default));
        }

        [Test]
        public void EverySelectableClusterSurvivesAPreferencesRoundTrip()
        {
            // The setting is stored as the MWA chain id, not the enum, so each offered
            // network must come back as itself after being written and read.
            foreach (var cluster in SolanaNetworkOptions.Selectable)
            {
                Assert.That(SolanaClusterExtensions.FromChainId(cluster.ToChainId()), Is.EqualTo(cluster));
            }
        }
    }

    public class SolanaMwaKeyTests
    {
        private static SolanaMwaKey SampleKey() => new()
        {
            AuthToken = "auth-token-abc123",
            Address = "ALSzrjtGi8MZGmAZa6ZhtUZq3rwurWuJqWFdgcj9MMFL",
            Chain = "solana:devnet",
            WalletUriBase = "https://phantom.app",
            AccountLabel = "Phantom",
        };

        /// <summary>
        /// The whole record is serialized into SecureStorage as the key's "secret",
        /// so a lossy roundtrip would silently drop the auth token or the cluster.
        /// </summary>
        [Test]
        public void SerializesAndDeserializesWithoutLoss()
        {
            var original = SampleKey();

            var restored = JsonSerializer.Deserialize<SolanaMwaKey>(JsonSerializer.Serialize(original));

            Assert.That(restored, Is.EqualTo(original));
        }

        [Test]
        public void SerializesWithNullOptionalFields()
        {
            var original = SampleKey() with { WalletUriBase = null, AccountLabel = null };

            var restored = JsonSerializer.Deserialize<SolanaMwaKey>(JsonSerializer.Serialize(original));

            Assert.That(restored, Is.EqualTo(original));
        }

        [Test]
        public void ClusterReflectsStoredChain()
        {
            var key = SampleKey() with { Chain = "solana:testnet" };

            Assert.That(key.Cluster, Is.EqualTo(SolanaCluster.Testnet));
        }

        [Test]
        public void DisplayNameFallsBackToWalletWhenUnlabelled()
        {
            var key = SampleKey() with { AccountLabel = null };

            Assert.That(key.DisplayName, Is.EqualTo("Solana wallet"));
        }

        [Test]
        public void DisplayNameUsesAccountLabelWhenPresent()
        {
            Assert.That(SampleKey().DisplayName, Is.EqualTo("Phantom"));
        }
    }

    public class SolanaMnemonicKeyTests
    {
        private const string TestMnemonics =
            "lens scheme misery search address destroy shallow police picture gown apart rural cotton vivid cage disagree enrich govern history kit early near cloth alarm";

        [Test]
        public void AddressMatchesDerivedAccount()
        {
            var key = new SolanaMnemonicKey { Mnemonics = TestMnemonics };

            Assert.That(key.Address, Is.EqualTo("ALSzrjtGi8MZGmAZa6ZhtUZq3rwurWuJqWFdgcj9MMFL"));
        }
    }

    public class KeyTypeEnumSolanaTests
    {
        [Test]
        public void SolanaKeyTypesHaveDisplayNames()
        {
            Assert.Multiple(() =>
            {
                Assert.That(KeyTypeEnum.SolanaMnemonic.GetName(), Is.EqualTo("Solana key"));
                Assert.That(KeyTypeEnum.SolanaMwa.GetName(), Is.EqualTo("Solana wallet"));
            });
        }

        /// <summary>
        /// KeyTypeEnum is persisted by name inside the SQLite Serialized column.
        /// Renaming a member would orphan every previously stored key.
        /// </summary>
        [Test]
        public void SolanaKeyTypesSerializeByName()
        {
            Assert.Multiple(() =>
            {
                Assert.That(KeyTypeEnum.SolanaMnemonic.ToString(), Is.EqualTo("SolanaMnemonic"));
                Assert.That(KeyTypeEnum.SolanaMwa.ToString(), Is.EqualTo("SolanaMwa"));
            });
        }
    }
}
