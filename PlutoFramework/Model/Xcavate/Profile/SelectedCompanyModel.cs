namespace PlutoFramework.Model.Xcavate.Profile
{
    /// <summary>
    /// Which of the wallet's companies is the developer's selected one. The profile API has
    /// no "selected company" concept - a wallet simply owns any number of them - so the
    /// choice is stored locally, per address, in preferences.
    /// </summary>
    public static class SelectedCompanyModel
    {
        private const string KeyPrefix = "xcavate_selected_company_";

        public static string? GetSelectedCompanyId(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return null;
            }

            var companyId = Preferences.Get(KeyFor(address), string.Empty);

            return string.IsNullOrEmpty(companyId) ? null : companyId;
        }

        public static void SetSelectedCompanyId(string address, string? companyId)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return;
            }

            if (companyId is null)
            {
                Preferences.Remove(KeyFor(address));
            }
            else
            {
                Preferences.Set(KeyFor(address), companyId);
            }
        }

        private static string KeyFor(string address) => KeyPrefix + address;
    }
}
