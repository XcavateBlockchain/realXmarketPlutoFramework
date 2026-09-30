using PlutoFramework.Components.Account;
using PlutoFramework.Components.AddFunds;
using PlutoFramework.Components.Loading;
using PlutoFramework.Model;
using PlutoFramework.Model.Sumsub;
using PlutoFrameworkCore.Solana;

namespace PlutoFramework.Components.Redemption;

/// <summary>
/// The sell counterpart of the on-ramp buy flow (<see cref="AddFundsPopupViewModel.BuyAsync"/>):
/// opens the Xcavate redemption page with the user's Sumsub applicant id and Solana wallet
/// address, so the off-ramp can pay out the tGBP the wallet sells.
/// </summary>
public static class RedemptionModel
{
    /// <summary>The off-ramp lives under the same base URL as the on-ramp, one path down.</summary>
    private const string RedemptionPath = "redemption/";

    public static async Task RedeemAsync()
    {
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
            // The off-ramp starts KYC on its own when the id is missing, so a failed
            // lookup must not keep the user from selling.
        }
        finally
        {
            loadingViewModel.IsVisible = false;
        }

        await NavigationModel.PushAsync(new RedemptionPage(BuildRedemptionUrl(address, sumsubId)));
    }

    public static string BuildRedemptionUrl(string address, string? sumsubId)
    {
        var baseUrl = SolanaNetworkModel.SelectedCluster == SolanaCluster.Mainnet
            ? AddFundsPopupViewModel.ProductionOnrampBaseUrl
            : AddFundsPopupViewModel.StagingOnrampBaseUrl;

        return string.IsNullOrEmpty(sumsubId)
            ? $"{baseUrl}{RedemptionPath}?wallet={address}"
            : $"{baseUrl}{RedemptionPath}?sumsubId={sumsubId}&wallet={address}";
    }
}
