using CommunityToolkit.Maui.Alerts;
using PlutoFramework.Components.Kilt;
using PlutoFramework.Components.Mnemonics;
using PlutoFramework.Components.Solana;
using PlutoFramework.Model;
using PlutoFramework.Model.SQLite;
using PlutoFrameworkCore.Keys;

namespace PlutoFramework.Components.Keys;

public partial class NewKeyView : ContentView
{
    public static readonly BindableProperty KeyTypeProperty = BindableProperty.Create(
        nameof(KeyType),
        typeof(KeyTypeEnum),
        typeof(NewKeyView),
        null,
        propertyChanged: (bindable, oldValue, newValue) =>
        {
            var control = (NewKeyView)bindable;

            if (newValue == null)
            {
                return;
            }

            var keyType = (KeyTypeEnum)newValue;

            control.nameLabelText.Text = keyType.GetName();

            _ = control.ChangeButtonsIfKeyExistsAsync();
        });

    public NewKeyView()
    {
        InitializeComponent();
    }

    public KeyTypeEnum KeyType
    {
        get => (KeyTypeEnum)GetValue(KeyTypeProperty);
        set => SetValue(KeyTypeProperty, value);
    }

    /// <summary>
    /// Opens the popup that creates a Solana account from a new seed phrase. The Solana flows
    /// do not create the key inline, so the buttons can only be re-evaluated once the popup
    /// reports back - not when the tap is handled.
    /// </summary>
    private void ShowCreateSolanaMnemonicsPopup()
    {
        var popup = DependencyService.Get<CreateSolanaMnemonicsPopupViewModel>();

        popup.Completed = ChangeButtonsIfKeyExistsAsync;

        popup.IsVisible = true;
    }

    private void ShowEnterSolanaMnemonicsPopup()
    {
        var popup = DependencyService.Get<EnterSolanaMnemonicsPopupViewModel>();

        popup.Completed = (mnemonics) => ChangeButtonsIfKeyExistsAsync();

        popup.IsVisible = true;
    }

    /// <summary>
    /// The popup reports the authorization without persisting it, so this saves it. There is
    /// already a password here - this screen is only reachable once the app is set up.
    /// </summary>
    private void ShowConnectMwaPopup()
    {
        var popup = DependencyService.Get<ConnectMwaPopupViewModel>();

        popup.Completed = async (key) =>
        {
            await KeysModel.SaveSolanaMwaKeyAsync(key);

            await ChangeButtonsIfKeyExistsAsync();
        };

        popup.IsVisible = true;
    }

    private async Task ChangeButtonsIfKeyExistsAsync()
    {
        if (!await CheckKeyExistsAsync(disableToast: true))
        {
            return;
        }

        import.Opacity = 0.3;
        plus.Opacity = 0.3;
    }

    private async Task<bool> CheckKeyExistsAsync(bool disableToast = false)
    {
        var allSavedKeys = await KeysDatabase.GetAllKeysAsync();

        var keyIsPolkadotType = KeyType.IsPolkadotAccountType();

        // The two Solana variants share one account slot, exactly as the two Polkadot
        // ones do, so either occupying it blocks both.
        var keyIsSolanaType = KeyType.IsSolanaAccountType();

        if (allSavedKeys.Where(key => key.Type == KeyType ||
            (keyIsPolkadotType && key.Type.IsPolkadotAccountType()) ||
            (keyIsSolanaType && key.Type.IsSolanaAccountType())).Any())
        {
            if (!disableToast)
            {
                var toast = Toast.Make($"{KeyType.GetName()} already exists.");
                await toast.Show();
            }

            return true;
        }

        return false;
    }
    private async void OnAddClicked(object sender, TappedEventArgs e)
    {
        if (await CheckKeyExistsAsync())
        {
            return;
        }

        switch (KeyType)
        {
            case KeyTypeEnum.Sr25519:
            case KeyTypeEnum.PolkadotJson:
                await KeysModel.GenerateNewAccountAsync();

                var sr25519toast = Toast.Make($"{KeyType.GetName()} created successfully.");
                await sr25519toast.Show();

                break;
            case KeyTypeEnum.Did:
                await KeysModel.GenerateNewDidAsync();

                var didToast = Toast.Make($"{KeyType.GetName()} created successfully.");
                await didToast.Show();

                break;
            case KeyTypeEnum.EncryptionX25519:
                await KeysModel.GenerateNewEncryptionX25519KeyAsync();

                var encryptionX25519Toast = Toast.Make($"{KeyType.GetName()} created successfully.");
                await encryptionX25519Toast.Show();

                break;
            case KeyTypeEnum.SolanaMnemonic:
                // Asks rather than generating inline, so the seed phrase is shown for
                // backup before it becomes the only copy of the key.
                ShowCreateSolanaMnemonicsPopup();

                return;
            case KeyTypeEnum.SolanaMwa:
                ShowConnectMwaPopup();

                return;
            default:
                var toast = Toast.Make($"Creating {KeyType.GetName()} keys is not supported yet.");
                await toast.Show();
                break;
        }

        await ChangeButtonsIfKeyExistsAsync();
    }
    private async void OnImportClicked(object sender, TappedEventArgs e)
    {
        if (await CheckKeyExistsAsync())
        {
            import.Opacity = 0.3;
            plus.Opacity = 0.3;

            return;
        }
        ;

        switch (KeyType)
        {
            case KeyTypeEnum.Sr25519:
                await Shell.Current.Navigation.PushAsync(new EnterMnemonicsPage(new EnterMnemonicsViewModel
                {
                    Navigation = async (mnemonics) =>
                    {
                        await KeysModel.SaveSr25519KeyAsync(mnemonics);

                        await Shell.Current.Navigation.PopAsync();
                    },
                }));

                break;

            case KeyTypeEnum.PolkadotJson:
                await KeysModel.ImportJsonKeyAsync();

                break;

            case KeyTypeEnum.Did:
                await Shell.Current.Navigation.PushAsync(new ImportDidPage(new ImportDidViewModel
                {
                    Navigation = Shell.Current.Navigation.PopAsync,
                }));

                break;

            case KeyTypeEnum.EncryptionX25519:
                await Shell.Current.Navigation.PushAsync(new ImportEncryptionX25519KeyPage(new ImportEncryptionX25519KeyPageViewModel
                {
                    Navigation = Shell.Current.Navigation.PopAsync,
                }));

                break;

            case KeyTypeEnum.SolanaMnemonic:
                ShowEnterSolanaMnemonicsPopup();

                break;

            case KeyTypeEnum.SolanaMwa:
                // Connecting an existing wallet is both the create and the import path,
                // since there is no local key to generate either way.
                ShowConnectMwaPopup();

                break;

            default:
                var toast = Toast.Make($"Importing {KeyType.GetName()} keys is not supported yet.");
                await toast.Show();

                break;
        }
    }
}