namespace PlutoFrameworkCore.Xcavate
{
    public enum MainPageRole
    {
        Developer,
        Investor,
        Lawyer,
        LettingAgent
    }

    public static class MainPageRoleOptions
    {
        private const string InvestorRoute = "//Account";
        private const string LawyerRoute = "//LawyerAccount";
        private const string DeveloperRoute = "//DeveloperAccount";

        public static IReadOnlyList<MainPageRole> Selectable { get; } = new[]
        {
            MainPageRole.Developer,
            MainPageRole.Investor,
            MainPageRole.Lawyer,
            MainPageRole.LettingAgent,
        };

        public static MainPageRole Resolve(MainPageRole? overrideRole, MainPageRole? profileRole)
        {
            return overrideRole ?? profileRole ?? MainPageRole.Investor;
        }

        /// <summary>
        /// The landing page behind a role. <see cref="MainPageRole.LettingAgent"/> still has
        /// no page of its own and falls back to the investor one, as every unimplemented role
        /// has always done.
        /// </summary>
        public static string ResolveRoute(MainPageRole role) => role switch
        {
            MainPageRole.Lawyer => LawyerRoute,
            MainPageRole.Developer => DeveloperRoute,
            _ => InvestorRoute,
        };

        public static bool TryParse(string? value, out MainPageRole role)
        {
            return Enum.TryParse(value, out role) && Enum.IsDefined(role);
        }
    }
}
