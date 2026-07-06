namespace PlutoFramework.Components.Xcavate;

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

    private Button? _leftButton;
    private Button? _rightButton;

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
        }
        if (_rightButton != null)
        {
            _rightButton.Command = RightCommand;
            _rightButton.Text = RightText;
        }
    }
}