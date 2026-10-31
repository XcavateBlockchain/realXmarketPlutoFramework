namespace PlutoFramework.Model.Xcavate
{
    public static class DeveloperPropertyDetailNavigation
    {
        public static bool TryGetOnChainListingId(
            DeveloperPropertyStatus status, string? value, out long listingId)
        {
            listingId = 0;

            return status == DeveloperPropertyStatus.Listed
                && long.TryParse(value, System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out listingId);
        }
    }
}
