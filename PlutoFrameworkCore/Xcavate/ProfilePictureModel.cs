namespace PlutoFrameworkCore.Xcavate
{
    /// <summary>
    /// The profile picture URL, made safe to show after the picture behind it changes.
    /// </summary>
    /// <remarks>
    /// The profile API keys a picture on the address alone - <c>profiles/profile_{address}.jpg</c>
    /// - and an upload overwrites that object in place, so the URL a profile hands back is
    /// byte for byte the one it handed back before the change. Caches key on exactly that
    /// string, and there is no version field on a profile to key on instead, so the app has to
    /// supply the thing that moves.
    /// </remarks>
    public static class ProfilePictureModel
    {
        /// <summary>
        /// Seeded from the clock so a token does not repeat across app restarts, and
        /// incremented rather than re-read because <see cref="DateTime.UtcNow"/> is only
        /// accurate to about 15ms on Windows - two loads in the same tick would share a URL.
        /// </summary>
        private static long lastToken = DateTime.UtcNow.Ticks;

        /// <summary>
        /// The URL to load, or null when the user has no picture.
        /// </summary>
        public static string? WithCacheBuster(string? url) =>
            WithCacheBuster(url, Interlocked.Increment(ref lastToken));

        /// <inheritdoc cref="WithCacheBuster(string?)"/>
        /// <param name="url">The URL as the profile stores it.</param>
        /// <param name="token">What makes this load distinct from the last one.</param>
        public static string? WithCacheBuster(string? url, long token)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            // Object storage ignores a parameter it does not know, so the object still
            // resolves; every cache in front of it sees a URL it has never fetched.
            var separator = url.Contains('?') ? '&' : '?';

            return $"{url}{separator}v={token}";
        }
    }
}
