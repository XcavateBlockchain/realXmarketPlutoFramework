namespace PlutoFrameworkCore.Solana
{
    public enum SolanaCluster
    {
        Devnet,
        Testnet,
        Mainnet,
    }

    public static class SolanaClusterExtensions
    {
        /// <summary>
        /// The chain identifier sent in the Mobile Wallet Adapter "chain" field.
        /// </summary>
        public static string ToChainId(this SolanaCluster cluster) => cluster switch
        {
            SolanaCluster.Devnet => "solana:devnet",
            SolanaCluster.Testnet => "solana:testnet",
            _ => "solana:mainnet",
        };

        public static string GetName(this SolanaCluster cluster) => cluster switch
        {
            SolanaCluster.Devnet => "Devnet",
            SolanaCluster.Testnet => "Testnet",
            _ => "Mainnet",
        };

        /// <summary>
        /// Unknown or empty input resolves to Mainnet, matching the Mobile Wallet Adapter
        /// default for an unspecified chain. Never guess a test cluster for a key whose
        /// stored chain could not be read.
        /// </summary>
        public static SolanaCluster FromChainId(string? chainId) => chainId switch
        {
            "solana:devnet" => SolanaCluster.Devnet,
            "solana:testnet" => SolanaCluster.Testnet,
            _ => SolanaCluster.Mainnet,
        };
    }
}
