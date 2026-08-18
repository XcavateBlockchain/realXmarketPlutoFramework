using PlutoFramework.Constants;
using PlutoFramework.Model.AjunaExt;
using PlutoFramework.Model.Xcavate;

namespace PlutoFrameworkTests
{
    internal class WhitelistModelTests
    {
        private SubstrateClientExt client;

        [SetUp]
        public async Task SetupAsync()
        {
            Endpoint endpoint = Endpoints.GetEndpointDictionary[EndpointEnum.XcavatePaseo];

            client = new SubstrateClientExt(
                endpoint,
                new Uri(endpoint.URLs[0]),
                Substrate.NetApi.Model.Extrinsics.ChargeTransactionPayment.Default());

            await client.ConnectAndLoadMetadataAsync();
            WhitelistModel.Clear();
        }

        [Test]
        [TestCase("12eQa8EazEoQngCfNqYHecjgWPwR81rUo1nRk7Y2vA9f1a1z")]
        public async Task GetRolesAsync_ReturnsRolesForAddressAsync(string address)
        {
            HashSet<XcavateRole> roles = await WhitelistModel.GetRolesAsync(
                (XcavatePaseo.NetApi.Generated.SubstrateClientExt)client.SubstrateClient,
                address,
                CancellationToken.None);

            Assert.That(roles, Is.Not.Null);
            Assert.That(roles, Is.Not.Empty);

            foreach (XcavateRole role in roles)
            {
                Console.WriteLine(role);
            }
        }
    }
}
