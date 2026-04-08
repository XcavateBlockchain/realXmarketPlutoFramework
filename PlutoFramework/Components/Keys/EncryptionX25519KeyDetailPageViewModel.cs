extern alias bc26;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Model;
using PlutoFrameworkCore.Keys;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlutoFramework.Components.Keys
{
    public partial class EncryptionX25519KeyDetailPageViewModel : BaseDetailPageViewModel
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SecretKey))]
        private EncryptionX25519Key? unlockedKey;
        public string SecretKey => UnlockedKey is not null ? Convert.ToBase64String(UnlockedKey.SecretKey) : "No secret key";

        [RelayCommand]
        public async Task ExportJsonAsync()
        {
            var token = CancellationToken.None;

            if (UnlockedKey is null)
            {
                return;
            }

            await KeysModel.ExportJsonFileAsync(GenerateKeyJson(), token);
        }

        private string GenerateKeyJson() {
            if (UnlockedKey is null)
            {
                return string.Empty;
            }

            var privateKeyParams = new bc26::Org.BouncyCastle.Crypto.Parameters.X25519PrivateKeyParameters(UnlockedKey.SecretKey);
            var publicKeyParams = privateKeyParams.GeneratePublicKey();

            var publicKeyBase64 = Convert.ToBase64String(publicKeyParams.GetEncoded());
            var privateKeyBase64 = Convert.ToBase64String(UnlockedKey.SecretKey);
            var kid = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + "-" + Random.Shared.Next();

            var jsonObject = new
            {
                publicJwk = new
                {
                    crv = "X25519",
                    kty = "OKP",
                    x = publicKeyBase64,
                    kid = kid
                },
                privateJwk = new
                {
                    crv = "X25519",
                    d = privateKeyBase64,
                    kty = "OKP",
                    x = publicKeyBase64,
                    kid = kid
                }
            };

            return JsonSerializer.Serialize(jsonObject, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}
