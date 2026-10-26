using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PlutoFrameworkCore.Xcavate
{
    public static class DeveloperPropertyMetadata
    {
        private static readonly string[] StoreKeys =
        [
            "pk", "sk", "__edb_e__", "__edb_v__",
            "gsi1pk", "gsi1sk", "gsi2pk", "gsi2sk", "gsi3pk", "gsi3sk",
        ];

        public static string Serialize(JsonObject record)
        {
            var metadata = (JsonObject)record.DeepClone();

            foreach (var key in StoreKeys)
            {
                metadata.Remove(key);
            }

            return metadata.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }

        public static string BuildObjectKey(string propertyId, DateTimeOffset timestamp) =>
            string.Create(
                CultureInfo.InvariantCulture,
                $"properties/metadata/{propertyId}-{timestamp.ToUnixTimeMilliseconds()}.json");
    }
}
