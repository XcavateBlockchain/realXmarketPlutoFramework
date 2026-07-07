namespace PlutoFramework.Components.Xcavate;

/// <summary>
/// A single-digit rolling ticker view that animates digit changes with a slot-machine effect.
/// </summary>
public class RollingDigitView : ContentView
{
    public static readonly BindableProperty DigitProperty = BindableProperty.Create(
        nameof(Digit),
        typeof(string),
        typeof(RollingDigitView),
        "0",
        propertyChanged: (bindable, oldValue, newValue) =>
        {
            if (bindable is RollingDigitView view && newValue is string newDigit)
            {
                view.SetDigit(newDigit, animate: true);
            }
        });

    private readonly Label _digitLabel;
    private bool _isAnimating;

    public RollingDigitView()
    {
        _digitLabel = new Label
        {
            Text = "0",
            FontSize = 20,
            FontFamily = "XcavateFont",
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#FFFFFF"),
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            HeightRequest = 28,
            WidthRequest = 40
        };

        HeightRequest = 28;
        WidthRequest = 40;
        Content = _digitLabel;
    }

    public string Digit
    {
        get => (string)GetValue(DigitProperty);
        set => SetValue(DigitProperty, value);
    }

    private async void SetDigit(string newDigit, bool animate)
    {
        if (_isAnimating) return;

        var currentDigit = Digit;
        if (currentDigit == newDigit) return;

        if (!char.IsDigit(newDigit[0])) return;

        if (!animate)
        {
            _digitLabel.Text = newDigit;
            return;
        }

        _isAnimating = true;
        await Task.WhenAll(
            _digitLabel.TranslateTo(0, -28, 150, Easing.CubicIn),
            _digitLabel.FadeTo(0, 150, Easing.CubicIn));

        _digitLabel.Text = newDigit;
        _digitLabel.TranslationY = 28;

        await Task.WhenAll(
            _digitLabel.TranslateTo(0, 0, 250, Easing.CubicOut),
            _digitLabel.FadeTo(1, 250, Easing.CubicOut));

        _isAnimating = false;
    }
}