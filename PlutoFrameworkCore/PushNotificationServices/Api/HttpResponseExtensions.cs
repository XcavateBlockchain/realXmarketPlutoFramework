namespace PlutoFrameworkCore.PushNotificationServices.Api;

public static class HttpResponseExtensions
{
    private const int MaxQuotedBodyLength = 1000;

    /// <summary>
    /// Like <see cref="HttpResponseMessage.EnsureSuccessStatusCode"/>, but quotes the
    /// response body in the exception. The API explains rejections only there (DRF
    /// validation messages such as "Attestation verification failed."), so without it a
    /// failed registration logs a bare 400 that cannot be diagnosed from the client.
    /// </summary>
    public static async Task<HttpResponseMessage> EnsureSuccessWithBodyAsync(this HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return response;

        var body = await response.Content.ReadAsStringAsync();
        if (body.Length > MaxQuotedBodyLength)
            body = body[..MaxQuotedBodyLength] + "…";

        throw new HttpRequestException(
            $"Response status code does not indicate success: {(int)response.StatusCode} ({response.ReasonPhrase}). Body: {body}",
            inner: null,
            statusCode: response.StatusCode);
    }
}
