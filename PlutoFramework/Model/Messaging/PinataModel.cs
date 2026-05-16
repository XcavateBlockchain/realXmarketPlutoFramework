using System.Text;

namespace PlutoFramework.Model.Messaging
{
    public interface IStorageAdapter
    {
        Task<string> UploadAsync(byte[] data);
        Task<string> UploadAsync(string data);
        Task<byte[]> DownloadAsync(string identifier);
    }

    public class PinataAdapterOptions
    {
        public string? UploadEndpoint { get; set; } = "https://uploads.pinata.cloud/v3/files";
        public string? PublicGateway { get; set; } = "https://gateway.pinata.cloud/ipfs";
        public string Network { get; set; } = "public";
        public string? Jwt { get; set; }
        public string? ApiKey { get; set; }
        public string? ApiSecret { get; set; }
    }

    public class PinataStorageAdapter : IStorageAdapter
    {
        private readonly PinataAdapterOptions _options;
        private readonly HttpClient _httpClient;

        public PinataStorageAdapter(PinataAdapterOptions? options = null)
        {
            _options = options ?? new PinataAdapterOptions();

            _options.UploadEndpoint = _options.UploadEndpoint?.Trim() ?? "https://uploads.pinata.cloud/v3/files";
            _options.PublicGateway = _options.PublicGateway?.Trim() ?? "https://gateway.pinata.cloud/ipfs";
            _options.Network = _options.Network?.Trim() ?? "public";
            _options.Jwt = _options.Jwt?.Trim();
            _options.ApiKey = _options.ApiKey?.Trim();
            _options.ApiSecret = _options.ApiSecret?.Trim();

            var hasJwt = !string.IsNullOrEmpty(_options.Jwt);
            var hasApiPair = !string.IsNullOrEmpty(_options.ApiKey) && !string.IsNullOrEmpty(_options.ApiSecret);

            if (!hasJwt && !hasApiPair)
            {
                throw new InvalidOperationException("Pinata credentials are missing. Set Jwt or both ApiKey and ApiSecret.");
            }

            _httpClient = new HttpClient();
        }

        public async Task<string> UploadAsync(byte[] data)
        {
            System.Diagnostics.Debug.WriteLine("PinataAdapter: Starting upload process...");

            var blob = new ByteArrayContent(data);
            using var formData = new MultipartFormDataContent();
            formData.Add(blob, "file", "assetdidcomm-message.jwe");
            formData.Add(new StringContent(_options.Network), "network");

            using var request = new HttpRequestMessage(HttpMethod.Post, _options.UploadEndpoint);
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
            var url = $"{_options.PublicGateway}/{trimmed}";
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
            if (!string.IsNullOrEmpty(_options.Jwt))
            {
                return $"Bearer {_options.Jwt}";
            }

            return _options.ApiKey ?? string.Empty;
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
