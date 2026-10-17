
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Model;
using PlutoFramework.Model.Xcavate;

namespace PlutoFramework.Components.Xcavate
{

    public partial class XcavateNavigationBarViewModel : ObservableObject
    {
        public enum XcavateNavigationBarSelection
        {
            Account,
            Help,
            Marketplace
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AccountIsSelected))]
        [NotifyPropertyChangedFor(nameof(HelpIsSelected))]
        [NotifyPropertyChangedFor(nameof(MarketplaceIsSelected))]
        private XcavateNavigationBarSelection selected = XcavateNavigationBarSelection.Account;

        public bool AccountIsSelected => Selected == XcavateNavigationBarSelection.Account;

        public bool HelpIsSelected => Selected == XcavateNavigationBarSelection.Help;

        public bool MarketplaceIsSelected => Selected == XcavateNavigationBarSelection.Marketplace;

        [RelayCommand]
        public async Task SelectAccountAsync()
        {
            if (AccountIsSelected)
            {
                return;
            }

            Selected = XcavateNavigationBarSelection.Account;

            // Lawyers land on their own main page; the cached role model keeps a tap from
            // re-querying the indexer.
            var route = await LawyerRoleModel.IsLawyerAsync(
                SolanaNetworkModel.SelectedCluster, CancellationToken.None)
                ? "//LawyerAccount"
                : "//Account";

            await Shell.Current.GoToAsync(route, animate: false);
        }

        [RelayCommand]
        public async Task SelectHelpAsync()
        {
            if (HelpIsSelected)
            {
                return;
            }

            Selected = XcavateNavigationBarSelection.Help;

            await Shell.Current.GoToAsync("//Help", animate: false);
        }

        [RelayCommand]
        public async Task SelectMarketplaceAsync()
        {
            if (MarketplaceIsSelected)
            {
                return;
            }

            Selected = XcavateNavigationBarSelection.Marketplace;

            await Shell.Current.GoToAsync("//Marketplace", animate: false);
        }
    }
}
