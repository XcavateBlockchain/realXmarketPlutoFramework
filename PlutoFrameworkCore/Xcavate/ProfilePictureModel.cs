namespace PlutoFrameworkCore.Xcavate
{
    /// <summary>
    /// Naming a profile picture for upload, and keeping the URL that comes back usable.
    /// </summary>
    /// <remarks>
    /// The profile API stores a picture at <c>profiles/{address}/{fileName}</c>, and an upload
    /// to a name that already exists overwrites that object in place. So the URL a profile
    /// hands back is byte for byte the one it handed back before the change. Caches key on
    /// exactly that string, and there is no version field on a profile to key on instead, so
    /// the app has to supply the thing that moves.
    /// </remarks>
    public static class ProfilePictureModel
    {
        /// <summary>
        /// What to call the uploaded file.
        /// </summary>
        /// <remarks>
        /// The extension is not decoration. The upload endpoint reads the content type off it
        /// against an allow-list - <c>.jpg .jpeg .png .gif .webp .bmp</c> - and never off the
        /// client, because the bucket serves objects publicly. A name carrying no extension
        /// misses that list and the whole upload comes back 400, which is how a picked picture
        /// ends up never reaching the bucket at all. <c>.jpg</c> because that is what
        /// <c>ImageModel.CompressImageToJpeg</c> encoded; naming any other allowed extension
        /// would label the object a format its bytes are not.
        ///
        /// The address is in the name so that one account's upload cannot land on the object
        /// another account is serving.
        /// </remarks>
        public static string UploadFileName(string address) => $"ProfilePicture_{address}.jpg";

        /// <summary>
        /// The URL to store on the profile after a save.
        /// </summary>
        /// <remarks>
        /// A profile update replaces every field it is sent - the API assigns the incoming
        /// picture over the stored one without checking it for null - so a save that uploaded
        /// nothing must send the URL already on the profile rather than the null it happens to
        /// be holding. Otherwise editing a nickname wipes the picture, and it disappears from
        /// the menu and the edit page at once.
        /// </remarks>
        /// <param name="uploadedUrl">What this save uploaded, if it uploaded anything.</param>
        /// <param name="existingUrl">What the profile had before this save.</param>
        public static string? ResolveStoredUrl(string? uploadedUrl, string? existingUrl) =>
            string.IsNullOrWhiteSpace(uploadedUrl)
                ? (string.IsNullOrWhiteSpace(existingUrl) ? null : existingUrl)
                : uploadedUrl;

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
