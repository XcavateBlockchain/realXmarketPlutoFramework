namespace PlutoFramework.Model.Xcavate
{
    /// <summary>
    /// The ElectroDB key encoding of the DynamoDB table the web app writes developer property
    /// records to, and the key condition a query against it is built from. The table is shared
    /// with the web app and keyed by an ElectroDB entity, so the keys are not plain ids - they
    /// are composed strings, and getting one character wrong returns an empty list with no
    /// error at all. Every piece of that shape was read off the live table rather than derived
    /// from the entity definition:
    /// </summary>
    /// <code>
    /// pk       $realxmarket#propertyid_prp_5zpvkvwr4u4y
    /// sk       $property_1
    /// gsi1pk   $realxmarket#companyid_company_4vaesqkcsxbxlhjnfz3jiq
    /// gsi1sk   $property_1#status_listed#propertyid_prp_5zpvkvwr4u4y
    /// </code>
    /// <remarks>
    /// Two details of the encoding matter. Every key value is stored lower-cased - the ids
    /// themselves are mixed-case base58-style strings, so <see cref="ToKeyPart"/> has to
    /// lowercase them or the lookup simply misses. And the sort key is the entity plus the two
    /// attributes that make up the index, in the order the entity declares them, joined by
    /// <c>#</c>: that is why a status filter is a <c>begins_with</c> on
    /// <c>$property_1#status_listed#</c> and the All filter is the shorter
    /// <c>$property_1#status_</c>.
    /// </remarks>
    public static class DeveloperPropertyQuery
    {
        private const string Service = "realxmarket";
        private const string Entity = "property";
        private const int EntityVersion = 1;

        /// <summary>The lower-cased wire form of a status, as it sits inside the sort key.</summary>
        public static string ToStatusKeyPart(DeveloperPropertyStatus status) =>
            status.ToString().ToLowerInvariant();

        /// <summary>
        /// ElectroDB's key encoding of a stored value: lower-cased, because the index keys are
        /// case-folded and the ids themselves are not.
        /// </summary>
        public static string ToKeyPart(string value) => value.Trim().ToLowerInvariant();

        /// <summary>The company the properties are filed under.</summary>
        public static string BuildCompanyKey(string companyId) =>
            $"${Service}#companyid_{ToKeyPart(companyId)}";

        /// <summary>The wallet that created the properties.</summary>
        public static string BuildUserKey(string userId) =>
            $"${Service}#userid_{ToKeyPart(userId)}";

        /// <summary>
        /// The sort-key prefix a query matches on: one status when the caller filtered, every
        /// status when it did not. The trailing <c>#</c> on the filtered form is what stops a
        /// filter for <c>verified</c> from also matching a status that merely starts the same way.
        /// </summary>
        public static string BuildStatusPrefix(DeveloperPropertyStatus? status) =>
            status is null
                ? $"${Entity}_{EntityVersion}#status_"
                : $"${Entity}_{EntityVersion}#status_{ToStatusKeyPart(status.Value)}#";

        /// <summary>
        /// The key condition for one page of the query, and the values its placeholders resolve
        /// to. DynamoDB ties the two together by name, colon included: every <c>:token</c> in
        /// the expression must appear as a key in the values map, and a key that is not a
        /// placeholder - the attribute's own name, say - is rejected as a syntax error before
        /// the expression is even read.
        /// </summary>
        public static (string Expression, IReadOnlyDictionary<string, string> Values) BuildKeyCondition(
            string partitionKey,
            string sortKey,
            string key,
            string prefix) =>
            (
                $"{partitionKey} = :key AND begins_with({sortKey}, :prefix)",
                new Dictionary<string, string>
                {
                    [":key"] = key,
                    [":prefix"] = prefix,
                }
            );

        public static DeveloperPropertyStatus ParseStatus(string? value) =>
            Enum.TryParse<DeveloperPropertyStatus>(value, ignoreCase: true, out var parsed)
                ? parsed
                // A status the app does not know is still a record; it shows as a draft rather
                // as vanishing, which is what a reader on the All tab would want.
                : DeveloperPropertyStatus.Draft;
    }
}
