using PlutoFramework.Model.Xcavate;
using PlutoFrameworkCore.Solana;

namespace PlutoFrameworkTests
{
    internal class LawyerRegistrationTransactionTests
    {
        [Test]
        public void Matches_RequiresTheExactSignatureAndCluster()
        {
            var registration = new LawyerRegistrationTransaction("registration-signature", SolanaCluster.Devnet);

            Assert.Multiple(() =>
            {
                Assert.That(registration.Matches("registration-signature", SolanaCluster.Devnet), Is.True);
                Assert.That(registration.Matches("unrelated-signature", SolanaCluster.Devnet), Is.False);
                Assert.That(registration.Matches("registration-signature", SolanaCluster.Mainnet), Is.False);
            });
        }
    }
}
