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
        // CanGoBackInPage rather than the WebView's own CanGoBack: that one is MAUI's
        // cached copy and does not keep up with the dashboard's in-page routing on iOS,
        // which sent every tap on the back button straight to PopAsync.
        if (!webView.CanGoBackInPage)
        {
            return false;
        }

        webView.GoBack();

        return true;
    }
}
