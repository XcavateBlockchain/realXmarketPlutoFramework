using PlutoFramework.Model;
using PlutoFramework.Model.Sumsub;
using PlutoFrameworkCore.Keys;

namespace PlutoFramework.Components.Kilt
{
    public class NoDidModel
    {
        public static async Task DidNavigateToNextPageAsync(Func<Task> verifiedNavigation, Func<Task> unverifiedNavigation)
        {
            CancellationToken token = CancellationToken.None;

            // Check if the account already exists
            var didLockedKey = await KeysModel.GetKeyOfTypeAsync(KeyTypeEnum.Did);
            if (didLockedKey is null)
            {
                Console.WriteLine("No did");

                return;
            }

            // Sumsub applicants are keyed by the Solana wallet address, never the DID.
            var solanaAddress = KeysModel.GetSolanaAddress();

            if (solanaAddress is null)
            {
                await unverifiedNavigation.Invoke();

                return;
            }

            var secrets = SumsubSecretModel.GetSecrets();

            var applicantData = await SumsubModel.GetApplicantDataAsync(solanaAddress, secrets.SecretKey, secrets.AppToken, token);

            if (applicantData is not null)
            {
                await verifiedNavigation.Invoke();

                return;
            }

            await unverifiedNavigation.Invoke();
        }
    }
}
