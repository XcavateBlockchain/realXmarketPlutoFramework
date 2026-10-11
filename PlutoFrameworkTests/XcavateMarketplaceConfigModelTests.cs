using PlutoFramework.Model.Xcavate;
using PlutoFrameworkCore.Solana;
using Solnet.Wallet;

namespace PlutoFrameworkTests
{
    /// <summary>
    /// The parser tests are pure, against a real devnet capture of the Config account and
    /// a synthetic ShareListing built field by field from the IDL layout (no ShareListing
    /// exists on devnet yet to capture). One live test confirms the RPC fetch path parses
    /// the config as deployed.
    /// </summary>
    internal class XcavateMarketplaceConfigModelTests
    {
        private static readonly XcavateProgramSet Programs = XcavateProgramAddresses.Devnet;

        // The devnet marketplace Config account at FvypPqraGoYskTM9jiUywfsdfTeoqkXg2RdPHuw5S7aK,
        // captured 2026-10-09 via getAccountInfo (base64) against https://api.devnet.solana.com.
        private const string DevnetConfigBase64 =
            "mwyq4B76zIJh7erC34qDL6eAq+aqWunNgGpk+m658OSgFL8TtPNs7wBxW6o9VpqiM+5Ozky/u7OS/O/0nT3zCnJZFTzbZAD+YGLLfKNPxIwzLGYMZkGT+qLNkjtcOjFNd590zNKCm0xXGnivCLb4e8rbwYWCd8Pdue7owsJsS8oqM2tYXnkU+g8CAAAAWTciJtsF9zw+SeMM17zMMoJVNkMAZBxN2FuROIhmQ+B1hsu+nRYmTaBiljTh2PZy6Jeu4yD5c3pOFLykCSNLWwDodkgXAAAAAOh2SBcAAAACAAAAZAAAAGQAZAAQJwgHAAAAAAAAgFEBAAAAAABYAgAAAAAAAOgDDQAAAAAAAAAAAAAAAAAAAP8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

        private static PublicKey SyntheticKey(byte seed) =>
            new(SolanaBase58.Encode([.. Enumerable.Repeat(seed, 32)]));

        [Test]
        public void Config_ParsesTheCapturedDevnetAccount()
        {
            var config = XcavateMarketplaceConfigModel.Parse(Convert.FromBase64String(DevnetConfigBase64));

            Assert.Multiple(() =>
            {
                Assert.That(config.Authority.Key, Is.EqualTo("7bGxnDFi3zKLAbgeXtCANcf8MGSYob1EAmoWZY77qjp2"));
                Assert.That(config.PendingAuthority, Is.Null);
                Assert.That(config.XcavMint.Key, Is.EqualTo("8dW943dozaNPdRRaW6xpV2vxFv1Kcpz3z63Nji3VLups"));
                Assert.That(config.Treasury.Key, Is.EqualTo("7eevKBRGmLXmDNaHfRGMpYTVPQYCTtNXnPNuvvYZLGcv"));
                Assert.That(config.RentCollector.Key, Is.EqualTo("2nLLLhSQmJHWmj4zcfcmZPVmDmxzqJqYGKDS5shPLB8z"));

                // The vec mid-layout: a fixed-offset parse would read the count as a key.
                Assert.That(config.AcceptedPaymentMints.Select(mint => mint.Key), Is.EqualTo(
                    new[]
                    {
                        "71G3dc4B9p9QBosLx3XhWY3ULRPAxjopngsin66M9HUb",
                        "8umv4NXybZFGiT3tQb1DqJ6DXxLa3rLNhPbcqbQsjXxW",
                    }));

                Assert.That(config.ListingDeposit, Is.EqualTo(100_000_000_000UL));
                Assert.That(config.LawyerDeposit, Is.EqualTo(100_000_000_000UL));
                Assert.That(config.MinPropertyShares, Is.EqualTo(2u));
                Assert.That(config.MaxPropertyShares, Is.EqualTo(100u));
                Assert.That(config.MarketplaceFeeBps, Is.EqualTo(100));
                Assert.That(config.InvestorFeeBps, Is.EqualTo(100));
                Assert.That(config.MaxOwnershipBps, Is.EqualTo(10_000));
                Assert.That(config.ClaimingTime, Is.EqualTo(1_800L));
                Assert.That(config.LegalProcessTime, Is.EqualTo(86_400L));
                Assert.That(config.LawyerVotingTime, Is.EqualTo(600L));
                Assert.That(config.MinVotingQuorumBps, Is.EqualTo(1_000));
                Assert.That(config.NextListingId, Is.EqualTo(13UL));
                Assert.That(config.NextShareListingId, Is.EqualTo(0UL));
                Assert.That(config.Bump, Is.EqualTo(255));
            });
        }

        [Test]
        public void Config_RejectsAForeignDiscriminator()
        {
            var data = Convert.FromBase64String(DevnetConfigBase64);
            data[0] ^= 0xFF;

            Assert.Throws<InvalidOperationException>(() => XcavateMarketplaceConfigModel.Parse(data));
        }

        [Test]
        public void Config_RejectsTruncatedData()
        {
            var data = Convert.FromBase64String(DevnetConfigBase64)[..100];

            Assert.Throws<InvalidOperationException>(() => XcavateMarketplaceConfigModel.Parse(data));
        }

        /// <summary>
        /// A ShareListing account assembled field by field from the IDL layout - the
        /// mirror image of the parser, so any field-order or width drift breaks the round
        /// trip. (Devnet holds no real ShareListing to capture yet.)
        /// </summary>
        private static byte[] BuildShareListingAccount(
            ulong id,
            ulong assetId,
            PublicKey seller,
            ulong sharePrice,
            uint amount,
            ushort feeBps,
            ulong nextOfferNonce,
            PublicKey rentPayer,
            byte bump)
        {
            var data = new List<byte>
            {
                // The Anchor discriminator, from idls/devnet/marketplace.json.
                38, 219, 182, 80, 137, 29, 254, 143,
            };

            data.AddRange(BitConverter.GetBytes(id));
            data.AddRange(BitConverter.GetBytes(assetId));
            data.AddRange(seller.KeyBytes);
            data.AddRange(BitConverter.GetBytes(sharePrice));
            data.AddRange(BitConverter.GetBytes(amount));
            data.AddRange(BitConverter.GetBytes(feeBps));
            data.AddRange(BitConverter.GetBytes(nextOfferNonce));
            data.AddRange(rentPayer.KeyBytes);
            data.Add(bump);

            return [.. data];
        }

        [Test]
        public void ShareListing_ParsesTheIdlLayout()
        {
            var seller = SyntheticKey(1);
            var rentPayer = SyntheticKey(2);

            var data = BuildShareListingAccount(
                id: 0x0102030405060708,
                assetId: 42,
                seller,
                sharePrice: 10_400_000_000,
                amount: 25,
                feeBps: 100,
                nextOfferNonce: 7,
                rentPayer,
                bump: 254);

            var shareListing = XcavateShareListingModel.Parse(data);

            Assert.Multiple(() =>
            {
                Assert.That(shareListing.Id, Is.EqualTo(0x0102030405060708UL));
                Assert.That(shareListing.AssetId, Is.EqualTo(42UL));
                Assert.That(shareListing.Seller.Key, Is.EqualTo(seller.Key));
                Assert.That(shareListing.SharePrice, Is.EqualTo(10_400_000_000UL));
                Assert.That(shareListing.Amount, Is.EqualTo(25u));
                Assert.That(shareListing.FeeBps, Is.EqualTo(100));
                Assert.That(shareListing.NextOfferNonce, Is.EqualTo(7UL));
                Assert.That(shareListing.RentPayer.Key, Is.EqualTo(rentPayer.Key));
                Assert.That(shareListing.Bump, Is.EqualTo(254));
            });
        }

        [Test]
        public void ShareListing_RejectsAForeignDiscriminator()
        {
            var data = BuildShareListingAccount(1, 2, SyntheticKey(1), 1_000, 3, 100, 0, SyntheticKey(2), 255);
            data[0] ^= 0xFF;

            Assert.Throws<InvalidOperationException>(() => XcavateShareListingModel.Parse(data));
        }

        [Test]
        [TestCase(10_400_000_000u, 9, 10_400_000_000u)]
        // The 6-decimal accepted mint: the quote scales down by the decimal difference.
        [TestCase(10_400_000_000u, 6, 10_400_000u)]
        // Scaling down FLOORS, unlike the primary purchase's conservative ScaleToMintDecimals
        // (which would give 10_400_001 here); the caller adds its own headroom to the cap.
        [TestCase(10_400_000_001u, 6, 10_400_000u)]
        [TestCase(1u, 6, 0u)]
        // Scaling up to a higher-decimal mint multiplies.
        [TestCase(10_400_000u, 12, 10_400_000_000u)]
        public void ScaleQuoteToMint_ConvertsByDecimalCountWithFloorSemantics(ulong quote, int mintDecimals, ulong expected)
        {
            Assert.That(
                XcavateMarketplaceCallsModel.ScaleQuoteToMint(quote, mintDecimals),
                Is.EqualTo(expected));
        }

        [Test]
        public void ScaleQuoteToMint_RejectsAnOverflowingScaleUp()
        {
            Assert.Throws<OverflowException>(() =>
                XcavateMarketplaceCallsModel.ScaleQuoteToMint(ulong.MaxValue, 18));
        }

        [Test]
        [TestCase(10.4, 10_400_000_000UL)]
        // The smallest price the program accepts: 1000 quote units (0.000001).
        [TestCase(0.000001, 1_000UL)]
        [TestCase(1, 1_000_000_000UL)]
        [TestCase(123_456.789012345, 123_456_789_012_345UL)]
        public void QuoteFromDecimal_ScalesToNineDecimals(decimal price, ulong expected)
        {
            Assert.That(XcavateMarketplaceCallsModel.QuoteFromDecimal(price), Is.EqualTo(expected));
        }

        [Test]
        public void QuoteFromDecimal_RejectsNonPositivePrices()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => XcavateMarketplaceCallsModel.QuoteFromDecimal(0m));
            Assert.Throws<ArgumentOutOfRangeException>(() => XcavateMarketplaceCallsModel.QuoteFromDecimal(-5m));
        }

        [Test]
        public void QuoteFromDecimal_RejectsMoreThanNineDecimalPlaces()
        {
            // 1e-10 would truncate to zero quote units - a silently free listing.
            Assert.Throws<ArgumentOutOfRangeException>(() => XcavateMarketplaceCallsModel.QuoteFromDecimal(0.0000000001m));
            Assert.Throws<ArgumentOutOfRangeException>(() => XcavateMarketplaceCallsModel.QuoteFromDecimal(10.4000000005m));
        }

        [Test]
        public void QuoteFromDecimal_RejectsAnOverflowingPrice()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                XcavateMarketplaceCallsModel.QuoteFromDecimal(decimal.MaxValue));
        }

        [Test]
        public void RelistSharesAsync_RejectsBadArgsBeforeTouchingTheChain()
        {
            var validPrice = XcavateMarketplaceCallsModel.QuoteFromDecimal(10.4m);

            Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                XcavateMarketplaceCallsModel.RelistSharesAsync(
                    SyntheticKey(1).Key, SolanaCluster.Devnet, assetId: 1, amount: 0, validPrice));

            Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                XcavateMarketplaceCallsModel.RelistSharesAsync(
                    SyntheticKey(1).Key, SolanaCluster.Devnet, assetId: 1, amount: 5,
                    XcavateMarketplaceCallsModel.MinSharePriceQuote - 1));

            Assert.ThrowsAsync<OverflowException>(() =>
                XcavateMarketplaceCallsModel.RelistSharesAsync(
                    SyntheticKey(1).Key, SolanaCluster.Devnet, assetId: 1, amount: 2, ulong.MaxValue));
        }

        [Test]
        public void DelistAndRelistAsync_RejectsBadArgsBeforeTouchingTheChain()
        {
            Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                XcavateMarketplaceCallsModel.DelistAndRelistAsync(
                    SyntheticKey(1).Key, SolanaCluster.Devnet, shareListingId: 0, assetId: 1,
                    newSharePriceQuote: 500, newAmount: 5));
        }

        [Test]
        public async Task GetConfigAsync_FetchesAndParsesTheDevnetConfig()
        {
            var config = await XcavateMarketplaceConfigModel.GetConfigAsync(
                SolanaCluster.Devnet, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(config.AcceptedPaymentMints, Is.Not.Empty);
                // 13 at capture time and monotonic - it can only have grown since.
                Assert.That(config.NextListingId, Is.GreaterThanOrEqualTo(13UL));
                Assert.That(config.MarketplaceFeeBps, Is.LessThanOrEqualTo(10_000));
            });
        }
    }
}
