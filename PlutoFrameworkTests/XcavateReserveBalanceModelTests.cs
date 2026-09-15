using PlutoFramework.Model.Xcavate;
using PlutoFrameworkCore;
using PlutoFrameworkCore.Solana;

namespace PlutoFrameworkTests
{
    public class XcavateReserveBalanceModelTests
    {
        private const string TgBpMint = "71G3dc4B9p9QBosLx3XhWY3ULRPAxjopngsin66M9HUb";

        [SetUp]
        public void SetUp()
        {
            PlutoConfigurationModel.WhitelistedSolanaTokens =
            [
                new SolanaTokenWhitelistEntry
                {
                    Cluster = SolanaCluster.Devnet,
                    Mint = TgBpMint,
                    Symbol = "tGBP",
                    Decimals = 9,
                },
            ];
        }

        [TearDown]
        public void TearDown() => PlutoConfigurationModel.WhitelistedSolanaTokens = [];

        [Test]
        public void CanAfford_WhenBalanceExactlyCoversReservedPlusCost() =>
            Assert.That(XcavateReserveBalanceModel.CanAfford(70m, 30m, 40m), Is.True);

        [Test]
        public void CanAfford_WhenBalanceExceedsReservedPlusCost() =>
            Assert.That(XcavateReserveBalanceModel.CanAfford(100m, 30m, 40m), Is.True);

        /// <summary>
        /// The core rule: the balance minus the already reserved amount must not drop
        /// below zero once the new reservation is subtracted too.
        /// </summary>
        [Test]
        public void CannotAfford_WhenReservedPlusCostExceedsBalanceByOneStep() =>
            Assert.That(XcavateReserveBalanceModel.CanAfford(69.9999m, 30m, 40m), Is.False);

        [Test]
        public void CannotAfford_WhenNothingHeldAndNothingReservedButCostIsPositive() =>
            Assert.That(XcavateReserveBalanceModel.CanAfford(0m, 0m, 0.01m), Is.False);

        [Test]
        public void CanAfford_WhenEverythingIsZero() =>
            Assert.That(XcavateReserveBalanceModel.CanAfford(0m, 0m, 0m), Is.True);

        [Test]
        public void TotalCostAddsOnePercentFeeOnTopOfTheFunds()
        {
            Assert.Multiple(() =>
            {
                Assert.That(XcavateReserveBalanceModel.ComputeReservationTotalCost(10, 50m), Is.EqualTo(505m));
                Assert.That(XcavateReserveBalanceModel.ComputeReservationTotalCost(1, 100m), Is.EqualTo(101m));
            });
        }

        [Test]
        public void InsufficientBalanceMessageNamesTheBalanceTheReservedValueAndTheCost()
        {
            var message = XcavateReserveBalanceModel.InsufficientBalanceMessage(50m, 30m, 40m);

            Assert.Multiple(() =>
            {
                Assert.That(message, Does.Contain("50.0000"));
                Assert.That(message, Does.Contain("30.0000"));
                Assert.That(message, Does.Contain("40.0000"));
            });
        }

        [Test]
        public void FindTgBpEntry_MatchesTheSymbolCaseInsensitively()
        {
            PlutoConfigurationModel.WhitelistedSolanaTokens =
            [
                new SolanaTokenWhitelistEntry
                {
                    Cluster = SolanaCluster.Devnet,
                    Mint = TgBpMint,
                    Symbol = "TGBP",
                    Decimals = 9,
                },
            ];

            Assert.That(XcavateReserveBalanceModel.FindTgBpEntry(SolanaCluster.Devnet), Is.Not.Null);
        }

        [Test]
        public void FindTgBpEntry_IsNullWhenTgBpIsNotConfigured()
        {
            PlutoConfigurationModel.WhitelistedSolanaTokens =
            [
                new SolanaTokenWhitelistEntry
                {
                    Cluster = SolanaCluster.Devnet,
                    Mint = "4zMMC9srt5Ri5X14GAgXhaHii3GnPAEERYPJgZJDncDU",
                    Symbol = "tUSDC",
                    Decimals = 6,
                },
            ];

            Assert.That(XcavateReserveBalanceModel.FindTgBpEntry(SolanaCluster.Devnet), Is.Null);
        }
    }
}
