using PlutoFramework.Model.Xcavate;
using PlutoFrameworkCore.Solana;

namespace PlutoFrameworkTests
{
    /// <summary>
    /// Pins the deployment reality behind XcavateDeploymentModel: devnet has the Xcavate
    /// programs and indexer, mainnet and testnet do not. The mainnet assertions are the
    /// placeholder contract - they are the tests to update when the mainnet deployment
    /// lands (idls/mainnet/, XcavateProgramAddresses.Mainnet, XcavateWhitelistIndexer.MainnetUrl).
    /// </summary>
    internal class XcavateDeploymentModelTests
    {
        [Test]
        public void IsDeployed_Devnet_IsTrue()
        {
            Assert.That(XcavateDeploymentModel.IsDeployed(SolanaCluster.Devnet), Is.True);
        }

        [Test]
        public void IsDeployed_Mainnet_IsFalseUntilTheProgramsDeploy()
        {
            Assert.That(XcavateDeploymentModel.IsDeployed(SolanaCluster.Mainnet), Is.False);
        }

        [Test]
        public void IsDeployed_Testnet_IsFalse()
        {
            // Nothing of Xcavate's runs on testnet, by design.
            Assert.That(XcavateDeploymentModel.IsDeployed(SolanaCluster.Testnet), Is.False);
        }

        [Test]
        public void NotDeployedMessage_NamesTheClusterAndPointsAtADeployedOne()
        {
            var message = XcavateDeploymentModel.NotDeployedMessage(SolanaCluster.Mainnet);

            Assert.That(message, Does.Contain("Mainnet"));
            // While devnet is the only deployment, it is the network the message sends
            // the user to.
            Assert.That(message, Does.Contain("Devnet"));
        }

        [Test]
        public void Require_Mainnet_ThrowsNotSupportedInsteadOfNullReference()
        {
            Assert.That(
                () => XcavateProgramAddresses.Require(SolanaCluster.Mainnet),
                Throws.InstanceOf<NotSupportedException>());
        }
    }
}
