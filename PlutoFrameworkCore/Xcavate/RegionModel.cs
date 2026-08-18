
using PlutoFramework.Constants;
using PlutoFramework.Model.AjunaExt;

namespace PlutoFramework.Model.Xcavate
{
    public record XcavateRegion
    {
        public required EndpointEnum EndpointKey { get; init; }
        public required uint CollectionId { get; set; }
        public required uint ListingDuration { get; set; }
        public required string Owner { get; set; }
        public required uint Tax { get; set; }
        public required bool HasExpired { get; set; }
    }

    public static class RegionModel
    {
        private static Dictionary<(EndpointEnum, uint), XcavateRegion> regions = new();

        public static Task<XcavateRegion?> GetCachedRegionAsync(SubstrateClientExt client, uint regionId, CancellationToken token)
        {
            var key = (client.Endpoint.Key, regionId);
            if (regions.ContainsKey(key))
            {
                return Task.FromResult<XcavateRegion?>(regions[key]);
            }
            return GetRegionAsync(client, regionId, token);
        }

        public static Task<XcavateRegion?> GetRegionAsync(SubstrateClientExt client, uint regionId, CancellationToken token)
        {
            // Region details lived on-chain and are not available anymore.
            return Task.FromResult<XcavateRegion?>(null);
        }
    }
}
