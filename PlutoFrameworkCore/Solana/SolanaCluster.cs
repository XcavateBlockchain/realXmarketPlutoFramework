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

        /// <summary>
        /// The Mobile Wallet Adapter 1.0 "cluster" value, sent alongside the 2.0
        /// "chain" field in authorize. The 2.0 specification keeps it as an alias that
        /// v2 wallets ignore when "chain" is present; wallets that only read the 1.0
        /// field - Phantom included - default a missing one to mainnet, which is how a
        /// devnet authorize gets presented as a mainnet connection. The legacy mainnet
        /// value really is "mainnet-beta".
        /// </summary>
        public static string ToLegacyClusterId(this SolanaCluster cluster) => cluster switch
        {
            SolanaCluster.Devnet => "devnet",
            SolanaCluster.Testnet => "testnet",
            _ => "mainnet-beta",
        };

        /// <summary>
        /// The corresponding Solnet cluster, which selects the public RPC endpoint.
        /// </summary>
        public static Solnet.Rpc.Cluster ToSolnetCluster(this SolanaCluster cluster) => cluster switch
        {
            SolanaCluster.Devnet => Solnet.Rpc.Cluster.DevNet,
            SolanaCluster.Testnet => Solnet.Rpc.Cluster.TestNet,
            _ => Solnet.Rpc.Cluster.MainNet,
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
