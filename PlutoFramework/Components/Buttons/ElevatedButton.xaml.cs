using System.Windows.Input;

namespace PlutoFramework.Components.Buttons;

public partial class ElevatedButton : Button
{
    public static readonly BindableProperty ButtonStateProperty = BindableProperty.Create(
        nameof(ButtonState), typeof(ButtonStateEnum), typeof(ElevatedButton),
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanging: (bindable, oldValue, newValue) =>
        {
            var control = (ElevatedButton)bindable;

            control.IsVisible = (ButtonStateEnum)newValue != ButtonStateEnum.Invisible;

            control.BorderWidth = 0;

            switch ((ButtonStateEnum)newValue)
            {
                case ButtonStateEnum.Enabled:
                    if (Application.Current.Resources.TryGetValue("Primary", out object primaryColor))
                    {
                        control.BackgroundColor = (Color)primaryColor;
                    }

                    control.TextColor = Colors.White;

                    control.IsEnabled = true;
                    break;
                case ButtonStateEnum.GrayEnabled:
                    control.SetAppThemeColor(Button.BackgroundColorProperty, Colors.White, Colors.Black);

                    control.BorderWidth = (double)Application.Current.Resources["GrayButtonBorderWidth"];
                    control.BorderColor = Color.FromArgb("#88A6A6A6");

                    control.SetAppThemeColor(Button.TextColorProperty, Color.FromArgb("#A6A6A6"), Colors.White);
                    control.IsEnabled = true;
                    break;
                case ButtonStateEnum.Disabled:
                    if (Application.Current.Resources.TryGetValue("PrimaryUnimportant", out object primaryUnimportantColor))
                    {
                        control.BackgroundColor = (Color)primaryUnimportantColor;
                    }

                    // Stays natively enabled so taps are still received (a disabled native
                    // button gets no touch events on Android). Clicks are swallowed in
                    // OnBaseClicked, which vibrates and shakes the button instead.
                    control.IsEnabled = true;
                    break;
                case ButtonStateEnum.Warning:
                    control.IsEnabled = true;
                    control.BackgroundColor = (Color)Application.Current.Resources["DangerousRed"];
                    control.TextColor = Colors.White;
                    break;
            }
        },
        defaultValue: ButtonStateEnum.Enabled);

    // Command and Clicked are shadowed so that ElevatedButton controls when they fire.
    // When ButtonState is Disabled, clicks are swallowed and feedback is shown instead.
    public static new readonly BindableProperty CommandProperty = BindableProperty.Create(
        nameof(Command), typeof(ICommand), typeof(ElevatedButton));

    public static new readonly BindableProperty CommandParameterProperty = BindableProperty.Create(
        nameof(CommandParameter), typeof(object), typeof(ElevatedButton));

    public new event EventHandler? Clicked;

    private bool isIndicatingDisabled = false;

    public ElevatedButton()
    {
        InitializeComponent();

        base.Clicked += OnBaseClicked;
    }

    public ButtonStateEnum ButtonState
    {
        get => (ButtonStateEnum)GetValue(ButtonStateProperty);
        set => SetValue(ButtonStateProperty, value);
    }

    public new ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public new object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    private async void OnBaseClicked(object? sender, EventArgs e)
    {
        if (ButtonState == ButtonStateEnum.Disabled)
        {
            await IndicateDisabledAsync();

            return;
        }

        Clicked?.Invoke(this, e);

        if (Command is not null && Command.CanExecute(CommandParameter))
        {
            Command.Execute(CommandParameter);
        }
    }

    /// <summary>
    /// Vibrates the phone and shakes the button from side to side
    /// to tell the user that the button is disabled.
    /// </summary>
    private async Task IndicateDisabledAsync()
    {
        if (isIndicatingDisabled)
        {
            return;
        }

        isIndicatingDisabled = true;

        try
        {
            try
            {
                Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(100));
            }
            catch (FeatureNotSupportedException)
            {
                // Vibration is not supported on this platform.
            }

            const uint stepDuration = 50;

            await this.TranslateTo(-10, 0, stepDuration);
            await this.TranslateTo(8, 0, stepDuration);
            await this.TranslateTo(-6, 0, stepDuration);
            await this.TranslateTo(4, 0, stepDuration);
            await this.TranslateTo(-2, 0, stepDuration);
            await this.TranslateTo(0, 0, stepDuration);
        }
        finally
        {
            isIndicatingDisabled = false;
        }
    }
}
