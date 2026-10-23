using PlutoFramework.Model.Xcavate;
using System.Text.RegularExpressions;

namespace PlutoFrameworkTests
{
    /// <summary>
    /// The DynamoDB query the developer page's property list is built from: the key-condition
    /// expression together with the values its placeholders resolve to, and the ElectroDB key
    /// encoding the shared table is keyed on. The encoding was read off the live table
    /// (docs/developer-page-dynamo-findings.md), so these values are the store's real shape.
    /// </summary>
    public class DeveloperPropertyClientTests
    {
        [Test]
        public void BuildKeyCondition_EveryPlaceholderHasExactlyOneValue()
        {
            // DynamoDB ties the two together by name: a placeholder without a value fails the
            // query, and a value without a placeholder fails it too ("contains invalid key").
            var (expression, values) = DeveloperPropertyQuery.BuildKeyCondition(
                "gsi1pk",
                "gsi1sk",
                "$realxmarket#companyid_company_4vaesqkcsxbxlhjnfz3jiq",
                "$property_1#status_");

            var placeholders = Regex.Matches(expression, @":[a-zA-Z0-9_]+")
                .Select(match => match.Value)
                .ToArray();

            Assert.That(values.Keys, Is.EquivalentTo(placeholders));
        }

        [Test]
        public void BuildKeyCondition_ValueKeysCarryTheColonPrefix()
        {
            // The live failure this guards: keys of "gsi1pk" and "prefix" in place of ":key"
            // and ":prefix", which the server rejects as a syntax error before it even looks
            // at the expression.
            var (_, values) = DeveloperPropertyQuery.BuildKeyCondition(
                "gsi2pk", "gsi2sk", "key", "prefix");

            Assert.That(values.Keys, Has.All.StartsWith(":"));
        }

        [Test]
        public void BuildKeyCondition_ConditionsThePartitionAndSortKeyItWasGiven()
        {
            var (expression, _) = DeveloperPropertyQuery.BuildKeyCondition(
                "gsi1pk", "gsi1sk", "key", "prefix");

            Assert.That(expression, Does.Contain("gsi1pk = :key"));
            Assert.That(expression, Does.Contain("begins_with(gsi1sk, :prefix)"));
        }

        [Test]
        public void BuildKeyCondition_ValuesAreTheKeyAndPrefixGiven()
        {
            var (_, values) = DeveloperPropertyQuery.BuildKeyCondition(
                "gsi1pk", "gsi1sk", "$realxmarket#companyid_x", "$property_1#status_listed#");

            Assert.That(values[":key"], Is.EqualTo("$realxmarket#companyid_x"));
            Assert.That(values[":prefix"], Is.EqualTo("$property_1#status_listed#"));
        }

        [Test]
        public void BuildCompanyKey_LowerCasesTheIdIntoTheElectroDbShape()
        {
            // The ids are mixed-case base58-style strings and the table's keys are case-folded,
            // so a key that keeps the casing simply misses.
            Assert.That(
                DeveloperPropertyQuery.BuildCompanyKey("company_4vAESqKcSxBXlHjNFZ3JiQ"),
                Is.EqualTo("$realxmarket#companyid_company_4vaesqkcsxbxlhjnfz3jiq"));
        }

        [Test]
        public void BuildUserKey_LowerCasesTheWalletIntoTheElectroDbShape()
        {
            Assert.That(
                DeveloperPropertyQuery.BuildUserKey("3oVtApF8dSfZjSeBcI8Tm6Fq7XzGwf2J2weweeje36q5"),
                Is.EqualTo("$realxmarket#userid_3ovtapf8dsfzjsebci8tm6fq7xzgwf2j2weweeje36q5"));
        }

        [Test]
        public void BuildStatusPrefix_NoFilterMatchesEveryStatus()
        {
            Assert.That(
                DeveloperPropertyQuery.BuildStatusPrefix(null),
                Is.EqualTo("$property_1#status_"));
        }

        [Test]
        public void BuildStatusPrefix_AFilterEndsInASeparator()
        {
            // The trailing '#' is what stops "verified" from matching a status that merely
            // starts the same way.
            Assert.That(
                DeveloperPropertyQuery.BuildStatusPrefix(DeveloperPropertyStatus.Listed),
                Is.EqualTo("$property_1#status_listed#"));
        }

        [Test]
        public void ParseStatus_AnUnknownStatusReadsAsADraft()
        {
            // A status the app does not know is still a record; it shows as a draft rather
            // than vanishing from the All tab.
            Assert.That(
                DeveloperPropertyQuery.ParseStatus("pending"),
                Is.EqualTo(DeveloperPropertyStatus.Draft));
        }

        [Test]
        public void PageToken_RoundTripsEveryAttributeThroughTheEncoding()
        {
            // The token carries a DynamoDB LastEvaluatedKey verbatim: the composed ElectroDB
            // keys contain '#' and '$', so any escaping slip breaks the round trip.
            var key = new Dictionary<string, (string Type, string Value)>
            {
                ["pk"] = ("S", "$realxmarket#propertyid_prp_5zpvkvwr4u4y"),
                ["sk"] = ("S", "$property_1"),
                ["gsi1pk"] = ("S", "$realxmarket#companyid_company_4vaesqkcsxbxlhjnfz3jiq"),
                ["gsi1sk"] = ("S", "$property_1#status_submitted#propertyid_prp_5zpvkvwr4u4y"),
            };

            var decoded = DeveloperPropertyQuery.DecodePageToken(
                DeveloperPropertyQuery.EncodePageToken(key));

            Assert.That(decoded, Is.Not.Null);
            Assert.That(decoded, Is.EquivalentTo(key));
        }

        [Test]
        public void PageToken_AnEmptyPositionEncodesAnEmptyKey()
        {
            var decoded = DeveloperPropertyQuery.DecodePageToken(
                DeveloperPropertyQuery.EncodePageToken(new Dictionary<string, (string Type, string Value)>()));

            Assert.That(decoded, Is.Not.Null);
            Assert.That(decoded, Is.Empty);
        }

        [Test]
        public void PageToken_AMalformedTokenDecodesToNothing()
        {
            // The token is handed out and handed back verbatim, so anything that does not
            // parse is "no valid position", not an exception.
            Assert.That(DeveloperPropertyQuery.DecodePageToken("not a token!!!"), Is.Null);
        }

        [Test]
        public void BuildPropertyKey_LowerCasesTheIdIntoTheElectroDbShape()
        {
            var (partitionKey, sortKey) = DeveloperPropertyQuery.BuildPropertyKey("prp_5ZPVkVWR4u4Y");

            Assert.That(partitionKey, Is.EqualTo("$realxmarket#propertyid_prp_5zpvkvwr4u4y"));
            Assert.That(sortKey, Is.EqualTo("$property_1"));
        }

        [Test]
        public void WithStatus_RewritesTheStatusSegmentAndKeepsTheRest()
        {
            // The index sort keys embed the status, so a status change has to follow them
            // into the key or the record keeps answering its old status's queries.
            Assert.That(
                DeveloperPropertyQuery.WithStatus(
                    "$property_1#status_submitted#propertyid_prp_5zpvkvwr4u4y",
                    DeveloperPropertyStatus.Verified),
                Is.EqualTo("$property_1#status_verified#propertyid_prp_5zpvkvwr4u4y"));
        }

        [Test]
        public void WithStatus_AKeyWithoutAStatusSegmentIsLeftAlone()
        {
            Assert.That(
                DeveloperPropertyQuery.WithStatus("$property_1", DeveloperPropertyStatus.Rejected),
                Is.EqualTo("$property_1"));
        }
    }
}
