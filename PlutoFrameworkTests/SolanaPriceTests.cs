using PlutoFrameworkCore.Solana;

namespace PlutoFrameworkTests
{
    public class SolanaPriceParserTests
    {
        /// <summary>The exact shape returned by lite-api.jup.ag/price/v3, captured live.</summary>
        private const string SampleResponse = """
        {"So11111111111111111111111111111111111111112":{"createdAt":"2024-06-05T08:55:25.527Z",
        "liquidity":649200458.3446863,"usdPrice":74.15443178403174,"blockId":435157282,
        "decimals":9,"priceChange24h":0.3258998223782334}}
        """;

        [Test]
        public void ReadsUsdPriceKeyedByMint()
        {
            var prices = SolanaPriceParser.Parse(SampleResponse);

            Assert.That(prices[SolanaNativeToken.Mint], Is.EqualTo(74.15443178403174).Within(0.0000001));
        }

        [Test]
        public void AbsentMintIsAbsentFromTheResult()
        {
            var prices = SolanaPriceParser.Parse("""{"SomeOtherMint":{"usdPrice":3.0}}""");

            Assert.That(prices.ContainsKey(SolanaNativeToken.Mint), Is.False);
        }

        [Test]
        public void EntryWithoutUsdPriceIsSkipped()
        {
            var prices = SolanaPriceParser.Parse("""{"MintA":{"liquidity":1.0},"MintB":{"usdPrice":2.0}}""");

            Assert.Multiple(() =>
            {
                Assert.That(prices.ContainsKey("MintA"), Is.False);
                Assert.That(prices["MintB"], Is.EqualTo(2.0));
            });
        }

        /// <summary>
        /// A malformed body is a feed problem. It must degrade to "no prices", never throw
        /// into the page's load path.
        /// </summary>
        [Test]
        public void MalformedJsonYieldsNoPrices()
        {
            Assert.Multiple(() =>
            {
                Assert.That(SolanaPriceParser.Parse("not json"), Is.Empty);
                Assert.That(SolanaPriceParser.Parse(""), Is.Empty);
                Assert.That(SolanaPriceParser.Parse("[1,2,3]"), Is.Empty);
            });
        }
    }

    public class SolanaPriceModelTests
    {
        private const string UsdcMint = "EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v";
        private const string UnpinnedMint = "4zMMC9srt5Ri5X14GAgXhaHii3GnPAEERYPJgZJDncDU";

        private static SolanaTokenWhitelistEntry Pinned() => new()
        {
            Cluster = SolanaCluster.Mainnet,
            Mint = UsdcMint,
            Symbol = "USDC",
            Decimals = 6,
            PinnedUsdPrice = 1.00,
        };

        private static SolanaTokenWhitelistEntry Unpinned() => new()
        {
            Cluster = SolanaCluster.Mainnet,
            Mint = UnpinnedMint,
            Symbol = "TEST",
            Decimals = 6,
        };

        /// <summary>
        /// SOL always needs a live price. A pinned stablecoin never reaches the network, so
        /// a feed outage cannot reprice it.
        /// </summary>
        [Test]
        public void OnlyUnpinnedMintsAndSolNeedTheNetwork()
        {
            var mints = SolanaPriceModel.MintsNeedingLivePrice([Pinned(), Unpinned()]);

            Assert.That(mints, Is.EquivalentTo(new[] { SolanaNativeToken.Mint, UnpinnedMint }));
        }

        [Test]
        public void PinnedPriceWinsOverTheFeed()
        {
            var resolved = SolanaPriceModel.ResolvePrices(
                [Pinned()],
                new Dictionary<string, double> { [UsdcMint] = 0.87 });

            Assert.That(resolved[UsdcMint], Is.EqualTo(1.00));
        }

        [Test]
        public void LivePricesFillUnpinnedMints()
        {
            var resolved = SolanaPriceModel.ResolvePrices(
                [Pinned(), Unpinned()],
                new Dictionary<string, double>
                {
                    [UnpinnedMint] = 3.5,
                    [SolanaNativeToken.Mint] = 74.0,
                });

            Assert.Multiple(() =>
            {
                Assert.That(resolved[UsdcMint], Is.EqualTo(1.00));
                Assert.That(resolved[UnpinnedMint], Is.EqualTo(3.5));
                Assert.That(resolved[SolanaNativeToken.Mint], Is.EqualTo(74.0));
            });
        }

        /// <summary>
        /// A dead feed still leaves pinned prices usable, and leaves everything else unpriced
        /// rather than priced at zero.
        /// </summary>
        [Test]
        public void NoLivePricesStillResolvesPinnedOnes()
        {
            var resolved = SolanaPriceModel.ResolvePrices(
                [Pinned(), Unpinned()],
                new Dictionary<string, double>());

            Assert.Multiple(() =>
            {
                Assert.That(resolved[UsdcMint], Is.EqualTo(1.00));
                Assert.That(resolved.ContainsKey(UnpinnedMint), Is.False);
                Assert.That(resolved.ContainsKey(SolanaNativeToken.Mint), Is.False);
            });
        }
    }
}
