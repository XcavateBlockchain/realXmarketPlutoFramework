using PlutoFrameworkCore.Solana;
using Solnet.Wallet;
using System.Buffers.Binary;

namespace PlutoFramework.Model.Xcavate
{
    /// <summary>
    /// The marketplace program's on-chain Config account, parsed field by field from its
    /// Borsh layout (idls/devnet/marketplace.json, account Config). The indexer also
    /// serves config rows, but the secondary-market calls need next_share_listing_id -
    /// which the indexer does not expose - so this reads the account straight from the
    /// chain, fresh at transaction-build time.
    /// </summary>
    public sealed record XcavateMarketplaceConfig
    {
        public required PublicKey Authority { get; init; }
        public required PublicKey? PendingAuthority { get; init; }
        public required PublicKey XcavMint { get; init; }
        public required PublicKey Treasury { get; init; }
        public required PublicKey RentCollector { get; init; }
        public required List<PublicKey> AcceptedPaymentMints { get; init; }
        public required ulong ListingDeposit { get; init; }
        public required ulong LawyerDeposit { get; init; }
        public required uint MinPropertyShares { get; init; }
        public required uint MaxPropertyShares { get; init; }
        public required ushort MarketplaceFeeBps { get; init; }
        public required ushort InvestorFeeBps { get; init; }
        public required ushort MaxOwnershipBps { get; init; }
        public required long ClaimingTime { get; init; }
        public required long LegalProcessTime { get; init; }
        public required long LawyerVotingTime { get; init; }
        public required ushort MinVotingQuorumBps { get; init; }
        public required ulong NextListingId { get; init; }

        /// <summary>The id the next relist_shares initializes its ShareListing under.</summary>
        public required ulong NextShareListingId { get; init; }
        public required byte Bump { get; init; }
    }

    public static class XcavateMarketplaceConfigModel
    {
        /// <summary>The Config account's Anchor discriminator, checked before parsing.</summary>
        private static readonly byte[] ConfigDiscriminator = [155, 12, 170, 224, 30, 250, 204, 130];

        /// <summary>
        /// Fetches and parses the marketplace Config account on <paramref name="cluster"/>.
        /// </summary>
        public static async Task<XcavateMarketplaceConfig> GetConfigAsync(
            SolanaCluster cluster, CancellationToken token = default)
        {
            var programs = XcavateProgramAddresses.Require(cluster);
            var configAddress = XcavateMarketplaceProgram.DeriveConfig(programs);

            var accountInfo = await SolanaRpcModel.GetAccountInfoAsync(cluster, configAddress.Key, token)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException(
                    $"The marketplace is not initialized on {cluster.GetName()}.");

            if (accountInfo.Owner != programs.Marketplace)
            {
                throw new InvalidOperationException(
                    $"The account at the config PDA is owned by {accountInfo.Owner}, not the marketplace program.");
            }

            return Parse(Convert.FromBase64String(accountInfo.Data[0]));
        }

        /// <summary>
        /// Parses the Config account's raw bytes (discriminator included). The parse is
        /// sequential: accepted_payment_mints is a Borsh vec, so no field past it sits at
        /// a fixed offset. Trailing bytes are tolerated - the account allocation can
        /// outgrow the layout.
        /// </summary>
        public static XcavateMarketplaceConfig Parse(byte[] data)
        {
            var reader = new AnchorBorshReader(data, ConfigDiscriminator, nameof(XcavateMarketplaceConfig));

            return new XcavateMarketplaceConfig
            {
                Authority = reader.Pubkey(),
                PendingAuthority = reader.OptionPubkey(),
                XcavMint = reader.Pubkey(),
                Treasury = reader.Pubkey(),
                RentCollector = reader.Pubkey(),
                AcceptedPaymentMints = reader.PubkeyVec(),
                ListingDeposit = reader.U64(),
                LawyerDeposit = reader.U64(),
                MinPropertyShares = reader.U32(),
                MaxPropertyShares = reader.U32(),
                MarketplaceFeeBps = reader.U16(),
                InvestorFeeBps = reader.U16(),
                MaxOwnershipBps = reader.U16(),
                ClaimingTime = reader.I64(),
                LegalProcessTime = reader.I64(),
                LawyerVotingTime = reader.I64(),
                MinVotingQuorumBps = reader.U16(),
                NextListingId = reader.U64(),
                NextShareListingId = reader.U64(),
                Bump = reader.U8(),
            };
        }
    }

    /// <summary>
    /// Sequential little-endian reader over an Anchor account's raw bytes. The leading
    /// 8-byte discriminator is verified up front, so a layout/account mix-up fails at the
    /// first field rather than surfacing as garbage values downstream.
    /// </summary>
    internal sealed class AnchorBorshReader
    {
        private readonly byte[] data;
        private int offset;

        public AnchorBorshReader(byte[] data, byte[] discriminator, string accountName)
        {
            if (data.Length < 8 || !data[..8].SequenceEqual(discriminator))
            {
                throw new InvalidOperationException(
                    $"The account is not a {accountName}: its discriminator does not match the IDL.");
            }

            this.data = data;
            offset = 8;
        }

        public byte U8() => Slice(1)[0];

        public ushort U16() => BinaryPrimitives.ReadUInt16LittleEndian(Slice(2));

        public uint U32() => BinaryPrimitives.ReadUInt32LittleEndian(Slice(4));

        public ulong U64() => BinaryPrimitives.ReadUInt64LittleEndian(Slice(8));

        public long I64() => BinaryPrimitives.ReadInt64LittleEndian(Slice(8));

        public PublicKey Pubkey() => new(Slice(32));

        public PublicKey? OptionPubkey() => U8() switch
        {
            0 => null,
            1 => Pubkey(),
            var tag => throw new InvalidOperationException($"Invalid Borsh option tag {tag}."),
        };

        public List<PublicKey> PubkeyVec()
        {
            var count = U32();

            if (count > 1_000)
            {
                // A config never holds a thousand mints; this many means the parse drifted.
                throw new InvalidOperationException($"Unreasonable Borsh vec length {count}.");
            }

            return [.. Enumerable.Range(0, (int)count).Select(_ => Pubkey())];
        }

        private byte[] Slice(int length)
        {
            if (offset + length > data.Length)
            {
                throw new InvalidOperationException("The account data ended mid-layout: not the expected account shape.");
            }

            var slice = data[offset..(offset + length)];
            offset += length;

            return slice;
        }
    }
}
