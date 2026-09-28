using PlutoFrameworkCore.AssetDidComm;

namespace PlutoFrameworkTests
{
    /// <summary>
    /// The messenger's static host answers client-side routes with the working app shell
    /// but an error status, so the web view's load-failure monitor exempts this host from
    /// its http-status check. The exemption is host-exact: a loose match would silence
    /// error reporting on lookalike hosts as well.
    /// </summary>
    public class MessengerDashboardTests
    {
        [Test]
        [TestCase("https://realxmessenger.xcavate.io/messages/namespace/13?isHeaderVisible=false")]
        [TestCase("https://realxmessenger.xcavate.io/indexed-bucket/abc")]
        [TestCase("https://realxmessenger.xcavate.io/")]
        [TestCase("https://realxmessenger.xcavate.io")]
        [TestCase("https://REALXMESSENGER.XCAVATE.IO/messages/my-buckets/")]
        // Host-only check on purpose: the scheme is the server's own redirect concern.
        [TestCase("http://realxmessenger.xcavate.io/")]
        public void IsHost_ReturnsTrueForDashboardUrls(string url)
        {
            Assert.That(MessengerDashboard.IsHost(url), Is.True);
        }

        [Test]
        [TestCase("https://roles.xcavate.io/")]
        [TestCase("https://xcavate.io/")]
        // Suffix lookalikes: a contains-style match would wrongly exempt these.
        [TestCase("https://realxmessenger.xcavate.io.evil.example/")]
        [TestCase("https://notrealxmessenger.xcavate.io/")]
        [TestCase(null)]
        [TestCase("")]
        [TestCase("not a url")]
        public void IsHost_ReturnsFalseForAnythingElse(string? url)
        {
            Assert.That(MessengerDashboard.IsHost(url), Is.False);
        }
    }
}
