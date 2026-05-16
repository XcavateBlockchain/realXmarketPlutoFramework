using Microsoft.Extensions.Configuration;

namespace PlutoFramework.Model.Messaging
{
    public record PinataSecretData
    {
        public required string ApiKey { get; set; }
        public required string ApiSecret { get; set; }
        public required string Jwt { get; set; }
        public required string Gateway { get; set; }
    }

    public class SumsubSecretModel
    {
        public static PinataSecretData GetSecrets()
        {
            var configuration = MauiAppBuilderExtensions.Services.GetService<IConfiguration>() ?? throw new Exception("Configuration is null");

            var apiKey = configuration.GetValue<string>("PINATA_API_KEY");
            var apiSecret = configuration.GetValue<string>("PINATA_API_SECRET");
            var jwt = configuration.GetValue<string>("PINATA_JWT");
            var gateway = configuration.GetValue<string>("PINATA_GATEWAY");

            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiSecret) || string.IsNullOrEmpty(jwt) || string.IsNullOrEmpty(gateway))
            {
                throw new Exception("Pinata secrets are not set in appsettings.json");
            }

            return new PinataSecretData
            {
                ApiKey = apiKey,
                ApiSecret = apiSecret,
                Jwt = jwt,
                Gateway = gateway
            };
        }
    }
}
