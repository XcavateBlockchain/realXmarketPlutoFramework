namespace PlutoFrameworkCore.PushNotificationServices.Core.Misc;

/// <summary>
/// The chain identifiers the notifications API accepts for wallet registration.
/// Anything else is rejected with a serializer error, so call sites use these
/// rather than free-form strings.
/// </summary>
public static class WalletChain
{
    public const string Solana = "solana";
    public const string Polkadot = "polkadot";
}
