using PlutoFrameworkCore.Solana;
using Solnet.Programs;
using Solnet.Rpc.Models;
using Solnet.Wallet;
using System.Buffers.Binary;
using System.Text;

namespace PlutoFramework.Model.Xcavate
{
    /// <summary>
    /// Hand-built instructions of the Xcavate marketplace Solana program, transcribed
    /// from <c>idls/devnet/marketplace.json</c> - the successor to the hand-encoded
    /// XcavatePaseo Marketplace pallet calls. Everything here is pure: account orders,
    /// PDA seeds and discriminators come from the IDL, and the caller supplies whatever
    /// has to be looked up elsewhere (payment mints, recorded payment accounts, the
    /// rent collector).
    /// </summary>
    public static class XcavateMarketplaceProgram
    {
        // Anchor instruction discriminators: sha256("global:<instruction_name>")[0..8],
        // as listed in the IDL. VerifyDiscriminators in the tests recomputes them.
        private static readonly byte[] BuyPropertySharesDiscriminator = [4, 160, 53, 28, 202, 98, 234, 11];
        private static readonly byte[] ClaimSharesDiscriminator = [130, 131, 29, 237, 134, 20, 110, 245];
        private static readonly byte[] UnreserveSharesDiscriminator = [142, 74, 199, 174, 245, 11, 172, 40];
        private static readonly byte[] CreateSpvDiscriminator = [155, 147, 106, 185, 120, 239, 157, 204];
        private static readonly byte[] WithdrawExpiredDiscriminator = [58, 40, 206, 163, 80, 59, 31, 1];
        private static readonly byte[] WithdrawCancelledDiscriminator = [211, 39, 47, 234, 73, 113, 193, 59];
        private static readonly byte[] WithdrawLegalProcessExpiredDiscriminator = [64, 222, 8, 241, 156, 43, 91, 129];

        // PDA seed prefixes, from the IDL's constants section.
        private static readonly byte[] ConfigSeed = Encoding.UTF8.GetBytes("config");
        private static readonly byte[] ListingSeed = Encoding.UTF8.GetBytes("listing");
        private static readonly byte[] PropertySeed = Encoding.UTF8.GetBytes("property");
        private static readonly byte[] PositionSeed = Encoding.UTF8.GetBytes("position");
        private static readonly byte[] ShareSeed = Encoding.UTF8.GetBytes("share");
        private static readonly byte[] ShareMintSeed = Encoding.UTF8.GetBytes("share-mint");
        private static readonly byte[] MintAuthSeed = Encoding.UTF8.GetBytes("mint-auth");
        private static readonly byte[] ListingVaultSeed = Encoding.UTF8.GetBytes("listing-vault");
        private static readonly byte[] PropertyVaultSeed = Encoding.UTF8.GetBytes("property-vault");
        private static readonly byte[] ReservationSeed = Encoding.UTF8.GetBytes("reservation");

        /// <summary>ROLE_SEED of the whitelist program, whose RoleAccount PDAs gate the calls.</summary>
        private static readonly byte[] RoleSeed = Encoding.UTF8.GetBytes("role");

        /// <summary>The share mint's program is always Token-2022 (IDL-pinned).</summary>
        private static readonly PublicKey ShareTokenProgram = new(SolanaTokenProgram.Token2022);

        public static PublicKey DeriveConfig(XcavateProgramSet programs) =>
            SolanaProgramAddress.Derive(new(programs.Marketplace), ConfigSeed);

        public static PublicKey DeriveListing(XcavateProgramSet programs, ulong listingId) =>
            SolanaProgramAddress.Derive(new(programs.Marketplace), ListingSeed, U64(listingId));

        public static PublicKey DeriveProperty(XcavateProgramSet programs, ulong listingId) =>
            SolanaProgramAddress.Derive(new(programs.Marketplace), PropertySeed, U64(listingId));

        public static PublicKey DerivePosition(XcavateProgramSet programs, ulong listingId, PublicKey investor) =>
            SolanaProgramAddress.Derive(new(programs.Marketplace), PositionSeed, U64(listingId), investor.KeyBytes);

        public static PublicKey DeriveHolding(XcavateProgramSet programs, ulong listingId, PublicKey investor) =>
            SolanaProgramAddress.Derive(new(programs.Marketplace), ShareSeed, U64(listingId), investor.KeyBytes);

        public static PublicKey DeriveShareMint(XcavateProgramSet programs, ulong listingId) =>
            SolanaProgramAddress.Derive(new(programs.Marketplace), ShareMintSeed, U64(listingId));

        public static PublicKey DeriveMintAuth(XcavateProgramSet programs, ulong listingId) =>
            SolanaProgramAddress.Derive(new(programs.Marketplace), MintAuthSeed, U64(listingId));

        public static PublicKey DeriveListingVault(XcavateProgramSet programs, ulong listingId) =>
            SolanaProgramAddress.Derive(new(programs.Marketplace), ListingVaultSeed, U64(listingId));

        public static PublicKey DerivePropertyVault(XcavateProgramSet programs, ulong listingId) =>
            SolanaProgramAddress.Derive(new(programs.Marketplace), PropertyVaultSeed, U64(listingId));

        /// <summary>Keyed by the payment token account the reservation binds, not by the investor.</summary>
        public static PublicKey DeriveReservation(XcavateProgramSet programs, PublicKey paymentAccount) =>
            SolanaProgramAddress.Derive(new(programs.Marketplace), ReservationSeed, paymentAccount.KeyBytes);

        /// <summary>
        /// The whitelist program's RoleAccount PDA for one (user, role) pair - what the
        /// marketplace instructions take as <c>investor_role</c> / <c>confirmer_role</c>.
        /// The role byte is the Role enum's variant index, which <see cref="XcavateRole"/>
        /// mirrors for the six on-chain roles.
        /// </summary>
        public static PublicKey DeriveRoleAccount(XcavateProgramSet programs, PublicKey user, XcavateRole role)
        {
            if ((int)role > (int)XcavateRole.SpvConfirmation)
            {
                throw new ArgumentOutOfRangeException(nameof(role), role,
                    "Not a role the Solana whitelist program knows about");
            }

            return SolanaProgramAddress.Derive(new(programs.Whitelist), RoleSeed, user.KeyBytes, [(byte)role]);
        }

        /// <summary>
        /// buy_property_shares(listing_id, amount, max_total_cost): an immediate purchase.
        /// <paramref name="maxTotalCost"/> caps what the program may charge (funds + fee +
        /// tax) so a listing repriced between building and landing aborts instead of
        /// overcharging.
        /// </summary>
        /// <param name="payer">
        /// The sponsor wallet fronting rent for the investor's new accounts, which the
        /// program pins to the config's rent collector - see
        /// <see cref="XcavateMarketplaceCallsModel"/> for what that means for submission.
        /// </param>
        public static TransactionInstruction BuyPropertyShares(
            XcavateProgramSet programs,
            PublicKey investor,
            PublicKey payer,
            ulong listingId,
            uint amount,
            ulong maxTotalCost,
            PublicKey paymentMint,
            PublicKey investorPaymentAccount,
            PublicKey paymentTokenProgram)
        {
            var shareMint = DeriveShareMint(programs, listingId);
            var listingVault = DeriveListingVault(programs, listingId);
            var propertyVault = DerivePropertyVault(programs, listingId);

            return new TransactionInstruction
            {
                ProgramId = new PublicKey(programs.Marketplace).KeyBytes,
                Keys =
                [
                    AccountMeta.ReadOnly(investor, true),
                    AccountMeta.Writable(payer, true),
                    AccountMeta.ReadOnly(DeriveConfig(programs), false),
                    AccountMeta.ReadOnly(DeriveRoleAccount(programs, investor, XcavateRole.RealEstateInvestor), false),
                    AccountMeta.Writable(DeriveListing(programs, listingId), false),
                    AccountMeta.Writable(DeriveProperty(programs, listingId), false),
                    AccountMeta.Writable(DerivePosition(programs, listingId, investor), false),
                    AccountMeta.Writable(DeriveHolding(programs, listingId, investor), false),
                    AccountMeta.ReadOnly(paymentMint, false),
                    AccountMeta.Writable(investorPaymentAccount, false),
                    AccountMeta.ReadOnly(listingVault, false),
                    AccountMeta.Writable(SolanaAssociatedTokenAccount.Derive(listingVault, paymentMint, paymentTokenProgram), false),
                    AccountMeta.ReadOnly(shareMint, false),
                    AccountMeta.ReadOnly(DeriveMintAuth(programs, listingId), false),
                    AccountMeta.ReadOnly(propertyVault, false),
                    AccountMeta.Writable(SolanaAssociatedTokenAccount.Derive(propertyVault, shareMint, ShareTokenProgram), false),
                    AccountMeta.Writable(SolanaAssociatedTokenAccount.Derive(investor, shareMint, ShareTokenProgram), false),
                    AccountMeta.ReadOnly(paymentTokenProgram, false),
                    AccountMeta.ReadOnly(ShareTokenProgram, false),
                    AccountMeta.ReadOnly(AssociatedTokenAccountProgram.ProgramIdKey, false),
                    AccountMeta.ReadOnly(SystemProgram.ProgramIdKey, false),
                ],
                Data = Encode(BuyPropertySharesDiscriminator, U64(listingId), U32(amount), U64(maxTotalCost)),
            };
        }

        /// <summary>
        /// claim_shares(listing_id): turns the investor's reservation into delivered
        /// shares, paying from the exact payment account the reservation was made with -
        /// the reservation PDA is keyed by it, so no other account can stand in.
        /// </summary>
        public static TransactionInstruction ClaimShares(
            XcavateProgramSet programs,
            PublicKey investor,
            PublicKey payer,
            ulong listingId,
            PublicKey paymentMint,
            PublicKey recordedPaymentAccount,
            PublicKey paymentTokenProgram)
        {
            var shareMint = DeriveShareMint(programs, listingId);
            var listingVault = DeriveListingVault(programs, listingId);
            var propertyVault = DerivePropertyVault(programs, listingId);

            return new TransactionInstruction
            {
                ProgramId = new PublicKey(programs.Marketplace).KeyBytes,
                Keys =
                [
                    AccountMeta.ReadOnly(investor, true),
                    AccountMeta.Writable(payer, true),
                    AccountMeta.ReadOnly(DeriveConfig(programs), false),
                    AccountMeta.ReadOnly(DeriveRoleAccount(programs, investor, XcavateRole.RealEstateInvestor), false),
                    AccountMeta.Writable(DeriveListing(programs, listingId), false),
                    AccountMeta.Writable(DeriveProperty(programs, listingId), false),
                    AccountMeta.Writable(DerivePosition(programs, listingId, investor), false),
                    AccountMeta.Writable(DeriveHolding(programs, listingId, investor), false),
                    AccountMeta.ReadOnly(paymentMint, false),
                    AccountMeta.Writable(recordedPaymentAccount, false),
                    AccountMeta.Writable(DeriveReservation(programs, recordedPaymentAccount), false),
                    AccountMeta.ReadOnly(listingVault, false),
                    AccountMeta.Writable(SolanaAssociatedTokenAccount.Derive(listingVault, paymentMint, paymentTokenProgram), false),
                    AccountMeta.ReadOnly(shareMint, false),
                    AccountMeta.ReadOnly(DeriveMintAuth(programs, listingId), false),
                    AccountMeta.ReadOnly(propertyVault, false),
                    AccountMeta.Writable(SolanaAssociatedTokenAccount.Derive(propertyVault, shareMint, ShareTokenProgram), false),
                    AccountMeta.Writable(SolanaAssociatedTokenAccount.Derive(investor, shareMint, ShareTokenProgram), false),
                    AccountMeta.ReadOnly(paymentTokenProgram, false),
                    AccountMeta.ReadOnly(ShareTokenProgram, false),
                    AccountMeta.ReadOnly(AssociatedTokenAccountProgram.ProgramIdKey, false),
                    AccountMeta.ReadOnly(SystemProgram.ProgramIdKey, false),
                ],
                Data = Encode(ClaimSharesDiscriminator, U64(listingId)),
            };
        }

        /// <summary>
        /// unreserve_shares(listing_id): the investor cancels their reservation - the
        /// replacement for the pallet's cancel_property_purchase.
        /// </summary>
        public static TransactionInstruction UnreserveShares(
            XcavateProgramSet programs,
            PublicKey investor,
            ulong listingId,
            PublicKey recordedPaymentAccount)
        {
            return new TransactionInstruction
            {
                ProgramId = new PublicKey(programs.Marketplace).KeyBytes,
                Keys =
                [
                    AccountMeta.ReadOnly(investor, true),
                    AccountMeta.Writable(DeriveListing(programs, listingId), false),
                    AccountMeta.Writable(DerivePosition(programs, listingId, investor), false),
                    AccountMeta.Writable(DeriveReservation(programs, recordedPaymentAccount), false),
                ],
                Data = Encode(UnreserveSharesDiscriminator, U64(listingId)),
            };
        }

        /// <summary>
        /// create_spv(listing_id), called by an SpvConfirmation role holder.
        /// </summary>
        public static TransactionInstruction CreateSpv(
            XcavateProgramSet programs,
            PublicKey confirmer,
            ulong listingId)
        {
            return new TransactionInstruction
            {
                ProgramId = new PublicKey(programs.Marketplace).KeyBytes,
                Keys =
                [
                    AccountMeta.ReadOnly(confirmer, true),
                    AccountMeta.ReadOnly(DeriveRoleAccount(programs, confirmer, XcavateRole.SpvConfirmation), false),
                    AccountMeta.Writable(DeriveListing(programs, listingId), false),
                    AccountMeta.Writable(DeriveProperty(programs, listingId), false),
                ],
                Data = Encode(CreateSpvDiscriminator, U64(listingId)),
            };
        }

        /// <summary>
        /// withdraw_expired(listing_id): refund after the listing expired unsold - the
        /// replacement for the pallet's withdraw_expired.
        /// </summary>
        public static TransactionInstruction WithdrawExpired(
            XcavateProgramSet programs,
            PublicKey investor,
            ulong listingId,
            PublicKey rentCollector,
            PublicKey paymentMint,
            PublicKey investorPaymentAccount,
            PublicKey paymentTokenProgram) =>
            Withdraw(WithdrawExpiredDiscriminator, programs, investor, listingId, rentCollector, paymentMint, investorPaymentAccount, paymentTokenProgram);

        /// <summary>
        /// withdraw_cancelled(listing_id): refund from a cancelled or refunding listing -
        /// the closest successor to the pallet's withdraw_unclaimed refund path.
        /// </summary>
        public static TransactionInstruction WithdrawCancelled(
            XcavateProgramSet programs,
            PublicKey investor,
            ulong listingId,
            PublicKey rentCollector,
            PublicKey paymentMint,
            PublicKey investorPaymentAccount,
            PublicKey paymentTokenProgram) =>
            Withdraw(WithdrawCancelledDiscriminator, programs, investor, listingId, rentCollector, paymentMint, investorPaymentAccount, paymentTokenProgram);

        /// <summary>
        /// withdraw_legal_process_expired(listing_id): refund after the post-sale legal
        /// phase blew its deadline - the closest successor to the pallet's
        /// withdraw_claiming_expired refund path.
        /// </summary>
        public static TransactionInstruction WithdrawLegalProcessExpired(
            XcavateProgramSet programs,
            PublicKey investor,
            ulong listingId,
            PublicKey rentCollector,
            PublicKey paymentMint,
            PublicKey investorPaymentAccount,
            PublicKey paymentTokenProgram) =>
            Withdraw(WithdrawLegalProcessExpiredDiscriminator, programs, investor, listingId, rentCollector, paymentMint, investorPaymentAccount, paymentTokenProgram);

        /// <summary>
        /// The three withdraw instructions share one account shape; only the
        /// discriminator differs.
        /// </summary>
        private static TransactionInstruction Withdraw(
            byte[] discriminator,
            XcavateProgramSet programs,
            PublicKey investor,
            ulong listingId,
            PublicKey rentCollector,
            PublicKey paymentMint,
            PublicKey investorPaymentAccount,
            PublicKey paymentTokenProgram)
        {
            var shareMint = DeriveShareMint(programs, listingId);
            var listingVault = DeriveListingVault(programs, listingId);
            var propertyVault = DerivePropertyVault(programs, listingId);

            return new TransactionInstruction
            {
                ProgramId = new PublicKey(programs.Marketplace).KeyBytes,
                Keys =
                [
                    AccountMeta.ReadOnly(investor, true),
                    AccountMeta.ReadOnly(DeriveConfig(programs), false),
                    AccountMeta.Writable(rentCollector, false),
                    AccountMeta.Writable(DeriveListing(programs, listingId), false),
                    AccountMeta.Writable(DeriveProperty(programs, listingId), false),
                    AccountMeta.Writable(DerivePosition(programs, listingId, investor), false),
                    AccountMeta.Writable(DeriveHolding(programs, listingId, investor), false),
                    AccountMeta.ReadOnly(paymentMint, false),
                    AccountMeta.Writable(investorPaymentAccount, false),
                    AccountMeta.ReadOnly(listingVault, false),
                    AccountMeta.Writable(SolanaAssociatedTokenAccount.Derive(listingVault, paymentMint, paymentTokenProgram), false),
                    AccountMeta.ReadOnly(shareMint, false),
                    AccountMeta.ReadOnly(DeriveMintAuth(programs, listingId), false),
                    AccountMeta.ReadOnly(propertyVault, false),
                    AccountMeta.Writable(SolanaAssociatedTokenAccount.Derive(propertyVault, shareMint, ShareTokenProgram), false),
                    AccountMeta.Writable(SolanaAssociatedTokenAccount.Derive(investor, shareMint, ShareTokenProgram), false),
                    AccountMeta.ReadOnly(paymentTokenProgram, false),
                    AccountMeta.ReadOnly(ShareTokenProgram, false),
                ],
                Data = Encode(discriminator, U64(listingId)),
            };
        }

        private static byte[] U64(ulong value)
        {
            var bytes = new byte[8];
            BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
            return bytes;
        }

        private static byte[] U32(uint value)
        {
            var bytes = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
            return bytes;
        }

        private static byte[] Encode(byte[] discriminator, params byte[][] args)
        {
            var data = new List<byte>(discriminator);

            foreach (var arg in args)
            {
                data.AddRange(arg);
            }

            return [.. data];
        }
    }
}
