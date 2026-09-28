using System.Reflection;

namespace PlutoFrameworkCore.Solana
{
    /// <summary>
    /// The IDL error catalog of each cluster, registered once at app startup and read
    /// wherever a transaction failure is turned into words. A cluster with no bundled
    /// IDLs - or an app that never registered - simply has no catalog, and every decoder
    /// falls back to the node's raw reason.
    /// </summary>
    public static class SolanaProgramErrorCatalogs
    {
        private static readonly Dictionary<SolanaCluster, AnchorIdlErrorCatalog> Catalogs = new();

        public static AnchorIdlErrorCatalog? Get(SolanaCluster cluster) =>
            Catalogs.TryGetValue(cluster, out var catalog) ? catalog : null;

        public static void Register(SolanaCluster cluster, AnchorIdlErrorCatalog catalog) =>
            Catalogs[cluster] = catalog;

        /// <summary>
        /// Builds the cluster's catalog from every embedded resource whose name starts
        /// with <paramref name="resourceNamePrefix"/> - the shape the app bundles
        /// <c>idls/{cluster}/*.json</c> as. Returns how many programs loaded; files that
        /// do not parse are skipped, so one stale or truncated IDL never blocks the rest.
        /// Nothing is registered when no resource matched.
        /// </summary>
        public static int RegisterFromAssembly(Assembly assembly, string resourceNamePrefix, SolanaCluster cluster)
        {
            var names = assembly.GetManifestResourceNames()
                .Where(name => name.StartsWith(resourceNamePrefix, StringComparison.Ordinal))
                .ToList();

            if (names.Count == 0)
            {
                return 0;
            }

            var catalog = new AnchorIdlErrorCatalog();
            var loaded = 0;

            foreach (var name in names)
            {
                using var stream = assembly.GetManifestResourceStream(name);

                if (stream is null)
                {
                    continue;
                }

                using var reader = new StreamReader(stream);

                string json;

                try
                {
                    json = reader.ReadToEnd();
                }
                catch (Exception)
                {
                    continue;
                }

                if (catalog.TryAddIdl(json))
                {
                    loaded++;
                }
            }

            Register(cluster, catalog);

            return loaded;
        }

        /// <summary>Drops every registration. Test isolation; the app registers once.</summary>
        internal static void Clear() => Catalogs.Clear();
    }
}
