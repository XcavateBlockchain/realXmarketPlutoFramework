using PlutoFrameworkCore.Solana;
using Solnet.Programs;
using Solnet.Wallet;

namespace PlutoFrameworkTests
{
    public class SolanaAssociatedTokenAccountTests
    {
        private static PublicKey Key(byte seed) =>
            new(Enumerable.Repeat(seed, 32).ToArray());

        private static readonly PublicKey Owner = Key(1);
        private static readonly PublicKey Mint = Key(2);

        /// <summary>
        /// The cross-check that makes the rest of this class trustworthy. Solnet ships a
        /// legacy-only derivation; ours takes the token program as an argument. For the
        /// legacy program the two must agree, which validates our seed order and program id
        /// against an independent implementation rather than against itself.
        /// </summary>
        [Test]
        public void LegacyDerivationMatchesSolnetsOwn()
        {
            Assert.That(
                SolanaAssociatedTokenAccount.Derive(Owner, Mint, TokenProgram.ProgramIdKey).Key,
                Is.EqualTo(AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(Owner, Mint).Key));
        }

        /// <summary>
        /// The whole reason this helper exists: the published Solana.Programs 8.7.0 hardcodes
        /// the legacy token program, so a Token-2022 mint would derive the wrong account.
        /// </summary>
        [Test]
        public void TokenProgramChangesTheDerivedAccount()
        {
            var legacy = SolanaAssociatedTokenAccount.Derive(Owner, Mint, TokenProgram.ProgramIdKey);
            var token2022 = SolanaAssociatedTokenAccount.Derive(
                Owner, Mint, new PublicKey(SolanaTokenProgram.Token2022));

            Assert.That(token2022.Key, Is.Not.EqualTo(legacy.Key));
        }

        [Test]
        public void DerivationIsDeterministic()
        {
            Assert.That(
                SolanaAssociatedTokenAccount.Derive(Owner, Mint, TokenProgram.ProgramIdKey).Key,
                Is.EqualTo(SolanaAssociatedTokenAccount.Derive(Owner, Mint, TokenProgram.ProgramIdKey).Key));
        }

        /// <summary>
        /// An associated account belongs to the owner but is not the owner. Returning the
        /// wallet address would send tokens somewhere unrecoverable.
        /// </summary>
        [Test]
        public void DerivedAccountIsNotTheOwner()
        {
            Assert.That(
                SolanaAssociatedTokenAccount.Derive(Owner, Mint, TokenProgram.ProgramIdKey).Key,
                Is.Not.EqualTo(Owner.Key));
        }

        /// <summary>
        /// Matches the layout Solnet's own create instruction uses, with the token program
        /// taken from the argument rather than assumed.
        /// </summary>
        [Test]
        public void CreateInstructionMatchesSolnetsLegacyLayout()
        {
            var payer = Key(3);

            var ours = SolanaAssociatedTokenAccount.CreateInstruction(
                payer, Owner, Mint, TokenProgram.ProgramIdKey);
            var solnet = AssociatedTokenAccountProgram.CreateAssociatedTokenAccount(payer, Owner, Mint);

            Assert.Multiple(() =>
            {
                Assert.That(new PublicKey(ours.ProgramId).Key,
                    Is.EqualTo(new PublicKey(solnet.ProgramId).Key));
                Assert.That(ours.Data, Is.EqualTo(solnet.Data));
                Assert.That(ours.Keys.Select(key => key.PublicKey),
                    Is.EqualTo(solnet.Keys.Select(key => key.PublicKey)));
                Assert.That(ours.Keys.Select(key => key.IsSigner),
                    Is.EqualTo(solnet.Keys.Select(key => key.IsSigner)));
                Assert.That(ours.Keys.Select(key => key.IsWritable),
                    Is.EqualTo(solnet.Keys.Select(key => key.IsWritable)));
            });
        }
    }
}
