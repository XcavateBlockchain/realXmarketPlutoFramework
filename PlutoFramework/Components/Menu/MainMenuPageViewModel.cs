
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Components.Keys;
using PlutoFramework.Components.Xcavate;
using PlutoFramework.Constants;
using PlutoFramework.Model;
using PlutoFramework.Model.SQLite;
using PlutoFramework.Model.Xcavate;
using XcavatePaseo.NetApi.Generated;

namespace PlutoFramework.Components.Menu
{
    public partial class MainMenuPageViewModel : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FullName))]
        private XcavateUser? user;

        public string FullName => User is not null ? $"{User.FirstName} {User.LastName}" : "None";

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
