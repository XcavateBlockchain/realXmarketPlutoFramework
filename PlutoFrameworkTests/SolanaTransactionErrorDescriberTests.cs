using PlutoFrameworkCore.Solana;
using Solnet.Rpc.Models;

namespace PlutoFrameworkTests
{
    public class SolanaTransactionErrorDescriberTests
    {
        /// <summary>
        /// A failure with no attached reason still has to say something. The error page
        /// would otherwise show a blank reason under a red status.
        /// </summary>
        [Test]
        public void NullErrorIsNotSilent()
        {
            var text = SolanaTransactionErrorDescriber.Describe(null);

            Assert.That(text, Is.Not.Empty);
        }

        /// <summary>
        /// The failure a transfer produces when it cannot pay its own fee. The user needs
        /// to hear "more SOL", not an enum name.
        /// </summary>
        [Test]
        public void InsufficientFundsForFeeIsPlainWords()
        {
            var text = SolanaTransactionErrorDescriber.Describe(
                new TransactionError() { Type = TransactionErrorType.InsufficientFundsForFee });

            Assert.That(text, Does.Contain("SOL"));
            Assert.That(text, Does.Contain("fee"));
            Assert.That(text, Does.Not.Contain("InsufficientFundsForFee"));
        }

        [Test]
        public void InsufficientFundsForRentIsPlainWords()
        {
            var text = SolanaTransactionErrorDescriber.Describe(
                new TransactionError() { Type = TransactionErrorType.InsufficientFundsForRent });

            Assert.That(text, Does.Contain("rent"));
            Assert.That(text, Does.Not.Contain("InsufficientFundsForRent"));
        }

        [Test]
        public void BlockhashNotFoundIsPlainWords()
        {
            var text = SolanaTransactionErrorDescriber.Describe(
                new TransactionError() { Type = TransactionErrorType.BlockhashNotFound });

            Assert.That(text, Does.Contain("blockhash"));
            Assert.That(text, Does.Not.Contain("BlockhashNotFound"));
        }

        /// <summary>
        /// The common on-chain failure: a program rejected one instruction. The index and
        /// the program error are the only facts that let the user or the explorer match
        /// the failure to a program, so both must be carried.
        /// </summary>
        [Test]
        public void InstructionErrorCarriesIndexAndInnerType()
        {
            var error = new TransactionError()
            {
                Type = TransactionErrorType.InstructionError,
                InstructionError = new InstructionError()
                {
                    InstructionIndex = 1,
                    Type = InstructionErrorType.InsufficientFunds,
                },
            };

            var text = SolanaTransactionErrorDescriber.Describe(error);

            Assert.That(text, Does.Contain("1"));
            Assert.That(text, Does.Contain("Insufficient funds"));
            Assert.That(text, Does.Not.Contain("InstructionError"));
        }

        /// <summary>
        /// SPL program errors carry a numeric code that Solscan shows too. Omitting it
        /// would leave the two accounts of the same failure disagreeing.
        /// </summary>
        [Test]
        public void InstructionErrorCarriesCustomCodeWhenPresent()
        {
            var error = new TransactionError()
            {
                Type = TransactionErrorType.InstructionError,
                InstructionError = new InstructionError()
                {
                    InstructionIndex = 0,
                    Type = InstructionErrorType.Custom,
                    CustomError = 3013,
                },
            };

            var text = SolanaTransactionErrorDescriber.Describe(error);

            Assert.That(text, Does.Contain("3013"));
        }

        [Test]
        public void InstructionErrorWithoutCustomCodeDoesNotInventOne()
        {
            var error = new TransactionError()
            {
                Type = TransactionErrorType.InstructionError,
                InstructionError = new InstructionError()
                {
                    InstructionIndex = 0,
                    Type = InstructionErrorType.InvalidArgument,
                },
            };

            var text = SolanaTransactionErrorDescriber.Describe(error);

            Assert.That(text, Does.Not.Contain("code"));
        }

        /// <summary>
        /// An error code this client has no special words for still reads: the enum name is
        /// split into words rather than surfaced as CamelCase or dropped.
        /// </summary>
        [Test]
        public void UnmappedErrorFallsBackToSplitEnumName()
        {
            var text = SolanaTransactionErrorDescriber.Describe(
                new TransactionError() { Type = TransactionErrorType.AccountNotFound });

            Assert.That(text, Does.Contain("Account not found"));
        }
    }
}
