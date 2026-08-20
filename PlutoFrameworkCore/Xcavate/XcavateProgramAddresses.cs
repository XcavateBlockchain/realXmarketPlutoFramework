using PlutoFrameworkCore.Solana;

namespace PlutoFramework.Model.Xcavate
{
    /// <summary>
    /// The deployed addresses of one cluster's Xcavate Solana programs.
    /// </summary>
    public sealed record XcavateProgramSet
    {
        public required string Marketplace { get; init; }
        public required string Whitelist { get; init; }
        public required string Property { get; init; }
        public required string Regions { get; init; }
    }

    /// <summary>
    /// Where the Xcavate Solana programs are deployed, per cluster.
    /// <para>
    /// The addresses are transcribed from the checked-in IDLs - each file's top-level
    /// <c>address</c> field - so <c>idls/devnet/*.json</c> is the source of truth for
    /// devnet. When the mainnet programs deploy, their IDLs land in
    /// <c>idls/mainnet/</c> (a placeholder folder today) and <see cref="Mainnet"/> is
    /// filled in from them; nothing else has to change.
    /// </para>
    /// </summary>
    public static class XcavateProgramAddresses
    {
        /// <summary>From idls/devnet/: marketplace.json, xcavate_whitelist.json, property.json, regions.json.</summary>
        public static readonly XcavateProgramSet Devnet = new()
        {
            Marketplace = "B6YRVAmjmhN28smZxNfCnuKc19CamBbAEMXsp5KTfWog",
            Whitelist = "2vVARM46pPD4rcHdbXHnYA4vTGN14q6skQAzsQWcHUxn",
            Property = "8f4NHc1wGBM1BAufDFd9dNechLW8pxmStSfxfuJfDzob",
            Regions = "FYysH5v23qtz4gK4H1yLDHneFwx6PSAT7oQwHcuRyRh",
        };

        /// <summary>
        /// Placeholder. The Xcavate programs are not deployed to Solana mainnet yet; fill
        /// this in from idls/mainnet/ when those IDLs are provided.
        /// </summary>
        public static readonly XcavateProgramSet? Mainnet = null;

        /// <summary>
        /// The program set for <paramref name="cluster"/>, or null where the programs are
        /// not deployed. Testnet is deliberately absent: nothing of Xcavate's runs there.
        /// </summary>
        public static XcavateProgramSet? Get(SolanaCluster cluster) => cluster switch
        {
            SolanaCluster.Devnet => Devnet,
            SolanaCluster.Mainnet => Mainnet,
            _ => null,
        };

        /// <exception cref="NotSupportedException">
        /// The programs are not deployed on <paramref name="cluster"/>. Thrown rather than
        /// returning null so a caller building a transaction fails with the real reason
        /// instead of a null-reference downstream.
        /// </exception>
        public static XcavateProgramSet Require(SolanaCluster cluster) =>
            Get(cluster) ?? throw new NotSupportedException(
                $"The Xcavate programs are not deployed on Solana {cluster.GetName()}.");
    }
}
