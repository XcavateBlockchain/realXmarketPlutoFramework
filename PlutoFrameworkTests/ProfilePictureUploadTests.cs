using PlutoFrameworkCore.Xcavate;

namespace PlutoFrameworkTests
{
    /// <summary>
    /// What has to be true for a picture to reach the bucket at all, and to still be there
    /// after the next save.
    /// </summary>
    public class ProfilePictureUploadTests
    {
        private const string Address = "5Di7RnyX8TXwM9C9RCVHWTuXemwmRiJLiX3wapYgN588qB2E";

        /// <summary>
        /// The upload endpoint derives the content type from the file name's extension and
        /// never from the client, because the bucket serves objects publicly. An extension it
        /// does not recognise - which includes no extension at all - is a 400.
        /// </summary>
        private static readonly string[] ExtensionsTheApiAccepts =
            [".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp"];

        [Test]
        public void NamesTheUploadWithAnExtensionTheApiAccepts()
        {
            Assert.That(
                ExtensionsTheApiAccepts,
                Does.Contain(Path.GetExtension(ProfilePictureModel.UploadFileName(Address))));
        }

        /// <summary>
        /// The extension is the only thing the bucket goes on when it labels the object, so it
        /// has to name the format we actually encoded - JPEG - or every client fetching the
        /// picture is handed the wrong content type.
        /// </summary>
        [Test]
        public void NamesTheUploadForTheFormatWeEncode()
        {
            Assert.That(Path.GetExtension(ProfilePictureModel.UploadFileName(Address)), Is.EqualTo(".jpg"));
        }

        /// <summary>
        /// Uploading over a name that already exists replaces that object, so the name has to
        /// belong to one account.
        /// </summary>
        [Test]
        public void GivesTwoAccountsTwoDifferentNames()
        {
            Assert.That(
                ProfilePictureModel.UploadFileName(Address),
                Is.Not.EqualTo(ProfilePictureModel.UploadFileName("9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWM")));
        }

        /// <summary>
        /// The bug behind a picture that vanishes: a profile update replaces every field it is
        /// sent, so saving a nickname while uploading nothing writes a null over the URL and
        /// the picture is gone from the menu and the edit page both.
        /// </summary>
        [Test]
        public void KeepsThePictureWhenTheSaveUploadedNothing()
        {
            Assert.That(
                ProfilePictureModel.ResolveStoredUrl(uploadedUrl: null, existingUrl: "https://bucket/old.jpg"),
                Is.EqualTo("https://bucket/old.jpg"));
        }

        /// <summary>
        /// A failed upload returns nothing rather than throwing on every path, and an empty
        /// string is no more a picture than a null is.
        /// </summary>
        [Test]
        public void KeepsThePictureWhenTheUploadCameBackEmpty()
        {
            Assert.That(
                ProfilePictureModel.ResolveStoredUrl(uploadedUrl: "", existingUrl: "https://bucket/old.jpg"),
                Is.EqualTo("https://bucket/old.jpg"));
        }

        [Test]
        public void TakesTheNewPictureOverTheOldOne()
        {
            Assert.That(
                ProfilePictureModel.ResolveStoredUrl(uploadedUrl: "https://bucket/new.jpg", existingUrl: "https://bucket/old.jpg"),
                Is.EqualTo("https://bucket/new.jpg"));
        }

        /// <summary>
        /// Null is what the API stores for a user with no picture, and what the pages read to
        /// show the placeholder, so it has to survive rather than become an empty string.
        /// </summary>
        [Test]
        public void HasNoPictureWhenThereHasNeverBeenOne()
        {
            Assert.That(ProfilePictureModel.ResolveStoredUrl(uploadedUrl: null, existingUrl: null), Is.Null);
            Assert.That(ProfilePictureModel.ResolveStoredUrl(uploadedUrl: "", existingUrl: ""), Is.Null);
        }

        /// <summary>
        /// The stored URL is what every later load is built from, so a display-time cache
        /// buster must never be written back - it would pin the picture to one token and undo
        /// the busting for good.
        /// </summary>
        [Test]
        public void StoresTheUrlWithoutACacheBuster()
        {
            const string url = "https://bucket/profiles/a/ProfilePicture_a.jpg";

            Assert.That(ProfilePictureModel.ResolveStoredUrl(url, null), Does.Not.Contain("?v="));
        }
    }
}
