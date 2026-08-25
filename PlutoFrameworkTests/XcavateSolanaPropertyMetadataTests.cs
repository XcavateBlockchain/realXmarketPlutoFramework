using System.Text.Json;
using PlutoFramework.Model.Xcavate;

namespace PlutoFrameworkTests
{
    /// <summary>
    /// The devnet indexer's <c>PropertyAsset.metadataUri</c> points at a JSON document in
    /// the marketplace webapp's format. These tests pin that format's deserialization and
    /// its mapping into the <c>PropertyMetadata</c> shape the property views bind to.
    /// </summary>
    internal class XcavateSolanaPropertyMetadataTests
    {
        // Verbatim from a real devnet upload (property "prp_ow2wHZvnyMsw").
        private const string SampleJson = """
        {
          "address": {
            "street": "The Willows",
            "townCity": "St Albans",
            "flatOrUnit": "Apartment 2B",
            "postCode": "AL1",
            "localAuthority": "East London District Council",
            "region": "Hertfordshire",
            "location": "St Albans, Hertfordshire"
          },
          "attributes": {
            "area": "72 m² (775 sq ft)",
            "quality": "High",
            "outdoorSpace": "Juliet balcony",
            "numberOfBedrooms": 2,
            "numberOfBathrooms": 1,
            "constructionDate": "2026-08-25",
            "offStreetParking": "Allocated parking space"
          },
          "buildingControlCode": "BC-01-8821",
          "companyId": "company_4vAESqKcSxBXlHjNFZ3JiQ",
          "companyLogo": "https://xcavate-profile.fsn1.your-objectstorage.com/companies/company_4vAESqKcSxBXlHjNFZ3JiQ/oak_spire.png",
          "companyName": "Oak & Spire Developments Ltd.",
          "companyWalletAddress": "3oVtApF8dsfZJSEbCi8TM6fq7xZgWF2J2WeWeeJe36Q5",
          "createdAt": "2026-08-25T09:08:44.630Z",
          "finances": {
            "propertyPrice": 395000,
            "numberOfShares": 100,
            "sharePrice": 3950,
            "estimatedRentalIncome": 2000,
            "annualServiceCharge": 0,
            "stampDutyTax": 0,
            "isStampDutyPaid": true,
            "isAnnualServiceChargePaid": true
          },
          "floorPlan": "https://realxmarketplace-dev-bucket.s3.eu-west-1.amazonaws.com/properties/floor-plans/1787649723514-What-is-a-floor-plan-with-dimensions.png",
          "map": "https://maps.google.com/?q=property+01",
          "otherDocuments": [
            "https://realxmarketplace-dev-bucket.s3.eu-west-1.amazonaws.com/properties/other-documents/1787649727282-Building_Control_Completion_Certificate.pdf"
          ],
          "planningCode": "PLN-01-2024",
          "propertyDescription": "A light-filled apartment in a boutique 12-unit block, moments from Verulamium Park and the historic city centre. Features open-plan living, underfloor heating.",
          "propertyId": "prp_ow2wHZvnyMsw",
          "propertyImages": [
            "https://realxmarketplace-dev-bucket.s3.eu-west-1.amazonaws.com/properties/property-images/1787649725328-prop5-3.jpg",
            "https://realxmarketplace-dev-bucket.s3.eu-west-1.amazonaws.com/properties/property-images/1787649725342-prop2-2.jpg"
          ],
          "propertyName": "The Willows – Apartment 2B",
          "propertyType": "Apartment",
          "salesAgreement": "https://realxmarketplace-dev-bucket.s3.eu-west-1.amazonaws.com/properties/sales-agreements/1787649724855-PropertySaleAgreement.pdf",
          "status": "verified",
          "tenure": "Leasehold",
          "updatedAt": "2026-08-25T09:27:05.623Z",
          "userId": "3oVtApF8dsfZJSEbCi8TM6fq7xZgWF2J2WeWeeJe36Q5"
        }
        """;

        [Test]
        public void ToPropertyMetadata_MapsTheWebappFormatIntoTheViewsContract()
        {
            var dto = JsonSerializer.Deserialize<XcavateSolanaPropertyMetadata>(SampleJson);

            Assert.That(dto, Is.Not.Null);

            var metadata = dto!.ToPropertyMetadata();

            Assert.Multiple(() =>
            {
                Assert.That(metadata.PropertyName, Is.EqualTo("The Willows – Apartment 2B"));
                Assert.That(metadata.PropertyType, Is.EqualTo("Apartment"));
                Assert.That(metadata.PropertyDescription, Does.StartWith("A light-filled apartment"));
                Assert.That(metadata.PropertyId, Is.EqualTo("prp_ow2wHZvnyMsw"));
                Assert.That(metadata.Status, Is.EqualTo("verified"));
                Assert.That(metadata.Map, Is.EqualTo("https://maps.google.com/?q=property+01"));
                Assert.That(metadata.PlanningCode, Is.EqualTo("PLN-01-2024"));

                // The views' image source is Files; the webapp format calls it propertyImages.
                Assert.That(metadata.Files, Has.Count.EqualTo(2));
                Assert.That(metadata.Files[0], Does.EndWith("prop5-3.jpg"));

                Assert.That(metadata.Address.Street, Is.EqualTo("The Willows"));
                Assert.That(metadata.Address.TownCity, Is.EqualTo("St Albans"));
                Assert.That(metadata.Address.PostCode, Is.EqualTo("AL1"));
                Assert.That(metadata.Address.FlatOrUnit, Is.EqualTo("Apartment 2B"));
                Assert.That(metadata.Address.LocalAuthority, Is.EqualTo("East London District Council"));

                // finances / numberOfShares / sharePrice in the webapp format.
                Assert.That(metadata.Financials.PropertyPrice, Is.EqualTo(395000m));
                Assert.That(metadata.Financials.NumberOfTokens, Is.EqualTo(100));
                Assert.That(metadata.Financials.PricePerToken, Is.EqualTo(3950m));
                Assert.That(metadata.Financials.EstimatedRentalIncome, Is.EqualTo(2000m));
                Assert.That(metadata.Financials.IsStampDutyPaid, Is.True);
                Assert.That(metadata.Financials.IsAnnualServiceChargePaid, Is.True);

                Assert.That(metadata.Company, Is.Not.Null);
                Assert.That(metadata.Company!.Name, Is.EqualTo("Oak & Spire Developments Ltd."));
                Assert.That(metadata.Company.Logo, Does.EndWith("oak_spire.png"));

                Assert.That(metadata.Attributes, Is.Not.Null);
                Assert.That(metadata.Attributes!.NumberOfBedrooms, Is.EqualTo(2));
                Assert.That(metadata.Attributes.NumberOfBathrooms, Is.EqualTo(1));
                Assert.That(metadata.Attributes.Area, Is.EqualTo("72 m² (775 sq ft)"));

                // The developer identity the webapp knows; chain data overrides it later.
                Assert.That(metadata.DeveloperAddress, Is.EqualTo("3oVtApF8dsfZJSEbCi8TM6fq7xZgWF2J2WeWeeJe36Q5"));
            });
        }

        [Test]
        public void ToPropertyMetadata_ToleratesAnEmptyDocument()
        {
            var dto = JsonSerializer.Deserialize<XcavateSolanaPropertyMetadata>("{}");

            Assert.That(dto, Is.Not.Null);

            // The views require non-null Financials / Address / Files to render at all.
            var metadata = dto!.ToPropertyMetadata();

            Assert.Multiple(() =>
            {
                Assert.That(metadata.Financials, Is.Not.Null);
                Assert.That(metadata.Address, Is.Not.Null);
                Assert.That(metadata.Files, Is.Not.Null);
                Assert.That(metadata.Company, Is.Null);
            });
        }
    }
}
