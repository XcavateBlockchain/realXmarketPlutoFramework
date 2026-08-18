using System.Net;
using System.Text;
using PlutoFrameworkCore.PushNotificationServices.Api;

namespace PlutoFrameworkTests
{
    public class HttpResponseBodyErrorTests
    {
        [Test]
        public async Task ReturnsTheResponseUntouchedOnSuccess()
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"nonce\":\"abc\"}", Encoding.UTF8, "application/json")
            };

            var result = await response.EnsureSuccessWithBodyAsync();

            Assert.That(result, Is.SameAs(response));
        }

        /// <summary>
        /// The API explains rejections only in the response body (e.g. DRF's
        /// "Attestation verification failed."), so the thrown exception has to carry it -
        /// the status code alone cannot distinguish a bad nonce from a bad certificate.
        /// </summary>
        [Test]
        public void ThrowsWithStatusCodeAndServerBodyOnFailure()
        {
            var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(
                    "{\"non_field_errors\":[\"Attestation verification failed.\"]}",
                    Encoding.UTF8,
                    "application/json")
            };

            var ex = Assert.ThrowsAsync<HttpRequestException>(response.EnsureSuccessWithBodyAsync);

            Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(ex.Message, Does.Contain("400"));
            Assert.That(ex.Message, Does.Contain("Attestation verification failed."));
        }

        /// <summary>
        /// A misconfigured server can answer with a full HTML error page; capping the
        /// quoted body keeps the log line readable instead of pages long.
        /// </summary>
        [Test]
        public void TruncatesOversizedBodies()
        {
            var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent(new string('x', 5000))
            };

            var ex = Assert.ThrowsAsync<HttpRequestException>(response.EnsureSuccessWithBodyAsync);

            Assert.That(ex!.Message.Length, Is.LessThan(1500));
        }
    }
}
