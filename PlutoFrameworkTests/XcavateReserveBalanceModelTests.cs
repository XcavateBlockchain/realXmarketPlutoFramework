using PlutoFramework.Model.Xcavate;
using PlutoFrameworkCore;
using PlutoFrameworkCore.Solana;
using UniqueryPlus.Metadata;

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

        /// <summary>
        /// Builds a balances-page row with only the record's required properties set.
        /// </summary>
        private static SolanaTokenBalance Row(string symbol, string mint, decimal amount, double? usd, bool netted = false) =>
            new()
            {
                Symbol = symbol,
                Mint = mint,
                Amount = amount,
                Decimals = 9,
                IsNative = false,
                ShowPriceChart = false,
                UsdValue = usd,
                IsAmountNetted = netted,
            };

        [Test]
        public void NetReservedValue_NetsTheTgBpRowAndLeavesTheOtherRowsAlone()
        {
            var solRow = Row("SOL", "So11111111111111111111111111111111111111112", 10m, 740.0);
            var tGbpRow = Row("tGBP", TgBpMint, 50m, 125.0);

            var netted = XcavateReserveBalanceModel.NetReservedValue([solRow, tGbpRow], SolanaCluster.Devnet, 20m);

            Assert.Multiple(() =>
            {
                Assert.That(netted, Is.Not.Null);
                // The SOL row is the very same instance - no copy, no change.
                Assert.That(netted![0], Is.SameAs(solRow));

                var nettedTgBp = netted[1];

                // 50 - 20 reserved, repriced at 2.5 USD per tGBP: 30 × 2.5 = 75.
                Assert.That(nettedTgBp.Amount, Is.EqualTo(30m));
                Assert.That(nettedTgBp.UsdValue, Is.EqualTo(75.0));
                Assert.That(nettedTgBp.IsAmountNetted, Is.True);
            });
        }

        [Test]
        public void NetReservedValue_ClampsTheAmountAtZeroWhenTheReservationExceedsTheBalance()
        {
            var netted = XcavateReserveBalanceModel.NetReservedValue(
                [Row("tGBP", TgBpMint, 10m, 10.0)], SolanaCluster.Devnet, 30m);

            Assert.Multiple(() =>
            {
                Assert.That(netted, Is.Not.Null);
                Assert.That(netted![0].Amount, Is.EqualTo(0m));
                Assert.That(netted[0].UsdValue, Is.EqualTo(0.0));
                Assert.That(netted[0].IsAmountNetted, Is.True);
            });
        }

        [Test]
        public void NetReservedValue_LeavesTheUsdValueNullOnAnUnpricedRow()
        {
            var netted = XcavateReserveBalanceModel.NetReservedValue(
                [Row("tGBP", TgBpMint, 100m, null)], SolanaCluster.Devnet, 30m);

            Assert.Multiple(() =>
            {
                Assert.That(netted, Is.Not.Null);
                Assert.That(netted![0].Amount, Is.EqualTo(70m));
                Assert.That(netted[0].UsdValue, Is.Null);
                Assert.That(netted[0].IsAmountNetted, Is.True);
            });
        }

        /// <summary>
        /// A row that arrived already netted must pass through untouched - netting it again
        /// would subtract the reserved value twice.
        /// </summary>
        [Test]
        public void NetReservedValue_DoesNotNetARowThatIsAlreadyNetted()
        {
            var netted = XcavateReserveBalanceModel.NetReservedValue(
                [Row("tGBP", TgBpMint, 70m, 70.0, netted: true)], SolanaCluster.Devnet, 30m);

            Assert.That(netted, Is.Null);
        }

        [Test]
        public void NetReservedValue_ReturnsNullWhenNothingIsReserved()
        {
            var netted = XcavateReserveBalanceModel.NetReservedValue(
                [Row("tGBP", TgBpMint, 50m, 125.0)], SolanaCluster.Devnet, 0m);

            Assert.That(netted, Is.Null);
        }

        [Test]
        public void NetReservedValue_ReturnsNullWhenTheListHasNoTgBpRow()
        {
            var netted = XcavateReserveBalanceModel.NetReservedValue(
                [Row("SOL", "So11111111111111111111111111111111111111112", 10m, 740.0)], SolanaCluster.Devnet, 20m);

            Assert.That(netted, Is.Null);
        }

        [Test]
        public void NetReservedValue_ReturnsNullWhenTgBpIsNotConfiguredForTheCluster()
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

            var netted = XcavateReserveBalanceModel.NetReservedValue(
                [Row("tUSDC", "4zMMC9srt5Ri5X14GAgXhaHii3GnPAEERYPJgZJDncDU", 50m, 50.0)], SolanaCluster.Devnet, 20m);

            Assert.That(netted, Is.Null);
        }

        /// <summary>
        /// Builds a position the way the indexer does, with only the record's required
        /// properties set. A null price simulates a listing whose offchain metadata has not
        /// been attached yet: it contributes nothing rather than throwing.
        /// </summary>
        private static XcavateSolanaInvestorProperty Position(uint bought, uint reserved, decimal? pricePerToken)
        {
            XcavateSolanaListingNft listing = new()
            {
                Owner = "owner",
                ListingId = 1,
                AssetId = 1,
                ListingExpiryTimestamp = 0,
                ClaimDeadlineTimestamp = 0,
                ListingStatus = "Listed",
                OpenForSale = true,
                IsTornDown = false,
                XcavateMetadata = pricePerToken is null
                    ? null
                    : new PropertyMetadata
                    {
                        Financials = new PropertyFinancials { PricePerToken = pricePerToken.Value },
                        Address = new PropertyAddress(),
                    },
            };

            return new XcavateSolanaInvestorProperty
            {
                Listing = listing,
                BoughtShares = bought,
                ReservedShares = reserved,
            };
        }

        [Test]
        public void ComputeReservedValue_SumsReservedSharesTimesPricePerListing()
        {
            var positions = new[]
            {
                Position(2, 3, 100m),  // reserved 3 × 100 = 300, bought 2 not counted
                Position(0, 4, 50m),   // reserved 4 × 50  = 200
            };

            Assert.That(XcavateReserveBalanceModel.ComputeReservedValue(positions), Is.EqualTo(500m));
        }

        [Test]
        public void ComputeReservedValue_CountsReservedSharesEvenWhenNothingIsBought()
        {
            var position = Position(0, 3, 20m);

            Assert.That(XcavateReserveBalanceModel.ComputeReservedValue([position]), Is.EqualTo(60m));
        }

        [Test]
        public void ComputeReservedValue_TreatsMissingMetadataAsZeroPrice()
        {
            var positions = new[]
            {
                Position(1, 3, null),   // no metadata: contributes nothing
                Position(2, 2, 10m),    // reserved 2 × 10 = 20, bought 2 not counted
            };

            Assert.That(XcavateReserveBalanceModel.ComputeReservedValue(positions), Is.EqualTo(20m));
        }

        /// <summary>
        /// Bought shares are already paid for - the money has left the wallet - so they
        /// hold no tGBP against the balance and must not be netted away from it.
        /// </summary>
        [Test]
        public void ComputeReservedValue_IgnoresBoughtShares()
        {
            var position = Position(11, 0, 100m);

            Assert.That(XcavateReserveBalanceModel.ComputeReservedValue([position]), Is.EqualTo(0m));
        }

        [Test]
        public void ComputeReservedValue_IsZeroForAnEmptyPortfolio() =>
            Assert.That(XcavateReserveBalanceModel.ComputeReservedValue([]), Is.EqualTo(0m));

        [Test]
        public void ComputeTotalAssetValue_SumsBoughtPlusReservedSharesTimesPricePerListing()
        {
            var positions = new[]
            {
                Position(2, 3, 100m),  // committed 5 × 100 = 500
                Position(0, 4, 50m),   // committed 4 × 50  = 200
            };

            Assert.That(XcavateReserveBalanceModel.ComputeTotalAssetValue(positions), Is.EqualTo(700m));
        }

        [Test]
        public void ComputeTotalAssetValue_CountsReservedSharesEvenWhenNothingIsBought()
        {
            var position = Position(0, 3, 20m);

            Assert.That(XcavateReserveBalanceModel.ComputeTotalAssetValue([position]), Is.EqualTo(60m));
        }

        /// <summary>
        /// Unlike <see cref="XcavateReserveBalanceModel.ComputeReservedValue"/>, bought
        /// shares count here too: they are paid-for assets the investor still holds.
        /// </summary>
        [Test]
        public void ComputeTotalAssetValue_CountsBoughtSharesAsAssets()
        {
            var position = Position(11, 0, 100m);

            Assert.That(XcavateReserveBalanceModel.ComputeTotalAssetValue([position]), Is.EqualTo(1100m));
        }

        [Test]
        public void ComputeTotalAssetValue_TreatsMissingMetadataAsZeroPrice()
        {
            var positions = new[]
            {
                Position(1, 3, null),   // no metadata: contributes nothing
                Position(2, 2, 10m),    // committed 4 × 10 = 40
            };

            Assert.That(XcavateReserveBalanceModel.ComputeTotalAssetValue(positions), Is.EqualTo(40m));
        }

        [Test]
        public void ComputeTotalAssetValue_IsZeroForAnEmptyPortfolio() =>
            Assert.That(XcavateReserveBalanceModel.ComputeTotalAssetValue([]), Is.EqualTo(0m));

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
