using PlutoFramework.Components.Buttons;
using System.Windows.Input;

namespace PlutoFramework.Components.XcavateProperty;

/// <summary>
/// The property detail page's finalization-crank banner: a compact card stating what
/// ended and offering the one-tap crank that moves it forward. One control hosts every
/// crank (proposal/challenge/SPV election finalizations, the silent-verdict resolution,
/// the deal settlement and the empty-position close) - each instance's copy and command
/// come from the page's view model through the bindable properties below, following the
/// PropertyTitleWithInfoView push-in idiom.
/// </summary>
public partial class FinalizationBannerView : VerticalStackLayout
{
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(FinalizationBannerView), default(string),
            propertyChanged: (bindable, oldValue, newValue) =>
            {
                var control = (FinalizationBannerView)bindable;
                control.titleLabel.Text = (string)newValue;
            });

    public static readonly BindableProperty DescriptionProperty =
        BindableProperty.Create(nameof(Description), typeof(string), typeof(FinalizationBannerView), default(string),
            propertyChanged: (bindable, oldValue, newValue) =>
            {
                var control = (FinalizationBannerView)bindable;
                control.descriptionLabel.Text = (string)newValue;
            });

    public static readonly BindableProperty ButtonTextProperty =
        BindableProperty.Create(nameof(ButtonText), typeof(string), typeof(FinalizationBannerView), default(string),
            propertyChanged: (bindable, oldValue, newValue) =>
            {
                var control = (FinalizationBannerView)bindable;
                control.actionButton.Text = (string)newValue;
            });

    public static readonly BindableProperty ButtonCommandProperty =
        BindableProperty.Create(nameof(ButtonCommand), typeof(ICommand), typeof(FinalizationBannerView), default(ICommand),
            propertyChanged: (bindable, oldValue, newValue) =>
            {
                var control = (FinalizationBannerView)bindable;
                control.actionButton.Command = (ICommand?)newValue;
            });

    public static readonly BindableProperty ButtonStateProperty =
        BindableProperty.Create(nameof(ButtonState), typeof(ButtonStateEnum), typeof(FinalizationBannerView),
            ButtonStateEnum.Enabled,
            propertyChanged: (bindable, oldValue, newValue) =>
            {
                var control = (FinalizationBannerView)bindable;
                control.actionButton.ButtonState = (ButtonStateEnum)newValue;
            });

    public FinalizationBannerView()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public string ButtonText
    {
        get => (string)GetValue(ButtonTextProperty);
        set => SetValue(ButtonTextProperty, value);
    }

    public ICommand? ButtonCommand
    {
        get => (ICommand?)GetValue(ButtonCommandProperty);
        set => SetValue(ButtonCommandProperty, value);
    }

    public ButtonStateEnum ButtonState
    {
        get => (ButtonStateEnum)GetValue(ButtonStateProperty);
        set => SetValue(ButtonStateProperty, value);
    }
}
