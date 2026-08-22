using PlutoFramework.Model;

namespace PlutoFramework.Components.Form;

/// <summary>
/// A phone number field: a country picked from a list on the left, the national number typed on
/// the right, and the reason underneath when the two do not make a usable number.
/// </summary>
/// <remarks>
/// <see cref="Text"/> is the whole number in E.164 form - the only shape Sumsub and the profile
/// API accept - and is empty whenever the field does not currently hold a valid number. That
/// keeps the "is this form ready" question a plain emptiness check in the view model, and stops
/// a half typed number from ever reaching the backend.
/// </remarks>
public partial class FormPhoneInputView : ContentView, IFormFocusable
{
    public static readonly BindableProperty CardWidthProperty = BindableProperty.Create(
        nameof(CardWidth), typeof(int), typeof(FormPhoneInputView),
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanging: (bindable, oldValue, newValue) =>
        {
            var control = (FormPhoneInputView)bindable;

            // Leaves room for the card padding and the country button beside the entry.
            control.entry.WidthRequest = (int)newValue - 20 - COUNTRY_BUTTON_WIDTH;
        });

    public static readonly BindableProperty PlaceholderProperty = BindableProperty.Create(
        nameof(Placeholder), typeof(string), typeof(FormPhoneInputView),
        defaultBindingMode: BindingMode.OneWay,
        propertyChanged: (bindable, oldValue, newValue) =>
        {
            var control = (FormPhoneInputView)bindable;
            control.entry.Placeholder = (string)newValue;
        });

    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text), typeof(string), typeof(FormPhoneInputView),
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanged: (bindable, oldValue, newValue) =>
        {
            var control = (FormPhoneInputView)bindable;

            // Everything this control writes out it already knows about; only a value pushed in
            // from the view model should move the country or the entry.
            if (control.publishing)
            {
                return;
            }

            control.Adopt((string?)newValue);
        });

    public static readonly BindableProperty ReturnTypeProperty = BindableProperty.Create(
        nameof(ReturnType), typeof(ReturnType), typeof(FormPhoneInputView),
        defaultValue: ReturnType.Done,
        defaultBindingMode: BindingMode.OneWay,
        propertyChanged: (bindable, oldValue, newValue) =>
        {
            var control = (FormPhoneInputView)bindable;
            control.returnTypeWasSetExplicitly = true;
            control.entry.ReturnType = (ReturnType)newValue;
        });

    /// <summary>
    /// Roughly what the flag, the longest calling code and the arrow take up.
    /// </summary>
    private const int COUNTRY_BUTTON_WIDTH = 80;

    private PhoneCountry country = PhoneCountries.Default;

    private bool publishing = false;

    private bool returnTypeWasSetExplicitly = false;

    private VisualElement? nextView = null;

    public FormPhoneInputView()
    {
        InitializeComponent();

        // A phone number is the last field of every form that has one, so Done is the honest
        // default. NextView changes it when a page puts something after this field.
        entry.ReturnType = ReturnType.Done;

        ShowCountry();
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

    public ReturnType ReturnType
    {
        get => (ReturnType)GetValue(ReturnTypeProperty);
        set => SetValue(ReturnTypeProperty, value);
    }

    public int CardWidth
    {
        get => (int)GetValue(CardWidthProperty);
        set => SetValue(CardWidthProperty, value);
    }

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

            if (!returnTypeWasSetExplicitly)
            {
                entry.ReturnType = value is null ? ReturnType.Done : ReturnType.Next;
            }
        }
    }

    /// <summary>
    /// The country currently shown on the button. Exposed so a page can preselect one without
    /// having a number to parse it out of.
    /// </summary>
    public PhoneCountry Country
    {
        get => country;
        set
        {
            country = value;

            ShowCountry();
            Publish();
        }
    }

    public void FocusEntry() => entry.Focus();

    /// <summary>
    /// Splits a value handed in by the view model into the country to show and the national
    /// digits to type over.
    /// </summary>
    private void Adopt(string? value)
    {
        var (parsedCountry, nationalNumber) = PhoneNumberModel.Parse(value, country);

        country = parsedCountry;

        ShowCountry();

        if ((entry.Text ?? "") != nationalNumber)
        {
            entry.Text = nationalNumber;
        }
    }

    private void ShowCountry()
    {
        flagLabel.Text = country.Flag;
        dialCodeLabel.Text = $"+{country.DialCode}";

        SemanticProperties.SetDescription(countryButton, $"Country code: {country.Name}, plus {country.DialCode}");
    }

    /// <summary>
    /// Writes the number out, or an empty string while it is not one. Never writes a number the
    /// backend would reject.
    /// </summary>
    private void Publish()
    {
        var nationalNumber = entry.Text ?? "";

        var value = PhoneNumberModel.IsValid(country, nationalNumber)
            ? PhoneNumberModel.ToE164(country, nationalNumber)
            : "";

        publishing = true;

        try
        {
            SetValue(TextProperty, value);
        }
        finally
        {
            publishing = false;
        }
    }

    /// <summary>
    /// Someone who pastes a whole international number gets the country moved for them rather
    /// than an error telling them the plus does not belong here.
    /// </summary>
    private void AdoptPastedCountryCode()
    {
        var text = (entry.Text ?? "").Trim();

        if (!text.StartsWith("+", StringComparison.Ordinal))
        {
            return;
        }

        var (parsedCountry, nationalNumber) = PhoneNumberModel.Parse(text, country);

        country = parsedCountry;

        ShowCountry();

        entry.Text = nationalNumber;
    }

    private void ShowValidationProblem()
    {
        var nationalNumber = entry.Text ?? "";

        // Silent until there is something to disagree with, so a field the user has not filled
        // in yet does not start out accusing them.
        if (nationalNumber == "")
        {
            ClearValidationProblem();

            return;
        }

        var problem = PhoneNumberModel.DescribeProblem(country, nationalNumber);

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

            Publish();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }

    private async void OnCountryTapped(object sender, TappedEventArgs e)
    {
        // An event handler is async void, so a failed navigation would otherwise take the
        // process down rather than just leaving the country unchanged.
        try
        {
            // The picker is a whole page, so the keyboard goes first rather than being
            // animated away underneath it.
            if (entry.IsSoftInputShowing())
            {
                await entry.HideSoftInputAsync(CancellationToken.None);
            }

            var navigation = Shell.Current?.Navigation;

            if (navigation is null)
            {
                return;
            }

            await navigation.PushAsync(new CountrySelectPage(country, selected =>
            {
                Country = selected;

                ShowValidationProblem();
            }));
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }

    private void OnFocused(object sender, FocusEventArgs e)
    {
        // Nothing to complain about while the user is still typing the answer.
        ClearValidationProblem();
    }

    private void OnUnfocused(object sender, FocusEventArgs e)
    {
        AdoptPastedCountryCode();
        ShowValidationProblem();
    }

    /// <summary>
    /// The Enter key: on to the next field, or just away with the keyboard on the last one.
    /// Submitting is left to the button, so Enter can never commit a half filled form.
    /// </summary>
    private async void OnCompleted(object? sender, EventArgs e)
    {
        AdoptPastedCountryCode();
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
}
