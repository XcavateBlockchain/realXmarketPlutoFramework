using System.Text.Json;
using System.Text.Json.Nodes;
using PlutoFrameworkCore.Solana.Mwa;

namespace PlutoFrameworkTests
{
    /// <summary>
    /// The two-signer marketplace flow signs with the wallet but submits itself, because a
    /// wallet whose own send fails can return a signature for a transaction it never
    /// broadcast (observed with Phantom 26.30.2 on devnet). That relies on the deprecated
    /// <c>sign_transactions</c> method's wire shape - payloads out, signed_payloads back -
    /// which these tests pin: a typo in either property name makes every sign-only request
    /// fail against a real wallet.
    /// </summary>
    public class MwaSignTransactionsWireTests
    {
        [Test]
        public void RequestSerializesPayloadsAsBase64()
        {
            var request = new MwaSignTransactionsRequest
            {
                Payloads = [Convert.ToBase64String([1, 2, 3]), Convert.ToBase64String([4, 5])],
            };

            var json = JsonSerializer.SerializeToNode(request)!.AsObject();

            Assert.Multiple(() =>
            {
                Assert.That(json.ContainsKey("payloads"), Is.True);
                Assert.That(json["payloads"]!.AsArray().Select(node => node!.GetValue<string>()),
                    Is.EqualTo(new[] { "AQID", "BAU=" }));
            });
        }

        [Test]
        public void ResponseReadsSignedPayloads()
        {
            var json = new JsonObject
            {
                ["signed_payloads"] = new JsonArray("AQID", "BAU="),
            };

            var response = json.Deserialize<MwaSignTransactionsResponse>();

            Assert.That(response!.SignedPayloads, Is.EqualTo(new[] { "AQID", "BAU=" }));
        }

        [Test]
        public void ResponseWithoutSignedPayloadsDeserializesToNull()
        {
            // SignTransactionsAsync turns this into a protocol exception rather than
            // submitting nothing and reporting success.
            var response = new JsonObject().Deserialize<MwaSignTransactionsResponse>();

            Assert.That(response!.SignedPayloads, Is.Null);
        }
    }
}
