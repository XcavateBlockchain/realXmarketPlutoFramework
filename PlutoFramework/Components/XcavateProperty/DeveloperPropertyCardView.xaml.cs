using PlutoFramework.Model.Xcavate;

namespace PlutoFramework.Components.XcavateProperty;

/// <summary>
/// One property record as the developer's own store holds it - a draft, a submitted form, a
/// listing that is live - rendered as a card. The marketplace card's shape, without the
/// listing-only rows: a record that has not been listed has no price, no tokens and no yield.
/// </summary>
public partial class DeveloperPropertyCardView : ContentView
{
    public DeveloperPropertyCardView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// The record this card shows. Named for its bindable-property backing so the type name
    /// <see cref="DeveloperProperty"/> stays free.
    /// </summary>
    public static readonly BindableProperty PropertyRecordProperty = BindableProperty.Create(
        nameof(PropertyRecord), typeof(DeveloperProperty), typeof(DeveloperPropertyCardView),
        defaultBindingMode: BindingMode.OneWay,
        propertyChanged: (bindable, oldValue, newValue) =>
        {
            var control = (DeveloperPropertyCardView)bindable;

            control.image.Source = newValue is not DeveloperProperty record
                ? "noimage.png"
                : ImageSource(record.ImageUrl);
        });

    public DeveloperProperty PropertyRecord
    {
        get => (DeveloperProperty)GetValue(PropertyRecordProperty);
        set => SetValue(PropertyRecordProperty, value);
    }

    /// <summary>
    /// The record's photo, or the app's placeholder. Images are uploaded to the web app's own
    /// bucket, so a saved record carries a full URL where a marketplace NFT carries a
    /// shortened one - both arrive here as strings and only one of them needs wrapping.
    /// </summary>
    private static ImageSource ImageSource(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return "noimage.png";
        }

        if (!imageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return imageUrl;
        }

        return new UriImageSource
        {
            Uri = new Uri(imageUrl),
            CacheValidity = new TimeSpan(1, 0, 0),
        };
    }
}
