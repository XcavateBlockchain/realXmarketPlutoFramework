namespace PlutoFramework.Model.Xcavate
{
    public enum LawyerRegistrationCardKind
    {
        Unavailable,
        Unregistered,
        Registered,
        Syncing,
    }

    public sealed record LawyerRegistrationCardState(
        LawyerRegistrationCardKind Kind,
        string Text,
        bool CanRegister)
    {
        public static LawyerRegistrationCardState Create(
            ushort? registeredRegion,
            ushort? pendingRegion,
            bool loadFailed,
            bool busy)
        {
            if (pendingRegion is not null)
            {
                return new(LawyerRegistrationCardKind.Syncing, "Registration confirmed · Syncing…", false);
            }

            if (registeredRegion is { } regionId)
            {
                return new(LawyerRegistrationCardKind.Registered, $"Registered in Region {regionId}", false);
            }

            if (loadFailed)
            {
                return new(
                    LawyerRegistrationCardKind.Unavailable,
                    "Registration status unavailable. Pull down to try again.",
                    false);
            }

            return new(LawyerRegistrationCardKind.Unregistered, "Select the region you work in", !busy);
        }
    }
}
