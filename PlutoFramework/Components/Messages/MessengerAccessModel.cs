using PlutoFramework.Components.Account;
using PlutoFramework.Components.Keys;
using PlutoFramework.Model;

namespace PlutoFramework.Components.Messages;

/// <summary>
/// The single gate for opening the messenger (realxmessenger.xcavate.io): the hosted
/// dashboard decrypts messages with the account's X25519 key, so the page may only open
/// for an account that holds one. Without a wallet at all the account popup is raised
/// instead; with a wallet but no key, the create-or-import popup is raised.
/// </summary>
public static class MessengerAccessModel
{
    /// <summary>
    /// Opens the messenger page (or the given bucket/namespace URL) when the account may
    /// open it, otherwise raises the popup that names what is missing. True when the page
    /// was opened.
    /// </summary>
    public static async Task<bool> TryOpenMessagesAsync(string? url = null)
    {
        if (!KeysModel.HasSolanaKey() && !KeysModel.HasSubstrateKey())
        {
            DependencyService.Get<NoAccountPopupViewModel>().IsVisible = true;

            return false;
        }

        if (!await KeysModel.HasEncryptionX25519KeyAsync())
        {
            DependencyService.Get<X25519KeyRequiredPopupViewModel>().IsVisible = true;

            return false;
        }

        await Shell.Current.Navigation.PushAsync(new MessageWebViewPage(url));

        return true;
    }
}
