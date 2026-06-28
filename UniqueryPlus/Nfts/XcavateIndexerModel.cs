using StrawberryShake;
using Substrate.NetApi.Model.Types.Primitive;
using System.Numerics;
using System.Text.Json;
using UniqueryPlus.Metadata;
using XcavateIndexer;
using XcavatePaseo.NetApi.Generated;

namespace UniqueryPlus.Nfts
{
    public static class XcavateIndexerModel
    {
        public const string DefaultOwnerAddress = "14XAmaujtAthi7KdWsJrKh1QEjiNXwabW1YdYUCbAM6TeGk";

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

        public static async Task<IReadOnlyList<XcavatePaseoNftsPalletNft>> GetOwnedAndBoughtPropertiesAsync(
            SubstrateClientExt client,
            int first = 10,
            int offset = 0,
            string tokenOwner = DefaultOwnerAddress)
        {
            var indexerClient = Indexers.GetXcavateIndexerClient();

            var result = await indexerClient.OwnedAndBoughtProperties
                .ExecuteAsync(first, offset, tokenOwner)
                .ConfigureAwait(false);

            result.EnsureNoErrors();

            var properties = result.Data?.RealEstateNfts?.Nodes;
            if (properties is null)
            {
                return [];
            }

            return properties
                .Select(property => MapOwnedAndBoughtProperty(client, property, tokenOwner))
                .Where(nft => nft is not null)
                .Cast<XcavatePaseoNftsPalletNft>()
                .ToList();
        }

        public static async Task<IReadOnlyList<XcavatePaseoNftsPalletNft>> GetOwnedAndBoughtPropertiesWithFilterAsync(
            SubstrateClientExt client,
            int first = 10,
            int offset = 0,
            string tokenOwner = DefaultOwnerAddress,
            string includesTownCity = "",
            string includesPropertyType = "",
            string includesPropertyName = "")
        {
            var indexerClient = Indexers.GetXcavateIndexerClient();

            var result = await indexerClient.OwnedAndBoughtPropertiesWithFilter
                .ExecuteAsync(first, offset, tokenOwner, includesTownCity, includesPropertyType, includesPropertyName)
                .ConfigureAwait(false);

            result.EnsureNoErrors();

            var properties = result.Data?.RealEstateNfts?.Nodes;
            if (properties is null)
            {
                return [];
            }

            return properties
                .Select(property => MapOwnedAndBoughtProperty(client, property, tokenOwner))
                .Where(nft => nft is not null)
                .Cast<XcavatePaseoNftsPalletNft>()
                .ToList();
        }

        public static async Task<IReadOnlyList<XcavatePaseoNftsPalletNft>> GetOwnedPropertiesAsync(
            SubstrateClientExt client,
            int first = 10,
            int offset = 0,
            string tokenOwner = DefaultOwnerAddress)
        {
            var indexerClient = Indexers.GetXcavateIndexerClient();

            var result = await indexerClient.OwnedProperties
                .ExecuteAsync(first, offset, tokenOwner)
                .ConfigureAwait(false);

            result.EnsureNoErrors();

            var properties = result.Data?.RealEstateNfts?.Nodes;
            if (properties is null)
            {
                return [];
            }

            return properties
                .Select(property => MapOwnedProperty(client, property, tokenOwner))
                .Where(nft => nft is not null)
                .Cast<XcavatePaseoNftsPalletNft>()
                .ToList();
        }

        public static async Task<IReadOnlyList<XcavatePaseoNftsPalletNft>> GetBoughtPropertiesAsync(
            SubstrateClientExt client,
            int first = 10,
            int offset = 0,
            string tokenOwner = DefaultOwnerAddress)
        {
            var indexerClient = Indexers.GetXcavateIndexerClient();

            var result = await indexerClient.BoughtProperties
                .ExecuteAsync(first, offset, tokenOwner)
                .ConfigureAwait(false);

            result.EnsureNoErrors();

            var properties = result.Data?.RealEstateNfts?.Nodes;
            if (properties is null)
            {
                return [];
            }

            return properties
                .Select(property => MapBoughtProperty(client, property, tokenOwner))
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
                    PricePerToken = ToDecimal(realEstateNft?.PricePerShare),
                    NumberOfTokens = realEstateNft?.NumberOfShares ?? 0,
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
                    ListedTokens = ToUInt32(listing.ListedShareAmount),
                    UnclaimedTokens = ToUInt32(listing.UnclaimedShareAmount),
                    AssetId = new U32(ToUInt32(listing.AssetId)),
                    CollectionId = new U32(ToUInt32(listing.CollectionId ?? realEstateNft?.Collection)),
                    ItemId = new U32(ToUInt32(listing.ItemId ?? realEstateNft?.Item)),
                }
            };
        }

        private static XcavatePaseoNftsPalletNft? MapOwnedAndBoughtProperty(
            SubstrateClientExt client,
            IOwnedAndBoughtProperties_RealEstateNfts_Nodes? property,
            string tokenOwner)
        {
            if (property is null)
            {
                return null;
            }

            var listing = property.MarketplaceOngoingObjectListings?.Nodes?.FirstOrDefault();
            var realWorldAsset = property.RealWorldAssets?.Nodes?.FirstOrDefault();

            var files = ParseFiles(property.Files);
            var firstImage = files.FirstOrDefault();

            var metadata = new MetadataBase
            {
                Name = property.PropertyName ?? "Unknown",
                Description = property.PropertyDescription ?? string.Empty,
                Image = firstImage
            };

            var propertyMetadata = new PropertyMetadata
            {
                Status = property.Status,
                PropertyName = property.PropertyName,
                Financials = new PropertyFinancials
                {
                    StampDutyTax = ToDecimal(property.StampDutyTax),
                    IsAnnualServiceChargePaid = property.IsAnnualServiceChargePaid ?? false,
                    EstimatedRentalIncome = ToDecimal(property.EstimatedRentalIncome),
                    PricePerToken = ToDecimal(property.PricePerShare),
                    NumberOfTokens = property.NumberOfShares ?? 0,
                    IsStampDutyPaid = property.IsStampDutyPaid ?? false,
                    PropertyPrice = ToDecimal(property.PropertyPrice),
                    AnnualServiceCharge = ToDecimal(property.AnnualServiceCharge),
                },
                Files = files,
                CreatedAt = ParseDateTimeOffset(property.CreatedAt),
                Address = new PropertyAddress
                {
                    PostCode = property.PostCode,
                    FlatOrUnit = property.FlatOrUnit,
                    LocalAuthority = property.LocalAuthority,
                    Street = property.Street,
                    TownCity = property.TownCity,
                },
                Company = new PropertyCompany
                {
                    Name = property.CompanyName,
                    Logo = property.CompanyLogo,
                },
                PropertyDescription = property.PropertyDescription,
                PropertyType = property.PropertyType,
                Map = property.Map,
                DeveloperAddress = property.DeveloperAddress,
                PlanningCode = property.PlanningCode,
                PropertyId = property.PropertyId,
                Id = ParseGuid(property.Id),
                AccountAddress = property.AccountAddress,
                UpdatedAt = ParseDateTimeOffset(property.UpdatedAt),
                LegalRepresentative = property.LegalRepresentative,
                Attributes = new PropertyAttributes
                {
                    Area = property.Area,
                    OffStreetParking = property.OffStreetParking,
                    OutdoorSpace = property.OutdoorSpace,
                    NumberOfBedrooms = property.NumberOfBedrooms,
                    ConstructionDate = property.ConstructionDate,
                    NumberOfBathrooms = property.NumberOfBathrooms,
                    Quality = property.Quality,
                }
            };

            return new XcavatePaseoNftsPalletNft(client)
            {
                CollectionId = new BigInteger(property.Collection ?? listing?.CollectionId ?? realWorldAsset?.CollectionId ?? 0),
                Id = new BigInteger(property.Item ?? listing?.ItemId ?? realWorldAsset?.ItemId ?? 0),
                Owner = property.AccountAddress ?? tokenOwner,
                Metadata = metadata,
                XcavateMetadata = propertyMetadata,
                NftMarketplaceDetails = realWorldAsset is null
                    ? null
                    : new NftMarketplaceDetails
                    {
                        SpvCreated = realWorldAsset.SpvCreated ?? false,
                        AssetId = ToUInt32(realWorldAsset.AssetId),
                        Region = ToUInt32(realWorldAsset.Region),
                        Location = realWorldAsset.Location ?? string.Empty,
                        Tokens = ToUInt32(realWorldAsset.ShareAmount),
                    },
                OngoingObjectListingDetails = listing is null
                    ? null
                    : new XcavateOngoingObjectListingDetails
                    {
                        RealEstateDeveloper = listing.RealEstateDeveloper ?? string.Empty,
                        TaxPaidByDeveloper = listing.TaxPaidByDeveloper ?? false,
                        ListingExpiry = ToUInt32(listing.ListingExpiry),
                        ClaimExpiry = listing.ClaimExpiry is null ? null : ToUInt32Nullable(listing.ClaimExpiry),
                        ListedTokens = ToUInt32(listing.ListedShareAmount),
                        UnclaimedTokens = ToUInt32(listing.UnclaimedShareAmount),
                        AssetId = new U32(ToUInt32(listing.AssetId)),
                        CollectionId = new U32(ToUInt32(listing.CollectionId ?? property.Collection ?? realWorldAsset?.CollectionId)),
                        ItemId = new U32(ToUInt32(listing.ItemId ?? property.Item ?? realWorldAsset?.ItemId)),
                    },
                RealWorldAssetDetails = realWorldAsset is null
                    ? null
                    : new XcavateRealWorldAssetDetails
                    {
                        Tokens = ToUInt32(realWorldAsset.ShareAmount),
                        Price = ParseBigInteger(realWorldAsset.Price),
                        SpvCreated = realWorldAsset.SpvCreated ?? false,
                        Finalized = realWorldAsset.Finalized ?? false,
                    },
            };
        }

        private static XcavatePaseoNftsPalletNft? MapOwnedAndBoughtProperty(
            SubstrateClientExt client,
            IOwnedAndBoughtPropertiesWithFilter_RealEstateNfts_Nodes? property,
            string tokenOwner)
        {
            if (property is null)
            {
                return null;
            }

            var listing = property.MarketplaceOngoingObjectListings?.Nodes?.FirstOrDefault();
            var realWorldAsset = property.RealWorldAssets?.Nodes?.FirstOrDefault();

            var files = ParseFiles(property.Files);
            var firstImage = files.FirstOrDefault();

            var metadata = new MetadataBase
            {
                Name = property.PropertyName ?? "Unknown",
                Description = property.PropertyDescription ?? string.Empty,
                Image = firstImage
            };

            var propertyMetadata = new PropertyMetadata
            {
                Status = property.Status,
                PropertyName = property.PropertyName,
                Financials = new PropertyFinancials
                {
                    StampDutyTax = ToDecimal(property.StampDutyTax),
                    IsAnnualServiceChargePaid = property.IsAnnualServiceChargePaid ?? false,
                    EstimatedRentalIncome = ToDecimal(property.EstimatedRentalIncome),
                    PricePerToken = ToDecimal(property.PricePerShare),
                    NumberOfTokens = property.NumberOfShares ?? 0,
                    IsStampDutyPaid = property.IsStampDutyPaid ?? false,
                    PropertyPrice = ToDecimal(property.PropertyPrice),
                    AnnualServiceCharge = ToDecimal(property.AnnualServiceCharge),
                },
                Files = files,
                CreatedAt = ParseDateTimeOffset(property.CreatedAt),
                Address = new PropertyAddress
                {
                    PostCode = property.PostCode,
                    FlatOrUnit = property.FlatOrUnit,
                    LocalAuthority = property.LocalAuthority,
                    Street = property.Street,
                    TownCity = property.TownCity,
                },
                Company = new PropertyCompany
                {
                    Name = property.CompanyName,
                    Logo = property.CompanyLogo,
                },
                PropertyDescription = property.PropertyDescription,
                PropertyType = property.PropertyType,
                Map = property.Map,
                DeveloperAddress = property.DeveloperAddress,
                PlanningCode = property.PlanningCode,
                PropertyId = property.PropertyId,
                Id = ParseGuid(property.Id),
                AccountAddress = property.AccountAddress,
                UpdatedAt = ParseDateTimeOffset(property.UpdatedAt),
                LegalRepresentative = property.LegalRepresentative,
                Attributes = new PropertyAttributes
                {
                    Area = property.Area,
                    OffStreetParking = property.OffStreetParking,
                    OutdoorSpace = property.OutdoorSpace,
                    NumberOfBedrooms = property.NumberOfBedrooms,
                    ConstructionDate = property.ConstructionDate,
                    NumberOfBathrooms = property.NumberOfBathrooms,
                    Quality = property.Quality,
                }
            };

            return new XcavatePaseoNftsPalletNft(client)
            {
                CollectionId = new BigInteger(property.Collection ?? listing?.CollectionId ?? realWorldAsset?.CollectionId ?? 0),
                Id = new BigInteger(property.Item ?? listing?.ItemId ?? realWorldAsset?.ItemId ?? 0),
                Owner = property.AccountAddress ?? tokenOwner,
                Metadata = metadata,
                XcavateMetadata = propertyMetadata,
                NftMarketplaceDetails = realWorldAsset is null
                    ? null
                    : new NftMarketplaceDetails
                    {
                        SpvCreated = realWorldAsset.SpvCreated ?? false,
                        AssetId = ToUInt32(realWorldAsset.AssetId),
                        Region = ToUInt32(realWorldAsset.Region),
                        Location = realWorldAsset.Location ?? string.Empty,
                        Tokens = ToUInt32(realWorldAsset.ShareAmount),
                    },
                OngoingObjectListingDetails = listing is null
                    ? null
                    : new XcavateOngoingObjectListingDetails
                    {
                        RealEstateDeveloper = listing.RealEstateDeveloper ?? string.Empty,
                        TaxPaidByDeveloper = listing.TaxPaidByDeveloper ?? false,
                        ListingExpiry = ToUInt32(listing.ListingExpiry),
                        ClaimExpiry = listing.ClaimExpiry is null ? null : ToUInt32Nullable(listing.ClaimExpiry),
                        ListedTokens = ToUInt32(listing.ListedShareAmount),
                        UnclaimedTokens = ToUInt32(listing.UnclaimedShareAmount),
                        AssetId = new U32(ToUInt32(listing.AssetId)),
                        CollectionId = new U32(ToUInt32(listing.CollectionId ?? property.Collection ?? realWorldAsset?.CollectionId)),
                        ItemId = new U32(ToUInt32(listing.ItemId ?? property.Item ?? realWorldAsset?.ItemId)),
                    },
                RealWorldAssetDetails = realWorldAsset is null
                    ? null
                    : new XcavateRealWorldAssetDetails
                    {
                        Tokens = ToUInt32(realWorldAsset.ShareAmount),
                        Price = ParseBigInteger(realWorldAsset.Price),
                        SpvCreated = realWorldAsset.SpvCreated ?? false,
                        Finalized = realWorldAsset.Finalized ?? false,
                    },
            };
        }

        private static XcavatePaseoNftsPalletNft? MapOwnedProperty(
            SubstrateClientExt client,
            IOwnedProperties_RealEstateNfts_Nodes? property,
            string tokenOwner)
        {
            if (property is null)
            {
                return null;
            }

            var listing = property.MarketplaceOngoingObjectListings?.Nodes?.FirstOrDefault();
            var realWorldAsset = property.RealWorldAssets?.Nodes?.FirstOrDefault();

            var files = ParseFiles(property.Files);
            var firstImage = files.FirstOrDefault();

            var metadata = new MetadataBase
            {
                Name = property.PropertyName ?? "Unknown",
                Description = property.PropertyDescription ?? string.Empty,
                Image = firstImage
            };

            var propertyMetadata = new PropertyMetadata
            {
                Status = property.Status,
                PropertyName = property.PropertyName,
                Financials = new PropertyFinancials
                {
                    StampDutyTax = ToDecimal(property.StampDutyTax),
                    IsAnnualServiceChargePaid = property.IsAnnualServiceChargePaid ?? false,
                    EstimatedRentalIncome = ToDecimal(property.EstimatedRentalIncome),
                    PricePerToken = ToDecimal(property.PricePerShare),
                    NumberOfTokens = property.NumberOfShares ?? 0,
                    IsStampDutyPaid = property.IsStampDutyPaid ?? false,
                    PropertyPrice = ToDecimal(property.PropertyPrice),
                    AnnualServiceCharge = ToDecimal(property.AnnualServiceCharge),
                },
                Files = files,
                CreatedAt = ParseDateTimeOffset(property.CreatedAt),
                Address = new PropertyAddress
                {
                    PostCode = property.PostCode,
                    FlatOrUnit = property.FlatOrUnit,
                    LocalAuthority = property.LocalAuthority,
                    Street = property.Street,
                    TownCity = property.TownCity,
                },
                Company = new PropertyCompany
                {
                    Name = property.CompanyName,
                    Logo = property.CompanyLogo,
                },
                PropertyDescription = property.PropertyDescription,
                PropertyType = property.PropertyType,
                Map = property.Map,
                DeveloperAddress = property.DeveloperAddress,
                PlanningCode = property.PlanningCode,
                PropertyId = property.PropertyId,
                Id = ParseGuid(property.Id),
                AccountAddress = property.AccountAddress,
                UpdatedAt = ParseDateTimeOffset(property.UpdatedAt),
                LegalRepresentative = property.LegalRepresentative,
                Attributes = new PropertyAttributes
                {
                    Area = property.Area,
                    OffStreetParking = property.OffStreetParking,
                    OutdoorSpace = property.OutdoorSpace,
                    NumberOfBedrooms = property.NumberOfBedrooms,
                    ConstructionDate = property.ConstructionDate,
                    NumberOfBathrooms = property.NumberOfBathrooms,
                    Quality = property.Quality,
                }
            };

            return new XcavatePaseoNftsPalletNft(client)
            {
                CollectionId = new BigInteger(property.Collection ?? listing?.CollectionId ?? realWorldAsset?.CollectionId ?? 0),
                Id = new BigInteger(property.Item ?? listing?.ItemId ?? realWorldAsset?.ItemId ?? 0),
                Owner = property.AccountAddress ?? tokenOwner,
                Metadata = metadata,
                XcavateMetadata = propertyMetadata,
                NftMarketplaceDetails = realWorldAsset is null
                    ? null
                    : new NftMarketplaceDetails
                    {
                        SpvCreated = realWorldAsset.SpvCreated ?? false,
                        AssetId = ToUInt32(realWorldAsset.AssetId),
                        Region = ToUInt32(realWorldAsset.Region),
                        Location = realWorldAsset.Location ?? string.Empty,
                        Tokens = ToUInt32(realWorldAsset.ShareAmount),
                    },
                OngoingObjectListingDetails = listing is null
                    ? null
                    : new XcavateOngoingObjectListingDetails
                    {
                        RealEstateDeveloper = listing.RealEstateDeveloper ?? string.Empty,
                        TaxPaidByDeveloper = listing.TaxPaidByDeveloper ?? false,
                        ListingExpiry = ToUInt32(listing.ListingExpiry),
                        ClaimExpiry = listing.ClaimExpiry is null ? null : ToUInt32Nullable(listing.ClaimExpiry),
                        ListedTokens = ToUInt32(listing.ListedShareAmount),
                        UnclaimedTokens = ToUInt32(listing.UnclaimedShareAmount),
                        AssetId = new U32(ToUInt32(listing.AssetId)),
                        CollectionId = new U32(ToUInt32(listing.CollectionId ?? property.Collection ?? realWorldAsset?.CollectionId)),
                        ItemId = new U32(ToUInt32(listing.ItemId ?? property.Item ?? realWorldAsset?.ItemId)),
                    },
                RealWorldAssetDetails = realWorldAsset is null
                    ? null
                    : new XcavateRealWorldAssetDetails
                    {
                        Tokens = ToUInt32(realWorldAsset.ShareAmount),
                        Price = ParseBigInteger(realWorldAsset.Price),
                        SpvCreated = realWorldAsset.SpvCreated ?? false,
                        Finalized = realWorldAsset.Finalized ?? false,
                    },
            };
        }

        private static XcavatePaseoNftsPalletNft? MapBoughtProperty(
            SubstrateClientExt client,
            IBoughtProperties_RealEstateNfts_Nodes? property,
            string tokenOwner)
        {
            if (property is null)
            {
                return null;
            }

            var listing = property.MarketplaceOngoingObjectListings?.Nodes?.FirstOrDefault();
            var realWorldAsset = property.RealWorldAssets?.Nodes?.FirstOrDefault();

            var files = ParseFiles(property.Files);
            var firstImage = files.FirstOrDefault();

            var metadata = new MetadataBase
            {
                Name = property.PropertyName ?? "Unknown",
                Description = property.PropertyDescription ?? string.Empty,
                Image = firstImage
            };

            var propertyMetadata = new PropertyMetadata
            {
                Status = property.Status,
                PropertyName = property.PropertyName,
                Financials = new PropertyFinancials
                {
                    StampDutyTax = ToDecimal(property.StampDutyTax),
                    IsAnnualServiceChargePaid = property.IsAnnualServiceChargePaid ?? false,
                    EstimatedRentalIncome = ToDecimal(property.EstimatedRentalIncome),
                    PricePerToken = ToDecimal(property.PricePerShare),
                    NumberOfTokens = property.NumberOfShares ?? 0,
                    IsStampDutyPaid = property.IsStampDutyPaid ?? false,
                    PropertyPrice = ToDecimal(property.PropertyPrice),
                    AnnualServiceCharge = ToDecimal(property.AnnualServiceCharge),
                },
                Files = files,
                CreatedAt = ParseDateTimeOffset(property.CreatedAt),
                Address = new PropertyAddress
                {
                    PostCode = property.PostCode,
                    FlatOrUnit = property.FlatOrUnit,
                    LocalAuthority = property.LocalAuthority,
                    Street = property.Street,
                    TownCity = property.TownCity,
                },
                Company = new PropertyCompany
                {
                    Name = property.CompanyName,
                    Logo = property.CompanyLogo,
                },
                PropertyDescription = property.PropertyDescription,
                PropertyType = property.PropertyType,
                Map = property.Map,
                DeveloperAddress = property.DeveloperAddress,
                PlanningCode = property.PlanningCode,
                PropertyId = property.PropertyId,
                Id = ParseGuid(property.Id),
                AccountAddress = property.AccountAddress,
                UpdatedAt = ParseDateTimeOffset(property.UpdatedAt),
                LegalRepresentative = property.LegalRepresentative,
                Attributes = new PropertyAttributes
                {
                    Area = property.Area,
                    OffStreetParking = property.OffStreetParking,
                    OutdoorSpace = property.OutdoorSpace,
                    NumberOfBedrooms = property.NumberOfBedrooms,
                    ConstructionDate = property.ConstructionDate,
                    NumberOfBathrooms = property.NumberOfBathrooms,
                    Quality = property.Quality,
                }
            };

            return new XcavatePaseoNftsPalletNft(client)
            {
                CollectionId = new BigInteger(property.Collection ?? listing?.CollectionId ?? realWorldAsset?.CollectionId ?? 0),
                Id = new BigInteger(property.Item ?? listing?.ItemId ?? realWorldAsset?.ItemId ?? 0),
                Owner = property.AccountAddress ?? tokenOwner,
                Metadata = metadata,
                XcavateMetadata = propertyMetadata,
                NftMarketplaceDetails = realWorldAsset is null
                    ? null
                    : new NftMarketplaceDetails
                    {
                        SpvCreated = realWorldAsset.SpvCreated ?? false,
                        AssetId = ToUInt32(realWorldAsset.AssetId),
                        Region = ToUInt32(realWorldAsset.Region),
                        Location = realWorldAsset.Location ?? string.Empty,
                        Tokens = ToUInt32(realWorldAsset.ShareAmount),
                    },
                OngoingObjectListingDetails = listing is null
                    ? null
                    : new XcavateOngoingObjectListingDetails
                    {
                        RealEstateDeveloper = listing.RealEstateDeveloper ?? string.Empty,
                        TaxPaidByDeveloper = listing.TaxPaidByDeveloper ?? false,
                        ListingExpiry = ToUInt32(listing.ListingExpiry),
                        ClaimExpiry = listing.ClaimExpiry is null ? null : ToUInt32Nullable(listing.ClaimExpiry),
                        ListedTokens = ToUInt32(listing.ListedShareAmount),
                        UnclaimedTokens = ToUInt32(listing.UnclaimedShareAmount),
                        AssetId = new U32(ToUInt32(listing.AssetId)),
                        CollectionId = new U32(ToUInt32(listing.CollectionId ?? property.Collection ?? realWorldAsset?.CollectionId)),
                        ItemId = new U32(ToUInt32(listing.ItemId ?? property.Item ?? realWorldAsset?.ItemId)),
                    },
                RealWorldAssetDetails = realWorldAsset is null
                    ? null
                    : new XcavateRealWorldAssetDetails
                    {
                        Tokens = ToUInt32(realWorldAsset.ShareAmount),
                        Price = ParseBigInteger(realWorldAsset.Price),
                        SpvCreated = realWorldAsset.SpvCreated ?? false,
                        Finalized = realWorldAsset.Finalized ?? false,
                    },
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

        private static BigInteger ParseBigInteger(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return BigInteger.Zero;
            }

            return BigInteger.TryParse(value, out var parsed) ? parsed : BigInteger.Zero;
        }
    }
}
