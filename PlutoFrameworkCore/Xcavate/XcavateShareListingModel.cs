using PlutoFrameworkCore.Solana;
using Solnet.Wallet;

namespace PlutoFramework.Model.Xcavate
{
    /// <summary>
    /// A holder's open secondary-market offer - the marketplace program's ShareListing
    /// account (idls/devnet/marketplace.json, account ShareListing), read straight from
    /// the chain because the delist and secondary-buy flows need its rent_payer, seller
    /// and live share_price exactly as they stand at transaction-build time.
    /// </summary>
    public sealed record XcavateShareListing
    {
        public required ulong Id { get; init; }
        public required ulong AssetId { get; init; }
        public required PublicKey Seller { get; init; }

        /// <summary>The asking price per share, at the 9-decimal price scale (PRICE_DECIMALS).</summary>
        public required ulong SharePrice { get; init; }

        /// <summary>Shares still for sale; partial buys draw it down.</summary>
        public required uint Amount { get; init; }

        /// <summary>The marketplace fee pinned at listing time, taken from the seller's proceeds.</summary>
        public required ushort FeeBps { get; init; }
        public required ulong NextOfferNonce { get; init; }

        /// <summary>The wallet that fronted the account's rent, refunded at delist.</summary>
        public required PublicKey RentPayer { get; init; }
        public required byte Bump { get; init; }
    }

    public static class XcavateShareListingModel
    {
        /// <summary>The ShareListing account's Anchor discriminator, checked before parsing.</summary>
        private static readonly byte[] ShareListingDiscriminator = [38, 219, 182, 80, 137, 29, 254, 143];

        /// <summary>
        /// Fetches and parses the ShareListing account for <paramref name="shareListingId"/>
        /// on <paramref name="cluster"/>.
        /// </summary>
        public static async Task<XcavateShareListing> GetShareListingAsync(
            SolanaCluster cluster, ulong shareListingId, CancellationToken token = default)
        {
            var programs = XcavateProgramAddresses.Require(cluster);
            var address = XcavateMarketplaceProgram.DeriveShareListing(programs, shareListingId);

            var accountInfo = await SolanaRpcModel.GetAccountInfoAsync(cluster, address.Key, token)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException(
                    $"Share listing {shareListingId} does not exist (any more) on {cluster.GetName()}.");

            if (accountInfo.Owner != programs.Marketplace)
            {
                throw new InvalidOperationException(
                    $"The account at the share-listing PDA is owned by {accountInfo.Owner}, not the marketplace program.");
            }

            return Parse(Convert.FromBase64String(accountInfo.Data[0]));
        }

        /// <summary>
        /// Parses the ShareListing account's raw bytes (discriminator included), field by
        /// field in IDL order.
        /// </summary>
        public static XcavateShareListing Parse(byte[] data)
        {
            var reader = new AnchorBorshReader(data, ShareListingDiscriminator, nameof(XcavateShareListing));

            return new XcavateShareListing
            {
                Id = reader.U64(),
                AssetId = reader.U64(),
                Seller = reader.Pubkey(),
                SharePrice = reader.U64(),
                Amount = reader.U32(),
                FeeBps = reader.U16(),
                NextOfferNonce = reader.U64(),
                RentPayer = reader.Pubkey(),
                Bump = reader.U8(),
            };
        }
    }
}
