using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Components.Buttons;
using PlutoFramework.Components.Loading;
using PlutoFramework.Components.TransactionAnalyzer;
using PlutoFramework.Components.WebView;
using PlutoFramework.Constants;
using PlutoFramework.Model;
using PlutoFramework.Model.Currency;
using PlutoFramework.Model.SQLite;
using PlutoFramework.Model.Xcavate;
using PlutoFrameworkCore.Xcavate;
using UniqueryPlus.Metadata;
using UniqueryPlus.Nfts;
using XcavatePaseo.NetApi.Generated.Storage;
using PropertyModel = PlutoFramework.Model.Xcavate.XcavatePropertyModel;

namespace PlutoFramework.Components.XcavateProperty
{
    public enum MainActionStates
    {
        CanNotInvest,
        Buy,
        ListingExpired,
        SpvToBeCreated,
        CreateSpv,
        RefundBought,
        SoldOut,
        Claim,
        ToBeClaimed,
        ClaimExpired,
        RefundUnclaimed,
        RefundClaimed,
        Relist,
        Unknown
    }

    public partial class PropertyDetailViewModel : ObservableObject
    {
        private MainActionStates getMainActionState()
        {
            if (NftWrapper.ListingHasExpired && ListingDetails?.ListedTokens > 0 && TokensBought > 0)
            {
                return MainActionStates.RefundBought;
            }

            if (NftWrapper.ListingHasExpired && ListingDetails?.ListedTokens > 0)
            {
                return MainActionStates.ListingExpired;
            }

            if (!NftWrapper.ListingHasExpired && ListingDetails?.ListedTokens > 0)
            {
                return MainActionStates.Buy;
            }

            if (ListingDetails?.ListedTokens == 0 && !SpvCreated && (Roles?.Contains(XcavateRole.SpvConfirmation) ?? false))
            {
                return MainActionStates.CreateSpv;
            }

            if (ListingDetails?.ListedTokens == 0 && !SpvCreated && !(Roles?.Contains(XcavateRole.SpvConfirmation) ?? false))
            {
                return MainActionStates.SpvToBeCreated;
            }

            if (NftWrapper.ClaimHasExpired && ListingDetails?.UnclaimedTokens > 0 && TokensOwned > 0)
            {
                return MainActionStates.RefundClaimed;
            }

            if (NftWrapper.ClaimHasExpired && ListingDetails?.UnclaimedTokens > 0 && TokensBought > 0)
            {
                return MainActionStates.RefundUnclaimed;
            }

            if (NftWrapper.ClaimHasExpired && ListingDetails?.UnclaimedTokens > 0)
            {
                return MainActionStates.ClaimExpired;
            }

            if (!NftWrapper.ClaimHasExpired && ListingDetails?.UnclaimedTokens > 0 && TokensBought > 0)
            {
                return MainActionStates.Claim;
            }

            if (!NftWrapper.ClaimHasExpired && ListingDetails?.UnclaimedTokens > 0 && TokensBought == 0)
            {
                return MainActionStates.ToBeClaimed;
            }

            if (ListingDetails?.UnclaimedTokens == 0 && TokensOwned > 0)
            {
                return MainActionStates.Relist;
            }

            if (!NftWrapper.ListingHasExpired && ListingDetails?.ListedTokens == 0 && TokensBought == 0 && TokensOwned == 0)
            {
                return MainActionStates.SoldOut;
            }

            return MainActionStates.Unknown;
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusText))]
        [NotifyPropertyChangedFor(nameof(StatusIsVisible))]
        [NotifyPropertyChangedFor(nameof(MainActionButtonState))]
        [NotifyPropertyChangedFor(nameof(MainActionText))]
        private HashSet<XcavateRole>? roles = null;

        [ObservableProperty]
        private bool spvCreated;

        [ObservableProperty]
        private Endpoint endpoint;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusText))]
        [NotifyPropertyChangedFor(nameof(StatusIsVisible))]
        [NotifyPropertyChangedFor(nameof(MainActionButtonState))]
        [NotifyPropertyChangedFor(nameof(MainActionText))]
        private XcavateNftWrapper nftWrapper;

        [ObservableProperty]
        private XcavateRegion region;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AreaPricesPercentage))]
        [NotifyPropertyChangedFor(nameof(RentalDemandPercentage))]
        [NotifyPropertyChangedFor(nameof(LocationShortName))]
        [NotifyPropertyChangedFor(nameof(PropertyImages))]
        [NotifyPropertyChangedFor(nameof(PropertyStatus))]
        [NotifyPropertyChangedFor(nameof(PropertyAddressLine))]
        [NotifyPropertyChangedFor(nameof(ListingPrice))]
        [NotifyPropertyChangedFor(nameof(PricePerTokenText))]
        [NotifyPropertyChangedFor(nameof(Apy))]
        [NotifyPropertyChangedFor(nameof(TokensAvailable))]
        [NotifyPropertyChangedFor(nameof(RentalIncome))]
        [NotifyPropertyChangedFor(nameof(TokensOwnedWorth))]
        [NotifyPropertyChangedFor(nameof(CompanyName))]
        [NotifyPropertyChangedFor(nameof(CompanyImage))]
        private PropertyMetadata? metadata;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(MainActionButtonState))]
        [NotifyPropertyChangedFor(nameof(MainActionText))]
        private XcavateOngoingObjectListingDetails? listingDetails;

        public double AreaPricesPercentage => PropertyModel.GetAreaPricesPercentage(Metadata?.Financials.PropertyPrice ?? 0);
        public double RentalDemandPercentage => PropertyModel.GetRentalDemand();

        public string LocationShortName => $"{Metadata?.Address.Street}, {Metadata?.Address.TownCity}";

        public string PropertyAddressLine => Metadata is null
            ? "Unknown address"
            : $"{Metadata.Address.FlatOrUnit}, {Metadata.Address.Street}, {Metadata.Address.TownCity}, {Metadata.Address.PostCode}";

        public IReadOnlyList<string> PropertyImages => Metadata?.Files ?? [];

        public string PropertyStatus => Metadata?.Status ?? "Unknown";

        public string ListingPrice => ((decimal)(Metadata?.Financials.PropertyPrice ?? 0)).ToCurrencyString();

        public string PricePerTokenText => $"{((decimal)(Metadata?.Financials.PricePerToken ?? 0)).ToCurrencyString()} [{String.Format((string)Application.Current.Resources["CurrencyFormat"], Metadata?.Financials.PricePerToken)} USDT]";

        public string Apy => PropertyModel.GetAPY(Metadata?.Financials.EstimatedRentalIncome ?? (decimal)1, Metadata?.Financials.PropertyPrice ?? 1);

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TokensAvailable))]
        private uint tokensListed;

        public string TokensAvailable => $"{ListingDetails?.ListedTokens.ToString() ?? "-"} / {Metadata?.Financials.NumberOfTokens.ToString() ?? "-"}";

        public string RentalIncome => ((decimal)(Metadata?.Financials.EstimatedRentalIncome ?? 0)).ToCurrencyString();

        public string CompanyName => Metadata?.Company?.Name ?? "Unknown company";

        public string CompanyImage => Metadata?.Company?.Logo ?? "xcavate.png";

        public string PropertyArea => Metadata?.Attributes?.Area ?? "Unknown";

        public string OffStreetParking => Metadata?.Attributes?.OffStreetParking ?? "Unknown";

        public string OutdoorSpace => Metadata?.Attributes?.OutdoorSpace ?? "Unknown";

        public string NumberOfBedrooms => Metadata?.Attributes?.NumberOfBedrooms?.ToString() ?? "Unknown";

        public string ConstructionDate => DateTime.TryParse(Metadata?.Attributes?.ConstructionDate, out var constructionDate)
            ? constructionDate.ToString("yyyy-MM-dd")
            : Metadata?.Attributes?.ConstructionDate ?? "Unknown";

        public string NumberOfBathrooms => Metadata?.Attributes?.NumberOfBathrooms?.ToString() ?? "Unknown";

        public string Quality => Metadata?.Attributes?.Quality ?? "Unknown";


        [RelayCommand]
        public Task OpenMapAsync() => Task.FromResult(0); //Browser.Default.OpenAsync(<location url>, BrowserLaunchMode.SystemPreferred);

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TokensBoughtWorth))]
        [NotifyPropertyChangedFor(nameof(BoughtPropertyTokensViewIsVisible))]
        [NotifyPropertyChangedFor(nameof(RelistPropertyTokensButtonIsVisible))]
        private uint tokensBought = 0;
        public string TokensBoughtWorth => ((decimal)(TokensBought * Metadata?.Financials.PricePerToken ?? 0)).ToCurrencyString();

        public bool BoughtPropertyTokensViewIsVisible => TokensBought > 0;
        public bool RelistPropertyTokensButtonIsVisible => TokensOwned > 0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TokensOwnedWorth))]
        [NotifyPropertyChangedFor(nameof(OwnedPropertyTokensViewIsVisible))]
        private uint tokensOwned = 0;
        public string TokensOwnedWorth => ((decimal)(TokensOwned * Metadata?.Financials.PricePerToken ?? 0)).ToCurrencyString();

        public bool OwnedPropertyTokensViewIsVisible => TokensBought > 0;

        public string MainActionText => getMainActionState() switch
        {
            MainActionStates.Buy => "Buy",
            MainActionStates.ListingExpired => "Expired",
            MainActionStates.RefundBought => "Refund",
            MainActionStates.SoldOut => "Sold Out",
            MainActionStates.CanNotInvest => "You can not invest",
            MainActionStates.SpvToBeCreated => "Waiting for SPV to be created",
            MainActionStates.CreateSpv => "Create SPV",
            MainActionStates.Claim => "Claim",
            MainActionStates.ToBeClaimed => "Waiting for others to claim",
            MainActionStates.ClaimExpired => "Claim Expired",
            MainActionStates.RefundUnclaimed => "Refund",
            MainActionStates.RefundClaimed => "Refund",
            MainActionStates.Relist => "Relist",
            MainActionStates.Unknown => "Unknown",
        };
        public ButtonStateEnum MainActionButtonState => getMainActionState() switch
        {
            MainActionStates.CanNotInvest => ButtonStateEnum.Disabled,
            MainActionStates.Buy => ButtonStateEnum.Enabled,
            MainActionStates.ListingExpired => ButtonStateEnum.Disabled,
            MainActionStates.RefundBought => ButtonStateEnum.Enabled,
            MainActionStates.SoldOut => ButtonStateEnum.Disabled,
            MainActionStates.SpvToBeCreated => ButtonStateEnum.Disabled,
            MainActionStates.CreateSpv => ButtonStateEnum.Enabled,
            MainActionStates.Claim => ButtonStateEnum.Enabled,
            MainActionStates.ToBeClaimed => ButtonStateEnum.Enabled,
            MainActionStates.ClaimExpired => ButtonStateEnum.Disabled,
            MainActionStates.RefundUnclaimed => ButtonStateEnum.Enabled,
            MainActionStates.RefundClaimed => ButtonStateEnum.Enabled,
            MainActionStates.Relist => ButtonStateEnum.Enabled,
            MainActionStates.Unknown => ButtonStateEnum.Disabled,
        };

        public string StatusText => NftWrapper?.Status ?? "Unknown";
        public bool StatusIsVisible => NftWrapper?.StatusIsVisible ?? false;

        [RelayCommand]
        public Task MainActionAsync()
        {
            switch (getMainActionState())
            {
                case MainActionStates.Buy:
                    return BuyAsync();

                case MainActionStates.CreateSpv:
                    return CreateSpvAsync();

                case MainActionStates.Claim:
                    return ClaimAsync();

                case MainActionStates.RefundBought:
                    return RefundBoughtAsync();

                case MainActionStates.RefundUnclaimed:
                    return RefundUnclaimedAsync();

                case MainActionStates.RefundClaimed:
                    return RefundClaimedAsync();

            }

            return Task.FromResult(0);
        }

        public async Task BuyAsync()
        {
            var fullPageLoadingViewModel = DependencyService.Get<FullPageLoadingViewModel>();

            fullPageLoadingViewModel.IsVisible = true;

            if (!await RequirementsModel.CheckRequirementsAsync())
            {
                fullPageLoadingViewModel.IsVisible = false;
                return;
            }

            if (!await RequirementsModel.CheckXcavateRoleAsync(XcavateRole.RealEstateInvestor, CancellationToken.None))
            {
                fullPageLoadingViewModel.IsVisible = false;
                return;
            }

            fullPageLoadingViewModel.IsVisible = false;

            var viewModel = DependencyService.Get<BuyPropertyTokensViewModel>();

            viewModel.ListingDetails = ListingDetails;
            viewModel.Metadata = Metadata;
            viewModel.IsVisible = true;
            viewModel.EndpointKey = PlutoFrameworkCore.NftModel.GetEndpointKey(NftWrapper.NftBase.Type);
        }

        public async Task CreateSpvAsync()
        {
            var token = CancellationToken.None;
            var fullPageLoadingViewModel = DependencyService.Get<FullPageLoadingViewModel>();

            fullPageLoadingViewModel.IsVisible = true;

            if (!await RequirementsModel.CheckRequirementsAsync())
            {
                fullPageLoadingViewModel.IsVisible = false;
                return;
            }

            if (!await RequirementsModel.CheckXcavateRoleAsync(XcavateRole.SpvConfirmation, token))
            {
                fullPageLoadingViewModel.IsVisible = false;
                return;
            }

            fullPageLoadingViewModel.Message = "Connecting to Substrate";

            var client = await SubstrateClientModel.GetOrAddSubstrateClientAsync(Endpoint.Key, token);

            fullPageLoadingViewModel.IsVisible = false;

            var method = MarketplaceCalls.CreateSpv(ListingDetails?.AssetId);

            // Submitting the extrinsic
            var transactionAnalyzerConfirmationViewModel = DependencyService.Get<TransactionAnalyzerConfirmationViewModel>();
            await transactionAnalyzerConfirmationViewModel.LoadAsync(
                client,
                method,
                showDAppView: false,
                token: token
            );
        }

        public async Task ClaimAsync()
        {
            var token = CancellationToken.None;
            var fullPageLoadingViewModel = DependencyService.Get<FullPageLoadingViewModel>();

            fullPageLoadingViewModel.IsVisible = true;

            if (!await RequirementsModel.CheckRequirementsAsync())
            {
                fullPageLoadingViewModel.IsVisible = false;
                return;
            }

            if (!await RequirementsModel.CheckXcavateRoleAsync(XcavateRole.RealEstateInvestor, token))
            {
                fullPageLoadingViewModel.IsVisible = false;
                return;
            }

            fullPageLoadingViewModel.Message = "Connecting to Substrate";

            var client = await SubstrateClientModel.GetOrAddSubstrateClientAsync(Endpoint.Key, token);

            fullPageLoadingViewModel.IsVisible = false;

            var method = MarketplaceCalls.ClaimPropertyShares(ListingDetails?.AssetId);

            // Submitting the extrinsic
            var transactionAnalyzerConfirmationViewModel = DependencyService.Get<TransactionAnalyzerConfirmationViewModel>();
            await transactionAnalyzerConfirmationViewModel.LoadAsync(
                client,
                method,
                showDAppView: false,
                token: token
            );
        }

        public async Task RefundBoughtAsync()
        {
            var token = CancellationToken.None;
            var fullPageLoadingViewModel = DependencyService.Get<FullPageLoadingViewModel>();

            fullPageLoadingViewModel.IsVisible = true;

            if (!await RequirementsModel.CheckRequirementsAsync())
            {
                fullPageLoadingViewModel.IsVisible = false;
                return;
            }

            if (!await RequirementsModel.CheckXcavateRoleAsync(XcavateRole.RealEstateInvestor, token))
            {
                fullPageLoadingViewModel.IsVisible = false;
                return;
            }

            fullPageLoadingViewModel.Message = "Connecting to Substrate";

            var client = await SubstrateClientModel.GetOrAddSubstrateClientAsync(Endpoint.Key, token);

            fullPageLoadingViewModel.IsVisible = false;

            var method = MarketplaceCalls.WithdrawExpired(ListingDetails?.AssetId);

            // Submitting the extrinsic
            var transactionAnalyzerConfirmationViewModel = DependencyService.Get<TransactionAnalyzerConfirmationViewModel>();
            await transactionAnalyzerConfirmationViewModel.LoadAsync(
                client,
                method,
                showDAppView: false,
                token: token
            );
        }

        public async Task RefundUnclaimedAsync()
        {
            var token = CancellationToken.None;
            var fullPageLoadingViewModel = DependencyService.Get<FullPageLoadingViewModel>();

            fullPageLoadingViewModel.IsVisible = true;

            if (!await RequirementsModel.CheckRequirementsAsync())
            {
                fullPageLoadingViewModel.IsVisible = false;
                return;
            }

            if (!await RequirementsModel.CheckXcavateRoleAsync(XcavateRole.RealEstateInvestor, token))
            {
                fullPageLoadingViewModel.IsVisible = false;
                return;
            }

            fullPageLoadingViewModel.Message = "Connecting to Substrate";

            var client = await SubstrateClientModel.GetOrAddSubstrateClientAsync(Endpoint.Key, token);

            fullPageLoadingViewModel.IsVisible = false;

            var method = MarketplaceCalls.WithdrawUnclaimed(ListingDetails?.AssetId);

            // Submitting the extrinsic
            var transactionAnalyzerConfirmationViewModel = DependencyService.Get<TransactionAnalyzerConfirmationViewModel>();
            await transactionAnalyzerConfirmationViewModel.LoadAsync(
                client,
                method,
                showDAppView: false,
                token: token
            );
        }

        public async Task RefundClaimedAsync()
        {
            var token = CancellationToken.None;
            var fullPageLoadingViewModel = DependencyService.Get<FullPageLoadingViewModel>();

            fullPageLoadingViewModel.IsVisible = true;

            if (!await RequirementsModel.CheckRequirementsAsync())
            {
                fullPageLoadingViewModel.IsVisible = false;
                return;
            }

            if (!await RequirementsModel.CheckXcavateRoleAsync(XcavateRole.RealEstateInvestor, token))
            {
                fullPageLoadingViewModel.IsVisible = false;
                return;
            }

            fullPageLoadingViewModel.Message = "Connecting to Substrate";

            var client = await SubstrateClientModel.GetOrAddSubstrateClientAsync(Endpoint.Key, token);

            fullPageLoadingViewModel.IsVisible = false;

            var method = MarketplaceCalls.WithdrawClaimingExpired(ListingDetails?.AssetId);

            // Submitting the extrinsic
            var transactionAnalyzerConfirmationViewModel = DependencyService.Get<TransactionAnalyzerConfirmationViewModel>();
            await transactionAnalyzerConfirmationViewModel.LoadAsync(
                client,
                method,
                showDAppView: false,
                token: token
            );
        }

        [RelayCommand]
        public async Task RelistAsync()
        {
            var fullPageLoadingViewModel = DependencyService.Get<FullPageLoadingViewModel>();

            fullPageLoadingViewModel.IsVisible = true;

            if (!await RequirementsModel.CheckRequirementsAsync())
            {
                fullPageLoadingViewModel.IsVisible = false;

                return;
            }

            fullPageLoadingViewModel.IsVisible = false;

            var viewModel = DependencyService.Get<RelistPropertyTokensViewModel>();

            viewModel.ListingDetails = ListingDetails;
            viewModel.Metadata = Metadata;
            viewModel.IsVisible = true;
            viewModel.EndpointKey = PlutoFrameworkCore.NftModel.GetEndpointKey(NftWrapper.NftBase.Type);
            viewModel.TokensOwned = TokensOwned;
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FavouriteImage))]
        private bool favourite = false;

        public ImageSource FavouriteImage => new FontImageSource
        {
            Color = (Color)Application.Current.Resources["Primary"],
            FontFamily = "FontAwesome",
            Size = 25,
            Glyph = Favourite ? "\uf004" : "\uf08a",
            FontAutoScalingEnabled = false
        };



        [RelayCommand]
        public async Task MakeFavouriteAsync()
        {
            Favourite = !Favourite;

            await XcavatePropertyDatabase.SavePropertyAsync(new NftWrapper
            {
                Endpoint = Endpoint,
                NftBase = NftWrapper.NftBase,
                Favourite = Favourite
            });

            UpdateFavouritePropertiesModel.UpdateFavourite((INftXcavateBase)NftWrapper.NftBase, Favourite);
        }

        [RelayCommand]
        public Task ShareAsync() => Share.RequestAsync(new ShareTextRequest
        {
            Uri = $"https://app.realxmarket.io/marketplace/{ListingDetails?.AssetId}",
            Title = $"Share {Metadata?.PropertyName}",
        });

        [RelayCommand]
        public Task NavigateToFeesAsync() => Shell.Current.Navigation.PushAsync(new ExtensionWebViewPage("https://app.realxmarket.io/property-info-fees"));
    }
}
