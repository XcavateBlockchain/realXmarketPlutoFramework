using UniqueryPlus.Metadata;

namespace UniqueryPlusTests
{
    /// <summary>
    /// <see cref="PropertyMetadata.DisplayImages"/> is the list every image surface except
    /// the full-screen image page binds to: the indexer's 720x720 mirror thumbnails when
    /// the mirror has uploaded them, the document's full-resolution <c>Files</c>
    /// otherwise (mirror disabled or no upload for the asset yet).
    /// </summary>
    internal class XcavateMetadataTests
    {
        [Test]
        public void DisplayImages_PrefersThumbnailsWhenPresent()
        {
            var metadata = new PropertyMetadata
            {
                Financials = new PropertyFinancials(),
                Address = new PropertyAddress(),
                Files = ["https://cdn.example/full-1.jpg", "https://cdn.example/full-2.jpg"],
                ThumbnailFiles = ["https://cdn.example/thumb-1.jpg"],
            };

            Assert.That(metadata.DisplayImages, Is.EqualTo(metadata.ThumbnailFiles));
        }

        [Test]
        public void DisplayImages_FallsBackToFullImagesWhenNoThumbnails()
        {
            var metadata = new PropertyMetadata
            {
                Financials = new PropertyFinancials(),
                Address = new PropertyAddress(),
                Files = ["https://cdn.example/full-1.jpg"],
            };

            Assert.That(metadata.DisplayImages, Is.EqualTo(metadata.Files));
        }

        [Test]
        public void DisplayImages_IsEmptyWhenNeitherListHasImages()
        {
            var metadata = new PropertyMetadata
            {
                Financials = new PropertyFinancials(),
                Address = new PropertyAddress(),
            };

            Assert.That(metadata.DisplayImages, Is.Empty);
        }
    }
}
