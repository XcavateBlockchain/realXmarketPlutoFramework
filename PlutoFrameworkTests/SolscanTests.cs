using PlutoFrameworkCore.Constants;
using PlutoFrameworkCore.Solana;

namespace PlutoFrameworkTests
{
    public class SolscanTests
    {
        private const string Signature =
            "5VERv8NMvzbJMEkV8xnrLkEaWRtSz9CosKDYjCJjBRnbJLgp8uirBgmQpjKhoR4tjF3ZpRzrFmBV6UjKdiSZkQUW";

        /// <summary>
        /// Solscan defaults to mainnet, so the parameter is omitted rather than spelled out.
        /// </summary>
        [Test]
        public void MainnetUrlCarriesNoClusterParameter()
        {
            Assert.That(Solscan.TransactionUrl(Signature, SolanaCluster.Mainnet),
                Is.EqualTo($"https://solscan.io/tx/{Signature}"));
        }

        /// <summary>
        /// Without the parameter a devnet signature opens a mainnet page, which shows "not
        /// found" for a transaction that succeeded.
        /// </summary>
        [Test]
        public void DevnetUrlCarriesTheCluster()
        {
            Assert.That(Solscan.TransactionUrl(Signature, SolanaCluster.Devnet),
                Is.EqualTo($"https://solscan.io/tx/{Signature}?cluster=devnet"));
        }

        [Test]
        public void TestnetUrlCarriesTheCluster()
        {
            Assert.That(Solscan.TransactionUrl(Signature, SolanaCluster.Testnet),
                Is.EqualTo($"https://solscan.io/tx/{Signature}?cluster=testnet"));
        }
    }
}
