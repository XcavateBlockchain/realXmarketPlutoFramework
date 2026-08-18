using Plugin.Fingerprint;
using Plugin.Fingerprint.Abstractions;
using PlutoFramework.Components.Account;
using PlutoFramework.Components.Kilt;
using PlutoFramework.Components.Loading;
using PlutoFramework.Components.Password;
using PlutoFramework.Components.Xcavate;
using PlutoFramework.Constants;
using PlutoFramework.Model.Sumsub;
using PlutoFramework.Model.Xcavate;
using PlutoFrameworkCore.Keys;

namespace PlutoFramework.Model
{
    public record AuthenticationResult
    {
        public required string Password { get; set; }
        public required bool Value { get; set; }
    }

    public class RequirementsModel
    {
        public static Task<bool> CheckRequirementsAsync()
        {
            return CheckRequirementsAsync(CancellationToken.None);
        }

        public static async Task<bool> CheckRequirementsAsync(CancellationToken token)
        {
            var fullPageLoadingViewModel = DependencyService.Get<FullPageLoadingViewModel>();

            fullPageLoadingViewModel.Message = "Getting Account";

            if (!CheckAccountExists())
            {
                return false;
            }

            #region Sumsub
            fullPageLoadingViewModel.Message = "Verifying on Sumsub";

            var address = KeysModel.GetSolanaAddress();

            var sumsubSecrets = SumsubSecretModel.GetSecrets();

            var applicantData = await SumsubModel.GetApplicantDataAsync(
                address,
                sumsubSecrets.SecretKey,
                sumsubSecrets.AppToken,
                token
            );

            if (applicantData is null)
            {
                Console.WriteLine("applicantData was null");
                var userProfileNotCreatedPopupViewModel = DependencyService.Get<UserProfileNotCreatedPopupViewModel>();

                userProfileNotCreatedPopupViewModel.IsVisible = true;

                return false;
            }
            #endregion

            return true;
        }

        public static async Task<bool> CheckXcavateRoleAsync(XcavateRole role, CancellationToken token)
        {
            // Roles live in the Xcavate whitelist Solana program, keyed by Solana address. A
            // Substrate-only account has none, which is the same answer as "not whitelisted".
            var address = KeysModel.GetSolanaAddress();

            if (address is null)
            {
                DependencyService.Get<NotWhitelistedPopupViewModel>().IsVisible = true;

                return false;
            }

            var fullPageLoadingViewModel = DependencyService.Get<FullPageLoadingViewModel>();

            fullPageLoadingViewModel.Message = "Querying roles";

            if (!await WhitelistModel.HasRoleAsync(address, role, token))
            {
                var notWhitelistedPopupViewModel = DependencyService.Get<NotWhitelistedPopupViewModel>();

                notWhitelistedPopupViewModel.IsVisible = true;

                return false;
            }

            return true;
        }

        public static bool CheckAccountExists()
        {
            var onboardingCompleted = OnboardingModel.IsOnboardingCompleted();

            if (!KeysModel.HasSolanaKey() || !onboardingCompleted)
            {
                var noAccountPopupViewModel = DependencyService.Get<NoAccountPopupViewModel>();

                noAccountPopupViewModel.IsVisible = true;

                return false;
            }

            return true;
        }

        public static bool CheckDidExists()
        {
            if (KeysModel.GetKeyOfTypeAsync(KeyTypeEnum.Did) is null)
            {
                var noDidPopupViewModel = DependencyService.Get<NoDidPopupViewModel>();

                noDidPopupViewModel.IsVisible = true;

                return false;
            }

            return true;
        }

        public static async Task<AuthenticationResult> CheckAuthenticationAsync(string passwordStorageKey = PreferencesModel.PASSWORD, string reason = "Authentication required")
        {
            var biometricsEnabled = Preferences.Get(PreferencesModel.BIOMETRICS_ENABLED, false);

            var request = new AuthenticationRequestConfiguration("Authentication required", reason);
            FingerprintAuthenticationResult result;

            if (biometricsEnabled)
            {
                result = await CrossFingerprint.Current.AuthenticateAsync(request).ConfigureAwait(false);
            }
            else
            {
                result = new FingerprintAuthenticationResult
                {
                    Status = FingerprintAuthenticationResultStatus.Denied,
                };
            }

            var correctPassword = await SecureStorage.Default.GetAsync(passwordStorageKey).ConfigureAwait(false) ?? throw new ArgumentNullException("Password was not setup");

            if (!result.Authenticated || result.Status == FingerprintAuthenticationResultStatus.Denied)
            {
                var viewModel = DependencyService.Get<EnterPasswordPopupViewModel>();

                viewModel.Reason = reason;
                viewModel.IsVisible = true;

                for (int i = 0; i < 5; i++)
                {
                    var password = await viewModel.EnteredPassword.Task;

                    viewModel.EnteredPassword = new();

                    if (password is null)
                    {
                        viewModel.SetToDefault();
                        throw new Exception("Failed to authenticate");
                    }

                    if (password == correctPassword)
                    {
                        viewModel.SetToDefault();

                        return new AuthenticationResult
                        {
                            Value = true,
                            Password = correctPassword,
                        };
                    }

                    viewModel.ErrorIsVisible = true;

                    if (i == 4)
                    {
                        viewModel.SetToDefault();

                        return new AuthenticationResult
                        {
                            Value = false,
                            Password = "-",
                        };
                    }
                }
            }

            return new AuthenticationResult
            {
                Value = true,
                Password = correctPassword,
            };
        }
    }
}
