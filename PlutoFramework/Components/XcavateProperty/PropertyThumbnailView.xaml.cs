using PlutoFramework.Components.Loading;
using PlutoFramework.Model.Currency;
using PlutoFramework.Model.SQLite;
using PlutoFrameworkCore.Xcavate;
using UniqueryPlus.Nfts;
using PropertyModel = PlutoFramework.Model.Xcavate.XcavatePropertyModel;

namespace PlutoFramework.Components.XcavateProperty;

public partial class PropertyThumbnailView : ContentView
{
    public static readonly BindableProperty XcavateNftWrapperProperty = BindableProperty.Create(
        nameof(XcavateNftWrapper), typeof(XcavateNftWrapper), typeof(PropertyThumbnailView),
        defaultBindingMode: BindingMode.OneWay,
        propertyChanging: (bindable, oldValue, newValue) =>
        {
            var control = (PropertyThumbnailView)bindable;

            var nftWrapper = (XcavateNftWrapper)newValue;

            var nftBase = (INftXcavateBase)nftWrapper.NftBase;

            if (nftBase.XcavateMetadata is null)
            {
                return;
            }

            control.propertyNameLabel.Text = nftBase.XcavateMetadata.PropertyName;

            control.apyLabel.Text = PropertyModel.GetAPY(nftBase.XcavateMetadata.Financials.EstimatedRentalIncome, nftBase.XcavateMetadata.Financials.PropertyPrice);

            control.priceLabelText.Text = ((double)nftBase.XcavateMetadata.Financials.PropertyPrice).ToCurrencyString();

            control.locationView.LocationName = $"{nftBase.XcavateMetadata.Address.Street}, {nftBase.XcavateMetadata.Address.TownCity}";

            control.image.Source = (nftBase.XcavateMetadata is not null && nftBase.XcavateMetadata.Files.Count() > 0) switch
            {
                // Default image
                false => "noimage.png",
                true => nftBase.XcavateMetadata.Files[0][0..4] switch
                {
                    "http" => new UriImageSource
                    {
                        Uri = new Uri(nftBase.XcavateMetadata.Files[0]),
                        CacheValidity = new TimeSpan(1, 0, 0),
                    },
                    _ => nftBase.XcavateMetadata.Files[0]
                },
            };

            // Status
            if (string.IsNullOrEmpty(nftWrapper.Status))
            {
                control.status.IsVisible = false;
            }
            else
            {
                control.status.IsVisible = true;
                control.statusLabel.Text = nftWrapper.Status;
            }

            // Favourite
            control.filledFavouriteIcon.IsVisible = nftWrapper.Favourite;

            // Tokens owned
            if (nftWrapper.TokensOwned > 0)
            {
                control.tokensTitleLabel.Text = "Tokens owned";

                var tokensOwned = (uint)newValue;

                if (nftBase.XcavateMetadata is not null)
                {
                    control.tokensLabel.Text = $"{tokensOwned} / {nftBase.XcavateMetadata.Financials.NumberOfTokens}";
                }
                else
                {
                    control.tokensLabel.Text = $"{tokensOwned}";
                }
            }
            // Tokens bought
            else if (nftWrapper.TokensBought > 0)
            {
                control.tokensTitleLabel.Text = "Tokens bought";
                var tokensBought = (uint)newValue;
                if (nftBase.XcavateMetadata is not null)
                {
                    control.tokensLabel.Text = $"{tokensBought} / {nftBase.XcavateMetadata.Financials.NumberOfTokens}";
                }
                else
                {
                    control.tokensLabel.Text = $"{tokensBought}";
                }
            }
            else
            {
                if (nftBase is INftXcavateOngoingObjectListing)
                {
                    control.tokensLabel.Text = ((INftXcavateOngoingObjectListing)nftBase).OngoingObjectListingDetails?.ListedTokens.ToString() ?? "-";
                }
                else
                {
                    control.tokensLabel.Text = "-";
                }
            }
        });

    public PropertyThumbnailView()
    {
        InitializeComponent();
    }
    public XcavateNftWrapper XcavateNftWrapper
    {
        get => (XcavateNftWrapper)GetValue(XcavateNftWrapperProperty);
        set => SetValue(XcavateNftWrapperProperty, value);
    }

    void OnFavouriteClicked(System.Object sender, Microsoft.Maui.Controls.TappedEventArgs e)
    {
        XcavateNftWrapper.Favourite = !XcavateNftWrapper.Favourite;

        Task save = XcavatePropertyDatabase.SavePropertyAsync(XcavateNftWrapper);

        UpdateFavouritePropertiesModel.UpdateFavourite((INftXcavateBase)XcavateNftWrapper.NftBase, XcavateNftWrapper.Favourite);
    }

    async void OnMoreClicked(System.Object sender, Microsoft.Maui.Controls.TappedEventArgs e)
    {
        try
        {
            var loadingViewModel = DependencyService.Get<FullPageLoadingViewModel>();

            loadingViewModel.IsVisible = true;

            await XcavatePropertyModel.NavigateToPropertyDetailPageAsync(XcavateNftWrapper, CancellationToken.None);

            loadingViewModel.IsVisible = false;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error navigating to property detail page: " + ex);

            throw;
        }
    }
}