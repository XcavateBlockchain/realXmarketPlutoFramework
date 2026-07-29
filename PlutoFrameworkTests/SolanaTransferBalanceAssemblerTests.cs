using System.Numerics;
using PlutoFrameworkCore.Solana;
using Solnet.Programs;
using Solnet.Wallet;

namespace PlutoFrameworkTests
{
    public class SolanaTransferBalanceAssemblerTests
    {
        private static PublicKey Key(byte seed) => new(Enumerable.Repeat(seed, 32).ToArray());

        private static readonly PublicKey Owner = Key(1);
        private static readonly string Usdc = Key(2).Key;
        private static readonly string Unlisted = Key(3).Key;

        private static string OwnerAta(string mint, string? programId = null) =>
            SolanaAssociatedTokenAccount
                .Derive(Owner, new PublicKey(mint),
                    new PublicKey(programId ?? SolanaTokenProgram.Legacy))
                .Key;

        private static List<SolanaTokenWhitelistEntry> Whitelist(string? programId = null) =>
        [
            new()
            {
                Cluster = SolanaCluster.Mainnet,
                Mint = Usdc,
                Symbol = "USDC",
                Decimals = 6,
                ProgramId = programId ?? SolanaTokenProgram.Legacy,
            },
        ];

        private static SolanaTokenAccountAmount Account(string address, string mint, string raw, int decimals = 6) =>
            new() { Address = address, Mint = mint, RawAmount = raw, Decimals = decimals };

        [Test]
        public void SolIsFirstAndComesFromLamports()
        {
            var rows = SolanaTransferBalanceAssembler.Assemble(
                2_500_000_000UL, [], Whitelist(), Owner.Key);

            Assert.Multiple(() =>
            {
                Assert.That(rows[0].Symbol, Is.EqualTo(SolanaNativeToken.Symbol));
                Assert.That(rows[0].IsNative, Is.True);
                Assert.That(rows[0].SpendableBaseUnits, Is.EqualTo(new BigInteger(2_500_000_000)));
                Assert.That(rows[0].Decimals, Is.EqualTo(9));
            });
        }

        /// <summary>
        /// The picker lists what the app deals in, so a token the user holds none of still
        /// appears — at zero, not omitted.
        /// </summary>
        [Test]
        public void EveryWhitelistedTokenAppearsEvenAtZero()
        {
            var rows = SolanaTransferBalanceAssembler.Assemble(0UL, [], Whitelist(), Owner.Key);

            Assert.Multiple(() =>
            {
                Assert.That(rows, Has.Count.EqualTo(2));
                Assert.That(rows[1].Symbol, Is.EqualTo("USDC"));
                Assert.That(rows[1].SpendableBaseUnits, Is.EqualTo(BigInteger.Zero));
                Assert.That(rows[1].IsNative, Is.False);
            });
        }

        [Test]
        public void SpendableComesFromTheAssociatedAccount()
        {
            var rows = SolanaTransferBalanceAssembler.Assemble(
                0UL, [Account(OwnerAta(Usdc), Usdc, "40000000")], Whitelist(), Owner.Key);

            Assert.That(rows[1].SpendableBaseUnits, Is.EqualTo(new BigInteger(40_000_000)));
        }

        /// <summary>
        /// The balances page sums every account for a mint. A transfer spends from one, so
        /// the picker must not offer the sum — Max would fill an amount that cannot send.
        /// </summary>
        [Test]
        public void SpendableIgnoresAccountsOtherThanTheAssociatedOne()
        {
            var rows = SolanaTransferBalanceAssembler.Assemble(
                0UL,
                [
                    Account(OwnerAta(Usdc), Usdc, "25000000"),
                    Account(Key(9).Key, Usdc, "15000000"),
                ],
                Whitelist(),
                Owner.Key);

            Assert.That(rows[1].SpendableBaseUnits, Is.EqualTo(new BigInteger(25_000_000)),
                "the sum would be 40000000, which the transfer cannot spend");
        }

        /// <summary>
        /// A mint held only outside the associated account reports zero, because that is what
        /// a transfer can move. Reporting the held amount would promise a send that fails.
        /// </summary>
        [Test]
        public void AMintHeldOnlyElsewhereIsNotSpendable()
        {
            var rows = SolanaTransferBalanceAssembler.Assemble(
                0UL, [Account(Key(9).Key, Usdc, "15000000")], Whitelist(), Owner.Key);

            Assert.That(rows[1].SpendableBaseUnits, Is.EqualTo(BigInteger.Zero));
        }

        [Test]
        public void UnlistedMintsAreIgnored()
        {
            var rows = SolanaTransferBalanceAssembler.Assemble(
                0UL,
                [Account(OwnerAta(Unlisted), Unlisted, "99000000")],
                Whitelist(),
                Owner.Key);

            Assert.Multiple(() =>
            {
                Assert.That(rows, Has.Count.EqualTo(2));
                Assert.That(rows[1].SpendableBaseUnits, Is.EqualTo(BigInteger.Zero));
            });
        }

        /// <summary>
        /// The planner derives accounts from these fields, so a row carrying the wrong token
        /// program would build a transfer against the wrong program.
        /// </summary>
        [Test]
        public void RowsCarryTheWhitelistedProgramIdAndDecimals()
        {
            var rows = SolanaTransferBalanceAssembler.Assemble(
                0UL, [], Whitelist(SolanaTokenProgram.Token2022), Owner.Key);

            Assert.Multiple(() =>
            {
                Assert.That(rows[1].ProgramId, Is.EqualTo(SolanaTokenProgram.Token2022));
                Assert.That(rows[1].Decimals, Is.EqualTo(6));
                Assert.That(rows[0].ProgramId, Is.EqualTo(SolanaTokenProgram.Legacy));
            });
        }

        /// <summary>
        /// A Token-2022 mint's associated account derives from its own program, so a legacy
        /// account address must not satisfy it.
        /// </summary>
        [Test]
        public void Token2022SpendableUsesTheToken2022DerivedAccount()
        {
            var whitelist = Whitelist(SolanaTokenProgram.Token2022);

            var rows = SolanaTransferBalanceAssembler.Assemble(
                0UL,
                [
                    Account(OwnerAta(Usdc), Usdc, "11000000"),
                    Account(OwnerAta(Usdc, SolanaTokenProgram.Token2022), Usdc, "22000000"),
                ],
                whitelist,
                Owner.Key);

            Assert.That(rows[1].SpendableBaseUnits, Is.EqualTo(new BigInteger(22_000_000)));
        }

        /// <summary>
        /// An account with no address cannot be matched against a derived one. Treating it as
        /// a match would let an arbitrary account stand in for the associated one.
        /// </summary>
        [Test]
        public void AccountsWithoutAnAddressAreIgnored()
        {
            var rows = SolanaTransferBalanceAssembler.Assemble(
                0UL,
                [new SolanaTokenAccountAmount { Mint = Usdc, RawAmount = "40000000", Decimals = 6 }],
                Whitelist(),
                Owner.Key);

            Assert.That(rows[1].SpendableBaseUnits, Is.EqualTo(BigInteger.Zero));
        }
    }
}
