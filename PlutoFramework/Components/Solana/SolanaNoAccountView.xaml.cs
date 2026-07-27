using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Model;
using System.Windows.Input;

namespace PlutoFramework.Components.Solana;

/// <summary>
/// Shown where a Solana account is required but none exists. Routes into the same create and
/// import flows onboarding uses, so an existing Substrate-only user reaches a Solana account
/// without reinstalling.
/// </summary>
public partial class SolanaNoAccountView : ContentView
{
    /// <summary>
    /// Runs once an account exists. The create and import flows are popups rather than pages,
    /// so the host page never disappears and reappears - without this it would keep showing
    /// the empty state until something else made it look again.
    /// </summary>
    public static readonly BindableProperty AccountAddedCommandProperty = BindableProperty.Create(
        nameof(AccountAddedCommand), typeof(ICommand), typeof(SolanaNoAccountView));

    public SolanaNoAccountView()
    {
        InitializeComponent();

        CreateCommand = new AsyncRelayCommand(CreateAsync);
        ImportCommand = new AsyncRelayCommand(ImportAsync);
    }

    public ICommand? AccountAddedCommand
    {
        get => (ICommand?)GetValue(AccountAddedCommandProperty);
        set => SetValue(AccountAddedCommandProperty, value);
    }

    public IAsyncRelayCommand CreateCommand { get; }

    public IAsyncRelayCommand ImportCommand { get; }

    private Task CreateAsync()
    {
        var popup = DependencyService.Get<CreateSolanaMnemonicsPopupViewModel>();

        popup.Completed = NotifyAccountAddedAsync;

        popup.IsVisible = true;

        return Task.CompletedTask;
    }

    private Task ImportAsync()
    {
        var popup = DependencyService.Get<ImportMethodPopupViewModel>();

        // The seed-phrase popup saves the key itself through
        // KeysModel.SaveSolanaMnemonicKeyAsync before reporting back, so its callback only
        // refreshes. The MWA popup does not save - it reports the authorization and leaves
        // persisting it to whoever asked, so this saves before refreshing.
        popup.SeedPhraseChosen = () =>
        {
            var seedPhrasePopup = DependencyService.Get<EnterSolanaMnemonicsPopupViewModel>();

            seedPhrasePopup.Completed = (mnemonics) => NotifyAccountAddedAsync();

            seedPhrasePopup.IsVisible = true;

            return Task.CompletedTask;
        };

        popup.MwaChosen = () =>
        {
            var mwaPopup = DependencyService.Get<ConnectMwaPopupViewModel>();

            mwaPopup.Completed = async (key) =>
            {
                await KeysModel.SaveSolanaMwaKeyAsync(key);

                await NotifyAccountAddedAsync();
            };

            mwaPopup.IsVisible = true;

            return Task.CompletedTask;
        };

        popup.IsVisible = true;

        return Task.CompletedTask;
    }

    private Task NotifyAccountAddedAsync()
    {
        AccountAddedCommand?.Execute(null);

        return Task.CompletedTask;
    }
}
