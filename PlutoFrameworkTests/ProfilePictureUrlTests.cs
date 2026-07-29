using PlutoFrameworkCore.Xcavate;

namespace PlutoFrameworkTests
{
    /// <summary>
    /// The profile API stores a picture at <c>profiles/{address}/{fileName}</c>, and because
    /// the app names every upload for the same account the same thing, a new picture overwrites
    /// the old object in place. The URL a profile hands back is therefore the same string
    /// before and after the user changes their picture, which is exactly the shape every image
    /// cache keys on. Without a token that moves, the menu and the edit page keep showing the
    /// previous picture.
    /// </summary>
    public class ProfilePictureUrlTests
    {
        private const string Url = "https://xcavate-profile.fsn1.your-objectstorage.com/profiles/5Di7RnyX8TXwM9C9RCVHWTuXemwmRiJLiX3wapYgN588qB2E/ProfilePicture_5Di7RnyX8TXwM9C9RCVHWTuXemwmRiJLiX3wapYgN588qB2E.jpg";

        /// <summary>
        /// The bug this whole function exists for: two loads of an unchanged URL have to ask
        /// for two different resources, or the second one is served from a cache.
        /// </summary>
        [Test]
        public void GivesADifferentUrlForEveryToken()
        {
            Assert.That(
                ProfilePictureModel.WithCacheBuster(Url, 637_000_000_000_000_001),
                Is.Not.EqualTo(ProfilePictureModel.WithCacheBuster(Url, 637_000_000_000_000_002)));
        }

        /// <summary>
        /// Busting the cache must not cost us the object: everything the storage keys on has
        /// to survive ahead of the token it does not read.
        /// </summary>
        [Test]
        public void KeepsTheStoredUrlIntact()
        {
            Assert.That(ProfilePictureModel.WithCacheBuster(Url, 1), Does.StartWith(Url + "?"));
        }

        [Test]
        public void AppendsTheTokenAsAQueryParameter()
        {
            Assert.That(ProfilePictureModel.WithCacheBuster(Url, 42), Is.EqualTo(Url + "?v=42"));
        }

        /// <summary>
        /// A URL that already carries a query - a signature, say - must not have its first
        /// parameter turned into part of the last one.
        /// </summary>
        [Test]
        public void AddsToAnExistingQueryRatherThanStartingASecondOne()
        {
            Assert.That(
                ProfilePictureModel.WithCacheBuster("https://example.com/a.jpg?x=1", 42),
                Is.EqualTo("https://example.com/a.jpg?x=1&v=42"));
        }

        /// <summary>
        /// Null means the user has no picture. Callers show a placeholder for it, so it has to
        /// stay null rather than become a query string pointing at nothing.
        /// </summary>
        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void HasNoUrlWhenThereIsNoPicture(string? url)
        {
            Assert.That(ProfilePictureModel.WithCacheBuster(url, 42), Is.Null);
        }

        /// <summary>
        /// The convenience overload is what the two call sites use, so its token has to move on
        /// its own - a fixed one would reintroduce the bug.
        /// </summary>
        [Test]
        public void GeneratesAMovingTokenWhenTheCallerSuppliesNone()
        {
            Assert.That(
                ProfilePictureModel.WithCacheBuster(Url),
                Is.Not.EqualTo(ProfilePictureModel.WithCacheBuster(Url)));
        }
    }
}
