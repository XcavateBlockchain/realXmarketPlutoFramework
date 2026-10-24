using Solnet.Programs;
using PlutoFramework.Model.Xcavate;
using PlutoFrameworkCore.Solana;
using Solnet.Rpc.Builders;
using Solnet.Wallet;
using System.Security.Cryptography;
using System.Text;

namespace PlutoFrameworkTests
{
    /// <summary>
    /// The instruction-shape and math tests are pure. The PDA tests hit devnet: they
    /// assert the derived addresses actually exist on chain under the right program,
    /// which pins the seed layouts against the deployed programs rather than against
    /// this code's own assumptions.
    /// </summary>
    internal class XcavateMarketplaceProgramTests
    {
        private static readonly XcavateProgramSet Programs = XcavateProgramAddresses.Devnet;

        // A devnet wallet holding the RealEstateInvestor role (also used by WhitelistModelTests).
        private const string KnownInvestor = "EJpEpZ8rQY5gVkv6exjZ2urQpPwF6BS6RTaE4UzvhhsF";

        private static PublicKey SyntheticKey(byte seed) =>
            new(SolanaBase58.Encode([.. Enumerable.Repeat(seed, 32)]));

        [Test]
        [TestCase("buy_property_shares")]
        [TestCase("reserve_shares")]
        [TestCase("claim_shares")]
        [TestCase("unreserve_shares")]
        [TestCase("release_reservation")]
        [TestCase("create_spv")]
        [TestCase("withdraw_expired")]
        [TestCase("withdraw_cancelled")]
        [TestCase("withdraw_legal_process_expired")]
        [TestCase("vote_on_spv_lawyer")]
        [TestCase("relist_shares")]
        [TestCase("delist_shares")]
        [TestCase("buy_relisted_shares")]
        [TestCase("send_property_shares")]
        [TestCase("close_share_holding")]
        [TestCase("finalize_spv_election")]
        [TestCase("resolve_silent_verdict")]
        [TestCase("execute_deal")]
        [TestCase("assign_developer_lawyer")]
        [TestCase("claim_spv_case")]
        [TestCase("list_property")]
        public void Discriminators_MatchTheAnchorFormula(string instructionName)
        {
            var expected = SHA256.HashData(Encoding.UTF8.GetBytes($"global:{instructionName}"))[..8];

            var instruction = instructionName switch
            {
                "buy_property_shares" => XcavateMarketplaceProgram.BuyPropertyShares(
                    Programs, SyntheticKey(1), SyntheticKey(2), 7, 5, 1_000, SyntheticKey(3), SyntheticKey(4), new PublicKey(SolanaTokenProgram.Legacy)),
                "reserve_shares" => XcavateMarketplaceProgram.ReserveShares(
                    Programs, SyntheticKey(1), SyntheticKey(2), 7, 5, 1_000, SyntheticKey(3), SyntheticKey(4), new PublicKey(SolanaTokenProgram.Legacy)),
                "claim_shares" => XcavateMarketplaceProgram.ClaimShares(
                    Programs, SyntheticKey(1), SyntheticKey(2), 7, SyntheticKey(3), SyntheticKey(4), new PublicKey(SolanaTokenProgram.Legacy)),
                "unreserve_shares" => XcavateMarketplaceProgram.UnreserveShares(
                    Programs, SyntheticKey(1), 7, SyntheticKey(4)),
                "release_reservation" => XcavateMarketplaceProgram.ReleaseReservation(
                    Programs, SyntheticKey(1), 7, SyntheticKey(2), SyntheticKey(1), SyntheticKey(4)),
                "create_spv" => XcavateMarketplaceProgram.CreateSpv(Programs, SyntheticKey(1), 7),
                "withdraw_expired" => XcavateMarketplaceProgram.WithdrawExpired(
                    Programs, SyntheticKey(1), 7, SyntheticKey(2), SyntheticKey(3), SyntheticKey(4), new PublicKey(SolanaTokenProgram.Legacy)),
                "withdraw_cancelled" => XcavateMarketplaceProgram.WithdrawCancelled(
                    Programs, SyntheticKey(1), 7, SyntheticKey(2), SyntheticKey(3), SyntheticKey(4), new PublicKey(SolanaTokenProgram.Legacy)),
                "withdraw_legal_process_expired" => XcavateMarketplaceProgram.WithdrawLegalProcessExpired(
                    Programs, SyntheticKey(1), 7, SyntheticKey(2), SyntheticKey(3), SyntheticKey(4), new PublicKey(SolanaTokenProgram.Legacy)),
                "vote_on_spv_lawyer" => XcavateMarketplaceProgram.VoteOnSpvLawyer(
                    Programs, SyntheticKey(1), SyntheticKey(2), 7, 1, SyntheticKey(3), null, 5),
                "relist_shares" => XcavateMarketplaceProgram.RelistShares(
                    Programs, SyntheticKey(1), SyntheticKey(2), 7, 5, 10_000, 3),
                "delist_shares" => XcavateMarketplaceProgram.DelistShares(
                    Programs, SyntheticKey(1), SyntheticKey(2), 3, 7),
                "buy_relisted_shares" => XcavateMarketplaceProgram.BuyRelistedShares(
                    Programs, SyntheticKey(1), SyntheticKey(2), 7, 3, 5, 1_000,
                    SyntheticKey(5), SyntheticKey(6), SyntheticKey(3), SyntheticKey(4), SyntheticKey(7),
                    new PublicKey(SolanaTokenProgram.Legacy)),
                "send_property_shares" => XcavateMarketplaceProgram.SendPropertyShares(
                    Programs, SyntheticKey(1), SyntheticKey(2), 7, SyntheticKey(3), 5),
                "close_share_holding" => XcavateMarketplaceProgram.CloseShareHolding(
                    Programs, SyntheticKey(1), SyntheticKey(2), 7, SyntheticKey(3)),
                "finalize_spv_election" => XcavateMarketplaceProgram.FinalizeSpvElection(
                    Programs, SyntheticKey(1), 7, 1, null, []),
                "resolve_silent_verdict" => XcavateMarketplaceProgram.ResolveSilentVerdict(
                    Programs, SyntheticKey(1), 7),
                "execute_deal" => XcavateMarketplaceProgram.ExecuteDeal(
                    Programs, SyntheticKey(1), 7, 3, SyntheticKey(2), SyntheticKey(3), SyntheticKey(4), SyntheticKey(5),
                    new PublicKey(SolanaTokenProgram.Token2022), new PublicKey(SolanaTokenProgram.Legacy),
                    SyntheticKey(6), SyntheticKey(7), []),
                "assign_developer_lawyer" => XcavateMarketplaceProgram.AssignDeveloperLawyer(
                    Programs, SyntheticKey(1), SyntheticKey(2), 7),
                "claim_spv_case" => XcavateMarketplaceProgram.ClaimSpvCase(
                    Programs, SyntheticKey(1), 7, 2, 10_000),
                "list_property" => XcavateMarketplaceProgram.ListProperty(
                    Programs, SyntheticKey(1), SyntheticKey(3), SyntheticKey(4),
                    new PublicKey(SolanaTokenProgram.Legacy), 3, Encoding.UTF8.GetBytes("AB12CD"),
                    1_000, 100, false, 5_000, 7),
                _ => throw new ArgumentOutOfRangeException(nameof(instructionName)),
            };

            Assert.That(instruction.Data[..8], Is.EqualTo(expected));
        }

        [Test]
        public void BuyPropertyShares_EncodesArgsLittleEndian()
        {
            var instruction = XcavateMarketplaceProgram.BuyPropertyShares(
                Programs,
                investor: SyntheticKey(1),
                payer: SyntheticKey(2),
                listingId: 0x0102030405060708,
                amount: 0x0A0B0C0D,
                maxTotalCost: 0x1112131415161718,
                paymentMint: SyntheticKey(3),
                investorPaymentAccount: SyntheticKey(4),
                paymentTokenProgram: new PublicKey(SolanaTokenProgram.Legacy));

            // discriminator + u64 listing_id + u32 amount + u64 max_total_cost
            Assert.That(instruction.Data, Has.Length.EqualTo(8 + 8 + 4 + 8));
            Assert.That(instruction.Data[8..16], Is.EqualTo(new byte[] { 8, 7, 6, 5, 4, 3, 2, 1 }));
            Assert.That(instruction.Data[16..20], Is.EqualTo(new byte[] { 0x0D, 0x0C, 0x0B, 0x0A }));
            Assert.That(instruction.Data[20..28], Is.EqualTo(new byte[] { 0x18, 0x17, 0x16, 0x15, 0x14, 0x13, 0x12, 0x11 }));
        }

        [Test]
        public void ReleaseReservation_EncodesArgsLittleEndian()
        {
            var investor = SyntheticKey(9);
            var instruction = XcavateMarketplaceProgram.ReleaseReservation(
                Programs,
                cranker: SyntheticKey(1),
                listingId: 0x0102030405060708,
                rentCollector: SyntheticKey(2),
                investor: investor,
                recordedPaymentAccount: SyntheticKey(4));

            // discriminator + u64 listing_id + pubkey investor
            Assert.That(instruction.Data, Has.Length.EqualTo(8 + 8 + 32));
            Assert.That(instruction.Data[8..16], Is.EqualTo(new byte[] { 8, 7, 6, 5, 4, 3, 2, 1 }));
            Assert.That(instruction.Data[16..48], Is.EqualTo(investor.KeyBytes));
        }

        [Test]
        public void Instructions_HaveTheIdlAccountShapes()
        {
            var investor = SyntheticKey(1);
            var payer = SyntheticKey(2);
            var mint = SyntheticKey(3);
            var paymentAccount = SyntheticKey(4);
            var legacyToken = new PublicKey(SolanaTokenProgram.Legacy);

            var buy = XcavateMarketplaceProgram.BuyPropertyShares(Programs, investor, payer, 7, 5, 1_000, mint, paymentAccount, legacyToken);
            var reserve = XcavateMarketplaceProgram.ReserveShares(Programs, investor, payer, 7, 5, 1_000, mint, paymentAccount, legacyToken);
            var claim = XcavateMarketplaceProgram.ClaimShares(Programs, investor, payer, 7, mint, paymentAccount, legacyToken);
            var withdraw = XcavateMarketplaceProgram.WithdrawExpired(Programs, investor, 7, payer, mint, paymentAccount, legacyToken);
            var unreserve = XcavateMarketplaceProgram.UnreserveShares(Programs, investor, 7, paymentAccount);
            var release = XcavateMarketplaceProgram.ReleaseReservation(Programs, investor, 7, payer, investor, paymentAccount);
            var createSpv = XcavateMarketplaceProgram.CreateSpv(Programs, investor, 7);

            Assert.Multiple(() =>
            {
                Assert.That(buy.Keys, Has.Count.EqualTo(21));
                Assert.That(reserve.Keys, Has.Count.EqualTo(11));
                Assert.That(claim.Keys, Has.Count.EqualTo(22));
                Assert.That(withdraw.Keys, Has.Count.EqualTo(18));
                Assert.That(unreserve.Keys, Has.Count.EqualTo(4));
                Assert.That(release.Keys, Has.Count.EqualTo(6));
                Assert.That(createSpv.Keys, Has.Count.EqualTo(4));

                // The investor/confirmer leads every instruction and is its signer,
                // and every account in the message is writable: the deployed binary
                // escalates the whole message, so a readonly non-signer dies with
                // PrivilegeEscalation in simulation.
                foreach (var instruction in new[] { buy, reserve, claim, withdraw, unreserve, release, createSpv })
                {
                    Assert.That(instruction.Keys[0].PublicKey, Is.EqualTo(investor.Key));
                    Assert.That(instruction.Keys[0].IsSigner, Is.True);
                    Assert.That(instruction.Keys[0].IsWritable, Is.True);

                    foreach (var key in instruction.Keys.Where(key => !key.IsSigner))
                    {
                        Assert.That(key.IsWritable, Is.True);
                    }
                }

                // The rent-fronting payer co-signs buy, reserve and claim, and only those.
                foreach (var instruction in new[] { buy, reserve, claim })
                {
                    Assert.That(instruction.Keys[1].PublicKey, Is.EqualTo(payer.Key));
                    Assert.That(instruction.Keys[1].IsSigner, Is.True);
                    Assert.That(instruction.Keys[1].IsWritable, Is.True);
                }

                Assert.That(withdraw.Keys.Count(key => key.IsSigner), Is.EqualTo(1));
                Assert.That(unreserve.Keys.Count(key => key.IsSigner), Is.EqualTo(1));
                Assert.That(release.Keys.Count(key => key.IsSigner), Is.EqualTo(1));
                Assert.That(createSpv.Keys.Count(key => key.IsSigner), Is.EqualTo(1));
            });
        }

        /// <summary>
        /// The regression behind "failed to sanitize accounts offsets": with the rent
        /// collector fronting rent as a distinct payer, the compiled message's header
        /// requires two signature slots (investor plus the rent collector), and a wire
        /// transaction framed with fewer slots is rejected as malformed. (A payer that
        /// is the investor themselves leaves a single signature slot - see
        /// <see cref="ReserveShares_InvestorFrontedRequiresOneSignature"/>.)
        /// </summary>
        [Test]
        public void ReserveShares_CompiledMessageRequiresTwoSignatures()
        {
            var investor = SyntheticKey(1);
            var payer = SyntheticKey(2);
            var mint = SyntheticKey(3);
            var paymentAccount = SyntheticKey(4);
            var legacyToken = new PublicKey(SolanaTokenProgram.Legacy);

            var builder = new TransactionBuilder()
                .SetRecentBlockHash(SolanaBase58.Encode(new byte[32]))
                .SetFeePayer(investor)
                .AddInstruction(
                    XcavateMarketplaceProgram.ReserveShares(
                        Programs, investor, payer, 7, 5, 1_000, mint, paymentAccount, legacyToken));

            var compiled = builder.CompileMessage();

            Assert.That(SolanaTransactionFramer.GetRequiredSignatures(compiled), Is.EqualTo(2));
        }

        /// <summary>
        /// Encoder-level check: when the payer IS the investor, only one signer is
        /// left, so the compiled message needs a single signature slot. The deployed
        /// program does not accept that layout for reserve_shares (it pins the payer
        /// to the config's rent collector and rejects the investor with
        /// NotRentCollector), so the app always sends the two-signer layout above.
        /// </summary>
        [Test]
        public void ReserveShares_InvestorFrontedRequiresOneSignature()
        {
            var investor = SyntheticKey(1);
            var mint = SyntheticKey(3);
            var paymentAccount = SyntheticKey(4);
            var legacyToken = new PublicKey(SolanaTokenProgram.Legacy);

            var builder = new TransactionBuilder()
                .SetRecentBlockHash(SolanaBase58.Encode(new byte[32]))
                .SetFeePayer(investor)
                .AddInstruction(
                    XcavateMarketplaceProgram.ReserveShares(
                        Programs, investor, investor, 7, 5, 1_000, mint, paymentAccount, legacyToken));

            var compiled = builder.CompileMessage();

            Assert.That(SolanaTransactionFramer.GetRequiredSignatures(compiled), Is.EqualTo(1));
        }

        [Test]
        public void VoteOnSpvLawyer_HasTheIdlAccountShape()
        {
            var voter = SyntheticKey(1);
            var payer = SyntheticKey(2);
            var candidate = SyntheticKey(3);

            var vote = XcavateMarketplaceProgram.VoteOnSpvLawyer(
                Programs, voter, payer, 7, 2, candidate, null, 5);

            Assert.Multiple(() =>
            {
                // voter, payer, voter_role, listing, holding, vote_record, candidacy,
                // previous_candidacy, system_program - 9 accounts, in IDL order.
                Assert.That(vote.Keys, Has.Count.EqualTo(9));
                Assert.That(vote.Keys[0].PublicKey, Is.EqualTo(voter.Key));
                Assert.That(vote.Keys[0].IsSigner, Is.True);
                Assert.That(vote.Keys[1].PublicKey, Is.EqualTo(payer.Key));
                Assert.That(vote.Keys[1].IsSigner, Is.True);

                // Every non-signer account meta is writable (the deployed-binary
                // escalation quirk documented on the program class) - except the
                // previous-candidacy sentinel, which is the program's own id.
                Assert.That(vote.Keys[7].PublicKey, Is.EqualTo(new PublicKey(Programs.Marketplace).Key));

                Assert.That(vote.Keys[2].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveRoleAccount(Programs, voter, XcavateRole.RealEstateInvestor).Key));
                Assert.That(vote.Keys[3].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveListing(Programs, 7).Key));
                Assert.That(vote.Keys[4].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveHolding(Programs, 7, voter).Key));
                Assert.That(vote.Keys[5].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveLawyerVote(Programs, 7, 2, voter).Key));
                Assert.That(vote.Keys[6].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveLawyerCandidacy(Programs, 7, 2, candidate).Key));
                Assert.That(vote.Keys[8].PublicKey, Is.EqualTo(SystemProgram.ProgramIdKey.Key));

                // A vote switch keys the previous candidacy instead of the sentinel.
                var previous = SyntheticKey(4);
                var switchVote = XcavateMarketplaceProgram.VoteOnSpvLawyer(
                    Programs, voter, payer, 7, 2, candidate, previous, 5);
                Assert.That(switchVote.Keys[7].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveLawyerCandidacy(Programs, 7, 2, previous).Key));
            });
        }

        [Test]
        public void VoteOnSpvLawyer_EncodesArgsLittleEndian()
        {
            var instruction = XcavateMarketplaceProgram.VoteOnSpvLawyer(
                Programs,
                voter: SyntheticKey(1),
                payer: SyntheticKey(2),
                listingId: 0x0102030405060708,
                round: 0x0A0B0C0D0E0F1011,
                candidate: SyntheticKey(3),
                previousCandidate: null,
                amount: 0x0C0D0E0F);

            // discriminator + u64 listing_id + u32 amount (no choice field)
            Assert.That(instruction.Data, Has.Length.EqualTo(8 + 8 + 4));
            Assert.That(instruction.Data[8..16], Is.EqualTo(new byte[] { 8, 7, 6, 5, 4, 3, 2, 1 }));
            Assert.That(instruction.Data[16..20], Is.EqualTo(new byte[] { 0x0F, 0x0E, 0x0D, 0x0C }));
        }

        [Test]
        public void AssignDeveloperLawyer_HasTheIdlAccountShape()
        {
            var developer = SyntheticKey(1);
            var lawyer = SyntheticKey(2);

            var instruction = XcavateMarketplaceProgram.AssignDeveloperLawyer(Programs, developer, lawyer, 7);

            Assert.Multiple(() =>
            {
                // developer, developer_role, lawyer_role, registry, listing, property -
                // 6 accounts, in IDL order; the named lawyer never signs.
                Assert.That(instruction.Keys, Has.Count.EqualTo(6));
                Assert.That(instruction.Keys[0].PublicKey, Is.EqualTo(developer.Key));
                Assert.That(instruction.Keys[0].IsSigner, Is.True);
                Assert.That(instruction.Keys[1].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveRoleAccount(Programs, developer, XcavateRole.RealEstateDeveloper).Key));
                Assert.That(instruction.Keys[2].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveRoleAccount(Programs, lawyer, XcavateRole.Lawyer).Key));
                Assert.That(instruction.Keys[3].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveLawyer(Programs, lawyer).Key));
                Assert.That(instruction.Keys[4].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveListing(Programs, 7).Key));
                Assert.That(instruction.Keys[5].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveProperty(Programs, 7).Key));
            });
        }

        [Test]
        public void AssignDeveloperLawyer_EncodesArgsLittleEndian()
        {
            var lawyer = SyntheticKey(2);

            var instruction = XcavateMarketplaceProgram.AssignDeveloperLawyer(
                Programs, SyntheticKey(1), lawyer, 0x0102030405060708);

            // discriminator + u64 listing_id + pubkey lawyer
            Assert.That(instruction.Data, Has.Length.EqualTo(8 + 8 + 32));
            Assert.That(instruction.Data[8..16], Is.EqualTo(new byte[] { 8, 7, 6, 5, 4, 3, 2, 1 }));
            Assert.That(instruction.Data[16..48], Is.EqualTo(lawyer.KeyBytes));
        }

        [Test]
        public void ClaimSpvCase_HasTheIdlAccountShape()
        {
            var lawyer = SyntheticKey(1);

            var claim = XcavateMarketplaceProgram.ClaimSpvCase(Programs, lawyer, 7, 2, 10_000);

            Assert.Multiple(() =>
            {
                // lawyer, payer, lawyer_role, registry, listing, property, candidacy,
                // system_program - 8 accounts, in IDL order. The lawyer wallet fronts the
                // candidacy's rent itself (payer == lawyer): the program accepts any
                // paying wallet and the app takes the single-signer path.
                Assert.That(claim.Keys, Has.Count.EqualTo(8));
                Assert.That(claim.Keys[0].PublicKey, Is.EqualTo(lawyer.Key));
                Assert.That(claim.Keys[0].IsSigner, Is.True);
                Assert.That(claim.Keys[1].PublicKey, Is.EqualTo(lawyer.Key));
                Assert.That(claim.Keys[1].IsSigner, Is.True);
                Assert.That(claim.Keys[2].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveRoleAccount(Programs, lawyer, XcavateRole.Lawyer).Key));
                Assert.That(claim.Keys[3].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveLawyer(Programs, lawyer).Key));
                Assert.That(claim.Keys[4].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveListing(Programs, 7).Key));
                Assert.That(claim.Keys[5].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveProperty(Programs, 7).Key));
                Assert.That(claim.Keys[6].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveLawyerCandidacy(Programs, 7, 2, lawyer).Key));
                Assert.That(claim.Keys[7].PublicKey, Is.EqualTo(SystemProgram.ProgramIdKey.Key));
            });
        }

        [Test]
        public void ClaimSpvCase_EncodesArgsLittleEndian()
        {
            var instruction = XcavateMarketplaceProgram.ClaimSpvCase(
                Programs,
                lawyer: SyntheticKey(1),
                listingId: 0x0102030405060708,
                round: 0x0A0B0C0D0E0F1011,
                costs: 0x1213141516171819);

            // discriminator + u64 listing_id + u64 round + u64 costs
            Assert.That(instruction.Data, Has.Length.EqualTo(8 + 8 + 8 + 8));
            Assert.That(instruction.Data[8..16], Is.EqualTo(new byte[] { 8, 7, 6, 5, 4, 3, 2, 1 }));
            Assert.That(instruction.Data[16..24], Is.EqualTo(new byte[] { 0x11, 0x10, 0x0F, 0x0E, 0x0D, 0x0C, 0x0B, 0x0A }));
            Assert.That(instruction.Data[24..32], Is.EqualTo(new byte[] { 0x19, 0x18, 0x17, 0x16, 0x15, 0x14, 0x13, 0x12 }));
        }

        [Test]
        public void ListProperty_HasTheIdlAccountShape()
        {
            var developer = SyntheticKey(1);
            var xcavMint = SyntheticKey(3);
            var developerToken = SyntheticKey(4);
            var legacyToken = new PublicKey(SolanaTokenProgram.Legacy);
            var postcode = Encoding.UTF8.GetBytes("AB12CD");

            var instruction = XcavateMarketplaceProgram.ListProperty(
                Programs, developer, xcavMint, developerToken, legacyToken,
                3, postcode, 1_000, 100, false, 5_000, 7);

            Assert.Multiple(() =>
            {
                Assert.That(instruction.ProgramId, Is.EqualTo(new PublicKey(Programs.Marketplace).KeyBytes));

                // developer, config, developer_role, region, location, property, listing,
                // xcav_mint, developer_token, vault, token_program, system_program -
                // 12 accounts, in IDL order; the developer alone signs.
                Assert.That(instruction.Keys, Has.Count.EqualTo(12));
                Assert.That(instruction.Keys[0].PublicKey, Is.EqualTo(developer.Key));
                Assert.That(instruction.Keys[0].IsSigner, Is.True);
                Assert.That(instruction.Keys[1].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveConfig(Programs).Key));
                Assert.That(instruction.Keys[2].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveRoleAccount(Programs, developer, XcavateRole.RealEstateDeveloper).Key));
                Assert.That(instruction.Keys[3].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveRegion(Programs, 3).Key));
                Assert.That(instruction.Keys[4].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveLocation(Programs, 3, postcode).Key));
                Assert.That(instruction.Keys[5].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveProperty(Programs, 7).Key));
                Assert.That(instruction.Keys[6].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveListing(Programs, 7).Key));
                Assert.That(instruction.Keys[7].PublicKey, Is.EqualTo(xcavMint.Key));
                Assert.That(instruction.Keys[8].PublicKey, Is.EqualTo(developerToken.Key));
                Assert.That(instruction.Keys[9].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveVault(Programs).Key));
                Assert.That(instruction.Keys[10].PublicKey, Is.EqualTo(legacyToken.Key));
                Assert.That(instruction.Keys[11].PublicKey, Is.EqualTo(SystemProgram.ProgramIdKey.Key));

                // The deployed binary requires every account writable, and nobody but the
                // developer signs.
                Assert.That(instruction.Keys.All(key => key.IsWritable), Is.True);
                Assert.That(instruction.Keys.Skip(1).All(key => !key.IsSigner), Is.True);
            });
        }

        [Test]
        public void ListProperty_EncodesArgsLittleEndian()
        {
            var postcode = Encoding.UTF8.GetBytes("AB12CD");

            var instruction = XcavateMarketplaceProgram.ListProperty(
                Programs,
                developer: SyntheticKey(1),
                xcavMint: SyntheticKey(3),
                developerToken: SyntheticKey(4),
                xcavTokenProgram: new PublicKey(SolanaTokenProgram.Legacy),
                regionId: 0x0102,
                postcode: postcode,
                sharePrice: 0x1112131415161718,
                shareAmount: 0x0A0B0C0D,
                taxPaidByDeveloper: true,
                maxDeposit: 0x2122232425262728,
                listingId: 7);

            // discriminator + u16 region_id + vec<u8> postcode (u32 length prefix + bytes)
            // + u64 share_price + u32 share_amount + bool tax_paid_by_developer + u64 max_deposit
            Assert.That(instruction.Data, Has.Length.EqualTo(8 + 2 + 4 + 6 + 8 + 4 + 1 + 8));
            Assert.That(instruction.Data[8..10], Is.EqualTo(new byte[] { 0x02, 0x01 }));
            Assert.That(instruction.Data[10..14], Is.EqualTo(new byte[] { 6, 0, 0, 0 }));
            Assert.That(instruction.Data[14..20], Is.EqualTo(postcode));
            Assert.That(instruction.Data[20..28], Is.EqualTo(new byte[] { 0x18, 0x17, 0x16, 0x15, 0x14, 0x13, 0x12, 0x11 }));
            Assert.That(instruction.Data[28..32], Is.EqualTo(new byte[] { 0x0D, 0x0C, 0x0B, 0x0A }));
            Assert.That(instruction.Data[32], Is.EqualTo((byte)1));
            Assert.That(instruction.Data[33..41], Is.EqualTo(new byte[] { 0x28, 0x27, 0x26, 0x25, 0x24, 0x23, 0x22, 0x21 }));
        }

        [Test]
        public void LawyerElectionPdas_AreKeyedByTheListingIdAndTheU64Round()
        {
            // The vote record: [b"lawyer-vote", listing_id LE, round LE (u64), voter].
            var voter = SyntheticKey(1);
            var expectedVote = SolanaProgramAddress.Derive(
                new PublicKey(Programs.Marketplace),
                Encoding.UTF8.GetBytes("lawyer-vote"),
                BitConverter.GetBytes(7UL),
                BitConverter.GetBytes(0x0A0B0C0D0E0F1011UL),
                voter.KeyBytes);

            Assert.That(
                XcavateMarketplaceProgram.DeriveLawyerVote(Programs, 7, 0x0A0B0C0D0E0F1011, voter).Key,
                Is.EqualTo(expectedVote.Key));

            // The candidacy: [b"lawyer-candidate", listing_id LE, round LE (u64), lawyer].
            var lawyer = SyntheticKey(3);
            var expectedCandidacy = SolanaProgramAddress.Derive(
                new PublicKey(Programs.Marketplace),
                Encoding.UTF8.GetBytes("lawyer-candidate"),
                BitConverter.GetBytes(7UL),
                BitConverter.GetBytes(2UL),
                lawyer.KeyBytes);

            Assert.That(
                XcavateMarketplaceProgram.DeriveLawyerCandidacy(Programs, 7, 2, lawyer).Key,
                Is.EqualTo(expectedCandidacy.Key));
        }

        [Test]
        public void SharePriceDecimals_IsNine()
        {
            Assert.That(XcavateMarketplaceIndexerModel.SharePriceDecimals, Is.EqualTo(9));
        }

        [Test]
        public void SecondaryMarketInstructions_HaveTheIdlAccountShapes()
        {
            var seller = SyntheticKey(1);
            var payer = SyntheticKey(2);
            var buyer = SyntheticKey(3);
            var mint = SyntheticKey(4);
            var buyerPayment = SyntheticKey(5);
            var rentPayer = SyntheticKey(6);
            var treasury = SyntheticKey(7);
            var legacyToken = new PublicKey(SolanaTokenProgram.Legacy);
            var token2022 = new PublicKey(SolanaTokenProgram.Token2022);

            var relist = XcavateMarketplaceProgram.RelistShares(Programs, seller, payer, 7, 5, 10_000, 3);
            var delist = XcavateMarketplaceProgram.DelistShares(Programs, seller, rentPayer, 3, 7);
            var buyRelisted = XcavateMarketplaceProgram.BuyRelistedShares(
                Programs, buyer, payer, 7, 3, 5, 1_000, seller, rentPayer, mint, buyerPayment, treasury, legacyToken);

            Assert.Multiple(() =>
            {
                // relist: seller, payer, config, seller_role, listing, holding,
                // share_listing, system_program - 8 accounts, in IDL order.
                Assert.That(relist.Keys, Has.Count.EqualTo(8));
                Assert.That(relist.Keys[0].PublicKey, Is.EqualTo(seller.Key));
                Assert.That(relist.Keys[0].IsSigner, Is.True);
                Assert.That(relist.Keys[1].PublicKey, Is.EqualTo(payer.Key));
                Assert.That(relist.Keys[1].IsSigner, Is.True);
                Assert.That(relist.Keys[2].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveConfig(Programs).Key));
                Assert.That(relist.Keys[3].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveRoleAccount(Programs, seller, XcavateRole.RealEstateInvestor).Key));
                Assert.That(relist.Keys[4].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveListing(Programs, 7).Key));
                Assert.That(relist.Keys[5].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveHolding(Programs, 7, seller).Key));
                Assert.That(relist.Keys[6].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveShareListing(Programs, 3).Key));
                Assert.That(relist.Keys[7].PublicKey, Is.EqualTo(SystemProgram.ProgramIdKey.Key));

                // delist: seller, rent_payer, share_listing, holding - no args, no payer.
                Assert.That(delist.Keys, Has.Count.EqualTo(4));
                Assert.That(delist.Data, Has.Length.EqualTo(8));
                Assert.That(delist.Keys[0].PublicKey, Is.EqualTo(seller.Key));
                Assert.That(delist.Keys[0].IsSigner, Is.True);
                Assert.That(delist.Keys[1].PublicKey, Is.EqualTo(rentPayer.Key));
                Assert.That(delist.Keys[2].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveShareListing(Programs, 3).Key));
                Assert.That(delist.Keys[3].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveHolding(Programs, 7, seller).Key));

                // buy_relisted: the IDL's 29 accounts, in order.
                Assert.That(buyRelisted.Keys, Has.Count.EqualTo(29));
                Assert.That(buyRelisted.Keys[0].PublicKey, Is.EqualTo(buyer.Key));
                Assert.That(buyRelisted.Keys[0].IsSigner, Is.True);
                Assert.That(buyRelisted.Keys[1].PublicKey, Is.EqualTo(payer.Key));
                Assert.That(buyRelisted.Keys[1].IsSigner, Is.True);

                var shareMint = XcavateMarketplaceProgram.DeriveShareMint(Programs, 7);

                Assert.That(buyRelisted.Keys[2].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveRoleAccount(Programs, buyer, XcavateRole.RealEstateInvestor).Key));
                Assert.That(buyRelisted.Keys[3].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveConfig(Programs).Key));
                Assert.That(buyRelisted.Keys[4].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveListing(Programs, 7).Key));
                Assert.That(buyRelisted.Keys[5].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveProperty(Programs, 7).Key));
                Assert.That(buyRelisted.Keys[6].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveShareListing(Programs, 3).Key));
                Assert.That(buyRelisted.Keys[7].PublicKey, Is.EqualTo(seller.Key));
                Assert.That(buyRelisted.Keys[8].PublicKey, Is.EqualTo(rentPayer.Key));
                Assert.That(buyRelisted.Keys[9].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveHolding(Programs, 7, seller).Key));
                Assert.That(buyRelisted.Keys[10].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveHolding(Programs, 7, buyer).Key));
                Assert.That(buyRelisted.Keys[11].PublicKey, Is.EqualTo(mint.Key));
                Assert.That(buyRelisted.Keys[12].PublicKey, Is.EqualTo(buyerPayment.Key));
                Assert.That(buyRelisted.Keys[13].PublicKey, Is.EqualTo(
                    SolanaAssociatedTokenAccount.Derive(seller, mint, legacyToken).Key));
                Assert.That(buyRelisted.Keys[14].PublicKey, Is.EqualTo(treasury.Key));
                Assert.That(buyRelisted.Keys[15].PublicKey, Is.EqualTo(
                    SolanaAssociatedTokenAccount.Derive(treasury, mint, legacyToken).Key));
                Assert.That(buyRelisted.Keys[16].PublicKey, Is.EqualTo(shareMint.Key));
                Assert.That(buyRelisted.Keys[17].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveMintAuth(Programs, 7).Key));

                // The share ATAs derive with the Token-2022 program id, not legacy.
                Assert.That(buyRelisted.Keys[18].PublicKey, Is.EqualTo(
                    SolanaAssociatedTokenAccount.Derive(seller, shareMint, token2022).Key));
                Assert.That(buyRelisted.Keys[19].PublicKey, Is.EqualTo(
                    SolanaAssociatedTokenAccount.Derive(buyer, shareMint, token2022).Key));

                Assert.That(buyRelisted.Keys[20].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveCpiAuth(Programs).Key));
                Assert.That(buyRelisted.Keys[21].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveIncome(Programs, 7).Key));
                Assert.That(buyRelisted.Keys[22].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveIncomeCheckpoint(Programs, 7, seller).Key));
                Assert.That(buyRelisted.Keys[23].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveIncomeCheckpoint(Programs, 7, buyer).Key));
                Assert.That(buyRelisted.Keys[24].PublicKey, Is.EqualTo(Programs.Property));
                Assert.That(buyRelisted.Keys[25].PublicKey, Is.EqualTo(legacyToken.Key));
                Assert.That(buyRelisted.Keys[26].PublicKey, Is.EqualTo(token2022.Key));
                Assert.That(buyRelisted.Keys[27].PublicKey, Is.EqualTo(AssociatedTokenAccountProgram.ProgramIdKey.Key));
                Assert.That(buyRelisted.Keys[28].PublicKey, Is.EqualTo(SystemProgram.ProgramIdKey.Key));

                // Every account in the message is writable: the deployed binary escalates
                // the whole message (see the program class doc comment).
                foreach (var instruction in new[] { relist, delist, buyRelisted })
                {
                    foreach (var key in instruction.Keys.Where(key => !key.IsSigner))
                    {
                        Assert.That(key.IsWritable, Is.True);
                    }
                }

                Assert.That(delist.Keys.Count(key => key.IsSigner), Is.EqualTo(1));
            });
        }

        [Test]
        public void RelistShares_EncodesArgsLittleEndian()
        {
            var instruction = XcavateMarketplaceProgram.RelistShares(
                Programs,
                seller: SyntheticKey(1),
                payer: SyntheticKey(2),
                assetId: 0x0102030405060708,
                amount: 0x0A0B0C0D,
                sharePrice: 0x1112131415161718,
                shareListingId: 3);

            // discriminator + u64 asset_id + u32 amount + u64 share_price
            Assert.That(instruction.Data, Has.Length.EqualTo(8 + 8 + 4 + 8));
            Assert.That(instruction.Data[8..16], Is.EqualTo(new byte[] { 8, 7, 6, 5, 4, 3, 2, 1 }));
            Assert.That(instruction.Data[16..20], Is.EqualTo(new byte[] { 0x0D, 0x0C, 0x0B, 0x0A }));
            Assert.That(instruction.Data[20..28], Is.EqualTo(new byte[] { 0x18, 0x17, 0x16, 0x15, 0x14, 0x13, 0x12, 0x11 }));
        }

        [Test]
        public void BuyRelistedShares_EncodesArgsLittleEndian()
        {
            var instruction = XcavateMarketplaceProgram.BuyRelistedShares(
                Programs,
                buyer: SyntheticKey(1),
                payer: SyntheticKey(2),
                assetId: 0x0102030405060708,
                shareListingId: 0x1112131415161718,
                amount: 0x0A0B0C0D,
                maxTotalCost: 0x2122232425262728,
                seller: SyntheticKey(5),
                rentPayer: SyntheticKey(6),
                paymentMint: SyntheticKey(3),
                buyerPaymentAccount: SyntheticKey(4),
                treasury: SyntheticKey(7),
                paymentTokenProgram: new PublicKey(SolanaTokenProgram.Legacy));

            // discriminator + u64 asset_id + u64 id + u32 amount + u64 max_total_cost
            Assert.That(instruction.Data, Has.Length.EqualTo(8 + 8 + 8 + 4 + 8));
            Assert.That(instruction.Data[8..16], Is.EqualTo(new byte[] { 8, 7, 6, 5, 4, 3, 2, 1 }));
            Assert.That(instruction.Data[16..24], Is.EqualTo(new byte[] { 0x18, 0x17, 0x16, 0x15, 0x14, 0x13, 0x12, 0x11 }));
            Assert.That(instruction.Data[24..28], Is.EqualTo(new byte[] { 0x0D, 0x0C, 0x0B, 0x0A }));
            Assert.That(instruction.Data[28..36], Is.EqualTo(new byte[] { 0x28, 0x27, 0x26, 0x25, 0x24, 0x23, 0x22, 0x21 }));
        }

        [Test]
        public void SecondaryMarketPdas_DeriveUnderTheRightPrograms()
        {
            var owner = SyntheticKey(1);

            // The share listing: [b"share-listing", id LE] under the marketplace program.
            var expectedShareListing = SolanaProgramAddress.Derive(
                new PublicKey(Programs.Marketplace),
                Encoding.UTF8.GetBytes("share-listing"),
                BitConverter.GetBytes(3UL));

            Assert.That(
                XcavateMarketplaceProgram.DeriveShareListing(Programs, 3).Key,
                Is.EqualTo(expectedShareListing.Key));

            // The marketplace's own CPI signer, [b"cpi-auth"] under the marketplace
            // program - NOT the property program's same-named PDA.
            var expectedCpiAuth = SolanaProgramAddress.Derive(
                new PublicKey(Programs.Marketplace),
                Encoding.UTF8.GetBytes("cpi-auth"));

            Assert.That(
                XcavateMarketplaceProgram.DeriveCpiAuth(Programs).Key,
                Is.EqualTo(expectedCpiAuth.Key));
            Assert.That(
                XcavateMarketplaceProgram.DeriveCpiAuth(Programs).Key,
                Is.Not.EqualTo(XcavatePropertyProgram.DeriveCpiAuth(Programs).Key));

            // The income accumulator: [b"income", asset_id LE] under the PROPERTY program.
            var expectedIncome = SolanaProgramAddress.Derive(
                new PublicKey(Programs.Property),
                Encoding.UTF8.GetBytes("income"),
                BitConverter.GetBytes(7UL));

            Assert.That(
                XcavateMarketplaceProgram.DeriveIncome(Programs, 7).Key,
                Is.EqualTo(expectedIncome.Key));

            // The income checkpoint: [b"checkpoint", asset_id LE, owner] under the
            // PROPERTY program.
            var expectedCheckpoint = SolanaProgramAddress.Derive(
                new PublicKey(Programs.Property),
                Encoding.UTF8.GetBytes("checkpoint"),
                BitConverter.GetBytes(7UL),
                owner.KeyBytes);

            Assert.That(
                XcavateMarketplaceProgram.DeriveIncomeCheckpoint(Programs, 7, owner).Key,
                Is.EqualTo(expectedCheckpoint.Key));

            // Derivation is deterministic, and keyed by every argument.
            Assert.That(
                XcavateMarketplaceProgram.DeriveShareListing(Programs, 3).Key,
                Is.EqualTo(XcavateMarketplaceProgram.DeriveShareListing(Programs, 3).Key));
            Assert.That(
                XcavateMarketplaceProgram.DeriveShareListing(Programs, 4).Key,
                Is.Not.EqualTo(XcavateMarketplaceProgram.DeriveShareListing(Programs, 3).Key));
            Assert.That(
                XcavateMarketplaceProgram.DeriveIncomeCheckpoint(Programs, 7, SyntheticKey(2)).Key,
                Is.Not.EqualTo(XcavateMarketplaceProgram.DeriveIncomeCheckpoint(Programs, 7, owner).Key));
        }

        [Test]
        public void IncomePdas_DelegateToThePropertyProgram()
        {
            // The income/checkpoint seeds belong to the property program's own
            // instructions; the marketplace's settle-CPI builders reuse the property
            // class's derivations so the seed literals live in exactly one place.
            var owner = SyntheticKey(1);

            Assert.Multiple(() =>
            {
                Assert.That(
                    XcavateMarketplaceProgram.DeriveIncome(Programs, 7).Key,
                    Is.EqualTo(XcavatePropertyProgram.DeriveIncome(Programs, 7).Key));
                Assert.That(
                    XcavateMarketplaceProgram.DeriveIncomeCheckpoint(Programs, 7, owner).Key,
                    Is.EqualTo(XcavatePropertyProgram.DeriveIncomeCheckpoint(Programs, 7, owner).Key));
            });
        }

        [Test]
        public void SendPropertyShares_HasTheIdlAccountShape()
        {
            var sender = SyntheticKey(1);
            var payer = SyntheticKey(2);
            var receiver = SyntheticKey(3);
            var token2022 = new PublicKey(SolanaTokenProgram.Token2022);

            var send = XcavateMarketplaceProgram.SendPropertyShares(
                Programs, sender, payer, 7, receiver, 5);

            Assert.Multiple(() =>
            {
                // sender, payer, sender_role, receiver, receiver_role, listing, property,
                // sender_holding, receiver_holding, share_mint, mint_auth,
                // sender_share_account, receiver_share_account, cpi_auth, income,
                // sender_checkpoint, receiver_checkpoint, property_program,
                // share_token_program, associated_token_program, system_program -
                // the IDL's 21 accounts, in order.
                Assert.That(send.Keys, Has.Count.EqualTo(21));
                Assert.That(send.Keys[0].PublicKey, Is.EqualTo(sender.Key));
                Assert.That(send.Keys[0].IsSigner, Is.True);
                Assert.That(send.Keys[1].PublicKey, Is.EqualTo(payer.Key));
                Assert.That(send.Keys[1].IsSigner, Is.True);
                Assert.That(send.Keys.Count(key => key.IsSigner), Is.EqualTo(2));

                Assert.That(send.Keys[2].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveRoleAccount(Programs, sender, XcavateRole.RealEstateInvestor).Key));
                Assert.That(send.Keys[3].PublicKey, Is.EqualTo(receiver.Key));
                Assert.That(send.Keys[4].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveRoleAccount(Programs, receiver, XcavateRole.RealEstateInvestor).Key));
                Assert.That(send.Keys[5].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveListing(Programs, 7).Key));
                Assert.That(send.Keys[6].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveProperty(Programs, 7).Key));
                Assert.That(send.Keys[7].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveHolding(Programs, 7, sender).Key));
                Assert.That(send.Keys[8].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveHolding(Programs, 7, receiver).Key));

                var shareMint = XcavateMarketplaceProgram.DeriveShareMint(Programs, 7);

                Assert.That(send.Keys[9].PublicKey, Is.EqualTo(shareMint.Key));
                Assert.That(send.Keys[10].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveMintAuth(Programs, 7).Key));
                Assert.That(send.Keys[11].PublicKey, Is.EqualTo(
                    SolanaAssociatedTokenAccount.Derive(sender, shareMint, token2022).Key));
                Assert.That(send.Keys[12].PublicKey, Is.EqualTo(
                    SolanaAssociatedTokenAccount.Derive(receiver, shareMint, token2022).Key));
                Assert.That(send.Keys[13].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveCpiAuth(Programs).Key));
                Assert.That(send.Keys[14].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveIncome(Programs, 7).Key));
                Assert.That(send.Keys[15].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveIncomeCheckpoint(Programs, 7, sender).Key));
                Assert.That(send.Keys[16].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveIncomeCheckpoint(Programs, 7, receiver).Key));
                Assert.That(send.Keys[17].PublicKey, Is.EqualTo(Programs.Property));
                Assert.That(send.Keys[18].PublicKey, Is.EqualTo(token2022.Key));
                Assert.That(send.Keys[19].PublicKey, Is.EqualTo(AssociatedTokenAccountProgram.ProgramIdKey.Key));
                Assert.That(send.Keys[20].PublicKey, Is.EqualTo(SystemProgram.ProgramIdKey.Key));

                // Every account in the message is writable: the deployed binary escalates
                // the whole message (see the program class doc comment).
                foreach (var key in send.Keys.Where(key => !key.IsSigner))
                {
                    Assert.That(key.IsWritable, Is.True);
                }
            });
        }

        [Test]
        public void SendPropertyShares_EncodesArgsLittleEndian()
        {
            var instruction = XcavateMarketplaceProgram.SendPropertyShares(
                Programs,
                sender: SyntheticKey(1),
                payer: SyntheticKey(2),
                assetId: 0x0102030405060708,
                receiver: SyntheticKey(3),
                amount: 0x0A0B0C0D);

            // discriminator + u64 asset_id + u32 amount
            Assert.That(instruction.Data, Has.Length.EqualTo(8 + 8 + 4));
            Assert.That(instruction.Data[8..16], Is.EqualTo(new byte[] { 8, 7, 6, 5, 4, 3, 2, 1 }));
            Assert.That(instruction.Data[16..20], Is.EqualTo(new byte[] { 0x0D, 0x0C, 0x0B, 0x0A }));
        }

        [Test]
        public void CrankInstructions_HaveTheIdlAccountShapes()
        {
            var cranker = SyntheticKey(1);
            var rentCollector = SyntheticKey(2);
            var owner = SyntheticKey(3);
            var developerLawyer = SyntheticKey(4);
            var spvLawyer = SyntheticKey(5);
            var xcavMint = SyntheticKey(6);
            var developer = SyntheticKey(7);
            var xcavToken = new PublicKey(SolanaTokenProgram.Token2022);
            var paymentToken = new PublicKey(SolanaTokenProgram.Legacy);

            var close = XcavateMarketplaceProgram.CloseShareHolding(Programs, cranker, rentCollector, 7, owner);
            var finalize = XcavateMarketplaceProgram.FinalizeSpvElection(Programs, cranker, 7, 2, null, []);
            var resolve = XcavateMarketplaceProgram.ResolveSilentVerdict(Programs, cranker, 7);
            var execute = XcavateMarketplaceProgram.ExecuteDeal(
                Programs, cranker, 7, 3, developerLawyer, spvLawyer, xcavMint, developer, xcavToken, paymentToken,
                developer, SyntheticKey(8), []);

            Assert.Multiple(() =>
            {
                // close_share_holding: cranker, config, rent_collector, property, holding
                // - no instruction args, so the data is the bare discriminator.
                Assert.That(close.Keys, Has.Count.EqualTo(5));
                Assert.That(close.Data, Has.Length.EqualTo(8));
                Assert.That(close.Keys[0].PublicKey, Is.EqualTo(cranker.Key));
                Assert.That(close.Keys[0].IsSigner, Is.True);
                Assert.That(close.Keys[1].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveConfig(Programs).Key));
                Assert.That(close.Keys[2].PublicKey, Is.EqualTo(rentCollector.Key));
                Assert.That(close.Keys[3].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveProperty(Programs, 7).Key));
                Assert.That(close.Keys[4].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveHolding(Programs, 7, owner).Key));

                // finalize_spv_election: cranker, listing, property, winner_registry -
                // with the round's candidacy PDAs trailing as remaining accounts
                // (empty here, asserted separately below).
                Assert.That(finalize.Keys, Has.Count.EqualTo(4));
                Assert.That(finalize.Keys[0].PublicKey, Is.EqualTo(cranker.Key));
                Assert.That(finalize.Keys[0].IsSigner, Is.True);
                Assert.That(finalize.Keys[1].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveListing(Programs, 7).Key));
                Assert.That(finalize.Keys[2].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveProperty(Programs, 7).Key));

                // winner_registry is optional: no winner encodes None as the program's
                // own id, a determined winner keys their registry entry.
                Assert.That(finalize.Keys[3].PublicKey, Is.EqualTo(new PublicKey(Programs.Marketplace).Key));

                var winner = SyntheticKey(8);
                var finalizeWithWinner = XcavateMarketplaceProgram.FinalizeSpvElection(Programs, cranker, 7, 2, winner, [winner]);
                Assert.That(finalizeWithWinner.Keys[3].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveLawyer(Programs, winner).Key));

                // resolve_silent_verdict: cranker, listing.
                Assert.That(resolve.Keys, Has.Count.EqualTo(2));
                Assert.That(resolve.Keys[0].PublicKey, Is.EqualTo(cranker.Key));
                Assert.That(resolve.Keys[0].IsSigner, Is.True);
                Assert.That(resolve.Keys[1].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveListing(Programs, 7).Key));

                // execute_deal: cranker, config, listing, property, region,
                // developer_lawyer_registry, spv_lawyer_registry, listing_vault,
                // xcav_mint, developer_token, vault, token_program,
                // payment_token_program - the IDL's 13 accounts, in order.
                Assert.That(execute.Keys, Has.Count.EqualTo(13));
                Assert.That(execute.Keys[0].PublicKey, Is.EqualTo(cranker.Key));
                Assert.That(execute.Keys[0].IsSigner, Is.True);
                Assert.That(execute.Keys[1].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveConfig(Programs).Key));
                Assert.That(execute.Keys[2].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveListing(Programs, 7).Key));
                Assert.That(execute.Keys[3].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveProperty(Programs, 7).Key));
                Assert.That(execute.Keys[4].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveRegion(Programs, 3).Key));
                Assert.That(execute.Keys[5].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveLawyer(Programs, developerLawyer).Key));
                Assert.That(execute.Keys[6].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveLawyer(Programs, spvLawyer).Key));
                Assert.That(execute.Keys[7].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveListingVault(Programs, 7).Key));
                Assert.That(execute.Keys[8].PublicKey, Is.EqualTo(xcavMint.Key));
                Assert.That(execute.Keys[9].PublicKey, Is.EqualTo(
                    SolanaAssociatedTokenAccount.Derive(developer, xcavMint, xcavToken).Key));
                Assert.That(execute.Keys[10].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveVault(Programs).Key));
                Assert.That(execute.Keys[11].PublicKey, Is.EqualTo(xcavToken.Key));
                Assert.That(execute.Keys[12].PublicKey, Is.EqualTo(paymentToken.Key));

                // The cranks are permissionless - the sole signer is the cranker - and
                // every account in the message is writable: the deployed binary escalates
                // the whole message (see the program class doc comment).
                foreach (var instruction in new[] { close, finalize, resolve, execute })
                {
                    Assert.That(instruction.Keys.Count(key => key.IsSigner), Is.EqualTo(1));
                    foreach (var key in instruction.Keys.Where(key => !key.IsSigner))
                    {
                        Assert.That(key.IsWritable, Is.True);
                    }
                }
            });
        }

        [Test]
        public void FinalizeSpvElection_EncodesArgsLittleEndian()
        {
            var instruction = XcavateMarketplaceProgram.FinalizeSpvElection(
                Programs,
                cranker: SyntheticKey(1),
                listingId: 0x0102030405060708,
                round: 2,
                winner: null,
                candidates: []);

            // discriminator + u64 listing_id (the election round feeds the candidacy
            // remaining-account derivations only - it is not an instruction argument)
            Assert.That(instruction.Data, Has.Length.EqualTo(8 + 8));
            Assert.That(instruction.Data[8..16], Is.EqualTo(new byte[] { 8, 7, 6, 5, 4, 3, 2, 1 }));
        }

        /// <summary>
        /// marketplace/src/instructions/election.rs finalize_spv_election_handler requires
        /// every (unique) candidacy of the round as a trailing remaining account
        /// ("remaining_accounts.len() == election.candidate_count"), each checked against
        /// its (listing_id, round) seeds; the property program's finalize_agent_election
        /// takes the same shape.
        /// </summary>
        [Test]
        public void FinalizeSpvElection_AppendsTheRoundsCandidaciesAsRemainingAccounts()
        {
            var cranker = SyntheticKey(1);
            var winner = SyntheticKey(2);
            var runnerUp = SyntheticKey(3);

            var finalize = XcavateMarketplaceProgram.FinalizeSpvElection(
                Programs, cranker, 7, 2, winner, [winner, runnerUp]);

            Assert.Multiple(() =>
            {
                Assert.That(finalize.Keys, Has.Count.EqualTo(4 + 2));

                Assert.That(finalize.Keys[0].PublicKey, Is.EqualTo(cranker.Key));
                Assert.That(finalize.Keys[0].IsSigner, Is.True);
                Assert.That(finalize.Keys.Count(key => key.IsSigner), Is.EqualTo(1));
                Assert.That(finalize.Keys[1].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveListing(Programs, 7).Key));
                Assert.That(finalize.Keys[2].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveProperty(Programs, 7).Key));
                Assert.That(finalize.Keys[3].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveLawyer(Programs, winner).Key));

                // Remaining accounts: the candidacy PDAs in the order the caller listed
                // the lawyers, keyed by [b"lawyer-candidate", listing_id, round, lawyer].
                Assert.That(finalize.Keys[4].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveLawyerCandidacy(Programs, 7, 2, winner).Key));
                Assert.That(finalize.Keys[5].PublicKey, Is.EqualTo(
                    XcavateMarketplaceProgram.DeriveLawyerCandidacy(Programs, 7, 2, runnerUp).Key));

                foreach (var key in finalize.Keys)
                {
                    Assert.That(key.IsWritable, Is.True);
                }

                // A no-winner finalization (tie at the top) still carries the candidacies;
                // the registry slot encodes Anchor's None as the program's own id.
                var tied = XcavateMarketplaceProgram.FinalizeSpvElection(
                    Programs, cranker, 7, 2, null, [winner, runnerUp]);
                Assert.That(tied.Keys, Has.Count.EqualTo(4 + 2));
                Assert.That(tied.Keys[3].PublicKey, Is.EqualTo(new PublicKey(Programs.Marketplace).Key));
            });
        }

        [Test]
        public void ResolveSilentVerdict_EncodesArgsLittleEndian()
        {
            var instruction = XcavateMarketplaceProgram.ResolveSilentVerdict(
                Programs,
                cranker: SyntheticKey(1),
                listingId: 0x0102030405060708);

            // discriminator + u64 listing_id
            Assert.That(instruction.Data, Has.Length.EqualTo(8 + 8));
            Assert.That(instruction.Data[8..16], Is.EqualTo(new byte[] { 8, 7, 6, 5, 4, 3, 2, 1 }));
        }

        [Test]
        public void ExecuteDeal_EncodesArgsLittleEndian()
        {
            var instruction = XcavateMarketplaceProgram.ExecuteDeal(
                Programs,
                cranker: SyntheticKey(1),
                listingId: 0x0102030405060708,
                regionId: 3,
                developerLawyer: SyntheticKey(2),
                spvLawyer: SyntheticKey(3),
                xcavMint: SyntheticKey(4),
                developer: SyntheticKey(5),
                xcavTokenProgram: new PublicKey(SolanaTokenProgram.Token2022),
                paymentTokenProgram: new PublicKey(SolanaTokenProgram.Legacy),
                regionOwner: SyntheticKey(6),
                treasury: SyntheticKey(7),
                collectedMints: []);

            // discriminator + u64 listing_id
            Assert.That(instruction.Data, Has.Length.EqualTo(8 + 8));
            Assert.That(instruction.Data[8..16], Is.EqualTo(new byte[] { 8, 7, 6, 5, 4, 3, 2, 1 }));
        }

        /// <summary>
        /// marketplace/src/instructions/settlement.rs execute_deal_handler requires
        /// "remaining_accounts.len() == listing.collected.len() * 7": one
        /// (mint, vault account, developer, developer's lawyer, SPV lawyer, region owner,
        /// treasury) token-account group per collected payment mint, in the listing's
        /// collected order. The payee accounts only have to match (owner, mint) - the
        /// associated account is the fall-through default, derived here with the mint's
        /// own token program (each mint must sit on token_program or
        /// payment_token_program).
        /// </summary>
        [Test]
        public void ExecuteDeal_AppendsOneTokenGroupPerCollectedMintInCollectedOrder()
        {
            var cranker = SyntheticKey(1);
            var developerLawyer = SyntheticKey(2);
            var spvLawyer = SyntheticKey(3);
            var xcavMint = SyntheticKey(4);
            var developer = SyntheticKey(5);
            var regionOwner = SyntheticKey(6);
            var treasury = SyntheticKey(7);
            var usdc = SyntheticKey(8);
            var xcavSharesMint = SyntheticKey(9);
            var legacy = new PublicKey(SolanaTokenProgram.Legacy);
            var token2022 = new PublicKey(SolanaTokenProgram.Token2022);

            // Two collected mints on different token programs, the second the mint's own
            // Token-2022 program - the per-mint ATA derivations follow the mint's owner.
            var execute = XcavateMarketplaceProgram.ExecuteDeal(
                Programs, cranker, 7, 3, developerLawyer, spvLawyer, xcavMint, developer,
                token2022, legacy, regionOwner, treasury,
                [
                    new XcavateMarketplaceProgram.CollectedMint(usdc, legacy),
                    new XcavateMarketplaceProgram.CollectedMint(xcavSharesMint, token2022),
                ]);

            var listingVault = XcavateMarketplaceProgram.DeriveListingVault(Programs, 7);

            Assert.Multiple(() =>
            {
                Assert.That(execute.Keys, Has.Count.EqualTo(13 + 7 + 7));

                foreach (var (index, mint, program) in new[]
                {
                    (0, usdc, legacy),
                    (1, xcavSharesMint, token2022),
                })
                {
                    var offset = 13 + index * 7;
                    Assert.That(execute.Keys[offset].PublicKey, Is.EqualTo(mint.Key));
                    Assert.That(execute.Keys[offset + 1].PublicKey, Is.EqualTo(
                        SolanaAssociatedTokenAccount.Derive(listingVault, mint, program).Key));
                    Assert.That(execute.Keys[offset + 2].PublicKey, Is.EqualTo(
                        SolanaAssociatedTokenAccount.Derive(developer, mint, program).Key));
                    Assert.That(execute.Keys[offset + 3].PublicKey, Is.EqualTo(
                        SolanaAssociatedTokenAccount.Derive(developerLawyer, mint, program).Key));
                    Assert.That(execute.Keys[offset + 4].PublicKey, Is.EqualTo(
                        SolanaAssociatedTokenAccount.Derive(spvLawyer, mint, program).Key));
                    Assert.That(execute.Keys[offset + 5].PublicKey, Is.EqualTo(
                        SolanaAssociatedTokenAccount.Derive(regionOwner, mint, program).Key));
                    Assert.That(execute.Keys[offset + 6].PublicKey, Is.EqualTo(
                        SolanaAssociatedTokenAccount.Derive(treasury, mint, program).Key));
                }

                Assert.That(execute.Keys.Count(key => key.IsSigner), Is.EqualTo(1));
                foreach (var key in execute.Keys.Where(key => !key.IsSigner))
                {
                    Assert.That(key.IsWritable, Is.True);
                }
            });
        }

        [Test]
        public void DealSettlementPdas_DeriveUnderTheRightPrograms()
        {
            var lawyer = SyntheticKey(1);

            // The lawyer registry entry: [b"lawyer", lawyer] under the marketplace program.
            var expectedLawyer = SolanaProgramAddress.Derive(
                new PublicKey(Programs.Marketplace),
                Encoding.UTF8.GetBytes("lawyer"),
                lawyer.KeyBytes);

            Assert.That(
                XcavateMarketplaceProgram.DeriveLawyer(Programs, lawyer).Key,
                Is.EqualTo(expectedLawyer.Key));

            // The protocol vault: [b"vault"] under the marketplace program.
            var expectedVault = SolanaProgramAddress.Derive(
                new PublicKey(Programs.Marketplace),
                Encoding.UTF8.GetBytes("vault"));

            Assert.That(
                XcavateMarketplaceProgram.DeriveVault(Programs).Key,
                Is.EqualTo(expectedVault.Key));

            // The region: [b"region", region_id LE (u16)] under the REGIONS program -
            // the one PDA owned by neither the marketplace nor the property program.
            var expectedRegion = SolanaProgramAddress.Derive(
                new PublicKey(Programs.Regions),
                Encoding.UTF8.GetBytes("region"),
                BitConverter.GetBytes((ushort)0x0102));

            Assert.That(
                XcavateMarketplaceProgram.DeriveRegion(Programs, 0x0102).Key,
                Is.EqualTo(expectedRegion.Key));

            // The location: [b"location", region_id LE (u16), postcode] under the REGIONS
            // program, next to the region it belongs to.
            var postcode = Encoding.UTF8.GetBytes("AB12CD");
            var expectedLocation = SolanaProgramAddress.Derive(
                new PublicKey(Programs.Regions),
                Encoding.UTF8.GetBytes("location"),
                BitConverter.GetBytes((ushort)0x0102),
                postcode);

            Assert.That(
                XcavateMarketplaceProgram.DeriveLocation(Programs, 0x0102, postcode).Key,
                Is.EqualTo(expectedLocation.Key));

            // Derivation is keyed by every argument.
            Assert.That(
                XcavateMarketplaceProgram.DeriveLawyer(Programs, SyntheticKey(2)).Key,
                Is.Not.EqualTo(XcavateMarketplaceProgram.DeriveLawyer(Programs, lawyer).Key));
            Assert.That(
                XcavateMarketplaceProgram.DeriveRegion(Programs, 0x0103).Key,
                Is.Not.EqualTo(XcavateMarketplaceProgram.DeriveRegion(Programs, 0x0102).Key));
            Assert.That(
                XcavateMarketplaceProgram.DeriveLocation(Programs, 0x0102, Encoding.UTF8.GetBytes("EF34GH")).Key,
                Is.Not.EqualTo(XcavateMarketplaceProgram.DeriveLocation(Programs, 0x0102, Encoding.UTF8.GetBytes("AB12CD")).Key));
        }

        [Test]
        public void ConfigPda_IsTheAddressTheIdlDeploymentUses()
        {
            // The devnet Config account the task pins, confirmed live via getAccountInfo.
            Assert.That(
                XcavateMarketplaceProgram.DeriveConfig(Programs).Key,
                Is.EqualTo("FvypPqraGoYskTM9jiUywfsdfTeoqkXg2RdPHuw5S7aK"));
        }

        [Test]
        [TestCase(10_400_000_000u, 9, 10_400_000_000u)]
        // The 6-decimal accepted mint: the cap scales down by the decimal difference.
        [TestCase(10_400_000_000u, 6, 10_400_000u)]
        // Scaling down rounds up, so the cap never lands under the program's charge.
        [TestCase(10_400_000_001u, 6, 10_400_001u)]
        public void ScaleToMintDecimals_ConvertsByDecimalCountAlone(ulong total, int mintDecimals, ulong expected)
        {
            Assert.That(
                XcavateMarketplaceCallsModel.ScaleToMintDecimals(total, mintDecimals),
                Is.EqualTo(expected));
        }

        [Test]
        [TestCase(1_000_000u, 10u, 100, 300, false, 10_400_000u)]
        [TestCase(1_000_000u, 10u, 100, 300, true, 10_100_000u)]
        [TestCase(1_000_000u, 10u, 0, 0, false, 10_000_000u)]
        // Sub-bps remainders round up, never down: the cap must cover a program that
        // rounds either way.
        [TestCase(1u, 1u, 1, 0, false, 2u)]
        public void ComputeMaxTotalCost_AddsFeeAndTaxRoundedUp(
            uint sharePrice, uint amount, int investorFeeBps, int taxBps, bool taxPaidByDeveloper, uint expected)
        {
            var total = XcavateMarketplaceCallsModel.ComputeMaxTotalCost(
                sharePrice, amount, investorFeeBps, taxBps, taxPaidByDeveloper);

            Assert.That(total, Is.EqualTo(expected));
        }

        [Test]
        public async Task ConfigPda_ExistsOnDevnetUnderTheMarketplaceProgramAsync()
        {
            var config = XcavateMarketplaceProgram.DeriveConfig(Programs);

            var accountInfo = await SolanaRpcModel.GetAccountInfoAsync(
                SolanaCluster.Devnet, config.Key, CancellationToken.None);

            Assert.That(accountInfo, Is.Not.Null, $"No account at derived config PDA {config.Key}");
            Assert.That(accountInfo!.Owner, Is.EqualTo(Programs.Marketplace));
        }

        [Test]
        public async Task RoleAccountPda_ExistsOnDevnetUnderTheWhitelistProgramAsync()
        {
            // Pins the ROLE_SEED layout ("role", user, role-variant-byte) against the
            // deployed program: this wallet's RealEstateInvestor assignment must sit at
            // the derived address.
            var roleAccount = XcavateMarketplaceProgram.DeriveRoleAccount(
                Programs, new PublicKey(KnownInvestor), XcavateRole.RealEstateInvestor);

            var accountInfo = await SolanaRpcModel.GetAccountInfoAsync(
                SolanaCluster.Devnet, roleAccount.Key, CancellationToken.None);

            Assert.That(accountInfo, Is.Not.Null, $"No account at derived role PDA {roleAccount.Key}");
            Assert.That(accountInfo!.Owner, Is.EqualTo(Programs.Whitelist));
        }

        [Test]
        public void PickPaymentMint_PrefersAWhitelistedMintAndFallsBackToTheFirst()
        {
            // 8umv... (devnet tUSDC) is in the app's whitelist only when the host app
            // configured it; in this test host nothing is configured, so the first
            // accepted mint wins.
            var accepted = "[\"71G3dc4B9p9QBosLx3XhWY3ULRPAxjopngsin66M9HUb\",\"8umv4NXybZFGiT3tQb1DqJ6DXxLa3rLNhPbcqbQsjXxW\"]";

            var configured = PlutoFrameworkCore.PlutoConfigurationModel.WhitelistedSolanaTokens;

            try
            {
                PlutoFrameworkCore.PlutoConfigurationModel.WhitelistedSolanaTokens = [];

                Assert.That(
                    XcavateMarketplaceCallsModel.PickPaymentMint(SolanaCluster.Devnet, accepted).Key,
                    Is.EqualTo("71G3dc4B9p9QBosLx3XhWY3ULRPAxjopngsin66M9HUb"));

                PlutoFrameworkCore.PlutoConfigurationModel.WhitelistedSolanaTokens =
                [
                    new SolanaTokenWhitelistEntry
                    {
                        Cluster = SolanaCluster.Devnet,
                        Mint = "8umv4NXybZFGiT3tQb1DqJ6DXxLa3rLNhPbcqbQsjXxW",
                        Symbol = "tUSDC",
                        Name = "test USD Coin",
                        Decimals = 6,
                    },
                ];

                Assert.That(
                    XcavateMarketplaceCallsModel.PickPaymentMint(SolanaCluster.Devnet, accepted).Key,
                    Is.EqualTo("8umv4NXybZFGiT3tQb1DqJ6DXxLa3rLNhPbcqbQsjXxW"));
            }
            finally
            {
                PlutoFrameworkCore.PlutoConfigurationModel.WhitelistedSolanaTokens = configured;
            }
        }
    }
}
