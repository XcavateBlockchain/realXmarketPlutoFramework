using FFImageLoading.Maui;
using Microcharts.Maui;
using PlutoFramework.Components.Account;
using PlutoFramework.Components.AddressView;
using PlutoFramework.Components.AssetSelect;
using PlutoFramework.Components.AzeroId;
using PlutoFramework.Components.CalamarView;
using PlutoFramework.Components.ChangeLayoutRequest;
using PlutoFramework.Components.CustomLayouts;
using PlutoFramework.Components.DAppConnection;
using PlutoFramework.Components.Extrinsic;
using PlutoFramework.Components.Fee;
using PlutoFramework.Components.Keys;
using PlutoFramework.Components.Kilt;
using PlutoFramework.Components.Loading;
using PlutoFramework.Components.MessagePopup;
using PlutoFramework.Components.Mnemonics;
using PlutoFramework.Components.NavigationBar;
using PlutoFramework.Components.NetworkSelect;
using PlutoFramework.Components.Nft;
using PlutoFramework.Components.Onboarding;
using PlutoFramework.Components.Password;
using PlutoFramework.Components.Settings;
using PlutoFramework.Components.Solana;
using PlutoFramework.Components.Staking;
using PlutoFramework.Components.Sumsub;
using PlutoFramework.Components.TransactionAnalyzer;
using PlutoFramework.Components.TransactionRequest;
using PlutoFramework.Components.TransferView;
using PlutoFramework.Components.Vault;
using PlutoFramework.Components.WebView;
using PlutoFramework.Components.Xcavate;
using PlutoFramework.Components.XcavateProperty;
using PlutoFramework.Model;
using PlutoFramework.Model.SQLite;
using PlutoFramework.Model.Xcavate.Profile;
using PlutoFrameworkCore;
using PlutoFrameworkCore.PushNotificationServices.Core.Utils;
using Xe.AcrylicView;
using ZXing.Net.Maui.Controls;

#if ANDROID26_0_OR_GREATER
using Microsoft.Maui.Controls.Compatibility.Platform.Android;
using PlutoFramework.Platforms.Android;
#endif

namespace PlutoFramework
{
    public static class MauiAppBuilderExtensions
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public static IServiceProvider Services { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        private static bool _isFullInitialized;

        public static MauiAppBuilder UsePlutoFrameworkMinimal(this MauiAppBuilder builder)
        {
            builder
                .UseAcrylicView()
                .UseFFImageLoading()
                .UseMicrocharts()
                .UseBarcodeReader()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("dmsans.ttf", "XcavateFont");
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("fontawesome-webfont.ttf", "FontAwesome");
                    fonts.AddFont("Exodar-Outline.ttf", "Exodar");
                    fonts.AddFont("FontOver.ttf", "FontOver");
                    fonts.AddFont("sourcecode.ttf", "SourceCode");
                    fonts.AddFont("samsungone700.ttf", "SamsungOne");
                    fonts.AddFont("unboundedbold.ttf", "UnboundedBold");
                });

            //https://stackoverflow.com/questions/76547461/how-to-remove-the-outline-of-entry-control-in-maui-ios
            Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("SetUpEntry", (handler, view) =>
            {
#if ANDROID
                handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Colors.Transparent.ToAndroid());
#elif IOS || MACCATALYST

                //remove outline
                handler.PlatformView.BorderStyle = UIKit.UITextBorderStyle.None;
#elif WINDOWS
  
#endif
            });


            Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("FixDuplicateCursor", (handler, view) =>
            {
#if ANDROID
                // Android specifically struggles with this caret issue
                handler.PlatformView.FocusChange += (sender, e) =>
                {
                    handler.PlatformView.SetCursorVisible(e.HasFocus);
                };
#endif
            });


            // Opt-in via RefreshColor="Transparent". Neither platform honours that colour on
            // its own: Android's CircleImageView renders regardless, so it is parked
            // off-screen, and on iOS the UIRefreshControl spinner is an activity indicator
            // that paints itself rather than following TintColor, so the control is faded out.
            Microsoft.Maui.Handlers.RefreshViewHandler.Mapper.AppendToMapping("HideNativeRefreshSpinner", (handler, view) =>
            {
#if ANDROID
                if (view is RefreshView refreshView && refreshView.RefreshColor?.Alpha == 0f)
                {
                    // setProgressViewOffset leaves mTotalDragDistance at its default, so the
                    // pull threshold is unchanged - only the circle's resting positions move.
                    handler.PlatformView.SetProgressViewOffset(false, -500, -400);

                    handler.PlatformView.SetColorSchemeColors(Android.Graphics.Color.Transparent.ToArgb());
                    handler.PlatformView.SetProgressBackgroundColorSchemeColor(
                        Android.Graphics.Color.Transparent.ToArgb());
                }
#elif IOS || MACCATALYST
                if (view is RefreshView refreshView && refreshView.RefreshColor?.Alpha == 0f)
                {
                    // Alpha rather than Hidden: UIScrollView keeps laying the control out and
                    // keeps driving the refresh from its content offset, so the pull gesture
                    // and the content slide-down RefreshView relies on are both untouched.
                    handler.PlatformView.RefreshControl.Alpha = 0f;
                    handler.PlatformView.RefreshControl.TintColor = UIKit.UIColor.Clear;
                    handler.PlatformView.RefreshControl.BackgroundColor = UIKit.UIColor.Clear;
                }
#endif
            });


            AssetsModel.DatabaseSaver = new BalancesDatabaseSaver();

            PushNotificationRegistrar.RegisterPushNotificationServices(builder.Services);

            PlutoConfigurationModel.SecureStorage = new PlutoSecureStorage();

            PlutoConfigurationModel.MwaIntentLauncher = new MwaIntentLauncher();

            CustomizeWebViewHandler();

            return builder;
        }

        public static void InitializePlutoFrameworkFull()
        {
            if (_isFullInitialized)
            {
                return;
            }

            _isFullInitialized = true;

            DependencyService.Register<CanNotRecoverKeyPopupViewModel>();

            DependencyService.Register<TransferViewModel>();

            DependencyService.Register<DAppConnectionRequestViewModel>();

            DependencyService.Register<MessagePopupViewModel>();

            DependencyService.Register<AddressQrCodeViewModel>();

            DependencyService.Register<DAppConnectionViewModel>();

            DependencyService.Register<StakingRegistrationRequestViewModel>();

            DependencyService.Register<MultiNetworkSelectViewModel>();

            DependencyService.Register<ChainAddressViewModel>();

            DependencyService.Register<StakingDashboardViewModel>();

            DependencyService.Register<CalamarViewModel>();

            DependencyService.Register<ExtrinsicStatusStackViewModel>();

            DependencyService.Register<Components.Solana.Status.SolanaTransactionStatusStackViewModel>();

            DependencyService.Register<Components.Solana.Transfer.SolanaTransferViewModel>();

            DependencyService.Register<Components.Solana.Transfer.SolanaTokenSelectViewModel>();

            DependencyService.Register<ExportPlutoLayoutQRViewModel>();

            DependencyService.Register<CustomItemViewModel>();

            DependencyService.Register<MessageSignRequestViewModel>();

            DependencyService.Register<AzeroPrimaryNameViewModel>();

            DependencyService.Register<AssetSelectViewModel>();

            DependencyService.Register<AssetSelectButtonViewModel>();

            DependencyService.Register<VaultSignViewModel>();

            DependencyService.Register<ChangeLayoutRequestViewModel>();

            DependencyService.Register<NetworkSelectPopupViewModel>();

            DependencyService.Register<NavigationBarViewModel>();

            DependencyService.Register<FeeAssetViewModel>();

            DependencyService.Register<AnalyzedOutcomeViewModel>();

            DependencyService.Register<TransactionAnalyzerConfirmationViewModel>();

            DependencyService.Register<AssetInputViewModel>();

            DependencyService.Register<EnterPasswordPopupViewModel>();

            DependencyService.Register<SuccessfulImportPopupViewModel>();

            DependencyService.Register<BuyPropertyTokensViewModel>();

            DependencyService.Register<NoAccountPopupViewModel>();

            DependencyService.Register<ImportWarningPopupViewModel>();

            DependencyService.Register<ImportMethodPopupViewModel>();

            DependencyService.Register<CreateSolanaMnemonicsPopupViewModel>();

            DependencyService.Register<EnterSolanaMnemonicsPopupViewModel>();

            DependencyService.Register<ConnectMwaPopupViewModel>();

            DependencyService.Register<MwaSignPopupViewModel>();

            DependencyService.Register<LogOutPopupViewModel>();

            DependencyService.Register<CancelReservationPopupViewModel>();

            DependencyService.Register<OnboardingInProgressPopupViewModel>();

            DependencyService.Register<NoDidPopupViewModel>();

            DependencyService.Register<NoKYCPopupViewModel>();

            DependencyService.Register<XcavateIndexedPropertyMarketplaceViewModel>();

            DependencyService.Register<FullPageLoadingViewModel>();

            DependencyService.Register<OwnedPropertiesListViewModel>();

            DependencyService.Register<RelistPropertyTokensViewModel>();

            DependencyService.Register<XcavateNavigationBarViewModel>();

            DependencyService.Register<XcavatePropertyNavigationBarViewModel>();

            DependencyService.Register<NotWhitelistedPopupViewModel>();

            DependencyService.Register<UserProfileNotCreatedPopupViewModel>();

            DependencyService.Register<XcavateProfileService>();

            DependencyService.Register<WebSignRawPopupViewModel>();

            DependencyService.Register<DAppWebViewConnectionRequestPopupViewModel>();

            DependencyService.Register<PropertyMarketplaceFilterPopupViewModel>();

            DependencyService.Register<PropertyMarketplaceSelectionPopupViewModel>();
        }

        /// <summary>
        /// https://learn.microsoft.com/en-us/dotnet/maui/user-interface/controls/webview?view=net-maui-9.0&pivots=devices-android#handle-permissions-on-android
        /// </summary>
        private static void CustomizeWebViewHandler()
        {
#if ANDROID26_0_OR_GREATER
            Microsoft.Maui.Handlers.WebViewHandler.Mapper.ModifyMapping(
                nameof(Android.Webkit.WebView.WebChromeClient),
                (handler, view, args) => handler.PlatformView.SetWebChromeClient(new WebChromeClientWithPermissions(handler)));
#endif
        }
    }
}
