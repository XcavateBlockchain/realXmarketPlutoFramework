using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Model;

namespace PlutoFramework.Components.Solana;

/// <summary>
/// Shown where a Solana account is required but none exists. Routes into the same create and
/// import flows onboarding uses, so an existing Substrate-only user reaches a Solana account
/// without reinstalling.
/// </summary>
public partial class SolanaNoAccountView : ContentView
{
    public SolanaNoAccountView()
    {
        InitializeComponent();

        CreateCommand = new AsyncRelayCommand(CreateAsync);
        ImportCommand = new AsyncRelayCommand(ImportAsync);
    }

    public IAsyncRelayCommand CreateCommand { get; }

    public IAsyncRelayCommand ImportCommand { get; }

    private static Task CreateAsync() =>
        NavigationModel.PushAsync(new CreateSolanaMnemonicsPage(new CreateSolanaMnemonicsViewModel
        {
            Navigation = () => NavigationModel.PopAsync(),
        }));

    private static Task ImportAsync()
    {
        var popup = DependencyService.Get<ImportMethodPopupViewModel>();

        // EnterSolanaMnemonicsViewModel.ContinueWithMnemonicsAsync already saves the key
        // via KeysModel.SaveSolanaMnemonicKeyAsync before invoking Navigation, so this
        // callback only needs to pop back - saving again here would delete and re-save
        // the key it just created.
        popup.SeedPhraseChosen = () => NavigationModel.PushAsync(new EnterSolanaMnemonicsPage(
            new EnterSolanaMnemonicsViewModel
            {
                Navigation = (mnemonics) => NavigationModel.PopAsync(),
            }));

        popup.MwaChosen = () => NavigationModel.PushAsync(new ConnectMwaPage(new ConnectMwaPageViewModel
        {
            Navigation = () => NavigationModel.PopAsync(),
        }));

        popup.IsVisible = true;

        return Task.CompletedTask;
    }
}
