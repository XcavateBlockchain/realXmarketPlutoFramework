using PlutoFrameworkCore.Constants;
using PlutoFrameworkCore.Solana;

namespace PlutoFrameworkTests
{
    public class SolanaExplorerTests
    {
        private const string Signature =
            "5VERv8NMvzbJMEkV8xnrLkEaWRtSz9CosKDYjCJjBRnbJLgp8uirBgmQpjKhoR4tjF3ZpRzrFmBV6UjKdiSZkQUW";

        /// <summary>
        /// Solana Explorer defaults to mainnet, so the parameter is omitted rather than
        /// spelled out.
        /// </summary>
        [Test]
        public void MainnetUrlCarriesNoClusterParameter()
        {
            Assert.That(SolanaExplorer.TransactionUrl(Signature, SolanaCluster.Mainnet),
                Is.EqualTo($"https://explorer.solana.com/tx/{Signature}"));
        }

        /// <summary>
        /// Without the parameter a devnet signature opens a mainnet page, which shows "not
        /// found" for a transaction that succeeded.
        /// </summary>
        [Test]
        public void DevnetUrlCarriesTheCluster()
        {
            Assert.That(SolanaExplorer.TransactionUrl(Signature, SolanaCluster.Devnet),
                Is.EqualTo($"https://explorer.solana.com/tx/{Signature}?cluster=devnet"));
        }

        [Test]
        public void TestnetUrlCarriesTheCluster()
        {
            Assert.That(SolanaExplorer.TransactionUrl(Signature, SolanaCluster.Testnet),
                Is.EqualTo($"https://explorer.solana.com/tx/{Signature}?cluster=testnet"));
        }
    }
}
