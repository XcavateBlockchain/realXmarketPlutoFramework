using PlutoFrameworkCore.Solana;

namespace PlutoFrameworkTests
{
    public class SolanaProgramErrorCatalogsTests
    {
        [SetUp]
        [TearDown]
        public void ClearRegistry()
        {
            SolanaProgramErrorCatalogs.Clear();
        }

        [Test]
        public void UnregisteredClusterHasNoCatalog()
        {
            Assert.That(SolanaProgramErrorCatalogs.Get(SolanaCluster.Devnet), Is.Null);
        }

        [Test]
        public void RegisteredCatalogIsReturnedPerCluster()
        {
            var devnet = new AnchorIdlErrorCatalog();
            devnet.TryAddIdl(IdlFixtures.MarketplaceIdl);

            SolanaProgramErrorCatalogs.Register(SolanaCluster.Devnet, devnet);

            Assert.That(SolanaProgramErrorCatalogs.Get(SolanaCluster.Devnet), Is.SameAs(devnet));
            Assert.That(SolanaProgramErrorCatalogs.Get(SolanaCluster.Mainnet), Is.Null);
        }

        /// <summary>
        /// The mechanism the app uses at startup: IDL files ship as embedded resources named
        /// "&lt;prefix&gt;&lt;file&gt;", and every loadable one joins the cluster's catalog.
        /// Broken files are skipped, not fatal.
        /// </summary>
        [Test]
        public void RegisterFromAssemblyLoadsPrefixedResourcesAndSkipsBrokenOnes()
        {
            var loaded = SolanaProgramErrorCatalogs.RegisterFromAssembly(
                typeof(SolanaProgramErrorCatalogsTests).Assembly,
                "PlutoFrameworkTests.TestIdls.Devnet.",
                SolanaCluster.Devnet);

            Assert.That(loaded, Is.EqualTo(1));

            var catalog = SolanaProgramErrorCatalogs.Get(SolanaCluster.Devnet);

            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog!.TryDescribe(IdlFixtures.MarketplaceAddress, 6013, out var error), Is.True);
            Assert.That(error!.Message, Is.EqualTo("Listing is not active"));
        }

        [Test]
        public void RegisterFromAssemblyWithNoMatchingResourcesRegistersNothing()
        {
            var loaded = SolanaProgramErrorCatalogs.RegisterFromAssembly(
                typeof(SolanaProgramErrorCatalogsTests).Assembly,
                "PlutoFrameworkTests.TestIdls.Mainnet.",
                SolanaCluster.Mainnet);

            Assert.That(loaded, Is.EqualTo(0));
            Assert.That(SolanaProgramErrorCatalogs.Get(SolanaCluster.Mainnet), Is.Null);
        }
    }
}
