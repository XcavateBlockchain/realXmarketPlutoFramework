using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using PlutoFrameworkCore.Xcavate;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PlutoFrameworkTests
{
    public class DeveloperPropertyMetadataTests
    {
        [Test]
        public void Serialize_PreservesTheSubmittedPropertyAndOmitsStoreKeys()
        {
            var record = JsonNode.Parse("""
                {
                  "pk": "$realxmarket#propertyid_prp_123",
                  "sk": "$property_1",
                  "__edb_e__": "property",
                  "__edb_v__": "1",
                  "gsi1pk": "$realxmarket#companyid_company_123",
                  "gsi1sk": "$property_1#status_verified#propertyid_prp_123",
                  "gsi2pk": "$realxmarket#userid_wallet",
                  "gsi2sk": "$property_1#status_verified#propertyid_prp_123",
                  "gsi3pk": "$realxmarket#companywallet_wallet",
                  "gsi3sk": "$property_1#status_verified#propertyid_prp_123",
                  "propertyId": "prp_123",
                  "propertyName": "Orchard Rise",
                  "propertyImages": ["https://example.com/one.jpg", "https://example.com/two.jpg"],
                  "address": { "street": "Orchard Rise", "postCode": "AL10" },
                  "attributes": { "numberOfBedrooms": 2 },
                  "finances": { "propertyPrice": 330000, "isStampDutyPaid": false },
                  "companyLogo": "https://example.com/logo.png"
                }
                """)!.AsObject();

            using var document = JsonDocument.Parse(DeveloperPropertyMetadata.Serialize(record));
            var metadata = document.RootElement;

            Assert.That(metadata.GetProperty("propertyName").GetString(), Is.EqualTo("Orchard Rise"));
            Assert.That(metadata.GetProperty("propertyImages").GetArrayLength(), Is.EqualTo(2));
            Assert.That(metadata.GetProperty("address").GetProperty("postCode").GetString(), Is.EqualTo("AL10"));
            Assert.That(metadata.GetProperty("attributes").GetProperty("numberOfBedrooms").GetInt32(), Is.EqualTo(2));
            Assert.That(metadata.GetProperty("finances").GetProperty("propertyPrice").GetDecimal(), Is.EqualTo(330000m));
            Assert.That(metadata.GetProperty("finances").GetProperty("isStampDutyPaid").GetBoolean(), Is.False);
            Assert.That(metadata.GetProperty("companyLogo").GetString(), Is.EqualTo("https://example.com/logo.png"));
            Assert.That(metadata.EnumerateObject().Select(property => property.Name),
                Has.None.Matches<string>(name => name is "pk" or "sk" || name.StartsWith("gsi") || name.StartsWith("__")));
        }

        [Test]
        public void Serialize_DynamoRecordConversionProducesPlainJsonValues()
        {
            var item = new Dictionary<string, AttributeValue>
            {
                ["propertyName"] = new AttributeValue { S = "Orchard Rise" },
                ["propertyImages"] = new AttributeValue
                {
                    L = [new AttributeValue { S = "https://example.com/one.jpg" }],
                },
                ["finances"] = new AttributeValue
                {
                    M = new Dictionary<string, AttributeValue>
                    {
                        ["propertyPrice"] = new AttributeValue { N = "330000" },
                        ["isStampDutyPaid"] = new AttributeValue { BOOL = false },
                        ["optional"] = new AttributeValue { NULL = true },
                    },
                },
            };

            var record = JsonNode.Parse(Document.FromAttributeMap(item).ToJson())!.AsObject();
            using var document = JsonDocument.Parse(DeveloperPropertyMetadata.Serialize(record));
            var metadata = document.RootElement;

            Assert.That(metadata.GetProperty("propertyName").GetString(), Is.EqualTo("Orchard Rise"));
            Assert.That(metadata.GetProperty("propertyImages")[0].GetString(), Is.EqualTo("https://example.com/one.jpg"));
            Assert.That(metadata.GetProperty("finances").GetProperty("propertyPrice").ValueKind, Is.EqualTo(JsonValueKind.Number));
            Assert.That(metadata.GetProperty("finances").GetProperty("propertyPrice").GetDecimal(), Is.EqualTo(330000m));
            Assert.That(metadata.GetProperty("finances").GetProperty("isStampDutyPaid").GetBoolean(), Is.False);
            Assert.That(metadata.GetProperty("finances").GetProperty("optional").ValueKind, Is.EqualTo(JsonValueKind.Null));
        }

        [Test]
        public void BuildObjectKey_UsesTheWebMetadataPathAndUnixMilliseconds()
        {
            var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(1790904563166);

            Assert.That(
                DeveloperPropertyMetadata.BuildObjectKey("prp_5zpvkvWR4u4Y", timestamp),
                Is.EqualTo("properties/metadata/prp_5zpvkvWR4u4Y-1790904563166.json"));
        }
    }
}
