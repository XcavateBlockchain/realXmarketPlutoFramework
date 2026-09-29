using Microsoft.Extensions.Configuration;
using Microsoft.Maui.Controls.Shapes;
using UniqueryPlus.Metadata;

namespace PlutoFramework.Components.Map;

public partial class PropertyMapView : ContentView
{
    /// <summary>
    /// Only created in the no-API-key fallback path. An interactive Google Maps WebView
    /// inside the page's ScrollView re-composites its (large, self-animating) surface on
    /// every scroll frame and swallows any scroll gesture that starts over it, so it is
    /// no longer the default - and Chromium is never started for the static image path.
    /// </summary>
    private Microsoft.Maui.Controls.WebView? mapWebView;

    private readonly Image staticMapImage;
    private readonly Grid mapContent;

    /// <summary>
    /// Per-URL verdict of the Static API probe: the Maps Static API has to be enabled on
    /// the same Google Cloud project as MAPS_EMBED_API_KEY, and when it is not, Google
    /// answers every staticmap request with a 403 and the map would render as an empty
    /// box. Remembered per URL so each address is probed once.
    /// </summary>
    private static readonly System.Collections.Generic.Dictionary<string, bool> staticMapSupport = new();

    private static readonly System.Net.Http.HttpClient probeClient = new();

    /// <summary>
    /// Whatever UpdateMap last put on screen (static map URL or embed HTML), so the
    /// LocationName / MapUrl / PropertyMetadata change callbacks - all of which fire when
    /// one metadata object arrives - do not each trigger a full map reload.
    /// </summary>
    private string? loadedMap;

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
        staticMapImage = new Image
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Aspect = Aspect.AspectFill,
        };

        // A tap on the map opens the live interactive map in the external maps app.
        staticMapImage.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () => await OpenMapAsync()),
        });

        mapContent = new Grid
        {
            Children =
            {
                staticMapImage,
            },
        };

        var mapBorder = new Border
        {
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle
            {
                CornerRadius = 20,
            },
            Content = mapContent,
        };

        var openMapButton = new Border
        {
            BackgroundColor = (Color)Application.Current!.Resources["Primary"],
            StrokeThickness = 0,
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

    private void UpdateMap()
    {
        try
        {
            string? staticMapUrl = GetStaticMapUrl();

            if (staticMapUrl is not null && StaticMapSupported(staticMapUrl))
            {
                if (staticMapUrl == loadedMap)
                {
                    return;
                }

                loadedMap = staticMapUrl;

                if (mapWebView is not null)
                {
                    mapWebView.IsVisible = false;
                }

                IsVisible = true;
                staticMapImage.IsVisible = true;
                staticMapImage.Source = staticMapUrl;
                return;
            }

            string? googleMapsHtml = GetGoogleMapsEmbedHtml();

            if (string.IsNullOrWhiteSpace(googleMapsHtml))
            {
                loadedMap = null;
                IsVisible = false;
                return;
            }

            if (googleMapsHtml == loadedMap)
            {
                return;
            }

            loadedMap = googleMapsHtml;

            IsVisible = true;
            staticMapImage.IsVisible = false;
            EnsureWebView();
            mapWebView!.Source = new HtmlWebViewSource
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

    /// <summary>
    /// A single cached bitmap from the Maps Static API, reusing the key that
    /// MAPS_EMBED_API_KEY already configures. Null when no key is set - the caller then
    /// falls back to the embed WebView.
    /// </summary>
    private string? GetStaticMapUrl()
    {
        string? mapQuery = GetMapQuery();

        if (string.IsNullOrWhiteSpace(mapQuery))
        {
            return null;
        }

        IConfiguration? configuration = MauiAppBuilderExtensions.Services.GetService<IConfiguration>();
        string? apiKey = configuration?.GetValue<string>("MAPS_EMBED_API_KEY");

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        string escapedQuery = Uri.EscapeDataString(mapQuery);

        // 640x350 at scale 2 covers full-width phones at native density. AspectFill crops
        // the bitmap vertically on wider layouts; the marker sits at the center, which the
        // crop preserves. The marker color matches the app's Primary (#3B4F74).
        return "https://maps.googleapis.com/maps/api/staticmap"
            + $"?center={escapedQuery}"
            + "&zoom=15"
            + "&size=640x350"
            + "&scale=2"
            + $"&markers=color:0x3b4f74%7C{escapedQuery}"
            + $"&key={Uri.EscapeDataString(apiKey)}";
    }

    /// <summary>
    /// Optimistic support check: the static image is shown immediately, and a one-off
    /// HEAD probe verifies the key can actually use the Maps Static API. A negative
    /// verdict flips this view to the embed WebView fallback.
    /// </summary>
    private bool StaticMapSupported(string staticMapUrl)
    {
        if (staticMapSupport.TryGetValue(staticMapUrl, out bool supported))
        {
            return supported;
        }

        _ = ProbeStaticMapAsync(staticMapUrl);

        return true;
    }

    private async Task ProbeStaticMapAsync(string staticMapUrl)
    {
        try
        {
            // GET (headers only) rather than HEAD: plain GET is proven to work on every
            // network the app itself uses, while a HEAD can be dropped by proxies and
            // would otherwise hang until the HttpClient timeout. The short timeout keeps
            // a silent drop from delaying the fallback for a minute and more.
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, staticMapUrl);
            using var response = await probeClient.SendAsync(request, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);

            staticMapSupport[staticMapUrl] = response.IsSuccessStatusCode;

            if (!response.IsSuccessStatusCode)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    // Force UpdateMap past the "already loaded" shortcut so the fallback
                    // embed replaces the (blank) static image.
                    loadedMap = null;
                    UpdateMap();
                });
            }
        }
        catch (Exception)
        {
            // Probe failure (offline, DNS, timeout): keep the static image - the fallback
            // WebView would have nothing to load either.
        }
    }

    private void EnsureWebView()
    {
        if (mapWebView is not null)
        {
            return;
        }

        mapWebView = new Microsoft.Maui.Controls.WebView
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,

            // Non-interactive even in the fallback: an interactive WebView inside the
            // page's ScrollView consumes scroll gestures that start over it. The map's
            // own tap gesture and the open-map button lead to the external live map.
            InputTransparent = true,
        };

        mapContent.Children.Add(mapWebView);
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

        // embed/v1 is the iframe-embeddable endpoint (plain google.com/maps URLs are
        // X-Frame-Options-blocked); it uses the Embed API, which the configured key has.
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

        string?[] addressParts =
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
