using Android.App;
using Android.Content;
using Android.Webkit;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using Object = Java.Lang.Object;

namespace PlutoFramework.Platforms.Android
{
    internal class WebChromeClientWithPermissions : MauiWebChromeClient
    {
        public WebChromeClientWithPermissions(IWebViewHandler handler) : base(handler)
        {

        }

        /// <summary>
        /// Ensures the picker intent opts into multi-select when the page requests it
        /// (<c>&lt;input type="file" multiple&gt;</c>, e.g. the property image upload).
        /// Chromium's FileChooserParams.CreateIntent normally sets
        /// <see cref="Intent.ExtraAllowMultiple"/> itself for OpenMultiple mode, but we set it
        /// explicitly in case a WebView provider omits it. The actual multi-select fix lives in
        /// <see cref="ParseResult"/>, which MAUI's ChooseFile calls to deliver the result.
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

        /// <summary>
        /// MAUI's ChooseFile delivers picker results through
        /// <see cref="WebChromeClient.FileChooserParams.ParseResult(int, Intent)"/>, which on
        /// Chromium-based WebViews only reads <see cref="Intent.Data"/> and silently ignores
        /// <see cref="Intent.ClipData"/>. Multi-select results arrive exclusively as ClipData,
        /// so every multi-file selection was reported back to the page as a cancellation
        /// (e.g. the property image upload appeared to lose all selected images).
        /// Parse ClipData here before falling back to the single-Uri path.
        /// </summary>
        protected override Object? ParseResult(Result resultCode, Intent? data)
        {
            if (resultCode != Result.Ok || data is null)
            {
                return null;
            }

            if (data.ClipData is not null)
            {
                ClipData clipData = data.ClipData;
                var uris = new global::Android.Net.Uri[clipData.ItemCount];

                for (int i = 0; i < clipData.ItemCount; i++)
                {
                    uris[i] = clipData.GetItemAt(i)?.Uri!;
                }

                return uris;
            }

            if (data.Data is not null)
            {
                return new global::Android.Net.Uri[] { data.Data };
            }

            return null;
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