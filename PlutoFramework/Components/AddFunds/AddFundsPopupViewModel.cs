using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Components.Account;
using PlutoFramework.Components.Loading;
using PlutoFramework.Model;
using PlutoFramework.Model.Sumsub;
using PlutoFrameworkCore.Solana;

namespace PlutoFramework.Components.AddFunds;

/// <summary>
/// The choice shown by the Solana "Add funds" button: deposit to the wallet address
/// (the QR code popup) or buy through the Xcavate on-ramp.
/// </summary>
public partial class AddFundsPopupViewModel : ObservableObject, IPopup
{
    public const string StagingOnrampBaseUrl = "https://onramp.xcavate.io/staging/";
    public const string ProductionOnrampBaseUrl = "https://onramp.xcavate.io/production/";

    [ObservableProperty]
    private bool isVisible = false;

    [RelayCommand]
    public void Deposit()
    {
        IsVisible = false;

        ReceiveAndTransferModel.ReceiveSolana();
    }

    [RelayCommand]
    public async Task BuyAsync()
    {
        IsVisible = false;

        var address = KeysModel.GetSolanaAddress();

        if (string.IsNullOrEmpty(address))
        {
            var noAccountPopupViewModel = DependencyService.Get<NoAccountPopupViewModel>();

            noAccountPopupViewModel.IsVisible = true;

            return;
        }

        var loadingViewModel = DependencyService.Get<FullPageLoadingViewModel>();

        string? sumsubId = null;

        loadingViewModel.IsVisible = true;

        try
        {
            var status = await SumsubUserModel.GetCurrentStatusAsync(CancellationToken.None);

            sumsubId = status?.ApplicantId;
        }
        catch
        {
            // The on-ramp starts KYC on its own when the id is missing, so a failed
            // lookup must not keep the user from buying.
        }
        finally
        {
            loadingViewModel.IsVisible = false;
        }

        await NavigationModel.PushAsync(new OnrampPage(BuildOnrampUrl(address, sumsubId)));
    }

    public static string BuildOnrampUrl(string address, string? sumsubId)
    {
        var baseUrl = SolanaNetworkModel.SelectedCluster == SolanaCluster.Mainnet
            ? ProductionOnrampBaseUrl
            : StagingOnrampBaseUrl;

        return string.IsNullOrEmpty(sumsubId)
            ? $"{baseUrl}?wallet={address}"
            : $"{baseUrl}?sumsubId={sumsubId}&wallet={address}";
    }
}
