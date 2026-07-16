using PlutoFramework.Components.Loading;
using XcavateProfile.Client;

namespace PlutoFramework.Model.Xcavate.Profile
{
    /// <summary>
    /// Xcavate Profile API Service
    /// This service wraps the XcavateProfileApiClient NuGet package (v1.0.29)
    /// to manage user profiles via the REST API.
    /// </summary>
    public class XcavateProfileService
    {
        private XcavateProfileClient _client = new XcavateProfileClient(new XcavateProfileClientOptions
        {
            ApiUrl = "https://profile-api.xcavate.io/",
        });

        public Task<XcavateProfile.Client.Profile?> GetProfileAsync(CancellationToken cancellationToken = default)
        {
            var address = KeysModel.GetSubstrateKey();
            return _client.GetProfileAsync(address);
        }

        public async Task<bool> RegisterProfileAsync(
            string? nickname = null,
            Stream? profilePictureStream = null,
            string? bio = null,
            CancellationToken cancellationToken = default)
        {
            var loadingViewModel = DependencyService.Get<FullPageLoadingViewModel>();
            loadingViewModel.IsVisible = true;
            loadingViewModel.Message = "Getting account";

            var account = await KeysModel.GetAccountAsync("To register your public profile.");

            loadingViewModel.Message = "Finding encryption key";
            var x25519key = await KeysModel.GetX25519KeyNoAuthAsync();

            if (account is null)
            {
                loadingViewModel.IsVisible = false;
                return false;
            }

            if (x25519key is null)
            {
                loadingViewModel.IsVisible = false;
                return false;
            }

            string? profilePictureUrl = null;

            if (profilePictureStream is not null)
            {
                try
                {
                    loadingViewModel.Message = "Uploading image";

                    // Server rejects large payloads with 413; crop to square, downscale to 256x256 and re-encode losslessly (PNG)
                    using var compressedPictureStream = ImageModel.CompressImageToJpeg(profilePictureStream, 256, 256, 1024 * 256);

                    if (compressedPictureStream.Length == 0)
                    {
                        throw new InvalidOperationException("Failed to decode the profile picture image");
                    }

                    var uploadResult = await _client.UploadImageAsync(account.Value, compressedPictureStream, $"ProfilePicture_{account.Value}", account);
                    Console.WriteLine("Upload image result: " + uploadResult);
                    if (!string.IsNullOrEmpty(uploadResult))
                    {
                        profilePictureUrl = uploadResult;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to upload profile picture: " + ex);
                    // Continue without profile picture
                }
            }

            var profile = new XcavateProfile.Client.Profile
            {
                Ss58Address = account.Value,
                Nickname = nickname != string.Empty ? nickname : null,
                ProfilePicture = profilePictureUrl != string.Empty ? profilePictureUrl : null,
                Bio = bio != string.Empty ? bio : null,
                X25519Key = x25519key.PublicKeyString,
            };

            try
            {
                loadingViewModel.Message = "Registering profile";

                await _client.UpdateProfileAsync(account.Value, profile, account);

                loadingViewModel.IsVisible = false;

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);

            }

            loadingViewModel.IsVisible = false;

            return false;
        }
    }
}
