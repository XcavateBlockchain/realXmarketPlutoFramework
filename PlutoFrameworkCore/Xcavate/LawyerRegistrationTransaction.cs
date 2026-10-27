using PlutoFrameworkCore.Solana;

namespace PlutoFramework.Model.Xcavate
{
    public sealed record LawyerRegistrationTransaction(string Signature, SolanaCluster Cluster)
    {
        public bool Matches(string signature, SolanaCluster cluster) =>
            Cluster == cluster && string.Equals(Signature, signature, StringComparison.Ordinal);
    }
}
