using PlutoFrameworkCore.Solana;

namespace PlutoFrameworkTests
{
    public class SolanaSubmissionErrorDescriberTests
    {
        private const string Raw = "Could not submit the transaction on Devnet: Transaction simulation failed: "
            + "Error processing Instruction 0: custom program error: 0x177d";

        private static AnchorIdlErrorCatalog MarketplaceCatalog()
        {
            var catalog = new AnchorIdlErrorCatalog();
            catalog.TryAddIdl(IdlFixtures.MarketplaceIdl);

            return catalog;
        }

        /// <summary>
        /// The case the whole feature exists for: the raw node reason is replaced by what
        /// the program's own metadata says the error means, while the numeric code stays
        /// for matching against Solscan.
        /// </summary>
        [Test]
        public void DecodesKnownCustomError()
        {
            var text = SolanaSubmissionErrorDescriber.Describe(
                Raw, MarketplaceCatalog(), [IdlFixtures.MarketplaceAddress]);

            Assert.That(text, Does.StartWith("Could not submit the transaction on Devnet"));
            Assert.That(text, Does.Contain("marketplace"));
            Assert.That(text, Does.Contain("Listing is not active"));
            Assert.That(text, Does.Contain("6013"));
            Assert.That(text, Does.Not.Contain("0x177d"));
        }

        [Test]
        public void DecodesDecimalCodeForm()
        {
            var text = SolanaSubmissionErrorDescriber.Describe(
                "Could not submit the transaction on Devnet: Transaction simulation failed: "
                + "Error processing Instruction 0: custom program error: 6013",
                MarketplaceCatalog(), [IdlFixtures.MarketplaceAddress]);

            Assert.That(text, Does.Contain("Listing is not active"));
        }

        /// <summary>
        /// The program is identified by which instruction failed, not by guessing from the
        /// code: instruction 0 here targets the property program, so its 6013 wording wins.
        /// </summary>
        [Test]
        public void UsesTheFailingInstructionsProgram()
        {
            var catalog = MarketplaceCatalog();
            catalog.TryAddIdl(IdlFixtures.PropertyIdl);

            var text = SolanaSubmissionErrorDescriber.Describe(
                Raw, catalog, [IdlFixtures.PropertyAddress]);

            Assert.That(text, Does.Contain("Postcode is too long"));
            Assert.That(text, Does.Not.Contain("Listing is not active"));
        }

        /// <summary>
        /// Stale metadata: the program deployed a new error after this app shipped. The user
        /// gets an honest "cannot decode" naming the program, plus the node's own words so
        /// support can still match the failure.
        /// </summary>
        [Test]
        public void UnknownCodeSaysCannotDecodeAndKeepsNodeDetail()
        {
            var text = SolanaSubmissionErrorDescriber.Describe(
                "Could not submit the transaction on Devnet: Transaction simulation failed: "
                + "Error processing Instruction 0: custom program error: 0x270f",
                MarketplaceCatalog(), [IdlFixtures.MarketplaceAddress]);

            Assert.That(text, Does.Contain("marketplace"));
            Assert.That(text, Does.Contain("cannot decode"));
            Assert.That(text, Does.Contain("9999"));
            Assert.That(text, Does.Contain("0x270f"));
        }

        /// <summary>
        /// A failing instruction from a program the app ships no IDL for (SPL token, system)
        /// must not get a made-up explanation: the raw reason stands.
        /// </summary>
        [Test]
        public void UnknownProgramKeepsRawMessage()
        {
            var text = SolanaSubmissionErrorDescriber.Describe(
                Raw, MarketplaceCatalog(), ["TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA"]);

            Assert.That(text, Is.EqualTo(Raw));
        }

        [Test]
        public void MissingCatalogKeepsRawMessage()
        {
            var text = SolanaSubmissionErrorDescriber.Describe(Raw, null, [IdlFixtures.MarketplaceAddress]);

            Assert.That(text, Is.EqualTo(Raw));
        }

        [Test]
        public void MissingProgramIdsKeepsRawMessage()
        {
            var text = SolanaSubmissionErrorDescriber.Describe(Raw, MarketplaceCatalog(), null);

            Assert.That(text, Is.EqualTo(Raw));
        }

        [Test]
        public void InstructionIndexOutOfRangeKeepsRawMessage()
        {
            var text = SolanaSubmissionErrorDescriber.Describe(Raw, MarketplaceCatalog(), []);

            Assert.That(text, Is.EqualTo(Raw));
        }

        /// <summary>
        /// Blockhash expiry, RPC outages and every other submission failure that is not a
        /// program rejection pass through untouched.
        /// </summary>
        [Test]
        public void NonProgramFailureKeepsRawMessage()
        {
            const string raw = "Could not submit the transaction on Devnet: Transaction simulation failed: Blockhash not found";

            var text = SolanaSubmissionErrorDescriber.Describe(raw, MarketplaceCatalog(), [IdlFixtures.MarketplaceAddress]);

            Assert.That(text, Is.EqualTo(raw));
        }
    }
}
