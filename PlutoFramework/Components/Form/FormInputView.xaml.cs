using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Model;

namespace PlutoFramework.Components.Form;

public partial class FormInputView : ContentView, IFormFocusable
{
    public static readonly BindableProperty CardWidthProperty = BindableProperty.Create(
       nameof(CardWidth), typeof(int), typeof(FormInputView),
       defaultBindingMode: BindingMode.TwoWay,
       propertyChanging: (bindable, oldValue, newValue) =>
       {
           var control = (FormInputView)bindable;

           var width = (int)newValue - 20;
           control.entry.WidthRequest = width;
       });

    public static readonly BindableProperty PlaceholderProperty = BindableProperty.Create(
        nameof(Placeholder), typeof(string), typeof(FormInputView),
        defaultBindingMode: BindingMode.OneWay,
        propertyChanged: (bindable, oldValue, newValue) =>
        {
            var control = (FormInputView)bindable;
            control.entry.Placeholder = (string)newValue;
        });

    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text), typeof(string), typeof(FormInputView),
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanged: (bindable, oldValue, newValue) =>
        {
            var control = (FormInputView)bindable;

            if (control.entry.Text == (string)newValue)
            {
                return;
            }

            try
            {
                control.entry.Text = (string)newValue;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        });


    public static readonly BindableProperty UpdateCommandProperty = BindableProperty.Create(
        nameof(UpdateCommand), typeof(IRelayCommand), typeof(FormInputView),
        defaultBindingMode: BindingMode.TwoWay);

    public static readonly BindableProperty KeyboardTypeProperty = BindableProperty.Create(
        nameof(KeyboardType), typeof(Keyboard), typeof(FormInputView),
        defaultValue: Keyboard.Default,
        defaultBindingMode: BindingMode.OneWay,
        propertyChanged: (bindable, oldValue, newValue) =>
        {
            var control = (FormInputView)bindable;
            control.entry.Keyboard = (Keyboard)newValue;
        });

    public static readonly BindableProperty ReturnTypeProperty = BindableProperty.Create(
        nameof(ReturnType), typeof(ReturnType), typeof(FormInputView),
        defaultValue: ReturnType.Default,
        defaultBindingMode: BindingMode.OneWay,
        propertyChanged: (bindable, oldValue, newValue) =>
        {
            var control = (FormInputView)bindable;
            control.returnTypeWasSetExplicitly = true;
            control.entry.ReturnType = (ReturnType)newValue;
        });

    public static readonly BindableProperty MaxValueProperty = BindableProperty.Create(
        nameof(MaxValue), typeof(string), typeof(FormInputView),
        propertyChanged: (bindable, oldValue, newValue) =>
        {
            var maxValue = (string?)newValue;
            var control = (FormInputView)bindable;
            control.maxButton.IsVisible = maxValue != null;
            control.card.CardPadding = new Thickness(10, 0, 0, 0);
            Grid.SetColumnSpan(control.entry, 1);
        }
    );
    public FormInputView()
    {
        InitializeComponent();
    }

    private bool returnTypeWasSetExplicitly = false;

    private VisualElement? nextView = null;

    /// <summary>
    /// Where the keyboard's Next key sends focus. Null - the default - turns that key into a
    /// Done that only closes the keyboard, which is what the last field of a form wants: the
    /// user still has to reach for the button to submit.
    /// </summary>
    public VisualElement? NextView
    {
        get => nextView;
        set
        {
            nextView = value;

            // The key should promise what pressing it actually does, unless the page has
            // already said what it wants the key to be.
            if (!returnTypeWasSetExplicitly)
            {
                entry.ReturnType = value is null ? ReturnType.Done : ReturnType.Next;
            }
        }
    }

    public void FocusEntry() => entry.Focus();

    public Keyboard KeyboardType
    {
        get => (Keyboard)GetValue(KeyboardTypeProperty);
        set => SetValue(KeyboardTypeProperty, value);
    }

    public ReturnType ReturnType
    {
        get => (ReturnType)GetValue(ReturnTypeProperty);
        set => SetValue(ReturnTypeProperty, value);
    }

    public string? MaxValue
    {
        get => (string?)GetValue(MaxValueProperty);
        set => SetValue(MaxValueProperty, value);
    }

    public int CardWidth
    {
        get => (int)GetValue(CardWidthProperty);
        set => SetValue(CardWidthProperty, value);
    }
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }
    public IRelayCommand UpdateCommand
    {
        get => (IRelayCommand)GetValue(UpdateCommandProperty);
        set => SetValue(UpdateCommandProperty, value);
    }

    public bool ValidateEmail { get; set; } = false;

    /// <summary>
    /// Shows why the current text was rejected, or clears the warning when it was not.
    /// </summary>
    private void ShowValidationProblem()
    {
        var text = entry.Text ?? "";

        // Silent until there is something to disagree with, so a field the user has not filled
        // in yet does not start out accusing them.
        if (!ValidateEmail || text == "")
        {
            ClearValidationProblem();

            return;
        }

        var problem = FormModel.DescribeEmailProblem(text);

        errorLabel.Text = problem ?? "";
        errorLabel.IsVisible = problem is not null;

        if (problem is null)
        {
            card.SetDefaultColor();
        }
        else
        {
            card.SetRedColor();
        }
    }

    private void ClearValidationProblem()
    {
        errorLabel.Text = "";
        errorLabel.IsVisible = false;

        card.SetDefaultColor();
    }

    private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        try
        {
            if (e.PropertyName != "Text")
            {
                return;
            }

            SetValue(TextProperty, ((Entry)sender).Text);

            if (UpdateCommand != null)
            {
                UpdateCommand.Execute(null);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }

    private void OnUnfocused(object sender, FocusEventArgs e)
    {
        ShowValidationProblem();
    }

    private void OnFocused(object sender, FocusEventArgs e)
    {
        // Nothing to complain about while the user is still typing the answer.
        ClearValidationProblem();
    }

    /// <summary>
    /// The Enter key: on to the next field, or just away with the keyboard on the last one.
    /// Submitting is left to the button, so Enter can never commit a half filled form.
    /// </summary>
    private async void OnCompleted(object? sender, EventArgs e)
    {
        ShowValidationProblem();

        if (NextView is IFormFocusable focusable)
        {
            focusable.FocusEntry();

            return;
        }

        if (NextView is not null)
        {
            NextView.Focus();

            return;
        }

        if (entry.IsSoftInputShowing())
        {
            await entry.HideSoftInputAsync(CancellationToken.None);
        }
    }

    private void OnMaxClicked(object sender, TappedEventArgs e)
    {
        SetValue(TextProperty, MaxValue);
    }
}