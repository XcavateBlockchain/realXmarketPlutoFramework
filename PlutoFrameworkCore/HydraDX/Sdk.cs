namespace PlutoFramework.Model.HydraDX
{
    public class Sdk
    {
        public static Dictionary<(uint?, string), HydraDXTokenInfo> Assets = new Dictionary<(uint?, string), HydraDXTokenInfo>();

        public static Dictionary<(uint?, uint), HydraDXTokenInfo> AssetsById = new Dictionary<(uint?, uint), HydraDXTokenInfo>();

        public static double? GetSpotPrice(string tokenSymbol, uint? blocknumber = null)
        {
            if (tokenSymbol.Equals("USDC", StringComparison.CurrentCultureIgnoreCase))
            {
                return 1;
            }

            if (tokenSymbol.Equals("USDT", StringComparison.CurrentCultureIgnoreCase))
            {
                return 1;
            }

            if (tokenSymbol.Equals("USD", StringComparison.CurrentCultureIgnoreCase))
            {
                return 1;
            }

            if (tokenSymbol.Equals("tGBP", StringComparison.CurrentCultureIgnoreCase))
            {
                return 1;
            }

            if (tokenSymbol.Equals("XCAV", StringComparison.CurrentCultureIgnoreCase))
            {
                return 0.1;
            }

            if (!Assets.TryGetValue((blocknumber, tokenSymbol), out var token))
            {
                return null;
            }

            if (!Assets.TryGetValue((blocknumber, PlutoFramework.Constants.HydraDX.STABLE_TOKEN), out var usdToken))
            {
                return null;
            }

            double price_a = token.HubReserve / token.PoolBalance;
            double price_b = usdToken.PoolBalance / usdToken.HubReserve;

            double result = price_a * price_b;

            return result;
        }

        public static double GetSpotPrice(uint assetId, uint? blocknumber = null)
        {
            if (!AssetsById.TryGetValue((blocknumber, assetId), out var token))
            {
                return 2;
            }

            if (!Assets.TryGetValue((blocknumber, PlutoFramework.Constants.HydraDX.STABLE_TOKEN), out var usdToken))
            {
                return 5;
            }

            double price_a = token.HubReserve / token.PoolBalance;
            double price_b = usdToken.PoolBalance / usdToken.HubReserve;

            double result = price_a * price_b;

            return result;
        }
    }

    public class HydraDXTokenInfo
    {
        public double PoolBalance { get; set; }
        public double HubReserve { get; set; }
        public string Symbol { get; set; }
        public int Decimals { get; set; }
    }
}
