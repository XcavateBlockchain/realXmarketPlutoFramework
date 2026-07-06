using PlutoFramework.Model.Sumsub;
using PlutoFramework.Templates.PageTemplate;
using System.Text.Json;

namespace PlutoFramework.Components.Sumsub
{
    public partial class SumsubWebSDKPage : PageTemplate
    {
      private readonly Func<Task> navigation;
      private bool navigated = false;

        public SumsubWebSDKPage(string accessToken, Applicant applicant, Func<Task> navigation)
        {
            NavigationPage.SetHasNavigationBar(this, false);
            Shell.SetNavBarIsVisible(this, false);

            InitializeComponent();

            var topNavigationBarHeight = (double)Application.Current.Resources["TopNavigationBarHeight"];

            webView.Margin = new Thickness(0, topNavigationBarHeight, 0, 0);

            var accessTokenJson = JsonSerializer.Serialize(accessToken);
            var emailJson = JsonSerializer.Serialize(applicant.ApplicantIdentifiers.Email);
            var phoneJson = JsonSerializer.Serialize(applicant.ApplicantIdentifiers.Phone);

            webView.Source = new HtmlWebViewSource
            {
                Html = @"
<html>

<head>
  <meta name='viewport' content='width=device-width, initial-scale=1.0, maximum-scale=1.0, minimum-scale=1.0, user-scalable=no'>
  <script src=""https://static.sumsub.com/idensic/static/sns-websdk-builder.js""></script>
</head>

<body>
  <div id=""sumsub-websdk-container""></div>

  <script>
    /**
     * @param accessToken - access token that you generated on the backend
     * @param applicantEmail - applicant email (not required)
     * @param applicantPhone - applicant phone (not required)
     * @param customI18nMessages - customized locale messages for current session (not required)
     */
    function navigateToNextPage() {
      if (window.sumsubNavigation && window.sumsubNavigation.navigateToNextPage) {
        window.sumsubNavigation.navigateToNextPage();
        return;
      }

      if (window.webkit && window.webkit.messageHandlers && window.webkit.messageHandlers.sumsubNavigation) {
        window.webkit.messageHandlers.sumsubNavigation.postMessage('navigateToNextPage');
        return;
      }

      console.warn('Sumsub navigation bridge is unavailable.');
    }

    function launchWebSdk(accessToken, applicantEmail, applicantPhone, customI18nMessages) {
      let snsWebSdkInstance = snsWebSdk
        .init(
          accessToken,
          // token update callback, must return Promise
          // Access token expired
          // get a new one and pass it to the callback to re-initiate the WebSDK
          () => this.getNewAccessToken()
        )
        .withConf({
          lang: ""en"", //language of WebSDK texts and comments (ISO 639-1 format)
          email: applicantEmail,
          phone: applicantPhone,
          theme: ""dark"" | ""light"",
        })
        .withOptions({ addViewportTag: false, adaptIframeHeight: true })
        // see below what kind of messages WebSDK generates
        .on(""idCheck.onStepCompleted"", (payload) => {
          console.log(""onStepCompleted"", payload);
        })
        .on(""idCheck.onApplicantSubmitted"", (payload) => {
            console.log(""onApplicantSubmitted"", payload);
          navigateToNextPage();
        })
        .on(""idCheck.onError"", (error) => {
          console.log(""onError"", error);
        })
        .build();

      // you are ready to go:
      // just launch the WebSDK by providing the container element for it
      snsWebSdkInstance.launch(""#sumsub-websdk-container"");
    }

    function getNewAccessToken() {
      return Promise.resolve(""ahojky""); // get a new token from your backend
    }

    launchWebSdk(
        " + accessTokenJson + @",
        " + emailJson + @",
        " + phoneJson + @"
    )
  </script>
</body>

</html>
                "
            };

            this.navigation = navigation;
        }

        private async void OnNextPageRequested(object sender, EventArgs e)
        {
          if (navigated)
            {
            return;
            }

          navigated = true;
        }
          await navigation.Invoke();
    }
}