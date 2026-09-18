using PlutoFramework.Model.Xcavate;
using PlutoFrameworkCore;
using PlutoFrameworkCore.Solana;
using UniqueryPlus.Metadata;

namespace PlutoFrameworkTests
{
    public class XcavateReserveBalanceModelTests
    {
        private const string TgBpMint = "71G3dc4B9p9QBosLx3XhWY3ULRPAxjopngsin66M9HUb";
        private const string UsdcMint = "4zMMC9srt5Ri5X14GAgXhaHii3GnPAEERYPJgZJDncDU";
        private const string UsdcSymbol = "tUSDC";

        private static readonly SolanaTokenWhitelistEntry TgBpEntry = new()
        {
            Cluster = SolanaCluster.Devnet,
            Mint = TgBpMint,
            Symbol = XcavateReserveBalanceModel.TgBpSymbol,
            Decimals = 9,
        };

        private static readonly SolanaTokenWhitelistEntry UsdcEntry = new()
        {
            Cluster = SolanaCluster.Devnet,
            Mint = UsdcMint,
            Symbol = UsdcSymbol,
            Decimals = 6,
        };

        [SetUp]
        public void SetUp() => PlutoConfigurationModel.WhitelistedSolanaTokens = [TgBpEntry];

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
        public void InsufficientBalanceMessageNamesTheSymbolTheBalanceTheReservedValueAndTheCost()
        {
            var message = XcavateReserveBalanceModel.InsufficientBalanceMessage(
                XcavateReserveBalanceModel.TgBpSymbol, 50m, 30m, 40m);

            Assert.Multiple(() =>
            {
                Assert.That(message, Does.Contain("50.0000 tGBP"));
                Assert.That(message, Does.Contain("30.0000 tGBP"));
                Assert.That(message, Does.Contain("40.0000 tGBP"));
            });
        }

        [Test]
        public void InsufficientBalanceMessageSpeaksInWhateverPaymentTokenItIsGiven()
        {
            var message = XcavateReserveBalanceModel.InsufficientBalanceMessage(UsdcSymbol, 50m, 30m, 40m);

            Assert.That(message, Does.Contain("tUSDC").And.Not.Contain("tGBP"));
        }

        [Test]
        public void FindPaymentTokenEntry_MatchesTheSymbolCaseInsensitively()
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

            Assert.That(
                XcavateReserveBalanceModel.FindPaymentTokenEntry(
                    SolanaCluster.Devnet, XcavateReserveBalanceModel.TgBpSymbol),
                Is.Not.Null);
        }

        [Test]
        public void FindPaymentTokenEntry_FindsAnyConfiguredSymbolNotJustTgBp()
        {
            PlutoConfigurationModel.WhitelistedSolanaTokens = [UsdcEntry];

            Assert.Multiple(() =>
            {
                Assert.That(
                    XcavateReserveBalanceModel.FindPaymentTokenEntry(SolanaCluster.Devnet, UsdcSymbol),
                    Is.Not.Null);
                Assert.That(XcavateReserveBalanceModel.FindTgBpEntry(SolanaCluster.Devnet), Is.Null);
            });
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

        private static SolanaTokenBalance TgBpRow(decimal amount, double? usd, bool netted = false) =>
            Row(XcavateReserveBalanceModel.TgBpSymbol, TgBpMint, amount, usd, netted);

        private static SolanaTokenBalance SolRow() =>
            Row("SOL", "So11111111111111111111111111111111111111112", 10m, 740.0);

        [Test]
        public void NetReservedValue_NetsThePaymentTokenRowAndLeavesTheOtherRowsAlone()
        {
            var solRow = SolRow();
            var tGbpRow = TgBpRow(50m, 125.0);

            var netted = XcavateReserveBalanceModel.NetReservedValue([solRow, tGbpRow], TgBpEntry, 20m);

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
            var netted = XcavateReserveBalanceModel.NetReservedValue([TgBpRow(10m, 10.0)], TgBpEntry, 30m);

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
            var netted = XcavateReserveBalanceModel.NetReservedValue([TgBpRow(100m, null)], TgBpEntry, 30m);

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
            var netted = XcavateReserveBalanceModel.NetReservedValue([TgBpRow(70m, 70.0, netted: true)], TgBpEntry, 30m);

            Assert.That(netted, Is.Null);
        }

        [Test]
        public void NetReservedValue_ReturnsNullWhenNothingIsReserved()
        {
            var netted = XcavateReserveBalanceModel.NetReservedValue([TgBpRow(50m, 125.0)], TgBpEntry, 0m);

            Assert.That(netted, Is.Null);
        }

        [Test]
        public void NetReservedValue_ReturnsNullWhenTheListHasNoRowForTheMint()
        {
            var netted = XcavateReserveBalanceModel.NetReservedValue([SolRow()], TgBpEntry, 20m);

            Assert.That(netted, Is.Null);
        }

        /// <summary>
        /// The multi-token netting each balances view runs: every payment token nets its
        /// own row, in its own currency, and nothing else moves.
        /// </summary>
        [Test]
        public void NetReservedValues_NetsEachPaymentTokenOutOfItsOwnRow()
        {
            PlutoConfigurationModel.WhitelistedSolanaTokens = [TgBpEntry, UsdcEntry];

            var rows = new[]
            {
                SolRow(),
                TgBpRow(50m, 125.0),
                Row(UsdcSymbol, UsdcMint, 40m, 40.0),
            };

            var netted = XcavateReserveBalanceModel.NetReservedValues(
                rows,
                SolanaCluster.Devnet,
                new Dictionary<string, decimal>
                {
                    [XcavateReserveBalanceModel.TgBpSymbol] = 20m,
                    [UsdcSymbol] = 10m,
                });

            Assert.Multiple(() =>
            {
                Assert.That(netted, Is.Not.Null);
                Assert.That(netted![0], Is.SameAs(rows[0]));

                // tGBP: 50 - 20 at 2.5 USD each.
                Assert.That(netted[1].Amount, Is.EqualTo(30m));
                Assert.That(netted[1].UsdValue, Is.EqualTo(75.0));
                Assert.That(netted[1].IsAmountNetted, Is.True);

                // tUSDC: 40 - 10 at 1 USD each.
                Assert.That(netted[2].Amount, Is.EqualTo(30m));
                Assert.That(netted[2].UsdValue, Is.EqualTo(30.0));
                Assert.That(netted[2].IsAmountNetted, Is.True);
            });
        }

        /// <summary>
        /// A reserved value in a token the cluster has no whitelist entry for must not be
        /// netted against any row: subtracting USDC reservations from the tGBP row would
        /// mix currencies.
        /// </summary>
        [Test]
        public void NetReservedValues_SkipsASymbolWithNoWhitelistEntryOnTheCluster()
        {
            var netted = XcavateReserveBalanceModel.NetReservedValues(
                [TgBpRow(50m, 125.0)],
                SolanaCluster.Devnet,
                new Dictionary<string, decimal> { [UsdcSymbol] = 10m });

            Assert.That(netted, Is.Null);
        }

        [Test]
        public void NetReservedValues_ReturnsNullWhenNothingIsReserved()
        {
            var netted = XcavateReserveBalanceModel.NetReservedValues(
                [TgBpRow(50m, 125.0)],
                SolanaCluster.Devnet,
                new Dictionary<string, decimal>());

            Assert.That(netted, Is.Null);
        }

        [Test]
        public void NetReservedValues_DoesNotNetARowThatIsAlreadyNetted()
        {
            var netted = XcavateReserveBalanceModel.NetReservedValues(
                [TgBpRow(70m, 70.0, netted: true)],
                SolanaCluster.Devnet,
                new Dictionary<string, decimal> { [XcavateReserveBalanceModel.TgBpSymbol] = 30m });

            Assert.That(netted, Is.Null);
        }

        /// <summary>
        /// Builds a position the way the indexer does, with only the record's required
        /// properties set. A null price simulates a listing whose offchain metadata has not
        /// been attached yet: it contributes nothing rather than throwing.
        /// </summary>
        private static XcavateSolanaInvestorProperty Position(
            uint bought,
            uint reserved,
            decimal? pricePerToken,
            string paymentToken = XcavateReserveBalanceModel.TgBpSymbol)
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
                PaymentTokenSymbol = paymentToken,
            };
        }

        [Test]
        public void ComputeReservedValues_SumsReservedSharesTimesPricePerListing()
        {
            var positions = new[]
            {
                Position(2, 3, 100m),  // reserved 3 × 100 = 300, bought 2 not counted
                Position(0, 4, 50m),   // reserved 4 × 50  = 200
            };

            var values = XcavateReserveBalanceModel.ComputeReservedValues(positions);

            Assert.That(
                XcavateReserveBalanceModel.ValueFor(values, XcavateReserveBalanceModel.TgBpSymbol),
                Is.EqualTo(500m));
        }

        /// <summary>
        /// The bug behind this suite: reserved value on one listing must net against the
        /// balance alongside every other listing's reserved value, never on its own.
        /// </summary>
        [Test]
        public void ComputeReservedValues_AccumulatesAcrossListingsIntoOneWalletWideFigure()
        {
            var positions = new[]
            {
                Position(0, 3, 100m),  // the listing being viewed: 300 reserved
                Position(0, 4, 50m),   // another listing's reservation: 200
            };

            var values = XcavateReserveBalanceModel.ComputeReservedValues(positions);

            Assert.That(
                XcavateReserveBalanceModel.ValueFor(values, XcavateReserveBalanceModel.TgBpSymbol),
                Is.EqualTo(500m));
        }

        [Test]
        public void ComputeReservedValues_CountsReservedSharesEvenWhenNothingIsBought()
        {
            var values = XcavateReserveBalanceModel.ComputeReservedValues([Position(0, 3, 20m)]);

            Assert.That(
                XcavateReserveBalanceModel.ValueFor(values, XcavateReserveBalanceModel.TgBpSymbol),
                Is.EqualTo(60m));
        }

        [Test]
        public void ComputeReservedValues_TreatsMissingMetadataAsZeroPrice()
        {
            var positions = new[]
            {
                Position(1, 3, null),   // no metadata: contributes nothing
                Position(2, 2, 10m),    // reserved 2 × 10 = 20, bought 2 not counted
            };

            var values = XcavateReserveBalanceModel.ComputeReservedValues(positions);

            Assert.That(
                XcavateReserveBalanceModel.ValueFor(values, XcavateReserveBalanceModel.TgBpSymbol),
                Is.EqualTo(20m));
        }

        /// <summary>
        /// Bought shares are already paid for - the money has left the wallet - so they
        /// hold no payment token against the balance and must not be netted away from it.
        /// </summary>
        [Test]
        public void ComputeReservedValues_IgnoresBoughtShares()
        {
            var values = XcavateReserveBalanceModel.ComputeReservedValues([Position(11, 0, 100m)]);

            Assert.That(
                XcavateReserveBalanceModel.ValueFor(values, XcavateReserveBalanceModel.TgBpSymbol),
                Is.EqualTo(0m));
        }

        [Test]
        public void ComputeReservedValues_IsEmptyForAnEmptyPortfolio()
        {
            var values = XcavateReserveBalanceModel.ComputeReservedValues([]);

            Assert.Multiple(() =>
            {
                Assert.That(values, Is.Empty);
                Assert.That(
                    XcavateReserveBalanceModel.ValueFor(values, XcavateReserveBalanceModel.TgBpSymbol),
                    Is.EqualTo(0m));
            });
        }

        /// <summary>
        /// Each payment token gets its own bucket: a listing sold for USDC must never add
        /// to the tGBP reserved figure, or the tGBP balance would be netted by a value
        /// that is not tGBP.
        /// </summary>
        [Test]
        public void ComputeReservedValues_GroupsEachPaymentTokenIntoItsOwnBucket()
        {
            var positions = new[]
            {
                Position(0, 3, 100m),                              // 300 tGBP
                Position(0, 4, 50m, paymentToken: UsdcSymbol),     // 200 tUSDC
                Position(0, 1, 25m),                               // 25 tGBP
            };

            var values = XcavateReserveBalanceModel.ComputeReservedValues(positions);

            Assert.Multiple(() =>
            {
                Assert.That(values.Count, Is.EqualTo(2));
                Assert.That(
                    XcavateReserveBalanceModel.ValueFor(values, XcavateReserveBalanceModel.TgBpSymbol),
                    Is.EqualTo(325m));
                Assert.That(XcavateReserveBalanceModel.ValueFor(values, UsdcSymbol), Is.EqualTo(200m));
            });
        }

        [Test]
        public void ComputeTotalAssetValues_SumsBoughtPlusReservedSharesTimesPricePerListing()
        {
            var positions = new[]
            {
                Position(2, 3, 100m),  // committed 5 × 100 = 500
                Position(0, 4, 50m),   // committed 4 × 50  = 200
            };

            var values = XcavateReserveBalanceModel.ComputeTotalAssetValues(positions);

            Assert.That(
                XcavateReserveBalanceModel.ValueFor(values, XcavateReserveBalanceModel.TgBpSymbol),
                Is.EqualTo(700m));
        }

        [Test]
        public void ComputeTotalAssetValues_CountsReservedSharesEvenWhenNothingIsBought()
        {
            var values = XcavateReserveBalanceModel.ComputeTotalAssetValues([Position(0, 3, 20m)]);

            Assert.That(
                XcavateReserveBalanceModel.ValueFor(values, XcavateReserveBalanceModel.TgBpSymbol),
                Is.EqualTo(60m));
        }

        /// <summary>
        /// Unlike <see cref="XcavateReserveBalanceModel.ComputeReservedValues"/>, bought
        /// shares count here too: they are paid-for assets the investor still holds.
        /// </summary>
        [Test]
        public void ComputeTotalAssetValues_CountsBoughtSharesAsAssets()
        {
            var values = XcavateReserveBalanceModel.ComputeTotalAssetValues([Position(11, 0, 100m)]);

            Assert.That(
                XcavateReserveBalanceModel.ValueFor(values, XcavateReserveBalanceModel.TgBpSymbol),
                Is.EqualTo(1100m));
        }

        [Test]
        public void ComputeTotalAssetValues_TreatsMissingMetadataAsZeroPrice()
        {
            var positions = new[]
            {
                Position(1, 3, null),   // no metadata: contributes nothing
                Position(2, 2, 10m),    // committed 4 × 10 = 40
            };

            var values = XcavateReserveBalanceModel.ComputeTotalAssetValues(positions);

            Assert.That(
                XcavateReserveBalanceModel.ValueFor(values, XcavateReserveBalanceModel.TgBpSymbol),
                Is.EqualTo(40m));
        }

        [Test]
        public void ComputeTotalAssetValues_IsEmptyForAnEmptyPortfolio() =>
            Assert.That(XcavateReserveBalanceModel.ComputeTotalAssetValues([]), Is.Empty);

        [Test]
        public void ComputeTotalAssetValues_GroupsEachPaymentTokenIntoItsOwnBucket()
        {
            var positions = new[]
            {
                Position(1, 0, 100m),                          // 100 tGBP
                Position(0, 2, 50m, paymentToken: UsdcSymbol), // 100 tUSDC
            };

            var values = XcavateReserveBalanceModel.ComputeTotalAssetValues(positions);

            Assert.Multiple(() =>
            {
                Assert.That(
                    XcavateReserveBalanceModel.ValueFor(values, XcavateReserveBalanceModel.TgBpSymbol),
                    Is.EqualTo(100m));
                Assert.That(XcavateReserveBalanceModel.ValueFor(values, UsdcSymbol), Is.EqualTo(100m));
            });
        }

        [Test]
        public void ReservedPositions_KeepsOnlyPositionsWithReservedSharesInTheGivenToken()
        {
            var reserved = Position(0, 3, 100m);
            var reservedOtherToken = Position(0, 4, 50m, paymentToken: UsdcSymbol);
            var boughtOnly = Position(2, 0, 100m);

            var positions = XcavateReserveBalanceModel.ReservedPositions(
                [reserved, reservedOtherToken, boughtOnly], XcavateReserveBalanceModel.TgBpSymbol);

            Assert.That(positions, Is.EqualTo(new[] { reserved }));
        }

        [Test]
        public void ReservedPositions_MatchesThePaymentTokenCaseInsensitively()
        {
            var position = Position(0, 3, 100m, paymentToken: "TGBP");

            var positions = XcavateReserveBalanceModel.ReservedPositions(
                [position], XcavateReserveBalanceModel.TgBpSymbol);

            Assert.That(positions, Has.Count.EqualTo(1));
        }

        [Test]
        public void ReservedPositions_IsEmptyWhenNothingIsReserved() =>
            Assert.That(
                XcavateReserveBalanceModel.ReservedPositions(
                    [Position(11, 0, 100m)], XcavateReserveBalanceModel.TgBpSymbol),
                Is.Empty);

        [Test]
        public void ComputePositionReservedValue_IsReservedSharesTimesPricePerToken() =>
            Assert.That(
                XcavateReserveBalanceModel.ComputePositionReservedValue(Position(2, 3, 100m)),
                Is.EqualTo(300m));

        [Test]
        public void ComputePositionReservedValue_TreatsMissingMetadataAsZeroPrice() =>
            Assert.That(
                XcavateReserveBalanceModel.ComputePositionReservedValue(Position(0, 3, null)),
                Is.EqualTo(0m));

        /// <summary>
        /// The section's total must equal the sum of its rows - the detail page builds both
        /// from the same query, and a drift between them would read as money gone missing.
        /// </summary>
        [Test]
        public void ReservedPositions_RowValuesSumToTheReservedBucket()
        {
            var positions = new[]
            {
                Position(0, 3, 100m),
                Position(2, 4, 50m),
                Position(0, 1, 25m, paymentToken: UsdcSymbol),  // another token's section
                Position(5, 0, 10m),                            // nothing reserved
            };

            var rows = XcavateReserveBalanceModel.ReservedPositions(
                positions, XcavateReserveBalanceModel.TgBpSymbol);

            var rowSum = rows.Sum(XcavateReserveBalanceModel.ComputePositionReservedValue);
            var bucket = XcavateReserveBalanceModel.ValueFor(
                XcavateReserveBalanceModel.ComputeReservedValues(positions),
                XcavateReserveBalanceModel.TgBpSymbol);

            Assert.That(rowSum, Is.EqualTo(bucket));
        }
    }
}
