using CommunityToolkit.Maui.Alerts;
using PlutoFramework.Components.Solana;
using PlutoFrameworkCore.Keys;

namespace PlutoFramework.Components.Keys;

public partial class KeyView : ContentView
{
    public static readonly BindableProperty KeyProperty = BindableProperty.Create(
        nameof(Key),
        typeof(GenericLockedKey),
        typeof(KeyView),
        null,
        propertyChanged: (bindable, oldValue, newValue) =>
        {
            var control = (KeyView)bindable;

            if (newValue == null)
            {
                return;
            }

            var key = (GenericLockedKey)newValue;

            control.nameLabel.Text = key.Type.GetName();

            control.publicKeyLabel.Text = key.PublicKey;

            control.descriptionLabel.Text = key.Type switch
            {
                KeyTypeEnum.EncryptionX25519 => "Used for message encryption/decryption",
                KeyTypeEnum.PolkadotJson => "Your Account key",
                KeyTypeEnum.Sr25519 => "Your Account key",
                KeyTypeEnum.Did => "Decentralised Identifier",
                KeyTypeEnum.SolanaMnemonic => "Your Solana account",
                KeyTypeEnum.SolanaMwa => "Connected Solana wallet",
                _ => "",
            };

            // TODO icon
        });
    public KeyView()
    {
        InitializeComponent();
    }

    public GenericLockedKey Key
    {
        get => (GenericLockedKey)GetValue(KeyProperty);
        set => SetValue(KeyProperty, value);
    }

    private async void OnClicked(object sender, TappedEventArgs e)
    {
        try
        {
            Page page = Key.Type switch
            {
                KeyTypeEnum.Sr25519 => new Sr25519KeyDetailPage(new Sr25519KeyDetailPageViewModel
                {
                    LockedKey = Key,
                    UnlockedKey = await Key.ToSr25519KeyAsync("Get access to account key"),
                }),
                KeyTypeEnum.PolkadotJson => new PolkadotJsonKeyDetailPage(new PolkadotJsonKeyDetailPageViewModel
                {
                    LockedKey = Key,
                    UnlockedKey = await Key.ToPolkadotJsonKeyAsync("Get access to account key"),
                }),
                KeyTypeEnum.Did => new DidKeyDetailPage(new DidKeyDetailPageViewModel
                {
                    LockedKey = Key,
                    UnlockedKey = await Key.ToDidKeyAsync(),
                }),
                KeyTypeEnum.EncryptionX25519 => new EncryptionX25519KeyDetailPage(new EncryptionX25519KeyDetailPageViewModel
                {
                    LockedKey = Key,
                    UnlockedKey = await Key.ToEncryptionX25519KeyAsync(),
                }),
                KeyTypeEnum.SolanaMnemonic => new SolanaMnemonicKeyDetailPage(new SolanaMnemonicKeyDetailPageViewModel
                {
                    LockedKey = Key,
                    UnlockedKey = await Key.ToSolanaMnemonicKeyAsync("Get access to Solana key"),
                }),
                KeyTypeEnum.SolanaMwa => new SolanaMwaKeyDetailPage(new SolanaMwaKeyDetailPageViewModel
                {
                    LockedKey = Key,
                    UnlockedKey = await Key.ToSolanaMwaKeyAsync("Get access to Solana wallet"),
                }),
                _ => throw new Exception($"Key {Key.Type} type is missing."),
            };

            await Shell.Current.Navigation.PushAsync(page);
        }
        catch
        {
            Console.WriteLine("Json content: ");
            Console.WriteLine(await SecureStorage.Default.GetAsync(Key.SecretStorageKey));

            Console.WriteLine(Key.PasswordStorageKey);
            Console.WriteLine(await SecureStorage.Default.GetAsync(Key.PasswordStorageKey));

            var toast = Toast.Make("Could not open the key");
            await toast.Show();
        }
    }
}