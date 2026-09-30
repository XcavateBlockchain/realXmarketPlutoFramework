using PlutoFramework.Model.Xcavate;
using UniqueryPlus.Metadata;

namespace PlutoFrameworkTests
{
    /// <summary>
    /// Yield figures are ratios and must come from one consistent source: the property
    /// document's own financials. The chain-overwritten PricePerToken / PropertyPrice live
    /// in the chain's 9-decimal price scale (XcavateMarketplaceIndexerModel.SharePriceDecimals),
    /// while the document's rental income keeps the document's scale - dividing one by the
    /// other inflates the ratio by the scale gap (the 1000x ROI / gross-yield regression).
    /// </summary>
    internal class XcavatePropertyModelTests
    {
        [Test]
        public void GetApy_UsesTheDocumentsOwnPropertyPrice()
        {
            // Devnet listing 0's actual figures: the chain price at 9 decimals reads
            // £3.40/share (£340 for 100 shares), the document says propertyPrice £340,000
            // with £2,000/month rent - the yield is 2000x12/340000.
            var financials = new PropertyFinancials
            {
                EstimatedRentalIncome = 2000m,
                PropertyPrice = 340m,
                DocumentPropertyPrice = 340_000m,
            };

            Assert.That(XcavatePropertyModel.GetAPY(financials), Is.EqualTo("7.06%"));
        }

        [Test]
        public void GetApy_FallsBackToTheLivePriceWithoutADocumentSnapshot()
        {
            // Records that never went through the document mapping (legacy Substrate
            // indexer, synthesized metadata) carry no snapshot; the live fields are
            // already in one consistent scale there.
            var financials = new PropertyFinancials
            {
                EstimatedRentalIncome = 2200m,
                PropertyPrice = 410_000m,
            };

            Assert.That(XcavatePropertyModel.GetAPY(financials), Is.EqualTo("6.44%"));
        }

        [Test]
        public void GetApy_ZeroPriceIsZeroPercent()
        {
            Assert.That(
                XcavatePropertyModel.GetAPY(new PropertyFinancials { EstimatedRentalIncome = 2000m }),
                Is.EqualTo("0.00%"));
            Assert.That(XcavatePropertyModel.GetAPY(null), Is.EqualTo("0.00%"));
        }

        [Test]
        public void YieldPricePerToken_PrefersTheDocumentsOwnFigure()
        {
            var financials = new PropertyFinancials
            {
                PricePerToken = 3.40m,
                DocumentPricePerToken = 3_400m,
            };

            Assert.That(XcavatePropertyModel.GetYieldPricePerToken(financials), Is.EqualTo(3_400m));
        }

        [Test]
        public void YieldPricePerToken_FallsBackToTheLivePrice()
        {
            var financials = new PropertyFinancials { PricePerToken = 3.40m };

            Assert.That(XcavatePropertyModel.GetYieldPricePerToken(financials), Is.EqualTo(3.40m));
        }

        [Test]
        public void YieldShareCount_PrefersTheDocumentsOwnFigure()
        {
            var financials = new PropertyFinancials
            {
                NumberOfTokens = 100_000,
                DocumentNumberOfShares = 100,
            };

            Assert.That(XcavatePropertyModel.GetYieldShareCount(financials), Is.EqualTo(100));
        }

        [Test]
        public void YieldShareCount_FallsBackToTheLiveCount()
        {
            var financials = new PropertyFinancials { NumberOfTokens = 100_000 };

            Assert.That(XcavatePropertyModel.GetYieldShareCount(financials), Is.EqualTo(100_000));
        }
    }
}
