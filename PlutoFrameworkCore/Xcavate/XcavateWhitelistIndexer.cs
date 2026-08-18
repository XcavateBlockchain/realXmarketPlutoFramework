using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using PlutoFrameworkCore.Solana;
using XcavateDevnetIndexer;

namespace PlutoFramework.Model.Xcavate
{
    /// <summary>
    /// Where the Xcavate Solana programs are indexed, and the GraphQL client that reads them.
    /// <para>
    /// One generated client type serves every cluster - the deployments run the same indexer
    /// against the same programs, so only the base address differs. The type is named after
    /// the devnet project because that is the deployment its schema was generated from, not
    /// because it is devnet-only.
    /// </para>
    /// </summary>
    public static class XcavateWhitelistIndexer
    {
        /// <summary>
        /// The GraphQL endpoint. Note the path: <c>/graphiql</c> on the same host is the
        /// browser IDE and answers HTML, so pointing the client at it fails at deserialization
        /// rather than at connect.
        /// </summary>
        public const string DevnetUrl = "https://indexer-devnet.xcavate.io/graphql";

        /// <summary>
        /// Placeholder. Xcavate's programs are not deployed to Solana mainnet yet, so there is
        /// no mainnet indexer to point at. Fill this in when there is; nothing else has to
        /// change, and <see cref="WhitelistModel.WhitelistCluster"/> is the switch that starts
        /// using it.
        /// </summary>
        public const string? MainnetUrl = null;

        /// <summary>
        /// Built clients, keyed by cluster. A StrawberryShake client owns an
        /// <see cref="HttpClient"/> and its handler chain, so building one per role check
        /// would leak sockets - role checks happen on every gated action.
        /// </summary>
        private static readonly ConcurrentDictionary<SolanaCluster, IXcavateDevnetIndexerClient> clients = new();

        /// <summary>
        /// The indexer for <paramref name="cluster"/>, or null where none is deployed.
        /// Testnet is deliberately absent: nothing of Xcavate's runs there.
        /// </summary>
        public static string? GetUrl(SolanaCluster cluster) => cluster switch
        {
            SolanaCluster.Devnet => DevnetUrl,
            SolanaCluster.Mainnet => MainnetUrl,
            _ => null,
        };

        /// <summary>
        /// Whether <paramref name="cluster"/> can be queried at all, so callers can degrade
        /// instead of catching.
        /// </summary>
        public static bool IsSupported(SolanaCluster cluster) => GetUrl(cluster) is not null;

        /// <exception cref="NotSupportedException">
        /// No indexer is deployed for <paramref name="cluster"/>. Thrown rather than returning
        /// an empty result, because "no roles" and "nowhere to ask" must not look alike to a
        /// caller deciding whether to let a user act.
        /// </exception>
        public static IXcavateDevnetIndexerClient GetClient(SolanaCluster cluster)
        {
            return clients.GetOrAdd(cluster, static cluster =>
            {
                var url = GetUrl(cluster)
                    ?? throw new NotSupportedException(
                        $"No Xcavate indexer is deployed for Solana {cluster.GetName()}.");

                var services = new ServiceCollection();

                services
                    .AddXcavateDevnetIndexerClient()
                    .ConfigureHttpClient(client => client.BaseAddress = new Uri(url));

                return services.BuildServiceProvider().GetRequiredService<IXcavateDevnetIndexerClient>();
            });
        }
    }
}
