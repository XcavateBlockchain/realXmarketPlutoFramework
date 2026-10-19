using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Microsoft.Extensions.Configuration;
using System.Globalization;

namespace PlutoFramework.Model.Xcavate
{
    /// <summary>
    /// The off-chain lifecycle states a developer's property travels through. They are the
    /// web app's <c>PROPERTY_STATUSES</c> verbatim, and <see cref="Pending"/> is deliberately
    /// absent: the web enum has it but its page never queries it, and nothing is ever written
    /// with it, so offering it would be a pill that is always empty.
    /// </summary>
    public enum DeveloperPropertyStatus
    {
        Draft,
        Submitted,
        Verified,
        Rejected,
        Listed,
    }

    /// <summary>
    /// One property record as the developer's own store holds it: what they declared, not what
    /// the chain currently says about it. The on-chain listing may add a live price and an
    /// expired state, but those belong to the listing, and the record here is what the web
    /// developer page shows.
    /// </summary>
    public sealed record DeveloperProperty
    {
        public required string PropertyId { get; init; }

        public required DeveloperPropertyStatus Status { get; init; }

        public string? Name { get; init; }

        public string? PropertyType { get; init; }

        public string? PostCode { get; init; }

        public string? TownCity { get; init; }

        public string? Region { get; init; }

        /// <summary>The free-form location line the form asks for, used when the parts are missing.</summary>
        public string? Location { get; init; }

        public string? Street { get; init; }

        public string? ImageUrl { get; init; }

        public string? CompanyName { get; init; }

        /// <summary>The declared property price in pounds - what the web page shows as "Price".</summary>
        public decimal? PropertyPrice { get; init; }

        /// <summary>How many shares the property is split into - the web page's "Shares" figure.</summary>
        public decimal? NumberOfShares { get; init; }

        /// <summary>Declared monthly rent, the other half of the gross-yield estimate.</summary>
        public decimal? EstimatedRentalIncome { get; init; }

        /// <summary>The on-chain listing this became, when it got that far.</summary>
        public string? ListingId { get; init; }

        public DateTimeOffset? CreatedAt { get; init; }

        public DateTimeOffset? UpdatedAt { get; init; }

        /// <summary>
        /// The card's location line, built the way the web item builds it: town, region and
        /// postcode joined up, with the free-form location (then the street) as the fallback.
        /// </summary>
        public string LocationText
        {
            get
            {
                var parts = new[] { TownCity, Region, PostCode }
                    .Where(part => !string.IsNullOrWhiteSpace(part))
                    .ToArray();

                if (parts.Length > 0)
                {
                    return string.Join(", ", parts);
                }

                return Location ?? Street ?? string.Empty;
            }
        }

        /// <summary>The card's name line; a half-filled draft has none yet.</summary>
        public string NameText =>
            string.IsNullOrWhiteSpace(Name) ? "Untitled property" : Name!;

        /// <summary>The card's type badge; the web item falls back the same way.</summary>
        public string TypeText =>
            string.IsNullOrWhiteSpace(PropertyType) ? "Property" : PropertyType!;

        /// <summary>
        /// Whether there is a photo to show. Most drafts have none - the image is uploaded at
        /// the end of the form, long after the draft exists - and the card shows a house icon
        /// instead of a grey frame that looks like it failed to load.
        /// </summary>
        public bool HasImage => !string.IsNullOrWhiteSpace(ImageUrl);

        /// <summary>
        /// The declared property price, shortened the way the web item shortens it
        /// (£415k, £2.5m). A draft mid-form has no finances section at all yet, so the row
        /// stays empty rather than showing zero, which would look like a decision.
        /// </summary>
        public string PriceText
        {
            get
            {
                if (PropertyPrice is null)
                {
                    return string.Empty;
                }

                var value = PropertyPrice.Value;

                if (value >= 1_000_000m)
                {
                    var millions = value / 1_000_000m;
                    var rounded = millions % 1m == 0m
                        ? millions.ToString("0", CultureInfo.InvariantCulture)
                        : millions.ToString("0.0", CultureInfo.InvariantCulture);

                    return $"£{rounded}m";
                }

                if (value >= 1_000m)
                {
                    return $"£{Math.Round(value / 1_000m).ToString("0", CultureInfo.InvariantCulture)}k";
                }

                return $"£{value.ToString("N0", CultureInfo.InvariantCulture)}";
            }
        }

        /// <summary>Whether the finances section exists at all; without it the Shares/Price row hides.</summary>
        public bool HasFinances => PropertyPrice is not null || NumberOfShares is not null;

        /// <summary>The card's Shares figure; empty when the finances section is not filled in yet.</summary>
        public string SharesText => NumberOfShares is null
            ? string.Empty
            : NumberOfShares.Value.ToString("N0", CultureInfo.InvariantCulture);

        /// <summary>
        /// The web item's gross-yield estimate: a year of declared rent over the declared price,
        /// to one decimal. Missing or nonsensical finances yield nothing rather than a fake figure.
        /// </summary>
        public string? GrossYieldText
        {
            get
            {
                if (PropertyPrice is not { } price || EstimatedRentalIncome is not { } rent
                    || price <= 0)
                {
                    return null;
                }

                var percent = (double)(rent * 12m / price) * 100d;

                if (!double.IsFinite(percent))
                {
                    return null;
                }

                return $"{percent.ToString("0.0", CultureInfo.InvariantCulture)}%";
            }
        }

        public bool HasGrossYield => GrossYieldText is not null;

        /// <summary>The status as a reader sees it, with the one word the enum had to shorten.</summary>
        public string StatusText =>
            Status switch
            {
                DeveloperPropertyStatus.Draft => "Draft",
                DeveloperPropertyStatus.Submitted => "Submitted",
                DeveloperPropertyStatus.Verified => "Verified",
                DeveloperPropertyStatus.Rejected => "Rejected",
                DeveloperPropertyStatus.Listed => "Listed",
                _ => Status.ToString(),
            };
    }

    /// <summary>
    /// Reads the developer's property records out of the DynamoDB table the web app writes
    /// them to, so the mobile page and the web page show the same list.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The table is shared with the web app and keyed by an ElectroDB entity, so the keys are
    /// not plain ids - they are composed strings, and getting one character wrong returns an
    /// empty list with no error at all. Every piece of that shape below was read off the live
    /// table rather than derived from the entity definition:
    /// </para>
    /// <code>
    /// pk       $realxmarket#propertyid_prp_5zpvkvwr4u4y
    /// sk       $property_1
    /// gsi1pk   $realxmarket#companyid_company_4vaesqkcsxbxlhjnfz3jiq
    /// gsi1sk   $property_1#status_listed#propertyid_prp_5zpvkvwr4u4y
    /// </code>
    /// <para>
    /// Two details of the encoding matter. Every key value is stored lower-cased - the ids
    /// themselves are mixed-case base58-style strings, so <see cref="ToKeyPart"/> has to
    /// lowercase them or the lookup simply misses. And the sort key is the entity plus the two
    /// attributes that make up the index, in the order the entity declares them, joined by
    /// <c>#</c>: that is why a status filter is a <c>begins_with</c> on
    /// <c>$property_1#status_listed#</c> and the All filter is the shorter
    /// <c>$property_1#status_</c>.
    /// </para>
    /// <para>
    /// Sort keys of the same status group those records together, so a multi-page read is
    /// sorted by hand afterwards - the web page leaves the order to the index and shows the
    /// same status grouping, but a phone-sized list reads better newest first.
    /// </para>
    /// <para>
    /// Everything about this type is optional in the same way the app's AWS usage already is:
    /// missing credentials give a null client and an empty result, never an exception. The
    /// table is a dev environment store that an unauthenticated app cannot assume anything
    /// about.
    /// </para>
    /// </remarks>
    public static class DeveloperPropertyClient
    {
        private const string Service = "realxmarket";
        private const string Entity = "property";
        private const int EntityVersion = 1;
        private const string DefaultTableName = "realxmarket-app";
        private const string CompanyIndex = "gsi1pk-gsi1sk-index";
        private const string UserIndex = "gsi2pk-gsi2sk-index";

        /// <summary>
        /// The most records a single read collects before giving up paging. Real developer
        /// accounts hold a handful of properties, so this is a runaway guard, not a page size.
        /// </summary>
        private const int MaxRecords = 200;

        private const int RecordsPerRequest = 100;

        private static readonly object ClientLock = new();
        private static IAmazonDynamoDB? cachedClient;
        private static string? cachedTableName;
        private static bool clientInitialized;

        /// <summary>
        /// The store's readable status values, in lifecycle order. The page's pills are built
        /// from this so the pills and the query cannot drift apart.
        /// </summary>
        public static IReadOnlyList<DeveloperPropertyStatus> Statuses { get; } =
            new[]
            {
                DeveloperPropertyStatus.Draft,
                DeveloperPropertyStatus.Submitted,
                DeveloperPropertyStatus.Verified,
                DeveloperPropertyStatus.Rejected,
                DeveloperPropertyStatus.Listed,
            };

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
        /// True when the store is reachable as configured, so callers can tell "nothing to
        /// show" apart from "no store configured" without running a query.
        /// </summary>
        public static bool IsConfigured => GetClient() is not null;

        /// <summary>
        /// The developer's property records, newest first.
        /// </summary>
        /// <param name="companyId">
        /// The developer's selected company - the same record the web page resolves from the
        /// session before it queries, and what the table is actually keyed on.
        /// </param>
        /// <param name="userId">
        /// The wallet, used only when no company resolved. This is the table's by-user index,
        /// which is how the web action reads properties without a company.
        /// </param>
        /// <param name="status">The pill the developer is on; null means every status.</param>
        /// <returns>
        /// The matching records, or nothing when there is no developer, no company, no store,
        /// or no matching properties. A wire failure is logged and surfaces as an empty list -
        /// the page's empty state is the honest answer to any of those.
        /// </returns>
        public static async Task<IReadOnlyList<DeveloperProperty>> GetPropertiesAsync(
            string? companyId,
            string? userId,
            DeveloperPropertyStatus? status,
            CancellationToken cancellationToken = default)
        {
            var client = GetClient();

            var byCompany = !string.IsNullOrWhiteSpace(companyId);
            var byUser = !byCompany && !string.IsNullOrWhiteSpace(userId);

            if (client is null || (!byCompany && !byUser))
            {
                return [];
            }

            var index = byCompany ? CompanyIndex : UserIndex;
            var partitionKey = byCompany ? $"gsi1pk" : "gsi2pk";
            var sortKey = byCompany ? "gsi1sk" : "gsi2sk";
            var key = byCompany ? BuildCompanyKey(companyId!) : BuildUserKey(userId!);
            var prefix = BuildStatusPrefix(status);

            var records = new List<DeveloperProperty>();

            try
            {
                Dictionary<string, AttributeValue>? startKey = null;

                while (records.Count < MaxRecords)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var request = new QueryRequest
                    {
                        TableName = GetTableName(),
                        IndexName = index,
                        Limit = RecordsPerRequest,
                        KeyConditionExpression = $"{partitionKey} = :key AND begins_with({sortKey}, :prefix)",
                        ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                        {
                            [partitionKey] = new AttributeValue(key),
                            ["prefix"] = new AttributeValue(prefix),
                        },
                    };

                    if (startKey is not null)
                    {
                        request.ExclusiveStartKey = startKey;
                    }

                    var response = await client.QueryAsync(request, cancellationToken)
                        .ConfigureAwait(false);

                    foreach (var item in response.Items)
                    {
                        records.Add(Parse(item));
                    }

                    if (response.LastEvaluatedKey.Count == 0)
                    {
                        break;
                    }

                    startKey = response.LastEvaluatedKey;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Developer property store query failed:");
                Console.WriteLine(ex);
                return [];
            }

            // The index orders by status then id, which is a grouping and not a recency.
            return [.. records
                .OrderByDescending(property => property.UpdatedAt ?? property.CreatedAt)
                .ThenByDescending(property => property.CreatedAt)];
        }

        /// <summary>
        /// Reads one record out of a response item. Every field is optional here: the web form
        /// saves a draft long before it has an address, an image or a company attached, and a
        /// half-filled record is the normal case, not the exception.
        /// </summary>
        private static DeveloperProperty Parse(IDictionary<string, AttributeValue> item)
        {
            return new DeveloperProperty
            {
                PropertyId = Text(item, "propertyId") ?? string.Empty,
                Status = ParseStatus(Text(item, "status")),
                Name = Text(item, "propertyName"),
                PropertyType = Text(item, "propertyType"),
                PostCode = MapText(item, "address", "postCode"),
                TownCity = MapText(item, "address", "townCity"),
                Region = MapText(item, "address", "region"),
                Location = MapText(item, "address", "location"),
                Street = MapText(item, "address", "street"),
                ImageUrl = FirstOf(item, "propertyImages"),
                CompanyName = Text(item, "companyName"),
                PropertyPrice = MapDecimal(item, "finances", "propertyPrice"),
                NumberOfShares = MapDecimal(item, "finances", "numberOfShares"),
                EstimatedRentalIncome = MapDecimal(item, "finances", "estimatedRentalIncome"),
                ListingId = Text(item, "listingId"),
                CreatedAt = Timestamp(item, "createdAt"),
                UpdatedAt = Timestamp(item, "updatedAt"),
            };
        }

        public static DeveloperPropertyStatus ParseStatus(string? value) =>
            Enum.TryParse<DeveloperPropertyStatus>(value, ignoreCase: true, out var parsed)
                ? parsed
                // A status the app does not know is still a record; it shows as a draft rather
                // as vanishing, which is what a reader on the All tab would want.
                : DeveloperPropertyStatus.Draft;

        private static string? Text(IDictionary<string, AttributeValue> item, string name) =>
            item.TryGetValue(name, out var value)
                ? value.S ?? value.N
                : null;

        private static string? MapText(IDictionary<string, AttributeValue> item, string map, string key) =>
            item.TryGetValue(map, out var value) && value.M != null
                ? Text(value.M, key)
                : null;

        private static decimal? MapDecimal(IDictionary<string, AttributeValue> item, string map, string key)
        {
            if (!item.TryGetValue(map, out var value) || value.M == null
                || !value.M.TryGetValue(key, out var entry) || entry.N == null)
            {
                return null;
            }

            return decimal.TryParse(entry.N, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;
        }

        private static string? FirstOf(IDictionary<string, AttributeValue> item, string name)
        {
            if (!item.TryGetValue(name, out var value) || value.L == null || value.L.Count == 0)
            {
                return null;
            }

            return value.L[0].S;
        }

        private static DateTimeOffset? Timestamp(IDictionary<string, AttributeValue> item, string name)
        {
            var text = Text(item, name);

            return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal, out var parsed)
                ? parsed
                : null;
        }

        private static IAmazonDynamoDB? GetClient()
        {
            lock (ClientLock)
            {
                if (clientInitialized)
                {
                    return cachedClient;
                }

                clientInitialized = true;

                try
                {
                    var configuration = MauiAppBuilderExtensions.Services.GetService<IConfiguration>();
                    var accessKey = configuration?.GetValue<string>("PROPERTY_DB_ACCESS_KEY");
                    var secretKey = configuration?.GetValue<string>("PROPERTY_DB_SECRET_KEY");
                    cachedTableName = configuration?.GetValue<string>("PROPERTY_DB_TABLE_NAME");

                    if (string.IsNullOrWhiteSpace(accessKey) || string.IsNullOrWhiteSpace(secretKey))
                    {
                        return null;
                    }

                    cachedClient = new AmazonDynamoDBClient(
                        accessKey, secretKey, RegionEndpoint.EUWest1);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }

                return cachedClient;
            }
        }

        private static string GetTableName() =>
            string.IsNullOrWhiteSpace(cachedTableName) ? DefaultTableName : cachedTableName!;
    }
}
