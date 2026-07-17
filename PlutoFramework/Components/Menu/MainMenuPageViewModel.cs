
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Components.Keys;
using PlutoFramework.Components.Xcavate;
using PlutoFramework.Constants;
using PlutoFramework.Model;
using PlutoFramework.Model.SQLite;
using PlutoFramework.Model.Xcavate;
using PlutoFramework.Model.Xcavate.Profile;
using XcavatePaseo.NetApi.Generated;
using XcavateProfile.Client;

namespace PlutoFramework.Components.Menu
{
    public partial class MainMenuPageViewModel : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FullName))]
        private XcavateUser? user;

        public string FullName
        {
            get
            {
                if (Profile is not null && Profile.Nickname is not null)
                {
                    return Profile.Nickname;
                }
                if (User is not null)
                {
                    return $"{User.FirstName} {User.LastName}";
                }

                return "None";
            }
        }

        private IReadOnlyList<XcavateRole> roles = [];
        public IReadOnlyList<XcavateRole> Roles
        {
            get => roles;
            set => SetProperty(ref roles, value);
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsLoggedIn))]
        private string? address = null;
        public bool IsLoggedIn => Address is not null;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FullName))]
        [NotifyPropertyChangedFor(nameof(ProfilePictureImageSource))]
        private Profile? profile;

        public ImageSource? ProfilePictureImageSource => Profile?.ProfilePicture is not null ? new UriImageSource
        {
            Uri = new Uri(Profile.ProfilePicture),
            CachingEnabled = false,
            CacheValidity = TimeSpan.FromSeconds(0),
        } : null;

        private readonly XcavateProfileService profileService = new();

        public MainMenuPageViewModel()
        {
            if (Preferences.ContainsKey(PreferencesModel.PUBLIC_KEY))
            {
                Address = Preferences.Get(PreferencesModel.PUBLIC_KEY, "None");
            }

            _ = LoadAsync();
        }

        private async Task LoadAsync()
        {
            User = await XcavateUserDatabase.GetUserInformationAsync();

            if (!Preferences.ContainsKey(PreferencesModel.PUBLIC_KEY))
            {
                return;
            }

            var client = await SubstrateClientModel.GetOrAddSubstrateClientAsync(EndpointEnum.XcavatePaseo, CancellationToken.None);
            var address = KeysModel.GetSubstrateKey();

            Roles = [.. await WhitelistModel.GetRolesCachedAsync((SubstrateClientExt)client.SubstrateClient, address, CancellationToken.None)];
        }

        public async Task LoadProfileAsync()
        {
            if (!KeysModel.HasSubstrateKey())
            {
                return;
            }

            try
            {
                Profile = await profileService.GetProfileAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load profile: {ex}");
                Profile = null;
            }
        }

        [RelayCommand]
        public Task OpenSettingsAsync() => NavigationModel.NavigateToSettingsPageAsync();

        [RelayCommand]
        public Task OpenQrScannerAsync() => NavigationModel.NavigateToQrScannerPageAsync();

        [RelayCommand]
        public Task OpenUserAsync() => NavigationModel.NavigateToUserPageAsync();

        [RelayCommand]
        public Task WalletActionAsync() => NavigationModel.NavigateToBalancesPageAsync();

        [RelayCommand]
        public Task SecurityActionAsync() => Shell.Current.Navigation.PushAsync(new KeyListPage());

        [RelayCommand]
        public Task KYCActionAsync() => NavigationModel.NavigateToKYCUserPage();

        [RelayCommand]
        public Task SupportActionAsync() => Shell.Current.Navigation.PushAsync(new ImportantLinksPage());
    }
}
