using PlutoFramework.Model.Currency;
using PlutoFramework.Model.SQLite;
using PlutoFramework.Model.Xcavate;
using PlutoFrameworkCore.Xcavate;
using UniqueryPlus.Nfts;
using PropertyModel = PlutoFramework.Model.Xcavate.XcavatePropertyModel;

namespace PlutoFramework.Components.XcavateProperty;

public partial class PropertyThumbnailView : ContentView
{
    public static readonly BindableProperty XcavateNftWrapperProperty = BindableProperty.Create(
        nameof(XcavateNftWrapper), typeof(XcavateNftWrapper), typeof(PropertyThumbnailView),
        defaultBindingMode: BindingMode.OneWay,
        propertyChanged: (bindable, oldValue, newValue) =>
        {
            Console.WriteLine("Beginning to load property info");
            var control = (PropertyThumbnailView)bindable;

            if (newValue is not XcavateNftWrapper nftWrapper || nftWrapper.NftBase is not INftXcavateBase nftBase)
            {
                return;
            }

            if (nftBase.XcavateMetadata is null)
            {
                return;
            }

            control.propertyNameLabel.Text = nftBase.XcavateMetadata.PropertyName;

            control.apyLabel.Text = PropertyModel.GetAPY(nftBase.XcavateMetadata.Financials);

            control.priceLabelText.Text = ((double)nftBase.XcavateMetadata.Financials.PropertyPrice).ToCurrencyString();

            control.locationView.LocationName = nftBase.XcavateMetadata.Address.TownCity!;

            // The compressed mirror thumbnails when the indexer supplied them, the
            // full-resolution images otherwise (DisplayImages does the fallback).
            var images = nftBase.XcavateMetadata.DisplayImages;

            control.image.Source = (images.Count > 0) switch
            {
                // Default image
                false => "noimage.png",
                true => images[0][0..4] switch
                {
                    "http" => new UriImageSource
                    {
                        Uri = new Uri(images[0]),
                        CacheValidity = new TimeSpan(1, 0, 0),
                    },
                    _ => images[0]
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

                var tokensOwned = nftWrapper.TokensOwned;

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
                var tokensBought = nftWrapper.TokensBought;
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

            Console.WriteLine("Property info loaded");
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

    /// <summary>
    /// The secondary-market share listing this card stands for, set by the noticeboard
    /// feed; null on the primary-market pages. Handed through to the property detail
    /// page when the card is tapped, so it opens with its secondary-market context.
    /// </summary>
    public static readonly BindableProperty ShareListingContextProperty = BindableProperty.Create(
        nameof(ShareListingContext), typeof(XcavateShareListingContext), typeof(PropertyThumbnailView),
        defaultValue: null,
        defaultBindingMode: BindingMode.OneWay);

    public XcavateShareListingContext? ShareListingContext
    {
        get => (XcavateShareListingContext?)GetValue(ShareListingContextProperty);
        set => SetValue(ShareListingContextProperty, value);
    }

    /// <summary>
    /// Drives the "Claim rent" chip's visibility. Bound by the portfolio page to the
    /// item's <see cref="XcavateNftWrapper.HasClaimableRent"/>; defaults to hidden on
    /// every other feed the thumbnail renders in.
    /// </summary>
    public static readonly BindableProperty ClaimRentIsVisibleProperty = BindableProperty.Create(
        nameof(ClaimRentIsVisible), typeof(bool), typeof(PropertyThumbnailView),
        defaultValue: false,
        defaultBindingMode: BindingMode.OneWay,
        propertyChanged: (bindable, oldValue, newValue) =>
        {
            ((PropertyThumbnailView)bindable).claimRentChip.IsVisible = (bool)newValue;
        });

    public bool ClaimRentIsVisible
    {
        get => (bool)GetValue(ClaimRentIsVisibleProperty);
        set => SetValue(ClaimRentIsVisibleProperty, value);
    }

    /// <summary>True while this chip's own claim is in flight (double-tap guard).</summary>
    private bool claimRentRunning;

    void OnFavouriteClicked(System.Object sender, Microsoft.Maui.Controls.TappedEventArgs e)
    {
        XcavateNftWrapper.Favourite = !XcavateNftWrapper.Favourite;

        Task save = XcavatePropertyDatabase.SavePropertyAsync(XcavateNftWrapper);

        UpdateFavouritePropertiesModel.UpdateFavourite((INftXcavateBase)XcavateNftWrapper.NftBase, XcavateNftWrapper.Favourite);
    }

    private async void OnMoreClicked(System.Object sender, Microsoft.Maui.Controls.TappedEventArgs e)
    {
        try
        {
            await XcavatePropertyModel.NavigateToPropertyDetailPageAsync(XcavateNftWrapper, CancellationToken.None, ShareListingContext);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error navigating to property detail page: " + ex);
        }
    }

    /// <summary>
    /// The "Claim rent" chip: claims this property's claimable rent straight from the
    /// portfolio, through <see cref="ClaimRentModel"/> so the claim queues behind any
    /// claim-all already running instead of racing it. The status popup reports the
    /// outcome; the portfolio's income figures refresh off the pipeline's confirmation
    /// event, so nothing here patches them. A confirmed claim also drops the chip: the
    /// re-scan flips <see cref="XcavateNftWrapper.HasClaimableRent"/> off.
    /// </summary>
    private async void OnClaimRentClicked(object sender, EventArgs e)
    {
        if (claimRentRunning
            || XcavateNftWrapper.NftBase is not XcavateSolanaListingNft solanaListing)
        {
            return;
        }

        claimRentRunning = true;
        claimRentChip.ButtonState = Components.Buttons.ButtonStateEnum.Disabled;

        try
        {
            var propertyName = solanaListing.XcavateMetadata?.PropertyName ?? "this property";

            await ClaimRentModel.ClaimAsync([new ClaimRentModel.ClaimTarget(solanaListing.AssetId, propertyName)]);
        }
        finally
        {
            claimRentRunning = false;
            claimRentChip.ButtonState = Components.Buttons.ButtonStateEnum.Enabled;
        }
    }
}
