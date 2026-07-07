
using CommunityToolkit.Mvvm.Input;
using System.Text.RegularExpressions;

namespace PlutoFramework.Components.Xcavate;

public partial class XcavateCell : ContentView
{
    private string _previousValue;
    private const int DigitHeight = 30;
    private const int StaggerDelay = 50;

    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title), typeof(string), typeof(XcavateCell),
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanging: (bindable, oldValue, newValue) =>
        {
            var control = (XcavateCell)bindable;

            control.titleView.Title = ((string)newValue);
        });

    public static readonly BindableProperty ValueProperty = BindableProperty.Create(
        nameof(Value), typeof(string), typeof(XcavateCell),
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanged: (bindable, oldValue, newValue) =>
        {
            var control = (XcavateCell)bindable;
            var newValueStr = newValue as string;

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (control.RollingTicker && !string.IsNullOrEmpty(newValueStr) &&
                    !string.Equals(newValueStr, control._previousValue, StringComparison.Ordinal))
                {
                    await control.ApplyRollingTickerAnimation(control._previousValue, newValueStr);
                }
                else
                {
                    control.UpdateValueDisplay(newValueStr);
                }
                control._previousValue = newValueStr;
            });
        });


    public static readonly BindableProperty CommandProperty = BindableProperty.Create(
        nameof(Command), typeof(IAsyncRelayCommand), typeof(XcavateCell),
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanging: (bindable, oldValue, newValue) =>
        {
            var control = (XcavateCell)bindable;

            control.tapGestureRecognizer.Command = (IAsyncRelayCommand)newValue;

            control.arrow.IsVisible = newValue != null;
        });

    public static readonly BindableProperty InfoCommandProperty = BindableProperty.Create(
        nameof(InfoCommand), typeof(IAsyncRelayCommand), typeof(XcavateCell),
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanging: (bindable, oldValue, newValue) =>
        {
            var control = (XcavateCell)bindable;

            control.titleView.Command = (IAsyncRelayCommand)newValue;
        });

    public static readonly BindableProperty RollingTickerProperty = BindableProperty.Create(
        nameof(RollingTicker), typeof(bool), typeof(XcavateCell),
        defaultValue: false,
        propertyChanged: (bindable, oldValue, newValue) =>
        {
            var control = (XcavateCell)bindable;
            if ((bool)newValue && !string.IsNullOrEmpty(control._previousValue))
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await control.ApplyRollingTickerAnimation(null, control._previousValue);
                });
            }
        });

    public XcavateCell()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public IAsyncRelayCommand Command
    {
        get => (IAsyncRelayCommand)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public IAsyncRelayCommand InfoCommand
    {
        get => (IAsyncRelayCommand)GetValue(InfoCommandProperty);
        set => SetValue(InfoCommandProperty, value);
    }

    public bool RollingTicker
    {
        get => (bool)GetValue(RollingTickerProperty);
        set => SetValue(RollingTickerProperty, value);
    }

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

    private Grid CreateRollingTickerView(int fromDigit, int toDigit)
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
        return new Label
        {
            Text = digit.ToString(),
            HeightRequest = DigitHeight,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            TextColor = (Color)Application.Current.Resources["Primary"],
            FontSize = 20,
            FontFamily = "XcavateFont",
            FontAttributes = FontAttributes.Bold
        };
    }

    private Label CreateStaticTextLabel(string text)
    {
        return new Label
        {
            Text = text,
            HeightRequest = DigitHeight,
            VerticalTextAlignment = TextAlignment.Center,
            HorizontalTextAlignment = TextAlignment.Start,
            TextColor = (Color)Application.Current.Resources["Primary"],
            FontSize = 20,
            FontFamily = "XcavateFont",
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(0)
        };
    }

    private void UpdateValueDisplay(string value)
    {
        if (valueContainer == null) return;

        valueContainer.Children.Clear();
        if (!string.IsNullOrEmpty(value))
        {
            valueContainer.Add(CreateStaticTextLabel(value));
        }
    }

    private async Task ApplyRollingTickerAnimation(string oldValue, string newValue)
    {
        if (valueContainer == null) return;

        valueContainer.Children.Clear();

        var newSegments = ParseValueSegments(newValue);
        var oldSegments = string.IsNullOrEmpty(oldValue) ? new List<Segment>() : ParseValueSegments(oldValue);
        var numericalSegments = newSegments.Where(s => s.IsNumerical).ToList();
        var totalNumericalDigits = numericalSegments.Sum(s => s.Digits.Count);

        if (totalNumericalDigits == 0)
        {
            UpdateValueDisplay(newValue);
            return;
        }

        // Flatten old numerical digits for easy index access
        var oldNumericalDigits = oldSegments.Where(s => s.IsNumerical).SelectMany(s => s.Digits).ToList();

        var animations = new List<Task>();
        var globalDigitIndex = 0;

        foreach (var segment in newSegments)
        {
            if (segment.IsNumerical && segment.Digits.Count > 0)
            {
                foreach (var digit in segment.Digits)
                {
                    // Get the previous digit at this position, or 0 if not available
                    var fromDigit = 0;
                    if (globalDigitIndex < oldNumericalDigits.Count)
                    {
                        fromDigit = oldNumericalDigits[globalDigitIndex];
                    }

                    var rollingView = CreateRollingTickerView(fromDigit, digit);
                    valueContainer.Add(rollingView);

                    var delay = (totalNumericalDigits - 1 - globalDigitIndex) * StaggerDelay;
                    animations.Add(StartDelayedAnimation(rollingView, fromDigit, digit, 600, delay));

                    globalDigitIndex++;
                }
            }
            else if (!segment.IsNumerical)
            {
                valueContainer.Add(CreateStaticTextLabel(segment.Text));
            }
        }

        await Task.WhenAll(animations);
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
}

internal class Segment
{
    public bool IsNumerical { get; set; }
    public string Text { get; set; }
    public List<int> Digits { get; set; }
}