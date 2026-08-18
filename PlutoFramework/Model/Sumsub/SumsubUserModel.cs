
using PlutoFramework.Model.SQLite;
using PlutoFramework.Model.Xcavate;

namespace PlutoFramework.Model.Sumsub
{
    public class SumsubUserModel
    {
        public static async Task LoadAndSaveUserInfoAsync(CancellationToken token)
        {
            // Sumsub applicants are keyed by the Solana wallet address, never Polkadot.
            var address = Model.KeysModel.GetSolanaAddress();

            if (address is null)
            {
                return;
            }

            var secrets = SumsubSecretModel.GetSecrets();

            var applicant = await SumsubModel.GetApplicantDataAsync(
                address,
                secrets.SecretKey,
                secrets.AppToken,
                token
            );

            if (applicant is null)
            {
                return;
            }

            var role = applicant.Review.LevelName switch
            {
                "csharp-verification-developer" => UserRoleEnum.Developer,
                "csharp-verification-investor" => UserRoleEnum.Investor,
                "csharp-verification-letting-agent" => UserRoleEnum.LettingAgent,
                "csharp-verification-lawyer" => UserRoleEnum.Lawyer,
                _ => UserRoleEnum.None,
            };

            var user = new XcavateUser
            {
                ProfilePicture = null,
                ProfileBackground = null,
                Role = role,
                FirstName = "",
                LastName = "",
                Email = applicant.Email,
                PhoneNumber = applicant.Phone,
                AccountCreatedAt = applicant.CreatedAtDateTime
            };

            await XcavateUserDatabase.SaveUserInformationAsync(user);
        }

        /// <summary>
        /// Fetches the current verification status for the Solana wallet address owned by this user.
        /// Returns null when the applicant does not exist in Sumsub.
        /// </summary>
        public static async Task<SumsubStatusData?> GetCurrentStatusAsync(CancellationToken token)
        {
            var address = Model.KeysModel.GetSolanaAddress();

            if (string.IsNullOrEmpty(address))
            {
                return null;
            }

            var secrets = SumsubSecretModel.GetSecrets();

            var applicant = await SumsubModel.GetApplicantDataAsync(
                address,
                secrets.SecretKey,
                secrets.AppToken,
                token
            );

            if (applicant is null)
            {
                return null;
            }

            return SumsubStatusModelParser.ParseStatus(applicant);
        }
    }
}
