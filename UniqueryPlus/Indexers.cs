using Microsoft.Extensions.DependencyInjection;
using XcavateIndexer;

namespace UniqueryPlus
{
    public static class Indexers
    {
        public static IXcavateIndexerClient GetXcavateIndexerClient()
        {
            var serviceCollection = new ServiceCollection();

            serviceCollection
                .AddXcavateIndexerClient()
                .ConfigureHttpClient(client => client.BaseAddress = new Uri("https://indexer.realxmarket.io/"));

            IServiceProvider services = serviceCollection.BuildServiceProvider();

            return services.GetRequiredService<IXcavateIndexerClient>();
        }
    }
}
