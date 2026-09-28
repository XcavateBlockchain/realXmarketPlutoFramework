namespace PlutoFrameworkCore.AssetDidComm
{
    /// <summary>
    /// The host serving the Asset DIDComm messenger dashboard. The dashboard is a Nuxt
    /// single-page app on static hosting: client-side routes such as
    /// <c>/messages/namespace/{id}</c> are answered with the complete, working app shell
    /// but a 404 status, so an http error status from this host does not mean the page
    /// failed to load.
    /// </summary>
    public static class MessengerDashboard
    {
        public const string Host = "realxmessenger.xcavate.io";

        /// <summary>
        /// True when <paramref name="url"/> is served by the dashboard host. Matched
        /// exactly rather than by suffix: a contains-style check would also clear
        /// lookalike hosts such as <c>realxmessenger.xcavate.io.evil.example</c>.
        /// </summary>
        public static bool IsHost(string? url) =>
            Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && uri.Host.Equals(Host, StringComparison.OrdinalIgnoreCase);
    }
}
