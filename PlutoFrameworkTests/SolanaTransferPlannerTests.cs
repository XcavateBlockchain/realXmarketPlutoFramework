using System.Numerics;
using PlutoFrameworkCore.Solana;
using Solnet.Programs;
using Solnet.Wallet;

namespace PlutoFrameworkTests
{
    public class SolanaTransferPlannerTests
    {
        /// <summary>
        /// Obviously synthetic but structurally valid keys — 32 bytes of one repeated value.
        /// Using real mints as stand-in wallets would read as though the test meant something
        /// by the choice.
        /// </summary>
        private static string Address(byte seed) =>
            SolanaBase58.Encode(Enumerable.Repeat(seed, 32).ToArray());

        private static readonly string Sender = Address(1);
        private static readonly string Recipient = Address(2);
        private static readonly string Mint = Address(3);

        private static SolanaTransferBalance Sol() => new()
        {
            Symbol = "SOL",
            Mint = SolanaNativeToken.Mint,
            Decimals = SolanaNativeToken.Decimals,
            ProgramId = SolanaTokenProgram.Legacy,
            IsNative = true,
            SpendableBaseUnits = new BigInteger(5_000_000_000),
        };

        private static SolanaTransferBalance Spl(string? programId = null) => new()
        {
            Symbol = "USDC",
            Mint = Mint,
            Decimals = 6,
            ProgramId = programId ?? SolanaTokenProgram.Legacy,
            IsNative = false,
            SpendableBaseUnits = new BigInteger(40_000_000),
        };

        private static string ProgramIdOf(Solnet.Rpc.Models.TransactionInstruction instruction) =>
            new PublicKey(instruction.ProgramId).Key;

        [Test]
        public void SolPlanIsASingleSystemTransfer()
        {
            var plan = SolanaTransferPlanner.Build(
                Sender, Recipient, Sol(), new BigInteger(1_000_000), recipientAccountExists: true);

            Assert.Multiple(() =>
            {
                Assert.That(plan.Instructions, Has.Count.EqualTo(1));
                Assert.That(ProgramIdOf(plan.Instructions[0]), Is.EqualTo(SystemProgram.ProgramIdKey.Key));
                Assert.That(plan.CreatesRecipientAccount, Is.False);
            });
        }

        /// <summary>
        /// A SOL transfer touches no token account, so the recipient's SPL account state is
        /// irrelevant. Creating one here would charge the sender rent for nothing.
        /// </summary>
        [Test]
        public void SolPlanNeverCreatesATokenAccount()
        {
            var plan = SolanaTransferPlanner.Build(
                Sender, Recipient, Sol(), new BigInteger(1_000_000), recipientAccountExists: false);

            Assert.Multiple(() =>
            {
                Assert.That(plan.Instructions, Has.Count.EqualTo(1));
                Assert.That(plan.CreatesRecipientAccount, Is.False);
            });
        }

        [Test]
        public void SplPlanWithAnExistingAccountIsASingleTransferChecked()
        {
            var plan = SolanaTransferPlanner.Build(
                Sender, Recipient, Spl(), new BigInteger(1_000_000), recipientAccountExists: true);

            Assert.Multiple(() =>
            {
                Assert.That(plan.Instructions, Has.Count.EqualTo(1));
                Assert.That(ProgramIdOf(plan.Instructions[0]), Is.EqualTo(TokenProgram.ProgramIdKey.Key));
                Assert.That(plan.CreatesRecipientAccount, Is.False);
            });
        }

        /// <summary>
        /// Order matters: transferring into an account that does not exist yet fails, so the
        /// create must come first.
        /// </summary>
        [Test]
        public void SplPlanWithNoAccountCreatesItBeforeTransferring()
        {
            var plan = SolanaTransferPlanner.Build(
                Sender, Recipient, Spl(), new BigInteger(1_000_000), recipientAccountExists: false);

            Assert.Multiple(() =>
            {
                Assert.That(plan.Instructions, Has.Count.EqualTo(2));
                Assert.That(ProgramIdOf(plan.Instructions[0]),
                    Is.EqualTo(AssociatedTokenAccountProgram.ProgramIdKey.Key));
                Assert.That(ProgramIdOf(plan.Instructions[1]), Is.EqualTo(TokenProgram.ProgramIdKey.Key));
                Assert.That(plan.CreatesRecipientAccount, Is.True);
            });
        }

        /// <summary>
        /// The sender pays the rent for an account they do not own. That is a real cost, so
        /// the flag that records it must be true exactly when the instruction is present.
        /// </summary>
        [Test]
        public void TheSenderIsThePayerForACreatedAccount()
        {
            var plan = SolanaTransferPlanner.Build(
                Sender, Recipient, Spl(), new BigInteger(1_000_000), recipientAccountExists: false);

            var create = plan.Instructions[0];

            Assert.Multiple(() =>
            {
                Assert.That(create.Keys[0].PublicKey, Is.EqualTo(Sender));
                Assert.That(create.Keys[0].IsSigner, Is.True);
                // The owner of the new account is the recipient, not the payer.
                Assert.That(create.Keys[2].PublicKey, Is.EqualTo(Recipient));
            });
        }

        /// <summary>
        /// The classic SPL mistake: sending to the wallet address instead of its associated
        /// token account. Funds sent that way are unrecoverable.
        /// </summary>
        [Test]
        public void SplTransferUsesAssociatedAccountsNotWalletAddresses()
        {
            var plan = SolanaTransferPlanner.Build(
                Sender, Recipient, Spl(), new BigInteger(1_000_000), recipientAccountExists: true);

            var transfer = plan.Instructions[0];
            var source = transfer.Keys[0].PublicKey;
            var destination = transfer.Keys[2].PublicKey;

            Assert.Multiple(() =>
            {
                Assert.That(source, Is.Not.EqualTo(Sender));
                Assert.That(destination, Is.Not.EqualTo(Recipient));
                Assert.That(source, Is.EqualTo(AssociatedTokenAccountProgram
                    .DeriveAssociatedTokenAccount(new PublicKey(Sender), new PublicKey(Mint)).Key));
                Assert.That(destination, Is.EqualTo(AssociatedTokenAccountProgram
                    .DeriveAssociatedTokenAccount(new PublicKey(Recipient), new PublicKey(Mint)).Key));
            });
        }

        /// <summary>
        /// The authority signing the transfer is the sender's wallet, not their token account.
        /// </summary>
        [Test]
        public void TheSenderSignsAsAuthority()
        {
            var plan = SolanaTransferPlanner.Build(
                Sender, Recipient, Spl(), new BigInteger(1_000_000), recipientAccountExists: true);

            var authority = plan.Instructions[0].Keys[3];

            Assert.Multiple(() =>
            {
                Assert.That(authority.PublicKey, Is.EqualTo(Sender));
                Assert.That(authority.IsSigner, Is.True);
            });
        }

        /// <summary>
        /// TransferChecked carries the mint and decimals so the chain rejects a decimals
        /// mistake. Plain Transfer does not, and would silently send 1000x on the same bug.
        /// </summary>
        [Test]
        public void SplTransferCarriesTheMintAndDecimals()
        {
            var plan = SolanaTransferPlanner.Build(
                Sender, Recipient, Spl(), new BigInteger(1_000_000), recipientAccountExists: true);

            var transfer = plan.Instructions[0];

            Assert.Multiple(() =>
            {
                Assert.That(transfer.Keys[1].PublicKey, Is.EqualTo(Mint));
                // [opcode 12][u64 amount][u8 decimals]
                Assert.That(transfer.Data, Has.Length.EqualTo(10));
                Assert.That(transfer.Data[0], Is.EqualTo(12));
                Assert.That(transfer.Data[9], Is.EqualTo(6));
            });
        }

        /// <summary>
        /// The whitelist's ProgramId field exists so a Token-2022 mint is configuration
        /// rather than a code change. If it were ignored, a Token-2022 transfer would be
        /// built against the legacy program and fail — or derive the wrong account.
        /// </summary>
        [Test]
        public void Token2022ProgramIdReachesBothDerivationAndTransfer()
        {
            var legacy = SolanaTransferPlanner.Build(
                Sender, Recipient, Spl(), new BigInteger(1_000_000), recipientAccountExists: true);
            var token2022 = SolanaTransferPlanner.Build(
                Sender, Recipient, Spl(SolanaTokenProgram.Token2022), new BigInteger(1_000_000),
                recipientAccountExists: true);

            Assert.Multiple(() =>
            {
                Assert.That(ProgramIdOf(token2022.Instructions[0]), Is.EqualTo(SolanaTokenProgram.Token2022));
                // Different token program means a different derived account for the same mint.
                Assert.That(token2022.Instructions[0].Keys[2].PublicKey,
                    Is.Not.EqualTo(legacy.Instructions[0].Keys[2].PublicKey));
            });
        }

        [Test]
        public void Token2022CreateUsesTheToken2022Program()
        {
            var plan = SolanaTransferPlanner.Build(
                Sender, Recipient, Spl(SolanaTokenProgram.Token2022), new BigInteger(1_000_000),
                recipientAccountExists: false);

            // Key 5 of the create instruction is the token program the account belongs to.
            Assert.That(plan.Instructions[0].Keys[5].PublicKey, Is.EqualTo(SolanaTokenProgram.Token2022));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void RejectsNonPositiveAmounts(int amount)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SolanaTransferPlanner.Build(
                Sender, Recipient, Sol(), new BigInteger(amount), recipientAccountExists: true));
        }

        /// <summary>
        /// The popup blocks this, but the planner is the last place that can. An amount above
        /// the balance builds a transaction that is certain to fail.
        /// </summary>
        [Test]
        public void RejectsAmountAboveTheSpendableBalance()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SolanaTransferPlanner.Build(
                Sender, Recipient, Spl(), new BigInteger(40_000_001), recipientAccountExists: true));
        }

        [Test]
        public void RejectsAnInvalidRecipient()
        {
            Assert.Throws<ArgumentException>(() => SolanaTransferPlanner.Build(
                Sender, "not-an-address", Sol(), new BigInteger(1), recipientAccountExists: true));
        }
    }
}
