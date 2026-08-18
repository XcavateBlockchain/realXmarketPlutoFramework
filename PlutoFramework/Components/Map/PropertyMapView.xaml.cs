using Microsoft.Extensions.Configuration;
using Microsoft.Maui.Controls.Shapes;
using UniqueryPlus.Metadata;

namespace PlutoFramework.Components.Map;

public partial class PropertyMapView : ContentView
{
    private readonly Microsoft.Maui.Controls.WebView mapWebView;

    public static readonly BindableProperty LocationNameProperty = BindableProperty.Create(
        nameof(LocationName), typeof(string), typeof(PropertyMapView), string.Empty,
        propertyChanged: (bindable, oldValue, newValue) =>
        {
            var control = (PropertyMapView)bindable;

            control.UpdateMap();
        });

    public static readonly BindableProperty MapUrlProperty = BindableProperty.Create(
        nameof(MapUrl), typeof(string), typeof(PropertyMapView), string.Empty,
        propertyChanged: (bindable, oldValue, newValue) =>
        {
            var control = (PropertyMapView)bindable;

            control.UpdateMap();
        });

    public static readonly BindableProperty PropertyMetadataProperty = BindableProperty.Create(
        nameof(PropertyMetadata), typeof(PropertyMetadata), typeof(PropertyMapView), null,
        propertyChanged: (bindable, oldValue, newValue) =>
        {
            var control = (PropertyMapView)bindable;

            control.UpdateMap();
        });

    public PropertyMapView()
    {
        mapWebView = new Microsoft.Maui.Controls.WebView
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
        };

        var mapBorder = new Border
        {
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle
            {
                CornerRadius = 20,
            },
            Content = mapWebView,
        };

        var openMapButton = new Border
        {
            BackgroundColor = (Color)Application.Current.Resources["Primary"],
            StrokeThickness = 0,
            IsVisible = false,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Start,
            Margin = 12,
            HeightRequest = 44,
            WidthRequest = 44,
            StrokeShape = new RoundRectangle
            {
                CornerRadius = 22,
            },
            Content = new Image
            {
                HeightRequest = 20,
                WidthRequest = 20,
                Source = new FontImageSource
                {
                    FontFamily = "FontAwesome",
                    Glyph = "\uf08e",
                    Color = (Color)Application.Current.Resources["PrimaryButtonTextColor"],
                    Size = 20,
                },
            },
        };

        openMapButton.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () => await OpenMapAsync()),
        });

        Content = new Grid
        {
            Children =
            {
                mapBorder,
                openMapButton,
            },
        };
    }

    public string LocationName
    {
        get => (string)GetValue(LocationNameProperty);
        set => SetValue(LocationNameProperty, value);
    }

    public string MapUrl
    {
        get => (string)GetValue(MapUrlProperty);
        set => SetValue(MapUrlProperty, value);
    }

    public PropertyMetadata? PropertyMetadata
    {
        get => (PropertyMetadata?)GetValue(PropertyMetadataProperty);
        set => SetValue(PropertyMetadataProperty, value);
    }

    private async void UpdateMap()
    {
        try
        {
            string? googleMapsHtml = GetGoogleMapsEmbedHtml();

            if (string.IsNullOrWhiteSpace(googleMapsHtml))
            {
                IsVisible = false;
                return;
            }

            IsVisible = true;
            mapWebView.Source = new HtmlWebViewSource
            {
                Html = googleMapsHtml,
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine("Property map load error:");
            Console.WriteLine(ex);

            IsVisible = false;
        }
    }

    private string? GetGoogleMapsUrl()
    {
        if (!string.IsNullOrWhiteSpace(MapUrl))
        {
            return MapUrl;
        }

        string? mapQuery = GetMapQuery();

        if (string.IsNullOrWhiteSpace(mapQuery))
        {
            return null;
        }

        return $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(mapQuery)}";
    }

    private string? GetGoogleMapsEmbedUrl()
    {
        string? mapQuery = GetMapQuery();

        if (string.IsNullOrWhiteSpace(mapQuery))
        {
            return MapUrl;
        }

        IConfiguration? configuration = MauiAppBuilderExtensions.Services.GetService<IConfiguration>();
        string? apiKey = configuration?.GetValue<string>("MAPS_EMBED_API_KEY");

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return GetGoogleMapsUrl();
        }

        return $"https://www.google.com/maps/embed/v1/place?key={Uri.EscapeDataString(apiKey)}&q={Uri.EscapeDataString(mapQuery)}";
    }

    private string? GetGoogleMapsEmbedHtml()
    {
        string? embedUrl = GetGoogleMapsEmbedUrl();

        if (string.IsNullOrWhiteSpace(embedUrl))
        {
            return null;
        }

        string escapedEmbedUrl = System.Net.WebUtility.HtmlEncode(embedUrl);

        return $$"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta name="viewport" content="width=device-width, initial-scale=1.0">
                <style>
                    html, body, iframe {
                        width: 100%;
                        height: 100%;
                        margin: 0;
                        padding: 0;
                        border: 0;
                        overflow: hidden;
                    }
                </style>
            </head>
            <body>
                <iframe src="{{escapedEmbedUrl}}"
                        width="100%"
                        height="100%"
                        style="border:0;"
                        allowfullscreen=""
                        loading="lazy"
                        referrerpolicy="no-referrer-when-downgrade">
                </iframe>
            </body>
            </html>
            """;
    }

    private string? GetMapQuery()
    {
        if (!string.IsNullOrWhiteSpace(LocationName) && LocationName != "Unknown address")
        {
            return LocationName;
        }

        if (PropertyMetadata?.Address is null)
        {
            return null;
        }

        string[] addressParts =
        [
            PropertyMetadata.Address.FlatOrUnit,
            PropertyMetadata.Address.Street,
            PropertyMetadata.Address.TownCity,
            PropertyMetadata.Address.LocalAuthority,
            PropertyMetadata.Address.PostCode,
        ];

        string address = string.Join(", ", addressParts.Where(part => !string.IsNullOrWhiteSpace(part)));

        return string.IsNullOrWhiteSpace(address) ? null : address;
    }

    private async Task OpenMapAsync()
    {
        string? googleMapsUrl = GetGoogleMapsUrl();

        if (string.IsNullOrWhiteSpace(googleMapsUrl))
        {
            return;
        }

        await Launcher.Default.OpenAsync(googleMapsUrl);
    }
}
