
using CommunityToolkit.Mvvm.Input;

namespace PlutoFramework.Components.Xcavate;

public partial class XcavateCell : ContentView
{
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

            // The rolling decision (animate vs. static) lives in RollingTickerView, keyed
            // off the IsRollingEnabled flag the RollingTicker property below keeps in sync.
            control.rollingTicker.Value = newValue as string;
        });

    public static readonly BindableProperty SecondaryValueProperty = BindableProperty.Create(
        nameof(SecondaryValue), typeof(string), typeof(XcavateCell),
        defaultValue: string.Empty,
        propertyChanged: (bindable, oldValue, newValue) =>
        {
            var control = (XcavateCell)bindable;
            var newValueStr = (string?)newValue ?? string.Empty;

            MainThread.BeginInvokeOnMainThread(() => control.ApplySecondaryValue(newValueStr));
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

            control.rollingTicker.IsRollingEnabled = (bool)newValue;
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

    public string SecondaryValue
    {
        get => (string)GetValue(SecondaryValueProperty);
        set => SetValue(SecondaryValueProperty, value);
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

    /// <summary>
    /// Shows or hides the optional second line under the value and grows the cell to fit
    /// it - 92 outer and 84 inner bounds, back to the XAML's 80/70 when cleared.
    /// </summary>
    private void ApplySecondaryValue(string value)
    {
        if (secondaryValueLabel is null || contentStack is null || cellLayout is null)
        {
            return;
        }

        var hasSecondary = !string.IsNullOrEmpty(value);

        secondaryValueLabel.Text = value;
        secondaryValueLabel.IsVisible = hasSecondary;

        var outerHeight = hasSecondary ? 92 : 80;
        var innerHeight = hasSecondary ? 84 : 70;

        HeightRequest = outerHeight;
        AbsoluteLayout.SetLayoutBounds(cellLayout, new Rect(0.5, 0.5, 1, outerHeight));
        AbsoluteLayout.SetLayoutBounds(contentStack, new Rect(0.5, 0.5, 1, innerHeight));
    }
}
