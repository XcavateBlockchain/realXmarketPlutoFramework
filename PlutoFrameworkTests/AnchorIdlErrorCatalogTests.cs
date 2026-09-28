using PlutoFrameworkCore.Solana;

namespace PlutoFrameworkTests
{
    public class AnchorIdlErrorCatalogTests
    {
        [Test]
        public void ParsesProgramErrorsByAddress()
        {
            var catalog = new AnchorIdlErrorCatalog();

            Assert.That(catalog.TryAddIdl(IdlFixtures.MarketplaceIdl), Is.True);
            Assert.That(catalog.ProgramCount, Is.EqualTo(1));
            Assert.That(catalog.TryDescribe(IdlFixtures.MarketplaceAddress, 6013, out var error), Is.True);
            Assert.That(error!.Name, Is.EqualTo("ListingNotActive"));
            Assert.That(error.Message, Is.EqualTo("Listing is not active"));
            Assert.That(error.Code, Is.EqualTo(6013));
        }

        [Test]
        public void ReadsProgramNameFromMetadata()
        {
            var catalog = new AnchorIdlErrorCatalog();
            catalog.TryAddIdl(IdlFixtures.MarketplaceIdl);

            Assert.That(catalog.ProgramName(IdlFixtures.MarketplaceAddress), Is.EqualTo("marketplace"));
        }

        /// <summary>
        /// The same custom code means different things in different programs (6013 exists in
        /// both real devnet IDLs), so the lookup must key on the program, never the code alone.
        /// </summary>
        [Test]
        public void SameCodeInTwoProgramsStaysDistinct()
        {
            var catalog = new AnchorIdlErrorCatalog();
            catalog.TryAddIdl(IdlFixtures.MarketplaceIdl);
            catalog.TryAddIdl(IdlFixtures.PropertyIdl);

            Assert.That(catalog.TryDescribe(IdlFixtures.MarketplaceAddress, 6013, out var market), Is.True);
            Assert.That(catalog.TryDescribe(IdlFixtures.PropertyAddress, 6013, out var property), Is.True);
            Assert.That(market!.Message, Is.EqualTo("Listing is not active"));
            Assert.That(property!.Message, Is.EqualTo("Postcode is too long"));
        }

        /// <summary>
        /// A bundled IDL is app-shipped data: a corrupt one must never crash startup, it
        /// just contributes nothing.
        /// </summary>
        [Test]
        public void MalformedJsonIsRejectedNotThrown()
        {
            var catalog = new AnchorIdlErrorCatalog();

            Assert.That(catalog.TryAddIdl("{ this is not json"), Is.False);
            Assert.That(catalog.ProgramCount, Is.EqualTo(0));
        }

        [Test]
        public void NullOrEmptyJsonIsRejected()
        {
            var catalog = new AnchorIdlErrorCatalog();

            Assert.That(catalog.TryAddIdl(null), Is.False);
            Assert.That(catalog.TryAddIdl(""), Is.False);
            Assert.That(catalog.TryAddIdl("   "), Is.False);
        }

        /// <summary>
        /// Without an address there is nothing to key the errors by, so the file is useless
        /// to the catalog even when the errors themselves are well-formed.
        /// </summary>
        [Test]
        public void IdlWithoutAddressIsRejected()
        {
            var catalog = new AnchorIdlErrorCatalog();

            Assert.That(catalog.TryAddIdl("""
                { "metadata": { "name": "marketplace" }, "errors": [ { "code": 6000, "name": "X", "msg": "Y" } ] }
                """), Is.False);
            Assert.That(catalog.ProgramCount, Is.EqualTo(0));
        }

        /// <summary>
        /// A program whose bundled IDL lost its errors array (a stale or hand-trimmed file)
        /// is still worth knowing by name: the caller can then say WHICH program rejected
        /// the transaction even though it cannot say why.
        /// </summary>
        [Test]
        public void IdlWithoutErrorsStillIdentifiesTheProgram()
        {
            var catalog = new AnchorIdlErrorCatalog();

            Assert.That(catalog.TryAddIdl("""
                { "address": "dj9Q3CpHvDHwexCbkgJ5APDx4JsTxPssNebkvP15g1T", "metadata": { "name": "marketplace" } }
                """), Is.True);
            Assert.That(catalog.ContainsProgram(IdlFixtures.MarketplaceAddress), Is.True);
            Assert.That(catalog.ProgramName(IdlFixtures.MarketplaceAddress), Is.EqualTo("marketplace"));
            Assert.That(catalog.TryDescribe(IdlFixtures.MarketplaceAddress, 6013, out _), Is.False);
        }

        [Test]
        public void UnknownCodeDescribesNothing()
        {
            var catalog = new AnchorIdlErrorCatalog();
            catalog.TryAddIdl(IdlFixtures.MarketplaceIdl);

            Assert.That(catalog.TryDescribe(IdlFixtures.MarketplaceAddress, 9999, out var error), Is.False);
            Assert.That(error, Is.Null);
        }

        [Test]
        public void UnknownProgramDescribesNothing()
        {
            var catalog = new AnchorIdlErrorCatalog();
            catalog.TryAddIdl(IdlFixtures.MarketplaceIdl);

            Assert.That(catalog.TryDescribe(IdlFixtures.PropertyAddress, 6013, out _), Is.False);
            Assert.That(catalog.ContainsProgram(IdlFixtures.PropertyAddress), Is.False);
            Assert.That(catalog.ContainsProgram(null), Is.False);
            Assert.That(catalog.ProgramName(IdlFixtures.PropertyAddress), Is.Null);
        }

        /// <summary>
        /// One malformed error entry must not take the well-formed ones down with it.
        /// </summary>
        [Test]
        public void BrokenEntriesAreSkippedIndividually()
        {
            var catalog = new AnchorIdlErrorCatalog();

            Assert.That(catalog.TryAddIdl("""
                {
                  "address": "dj9Q3CpHvDHwexCbkgJ5APDx4JsTxPssNebkvP15g1T",
                  "errors": [
                    { "name": "NoCode" },
                    { "code": 6000, "name": "NotAuthority", "msg": "Signer is not the authority" }
                  ]
                }
                """), Is.True);
            Assert.That(catalog.TryDescribe(IdlFixtures.MarketplaceAddress, 6000, out _), Is.True);
        }

        /// <summary>
        /// Re-adding a program (an updated IDL file replacing an older registration) keeps
        /// the newest table, not a merge of both.
        /// </summary>
        [Test]
        public void ReAddingAProgramOverwritesItsTable()
        {
            var catalog = new AnchorIdlErrorCatalog();
            catalog.TryAddIdl(IdlFixtures.MarketplaceIdl);
            catalog.TryAddIdl("""
                {
                  "address": "dj9Q3CpHvDHwexCbkgJ5APDx4JsTxPssNebkvP15g1T",
                  "metadata": { "name": "marketplace" },
                  "errors": [ { "code": 6001, "name": "NewError", "msg": "A new failure mode" } ]
                }
                """);

            Assert.That(catalog.ProgramCount, Is.EqualTo(1));
            Assert.That(catalog.TryDescribe(IdlFixtures.MarketplaceAddress, 6013, out _), Is.False);
            Assert.That(catalog.TryDescribe(IdlFixtures.MarketplaceAddress, 6001, out var error), Is.True);
            Assert.That(error!.Message, Is.EqualTo("A new failure mode"));
        }
    }
}
