using System.Net;
using PlutoFramework.Components.Loading;
using PlutoFrameworkCore.Xcavate;
using XcavateProfile.Client;

namespace PlutoFramework.Model.Xcavate.Profile
{
    /// <summary>
    /// Xcavate Profile API Service
    /// This service wraps the XcavateProfileApiClient NuGet package to manage user profiles
    /// via the REST API.
    /// </summary>
    /// <remarks>
    /// Chain-agnostic: the address and the signer both come from <see cref="MainKeyModel"/>,
    /// so a profile belongs to whichever key the user has made their main one. The API's
    /// <c>Ss58Address</c> field holds a Solana base58 address unchanged - the name is
    /// historical, and the server keys profiles on either format.
    /// </remarks>
    public class XcavateProfileService
    {
        private XcavateProfileClient _client = new XcavateProfileClient(new XcavateProfileClientOptions
        {
            ApiUrl = "https://profile-api.xcavate.io/",
        });

        public Task<XcavateProfile.Client.Profile?> GetProfileAsync(CancellationToken cancellationToken = default)
        {
            var address = MainKeyModel.GetAddress();

            // No key, so no profile to look up. Querying anyway used to send the
            // "Substrate key does not exist" placeholder to the API.
            if (address is null)
            {
                return Task.FromResult<XcavateProfile.Client.Profile?>(null);
            }

            return _client.GetProfileAsync(address, cancellationToken);
        }

        /// <summary>
        /// Whether <paramref name="nickname"/> is free for this user to publish under. True
        /// when nobody holds it, and true when the holder is this user's own profile.
        /// </summary>
        /// <remarks>
        /// Advisory only - two devices can claim the same nickname between this call and the
        /// write - but it is what turns a rejected save into something the user can act on
        /// before they are asked to sign anything.
        /// </remarks>
        public async Task<bool> IsNicknameAvailableAsync(string nickname, CancellationToken cancellationToken = default)
        {
            XcavateProfile.Client.Profile? holder;

            try
            {
                holder = await _client.GetProfileByNicknameAsync(nickname, cancellationToken);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Nobody holds it. An unclaimed nickname answers 404 rather than an empty
                // body, and reading that as a failed check would block every save.
                return true;
            }

            // Editing keeps the nickname the profile already publishes, so only another
            // address's claim on it blocks the save.
            return holder is null || holder.Ss58Address == MainKeyModel.GetAddress();
        }

        /// <summary>
        /// Writes the profile and reports whether it went through. False means there was
        /// nothing to sign with - no key, or a dismissed password prompt; anything that went
        /// wrong on the wire throws, carrying the server's explanation for the caller to show.
        /// </summary>
        public async Task<bool> RegisterProfileAsync(
            string? nickname = null,
            Stream? profilePictureStream = null,
            string? bio = null,
            CancellationToken cancellationToken = default)
        {
            var loadingViewModel = DependencyService.Get<FullPageLoadingViewModel>();
            loadingViewModel.IsVisible = true;

            try
            {
                return await RegisterProfileCoreAsync(loadingViewModel, nickname, profilePictureStream, bio, cancellationToken);
            }
            finally
            {
                // Every exit hides it, including the failures that now travel to the caller
                // rather than being swallowed here.
                loadingViewModel.IsVisible = false;
            }
        }

        private async Task<bool> RegisterProfileCoreAsync(
            FullPageLoadingViewModel loadingViewModel,
            string? nickname,
            Stream? profilePictureStream,
            string? bio,
            CancellationToken cancellationToken)
        {
            loadingViewModel.Message = "Getting account";

            var signer = await MainKeyModel.GetSignerAsync("To register your public profile.", cancellationToken);

            if (signer is null)
            {
                return false;
            }

            loadingViewModel.Message = "Finding encryption key";

            // Solana onboarding creates no X25519 key, and the API requires one. Generating it
            // here rather than only at key creation is what lets users already onboarded
            // Solana-only register a profile at all.
            await KeysModel.EnsureEncryptionX25519KeyAsync("To set up your encryption key.");

            var x25519key = await KeysModel.GetX25519KeyNoAuthAsync();

            if (x25519key is null)
            {
                return false;
            }

            string? profilePictureUrl = null;

            if (profilePictureStream is not null)
            {
                try
                {
                    loadingViewModel.Message = "Uploading image";

                    // Server rejects payloads over 25MB with 413; fit within 256x256 and
                    // re-encode as JPEG, dropping quality until it fits 256KB. JPEG is what
                    // the upload is named for - see ProfilePictureModel.UploadFileName - and
                    // the two have to agree or the bucket labels the object the wrong format.
                    using var compressedPictureStream = ImageModel.CompressImageToJpeg(profilePictureStream, 256, 256, 1024 * 256);

                    if (compressedPictureStream.Length == 0)
                    {
                        throw new InvalidOperationException("Failed to decode the profile picture image");
                    }

                    var uploadResult = await _client.UploadImageAsync(
                        signer.Address,
                        compressedPictureStream,
                        ProfilePictureModel.UploadFileName(signer.Address),
                        signer,
                        cancellationToken);

                    Console.WriteLine("Upload image result: " + uploadResult);
                    if (!string.IsNullOrEmpty(uploadResult))
                    {
                        profilePictureUrl = uploadResult;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to upload profile picture: " + ex);
                    // Save the rest of the profile anyway. The picture the user already has
                    // is kept below rather than dropped, so a failed upload costs them the
                    // new picture and not the old one too.
                }
            }

            string? existingPictureUrl = null;

            // The update below replaces every field it sends, so a save that uploaded nothing -
            // editing only a nickname, or an upload that failed - has to hand back the URL
            // already stored or it writes a null over the picture and it disappears from the
            // menu and the edit page at once. Only worth the round trip when there is no fresh
            // upload to store: a successful one has already replaced the object anyway.
            if (profilePictureUrl is null)
            {
                try
                {
                    loadingViewModel.Message = "Finding profile picture";

                    existingPictureUrl = (await GetProfileAsync(cancellationToken))?.ProfilePicture;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to read the stored profile picture: " + ex);
                }
            }

            var profile = new XcavateProfile.Client.Profile
            {
                Ss58Address = signer.Address,
                Nickname = nickname != string.Empty ? nickname : null,
                ProfilePicture = ProfilePictureModel.ResolveStoredUrl(profilePictureUrl, existingPictureUrl),
                Bio = bio != string.Empty ? bio : null,
                X25519Key = x25519key.PublicKeyString,
            };

            loadingViewModel.Message = "Registering profile";

            // Deliberately uncaught. Reporting a refused write as an ordinary false left the
            // caller navigating on to a profile that was never stored, and threw away the
            // reason it was refused along with it.
            await _client.UpdateProfileAsync(signer.Address, profile, signer, cancellationToken);

            return true;
        }
    }
}
