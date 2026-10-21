using PlutoFramework.Model.Xcavate;
using System.Windows.Input;

namespace PlutoFramework.Components.XcavateProperty;

/// <summary>
/// One property record as the developer's own store holds it - a draft, a submitted form, a
/// listing that is live - rendered as a card. The marketplace card's shape and rows, filled
/// from the record's declared fields: a draft mid-form has no finances section, so the shares,
/// price and gross-yield rows hide rather than showing zeroes.
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
    /// The command the card's tap fires, with <see cref="PropertyRecord"/> as the parameter.
    /// The recognizer lives on the inner card itself, next to the one <c>Card</c> is born
    /// with: MAUI gestures do not bubble, so a recognizer attached to this wrapper would be
    /// swallowed by the card's own and would never fire.
    /// </summary>
    public static readonly BindableProperty TapCommandProperty = BindableProperty.Create(
        nameof(TapCommand), typeof(ICommand), typeof(DeveloperPropertyCardView));

    public ICommand? TapCommand
    {
        get => (ICommand?)GetValue(TapCommandProperty);
        set => SetValue(TapCommandProperty, value);
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
