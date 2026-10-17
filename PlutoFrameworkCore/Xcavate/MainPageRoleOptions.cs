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

        public static string ResolveRoute(MainPageRole role)
        {
            return role == MainPageRole.Lawyer ? LawyerRoute : InvestorRoute;
        }

        public static bool TryParse(string? value, out MainPageRole role)
        {
            return Enum.TryParse(value, out role) && Enum.IsDefined(role);
        }
    }
}
