using PlutoFrameworkCore.Solana;
using PlutoFrameworkCore.Solana.Mwa;
using System.Text.Json;

namespace PlutoFrameworkTests
{
    /// <summary>
    /// Phantom's Mobile Wallet Adapter implementation reads the legacy 1.0 `cluster`
    /// field and treats a missing one as mainnet, so an authorize request carrying
    /// only the 2.0 `chain` field is presented as a mainnet connection - and rejected
    /// with "Incorrect mode" while the wallet is in testnet mode. The 2.0
    /// specification keeps `cluster` as an alias that v2 wallets must ignore when
    /// `chain` is present, so sending both is safe and reaches either implementation.
    /// </summary>
    public class MwaAuthorizeRequestTests
    {
        [Test]
        public void DevnetAuthorizeCarriesBothChainAndLegacyCluster()
        {
            var request = MwaClient.BuildAuthorizeRequest(
                new MwaIdentity { Name = "Test dapp", Uri = "https://example.com" },
                SolanaCluster.Devnet,
                authToken: null);

            var json = JsonSerializer.Serialize(request);

            Assert.Multiple(() =>
            {
                Assert.That(json, Does.Contain("\"chain\":\"solana:devnet\""));
                Assert.That(json, Does.Contain("\"cluster\":\"devnet\""));
            });
        }

        [Test]
        public void MainnetAuthorizeUsesTheLegacyMainnetBetaSpelling()
        {
            // A 1.0 wallet does not recognise "mainnet"; its mainnet value is
            // "mainnet-beta".
            var request = MwaClient.BuildAuthorizeRequest(
                new MwaIdentity { Name = "Test dapp", Uri = "https://example.com" },
                SolanaCluster.Mainnet,
                authToken: null);

            var json = JsonSerializer.Serialize(request);

            Assert.Multiple(() =>
            {
                Assert.That(json, Does.Contain("\"chain\":\"solana:mainnet\""));
                Assert.That(json, Does.Contain("\"cluster\":\"mainnet-beta\""));
            });
        }
    }
}
