
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Components.Keys;
using PlutoFramework.Components.Xcavate;
using PlutoFramework.Constants;
using PlutoFramework.Model;
using PlutoFramework.Model.SQLite;
using PlutoFramework.Model.Xcavate;
using PlutoFramework.Model.Xcavate.Profile;
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
        private Profile? profile;

        [ObservableProperty]
        private ImageSource? profilePictureImageSource;

        /// <summary>
        /// Resolved when the profile arrives rather than on every read of the property it
        /// feeds: the URL carries a cache buster, so re-resolving it would download the
        /// picture again each time a binding happened to ask.
        /// </summary>
        partial void OnProfileChanged(Profile? value) =>
            ProfilePictureImageSource = ProfilePictureImageSourceModel.Create(value?.ProfilePicture);

        private readonly XcavateProfileService profileService = new();

        public MainMenuPageViewModel()
        {
            Address = MainKeyModel.GetAddress();

            _ = LoadAsync();
        }

        private async Task LoadAsync()
        {
            User = await XcavateUserDatabase.GetUserInformationAsync();

            await LoadRolesAsync();
        }

        /// <summary>
        /// Roles come from the Xcavate whitelist Solana program on the selected network, so
        /// they follow the Solana key rather than the main one. A Substrate-only user simply
        /// has none, and neither does a wallet on a network the programs are not deployed on;
        /// the badge layout renders nothing for an empty list. Best effort: a failed query
        /// keeps whatever roles were shown before.
        /// </summary>
        private async Task LoadRolesAsync()
        {
            var address = KeysModel.GetSolanaAddress();

            if (address is null)
            {
                return;
            }

            var cluster = SolanaNetworkModel.SelectedCluster;

            if (!XcavateDeploymentModel.IsDeployed(cluster))
            {
                Roles = [];

                return;
            }

            try
            {
                Roles = [.. await WhitelistModel.GetRolesCachedAsync(address, cluster, CancellationToken.None)];
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load the wallet's Xcavate roles: {ex}");
            }
        }

        /// <summary>
        /// Called by the page on navigation, which is also how a change to the main key in
        /// Settings gets picked up: Settings is pushed over this page, so coming back
        /// re-resolves the address rather than leaving the previous chain's on screen.
        /// </summary>
        public async Task LoadProfileAsync()
        {
            // Settings is pushed over this page, so coming back re-reads the roles for the
            // network the user may just have switched to.
            _ = LoadRolesAsync();

            var address = MainKeyModel.GetAddress();

            if (address != Address)
            {
                Address = address;

                // Belongs to the address we just navigated away from.
                Profile = null;
            }

            if (address is null)
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

        [RelayCommand]
        public Task RolesActionAsync() => Shell.Current.Navigation.PushAsync(new RolesPage());
    }
}
