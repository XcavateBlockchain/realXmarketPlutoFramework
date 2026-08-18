using PlutoFrameworkCore;
using PlutoFrameworkCore.Solana;

namespace PlutoFrameworkTests
{
    public class SolanaTokenWhitelistTests
    {
        private const string UsdcMainnet = "EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v";
        private const string UsdcDevnet = "4zMMC9srt5Ri5X14GAgXhaHii3GnPAEERYPJgZJDncDU";

        [SetUp]
        public void SetUp()
        {
            PlutoConfigurationModel.WhitelistedSolanaTokens =
            [
                new SolanaTokenWhitelistEntry
                {
                    Cluster = SolanaCluster.Mainnet,
                    Mint = UsdcMainnet,
                    Symbol = "USDC",
                    Decimals = 6,
                    PinnedUsdPrice = 1.00,
                },
                new SolanaTokenWhitelistEntry
                {
                    Cluster = SolanaCluster.Devnet,
                    Mint = UsdcDevnet,
                    Symbol = "USDC",
                    Decimals = 6,
                    PinnedUsdPrice = 1.00,
                },
            ];
        }

        [TearDown]
        public void TearDown() => PlutoConfigurationModel.WhitelistedSolanaTokens = [];

        /// <summary>
        /// A mint address names a different token on each cluster. Returning the whole list
        /// would show mainnet balances while the app is pointed at devnet.
        /// </summary>
        [Test]
        public void ReturnsOnlyTheSelectedClustersMints()
        {
            var mainnet = SolanaTokenWhitelist.ForCluster(SolanaCluster.Mainnet);
            var devnet = SolanaTokenWhitelist.ForCluster(SolanaCluster.Devnet);

            Assert.Multiple(() =>
            {
                Assert.That(mainnet.Select(entry => entry.Mint), Is.EqualTo(new[] { UsdcMainnet }));
                Assert.That(devnet.Select(entry => entry.Mint), Is.EqualTo(new[] { UsdcDevnet }));
            });
        }

        [Test]
        public void ReturnsEmptyForAClusterWithNoEntries()
        {
            Assert.That(SolanaTokenWhitelist.ForCluster(SolanaCluster.Testnet), Is.Empty);
        }

        /// <summary>
        /// Entries default to the legacy SPL Token program. Token-2022 accounts are returned
        /// by a different program id, so a wrong default would silently report zero.
        /// </summary>
        [Test]
        public void DefaultsToTheLegacyTokenProgram()
        {
            Assert.That(
                SolanaTokenWhitelist.ForCluster(SolanaCluster.Mainnet)[0].ProgramId,
                Is.EqualTo("TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA"));
        }

        [Test]
        public void NativeSolIsNineDecimals()
        {
            Assert.Multiple(() =>
            {
                Assert.That(SolanaNativeToken.Decimals, Is.EqualTo(9));
                Assert.That(SolanaNativeToken.LamportsPerSol, Is.EqualTo(1_000_000_000UL));
                Assert.That(SolanaNativeToken.Mint, Is.EqualTo("So11111111111111111111111111111111111111112"));
            });
        }
    }
}
