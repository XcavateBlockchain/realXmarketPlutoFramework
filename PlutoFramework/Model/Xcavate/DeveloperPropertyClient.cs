using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Microsoft.Extensions.Configuration;
using System.Globalization;

namespace PlutoFramework.Model.Xcavate
{
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
    /// One page of a paged property read: the records the store returned and the token the
    /// next page is requested with, null when the read has reached the end.
    /// </summary>
    public sealed record DeveloperPropertyPage(
        IReadOnlyList<DeveloperProperty> Items,
        string? NextPageToken);

    /// <summary>
    /// Reads the developer's property records out of the DynamoDB table the web app writes
    /// them to, so the mobile page and the web page show the same list.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The table is shared with the web app and keyed by an ElectroDB entity, so the keys are
    /// composed strings rather than plain ids; <see cref="DeveloperPropertyQuery"/> owns that
    /// encoding and the key condition each page is queried with.
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
            var request = BuildQueryRequest(companyId, userId, status, RecordsPerRequest);

            if (client is null || request is null)
            {
                return [];
            }

            var records = new List<DeveloperProperty>();

            try
            {
                while (records.Count < MaxRecords)
                {
                    cancellationToken.ThrowIfCancellationRequested();

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

                    request.ExclusiveStartKey = response.LastEvaluatedKey;
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
        /// One page of the developer's property records, for a list that loads more as it is
        /// scrolled rather than reading the whole store up front.
        /// </summary>
        /// <remarks>
        /// Unlike <see cref="GetPropertiesAsync"/> there is no cross-page recency sort here:
        /// sorting needs the whole answer, and the whole answer is exactly what paging avoids.
        /// Pages arrive in the index's own order - status, then property id.
        /// </remarks>
        /// <param name="limit">The most records this page holds.</param>
        /// <param name="pageToken">
        /// The <see cref="DeveloperPropertyPage.NextPageToken"/> the previous page returned;
        /// null asks for the first page. A token that does not decode ends the list rather
        /// than restarting it silently.
        /// </param>
        public static async Task<DeveloperPropertyPage> GetPropertiesPageAsync(
            string? companyId,
            string? userId,
            DeveloperPropertyStatus? status,
            int limit,
            string? pageToken,
            CancellationToken cancellationToken = default)
        {
            var client = GetClient();
            var request = BuildQueryRequest(companyId, userId, status, limit);

            if (client is null || request is null)
            {
                return new DeveloperPropertyPage([], null);
            }

            if (pageToken is not null)
            {
                var startKey = DecodePageToken(pageToken);

                if (startKey is null)
                {
                    return new DeveloperPropertyPage([], null);
                }

                request.ExclusiveStartKey = startKey;
            }

            try
            {
                var response = await client.QueryAsync(request, cancellationToken)
                    .ConfigureAwait(false);

                var nextToken = response.LastEvaluatedKey.Count == 0
                    ? null
                    : EncodePageToken(response.LastEvaluatedKey);

                return new DeveloperPropertyPage(
                    [.. response.Items.Select(Parse)],
                    nextToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Developer property store query failed:");
                Console.WriteLine(ex);
                return new DeveloperPropertyPage([], null);
            }
        }

        /// <summary>
        /// Sets one record's status - the mobile half of the web repo's db:status script:
        /// read the record, leave an already-correct status alone, and patch the rest.
        /// </summary>
        /// <remarks>
        /// The patch rewrites more than the status attribute: every index sort key the item
        /// carries embeds the status, so each one present is rebuilt with the new value -
        /// otherwise the record would keep answering its old status's queries. The update is
        /// conditioned on the status the read just saw, so two admins deciding at once cannot
        /// both win; the loser gets the record as it actually stands.
        /// </remarks>
        /// <returns>
        /// The record as it stands after the call, or null when the record is gone, the store
        /// is not configured, or the write failed - the caller's honest answer to any of those
        /// is "the decision did not go through".
        /// </returns>
        public static async Task<DeveloperProperty?> UpdatePropertyStatusAsync(
            string propertyId,
            DeveloperPropertyStatus status,
            CancellationToken cancellationToken = default)
        {
            var client = GetClient();

            if (client is null || string.IsNullOrWhiteSpace(propertyId))
            {
                return null;
            }

            var (partitionKey, sortKey) = DeveloperPropertyQuery.BuildPropertyKey(propertyId);
            var key = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue(partitionKey),
                ["sk"] = new AttributeValue(sortKey),
            };

            try
            {
                var existing = await client.GetItemAsync(new GetItemRequest
                    {
                        TableName = GetTableName(),
                        Key = key,
                        // A decision is only valid against the record as it stands right now.
                        ConsistentRead = true,
                    }, cancellationToken)
                    .ConfigureAwait(false);

                if (!existing.IsItemSet)
                {
                    return null;
                }

                var previous = DeveloperPropertyQuery.ParseStatus(Text(existing.Item, "status"));

                if (previous == status)
                {
                    return Parse(existing.Item);
                }

                // "status" is a DynamoDB reserved word, so the expression names it indirectly.
                var names = new Dictionary<string, string> { ["#st"] = "status" };
                var values = new Dictionary<string, AttributeValue>
                {
                    [":status"] = new AttributeValue(DeveloperPropertyQuery.ToStatusKeyPart(status)),
                    [":previous"] = new AttributeValue(DeveloperPropertyQuery.ToStatusKeyPart(previous)),
                    [":updatedAt"] = new AttributeValue(
                        DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture)),
                };
                var sets = new List<string> { "#st = :status", "updatedAt = :updatedAt" };

                foreach (var indexSortKey in new[] { "gsi1sk", "gsi2sk", "gsi3sk" })
                {
                    if (Text(existing.Item, indexSortKey) is not { } current)
                    {
                        continue;
                    }

                    names[$"#{indexSortKey}"] = indexSortKey;
                    values[$":{indexSortKey}"] = new AttributeValue(
                        DeveloperPropertyQuery.WithStatus(current, status));
                    sets.Add($"#{indexSortKey} = :{indexSortKey}");
                }

                var updated = await client.UpdateItemAsync(new UpdateItemRequest
                    {
                        TableName = GetTableName(),
                        Key = key,
                        UpdateExpression = "SET " + string.Join(", ", sets),
                        ConditionExpression = "#st = :previous",
                        ExpressionAttributeNames = names,
                        ExpressionAttributeValues = values,
                        ReturnValues = ReturnValue.ALL_NEW,
                    }, cancellationToken)
                    .ConfigureAwait(false);

                return Parse(updated.Attributes);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ConditionalCheckFailedException)
            {
                // Someone else decided first; answer with the record as it actually stands.
                try
                {
                    var current = await client.GetItemAsync(new GetItemRequest
                        {
                            TableName = GetTableName(),
                            Key = key,
                            ConsistentRead = true,
                        }, cancellationToken)
                        .ConfigureAwait(false);

                    return current.IsItemSet ? Parse(current.Item) : null;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Developer property status re-read failed:");
                    Console.WriteLine(ex);
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Developer property status update failed:");
                Console.WriteLine(ex);
                return null;
            }
        }

        /// <summary>
        /// Marks one Verified record as Listed and pins the on-chain listing id to it - the
        /// record-keeping half of the developer's list_property transaction, run once the
        /// transaction confirms on chain.
        /// </summary>
        /// <remarks>
        /// Same store mechanics as <see cref="UpdatePropertyStatusAsync"/>: the status moves,
        /// so every index sort key the item carries is rebuilt with the new value alongside
        /// the listingId write. The update is conditioned on the record still being Verified,
        /// so a second listing attempt (or an admin's reversal) cannot silently re-list.
        /// </remarks>
        /// <returns>
        /// The record as it stands after the call, or null when the record is gone, the store
        /// is not configured, the write failed, or the record was no longer Verified - the
        /// on-chain listing then exists without its record link, which the caller reports.
        /// </returns>
        public static async Task<DeveloperProperty?> MarkPropertyListedAsync(
            string propertyId,
            ulong listingId,
            CancellationToken cancellationToken = default)
        {
            var client = GetClient();

            if (client is null || string.IsNullOrWhiteSpace(propertyId))
            {
                return null;
            }

            var (partitionKey, sortKey) = DeveloperPropertyQuery.BuildPropertyKey(propertyId);
            var key = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue(partitionKey),
                ["sk"] = new AttributeValue(sortKey),
            };

            try
            {
                var existing = await client.GetItemAsync(new GetItemRequest
                    {
                        TableName = GetTableName(),
                        Key = key,
                        // A listing is only valid against the record as it stands right now.
                        ConsistentRead = true,
                    }, cancellationToken)
                    .ConfigureAwait(false);

                if (!existing.IsItemSet)
                {
                    return null;
                }

                var previous = DeveloperPropertyQuery.ParseStatus(Text(existing.Item, "status"));

                if (previous == DeveloperPropertyStatus.Listed)
                {
                    return Parse(existing.Item);
                }

                // "status" is a DynamoDB reserved word, so the expression names it indirectly.
                var names = new Dictionary<string, string> { ["#st"] = "status" };
                var values = new Dictionary<string, AttributeValue>
                {
                    [":status"] = new AttributeValue(
                        DeveloperPropertyQuery.ToStatusKeyPart(DeveloperPropertyStatus.Listed)),
                    [":verified"] = new AttributeValue(
                        DeveloperPropertyQuery.ToStatusKeyPart(DeveloperPropertyStatus.Verified)),
                    [":listingId"] = new AttributeValue(
                        listingId.ToString(CultureInfo.InvariantCulture)),
                    [":updatedAt"] = new AttributeValue(
                        DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture)),
                };
                var sets = new List<string> { "#st = :status", "listingId = :listingId", "updatedAt = :updatedAt" };

                foreach (var indexSortKey in new[] { "gsi1sk", "gsi2sk", "gsi3sk" })
                {
                    if (Text(existing.Item, indexSortKey) is not { } current)
                    {
                        continue;
                    }

                    names[$"#{indexSortKey}"] = indexSortKey;
                    values[$":{indexSortKey}"] = new AttributeValue(
                        DeveloperPropertyQuery.WithStatus(current, DeveloperPropertyStatus.Listed));
                    sets.Add($"#{indexSortKey} = :{indexSortKey}");
                }

                var updated = await client.UpdateItemAsync(new UpdateItemRequest
                    {
                        TableName = GetTableName(),
                        Key = key,
                        UpdateExpression = "SET " + string.Join(", ", sets),
                        ConditionExpression = "#st = :verified",
                        ExpressionAttributeNames = names,
                        ExpressionAttributeValues = values,
                        ReturnValues = ReturnValue.ALL_NEW,
                    }, cancellationToken)
                    .ConfigureAwait(false);

                return Parse(updated.Attributes);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ConditionalCheckFailedException)
            {
                // The record was no longer Verified - nothing to re-read: the caller's
                // answer to a failed condition is "the record could not be updated".
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Developer property listed-marking failed:");
                Console.WriteLine(ex);
                return null;
            }
        }

        /// <summary>
        /// The query one page of the read is run with, or null when there is nothing to key it
        /// on - no developer's company and no wallet. Shared by the whole-list read and the
        /// paged one so the two cannot drift apart on how the store is asked.
        /// </summary>
        private static QueryRequest? BuildQueryRequest(
            string? companyId,
            string? userId,
            DeveloperPropertyStatus? status,
            int limit)
        {
            var byCompany = !string.IsNullOrWhiteSpace(companyId);
            var byUser = !byCompany && !string.IsNullOrWhiteSpace(userId);

            if (!byCompany && !byUser)
            {
                return null;
            }

            var key = byCompany
                ? DeveloperPropertyQuery.BuildCompanyKey(companyId!)
                : DeveloperPropertyQuery.BuildUserKey(userId!);
            var prefix = DeveloperPropertyQuery.BuildStatusPrefix(status);

            var (keyCondition, conditionValues) = DeveloperPropertyQuery.BuildKeyCondition(
                byCompany ? "gsi1pk" : "gsi2pk",
                byCompany ? "gsi1sk" : "gsi2sk",
                key,
                prefix);

            return new QueryRequest
            {
                TableName = GetTableName(),
                IndexName = byCompany ? CompanyIndex : UserIndex,
                Limit = limit,
                KeyConditionExpression = keyCondition,
                ExpressionAttributeValues = conditionValues.ToDictionary(
                    pair => pair.Key,
                    pair => new AttributeValue(pair.Value)),
            };
        }

        /// <summary>
        /// The store's resume position as an opaque token. The key attributes of this table are
        /// all strings; a number is carried through anyway rather than dropped, and anything
        /// else (there is nothing else on a key) breaks the read loudly, into the caller's
        /// empty page.
        /// </summary>
        private static string EncodePageToken(Dictionary<string, AttributeValue> key) =>
            DeveloperPropertyQuery.EncodePageToken(key.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.S is not null
                    ? ("S", pair.Value.S)
                    : pair.Value.N is not null
                        ? ("N", pair.Value.N)
                        : throw new InvalidOperationException(
                            $"A page position cannot be encoded: '{pair.Key}' is neither a string nor a number.")));

        private static Dictionary<string, AttributeValue>? DecodePageToken(string token)
        {
            var key = DeveloperPropertyQuery.DecodePageToken(token);

            return key?.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.Type == "N"
                    ? new AttributeValue { N = pair.Value.Value }
                    : new AttributeValue { S = pair.Value.Value });
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
                Status = DeveloperPropertyQuery.ParseStatus(Text(item, "status")),
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
