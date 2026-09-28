namespace PlutoFrameworkTests
{
    /// <summary>
    /// Small synthetic Anchor IDLs: just the fields the error catalog reads (address,
    /// metadata.name, errors), so tests do not depend on the real programs staying put.
    /// The addresses are the real devnet ones, matching <c>idls/devnet/*.json</c>.
    /// </summary>
    internal static class IdlFixtures
    {
        public const string MarketplaceAddress = "dj9Q3CpHvDHwexCbkgJ5APDx4JsTxPssNebkvP15g1T";
        public const string PropertyAddress = "deCp9srk9C6P4BXJaFpjR5H6Jsm6DCq8AL2kk338dVq";

        public const string MarketplaceIdl = """
            {
              "address": "dj9Q3CpHvDHwexCbkgJ5APDx4JsTxPssNebkvP15g1T",
              "metadata": { "name": "marketplace", "version": "0.1.0", "spec": "0.1.0" },
              "instructions": [],
              "errors": [
                { "code": 6000, "name": "NotAuthority", "msg": "Signer is not the authority" },
                { "code": 6013, "name": "ListingNotActive", "msg": "Listing is not active" }
              ]
            }
            """;

        public const string PropertyIdl = """
            {
              "address": "deCp9srk9C6P4BXJaFpjR5H6Jsm6DCq8AL2kk338dVq",
              "metadata": { "name": "property", "version": "0.1.0", "spec": "0.1.0" },
              "instructions": [],
              "errors": [
                { "code": 6013, "name": "PostcodeTooLong", "msg": "Postcode is too long" }
              ]
            }
            """;
    }
}
