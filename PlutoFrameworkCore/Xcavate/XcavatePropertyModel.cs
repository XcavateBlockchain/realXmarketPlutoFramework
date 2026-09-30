using UniqueryPlus.Metadata;

namespace PlutoFramework.Model.Xcavate
{
    public class XcavatePropertyModel
    {

        public static double GetAreaPricesPercentage(decimal price)
        {
            // TODO
            return 0.7;
        }

        public static double GetRentalDemand()
        {
            // TODO
            return 0.8;
        }

        /// <summary>
        /// Gross yield from the property document's own figures. A yield is a ratio and
        /// must not mix sources: the document's rental income lives in the document's price
        /// scale, while the chain-overwritten <see cref="PropertyFinancials.PropertyPrice"/>
        /// lives in the chain's price decimals - dividing one by the other inflates the
        /// result by the scale gap.
        /// </summary>
        public static string GetAPY(PropertyFinancials? financials) =>
            financials is null
                ? "0.00%"
                : GetAPY(financials.EstimatedRentalIncome, financials.DocumentPropertyPrice ?? financials.PropertyPrice);

        /// <summary>The price per token for yield math: the document's own figure when known.</summary>
        public static decimal GetYieldPricePerToken(PropertyFinancials financials) =>
            financials.DocumentPricePerToken ?? financials.PricePerToken;

        /// <summary>The share count for yield math: the document's own figure when known.</summary>
        public static int GetYieldShareCount(PropertyFinancials financials) =>
            financials.DocumentNumberOfShares ?? financials.NumberOfTokens;

        public static string GetAPY(decimal rentalIncome, decimal price)
        {
            if (price == 0)
            {
                return "0.00%";
            }

            var ari = rentalIncome * 12;
            var apy = ari / price;
            return $"{String.Format("{0:0.00}", apy * 100)}%";
        }
    }
}
