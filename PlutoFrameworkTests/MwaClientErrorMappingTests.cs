using PlutoFrameworkCore.Solana.Mwa;

namespace PlutoFrameworkTests
{
    /// <summary>
    /// The association URI negotiates Mobile Wallet Adapter 2.0 (see
    /// MwaAssociationUriTests), so wallet errors must be decoded with the 2.0 table:
    /// -4 is "signed but could not submit", not 1.0's "too many payloads". A wallet-side
    /// connection failure on submission has to surface as itself - mislabelled, it
    /// bypasses the blockhash-expiry retry and misinforms the user.
    /// </summary>
    public class MwaClientErrorMappingTests
    {
        [Test]
        public void NotSubmittedSurfacesTheWalletsReasonVerbatim()
        {
            var exception = MwaClient.MapError("sign_and_send_transactions", -4, "Connection failure");

            Assert.Multiple(() =>
            {
                Assert.That(exception, Is.TypeOf<MwaNotSubmittedException>());
                Assert.That(exception.Message, Does.Contain("could not submit"));
                Assert.That(exception.Message, Does.Contain("Connection failure"));
            });
        }

        [Test]
        public void NotSubmittedIsNotMisreadAsTooManyPayloads()
        {
            // The 1.0 decoding this regression guards against: the report then claims a
            // payload-count problem that never happened.
            var exception = MwaClient.MapError("sign_and_send_transactions", -4, "Connection failure");

            Assert.That(exception.Message, Does.Not.Contain("Too many payloads"));
        }

        [Test]
        public void TooManyPayloadsIsMinusSix()
        {
            var exception = MwaClient.MapError("sign_messages", -6, "limit is 1");

            Assert.Multiple(() =>
            {
                Assert.That(exception, Is.TypeOf<MwaProtocolException>());
                Assert.That(exception, Is.Not.TypeOf<MwaAuthorizationException>());
                Assert.That(exception.Message, Does.Contain("Too many payloads"));
                Assert.That(exception.Message, Does.Contain("sign_messages"));
            });
        }

        [Test]
        public void ChainNotSupportedIsMinusSevenAndReadsAsAuthorization()
        {
            var exception = MwaClient.MapError("authorize", -7, "solana:mainnet");

            Assert.Multiple(() =>
            {
                Assert.That(exception, Is.TypeOf<MwaAuthorizationException>());
                Assert.That(exception.Message, Does.Contain("does not support"));
                Assert.That(exception.Message, Does.Contain("solana:mainnet"));
            });
        }

        [Test]
        public void AuthorizationFailedReadsAsAuthorization()
        {
            var exception = MwaClient.MapError("authorize", -1, "declined");

            Assert.That(exception, Is.TypeOf<MwaAuthorizationException>());
        }

        [Test]
        public void NotSignedReadsAsAuthorization()
        {
            var exception = MwaClient.MapError("sign_and_send_transactions", -3, "user declined");

            Assert.That(exception, Is.TypeOf<MwaAuthorizationException>());
        }

        [Test]
        public void InvalidPayloadsNamesTheMethod()
        {
            var exception = MwaClient.MapError("sign_messages", -2, "not a message");

            Assert.Multiple(() =>
            {
                Assert.That(exception, Is.TypeOf<MwaProtocolException>());
                Assert.That(exception.Message, Does.Contain("sign_messages"));
                Assert.That(exception.Message, Does.Contain("not a message"));
            });
        }

        [Test]
        public void NotClonedIsAPlainProtocolFault()
        {
            var exception = MwaClient.MapError("clone_authorization", -5, "no grant");

            Assert.Multiple(() =>
            {
                Assert.That(exception, Is.TypeOf<MwaProtocolException>());
                Assert.That(exception, Is.Not.TypeOf<MwaAuthorizationException>());
            });
        }

        [Test]
        public void UnknownCodesKeepCodeAndMessage()
        {
            var exception = MwaClient.MapError("authorize", -32601, "method not found");

            Assert.Multiple(() =>
            {
                Assert.That(exception, Is.TypeOf<MwaProtocolException>());
                Assert.That(exception.Message, Does.Contain("-32601"));
                Assert.That(exception.Message, Does.Contain("method not found"));
            });
        }
    }
}
