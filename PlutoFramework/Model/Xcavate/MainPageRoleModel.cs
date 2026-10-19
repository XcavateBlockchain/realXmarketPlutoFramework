using PlutoFramework.Model.SQLite;
using PlutoFrameworkCore.Xcavate;

namespace PlutoFramework.Model.Xcavate
{
    public static class MainPageRoleModel
    {
        /// <summary>
        /// Raised after the role override changes so the app can re-route to the new main
        /// page immediately. Deliberately not raised by <see cref="ClearOverride"/> - logout
        /// drives its own navigation.
        /// </summary>
        public static event Action? OverrideChanged;

        public static async Task<MainPageRole> GetSelectedRoleAsync()
        {
            if (MainPageRoleOptions.TryParse(
                    Preferences.Get(PreferencesModel.SETTINGS_MAIN_PAGE_ROLE, string.Empty),
                    out var overrideRole))
            {
                return overrideRole;
            }

            try
            {
                var user = await XcavateUserDatabase.GetUserInformationAsync().ConfigureAwait(false);

                return MainPageRoleOptions.Resolve(overrideRole: null, ToMainPageRole(user?.Role));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Profile role could not be read for main-page routing:");
                Console.WriteLine(ex);

                return MainPageRoleOptions.Resolve(overrideRole: null, profileRole: null);
            }
        }

        public static async Task<string> GetRouteAsync()
        {
            var role = await GetSelectedRoleAsync().ConfigureAwait(false);

            return MainPageRoleOptions.ResolveRoute(role);
        }

        public static void SetOverride(MainPageRole role)
        {
            Preferences.Set(PreferencesModel.SETTINGS_MAIN_PAGE_ROLE, role.ToString());

            OverrideChanged?.Invoke();
        }

        public static void ClearOverride()
        {
            Preferences.Remove(PreferencesModel.SETTINGS_MAIN_PAGE_ROLE);
        }

        private static MainPageRole? ToMainPageRole(UserRoleEnum? role)
        {
            return role switch
            {
                UserRoleEnum.Developer => MainPageRole.Developer,
                UserRoleEnum.Investor => MainPageRole.Investor,
                UserRoleEnum.Lawyer => MainPageRole.Lawyer,
                UserRoleEnum.LettingAgent => MainPageRole.LettingAgent,
                _ => null,
            };
        }
    }
}
