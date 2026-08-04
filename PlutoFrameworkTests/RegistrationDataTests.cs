using System.Text.Json;
using PlutoFrameworkCore.PushNotificationServices.Api.ApiEndpoints;

namespace PlutoFrameworkTests
{
    public class RegistrationDataTests
    {
        /// <summary>
        /// The response the API documents for GET /api/user/registration/. Its fields are
        /// snake_case, which no default naming policy produces, so the mapping is worth
        /// pinning rather than trusting.
        /// </summary>
        private const string DocumentedResponse = """
            {
              "device_id": "abc-123",
              "platform": "android",
              "uid": "customer-42",
              "notifications_enabled": true,
              "wallets": [
                {
                  "chain": "solana",
                  "address": "9xQe",
                  "verified": true,
                  "linked_at": "2026-08-04T10:12:33.248146Z"
                }
              ]
            }
            """;

        [Test]
        public void DeserializesTheDocumentedResponse()
        {
            var data = JsonSerializer.Deserialize<RegistrationData>(DocumentedResponse);

            Assert.That(data, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(data!.DeviceId, Is.EqualTo("abc-123"));
                Assert.That(data.Platform, Is.EqualTo("android"));
                Assert.That(data.Uid, Is.EqualTo("customer-42"));
                Assert.That(data.NotificationsEnabled, Is.True);
                Assert.That(data.Wallets, Has.Count.EqualTo(1));
            });

            var wallet = data!.Wallets[0];

            Assert.Multiple(() =>
            {
                Assert.That(wallet.Chain, Is.EqualTo("solana"));
                Assert.That(wallet.Address, Is.EqualTo("9xQe"));
                Assert.That(wallet.Verified, Is.True);
                Assert.That(wallet.LinkedAt, Is.EqualTo(
                    new DateTimeOffset(2026, 8, 4, 10, 12, 33, TimeSpan.Zero).AddTicks(2481460)));
            });
        }

        /// <summary>
        /// A device with nothing set up answers with nulls and an empty list. This is read
        /// to diagnose exactly that device, so it must survive rather than throw.
        /// </summary>
        [Test]
        public void DeserializesAnUnconfiguredDevice()
        {
            var data = JsonSerializer.Deserialize<RegistrationData>("""
                {
                  "device_id": "abc-123",
                  "platform": "ios",
                  "uid": null,
                  "notifications_enabled": false,
                  "wallets": []
                }
                """);

            Assert.That(data, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(data!.Uid, Is.Null);
                Assert.That(data.NotificationsEnabled, Is.False);
                Assert.That(data.Wallets, Is.Empty);
            });
        }

        /// <summary>
        /// Fields the client does not know about must not fail the read - the page shows
        /// what it recognises and stays useful against a newer server.
        /// </summary>
        [Test]
        public void IgnoresUnknownFields()
        {
            var data = JsonSerializer.Deserialize<RegistrationData>("""
                {
                  "device_id": "abc-123",
                  "something_new": {"nested": 1},
                  "wallets": [{"chain": "polkadot", "address": "5Grw", "verified": false}]
                }
                """);

            Assert.That(data, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(data!.DeviceId, Is.EqualTo("abc-123"));
                Assert.That(data.Wallets, Has.Count.EqualTo(1));
                Assert.That(data.Wallets[0].Verified, Is.False);
                Assert.That(data.Wallets[0].LinkedAt, Is.Null);
            });
        }

        /// <summary>
        /// A response missing <c>wallets</c> entirely leaves an empty list rather than a
        /// null the UI would have to guard on every read.
        /// </summary>
        [Test]
        public void DefaultsWalletsToEmpty()
        {
            var data = JsonSerializer.Deserialize<RegistrationData>("""{"device_id": "abc-123"}""");

            Assert.That(data, Is.Not.Null);
            Assert.That(data!.Wallets, Is.Empty);
        }
    }
}
