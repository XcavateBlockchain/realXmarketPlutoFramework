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
        [TestCase("create_spv")]
        [TestCase("withdraw_expired")]
        [TestCase("withdraw_cancelled")]
        [TestCase("withdraw_legal_process_expired")]
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
                "create_spv" => XcavateMarketplaceProgram.CreateSpv(Programs, SyntheticKey(1), 7),
                "withdraw_expired" => XcavateMarketplaceProgram.WithdrawExpired(
                    Programs, SyntheticKey(1), 7, SyntheticKey(2), SyntheticKey(3), SyntheticKey(4), new PublicKey(SolanaTokenProgram.Legacy)),
                "withdraw_cancelled" => XcavateMarketplaceProgram.WithdrawCancelled(
                    Programs, SyntheticKey(1), 7, SyntheticKey(2), SyntheticKey(3), SyntheticKey(4), new PublicKey(SolanaTokenProgram.Legacy)),
                "withdraw_legal_process_expired" => XcavateMarketplaceProgram.WithdrawLegalProcessExpired(
                    Programs, SyntheticKey(1), 7, SyntheticKey(2), SyntheticKey(3), SyntheticKey(4), new PublicKey(SolanaTokenProgram.Legacy)),
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
            var createSpv = XcavateMarketplaceProgram.CreateSpv(Programs, investor, 7);

            Assert.Multiple(() =>
            {
                Assert.That(buy.Keys, Has.Count.EqualTo(21));
                Assert.That(reserve.Keys, Has.Count.EqualTo(11));
                Assert.That(claim.Keys, Has.Count.EqualTo(22));
                Assert.That(withdraw.Keys, Has.Count.EqualTo(18));
                Assert.That(unreserve.Keys, Has.Count.EqualTo(4));
                Assert.That(createSpv.Keys, Has.Count.EqualTo(4));

                // The investor/confirmer leads every instruction and is its signer,
                // and every account in the message is writable: the deployed binary
                // escalates the whole message, so a readonly non-signer dies with
                // PrivilegeEscalation in simulation.
                foreach (var instruction in new[] { buy, reserve, claim, withdraw, unreserve, createSpv })
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
                Assert.That(createSpv.Keys.Count(key => key.IsSigner), Is.EqualTo(1));
            });
        }

        /// <summary>
        /// The regression behind "failed to sanitize accounts offsets": the compiled
        /// message's header requires two signature slots (investor plus the rent collector),
        /// and a wire transaction framed with fewer slots is rejected as malformed.
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

        [Test]
        [TestCase(10_400_000u, 6, 10_400_000u)]
        // The 9-decimal accepted mint: the cap scales up by the decimal difference.
        [TestCase(10_400_000u, 9, 10_400_000_000u)]
        // Scaling down rounds up, so the cap never lands under the program's charge.
        [TestCase(10_400_001u, 5, 1_040_001u)]
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
                    XcavateMarketplaceCallsModel.PickPaymentMint(accepted).Key,
                    Is.EqualTo("71G3dc4B9p9QBosLx3XhWY3ULRPAxjopngsin66M9HUb"));

                PlutoFrameworkCore.PlutoConfigurationModel.WhitelistedSolanaTokens =
                [
                    new SolanaTokenWhitelistEntry
                    {
                        Cluster = SolanaCluster.Devnet,
                        Mint = "8umv4NXybZFGiT3tQb1DqJ6DXxLa3rLNhPbcqbQsjXxW",
                        Symbol = "tUSDC",
                        Decimals = 6,
                    },
                ];

                Assert.That(
                    XcavateMarketplaceCallsModel.PickPaymentMint(accepted).Key,
                    Is.EqualTo("8umv4NXybZFGiT3tQb1DqJ6DXxLa3rLNhPbcqbQsjXxW"));
            }
            finally
            {
                PlutoFrameworkCore.PlutoConfigurationModel.WhitelistedSolanaTokens = configured;
            }
        }
    }
}
