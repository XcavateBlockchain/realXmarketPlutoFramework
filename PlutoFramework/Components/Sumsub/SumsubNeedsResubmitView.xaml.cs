using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Model;
using PlutoFramework.Model.Sumsub;
using PlutoFramework.Model.SQLite;

namespace PlutoFramework.Components.Sumsub
{
    public partial class SumsubNeedsResubmitView : ContentView
    {
        public SumsubNeedsResubmitView()
        {
            InitializeComponent();
            BindingContext = new SumsubNeedsResubmitViewModel();

            ResubmitButton.Clicked += async (s, e) =>
            {
                if (BindingContext is SumsubNeedsResubmitViewModel vm)
                    await vm.ResubmitCommand.ExecuteAsync(null);
            };
        }

        public void Bind(SumsubStatusData viewModelInput)
        {
            if (BindingContext is SumsubNeedsResubmitViewModel vm)
                vm.Populate(viewModelInput);
        }
    }

    /// <summary>
    /// Displays a "needs resubmit" status with a button to navigate back to SumsubWebSDKPage.
    /// </summary>
    public partial class SumsubNeedsResubmitViewModel : ObservableObject
    {
        [ObservableProperty] private bool showReason;
        [ObservableProperty] private string reason = "";
        [ObservableProperty] private bool hasTimestamp;
        [ObservableProperty] private string timestampText = "";
        [ObservableProperty] private bool hasAttempts;
        [ObservableProperty] private string attemptsText = "";
        [ObservableProperty] private bool hasLevelName;
        [ObservableProperty] private string displayLevel = "";

        private string? currentUserAddress;

        public void Populate(SumsubStatusData viewModelInput)
        {
            if (viewModelInput.StatusType != SumsubStatusType.NeedsResubmit)
                throw new InvalidOperationException("Expected needsResubmit status data");

            currentUserAddress = KeysModel.GetSubstrateKey();

            ShowReason = !string.IsNullOrEmpty(viewModelInput.Reason);
            Reason = viewModelInput.Reason;

            TimestampText = $"Last submitted on {viewModelInput.Timestamp:dddd, dd MMMM yyyy, HH:mm}";
            HasTimestamp = true;

            if (viewModelInput.AttemptCount > 1)
            {
                AttemptsText = $"Previous attempt{(viewModelInput.AttemptCount > 1 ? "s" : "")}: {viewModelInput.AttemptCount}";
                HasAttempts = true;
            }

            if (!string.IsNullOrEmpty(viewModelInput.LevelName))
            {
                var role = viewModelInput.LevelName.Split('-').LastOrDefault() ?? viewModelInput.LevelName;
                DisplayLevel = $"Verification: {role.Replace("-", " ")}";
                HasLevelName = true;
            }
        }

        [RelayCommand]
        private async Task ResubmitAsync()
        {
            if (string.IsNullOrEmpty(currentUserAddress))
                currentUserAddress = KeysModel.GetSubstrateKey();

            if (string.IsNullOrEmpty(currentUserAddress) || currentUserAddress == "Substrate key does not exist")
                return;

            var userInfo = await XcavateUserDatabase.GetUserInformationAsync();

            var normalizedLevel = NormalizeRoleToSumsubLevel(userInfo?.Role);

            var applicant = new Applicant
            {
                ApplicantIdentifiers = new ApplicantIdentifiers
                {
                    Email = userInfo?.Email ?? "",
                    Phone = userInfo?.PhoneNumber ?? "",
                    ExternalUserId = KeysModel.GetSubstrateKey()
                },
                totalInSeconds = 600,
                UserId = KeysModel.GetSubstrateKey(),
                LevelName = normalizedLevel
            };

            var secrets = SumsubSecretModel.GetSecrets();

            try
            {
                var accessToken = await SumsubModel.GenerateWebSDKAccessTokenAsync(
                    applicant, secrets.SecretKey, secrets.AppToken, CancellationToken.None);

                await Shell.Current.Navigation.PushAsync(
                    new SumsubWebSDKPage(
                        accessToken ?? "",
                        applicant,
                        navigation: () => Task.FromResult(0)
                    )
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to start resubmission: {ex}");
            }
        }

        private static string NormalizeRoleToSumsubLevel(Model.Xcavate.UserRoleEnum? role)
        {
            if (role == null)
                return "csharp-verification-investor";

            return role.Value switch
            {
                Model.Xcavate.UserRoleEnum.Developer => "csharp-verification-developer",
                Model.Xcavate.UserRoleEnum.Investor => "csharp-verification-investor",
                Model.Xcavate.UserRoleEnum.LettingAgent => "csharp-verification-letting-agent",
                Model.Xcavate.UserRoleEnum.Lawyer => "csharp-verification-lawyer",
                _ => "csharp-verification-investor"
            };
        }
    }
}
