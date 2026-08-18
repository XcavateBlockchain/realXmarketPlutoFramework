using PlutoFrameworkCore.PushNotificationServices.Core.Utils;

namespace PlutoFrameworkTests
{
    public class PlayIntegrityNonceTests
    {
        /// <summary>
        /// The API's docs/client-integration.md: the nonce handed to Play Integrity must be
        /// padded to a multiple of 4, because Google echoes the string back verbatim and the
        /// server decodes that echo as strict base64. The server issues 43-character
        /// (32-byte, unpadded) nonces, so this is the case every registration hits.
        /// </summary>
        [Test]
        public void PadsServerIssuedNonceToAMultipleOfFour()
        {
            var padded = PlayIntegrityNonce.Pad("0Ecn-tT6_XuyJLVzCKqvHlNryxiK-xORztqm1JUcnjo");

            Assert.That(padded, Is.EqualTo("0Ecn-tT6_XuyJLVzCKqvHlNryxiK-xORztqm1JUcnjo="));
        }

        [Test]
        public void PadsLengthTwoRemainderWithTwoCharacters()
        {
            Assert.That(PlayIntegrityNonce.Pad("ab"), Is.EqualTo("ab=="));
        }

        [Test]
        public void LeavesAlignedNonceUntouched()
        {
            Assert.That(PlayIntegrityNonce.Pad("abcd"), Is.EqualTo("abcd"));
        }

        [Test]
        public void LeavesAlreadyPaddedNonceUntouched()
        {
            Assert.That(PlayIntegrityNonce.Pad("abc="), Is.EqualTo("abc="));
        }
    }
}
