using PlutoFrameworkCore.Xcavate;

namespace PlutoFramework.Model.Xcavate.Profile
{
    /// <summary>
    /// Turns the URL a profile stores into something an <see cref="Image"/> can show.
    /// </summary>
    /// <remarks>
    /// One place rather than one per page: the menu and the edit page show the same picture,
    /// and when only one of them busted the cache the two disagreed about whose face the user
    /// was looking at.
    /// </remarks>
    public static class ProfilePictureImageSourceModel
    {
        /// <summary>
        /// The picture to show, or null when the user has none.
        /// </summary>
        /// <remarks>
        /// <see cref="UriImageSource.CachingEnabled"/> is deliberately left alone. It reads
        /// like the fix for a stale picture and is not one: MAUI never implemented the cache it
        /// switches off (the code is commented out behind a TODO in UriImageSource), so it
        /// changed nothing while the caches that actually held the old picture - NSURLCache on
        /// iOS, Glide on Android - went on keying off a URL that never changed. The moving URL
        /// from <see cref="ProfilePictureModel.WithCacheBuster(string?)"/> is what they answer to.
        /// </remarks>
        public static ImageSource? Create(string? profilePictureUrl)
        {
            var url = ProfilePictureModel.WithCacheBuster(profilePictureUrl);

            return url is null ? null : new UriImageSource { Uri = new Uri(url) };
        }
    }
}
