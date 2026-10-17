using PlutoFrameworkCore.Xcavate;

namespace PlutoFrameworkTests
{
    /// <summary>
    /// The terms-document endpoint's response contract, as the app's client parses it:
    /// <c>{"cid","url","fileName","updatedAt"}</c> with an ISO-8601 <c>updatedAt</c>.
    /// </summary>
    public class TermsDocumentInfoTests
    {
        [Test]
        public void Parse_MapsEveryField()
        {
            var info = TermsDocumentInfo.Parse(
                "{\"cid\":\"bafybeigdyrzt5sfp7udm7hu76uh7y26nf3efuylqabf3oclgtqy55fbzdi\"," +
                "\"url\":\"https://gateway.pinata.cloud/ipfs/bafybeigdyrzt5sfp7udm7hu76uh7y26nf3efuylqabf3oclgtqy55fbzdi\"," +
                "\"fileName\":\"terms.pdf\"," +
                "\"updatedAt\":\"2026-10-17T12:34:56.789+00:00\"}");

            Assert.That(info.Cid, Is.EqualTo("bafybeigdyrzt5sfp7udm7hu76uh7y26nf3efuylqabf3oclgtqy55fbzdi"));
            Assert.That(info.Url, Is.EqualTo("https://gateway.pinata.cloud/ipfs/bafybeigdyrzt5sfp7udm7hu76uh7y26nf3efuylqabf3oclgtqy55fbzdi"));
            Assert.That(info.FileName, Is.EqualTo("terms.pdf"));
            Assert.That(info.UpdatedAt, Is.EqualTo(new DateTimeOffset(2026, 10, 17, 12, 34, 56, 789, TimeSpan.Zero)));
        }

        [Test]
        public void Parse_AcceptsFractionlessUtcTimestamp()
        {
            var info = TermsDocumentInfo.Parse(
                "{\"cid\":\"bafytest\",\"url\":\"https://gateway.test/ipfs/bafytest\",\"fileName\":\"t.pdf\",\"updatedAt\":\"2026-10-17T12:34:56Z\"}");

            Assert.That(info.UpdatedAt, Is.EqualTo(new DateTimeOffset(2026, 10, 17, 12, 34, 56, TimeSpan.Zero)));
        }

        /// <summary>
        /// A missing field must throw: the client would otherwise show an empty file name
        /// or a default date as if the server had sent them.
        /// </summary>
        [TestCase("{\"url\":\"https://gateway.test/ipfs/x\",\"fileName\":\"t.pdf\",\"updatedAt\":\"2026-10-17T12:34:56Z\"}")]
        [TestCase("{\"cid\":\"x\",\"fileName\":\"t.pdf\",\"updatedAt\":\"2026-10-17T12:34:56Z\"}")]
        [TestCase("{\"cid\":\"x\",\"url\":\"https://gateway.test/ipfs/x\",\"updatedAt\":\"2026-10-17T12:34:56Z\"}")]
        [TestCase("{\"cid\":\"x\",\"url\":\"https://gateway.test/ipfs/x\",\"fileName\":\"t.pdf\"}")]
        [TestCase("{\"cid\":\"x\",\"url\":\"https://gateway.test/ipfs/x\",\"fileName\":\"t.pdf\",\"updatedAt\":\"not-a-date\"}")]
        [TestCase("not json at all")]
        public void Parse_ThrowsOnMalformedAnswer(string json)
        {
            Assert.Catch(() => TermsDocumentInfo.Parse(json));
        }
    }
}
