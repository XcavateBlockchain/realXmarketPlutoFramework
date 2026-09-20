using System.Text.RegularExpressions;

namespace PlutoFramework.Components.Animations;

/// <summary>
/// A value display whose digits roll up into place whenever the value changes - the
/// animation the InvestorMainPage cards (<c>XcavateCell.RollingTicker</c>) use. Non-digit
/// characters (currency symbols, separators) render as static text; each digit rolls from
/// the previous value's digit at the same position (from 0 when there was none), staggered
/// right-to-left. A change to a value without digits, or any change while
/// <see cref="IsRollingEnabled"/> is false, renders statically.
/// </summary>
public class RollingTickerView : ContentView
{
    private const int DEFAULT_DIGIT_HEIGHT = 30;
    private const double DEFAULT_FONT_SIZE = 20;
    private const int STAGGER_DELAY_MILLISECONDS = 50;
    private const uint ROLL_DURATION_MILLISECONDS = 600;

    private readonly HorizontalStackLayout valueContainer;
    private string? previousValue;

    public static readonly BindableProperty ValueProperty = BindableProperty.Create(
        nameof(Value), typeof(string), typeof(RollingTickerView),
        defaultValue: null,
        propertyChanged: (bindable, oldValue, newValue) =>
        {
            var control = (RollingTickerView)bindable;
            var newValueStr = newValue as string;

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (control.IsRollingEnabled && !string.IsNullOrEmpty(newValueStr) &&
                    !string.Equals(newValueStr, control.previousValue, StringComparison.Ordinal))
                {
                    await control.ApplyRollingTickerAnimation(control.previousValue, newValueStr);
                }
                else
                {
                    control.UpdateValueDisplay(newValueStr);
                }
                control.previousValue = newValueStr;
            });
        });

    public static readonly BindableProperty IsRollingEnabledProperty = BindableProperty.Create(
        nameof(IsRollingEnabled), typeof(bool), typeof(RollingTickerView),
        defaultValue: true,
        propertyChanged: (bindable, oldValue, newValue) =>
        {
            var control = (RollingTickerView)bindable;
            if ((bool)newValue && !string.IsNullOrEmpty(control.previousValue))
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await control.ApplyRollingTickerAnimation(null, control.previousValue!);
                });
            }
        });

    public RollingTickerView()
    {
        valueContainer = new HorizontalStackLayout
        {
            Spacing = 0,
        };

        Content = valueContainer;
    }

    public string? Value
    {
        get => (string?)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public bool IsRollingEnabled
    {
        get => (bool)GetValue(IsRollingEnabledProperty);
        set => SetValue(IsRollingEnabledProperty, value);
    }

    /// <summary>Height of each digit row; also the distance a digit rolls.</summary>
    public int DigitHeight { get; set; } = DEFAULT_DIGIT_HEIGHT;

    public double FontSize { get; set; } = DEFAULT_FONT_SIZE;

    /// <summary>Null keeps the platform default font.</summary>
    public string? FontFamily { get; set; }

    /// <summary>Null resolves the app's Primary resource at render time.</summary>
    public Color? TextColor { get; set; }

    private List<Segment> ParseValueSegments(string value)
    {
        var segments = new List<Segment>();
        if (string.IsNullOrEmpty(value))
            return segments;

        var pattern = @"(\d+)|([^0-9]+)";
        var matches = Regex.Matches(value, pattern);

        foreach (Match match in matches)
        {
            if (match.Groups[1].Success && !string.IsNullOrEmpty(match.Groups[1].Value))
            {
                segments.Add(new Segment
                {
                    IsNumerical = true,
                    Digits = match.Groups[1].Value.Select(c => c - '0').ToList()
                });
            }
            else if (match.Groups[2].Success && !string.IsNullOrEmpty(match.Groups[2].Value))
            {
                segments.Add(new Segment
                {
                    IsNumerical = false,
                    Text = match.Groups[2].Value
                });
            }
        }

        return segments;
    }

    private Grid CreateRollingDigitView(int fromDigit, int toDigit)
    {
        var container = new Grid
        {
            HeightRequest = DigitHeight
        };

        var outgoingLabel = CreateDigitLabel(fromDigit);
        var incomingLabel = CreateDigitLabel(toDigit);
        incomingLabel.TranslationY = DigitHeight;
        incomingLabel.Opacity = 0;

        container.Children.Add(outgoingLabel);
        container.Children.Add(incomingLabel);

        return container;
    }

    private Label CreateDigitLabel(int digit)
    {
        var label = new Label
        {
            Text = digit.ToString(),
            HeightRequest = DigitHeight,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            TextColor = TextColor ?? (Color)Application.Current!.Resources["Primary"],
            FontSize = FontSize,
            FontAttributes = FontAttributes.Bold
        };

        if (FontFamily is not null)
        {
            label.FontFamily = FontFamily;
        }

        return label;
    }

    private Label CreateStaticTextLabel(string text)
    {
        var label = new Label
        {
            Text = text,
            HeightRequest = DigitHeight,
            VerticalTextAlignment = TextAlignment.Center,
            HorizontalTextAlignment = TextAlignment.Start,
            TextColor = TextColor ?? (Color)Application.Current!.Resources["Primary"],
            FontSize = FontSize,
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(0)
        };

        if (FontFamily is not null)
        {
            label.FontFamily = FontFamily;
        }

        return label;
    }

    private void UpdateValueDisplay(string? value)
    {
        valueContainer.Children.Clear();
        if (!string.IsNullOrEmpty(value))
        {
            valueContainer.Add(CreateStaticTextLabel(value));
        }
    }

    private async Task ApplyRollingTickerAnimation(string? oldValue, string newValue)
    {
        valueContainer.Children.Clear();

        var newSegments = ParseValueSegments(newValue);
        var oldSegments = string.IsNullOrEmpty(oldValue) ? new List<Segment>() : ParseValueSegments(oldValue);
        var numericalSegments = newSegments.Where(s => s.IsNumerical).ToList();
        var totalNumericalDigits = numericalSegments.Sum(s => s.Digits!.Count);

        if (totalNumericalDigits == 0)
        {
            UpdateValueDisplay(newValue);
            return;
        }

        // Flatten old numerical digits for easy index access
        var oldNumericalDigits = oldSegments.Where(s => s.IsNumerical).SelectMany(s => s.Digits!).ToList();

        var animations = new List<Task>();
        var globalDigitIndex = 0;

        foreach (var segment in newSegments)
        {
            if (segment.IsNumerical && segment.Digits!.Count > 0)
            {
                foreach (var digit in segment.Digits!)
                {
                    // Get the previous digit at this position, or 0 if not available
                    var fromDigit = 0;
                    if (globalDigitIndex < oldNumericalDigits.Count)
                    {
                        fromDigit = oldNumericalDigits[globalDigitIndex];
                    }

                    var rollingView = CreateRollingDigitView(fromDigit, digit);
                    valueContainer.Add(rollingView);

                    var delay = (totalNumericalDigits - 1 - globalDigitIndex) * STAGGER_DELAY_MILLISECONDS;
                    animations.Add(StartDelayedAnimation(rollingView, fromDigit, digit, ROLL_DURATION_MILLISECONDS, delay));

                    globalDigitIndex++;
                }
            }
            else if (!segment.IsNumerical)
            {
                valueContainer.Add(CreateStaticTextLabel(segment.Text!));
            }
        }

        await Task.WhenAll(animations);

        if (string.Equals(Value, newValue, StringComparison.Ordinal))
        {
            UpdateValueDisplay(newValue);
        }
    }

    private async Task StartDelayedAnimation(Grid rollingView, int fromDigit, int toDigit, uint duration, int delayMs)
    {
        await Task.Delay(delayMs);
        await AnimateDigit(rollingView, fromDigit, toDigit, duration);
    }

    private async Task AnimateDigit(Grid rollingView, int fromDigit, int toDigit, uint duration)
    {
        if (rollingView.Children.Count < 2 ||
            rollingView.Children[0] is not Label outgoingLabel ||
            rollingView.Children[1] is not Label incomingLabel)
            return;

        await Task.WhenAll(
            outgoingLabel.TranslateTo(0, -DigitHeight, duration, Easing.CubicOut),
            outgoingLabel.FadeTo(0, duration, Easing.CubicOut),
            incomingLabel.TranslateTo(0, 0, duration, Easing.CubicOut),
            incomingLabel.FadeTo(1, duration, Easing.CubicOut));
    }

    private class Segment
    {
        public bool IsNumerical { get; set; }
        public string? Text { get; set; }
        public List<int>? Digits { get; set; }
    }
}
