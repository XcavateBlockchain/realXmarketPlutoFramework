namespace PlutoFramework.Components.Xcavate;

using System.Windows.Input;
using PlutoFramework.Components.Buttons;

public partial class PageBottomBarTwoButtonsView : ContentView
{
    public static readonly BindableProperty LeftCommandProperty =
        BindableProperty.Create(nameof(LeftCommand), typeof(ICommand), typeof(PageBottomBarTwoButtonsView),
            propertyChanged: (BindableObject bindable, object oldValue, object newValue) =>
            {
                var control = (PageBottomBarTwoButtonsView)bindable;
                if (control._leftButton != null && newValue is ICommand command)
                {
                    control._leftButton.Command = command;
                }
            });

    public static readonly BindableProperty LeftTextProperty =
        BindableProperty.Create(nameof(LeftText), typeof(string), typeof(PageBottomBarTwoButtonsView),
            propertyChanged: (BindableObject bindable, object oldValue, object newValue) =>
            {
                var control = (PageBottomBarTwoButtonsView)bindable;
                if (control._leftButton != null)
                {
                    control._leftButton.Text = newValue?.ToString();
                }
            });

    public static readonly BindableProperty RightCommandProperty =
        BindableProperty.Create(nameof(RightCommand), typeof(ICommand), typeof(PageBottomBarTwoButtonsView),
            propertyChanged: (BindableObject bindable, object oldValue, object newValue) =>
            {
                var control = (PageBottomBarTwoButtonsView)bindable;
                if (control._rightButton != null && newValue is ICommand command)
                {
                    control._rightButton.Command = command;
                }
            });

    public static readonly BindableProperty RightTextProperty =
        BindableProperty.Create(nameof(RightText), typeof(string), typeof(PageBottomBarTwoButtonsView),
            propertyChanged: (BindableObject bindable, object oldValue, object newValue) =>
            {
                var control = (PageBottomBarTwoButtonsView)bindable;
                if (control._rightButton != null)
                {
                    control._rightButton.Text = newValue?.ToString();
                }
            });

    // ButtonState defaults to None, meaning "not bound": the inner buttons keep
    // their own Enabled default until a state is actually set.
    public static readonly BindableProperty LeftButtonStateProperty =
        BindableProperty.Create(nameof(LeftButtonState), typeof(ButtonStateEnum), typeof(PageBottomBarTwoButtonsView),
            defaultValue: ButtonStateEnum.None,
            propertyChanged: (BindableObject bindable, object oldValue, object newValue) =>
            {
                var control = (PageBottomBarTwoButtonsView)bindable;
                if (control._leftButton != null && (ButtonStateEnum)newValue != ButtonStateEnum.None)
                {
                    control._leftButton.ButtonState = (ButtonStateEnum)newValue;
                }
            });

    public static readonly BindableProperty RightButtonStateProperty =
        BindableProperty.Create(nameof(RightButtonState), typeof(ButtonStateEnum), typeof(PageBottomBarTwoButtonsView),
            defaultValue: ButtonStateEnum.None,
            propertyChanged: (BindableObject bindable, object oldValue, object newValue) =>
            {
                var control = (PageBottomBarTwoButtonsView)bindable;
                if (control._rightButton != null && (ButtonStateEnum)newValue != ButtonStateEnum.None)
                {
                    control._rightButton.ButtonState = (ButtonStateEnum)newValue;
                }
            });

    // The right button is typed as ElevatedButton so Command assignments go through
    // its shadowed Command property, which swallows clicks while the button is
    // Disabled. BasicGrayButton disables itself natively, so no shadowing is needed.
    private BasicGrayButton? _leftButton;
    private ElevatedButton? _rightButton;

    public ICommand LeftCommand
    {
        get => (ICommand)GetValue(LeftCommandProperty);
        set => SetValue(LeftCommandProperty, value);
    }

    public string LeftText
    {
        get => (string)GetValue(LeftTextProperty);
        set => SetValue(LeftTextProperty, value);
    }

    public ICommand RightCommand
    {
        get => (ICommand)GetValue(RightCommandProperty);
        set => SetValue(RightCommandProperty, value);
    }

    public string RightText
    {
        get => (string)GetValue(RightTextProperty);
        set => SetValue(RightTextProperty, value);
    }

    public ButtonStateEnum LeftButtonState
    {
        get => (ButtonStateEnum)GetValue(LeftButtonStateProperty);
        set => SetValue(LeftButtonStateProperty, value);
    }

    public ButtonStateEnum RightButtonState
    {
        get => (ButtonStateEnum)GetValue(RightButtonStateProperty);
        set => SetValue(RightButtonStateProperty, value);
    }

    public PageBottomBarTwoButtonsView()
    {
        InitializeComponent();
        Loaded += PageBottomBarTwoButtonsView_Loaded;
    }

    private void PageBottomBarTwoButtonsView_Loaded(object? sender, EventArgs e)
    {
        _leftButton = leftButton;
        _rightButton = rightButton;

        // Apply current values
        if (_leftButton != null)
        {
            _leftButton.Command = LeftCommand;
            _leftButton.Text = LeftText;

            if (LeftButtonState != ButtonStateEnum.None)
            {
                _leftButton.ButtonState = LeftButtonState;
            }
        }
        if (_rightButton != null)
        {
            _rightButton.Command = RightCommand;
            _rightButton.Text = RightText;

            if (RightButtonState != ButtonStateEnum.None)
            {
                _rightButton.ButtonState = RightButtonState;
            }
        }
    }
}