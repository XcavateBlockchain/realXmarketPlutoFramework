using PlutoFramework.Model;
using PlutoFrameworkCore;

namespace PlutoFramework.Components.WebView
{
    /// <summary>
    /// Decides whether a dapp hosted in a WebView may reach the wallet, and remembers the
    /// answer for the rest of the session.
    ///
    /// Shared by every injected provider. The Polkadot and Solana wallets must show the same
    /// connection screen and honour the same allow-lists, and two copies of this sequence
    /// would drift.
    /// </summary>
    public static class DAppApprovalModel
    {
        /// <summary>
        /// Approves a dapp, prompting the user when nothing already settles it.
        /// </summary>
        /// <param name="silent">
        /// Answer from what is already known and never prompt. The Wallet Standard's
        /// <c>standard:connect</c> passes this when a dapp reconnects on page load, where a
        /// popup the user did not ask for would be an ambush.
        /// </param>
        public static async Task<bool> RequestAsync(DAppInfo dAppInfo, bool silent = false)
        {
            if (IsAlreadyApproved(dAppInfo.Url))
            {
                return true;
            }

            if (silent)
            {
                return false;
            }

            var popupViewModel = DependencyService.Get<DAppWebViewConnectionRequestPopupViewModel>();

            var approved = await popupViewModel.ShowAsync(dAppInfo);

            ExtensionWebViewModel.ApprovedUrls[new Uri(dAppInfo.Url).Host] = approved;

            return approved;
        }

        /// <summary>
        /// Whether the dapp is already cleared to connect, without prompting or unlocking
        /// anything. Safe to call while a page is still loading.
        /// </summary>
        /// <remarks>
        /// A cached rejection deliberately does not count as an answer here: the user
        /// declining once should not lock the dapp out for the session, so the next
        /// non-silent request asks again.
        /// </remarks>
        public static bool IsAlreadyApproved(string url)
        {
            if (Application.Current?.Resources.TryGetValue("AllowedOrigins", out var configured) == true
                && configured is string[] allowedOrigins
                && allowedOrigins.Any(url.Contains))
            {
                return true;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return false;
            }

            if (PlutoConfigurationModel.WhitelistedDApps.Any(pattern => uri.Host.Contains(pattern)))
            {
                return true;
            }

            return ExtensionWebViewModel.ApprovedUrls.TryGetValue(uri.Host, out var cachedApproved) && cachedApproved;
        }

        /// <summary>
        /// Forgets a host's cached approval so the next connection request asks again.
        /// Backs the Wallet Standard's <c>standard:disconnect</c>.
        /// </summary>
        /// <remarks>
        /// Hosts cleared by <c>AllowedOrigins</c> or the whitelist stay cleared — those are
        /// configuration rather than a decision the user made and can take back.
        /// </remarks>
        public static void Revoke(string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                ExtensionWebViewModel.ApprovedUrls.Remove(uri.Host);
            }
        }
    }
}
