using PlutoFramework.Model.Xcavate;

namespace PlutoFrameworkTests
{
    internal class LawyerRegistryOperationContextTests
    {
        [Test]
        public void Matches_RequiresTheSameGenerationAndWallet()
        {
            var operation = new LawyerRegistryOperationContext(4, "lawyer-wallet");

            Assert.Multiple(() =>
            {
                Assert.That(operation.Matches(4, "lawyer-wallet"), Is.True);
                Assert.That(operation.Matches(5, "lawyer-wallet"), Is.False);
                Assert.That(operation.Matches(4, "replacement-wallet"), Is.False);
                Assert.That(operation.Matches(4, null), Is.False);
            });
        }

        [Test]
        public void Matches_RejectsAnEmptyExpectedWallet()
        {
            var operation = new LawyerRegistryOperationContext(4, string.Empty);

            Assert.That(operation.Matches(4, string.Empty), Is.False);
        }
    }
}
