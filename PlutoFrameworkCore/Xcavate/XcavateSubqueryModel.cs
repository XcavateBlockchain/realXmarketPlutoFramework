using XcavateSubquery;
using StrawberryShake;
using UniqueryPlus.Nfts;
using Microsoft.Extensions.DependencyInjection;

namespace PlutoFramework.Model.Xcavate
{
    public class XcavateSubqueryModel
    {
        public static IXcavateSubquery GetXcavateSubqueryClient()
        {
            var serviceCollection = new ServiceCollection();

            serviceCollection
                .AddXcavateSubquery()
                .ConfigureHttpClient(client => client.BaseAddress = new Uri("https://api.subquery.network/sq/XcavateBlockchain/realxmarket"));

            IServiceProvider services = serviceCollection.BuildServiceProvider();

            return services.GetRequiredService<IXcavateSubquery>();
        }
        public static async Task<IEnumerable<INftBase>> GetPropertiesForSaleAsync(int limit = 25, int offset = 0, CancellationToken token = default)
        {
            var subqueryClient = GetXcavateSubqueryClient();

            var result = await subqueryClient.PropertyListings.ExecuteAsync(limit, offset).ConfigureAwait(false);

            result.EnsureNoErrors();

            if (result.Data is null || result.Data.PropertyListings is null)
            {
                Console.WriteLine("Was null");
                return [];
            }

            return [];
        }
    }
}
