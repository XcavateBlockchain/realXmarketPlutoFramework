using System.Text;

namespace PlutoFramework.Model.Messaging
{
    public interface IStorageAdapter
    {
        Task<string> UploadAsync(byte[] data);
        Task<string> UploadAsync(string data);
        Task<byte[]> DownloadAsync(string identifier);
    }

    public class PinataStorageAdapter : IStorageAdapter
    {
        private readonly PinataSecretData _secrets;
        private readonly HttpClient _httpClient;
        private const string UploadEndpoint = "https://uploads.pinata.cloud/v3/files";

        public PinataStorageAdapter()
        {
            _secrets = PinataSecretModel.GetSecrets();
            _httpClient = new HttpClient();
        }

        public async Task<string> UploadAsync(byte[] data)
        {
            System.Diagnostics.Debug.WriteLine("PinataAdapter: Starting upload process...");

            var blob = new ByteArrayContent(data);
            using var formData = new MultipartFormDataContent();
            formData.Add(blob, "file", "assetdidcomm-message.jwe");
            formData.Add(new StringContent("public"), "network");

            using var request = new HttpRequestMessage(HttpMethod.Post, UploadEndpoint);
            request.Headers.Add("Authorization", CreateAuthHeaders());
            request.Content = formData;

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Pinata upload failed with status {response.StatusCode}: {errorText}");
            }

            var payload = await response.Content.ReadAsAsync<PinataUploadResponse>();
            var cid = payload?.Cid ?? payload?.Data?.Cid;

            if (string.IsNullOrEmpty(cid))
            {
                throw new InvalidOperationException("Pinata V3 upload response did not contain 'cid'.");
            }

            System.Diagnostics.Debug.WriteLine($"PinataAdapter: Upload successful. CID: {cid}");
            return cid;
        }

        public async Task<string> UploadAsync(string data)
        {
            var bytes = Encoding.UTF8.GetBytes(data);
            return await UploadAsync(bytes);
        }

        public async Task<byte[]> DownloadAsync(string identifier)
        {
            var trimmed = identifier.Trim();
            var url = $"{_secrets.Gateway}ipfs/{trimmed}";
            System.Diagnostics.Debug.WriteLine($"PinataAdapter: Downloading from {url}");

            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Pinata download failed with status {response.StatusCode}");
            }

            return await response.Content.ReadAsByteArrayAsync();
        }

        private string CreateAuthHeaders()
        {
            if (!string.IsNullOrEmpty(_secrets.Jwt))
            {
                return $"Bearer {_secrets.Jwt}";
            }

            return _secrets.ApiKey;
        }

        private class PinataUploadResponse
        {
            public string? Cid { get; set; }
            public PinataUploadData? Data { get; set; }
        }

        private class PinataUploadData
        {
            public string? Cid { get; set; }
        }
    }
}
