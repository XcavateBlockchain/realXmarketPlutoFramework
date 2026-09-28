using PlutoFrameworkCore.Solana;

namespace PlutoFrameworkTests
{
    public class SolanaCustomProgramErrorParserTests
    {
        /// <summary>
        /// The exact wording a node returns when preflight simulation rejects an Anchor
        /// program call, wrapped in the app's own prefix.
        /// </summary>
        [Test]
        public void ParsesHexCustomError()
        {
            var parsed = SolanaCustomProgramErrorParser.TryParse(
                "Could not submit the transaction on Devnet: Transaction simulation failed: "
                + "Error processing Instruction 0: custom program error: 0x177d",
                out var error);

            Assert.That(parsed, Is.True);
            Assert.That(error!.InstructionIndex, Is.EqualTo(0));
            Assert.That(error.Code, Is.EqualTo(6013));
        }

        /// <summary>
        /// getSignatureStatuses-style reasons print the same code in decimal; both forms
        /// must land on the same number.
        /// </summary>
        [Test]
        public void ParsesDecimalCustomError()
        {
            var parsed = SolanaCustomProgramErrorParser.TryParse(
                "Error processing Instruction 2: custom program error: 6013",
                out var error);

            Assert.That(parsed, Is.True);
            Assert.That(error!.InstructionIndex, Is.EqualTo(2));
            Assert.That(error.Code, Is.EqualTo(6013));
        }

        /// <summary>
        /// The decoded message keeps the node's own words as technical detail, so the parse
        /// must also report exactly which slice of the input they occupy.
        /// </summary>
        [Test]
        public void ReportsTheMatchedFragment()
        {
            var reason = "Transaction simulation failed: Error processing Instruction 0: custom program error: 0x177d";

            SolanaCustomProgramErrorParser.TryParse(reason, out var error);

            Assert.That(reason.Substring(error!.MatchStart, error.MatchLength),
                Is.EqualTo("Error processing Instruction 0: custom program error: 0x177d"));
        }

        [Test]
        public void NonCustomErrorReasonsDoNotParse()
        {
            Assert.That(SolanaCustomProgramErrorParser.TryParse(
                "Transaction simulation failed: Blockhash not found", out var error), Is.False);
            Assert.That(error, Is.Null);
        }

        [Test]
        public void GarbageDoesNotParse()
        {
            Assert.That(SolanaCustomProgramErrorParser.TryParse(
                "custom program error: 0xZZZ", out _), Is.False);
            Assert.That(SolanaCustomProgramErrorParser.TryParse("no reason given", out _), Is.False);
        }

        [Test]
        public void NullOrEmptyDoesNotParse()
        {
            Assert.That(SolanaCustomProgramErrorParser.TryParse(null, out _), Is.False);
            Assert.That(SolanaCustomProgramErrorParser.TryParse(string.Empty, out _), Is.False);
        }
    }
}
