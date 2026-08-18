using PlutoFramework.Model.Xcavate;

namespace PlutoFrameworkTests
{
    /// <summary>
    /// Hits the live Xcavate devnet indexer. The addresses below are devnet whitelist holders,
    /// so a failure here is as likely to mean "devnet was reset" as "the model is broken".
    /// </summary>
    internal class WhitelistModelTests
    {
        [SetUp]
        public void Setup()
        {
            WhitelistModel.Clear();
        }

        [Test]
        [TestCase("EJpEpZ8rQY5gVkv6exjZ2urQpPwF6BS6RTaE4UzvhhsF", XcavateRole.RealEstateInvestor)]
        [TestCase("H7VMSTwha14CzNvuo9GQ1WrhfJhqrHeNwfcrRwuv7iDz", XcavateRole.RealEstateDeveloper)]
        [TestCase("3krQjt3Nsb3qKwLWdatXN1kUV9p7fP6WrwWYvUHHwVGu", XcavateRole.SpvConfirmation)]
        public async Task GetRolesAsync_ReturnsRolesForAddressAsync(string address, XcavateRole expected)
        {
            HashSet<XcavateRole> roles = await WhitelistModel.GetRolesAsync(address, CancellationToken.None);

            Assert.That(roles, Is.Not.Null);
            Assert.That(roles, Does.Contain(expected));

            foreach (XcavateRole role in roles)
            {
                Console.WriteLine(role);
            }
        }

        [Test]
        public async Task GetRolesAsync_ReturnsEmptyForAddressWithNoRolesAsync()
        {
            // The system program's address. It exists on chain and is never whitelisted, so an
            // empty result here is a real answer rather than a lookup that silently failed.
            HashSet<XcavateRole> roles = await WhitelistModel.GetRolesAsync(
                "11111111111111111111111111111111",
                CancellationToken.None);

            Assert.That(roles, Is.Empty);
        }

        [Test]
        [TestCase("EJpEpZ8rQY5gVkv6exjZ2urQpPwF6BS6RTaE4UzvhhsF", XcavateRole.RealEstateInvestor, true)]
        [TestCase("EJpEpZ8rQY5gVkv6exjZ2urQpPwF6BS6RTaE4UzvhhsF", XcavateRole.Lawyer, false)]
        // Not a role the Solana whitelist program knows about, so it is answered without a
        // query rather than sent to the indexer as an unknown enum value.
        [TestCase("EJpEpZ8rQY5gVkv6exjZ2urQpPwF6BS6RTaE4UzvhhsF", XcavateRole.ModuleCreator, false)]
        public async Task HasRoleAsync_MatchesTheRoleSetAsync(string address, XcavateRole role, bool expected)
        {
            bool hasRole = await WhitelistModel.HasRoleAsync(address, role, CancellationToken.None);

            Assert.That(hasRole, Is.EqualTo(expected));
        }
    }
}
