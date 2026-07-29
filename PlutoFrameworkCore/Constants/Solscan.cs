using PlutoFrameworkCore.Solana;

namespace PlutoFrameworkCore.Constants
{
    /// <summary>
    /// Solscan links, the Solana counterpart of the Subscan deep link the Substrate
    /// extrinsic toast offers.
    /// </summary>
    public static class Solscan
    {
        private const string BaseUrl = "https://solscan.io";

        /// <summary>
        /// The explorer page for one transaction.
        /// </summary>
        /// <remarks>
        /// Solscan defaults to mainnet and takes any other cluster as a query parameter.
        /// Omitting it off-mainnet opens a mainnet page, which reports "not found" for a
        /// devnet transaction that in fact succeeded.
        /// </remarks>
        public static string TransactionUrl(string signature, SolanaCluster cluster) => cluster switch
        {
            SolanaCluster.Mainnet => $"{BaseUrl}/tx/{signature}",
            _ => $"{BaseUrl}/tx/{signature}?cluster={cluster.GetName().ToLowerInvariant()}",
        };
    }
}
