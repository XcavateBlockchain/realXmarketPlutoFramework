using System.Numerics;
using System.Text.Json;
using Substrate.NetApi.Model.Types.Primitive;
using StrawberryShake;
using UniqueryPlus;
using UniqueryPlus.Metadata;
using XcavateIndexer;
using XcavatePaseo.NetApi.Generated;

namespace UniqueryPlus.Nfts
{
    public static class XcavateIndexerModel
    {
        public static async Task<IReadOnlyList<XcavatePaseoNftsPalletNft>> GetMarketplaceListedPropertiesAsync(
            SubstrateClientExt client,
            int first = 10,
            int offset = 0,
            string includesTownCity = "",
            string includesPropertyType = "",
            string includesPropertyName = "")
        {
            var indexerClient = Indexers.GetXcavateIndexerClient();

            var result = await indexerClient.MarketplaceListedProperties
                .ExecuteAsync(first, offset, includesTownCity, includesPropertyType, includesPropertyName)
                .ConfigureAwait(false);

            result.EnsureNoErrors();

            var listings = result.Data?.MarketplaceOngoingObjectListings?.Nodes;
            if (listings is null)
            {
                return [];
            }

            return listings
                .Select(listing => MapListing(client, listing))
                .Where(nft => nft is not null)
                .Cast<XcavatePaseoNftsPalletNft>()
                .ToList();
        }

        private static XcavatePaseoNftsPalletNft? MapListing(SubstrateClientExt client, IMarketplaceListedProperties_MarketplaceOngoingObjectListings_Nodes? listing)
        {
            if (listing is null)
            {
                return null;
            }

            var realEstateNft = listing.RealEstateNft;
            var files = ParseFiles(realEstateNft?.Files);
            var firstImage = files.FirstOrDefault();

            var metadata = new MetadataBase
            {
                Name = realEstateNft?.PropertyName ?? "Unknown",
                Description = realEstateNft?.PropertyDescription ?? string.Empty,
                Image = firstImage
            };

            var propertyMetadata = new PropertyMetadata
            {
                Status = realEstateNft?.Status,
                PropertyName = realEstateNft?.PropertyName,
                Financials = new PropertyFinancials
                {
                    StampDutyTax = ToDecimal(realEstateNft?.StampDutyTax),
                    IsAnnualServiceChargePaid = realEstateNft?.IsAnnualServiceChargePaid ?? false,
                    EstimatedRentalIncome = ToDecimal(realEstateNft?.EstimatedRentalIncome),
                    PricePerToken = ToDecimal(realEstateNft?.PricePerToken),
                    NumberOfTokens = realEstateNft?.NumberOfTokens ?? 0,
                    IsStampDutyPaid = realEstateNft?.IsStampDutyPaid ?? false,
                    PropertyPrice = ToDecimal(realEstateNft?.PropertyPrice),
                    AnnualServiceCharge = ToDecimal(realEstateNft?.AnnualServiceCharge),
                },
                Files = files,
                CreatedAt = ParseDateTimeOffset(realEstateNft?.CreatedAt),
                Address = new PropertyAddress
                {
                    PostCode = realEstateNft?.PostCode,
                    FlatOrUnit = realEstateNft?.FlatOrUnit,
                    LocalAuthority = realEstateNft?.LocalAuthority,
                    Street = realEstateNft?.Street,
                    TownCity = realEstateNft?.TownCity,
                },
                Company = new PropertyCompany
                {
                    Name = realEstateNft?.CompanyName,
                    Logo = realEstateNft?.CompanyLogo,
                },
                PropertyDescription = realEstateNft?.PropertyDescription,
                PropertyType = realEstateNft?.PropertyType,
                Map = realEstateNft?.Map,
                DeveloperAddress = realEstateNft?.DeveloperAddress,
                PlanningCode = realEstateNft?.PlanningCode,
                PropertyId = realEstateNft?.PropertyId,
                Id = ParseGuid(realEstateNft?.Id),
                AccountAddress = realEstateNft?.AccountAddress,
                UpdatedAt = ParseDateTimeOffset(realEstateNft?.UpdatedAt),
                LegalRepresentative = realEstateNft?.LegalRepresentative,
                Attributes = new PropertyAttributes
                {
                    Area = realEstateNft?.Area,
                    OffStreetParking = realEstateNft?.OffStreetParking,
                    OutdoorSpace = realEstateNft?.OutdoorSpace,
                    NumberOfBedrooms = realEstateNft?.NumberOfBedrooms,
                    ConstructionDate = realEstateNft?.ConstructionDate,
                    NumberOfBathrooms = realEstateNft?.NumberOfBathrooms,
                    Quality = realEstateNft?.Quality,
                }
            };

            return new XcavatePaseoNftsPalletNft(client)
            {
                CollectionId = new BigInteger(realEstateNft?.Collection ?? listing.CollectionId ?? 0),
                Id = new BigInteger(realEstateNft?.Item ?? listing.ItemId ?? 0),
                Owner = realEstateNft?.AccountAddress ?? listing.RealEstateDeveloper ?? "Unknown",
                Metadata = metadata,
                XcavateMetadata = propertyMetadata,
                OngoingObjectListingDetails = new XcavateOngoingObjectListingDetails
                {
                    RealEstateDeveloper = listing.RealEstateDeveloper ?? string.Empty,
                    TaxPaidByDeveloper = listing.TaxPaidByDeveloper ?? false,
                    ListingExpiry = ToUInt32(listing.ListingExpiry),
                    ClaimExpiry = listing.ClaimExpiry is null ? null : ToUInt32Nullable(listing.ClaimExpiry),
                    ListedTokens = ToUInt32(listing.ListedTokenAmount),
                    UnclaimedTokens = ToUInt32(listing.UnclaimedTokenAmount),
                    AssetId = new U32(ToUInt32(listing.AssetId)),
                    CollectionId = new U32(ToUInt32(listing.CollectionId ?? realEstateNft?.Collection)),
                    ItemId = new U32(ToUInt32(listing.ItemId ?? realEstateNft?.Item)),
                }
            };
        }

        private static List<string> ParseFiles(object? filesValue)
        {
            if (filesValue is null)
            {
                return [];
            }

            if (filesValue is IEnumerable<string> stringList)
            {
                return stringList.Where(value => !string.IsNullOrWhiteSpace(value)).ToList();
            }

            if (filesValue is JsonElement element)
            {
                return ParseJsonElementFiles(element);
            }

            if (filesValue is string raw)
            {
                return ParseJsonStringFiles(raw);
            }

            return [];
        }

        private static List<string> ParseJsonElementFiles(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Array:
                    return element
                        .EnumerateArray()
                        .Select(item => item.GetString())
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .ToList()!;
                case JsonValueKind.String:
                    return ParseJsonStringFiles(element.GetString() ?? string.Empty);
                default:
                    return [];
            }
        }

        private static List<string> ParseJsonStringFiles(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return [];
            }

            try
            {
                using var doc = JsonDocument.Parse(raw);
                return ParseJsonElementFiles(doc.RootElement);
            }
            catch (JsonException)
            {
                return [raw];
            }
        }

        private static DateTimeOffset ParseDateTimeOffset(string? value)
        {
            if (DateTimeOffset.TryParse(value, out var parsed))
            {
                return parsed;
            }

            return DateTimeOffset.MinValue;
        }

        private static Guid ParseGuid(string? value)
        {
            if (Guid.TryParse(value, out var parsed))
            {
                return parsed;
            }

            return Guid.Empty;
        }

        private static decimal ToDecimal(int? value) => value is null ? 0m : value.Value;

        private static uint ToUInt32(int? value) => value is null || value < 0 ? 0u : (uint)value.Value;

        private static uint? ToUInt32Nullable(int? value)
        {
            if (value is null || value < 0)
            {
                return null;
            }

            return (uint)value.Value;
        }
    }
}
