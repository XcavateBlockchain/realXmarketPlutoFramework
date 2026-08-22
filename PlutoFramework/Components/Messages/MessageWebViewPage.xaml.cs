using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Templates.PageTemplate;
using PlutoFramework.Model;
using System.Text;

namespace PlutoFramework.Components.Messages;

public partial class MessageWebViewPage : PageTemplate
{
    private const string DefaultTitle = "Messages";

    public MessageWebViewPage() : this(null)
    {
    }

    public MessageWebViewPage(string? url)
    {
        InitializeComponent();

        webView.HeaderChanged += OnWebHeaderChanged;

        if (url is not null)
        {
            webView.Url = url;
        }
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        WireNavigationBarBack();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        WireNavigationBarBack();
    }

    /// <summary>
    /// Points the navigation bar's back button at this page rather than at the template's
    /// default, which pops the page.
    /// </summary>
    /// <remarks>
    /// Re-applied on every appearance rather than only when the template is applied. The
    /// bar is reached through the control template, whose one-shot hook runs from the
    /// PageTemplate constructor - before this page's own constructor body - and a lookup
    /// that comes back empty there would leave the button silently wired to PopAsync for
    /// the life of the page, which is exactly the bug this page is meant not to have.
    /// </remarks>
    private void WireNavigationBarBack()
    {
        if (TopNavigationBar is not null)
        {
            TopNavigationBar.BackFunc = NavigateBackAsync;
        }
    }

    /// <summary>
    /// Mirrors the hosted page's header into the native TopNavigationBar: the title
    /// replaces the page title and each web action button becomes an icon slot.
    /// Already dispatched on the main thread by <see cref="X25519WebView"/>.
    /// </summary>
    private void OnWebHeaderChanged(object? sender, WebPageHeader header)
    {
        if (!header.Present)
        {
            Title = DefaultTitle;
            NavigationBarExtra1Command = null;
            NavigationBarExtra2Command = null;
            return;
        }

        Title = string.IsNullOrWhiteSpace(header.Title) ? DefaultTitle : header.Title;

        // The TopNavigationBar exposes two icon slots: Extra1 (right-most / primary)
        // and Extra2. Map the header's action buttons onto them in order, so the
        // first web button lands in the primary slot.
        ApplyHeaderAction(header.Buttons, index: 0, isPrimary: true);
        ApplyHeaderAction(header.Buttons, index: 1, isPrimary: false);
    }

    private void ApplyHeaderAction(IReadOnlyList<string> buttons, int index, bool isPrimary)
    {
        var hasButton = index < buttons.Count && !string.IsNullOrWhiteSpace(buttons[index]);

        if (!hasButton)
        {
            // Clearing the command hides the slot (its IsVisible tracks the command).
            if (isPrimary)
            {
                NavigationBarExtra1Command = null;
            }
            else
            {
                NavigationBarExtra2Command = null;
            }

            return;
        }

        var image = ImageSource.FromFile(ButtonTextToIconFile(buttons[index]));
        var command = new AsyncRelayCommand(() => webView.InvokeHeaderActionAsync(index));

        if (isPrimary)
        {
            NavigationBarExtra1Image = image;
            NavigationBarExtra1Command = command;
        }
        else
        {
            NavigationBarExtra2Image = image;
            NavigationBarExtra2Command = command;
        }
    }

    /// <summary>
    /// Maps a header button's visible text to its icon file name using the agreed
    /// scheme: lower-cased with all whitespace removed, suffixed with ".png".
    /// e.g. "New Chat" -> "newchat.png".
    /// </summary>
    private static string ButtonTextToIconFile(string text)
    {
        var builder = new StringBuilder(text.Length);

        foreach (var c in text)
        {
            if (!char.IsWhiteSpace(c))
            {
                builder.Append(char.ToLowerInvariant(c));
            }
        }

        return builder.Length == 0 ? string.Empty : $"{builder}.png";
    }

    protected override bool OnBackButtonPressed()
    {
        if (NavigateBackInWebView())
        {
            return true;
        }

        return base.OnBackButtonPressed();
    }

    private Task NavigateBackAsync()
    {
        if (NavigateBackInWebView())
        {
            return Task.CompletedTask;
        }

        return NavigationModel.PopAsync();
    }

    private bool NavigateBackInWebView()
    {
        // CanGoBackInPage / GoBackInPage rather than the WebView's own CanGoBack and
        // GoBack: those go through MAUI's cached copy of the back-forward list, which does
        // not keep up with the dashboard's client-side routing and sent every tap on the
        // back button straight to PopAsync.
        if (!webView.CanGoBackInPage)
        {
            return false;
        }

        webView.GoBackInPage();

        return true;
    }
}
