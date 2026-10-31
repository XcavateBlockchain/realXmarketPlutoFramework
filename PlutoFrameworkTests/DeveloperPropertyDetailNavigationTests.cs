using PlutoFramework.Model.Xcavate;

namespace PlutoFrameworkTests;

public class DeveloperPropertyDetailNavigationTests
{
    [Test]
    public void TryGetOnChainListingId_ListedRecord_UsesMarketplaceDetailFlow()
    {
        var resolved = DeveloperPropertyDetailNavigation.TryGetOnChainListingId(
            DeveloperPropertyStatus.Listed, "42", out var listingId);

        Assert.That(resolved, Is.True);
        Assert.That(listingId, Is.EqualTo(42));
    }

    [Test]
    public void TryGetOnChainListingId_VerifiedRecord_StaysOnRecordDetailFlow()
    {
        var resolved = DeveloperPropertyDetailNavigation.TryGetOnChainListingId(
            DeveloperPropertyStatus.Verified, "42", out _);

        Assert.That(resolved, Is.False);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("not-a-listing-id")]
    public void TryGetOnChainListingId_ListedRecordWithoutValidId_StaysOnRecordDetailFlow(string? value)
    {
        var resolved = DeveloperPropertyDetailNavigation.TryGetOnChainListingId(
            DeveloperPropertyStatus.Listed, value, out _);

        Assert.That(resolved, Is.False);
    }
}
