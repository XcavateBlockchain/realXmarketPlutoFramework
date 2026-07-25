using PlutoFrameworkCore.Solana;

namespace PlutoFrameworkTests
{
    public class SolanaBalanceAssemblerTests
    {
        private const string UsdcMint = "EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v";
        private const string OtherMint = "4zMMC9srt5Ri5X14GAgXhaHii3GnPAEERYPJgZJDncDU";

        private static SolanaTokenWhitelistEntry Usdc() => new()
        {
            Cluster = SolanaCluster.Mainnet,
            Mint = UsdcMint,
            Symbol = "USDC",
            Decimals = 6,
            PinnedUsdPrice = 1.00,
        };

        private static SolanaTokenAccountAmount Account(string mint, string rawAmount, int decimals) => new()
        {
            Mint = mint,
            RawAmount = rawAmount,
            Decimals = decimals,
        };

        [Test]
        public void SolIsAlwaysFirstAndAlwaysPresent()
        {
            var rows = SolanaBalanceAssembler.Assemble(
                lamports: 2_500_000_000UL,
                tokenAccounts: [],
                whitelist: [],
                usdPrices: new Dictionary<string, double>());

            Assert.Multiple(() =>
            {
                Assert.That(rows, Has.Count.EqualTo(1));
                Assert.That(rows[0].Symbol, Is.EqualTo("SOL"));
                Assert.That(rows[0].IsNative, Is.True);
                Assert.That(rows[0].Amount, Is.EqualTo(2.5m));
            });
        }

        /// <summary>
        /// The page lists the tokens the app deals in. A token vanishing when its balance
        /// hits zero reads as a bug, and hides the account the user is about to fund.
        /// </summary>
        [Test]
        public void WhitelistedMintWithNoAccountAppearsAtZero()
        {
            var rows = SolanaBalanceAssembler.Assemble(
                lamports: 0UL,
                tokenAccounts: [],
                whitelist: [Usdc()],
                usdPrices: new Dictionary<string, double>());

            var usdc = rows.Single(row => row.Symbol == "USDC");

            Assert.Multiple(() =>
            {
                Assert.That(usdc.Amount, Is.EqualTo(0m));
                Assert.That(usdc.Decimals, Is.EqualTo(6));
            });
        }

        /// <summary>
        /// A wallet can hold more than one token account for the same mint. Taking the first
        /// would under-report the balance by however much sits in the others.
        /// </summary>
        [Test]
        public void SeveralAccountsForOneMintAreSummed()
        {
            var rows = SolanaBalanceAssembler.Assemble(
                lamports: 0UL,
                tokenAccounts:
                [
                    Account(UsdcMint, "40000000", 6),
                    Account(UsdcMint, "2500000", 6),
                ],
                whitelist: [Usdc()],
                usdPrices: new Dictionary<string, double>());

            Assert.That(rows.Single(row => row.Symbol == "USDC").Amount, Is.EqualTo(42.5m));
        }

        [Test]
        public void AccountsForUnlistedMintsAreIgnored()
        {
            var rows = SolanaBalanceAssembler.Assemble(
                lamports: 0UL,
                tokenAccounts: [Account(OtherMint, "999000000", 6)],
                whitelist: [Usdc()],
                usdPrices: new Dictionary<string, double>());

            Assert.Multiple(() =>
            {
                Assert.That(rows.Select(row => row.Mint), Does.Not.Contain(OtherMint));
                Assert.That(rows.Single(row => row.Symbol == "USDC").Amount, Is.EqualTo(0m));
            });
        }

        [Test]
        public void UsdValueIsAmountTimesPrice()
        {
            var rows = SolanaBalanceAssembler.Assemble(
                lamports: 2_000_000_000UL,
                tokenAccounts: [Account(UsdcMint, "40000000", 6)],
                whitelist: [Usdc()],
                usdPrices: new Dictionary<string, double>
                {
                    [SolanaNativeToken.Mint] = 74.0,
                    [UsdcMint] = 1.0,
                });

            Assert.Multiple(() =>
            {
                Assert.That(rows.Single(row => row.IsNative).UsdValue, Is.EqualTo(148.0).Within(0.0001));
                Assert.That(rows.Single(row => row.Symbol == "USDC").UsdValue, Is.EqualTo(40.0).Within(0.0001));
            });
        }

        /// <summary>
        /// A missing price is unknown, not zero. Rendering it as $0.00 tells the user their
        /// money is gone.
        /// </summary>
        [Test]
        public void MissingPriceLeavesUsdValueNull()
        {
            var rows = SolanaBalanceAssembler.Assemble(
                lamports: 1_000_000_000UL,
                tokenAccounts: [],
                whitelist: [Usdc()],
                usdPrices: new Dictionary<string, double>());

            Assert.Multiple(() =>
            {
                Assert.That(rows.Single(row => row.IsNative).UsdValue, Is.Null);
                Assert.That(rows.Single(row => row.Symbol == "USDC").UsdValue, Is.Null);
            });
        }

        [Test]
        public void TotalSumsOnlyPricedRows()
        {
            var rows = SolanaBalanceAssembler.Assemble(
                lamports: 1_000_000_000UL,
                tokenAccounts: [Account(UsdcMint, "40000000", 6)],
                whitelist: [Usdc()],
                usdPrices: new Dictionary<string, double> { [UsdcMint] = 1.0 });

            Assert.That(SolanaBalanceAssembler.TotalUsd(rows), Is.EqualTo(40.0).Within(0.0001));
        }

        [Test]
        public void RowsFollowWhitelistOrderAfterSol()
        {
            var second = Usdc() with { Mint = OtherMint, Symbol = "TEST" };

            var rows = SolanaBalanceAssembler.Assemble(
                lamports: 0UL,
                tokenAccounts: [],
                whitelist: [Usdc(), second],
                usdPrices: new Dictionary<string, double>());

            Assert.That(rows.Select(row => row.Symbol), Is.EqualTo(new[] { "SOL", "USDC", "TEST" }));
        }
    }
}
