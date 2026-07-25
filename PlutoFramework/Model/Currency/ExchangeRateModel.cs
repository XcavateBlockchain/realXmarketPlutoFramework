namespace PlutoFramework.Model.Currency
{
    public static class ExchangeRateModel
    {
        public static string ToCurrencyString(
            this double usdValue,
            string? location = null,
            string? currencyFormat = null
        )
        {
            return ToCurrencyString((decimal)usdValue, location, currencyFormat);
        }

        public static string ToCurrencyString(
            this decimal gbpValue,
            string? location = null,
            string? currencyFormat = null
        )
        {
            currencyFormat ??= (string)Application.Current.Resources["CurrencyFormat"];

            location ??= AppConfigurationModel.Location;
            var currency = GetCurrencyInLocation(location);

            return $"{currency}{String.Format(currencyFormat, (decimal)ExchangeRateModel.GetExchangeRate("tGBP", currency) * gbpValue)}";
        }

        /// <summary>
        /// Formats a genuine USD value in the user's currency.
        /// </summary>
        /// <remarks>
        /// <see cref="ToCurrencyString(double, string?, string?)"/> converts from tGBP no
        /// matter what it is given, so it cannot be used here: a USD total passed through it
        /// comes out mislabelled rather than converted.
        /// </remarks>
        public static string ToUsdCurrencyString(
            this double usdValue,
            string? location = null,
            string? currencyFormat = null
        )
        {
            currencyFormat ??= (string)Application.Current.Resources["CurrencyFormat"];

            location ??= AppConfigurationModel.Location;
            var currency = GetCurrencyInLocation(location);

            return $"{currency}{String.Format(currencyFormat, (decimal)GetExchangeRate("USDT", currency) * (decimal)usdValue)}";
        }

        public static double GetExchangeRate(string fromCurrency, string toCurrency)
        {
            if (fromCurrency == "USDT" && toCurrency == "£")
            {
                return (double)Application.Current.Resources["UsdToGbp"];
            }
            if (fromCurrency == "USDT" && toCurrency == "$")
            {
                return 1;
            }
            if (fromCurrency == "tGBP" && toCurrency == "£")
            {
                return 1;
            }
            if (fromCurrency == "tGBP" && toCurrency == "$")
            {
                return 1 / (double)Application.Current.Resources["UsdToGbp"];
            }

            return 1;
        }

        public static string GetCurrencyInLocation(string location)
        {
            if (location == "UK")
            {
                return "£";
            }

            if (location == "US")
            {
                return "$";
            }

            return "$";
        }
    }
}
