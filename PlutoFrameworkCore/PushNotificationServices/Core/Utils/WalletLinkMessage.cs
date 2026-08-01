namespace PlutoFrameworkCore.PushNotificationServices.Core.Utils;

/// <summary>
/// The canonical message a wallet signs to prove it owns an address being linked.
///
/// The server rebuilds this from the request fields and the JWT's device id, then
/// verifies the signature against its own copy - a client-supplied message is never
/// trusted - so the bytes here must match the API's documented format exactly:
/// UTF-8, LF separators, no trailing newline, nonce used verbatim.
/// </summary>
public static class WalletLinkMessage
{
    public static string Build(string chain, string address, string nonce, string deviceId) =>
        "PlutoFramework wallet link\n" +
        $"chain: {chain}\n" +
        $"address: {address}\n" +
        $"nonce: {nonce}\n" +
        $"device: {deviceId}";
}
