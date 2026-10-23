using Android.Content;
using Android.Webkit;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;

namespace PlutoFramework.Platforms.Android
{
    internal class WebChromeClientWithPermissions : MauiWebChromeClient
    {
        public WebChromeClientWithPermissions(IWebViewHandler handler) : base(handler)
        {

        }

        /// <summary>
        /// MAUI's <see cref="MauiWebChromeClient.OnShowFileChooser"/> launches the
        /// picker intent as-is and never sets <see cref="Intent.ExtraAllowMultiple"/>,
        /// so pages with <c>&lt;input type="file" multiple&gt;</c> (e.g. the property
        /// image upload) silently degrade to single-select or lose the selection
        /// entirely. Opt into multi-select when the page requests it; result parsing
        /// still goes through MAUI's ChooseFile/ParseResult, which handles ClipData.
        /// </summary>
        public override bool OnShowFileChooser(global::Android.Webkit.WebView? webView, IValueCallback? filePathCallback, FileChooserParams? fileChooserParams)
        {
            if (filePathCallback is null || fileChooserParams is null)
            {
                return base.OnShowFileChooser(webView, filePathCallback, fileChooserParams);
            }

            Intent? intent = fileChooserParams.CreateIntent();

            if (intent is not null && fileChooserParams.Mode == ChromeFileChooserMode.OpenMultiple)
            {
                intent.PutExtra(Intent.ExtraAllowMultiple, true);
            }

            return ChooseFile(filePathCallback, intent, fileChooserParams.Title);
        }

        public override void OnPermissionRequest(PermissionRequest? request)
        {
            if (request == null)
            {
                return;
            }

            // Process each request
            foreach (var resource in request.GetResources() ?? [])
            {
                // Check if the web page is requesting permission to the camera
                if (resource.Equals(PermissionRequest.ResourceVideoCapture, StringComparison.OrdinalIgnoreCase))
                {
                    // Get the status of the .NET MAUI app's access to the camera
                    PermissionStatus status = Permissions.CheckStatusAsync<Permissions.Camera>().Result;

                    // Deny the web page's request if the app's access to the camera is not "Granted"
                    if (status != PermissionStatus.Granted)
                        request?.Deny();
                    else
                        request.Grant(request.GetResources());

                    return;
                }
            }

            base.OnPermissionRequest(request);
        }
    }
}