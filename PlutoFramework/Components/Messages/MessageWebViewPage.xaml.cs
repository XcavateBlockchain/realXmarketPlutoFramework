using PlutoFramework.Templates.PageTemplate;
using PlutoFramework.Model;

namespace PlutoFramework.Components.Messages;

public partial class MessageWebViewPage : PageTemplate
{
    public MessageWebViewPage()
    {
        InitializeComponent();
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (TopNavigationBar is not null)
        {
            TopNavigationBar.BackFunc = NavigateBackAsync;
        }
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
        if (!webView.CanGoBack)
        {
            return false;
        }

        webView.GoBack();

        return true;
    }
}