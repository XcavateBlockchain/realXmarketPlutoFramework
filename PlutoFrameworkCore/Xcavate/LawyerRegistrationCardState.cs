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
        bool CanRegister,
        bool CanUnregister = false)
    {
        public static LawyerRegistrationCardState Create(
            ushort? registeredRegion,
            ushort? pendingRegion,
            bool statusLoaded,
            bool loadFailed,
            bool busy,
            bool unregistrationConfirmed = false)
        {
            if (unregistrationConfirmed)
            {
                return new(LawyerRegistrationCardKind.Syncing, "Unregistration confirmed · Syncing…", false);
            }

            if (pendingRegion is not null)
            {
                return new(LawyerRegistrationCardKind.Syncing, "Registration confirmed · Syncing…", false);
            }

            if (registeredRegion is { } regionId)
            {
                return new(LawyerRegistrationCardKind.Registered, $"Registered in Region {regionId}", false, !busy);
            }

            if (loadFailed)
            {
                return new(
                    LawyerRegistrationCardKind.Unavailable,
                    "Registration status unavailable. Pull down to try again.",
                    false);
            }

            if (!statusLoaded)
            {
                return new(LawyerRegistrationCardKind.Unavailable, "Checking registration status…", false);
            }

            return new(LawyerRegistrationCardKind.Unregistered, "Select the region you work in", !busy);
        }
    }
}
