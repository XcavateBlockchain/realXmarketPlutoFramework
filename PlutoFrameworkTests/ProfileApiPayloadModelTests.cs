using PlutoFrameworkCore.Xcavate;
using System.Globalization;
using System.Text;

namespace PlutoFrameworkTests
{
    internal class ProfileApiPayloadModelTests
    {
        private static readonly DateTime Now = new(2026, 7, 29, 12, 0, 0, DateTimeKind.Utc);

        private static string Timestamp(DateTime moment)
            => moment.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture);

        private static string FreshPayload(
            string method = "POST",
            string path = "/api/profiles",
            string hash = "0xFA8847B0C33183273F5945508B31C320")
            => $"{method}:{path}:{hash}:{Timestamp(Now)}";

        [Test]
        public void PostWithBodyHash_Matches()
        {
            Assert.That(ProfileApiPayloadModel.IsProfileApiSignPayload(FreshPayload(), Now), Is.True);
        }

        [Test]
        public void PutWithAddressPath_Matches()
        {
            var payload = FreshPayload(
                method: "PUT",
                path: "/api/profiles/5GrwvaEF5zKbXCEe9qGjZL23Y641mot2Ff6hS3s8jF3g3k3W",
                hash: "0x2937013F2181810606B2A799B05BDA28");

            Assert.That(ProfileApiPayloadModel.IsProfileApiSignPayload(payload, Now), Is.True);
        }

        [Test]
        public void DeleteWithEmptyBodyHash_Matches()
        {
            var payload = FreshPayload(method: "DELETE", hash: "");

            Assert.That(ProfileApiPayloadModel.IsProfileApiSignPayload(payload, Now), Is.True);
        }

        [Test]
        public void Utf8Bytes_MatchLikeTheString()
        {
            var bytes = Encoding.UTF8.GetBytes(FreshPayload());

            Assert.That(ProfileApiPayloadModel.IsProfileApiSignPayload(bytes, Now), Is.True);
        }

        [Test]
        public void TimestampInsideTolerance_Matches()
        {
            var stale = $"POST:/api/profiles:0xFA8847B0C33183273F5945508B31C320:{Timestamp(Now.AddMinutes(-9))}";
            var ahead = $"POST:/api/profiles:0xFA8847B0C33183273F5945508B31C320:{Timestamp(Now.AddMinutes(9))}";

            Assert.That(ProfileApiPayloadModel.IsProfileApiSignPayload(stale, Now), Is.True);
            Assert.That(ProfileApiPayloadModel.IsProfileApiSignPayload(ahead, Now), Is.True);
        }

        [Test]
        public void TimestampOutsideTolerance_DoesNotMatch()
        {
            var stale = $"POST:/api/profiles:0xFA8847B0C33183273F5945508B31C320:{Timestamp(Now.AddMinutes(-11))}";
            var future = $"POST:/api/profiles:0xFA8847B0C33183273F5945508B31C320:{Timestamp(Now.AddDays(30))}";

            Assert.That(ProfileApiPayloadModel.IsProfileApiSignPayload(stale, Now), Is.False);
            Assert.That(ProfileApiPayloadModel.IsProfileApiSignPayload(future, Now), Is.False);
        }

        [Test]
        public void MalformedBodyHash_DoesNotMatch()
        {
            // Lowercase hex, wrong length, and a missing 0x prefix in turn.
            Assert.That(ProfileApiPayloadModel.IsProfileApiSignPayload(
                FreshPayload(hash: "0xfa8847b0c33183273f5945508b31c320"), Now), Is.False);
            Assert.That(ProfileApiPayloadModel.IsProfileApiSignPayload(
                FreshPayload(hash: "0xFA8847B0C33183273F5945508B31C3"), Now), Is.False);
            Assert.That(ProfileApiPayloadModel.IsProfileApiSignPayload(
                FreshPayload(hash: "FA8847B0C33183273F5945508B31C320"), Now), Is.False);
        }

        [Test]
        public void UnknownOrLowercaseMethod_DoesNotMatch()
        {
            Assert.That(ProfileApiPayloadModel.IsProfileApiSignPayload(FreshPayload(method: "post"), Now), Is.False);
            Assert.That(ProfileApiPayloadModel.IsProfileApiSignPayload(FreshPayload(method: "FETCH"), Now), Is.False);
        }

        [Test]
        public void PathWithoutLeadingSlash_DoesNotMatch()
        {
            Assert.That(ProfileApiPayloadModel.IsProfileApiSignPayload(FreshPayload(path: "api/profiles"), Now), Is.False);
            Assert.That(ProfileApiPayloadModel.IsProfileApiSignPayload(FreshPayload(path: ""), Now), Is.False);
        }

        [Test]
        public void MillisecondTimestamp_DoesNotMatch()
        {
            // What JavaScript's toISOString() produces unpadded - the API pads to 7 digits.
            var payload = "POST:/api/profiles:0xFA8847B0C33183273F5945508B31C320:2026-07-29T12:00:00.123Z";

            Assert.That(ProfileApiPayloadModel.IsProfileApiSignPayload(payload, Now), Is.False);
        }

        [Test]
        public void OffsetOrImpossibleTimestamp_DoesNotMatch()
        {
            var offset = "POST:/api/profiles:0xFA8847B0C33183273F5945508B31C320:2026-07-29T12:00:00.0000000+00:00";
            var impossible = "POST:/api/profiles:0xFA8847B0C33183273F5945508B31C320:2026-13-29T12:00:00.0000000Z";

            Assert.That(ProfileApiPayloadModel.IsProfileApiSignPayload(offset, Now), Is.False);
            Assert.That(ProfileApiPayloadModel.IsProfileApiSignPayload(impossible, Now), Is.False);
        }

        [Test]
        public void SurroundingContent_DoesNotMatch()
        {
            Assert.That(ProfileApiPayloadModel.IsProfileApiSignPayload(FreshPayload() + "\n", Now), Is.False);
            Assert.That(ProfileApiPayloadModel.IsProfileApiSignPayload(" " + FreshPayload(), Now), Is.False);
            Assert.That(ProfileApiPayloadModel.IsProfileApiSignPayload(
                "Please sign in: " + FreshPayload(), Now), Is.False);
        }

        [Test]
        public void OrdinaryTextAndBinary_DoNotMatch()
        {
            Assert.That(ProfileApiPayloadModel.IsProfileApiSignPayload(
                "Sign this message to log in to Example.com", Now), Is.False);
            Assert.That(ProfileApiPayloadModel.IsProfileApiSignPayload(Array.Empty<byte>(), Now), Is.False);

            // Invalid UTF-8, like a transaction blob or a raw digest.
            Assert.That(ProfileApiPayloadModel.IsProfileApiSignPayload(
                new byte[] { 0x50, 0x4F, 0xFF, 0xFE, 0x00 }, Now), Is.False);
        }

        [Test]
        public void GraphqlPostPayload_Matches()
        {
            var payload = FreshPayload(path: "/graphql");

            Assert.That(ProfileApiPayloadModel.IsGraphqlPostPayload(Encoding.UTF8.GetBytes(payload)), Is.True);
        }

        [Test]
        public void GraphqlPostWithLooseTail_Matches()
        {
            // The prefix is the whole rule: a GraphQL signing string that the strict
            // Profile API check rejects still signs without any prompt.
            var payload = "POST:/graphql:0xfa8847b0c33183273f5945508b31c320:2026-07-29T12:00:00.123Z";

            Assert.That(ProfileApiPayloadModel.IsProfileApiSignPayload(payload, Now), Is.False);
            Assert.That(ProfileApiPayloadModel.IsGraphqlPostPayload(Encoding.UTF8.GetBytes(payload)), Is.True);
        }

        [Test]
        public void OtherGraphqlMethods_DoNotMatch()
        {
            foreach (var method in new[] { "GET", "PUT", "DELETE", "post", "Patch" })
            {
                var payload = Encoding.UTF8.GetBytes($"{method}:/graphql:0xA:2026-07-29T12:00:00.0000000Z");

                Assert.That(ProfileApiPayloadModel.IsGraphqlPostPayload(payload), Is.False);
            }
        }

        [Test]
        public void PostToOtherPath_DoesNotMatch()
        {
            var profiles = Encoding.UTF8.GetBytes(FreshPayload(path: "/api/profiles"));
            var graphqlWithoutColon = Encoding.UTF8.GetBytes("POST:/graphql-ops:0xA:2026-07-29T12:00:00.0000000Z");

            Assert.That(ProfileApiPayloadModel.IsGraphqlPostPayload(profiles), Is.False);
            Assert.That(ProfileApiPayloadModel.IsGraphqlPostPayload(graphqlWithoutColon), Is.False);
        }

        [Test]
        public void GraphqlPostEmptyOrBinary_DoesNotMatch()
        {
            Assert.That(ProfileApiPayloadModel.IsGraphqlPostPayload(Array.Empty<byte>()), Is.False);

            // The prefix's first two bytes, then invalid UTF-8.
            Assert.That(ProfileApiPayloadModel.IsGraphqlPostPayload(
                new byte[] { 0x50, 0x4F, 0xFF, 0xFE, 0x00 }), Is.False);
        }
    }
}
