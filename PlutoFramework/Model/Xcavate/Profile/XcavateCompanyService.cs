using CommunityToolkit.Maui.Alerts;
using XcavateProfile.Client;
using XcavateProfileApiClient.Signing;

namespace PlutoFramework.Model.Xcavate.Profile
{
    /// <summary>
    /// Xcavate Profile API company service. Wraps the XcavateProfileApiSolanaClient NuGet
    /// package's company endpoints (register, read, update, delete, logo upload) the same
    /// way <see cref="XcavateProfileService"/> wraps the profile endpoints.
    /// </summary>
    /// <remarks>
    /// Chain-agnostic: the owner address and the signer both come from <see cref="MainKeyModel"/>,
    /// so a company belongs to whichever key the user has made their main one. One wallet may
    /// own any number of companies; which of them is the developer's selected one is local
    /// state (see <see cref="SelectedCompanyModel"/>) - the API has no such concept.
    /// </remarks>
    public class XcavateCompanyService
    {
        private readonly XcavateProfileClient client = new(new XcavateProfileClientOptions
        {
            ApiUrl = "https://profile-api.xcavate.io/",
        });

        /// <summary>
        /// Every company the main key owns. Empty when there is no key or the wallet owns
        /// none - the API's "my companies" endpoint answers an empty list, never a 404.
        /// </summary>
        public async Task<List<Company>> GetMyCompaniesAsync(CancellationToken cancellationToken = default)
        {
            var address = MainKeyModel.GetAddress();

            if (address is null)
            {
                return [];
            }

            return await client.GetCompaniesByUserAsync(address, cancellationToken);
        }

        /// <summary>
        /// Registers a company owned by the main key. Null means there was nothing to sign
        /// with - no key, or a dismissed password prompt; anything that went wrong on the
        /// wire throws, carrying the server's explanation for the caller to show. The server
        /// assigns the id, so read it off the returned instance.
        /// </summary>
        public async Task<Company?> CreateAsync(
            string name,
            string email,
            string? website,
            string? summary,
            string? address,
            Stream? logoStream,
            CancellationToken cancellationToken = default)
        {
            var signer = await MainKeyModel.GetSignerAsync("To register your company.", cancellationToken);

            if (signer is null)
            {
                return null;
            }

            var company = new Company
            {
                // A non-admin create requires both to be the signer's own address.
                UserId = signer.Address,
                CompanyWalletAddress = signer.Address,
                Name = name,
                Email = email,
                Website = EmptyToNull(website),
                Summary = EmptyToNull(summary),
                Address = EmptyToNull(address),
            };

            var created = await client.CreateCompanyAsync(company, signer, cancellationToken);

            // The logo goes up only after the create, never before: the upload endpoint
            // answers 404 for a company that does not exist yet. Same ordering rule as the
            // profile picture in XcavateProfileService.RegisterProfileCoreAsync.
            if (logoStream is not null && created.CompanyId is not null)
            {
                created.Logo = await UploadLogoAsync(created.CompanyId, logoStream, signer, cancellationToken)
                    ?? created.Logo;
            }

            return created;
        }

        /// <summary>
        /// Replaces the stored company's fields. <see cref="Company.CompanyWalletAddress"/> is
        /// immutable and <see cref="Company.UserId"/> is carried over unchanged - reassigning
        /// it would transfer ownership away from this wallet. Null means there was nothing to
        /// sign with; wire failures throw with the server's explanation.
        /// </summary>
        public async Task<Company?> UpdateAsync(
            Company existing,
            string name,
            string email,
            string? website,
            string? summary,
            string? address,
            Stream? logoStream,
            CancellationToken cancellationToken = default)
        {
            if (existing.CompanyId is null)
            {
                return null;
            }

            var signer = await MainKeyModel.GetSignerAsync("To update your company.", cancellationToken);

            if (signer is null)
            {
                return null;
            }

            var company = new Company
            {
                UserId = existing.UserId,
                CompanyWalletAddress = existing.CompanyWalletAddress,
                Name = name,
                Email = email,
                // The update replaces every field it sends, so it has to carry the stored logo
                // URL or it writes a null over it and the logo disappears.
                Logo = existing.Logo,
                Website = EmptyToNull(website),
                Summary = EmptyToNull(summary),
                Address = EmptyToNull(address),
            };

            var updated = await client.UpdateCompanyAsync(existing.CompanyId, company, signer, cancellationToken);

            if (logoStream is not null)
            {
                updated.Logo = await UploadLogoAsync(existing.CompanyId, logoStream, signer, cancellationToken)
                    ?? updated.Logo;
            }

            return updated;
        }

        /// <summary>
        /// Deletes the company. False means there was nothing to sign with - no key, or a
        /// dismissed password prompt; wire failures throw with the server's explanation.
        /// </summary>
        public async Task<bool> DeleteAsync(string companyId, CancellationToken cancellationToken = default)
        {
            var signer = await MainKeyModel.GetSignerAsync("To delete your company.", cancellationToken);

            if (signer is null)
            {
                return false;
            }

            await client.DeleteCompanyAsync(companyId, signer, cancellationToken);

            return true;
        }

        /// <summary>
        /// Uploads the logo and returns its public URL, or null when the upload failed - the
        /// company itself is already stored by this point, so the save still counts, but the
        /// failure is said out loud rather than swallowed.
        /// </summary>
        private async Task<string?> UploadLogoAsync(
            string companyId,
            Stream logoStream,
            IRequestSigner signer,
            CancellationToken cancellationToken)
        {
            try
            {
                // Server rejects payloads over 26MB; fit within 512x512 and re-encode as JPEG,
                // dropping quality until it fits 256KB. The server derives the content type
                // from the file extension, so the upload is named .jpg to match the encoding.
                using var compressedLogoStream = ImageModel.CompressImageToJpeg(logoStream, 512, 512, 1024 * 256);

                if (compressedLogoStream.Length == 0)
                {
                    throw new InvalidOperationException("Failed to decode the company logo image");
                }

                var uploadResult = await client.UploadCompanyLogoAsync(
                    companyId,
                    compressedLogoStream,
                    "logo.jpg",
                    signer,
                    cancellationToken);

                Console.WriteLine("Company logo upload result: " + uploadResult);

                return uploadResult;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to upload the company logo: " + ex);

                await Toast.Make("The company was saved, but the logo could not be uploaded. Please try saving it again.").Show();

                return null;
            }
        }

        private static string? EmptyToNull(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
