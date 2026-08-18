namespace PlutoFrameworkCore.PushNotificationServices.Core.Utils;

/// <summary>
/// The API issues 43-character unpadded URL-safe base64 nonces, but the nonce given to
/// Play Integrity must be padded to a multiple of 4: Google echoes the string back
/// verbatim inside the signed verdict, and the server decodes that echo as strict
/// base64, rejecting the registration when the padding is missing
/// (per the API's docs/client-integration.md).
/// </summary>
public static class PlayIntegrityNonce
{
    public static string Pad(string nonce) =>
        nonce + new string('=', (4 - nonce.Length % 4) % 4);
}
