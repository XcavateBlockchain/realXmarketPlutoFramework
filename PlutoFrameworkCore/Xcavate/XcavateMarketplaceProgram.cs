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
    ///
    /// Writable flags deliberately do NOT follow the IDL. The deployed binary
    /// requires every account it receives to be writable in the transaction
    /// message - devnet simulation against the live program shows any readonly
    /// non-signer dying with PrivilegeEscalation - so every account meta below is
    /// marked writable to match the binary, not the IDL.
    /// </summary>
    public static class XcavateMarketplaceProgram
    {
        // Anchor instruction discriminators: sha256("global:<instruction_name>")[0..8],
        // as listed in the IDL. VerifyDiscriminators in the tests recomputes them.
        private static readonly byte[] BuyPropertySharesDiscriminator = [4, 160, 53, 28, 202, 98, 234, 11];
        private static readonly byte[] ReserveSharesDiscriminator = [137, 47, 218, 50, 106, 149, 133, 110];
        private static readonly byte[] ClaimSharesDiscriminator = [130, 131, 29, 237, 134, 20, 110, 245];
        private static readonly byte[] UnreserveSharesDiscriminator = [142, 74, 199, 174, 245, 11, 172, 40];
        private static readonly byte[] ReleaseReservationDiscriminator = [57, 15, 101, 135, 113, 21, 168, 247];
        private static readonly byte[] CreateSpvDiscriminator = [155, 147, 106, 185, 120, 239, 157, 204];
        private static readonly byte[] WithdrawExpiredDiscriminator = [58, 40, 206, 163, 80, 59, 31, 1];
        private static readonly byte[] WithdrawCancelledDiscriminator = [211, 39, 47, 234, 73, 113, 193, 59];
        private static readonly byte[] WithdrawLegalProcessExpiredDiscriminator = [64, 222, 8, 241, 156, 43, 91, 129];
        private static readonly byte[] VoteOnSpvLawyerDiscriminator = [245, 181, 143, 227, 83, 159, 141, 147];
        private static readonly byte[] RelistSharesDiscriminator = [233, 196, 4, 160, 236, 180, 219, 12];
        private static readonly byte[] DelistSharesDiscriminator = [75, 11, 92, 6, 15, 186, 201, 253];
        private static readonly byte[] BuyRelistedSharesDiscriminator = [205, 204, 84, 12, 55, 204, 167, 234];
        private static readonly byte[] SendPropertySharesDiscriminator = [214, 10, 132, 137, 120, 190, 89, 240];
        private static readonly byte[] CloseShareHoldingDiscriminator = [184, 83, 50, 153, 169, 129, 37, 176];
        private static readonly byte[] FinalizeSpvElectionDiscriminator = [2, 249, 39, 73, 224, 169, 145, 100];
        private static readonly byte[] ResolveSilentVerdictDiscriminator = [229, 121, 58, 51, 193, 189, 164, 60];
        private static readonly byte[] ExecuteDealDiscriminator = [107, 92, 176, 235, 53, 80, 14, 94];
        private static readonly byte[] AssignDeveloperLawyerDiscriminator = [197, 167, 242, 89, 235, 9, 169, 65];
        private static readonly byte[] ClaimSpvCaseDiscriminator = [175, 185, 102, 63, 166, 39, 168, 56];
        private static readonly byte[] ListPropertyDiscriminator = [254, 101, 42, 174, 220, 160, 42, 82];
        private static readonly byte[] InitPropertyAssetsDiscriminator = [86, 217, 248, 184, 33, 187, 144, 253];

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
        private static readonly byte[] LawyerVoteSeed = Encoding.UTF8.GetBytes("lawyer-vote");
        private static readonly byte[] LawyerCandidacySeed = Encoding.UTF8.GetBytes("lawyer-candidate");
        private static readonly byte[] ShareListingSeed = Encoding.UTF8.GetBytes("share-listing");
        private static readonly byte[] LawyerSeed = Encoding.UTF8.GetBytes("lawyer");
        private static readonly byte[] VaultSeed = Encoding.UTF8.GetBytes("vault");

        /// <summary>The regions program's Region PDA prefix - the one account the
        /// marketplace instructions pass that lives under a third program.</summary>
        private static readonly byte[] RegionSeed = Encoding.UTF8.GetBytes("region");

        /// <summary>The regions program's Location PDA prefix - like <see cref="RegionSeed"/>,
        /// owned by the regions program and passed to list_property so the program can bill
        /// the location's registration deposit.</summary>
        private static readonly byte[] LocationSeed = Encoding.UTF8.GetBytes("location");

        /// <summary>The marketplace program's own CPI signer seed - NOT the property
        /// program's [b"cpi-auth"] PDA (<see cref="XcavatePropertyProgram.DeriveCpiAuth"/>).
        /// buy_relisted_shares passes it so the property program's settle_income can verify
        /// the caller is this program.</summary>
        private static readonly byte[] CpiAuthSeed = Encoding.UTF8.GetBytes("cpi-auth");

        // The property program's income/checkpoint seeds are single-sourced on
        // XcavatePropertyProgram; DeriveIncome/DeriveIncomeCheckpoint below delegate.

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
        /// One voter's vote record in one SPV election round:
        /// [b"lawyer-vote", listing_id LE, round LE (u64), voter]. The round comes from the
        /// listing's election state (it is 1-based), so a vote built against a stale round
        /// fails the program's seed check.
        /// </summary>
        public static PublicKey DeriveLawyerVote(XcavateProgramSet programs, ulong listingId, ulong round, PublicKey voter) =>
            SolanaProgramAddress.Derive(
                new(programs.Marketplace), LawyerVoteSeed, U64(listingId), U64(round), voter.KeyBytes);

        /// <summary>
        /// One lawyer's candidacy in one SPV election round:
        /// [b"lawyer-candidate", listing_id LE, round LE (u64), lawyer].
        /// </summary>
        public static PublicKey DeriveLawyerCandidacy(XcavateProgramSet programs, ulong listingId, ulong round, PublicKey lawyer) =>
            SolanaProgramAddress.Derive(
                new(programs.Marketplace), LawyerCandidacySeed, U64(listingId), U64(round), lawyer.KeyBytes);

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
        /// A holder's secondary-market offer: [b"share-listing", id LE] under the
        /// marketplace program. The id is the config's next_share_listing_id at the moment
        /// of relisting - monotonic, so delisting never frees an id for reuse.
        /// </summary>
        public static PublicKey DeriveShareListing(XcavateProgramSet programs, ulong shareListingId) =>
            SolanaProgramAddress.Derive(new(programs.Marketplace), ShareListingSeed, U64(shareListingId));

        /// <summary>
        /// A lawyer's registry entry: [b"lawyer", lawyer] under the marketplace program -
        /// where an elected lawyer's open cases live. execute_deal closes both lawyers'
        /// entries and finalize_spv_election inspects the winner's.
        /// </summary>
        public static PublicKey DeriveLawyer(XcavateProgramSet programs, PublicKey lawyer) =>
            SolanaProgramAddress.Derive(new(programs.Marketplace), LawyerSeed, lawyer.KeyBytes);

        /// <summary>
        /// The protocol's XCAV vault: [b"vault"] under the marketplace program - what a
        /// completed deal's leftover deposits and fees settle into.
        /// </summary>
        public static PublicKey DeriveVault(XcavateProgramSet programs) =>
            SolanaProgramAddress.Derive(new(programs.Marketplace), VaultSeed);

        /// <summary>
        /// A region account: [b"region", region_id LE (u16)] under the REGIONS program -
        /// the only PDA here owned by a program other than the marketplace/property pair.
        /// execute_deal passes it so the region's operator collects its share of the
        /// leftover fees.
        /// </summary>
        public static PublicKey DeriveRegion(XcavateProgramSet programs, ushort regionId) =>
            SolanaProgramAddress.Derive(new(programs.Regions), RegionSeed, U16(regionId));

        /// <summary>
        /// A location account: [b"location", region_id LE (u16), postcode] under the REGIONS
        /// program - the postcode as the region registered it (no spaces, upper case).
        /// list_property passes it so the program can verify the property sits in a
        /// registered location before opening the sale.
        /// </summary>
        public static PublicKey DeriveLocation(XcavateProgramSet programs, ushort regionId, byte[] postcode) =>
            SolanaProgramAddress.Derive(new(programs.Regions), LocationSeed, U16(regionId), postcode);

        /// <summary>
        /// The marketplace program's own CPI signer PDA, [b"cpi-auth"] - what
        /// buy_relisted_shares passes so the property program's settle_income recognizes
        /// the marketplace as its caller. Not to be confused with the property program's
        /// same-named PDA (<see cref="XcavatePropertyProgram.DeriveCpiAuth"/>).
        /// </summary>
        public static PublicKey DeriveCpiAuth(XcavateProgramSet programs) =>
            SolanaProgramAddress.Derive(new(programs.Marketplace), CpiAuthSeed);

        /// <summary>
        /// The property program's per-asset income accumulator: [b"income", asset_id LE]
        /// under the PROPERTY program. buy_relisted_shares passes the derived address even
        /// when no income was ever distributed (the account may not exist yet). The
        /// derivation lives on <see cref="XcavatePropertyProgram"/> - this delegates so
        /// the seed literal exists once.
        /// </summary>
        public static PublicKey DeriveIncome(XcavateProgramSet programs, ulong assetId) =>
            XcavatePropertyProgram.DeriveIncome(programs, assetId);

        /// <summary>
        /// One holder's income-settlement checkpoint: [b"checkpoint", asset_id LE, owner]
        /// under the PROPERTY program - the property program settles both sides' unclaimed
        /// income up to these checkpoints when relisted shares change hands. Delegates to
        /// <see cref="XcavatePropertyProgram.DeriveIncomeCheckpoint"/>.
        /// </summary>
        public static PublicKey DeriveIncomeCheckpoint(XcavateProgramSet programs, ulong assetId, PublicKey owner) =>
            XcavatePropertyProgram.DeriveIncomeCheckpoint(programs, assetId, owner);

        /// <summary>
        /// buy_property_shares(listing_id, amount, max_total_cost): the direct purchase,
        /// which the program only opens after the claim window closes (until then it
        /// rejects with DirectBuyNotOpen - the sale phase buys through
        /// <see cref="ReserveShares"/>). <paramref name="maxTotalCost"/> caps what the
        /// program may charge (funds + fee + tax) so a listing repriced between building
        /// and landing aborts instead of overcharging.
        /// </summary>
        /// <param name="payer">
        /// The sponsor wallet fronting rent for the investor's new accounts. The
        /// deployed program pins this to the config's rent collector AND requires it
        /// to have signed - devnet simulation against the live program rejects a
        /// purchase whose payer is anyone else (even the investor) with
        /// NotRentCollector. When the signing wallet IS the rent collector the two
        /// roles collapse into one signature and the transaction completes.
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
                    AccountMeta.Writable(investor, true),
                    AccountMeta.Writable(payer, true),
                    AccountMeta.Writable(DeriveConfig(programs), false),
                    AccountMeta.Writable(DeriveRoleAccount(programs, investor, XcavateRole.RealEstateInvestor), false),
                    AccountMeta.Writable(DeriveListing(programs, listingId), false),
                    AccountMeta.Writable(DeriveProperty(programs, listingId), false),
                    AccountMeta.Writable(DerivePosition(programs, listingId, investor), false),
                    AccountMeta.Writable(DeriveHolding(programs, listingId, investor), false),
                    AccountMeta.Writable(paymentMint, false),
                    AccountMeta.Writable(investorPaymentAccount, false),
                    AccountMeta.Writable(listingVault, false),
                    AccountMeta.Writable(SolanaAssociatedTokenAccount.Derive(listingVault, paymentMint, paymentTokenProgram), false),
                    AccountMeta.Writable(shareMint, false),
                    AccountMeta.Writable(DeriveMintAuth(programs, listingId), false),
                    AccountMeta.Writable(propertyVault, false),
                    AccountMeta.Writable(SolanaAssociatedTokenAccount.Derive(propertyVault, shareMint, ShareTokenProgram), false),
                    AccountMeta.Writable(SolanaAssociatedTokenAccount.Derive(investor, shareMint, ShareTokenProgram), false),
                    AccountMeta.Writable(paymentTokenProgram, false),
                    AccountMeta.Writable(ShareTokenProgram, false),
                    AccountMeta.Writable(AssociatedTokenAccountProgram.ProgramIdKey, false),
                    AccountMeta.Writable(SystemProgram.ProgramIdKey, false),
                ],
                Data = Encode(BuyPropertySharesDiscriminator, U64(listingId), U32(amount), U64(maxTotalCost)),
            };
        }

        /// <summary>
        /// reserve_shares(listing_id, amount, max_total_cost): the sale-phase purchase.
        /// Money stays in the investor's payment account, bound by a reservation keyed to
        /// that account, until claim_shares pays for it - which is why this, not
        /// <see cref="BuyPropertyShares"/>, backs the Reserve button while a listing sells.
        /// </summary>
        /// <param name="payer">
        /// The wallet fronting rent for the investor's new accounts. The deployed
        /// program pins this to the config's rent collector and requires its
        /// signature, like <see cref="BuyPropertyShares"/>, so unless the signing
        /// wallet IS the rent collector the transaction needs the investor's and
        /// the rent collector's signatures.
        /// </param>
        public static TransactionInstruction ReserveShares(
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
            return new TransactionInstruction
            {
                ProgramId = new PublicKey(programs.Marketplace).KeyBytes,
                Keys =
                [
                    AccountMeta.Writable(investor, true),
                    AccountMeta.Writable(payer, true),
                    AccountMeta.Writable(DeriveConfig(programs), false),
                    AccountMeta.Writable(DeriveRoleAccount(programs, investor, XcavateRole.RealEstateInvestor), false),
                    AccountMeta.Writable(DeriveListing(programs, listingId), false),
                    AccountMeta.Writable(DeriveProperty(programs, listingId), false),
                    AccountMeta.Writable(DerivePosition(programs, listingId, investor), false),
                    AccountMeta.Writable(paymentMint, false),
                    AccountMeta.Writable(investorPaymentAccount, false),
                    AccountMeta.Writable(DeriveReservation(programs, investorPaymentAccount), false),
                    AccountMeta.Writable(SystemProgram.ProgramIdKey, false),
                ],
                Data = Encode(ReserveSharesDiscriminator, U64(listingId), U32(amount), U64(maxTotalCost)),
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
                    AccountMeta.Writable(investor, true),
                    AccountMeta.Writable(payer, true),
                    AccountMeta.Writable(DeriveConfig(programs), false),
                    AccountMeta.Writable(DeriveRoleAccount(programs, investor, XcavateRole.RealEstateInvestor), false),
                    AccountMeta.Writable(DeriveListing(programs, listingId), false),
                    AccountMeta.Writable(DeriveProperty(programs, listingId), false),
                    AccountMeta.Writable(DerivePosition(programs, listingId, investor), false),
                    AccountMeta.Writable(DeriveHolding(programs, listingId, investor), false),
                    AccountMeta.Writable(paymentMint, false),
                    AccountMeta.Writable(recordedPaymentAccount, false),
                    AccountMeta.Writable(DeriveReservation(programs, recordedPaymentAccount), false),
                    AccountMeta.Writable(listingVault, false),
                    AccountMeta.Writable(SolanaAssociatedTokenAccount.Derive(listingVault, paymentMint, paymentTokenProgram), false),
                    AccountMeta.Writable(shareMint, false),
                    AccountMeta.Writable(DeriveMintAuth(programs, listingId), false),
                    AccountMeta.Writable(propertyVault, false),
                    AccountMeta.Writable(SolanaAssociatedTokenAccount.Derive(propertyVault, shareMint, ShareTokenProgram), false),
                    AccountMeta.Writable(SolanaAssociatedTokenAccount.Derive(investor, shareMint, ShareTokenProgram), false),
                    AccountMeta.Writable(paymentTokenProgram, false),
                    AccountMeta.Writable(ShareTokenProgram, false),
                    AccountMeta.Writable(AssociatedTokenAccountProgram.ProgramIdKey, false),
                    AccountMeta.Writable(SystemProgram.ProgramIdKey, false),
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
                    AccountMeta.Writable(investor, true),
                    AccountMeta.Writable(DeriveListing(programs, listingId), false),
                    AccountMeta.Writable(DerivePosition(programs, listingId, investor), false),
                    AccountMeta.Writable(DeriveReservation(programs, recordedPaymentAccount), false),
                ],
                Data = Encode(UnreserveSharesDiscriminator, U64(listingId)),
            };
        }

        /// <summary>
        /// release_reservation(listing_id, investor): sweeps a reservation whose claim can
        /// no longer happen - the window closed, the listing expired, or the sale left
        /// Listed. The reserved payment never left the investor's wallet, so nothing is
        /// refunded; the position closes and its rent returns to the rent collector.
        /// Permissionless (the app signs with the investor themselves), the exit a
        /// reservation-only position takes where a paid one takes a withdraw instruction -
        /// the withdraws deserialize the share holding, which a never-claimed reservation
        /// never had created, and refuse it with AccountNotInitialized.
        /// </summary>
        /// <param name="cranker">The signer running the sweep; anyone may, exits are never gated.</param>
        /// <param name="investor">The position's owner, an instruction arg as well as a seed.</param>
        /// <param name="recordedPaymentAccount">The payment account the reservation binds - its PDA seed.</param>
        public static TransactionInstruction ReleaseReservation(
            XcavateProgramSet programs,
            PublicKey cranker,
            ulong listingId,
            PublicKey rentCollector,
            PublicKey investor,
            PublicKey recordedPaymentAccount)
        {
            return new TransactionInstruction
            {
                ProgramId = new PublicKey(programs.Marketplace).KeyBytes,
                Keys =
                [
                    AccountMeta.Writable(cranker, true),
                    AccountMeta.Writable(DeriveConfig(programs), false),
                    AccountMeta.Writable(rentCollector, false),
                    AccountMeta.Writable(DeriveListing(programs, listingId), false),
                    AccountMeta.Writable(DerivePosition(programs, listingId, investor), false),
                    AccountMeta.Writable(DeriveReservation(programs, recordedPaymentAccount), false),
                ],
                Data = Encode(ReleaseReservationDiscriminator, U64(listingId), investor.KeyBytes),
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
                    AccountMeta.Writable(confirmer, true),
                    AccountMeta.Writable(DeriveRoleAccount(programs, confirmer, XcavateRole.SpvConfirmation), false),
                    AccountMeta.Writable(DeriveListing(programs, listingId), false),
                    AccountMeta.Writable(DeriveProperty(programs, listingId), false),
                ],
                Data = Encode(CreateSpvDiscriminator, U64(listingId)),
            };
        }

        /// <summary>
        /// vote_on_spv_lawyer(listing_id, amount): the investor's share-weighted vote
        /// backing one candidacy in the listing's SPV-lawyer election - the pre-settlement
        /// vote the property page surfaces as the "terms" approval. The shares lock under
        /// LockReason::LawyerElection until unlock_voting_shares; revoting within the same
        /// candidacy reuses the record. <paramref name="round"/> is the election's current
        /// round, read from the indexer - the vote record's PDA is seeded with it.
        /// </summary>
        /// <param name="payer">
        /// Whoever fronts the vote record's rent - the program accepts any willing wallet;
        /// the app sends the config's rent collector, like on the purchase flow.
        /// </param>
        /// <param name="previousCandidate">
        /// The candidacy the voter backed earlier in this round, when switching votes;
        /// null on a first vote (the encoder then passes the program's own id, Anchor's
        /// sentinel for a None optional account).
        /// </param>
        public static TransactionInstruction VoteOnSpvLawyer(
            XcavateProgramSet programs,
            PublicKey voter,
            PublicKey payer,
            ulong listingId,
            ulong round,
            PublicKey candidate,
            PublicKey? previousCandidate,
            uint amount)
        {
            return new TransactionInstruction
            {
                ProgramId = new PublicKey(programs.Marketplace).KeyBytes,
                Keys =
                [
                    AccountMeta.Writable(voter, true),
                    AccountMeta.Writable(payer, true),
                    AccountMeta.Writable(DeriveRoleAccount(programs, voter, XcavateRole.RealEstateInvestor), false),
                    AccountMeta.Writable(DeriveListing(programs, listingId), false),
                    AccountMeta.Writable(DeriveHolding(programs, listingId, voter), false),
                    AccountMeta.Writable(DeriveLawyerVote(programs, listingId, round, voter), false),
                    AccountMeta.Writable(DeriveLawyerCandidacy(programs, listingId, round, candidate), false),
                    AccountMeta.Writable(previousCandidate is not null
                        ? DeriveLawyerCandidacy(programs, listingId, round, previousCandidate)
                        : new PublicKey(programs.Marketplace), false),
                    AccountMeta.Writable(SystemProgram.ProgramIdKey, false),
                ],
                Data = Encode(VoteOnSpvLawyerDiscriminator, U64(listingId), U32(amount)),
            };
        }

        /// <summary>
        /// assign_developer_lawyer(listing_id, lawyer): the listing's developer engages
        /// the named lawyer for the developer side of the legal phase. The program
        /// requires the listing to be SOLD_OUT inside its legal deadline, the
        /// developer-lawyer seat still vacant, the lawyer registered in the property's
        /// region with a compliant Lawyer role, and no conflict with the SPV side - all
        /// enforced on chain, so the caller only derives accounts. The named lawyer
        /// never signs; the developer's signature alone engages them.
        /// </summary>
        public static TransactionInstruction AssignDeveloperLawyer(
            XcavateProgramSet programs,
            PublicKey developer,
            PublicKey lawyer,
            ulong listingId)
        {
            return new TransactionInstruction
            {
                ProgramId = new PublicKey(programs.Marketplace).KeyBytes,
                Keys =
                [
                    AccountMeta.Writable(developer, true),
                    AccountMeta.Writable(DeriveRoleAccount(programs, developer, XcavateRole.RealEstateDeveloper), false),
                    AccountMeta.Writable(DeriveRoleAccount(programs, lawyer, XcavateRole.Lawyer), false),
                    AccountMeta.Writable(DeriveLawyer(programs, lawyer), false),
                    AccountMeta.Writable(DeriveListing(programs, listingId), false),
                    AccountMeta.Writable(DeriveProperty(programs, listingId), false),
                ],
                Data = Encode(AssignDeveloperLawyerDiscriminator, U64(listingId), lawyer.KeyBytes),
            };
        }

        /// <summary>
        /// list_property(region_id, postcode, share_price, share_amount,
        /// tax_paid_by_developer, max_deposit): the developer's opening of a verified
        /// property's sale. The Listing and Property accounts are initialized under the
        /// config's next_listing_id, so <paramref name="listingId"/> must be a fresh read
        /// of that counter - a stale id fails the accounts' seed checks. The program
        /// enforces the developer's compliant RealEstateDeveloper role, the registered
        /// region/location pair, the share-count and share-price limits, and the listing
        /// deposit (charged from <paramref name="developerToken"/> into the vault, capped
        /// at <paramref name="maxDeposit"/>) on chain, so the caller only derives accounts.
        /// The developer's signature alone opens the listing; no other wallet is involved.
        /// </summary>
        public static TransactionInstruction ListProperty(
            XcavateProgramSet programs,
            PublicKey developer,
            PublicKey xcavMint,
            PublicKey developerToken,
            PublicKey xcavTokenProgram,
            ushort regionId,
            byte[] postcode,
            ulong sharePrice,
            uint shareAmount,
            bool taxPaidByDeveloper,
            ulong maxDeposit,
            ulong listingId)
        {
            return new TransactionInstruction
            {
                ProgramId = new PublicKey(programs.Marketplace).KeyBytes,
                Keys =
                [
                    AccountMeta.Writable(developer, true),
                    AccountMeta.Writable(DeriveConfig(programs), false),
                    AccountMeta.Writable(DeriveRoleAccount(programs, developer, XcavateRole.RealEstateDeveloper), false),
                    AccountMeta.Writable(DeriveRegion(programs, regionId), false),
                    AccountMeta.Writable(DeriveLocation(programs, regionId, postcode), false),
                    AccountMeta.Writable(DeriveProperty(programs, listingId), false),
                    AccountMeta.Writable(DeriveListing(programs, listingId), false),
                    AccountMeta.Writable(xcavMint, false),
                    AccountMeta.Writable(developerToken, false),
                    AccountMeta.Writable(DeriveVault(programs), false),
                    AccountMeta.Writable(xcavTokenProgram, false),
                    AccountMeta.Writable(SystemProgram.ProgramIdKey, false),
                ],
                Data = Encode(
                    ListPropertyDiscriminator,
                    U16(regionId),
                    VecU8(postcode),
                    U64(sharePrice),
                    U32(shareAmount),
                    Bool(taxPaidByDeveloper),
                    U64(maxDeposit)),
            };
        }

        /// <summary>
        /// init_property_assets(listing_id, name, uri): the mandatory second half of
        /// opening a sale - list_property leaves the listing PENDING_ASSETS, and only this
        /// creates the share mint (Token-2022, 0 decimals, supply = the listed share
        /// amount), its authority and the property vault, then flips the listing to
        /// Listed. Sent in the same transaction as <see cref="ListProperty"/>: both are
        /// the developer's alone to sign, and one atomic submission leaves no
        /// PENDING_ASSETS listing behind when the second half fails. The program caps
        /// <paramref name="name"/> at 40 and <paramref name="uri"/> at 200 bytes, both
        /// non-empty; the caller truncates to fit.
        /// </summary>
        public static TransactionInstruction InitPropertyAssets(
            XcavateProgramSet programs,
            PublicKey developer,
            ulong listingId,
            string name,
            string uri)
        {
            var shareMint = DeriveShareMint(programs, listingId);
            var propertyVault = DerivePropertyVault(programs, listingId);

            return new TransactionInstruction
            {
                ProgramId = new PublicKey(programs.Marketplace).KeyBytes,
                Keys =
                [
                    AccountMeta.Writable(developer, true),
                    AccountMeta.Writable(DeriveRoleAccount(programs, developer, XcavateRole.RealEstateDeveloper), false),
                    AccountMeta.Writable(DeriveListing(programs, listingId), false),
                    AccountMeta.Writable(DeriveProperty(programs, listingId), false),
                    AccountMeta.Writable(shareMint, false),
                    AccountMeta.Writable(DeriveMintAuth(programs, listingId), false),
                    AccountMeta.Writable(propertyVault, false),
                    AccountMeta.Writable(SolanaAssociatedTokenAccount.Derive(propertyVault, shareMint, ShareTokenProgram), false),
                    AccountMeta.Writable(ShareTokenProgram, false),
                    AccountMeta.Writable(AssociatedTokenAccountProgram.ProgramIdKey, false),
                    AccountMeta.Writable(SystemProgram.ProgramIdKey, false),
                ],
                Data = Encode(InitPropertyAssetsDiscriminator, U64(listingId), BorshString(name), BorshString(uri)),
            };
        }

        /// <summary>
        /// claim_spv_case(listing_id, round, costs): a lawyer's candidacy for the
        /// listing's SPV-lawyer seat, which the token holders then vote on. The lawyer
        /// wallet fronts the candidacy account's rent itself (the program accepts any
        /// paying wallet, so the app takes the single-signer path with payer = lawyer).
        /// <paramref name="round"/> is the election round the candidacy stands in: when
        /// no round is running (the listing's spv_election expiry is 0) it must be the
        /// next round (election.round + 1, which opens the window); while a round runs
        /// it must be the current one. <paramref name="costs"/> is the lawyer's fee
        /// quote on the 9-decimal price scale, capped on chain by the listing's
        /// collected fee quote. The program further requires SOLD_OUT inside the legal
        /// deadline, spv_created, a vacant SPV seat, no conflict with the developer
        /// side, and a region-matching compliant lawyer registry entry.
        /// </summary>
        public static TransactionInstruction ClaimSpvCase(
            XcavateProgramSet programs,
            PublicKey lawyer,
            ulong listingId,
            ulong round,
            ulong costs)
        {
            return new TransactionInstruction
            {
                ProgramId = new PublicKey(programs.Marketplace).KeyBytes,
                Keys =
                [
                    AccountMeta.Writable(lawyer, true),
                    AccountMeta.Writable(lawyer, true),
                    AccountMeta.Writable(DeriveRoleAccount(programs, lawyer, XcavateRole.Lawyer), false),
                    AccountMeta.Writable(DeriveLawyer(programs, lawyer), false),
                    AccountMeta.Writable(DeriveListing(programs, listingId), false),
                    AccountMeta.Writable(DeriveProperty(programs, listingId), false),
                    AccountMeta.Writable(DeriveLawyerCandidacy(programs, listingId, round, lawyer), false),
                    AccountMeta.Writable(SystemProgram.ProgramIdKey, false),
                ],
                Data = Encode(ClaimSpvCaseDiscriminator, U64(listingId), U64(round), U64(costs)),
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
        /// withdraw_cancelled(listing_id): refund from a cancelled listing - the only
        /// status the program accepts it for. A refunding listing is mid-exit of the
        /// legal-deadline refund and takes <see cref="WithdrawLegalProcessExpired"/>
        /// instead. The closest successor to the pallet's withdraw_unclaimed refund path.
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
        /// relist_shares(asset_id, amount, share_price): the seller lists
        /// <paramref name="amount"/> of their shares of asset <paramref name="assetId"/> on
        /// the secondary market. The shares stay on the seller's holding (marked listed);
        /// the new ShareListing account is initialized at
        /// [b"share-listing", next_share_listing_id LE], so <paramref name="shareListingId"/>
        /// must be the config's next_share_listing_id read fresh - a stale one fails the
        /// program's seed check.
        /// </summary>
        /// <param name="payer">
        /// The wallet fronting the new account's rent, recorded as the listing's
        /// rent_payer and refunded at delist. The app sends the config's rent collector,
        /// like on the purchase flow.
        /// </param>
        /// <param name="sharePrice">
        /// The asking price per share at the program's 9-decimal price scale
        /// (PRICE_DECIMALS); the program rejects anything under 1000.
        /// </param>
        public static TransactionInstruction RelistShares(
            XcavateProgramSet programs,
            PublicKey seller,
            PublicKey payer,
            ulong assetId,
            uint amount,
            ulong sharePrice,
            ulong shareListingId)
        {
            return new TransactionInstruction
            {
                ProgramId = new PublicKey(programs.Marketplace).KeyBytes,
                Keys =
                [
                    AccountMeta.Writable(seller, true),
                    AccountMeta.Writable(payer, true),
                    AccountMeta.Writable(DeriveConfig(programs), false),
                    AccountMeta.Writable(DeriveRoleAccount(programs, seller, XcavateRole.RealEstateInvestor), false),
                    AccountMeta.Writable(DeriveListing(programs, assetId), false),
                    AccountMeta.Writable(DeriveHolding(programs, assetId, seller), false),
                    AccountMeta.Writable(DeriveShareListing(programs, shareListingId), false),
                    AccountMeta.Writable(SystemProgram.ProgramIdKey, false),
                ],
                Data = Encode(RelistSharesDiscriminator, U64(assetId), U32(amount), U64(sharePrice)),
            };
        }

        /// <summary>
        /// delist_shares(): the seller takes their secondary-market listing down,
        /// unmarking the shares on their holding and closing the ShareListing account -
        /// its rent goes back to <paramref name="rentPayer"/>, the wallet that fronted it,
        /// which the client reads from the ShareListing account first.
        /// </summary>
        public static TransactionInstruction DelistShares(
            XcavateProgramSet programs,
            PublicKey seller,
            PublicKey rentPayer,
            ulong shareListingId,
            ulong assetId)
        {
            return new TransactionInstruction
            {
                ProgramId = new PublicKey(programs.Marketplace).KeyBytes,
                Keys =
                [
                    AccountMeta.Writable(seller, true),
                    AccountMeta.Writable(rentPayer, false),
                    AccountMeta.Writable(DeriveShareListing(programs, shareListingId), false),
                    AccountMeta.Writable(DeriveHolding(programs, assetId, seller), false),
                ],
                Data = Encode(DelistSharesDiscriminator),
            };
        }

        /// <summary>
        /// buy_relisted_shares(asset_id, id, amount, max_total_cost): the secondary-market
        /// purchase - <paramref name="amount"/> shares off an open ShareListing, paying the
        /// seller their ask and the treasury the listing's pinned fee out of the seller's
        /// proceeds (so unlike <see cref="BuyPropertyShares"/> the buyer pays exactly
        /// share_price x amount, which is all <paramref name="maxTotalCost"/> caps). The
        /// program settles both sides' property income to their checkpoints as part of the
        /// transfer, which is what the property-program accounts are for.
        /// </summary>
        /// <param name="payer">
        /// The wallet fronting rent for the buyer's new accounts (their holding is
        /// init_if_needed). The app sends the config's rent collector, like on the
        /// primary purchase.
        /// </param>
        /// <param name="seller">The listing's seller, read from the ShareListing account.</param>
        /// <param name="rentPayer">The listing's rent_payer, read from the ShareListing account.</param>
        /// <param name="treasury">The config's treasury, which receives the fee's token account.</param>
        public static TransactionInstruction BuyRelistedShares(
            XcavateProgramSet programs,
            PublicKey buyer,
            PublicKey payer,
            ulong assetId,
            ulong shareListingId,
            uint amount,
            ulong maxTotalCost,
            PublicKey seller,
            PublicKey rentPayer,
            PublicKey paymentMint,
            PublicKey buyerPaymentAccount,
            PublicKey treasury,
            PublicKey paymentTokenProgram)
        {
            var shareMint = DeriveShareMint(programs, assetId);

            return new TransactionInstruction
            {
                ProgramId = new PublicKey(programs.Marketplace).KeyBytes,
                Keys =
                [
                    AccountMeta.Writable(buyer, true),
                    AccountMeta.Writable(payer, true),
                    AccountMeta.Writable(DeriveRoleAccount(programs, buyer, XcavateRole.RealEstateInvestor), false),
                    AccountMeta.Writable(DeriveConfig(programs), false),
                    AccountMeta.Writable(DeriveListing(programs, assetId), false),
                    AccountMeta.Writable(DeriveProperty(programs, assetId), false),
                    AccountMeta.Writable(DeriveShareListing(programs, shareListingId), false),
                    AccountMeta.Writable(seller, false),
                    AccountMeta.Writable(rentPayer, false),
                    AccountMeta.Writable(DeriveHolding(programs, assetId, seller), false),
                    AccountMeta.Writable(DeriveHolding(programs, assetId, buyer), false),
                    AccountMeta.Writable(paymentMint, false),
                    AccountMeta.Writable(buyerPaymentAccount, false),
                    AccountMeta.Writable(SolanaAssociatedTokenAccount.Derive(seller, paymentMint, paymentTokenProgram), false),
                    AccountMeta.Writable(treasury, false),
                    AccountMeta.Writable(SolanaAssociatedTokenAccount.Derive(treasury, paymentMint, paymentTokenProgram), false),
                    AccountMeta.Writable(shareMint, false),
                    AccountMeta.Writable(DeriveMintAuth(programs, assetId), false),
                    AccountMeta.Writable(SolanaAssociatedTokenAccount.Derive(seller, shareMint, ShareTokenProgram), false),
                    AccountMeta.Writable(SolanaAssociatedTokenAccount.Derive(buyer, shareMint, ShareTokenProgram), false),
                    AccountMeta.Writable(DeriveCpiAuth(programs), false),
                    AccountMeta.Writable(DeriveIncome(programs, assetId), false),
                    AccountMeta.Writable(DeriveIncomeCheckpoint(programs, assetId, seller), false),
                    AccountMeta.Writable(DeriveIncomeCheckpoint(programs, assetId, buyer), false),
                    AccountMeta.Writable(new PublicKey(programs.Property), false),
                    AccountMeta.Writable(paymentTokenProgram, false),
                    AccountMeta.Writable(ShareTokenProgram, false),
                    AccountMeta.Writable(AssociatedTokenAccountProgram.ProgramIdKey, false),
                    AccountMeta.Writable(SystemProgram.ProgramIdKey, false),
                ],
                Data = Encode(BuyRelistedSharesDiscriminator, U64(assetId), U64(shareListingId), U32(amount), U64(maxTotalCost)),
            };
        }

        /// <summary>
        /// send_property_shares(asset_id, amount): a direct investor-to-investor transfer
        /// of <paramref name="amount"/> shares of asset <paramref name="assetId"/> - no
        /// money moves, so unlike <see cref="BuyRelistedShares"/> there are no payment
        /// mint, treasury or share-listing accounts. The tail is the same settle_income
        /// CPI the secondary buy runs: the property program settles BOTH sides' unclaimed
        /// income to their checkpoints as part of the transfer, so neither wallet's
        /// pending rent follows the shares.
        /// </summary>
        /// <param name="payer">
        /// Whoever fronts rent for accounts created along the way (the receiver's holding
        /// is init_if_needed, their share account is created idempotently). The program
        /// accepts any willing wallet; the app sends the config's rent collector, like on
        /// the purchase flow.
        /// </param>
        /// <param name="receiver">
        /// The investor receiving the shares. Both parties' whitelist role PDAs gate the
        /// transfer: the program rejects a receiver without a compliant RealEstateInvestor
        /// role on <c>receiver_role</c>.
        /// </param>
        public static TransactionInstruction SendPropertyShares(
            XcavateProgramSet programs,
            PublicKey sender,
            PublicKey payer,
            ulong assetId,
            PublicKey receiver,
            uint amount)
        {
            var shareMint = DeriveShareMint(programs, assetId);

            return new TransactionInstruction
            {
                ProgramId = new PublicKey(programs.Marketplace).KeyBytes,
                Keys =
                [
                    AccountMeta.Writable(sender, true),
                    AccountMeta.Writable(payer, true),
                    AccountMeta.Writable(DeriveRoleAccount(programs, sender, XcavateRole.RealEstateInvestor), false),
                    AccountMeta.Writable(receiver, false),
                    AccountMeta.Writable(DeriveRoleAccount(programs, receiver, XcavateRole.RealEstateInvestor), false),
                    AccountMeta.Writable(DeriveListing(programs, assetId), false),
                    AccountMeta.Writable(DeriveProperty(programs, assetId), false),
                    AccountMeta.Writable(DeriveHolding(programs, assetId, sender), false),
                    AccountMeta.Writable(DeriveHolding(programs, assetId, receiver), false),
                    AccountMeta.Writable(shareMint, false),
                    AccountMeta.Writable(DeriveMintAuth(programs, assetId), false),
                    AccountMeta.Writable(SolanaAssociatedTokenAccount.Derive(sender, shareMint, ShareTokenProgram), false),
                    AccountMeta.Writable(SolanaAssociatedTokenAccount.Derive(receiver, shareMint, ShareTokenProgram), false),
                    AccountMeta.Writable(DeriveCpiAuth(programs), false),
                    AccountMeta.Writable(DeriveIncome(programs, assetId), false),
                    AccountMeta.Writable(DeriveIncomeCheckpoint(programs, assetId, sender), false),
                    AccountMeta.Writable(DeriveIncomeCheckpoint(programs, assetId, receiver), false),
                    AccountMeta.Writable(new PublicKey(programs.Property), false),
                    AccountMeta.Writable(ShareTokenProgram, false),
                    AccountMeta.Writable(AssociatedTokenAccountProgram.ProgramIdKey, false),
                    AccountMeta.Writable(SystemProgram.ProgramIdKey, false),
                ],
                Data = Encode(SendPropertySharesDiscriminator, U64(assetId), U32(amount)),
            };
        }

        /// <summary>
        /// close_share_holding(): closes an emptied ShareHolding (amount zero - after a
        /// full sale or send) and refunds its rent to <paramref name="rentCollector"/>.
        /// Carries no instruction args; the property and holding PDAs are seeded from the
        /// holding's own recorded asset_id and owner, which the caller supplies from the
        /// holding it read. Permissionless - anyone can crank anyone's empty holding, so
        /// the app signs with the owner themselves.
        /// </summary>
        public static TransactionInstruction CloseShareHolding(
            XcavateProgramSet programs,
            PublicKey cranker,
            PublicKey rentCollector,
            ulong assetId,
            PublicKey owner)
        {
            return new TransactionInstruction
            {
                ProgramId = new PublicKey(programs.Marketplace).KeyBytes,
                Keys =
                [
                    AccountMeta.Writable(cranker, true),
                    AccountMeta.Writable(DeriveConfig(programs), false),
                    AccountMeta.Writable(rentCollector, false),
                    AccountMeta.Writable(DeriveProperty(programs, assetId), false),
                    AccountMeta.Writable(DeriveHolding(programs, assetId, owner), false),
                ],
                Data = Encode(CloseShareHoldingDiscriminator),
            };
        }

        /// <summary>
        /// finalize_spv_election(listing_id): settles a listing's SPV-lawyer election once
        /// its window closed. Permissionless; the single signer is whoever cranks it.
        /// Every candidacy of the round rides along as a trailing remaining account - the
        /// on-chain handler pins their count to the election's candidate_count and walks
        /// them to find the plurality (the same requirement as the property program's
        /// finalize_agent_election) - so <paramref name="candidates"/> lists every lawyer
        /// who ran in <paramref name="round"/>, read from the indexer's pending
        /// finalizations. The round feeds only the candidacy PDA derivations; the
        /// instruction's sole argument is the listing id.
        /// </summary>
        /// <param name="winner">
        /// The lawyer whose candidacy leads the closing round, read from the indexer -
        /// the program inspects the winner's registry entry directly, so a wrong or
        /// vanished registry fails the election rather than mis-settling it. Null when no
        /// winner is determined (the encoder then passes the marketplace program's own id,
        /// Anchor's sentinel for a None optional account).
        /// </param>
        public static TransactionInstruction FinalizeSpvElection(
            XcavateProgramSet programs,
            PublicKey cranker,
            ulong listingId,
            ulong round,
            PublicKey? winner,
            IReadOnlyList<PublicKey> candidates)
        {
            var keys = new List<AccountMeta>
            {
                AccountMeta.Writable(cranker, true),
                AccountMeta.Writable(DeriveListing(programs, listingId), false),
                AccountMeta.Writable(DeriveProperty(programs, listingId), false),
                AccountMeta.Writable(winner is not null
                    ? DeriveLawyer(programs, winner)
                    : new PublicKey(programs.Marketplace), false),
            };

            keys.AddRange(candidates.Select(candidate =>
                AccountMeta.Writable(DeriveLawyerCandidacy(programs, listingId, round, candidate), false)));

            return new TransactionInstruction
            {
                ProgramId = new PublicKey(programs.Marketplace).KeyBytes,
                Keys = keys,
                Data = Encode(FinalizeSpvElectionDiscriminator, U64(listingId)),
            };
        }

        /// <summary>
        /// resolve_silent_verdict(listing_id): rules on a lawyer who let their review
        /// window lapse without a verdict. Permissionless; the single signer is whoever
        /// cranks it.
        /// </summary>
        public static TransactionInstruction ResolveSilentVerdict(
            XcavateProgramSet programs,
            PublicKey cranker,
            ulong listingId)
        {
            return new TransactionInstruction
            {
                ProgramId = new PublicKey(programs.Marketplace).KeyBytes,
                Keys =
                [
                    AccountMeta.Writable(cranker, true),
                    AccountMeta.Writable(DeriveListing(programs, listingId), false),
                ],
                Data = Encode(ResolveSilentVerdictDiscriminator, U64(listingId)),
            };
        }

        /// <summary>One collected payment mint with its token program, for <see cref="ExecuteDeal"/>.</summary>
        /// <param name="Mint">A mint the listing collected funds in, in the listing's collected order.</param>
        /// <param name="TokenProgram">The mint's owner program (legacy SPL or Token-2022) - the per-mint ATAs derive under it.</param>
        public sealed record CollectedMint(PublicKey Mint, PublicKey TokenProgram);

        /// <summary>
        /// execute_deal(listing_id): completes settlement once both lawyers approved and
        /// the legal window passed - releases the deposits, pays the lawyers, splits the
        /// leftover fees between the treasury's vault and the region's operator, and
        /// returns the developer's XCAV deposit. Permissionless; the single signer is
        /// whoever cranks it.
        /// </summary>
        /// <param name="regionId">
        /// The property's region, from the marketplace's PropertyAsset account - it seeds
        /// the regions program's Region PDA the fee share goes to.
        /// </param>
        /// <param name="developerLawyer">The listing's recorded developer lawyer (their registry entry closes here).</param>
        /// <param name="spvLawyer">The listing's recorded SPV lawyer.</param>
        /// <param name="xcavMint">The config's XCAV mint, which the deposit release is denominated in.</param>
        /// <param name="developer">The listing's developer; their XCAV associated account receives the deposit back.</param>
        /// <param name="xcavTokenProgram">The XCAV mint's token program.</param>
        /// <param name="paymentTokenProgram">The payment mints' token program - a second program id when it differs from the XCAV mint's.</param>
        /// <param name="regionOwner">The region account's operator, whose token account collects half the leftover fees.</param>
        /// <param name="treasury">The config's treasury; the last payee of every collected mint group.</param>
        /// <param name="collectedMints">
        /// Every mint the listing collected, in the listing's collected order (from the
        /// indexer's pending finalizations). The handler requires one trailing
        /// (mint, vault account, developer, developer's lawyer, SPV lawyer, region owner,
        /// treasury) group of accounts per collected mint - 7 entries each - and verifies
        /// the ATAs for the vault and rejects payee accounts that do not hold the mint.
        /// The builder derives every one as the standard associated token account under
        /// the mint's own token program, which the program accepts by construction.
        /// </param>
        public static TransactionInstruction ExecuteDeal(
            XcavateProgramSet programs,
            PublicKey cranker,
            ulong listingId,
            ushort regionId,
            PublicKey developerLawyer,
            PublicKey spvLawyer,
            PublicKey xcavMint,
            PublicKey developer,
            PublicKey xcavTokenProgram,
            PublicKey paymentTokenProgram,
            PublicKey regionOwner,
            PublicKey treasury,
            IReadOnlyList<CollectedMint> collectedMints)
        {
            var listingVault = DeriveListingVault(programs, listingId);

            var keys = new List<AccountMeta>
            {
                AccountMeta.Writable(cranker, true),
                AccountMeta.Writable(DeriveConfig(programs), false),
                AccountMeta.Writable(DeriveListing(programs, listingId), false),
                AccountMeta.Writable(DeriveProperty(programs, listingId), false),
                AccountMeta.Writable(DeriveRegion(programs, regionId), false),
                AccountMeta.Writable(DeriveLawyer(programs, developerLawyer), false),
                AccountMeta.Writable(DeriveLawyer(programs, spvLawyer), false),
                AccountMeta.Writable(listingVault, false),
                AccountMeta.Writable(xcavMint, false),
                AccountMeta.Writable(SolanaAssociatedTokenAccount.Derive(developer, xcavMint, xcavTokenProgram), false),
                AccountMeta.Writable(DeriveVault(programs), false),
                AccountMeta.Writable(xcavTokenProgram, false),
                AccountMeta.Writable(paymentTokenProgram, false),
            };

            foreach (var (mint, tokenProgram) in collectedMints)
            {
                keys.Add(AccountMeta.Writable(mint, false));
                keys.Add(AccountMeta.Writable(
                    SolanaAssociatedTokenAccount.Derive(listingVault, mint, tokenProgram), false));
                keys.Add(AccountMeta.Writable(
                    SolanaAssociatedTokenAccount.Derive(developer, mint, tokenProgram), false));
                keys.Add(AccountMeta.Writable(
                    SolanaAssociatedTokenAccount.Derive(developerLawyer, mint, tokenProgram), false));
                keys.Add(AccountMeta.Writable(
                    SolanaAssociatedTokenAccount.Derive(spvLawyer, mint, tokenProgram), false));
                keys.Add(AccountMeta.Writable(
                    SolanaAssociatedTokenAccount.Derive(regionOwner, mint, tokenProgram), false));
                keys.Add(AccountMeta.Writable(
                    SolanaAssociatedTokenAccount.Derive(treasury, mint, tokenProgram), false));
            }

            return new TransactionInstruction
            {
                ProgramId = new PublicKey(programs.Marketplace).KeyBytes,
                Keys = keys,
                Data = Encode(ExecuteDealDiscriminator, U64(listingId)),
            };
        }

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
                    AccountMeta.Writable(investor, true),
                    AccountMeta.Writable(DeriveConfig(programs), false),
                    AccountMeta.Writable(rentCollector, false),
                    AccountMeta.Writable(DeriveListing(programs, listingId), false),
                    AccountMeta.Writable(DeriveProperty(programs, listingId), false),
                    AccountMeta.Writable(DerivePosition(programs, listingId, investor), false),
                    AccountMeta.Writable(DeriveHolding(programs, listingId, investor), false),
                    AccountMeta.Writable(paymentMint, false),
                    AccountMeta.Writable(investorPaymentAccount, false),
                    AccountMeta.Writable(listingVault, false),
                    AccountMeta.Writable(SolanaAssociatedTokenAccount.Derive(listingVault, paymentMint, paymentTokenProgram), false),
                    AccountMeta.Writable(shareMint, false),
                    AccountMeta.Writable(DeriveMintAuth(programs, listingId), false),
                    AccountMeta.Writable(propertyVault, false),
                    AccountMeta.Writable(SolanaAssociatedTokenAccount.Derive(propertyVault, shareMint, ShareTokenProgram), false),
                    AccountMeta.Writable(SolanaAssociatedTokenAccount.Derive(investor, shareMint, ShareTokenProgram), false),
                    AccountMeta.Writable(paymentTokenProgram, false),
                    AccountMeta.Writable(ShareTokenProgram, false),
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

        private static byte[] U16(ushort value)
        {
            var bytes = new byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
            return bytes;
        }

        private static byte[] Bool(bool value) => [(byte)(value ? 1 : 0)];

        /// <summary>A Borsh vec of bytes: the u32 LE length prefix, then the bytes.</summary>
        private static byte[] VecU8(byte[] value)
        {
            var bytes = new byte[4 + value.Length];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, (uint)value.Length);
            value.CopyTo(bytes, 4);
            return bytes;
        }

        /// <summary>A Borsh string: the UTF-8 bytes behind a u32 LE length prefix.</summary>
        private static byte[] BorshString(string value) => VecU8(Encoding.UTF8.GetBytes(value));

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
