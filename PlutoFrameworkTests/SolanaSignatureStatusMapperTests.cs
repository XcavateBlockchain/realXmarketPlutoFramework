using PlutoFrameworkCore.Solana;
using Solnet.Rpc.Models;

namespace PlutoFrameworkTests
{
    public class SolanaSignatureStatusMapperTests
    {
        private static SignatureStatusInfo Status(string? confirmationStatus, TransactionError? error = null) =>
            new() { ConfirmationStatus = confirmationStatus!, Error = error! };

        private static TransactionError AnyError() =>
            new() { Type = TransactionErrorType.InstructionError };

        /// <summary>
        /// The single most important case. getSignatureStatuses returns a null entry for a
        /// signature the node has not seen yet, which is every transaction for its first
        /// moments. Reading that as a failure paints a red Failed toast over a healthy send.
        /// </summary>
        [Test]
        public void UnknownSignatureIsPendingNotFailed()
        {
            Assert.That(SolanaSignatureStatusMapper.Map(null), Is.EqualTo(SolanaTransactionStatus.Pending));
        }

        /// <summary>
        /// "processed" means it landed in a block but has not been voted on. It is folded
        /// into Pending so the toast vocabulary matches the Substrate stack.
        /// </summary>
        [Test]
        public void ProcessedIsPending()
        {
            Assert.That(SolanaSignatureStatusMapper.Map(Status("processed")),
                Is.EqualTo(SolanaTransactionStatus.Pending));
        }

        [Test]
        public void ConfirmedWithoutErrorIsSuccess()
        {
            Assert.That(SolanaSignatureStatusMapper.Map(Status("confirmed")),
                Is.EqualTo(SolanaTransactionStatus.ConfirmedSuccess));
        }

        [Test]
        public void ConfirmedWithErrorIsFailure()
        {
            Assert.That(SolanaSignatureStatusMapper.Map(Status("confirmed", AnyError())),
                Is.EqualTo(SolanaTransactionStatus.ConfirmedFailed));
        }

        [Test]
        public void FinalizedWithoutErrorIsSuccess()
        {
            Assert.That(SolanaSignatureStatusMapper.Map(Status("finalized")),
                Is.EqualTo(SolanaTransactionStatus.FinalizedSuccess));
        }

        /// <summary>
        /// A transaction can be finalized and still have failed. Reading finality alone as
        /// success would tell the user their transfer went through when it did not.
        /// </summary>
        [Test]
        public void FinalizedWithErrorIsFailure()
        {
            Assert.That(SolanaSignatureStatusMapper.Map(Status("finalized", AnyError())),
                Is.EqualTo(SolanaTransactionStatus.FinalizedFailed));
        }

        /// <summary>
        /// A status string this client does not recognise is not evidence of failure. Node
        /// implementations may add levels; guessing "failed" would be a lie.
        /// </summary>
        [TestCase("something-new")]
        [TestCase("")]
        [TestCase(null)]
        public void UnrecognisedConfirmationStatusIsPending(string? confirmationStatus)
        {
            Assert.That(SolanaSignatureStatusMapper.Map(Status(confirmationStatus)),
                Is.EqualTo(SolanaTransactionStatus.Pending));
        }

        /// <summary>
        /// An error reported before the confirmation level is known still means failure —
        /// the node would not attach an error to a transaction that succeeded.
        /// </summary>
        [Test]
        public void ErrorWithoutConfirmationStatusIsFailure()
        {
            Assert.That(SolanaSignatureStatusMapper.Map(Status(null, AnyError())),
                Is.EqualTo(SolanaTransactionStatus.ConfirmedFailed));
        }

        [Test]
        public void ConfirmationStatusIsCaseInsensitive()
        {
            Assert.That(SolanaSignatureStatusMapper.Map(Status("FINALIZED")),
                Is.EqualTo(SolanaTransactionStatus.FinalizedSuccess));
        }
    }
}
