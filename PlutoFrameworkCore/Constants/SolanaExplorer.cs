using PlutoFrameworkCore.Solana;

namespace PlutoFrameworkCore.Constants
{
    /// <summary>
    /// Solana Explorer links, offered by the transaction status popup's explorer button.
    /// </summary>
    public static class SolanaExplorer
    {
        private const string BaseUrl = "https://explorer.solana.com";

        /// <summary>
        /// The explorer page for one transaction.
        /// </summary>
        /// <remarks>
        /// Solana Explorer defaults to mainnet and takes any other cluster as a query
        /// parameter. Omitting it off-mainnet opens a mainnet page, which reports "not
        /// found" for a devnet transaction that in fact succeeded.
        /// </remarks>
        public static string TransactionUrl(string signature, SolanaCluster cluster) => cluster switch
        {
            SolanaCluster.Mainnet => $"{BaseUrl}/tx/{signature}",
            _ => $"{BaseUrl}/tx/{signature}?cluster={cluster.GetName().ToLowerInvariant()}",
        };
    }
}
