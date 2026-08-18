extern alias bc26;
using bc26::Org.BouncyCastle.Crypto.Parameters;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Storage;
using Microsoft.AspNetCore.WebUtilities;
using Plugin.Fingerprint;
using Plugin.Fingerprint.Abstractions;
using PlutoFramework.Components.Password;
using PlutoFramework.Model.SQLite;
using PlutoFrameworkCore.AssetDidComm;
using PlutoFrameworkCore.Keys;
using PlutoFrameworkCore.PushNotificationServices.Core;
using Polkadot.NetApi.Generated.Model.sp_core.crypto;
using Substrate.NET.Schnorrkel.Keys;
using Substrate.NetApi;
using Substrate.NetApi.Model.Types;
using System.Text.Json;
// Solnet and Substrate both define an Account type, and this file uses the Substrate one.
using SolanaAccount = Solnet.Wallet.Account;

namespace PlutoFramework.Model
{
    public enum AccountType
    {
        None,
        Mnemonic,
        PrivateKey,
        Json,
    }
    public static class KeysModel
    {
        // Can change with future updates to substrate
        private const ExpandMode DEFAULT_EXPAND_MODE = ExpandMode.Ed25519;
        public static Task GenerateNewAccountAsync()
        {
            string mnemonics = MnemonicsModel.GenerateMnemonics();

            return SaveSr25519KeyAsync(mnemonics);
        }

        public static Task GenerateNewDidAsync()
        {
            string mnemonics = MnemonicsModel.GenerateMnemonics();

            return SaveDidKeyAsync(mnemonics);
        }
        public static Task GenerateNewEncryptionX25519KeyAsync()
        {
            var keyPair = X25519Model.GenerateX25519KeyPair();

            return SaveEncryptionX25519KeyAsync(keyPair.PrivateKey);
        }

        public static async Task<bool> HasEncryptionX25519KeyAsync() =>
            (await KeysDatabase.GetAllKeysOfTypeAsync(KeyTypeEnum.EncryptionX25519)).Any();

        /// <summary>
        /// Ensures an X25519 encryption key exists, which the profile API requires and no
        /// Solana onboarding path creates. Derives it from the seed phrase when there is one,
        /// so it is recoverable from the same backup as the account, and generates a fresh one
        /// for Mobile Wallet Adapter, which keeps no phrase on the device.
        /// </summary>
        /// <remarks>
        /// Returns without touching anything when a key already exists. That guard is the
        /// point: <see cref="SaveEncryptionX25519KeyAsync(byte[])"/> deletes every existing
        /// X25519 key before writing, so calling this unguarded when a Substrate user later
        /// adds a Solana key would silently destroy the messaging key they already have.
        /// </remarks>
        /// <param name="mnemonics">
        /// The Solana phrase, when the caller already holds it. Saves unlocking the stored key
        /// again, which would mean a second authentication prompt.
        /// </param>
        public static async Task EnsureEncryptionX25519KeyAsync(
            string reason = "Set up your encryption key",
            string? mnemonics = null)
        {
            if (await HasEncryptionX25519KeyAsync())
            {
                return;
            }

            if (mnemonics is not null)
            {
                await SaveEncryptionX25519KeyAsync(mnemonics);

                return;
            }

            var lockedKey = (await KeysDatabase.GetAllKeysOfTypeAsync(
                KeyTypeEnum.SolanaMnemonic, KeyTypeEnum.SolanaMwa)).FirstOrDefault();

            switch (lockedKey?.Type)
            {
                case KeyTypeEnum.SolanaMnemonic:
                    var solanaKey = await lockedKey.ToSolanaMnemonicKeyAsync(reason);

                    await SaveEncryptionX25519KeyAsync(solanaKey.Mnemonics);

                    break;

                // Nothing to derive from: either an MWA wallet, which never hands over a
                // phrase, or no Solana key at all, which is a Substrate-only user - and their
                // key has always been random, generated alongside the account.
                default:
                    await GenerateNewEncryptionX25519KeyAsync();

                    break;
            }
        }

        /// <summary>Whether a Polkadot account key of either stored kind exists.</summary>
        public static async Task<bool> HasSubstrateAccountAsync() =>
            (await KeysDatabase.GetAllKeysOfTypeAsync(KeyTypeEnum.Sr25519, KeyTypeEnum.PolkadotJson)).Any();

        public static async Task<bool> HasDidKeyAsync() =>
            (await KeysDatabase.GetAllKeysOfTypeAsync(KeyTypeEnum.Did)).Any();

        /// <summary>
        /// Ensures the Substrate account and DID that the rest of onboarding is keyed to
        /// exist. Sumsub applicants are identified by SS58 address and DID, the questionnaire
        /// submits one, and roles live in the XcavatePaseo whitelist pallet - so a Solana-only
        /// account cannot complete verification, and cannot invest once it has.
        /// </summary>
        /// <remarks>
        /// Each key is guarded separately, and an existing one is never rewritten:
        /// <see cref="SaveSr25519KeyAsync"/> and <see cref="SaveDidKeyAsync"/> both delete
        /// before they write, so calling this unguarded for a user who already holds a
        /// Polkadot account would destroy it. Same shape as
        /// <see cref="EnsureEncryptionX25519KeyAsync"/>, which guards the third key of the set.
        /// </remarks>
        /// <param name="mnemonics">
        /// The phrase to derive from, when the caller has one. Both keys then come off the
        /// same backup as the account itself. Null for a Mobile Wallet Adapter wallet, which
        /// keeps its phrase in the wallet app and never hands it over; a fresh phrase is
        /// generated for those, exactly as the X25519 key already is.
        /// </param>
        public static async Task EnsureSubstrateIdentityAsync(string? mnemonics = null)
        {
            var seed = string.IsNullOrWhiteSpace(mnemonics) ? null : mnemonics.Trim();

            if (!await HasSubstrateAccountAsync())
            {
                seed ??= MnemonicsModel.GenerateMnemonics();

                await SaveSr25519KeyAsync(seed);
            }

            if (!await HasDidKeyAsync())
            {
                seed ??= MnemonicsModel.GenerateMnemonics();

                // An independent key derived from the same phrase, matching what the Polkadot
                // onboarding flow has always written.
                await SaveDidKeyAsync($"{seed}//did");
            }
        }

        public static async Task RegisterBiometricAuthenticationAsync()
        {
            if (Preferences.Get(PreferencesModel.BIOMETRICS_ENABLED, false))
            {
                return;
            }

            try
            {
                // Set biometrics
                for (int i = 0; i < 5; i++)
                {
                    var request = new AuthenticationRequestConfiguration("Biometric verification", "..");

                    var result = await CrossFingerprint.Current.AuthenticateAsync(request);

                    if (result.Authenticated)
                    {
                        // Fingerprint set, perhaps do with it something in the future

                        Preferences.Set(PreferencesModel.BIOMETRICS_ENABLED, true);

                        break;
                    }
                }
            }
            catch
            {
                // throws exception if Authentication is not awailable
                // Instead, just password will be used
            }
        }

        public static async Task SaveSr25519KeyAsync(string mnemonics)
        {
            await KeysDatabase.DeleteKeysOfTypeAsync(KeyTypeEnum.Sr25519);
            await KeysDatabase.DeleteKeysOfTypeAsync(KeyTypeEnum.PolkadotJson);

            Account account = MnemonicsModel.GetAccountFromMnemonics(mnemonics);

            Preferences.Set(
                PreferencesModel.PUBLIC_KEY,
                account.Value
            );

            // Just get and use the same main password without asking the user again
            var password = await SecureStorage.Default.GetAsync(PreferencesModel.PASSWORD);

            await KeysModel.SaveKeyAsync(
                publicKey: account.Value,
                secret: mnemonics,
                password: password!,
                type: KeyTypeEnum.Sr25519
            );
        }

        public static async Task SaveDidKeyAsync(string mnemonics)
        {
            await KeysDatabase.DeleteKeysOfTypeAsync(KeyTypeEnum.Did);

            Account account = MnemonicsModel.GetAccountFromMnemonics(mnemonics);

            // Just get and use the same main password without asking the user again
            var password = await SecureStorage.Default.GetAsync(PreferencesModel.PASSWORD);

            await KeysModel.SaveKeyAsync(
                publicKey: account.Value,
                secret: mnemonics,
                password: password!,
                type: KeyTypeEnum.Did
            );
        }

        public static Task GenerateNewSolanaAccountAsync()
        {
            string mnemonics = SolanaMnemonicsModel.GenerateMnemonics();

            return SaveSolanaMnemonicKeyAsync(mnemonics);
        }

        /// <summary>
        /// Both Solana key types occupy a single account slot, so adding either one
        /// clears the other. This mirrors how <see cref="SaveSr25519KeyAsync"/> treats
        /// Sr25519 and PolkadotJson as one Polkadot account.
        /// </summary>
        private static Task DeleteExistingSolanaKeysAsync() => Task.WhenAll(
            KeysDatabase.DeleteKeysOfTypeAsync(KeyTypeEnum.SolanaMnemonic),
            KeysDatabase.DeleteKeysOfTypeAsync(KeyTypeEnum.SolanaMwa)
        );

        public static async Task SaveSolanaMnemonicKeyAsync(string mnemonics)
        {
            if (!SolanaMnemonicsModel.ValidateMnemonics(mnemonics))
            {
                throw new ArgumentException("The mnemonic phrase is not a valid BIP39 phrase", nameof(mnemonics));
            }

            await DeleteExistingSolanaKeysAsync();

            string address = SolanaMnemonicsModel.GetAddressFromMnemonics(mnemonics);

            // Just get and use the same main password without asking the user again
            var password = await SecureStorage.Default.GetAsync(PreferencesModel.PASSWORD);

            await SaveKeyAsync(
                publicKey: address,
                secret: mnemonics.Trim(),
                password: password!,
                type: KeyTypeEnum.SolanaMnemonic
            );

            // Written only once the key is actually persisted. Mirrors PUBLIC_KEY for Substrate:
            // app start decides its shell before it can await, so key presence has to be
            // readable synchronously - which makes a preference claiming a key that the save
            // never stored strictly worse than no preference at all.
            Preferences.Set(PreferencesModel.SOLANA_PUBLIC_KEY, address);

            // Derived from the phrase the caller already has, so the user is not asked to
            // authenticate a second time and the key is recoverable from the same backup.
            await EnsureEncryptionX25519KeyAsync(mnemonics: mnemonics.Trim());
        }

        /// <summary>
        /// Persists a Mobile Wallet Adapter authorization. The whole record is stored as
        /// the secret because the auth token it carries grants signing access.
        /// </summary>
        public static async Task SaveSolanaMwaKeyAsync(SolanaMwaKey key)
        {
            await DeleteExistingSolanaKeysAsync();

            // Just get and use the same main password without asking the user again
            var password = await SecureStorage.Default.GetAsync(PreferencesModel.PASSWORD);

            await SaveKeyAsync(
                publicKey: key.Address,
                secret: JsonSerializer.Serialize(key),
                password: password!,
                type: KeyTypeEnum.SolanaMwa
            );

            // Written only once the key is actually persisted - see SaveSolanaMnemonicKeyAsync.
            Preferences.Set(PreferencesModel.SOLANA_PUBLIC_KEY, key.Address);

            // No phrase to derive from - the wallet keeps the key - so this one is random.
            await EnsureEncryptionX25519KeyAsync();
        }

        public static async Task<bool> HasSolanaKeyAsync() =>
            (await KeysDatabase.GetAllKeysOfTypeAsync(KeyTypeEnum.SolanaMnemonic, KeyTypeEnum.SolanaMwa)).Any();

        /// <summary>
        /// The Solana address for whichever variant is configured. Available for both,
        /// since the MWA key stores the authorized address alongside its token.
        /// </summary>
        public static async Task<string?> GetSolanaAddressAsync()
        {
            var keys = await KeysDatabase.GetAllKeysOfTypeAsync(KeyTypeEnum.SolanaMnemonic, KeyTypeEnum.SolanaMwa);

            return keys.FirstOrDefault()?.PublicKey;
        }

        /// <summary>
        /// A signing account, which only the mnemonic variant can produce. Under Mobile
        /// Wallet Adapter the private key never leaves the wallet app, so this returns
        /// null and callers must sign through <c>MwaClient</c> instead.
        /// </summary>
        public static async Task<SolanaAccount?> GetSolanaAccountAsync(string reason = "Get access to Solana key")
        {
            var keys = await KeysDatabase.GetAllKeysOfTypeAsync(KeyTypeEnum.SolanaMnemonic);

            var lockedKey = keys.FirstOrDefault();

            if (lockedKey is null)
            {
                return null;
            }

            try
            {
                return (await lockedKey.ToSolanaMnemonicKeyAsync(reason)).Account;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// The stored Mobile Wallet Adapter authorization, or null when the configured
        /// Solana key is a local mnemonic one.
        /// </summary>
        public static async Task<SolanaMwaKey?> GetSolanaMwaKeyAsync(string reason = "Get access to Solana wallet")
        {
            var keys = await KeysDatabase.GetAllKeysOfTypeAsync(KeyTypeEnum.SolanaMwa);

            var lockedKey = keys.FirstOrDefault();

            if (lockedKey is null)
            {
                return null;
            }

            try
            {
                return await lockedKey.ToSolanaMwaKeyAsync(reason);
            }
            catch
            {
                return null;
            }
        }

        [Obsolete("For migration purposes only")]
        public static async Task GenerateNewAccountFromPrivateKeyAsync(string privateKey, string accountVariant = "")
        {
            var miniSecret = new MiniSecret(Utils.HexToByteArray(privateKey), ExpandMode.Ed25519);

            Account account = Account.Build(
                KeyType.Sr25519,
                miniSecret.ExpandToSecret().ToEd25519Bytes(),
                miniSecret.GetPair().Public.Key
            );

            await SecureStorage.Default.SetAsync(
                 PreferencesModel.PRIVATE_KEY + accountVariant,
                 privateKey
            );

            Preferences.Set(
                 PreferencesModel.PUBLIC_KEY + accountVariant,
                 account.Value
            );
        }

        public static async Task SaveJsonKeyAsync(string json)
        {
            await KeysDatabase.DeleteKeysOfTypeAsync(KeyTypeEnum.Sr25519);
            await KeysDatabase.DeleteKeysOfTypeAsync(KeyTypeEnum.PolkadotJson);

            var viewModel = DependencyService.Get<EnterPasswordPopupViewModel>();

            viewModel.IsVisible = true;
            viewModel.Reason = "Unlock JSON account";

            string correctPassword = "";
            string publicKey = "";

            for (int i = 0; i < 5; i++)
            {
                var password = await viewModel.EnteredPassword.Task;

                viewModel.EnteredPassword = new();

                if (password is null)
                {
                    return;
                }

                var wallet = MnemonicsModel.ImportJson(json, password);

                if (wallet is not null && wallet.IsUnlocked)
                {
                    correctPassword = password;
                    publicKey = wallet.Account.Value;

                    viewModel.SetToDefault();

                    break;
                }

                viewModel.ErrorIsVisible = true;

                if (i == 4)
                {
                    viewModel.SetToDefault();
                    throw new Exception("Failed to authenticate");
                }
            }

            Preferences.Set(
                 PreferencesModel.PUBLIC_KEY,
                 publicKey
            );

            await SecureStorage.Default.SetAsync(
                PreferencesModel.PASSWORD,
                correctPassword
            );

            await KeysModel.SaveKeyAsync(
                publicKey: publicKey,
                secret: json,
                password: correctPassword,
                type: KeyTypeEnum.PolkadotJson
            );
        }

        public static Task SaveEncryptionX25519KeyAsync(string mnemonics)
        {

            // Derive X25519 private key from mnemonic via Ed25519 seed conversion.
            var account = MnemonicsModel.GetAccountFromMnemonics(mnemonics, Substrate.NetApi.Model.Types.KeyType.Ed25519);

            byte[] seed = account.PrivateKey;

            if (seed is null || seed.Length < 32)
            {
                throw new ArgumentException("Derived private key seed is too short to derive X25519 key");
            }

            if (seed.Length > 32)
            {
                // If representation contains extra data, take the first 32 bytes as seed
                var tmp = new byte[32];
                Array.Copy(seed, 0, tmp, 0, 32);
                seed = tmp;
            }

            // Hash the seed with SHA-512 and clamp to produce X25519 private scalar per RFC7748
            using var sha512 = System.Security.Cryptography.SHA512.Create();
            var hashed = sha512.ComputeHash(seed);

            var x25519 = new byte[32];
            Array.Copy(hashed, 0, x25519, 0, 32);

            // Clamp
            x25519[0] &= 248;
            x25519[31] &= 127;
            x25519[31] |= 64;

            return SaveEncryptionX25519KeyAsync(x25519);
        }

        public static async Task SaveEncryptionX25519KeyAsync(byte[] privateKey)
        {
            await KeysDatabase.DeleteKeysOfTypeAsync(KeyTypeEnum.EncryptionX25519);

            var key = new X25519PrivateKeyParameters(privateKey);

            var publicKey = key.GeneratePublicKey();

            // Just get and use the same main password without asking the user again
            var password = await SecureStorage.Default.GetAsync(PreferencesModel.PASSWORD);

            await KeysModel.SaveKeyAsync(
                publicKey: Convert.ToBase64String(publicKey.GetEncoded()),
                secret: Convert.ToBase64String(privateKey),
                password: password!,
                type: KeyTypeEnum.EncryptionX25519
            );
        }

        public static async Task ImportJsonKeyAsync()
        {
            var jsonType = new FilePickerFileType(
                new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                { DevicePlatform.iOS, new[] { "public.json" } }, // UTType values
                { DevicePlatform.Android, new[] { "application/json" } }, // MIME type
                { DevicePlatform.WinUI, new[] { ".json" } }, // file extension
                { DevicePlatform.Tizen, new[] { "*/*" } },
                { DevicePlatform.macOS, new[] { "public.json" } }, // UTType values
                });

            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Import json account",
                FileTypes = jsonType,
            });

            if (result is null || !result.FileName.Contains(".json"))
                return;

            using var jsonStream = await result.OpenReadAsync();

            string json = StreamToString(jsonStream);

            try
            {
                await KeysModel.SaveJsonKeyAsync(json);

                var toast = Toast.Make($"JSON key imported successfully.");
                await toast.Show();
            }
            catch (Exception ex)
            {
                Console.WriteLine("JSON import exception: ");
                Console.WriteLine(ex);

                var toast = Toast.Make($"Failed to import JSON key.");
                await toast.Show();
            }
        }

        public static async Task ImportJsonX25519KeyAsync()
        {
            var jsonType = new FilePickerFileType(
                new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                { DevicePlatform.iOS, new[] { "public.json" } }, // UTType values
                { DevicePlatform.Android, new[] { "application/json" } }, // MIME type
                { DevicePlatform.WinUI, new[] { ".json" } }, // file extension
                { DevicePlatform.Tizen, new[] { "*/*" } },
                { DevicePlatform.macOS, new[] { "public.json" } }, // UTType values
                });

            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Import X25519 key",
                FileTypes = jsonType,
            });

            if (result is null || !result.FileName.Contains(".json"))
                return;

            using var jsonStream = await result.OpenReadAsync();

            string json = StreamToString(jsonStream);

            try
            {
                var document = JsonDocument.Parse(json);
                var privateJwk = document.RootElement.GetProperty("privateJwk");
                var dValue = privateJwk.GetProperty("d").GetString();

                if (string.IsNullOrEmpty(dValue))
                {
                    throw new InvalidOperationException("Private key 'd' value not found in JSON");
                }

                var privateKeyBytes = WebEncoders.Base64UrlDecode(dValue);

                await SaveEncryptionX25519KeyAsync(privateKeyBytes);

                var toast = Toast.Make($"X25519 key JSON imported successfully.");
                await toast.Show();
            }
            catch
            {
                var toast = Toast.Make($"Failed to import X25519 key JSON.");
                await toast.Show();
            }
        }

        private static string StreamToString(Stream stream)
        {
            stream.Position = 0;
            using (StreamReader reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }
        public static bool HasSubstrateKey()
        {
            return Preferences.ContainsKey(PreferencesModel.PUBLIC_KEY);
        }

        public static string GetSubstrateKey()
        {
            return Preferences.Get(PreferencesModel.PUBLIC_KEY, "Substrate key does not exist");
        }

        public static string GetSubstrateKey(short ss58prefix)
        {
            return Utils.GetAddressFrom(Utils.GetPublicKeyFrom(KeysModel.GetSubstrateKey()), ss58prefix);
        }

        public static bool HasSolanaKey()
        {
            return Preferences.ContainsKey(PreferencesModel.SOLANA_PUBLIC_KEY);
        }

        /// <summary>
        /// The stored Solana address, or null when no Solana key is configured. Returns null
        /// rather than a placeholder string: <see cref="GetSubstrateKey()"/>'s placeholder
        /// habit is what makes <c>GetSubstrateKey(0)</c> throw further down the call chain.
        /// </summary>
        public static string? GetSolanaAddress()
        {
            return Preferences.ContainsKey(PreferencesModel.SOLANA_PUBLIC_KEY)
                ? Preferences.Get(PreferencesModel.SOLANA_PUBLIC_KEY, string.Empty)
                : null;
        }

        public static async Task<string> GetDidAddressAsync(CancellationToken token)
        {
            var dids = await KeysDatabase.GetAllKeysOfTypeAsync(KeyTypeEnum.Did);

            if (!dids.Any())
            {
                throw new Exception("Did key not found");
            }

            var did = dids.First();

            return did.PublicKey;
        }

        public static string GetPublicKey()
        {
            var array = GetPublicKeyBytes();
            return Utils.Bytes2HexString(array);
        }

        public static byte[] GetPublicKeyBytes()
        {
            return Utils.GetPublicKeyFrom(GetSubstrateKey());
        }

        public static async Task<Account?> GetAccountAsync(string reason = "..")
        {
            var accounts = await KeysDatabase.GetAllKeysOfTypeAsync(KeyTypeEnum.Sr25519, KeyTypeEnum.PolkadotJson);

            if (!accounts.Any())
            {
                return null;
            }

            var accountLockedKey = accounts.First();

            try
            {

                return accountLockedKey.Type switch
                {
                    KeyTypeEnum.Sr25519 => (await accountLockedKey.ToSr25519KeyAsync(reason)).Account,
                    KeyTypeEnum.PolkadotJson => (await accountLockedKey.ToPolkadotJsonKeyAsync(reason)).Account,
                };
            }
            catch
            {
                return null;
            }
        }

        public static async Task<Account?> GetDidAsync()
        {
            var accounts = await KeysDatabase.GetAllKeysOfTypeAsync(KeyTypeEnum.Did);

            if (!accounts.Any())
            {
                return null;
            }

            var accountLockedKey = accounts.First();

            try
            {
                var accountKey = await accountLockedKey.ToDidKeyAsync();

                return accountKey.Account;
            }
            catch
            {
                return null;
            }
        }

        public static async Task<EncryptionX25519Key?> GetX25519KeyAsync()
        {
            var accounts = await KeysDatabase.GetAllKeysOfTypeAsync(KeyTypeEnum.EncryptionX25519);

            if (!accounts.Any())
            {
                return null;
            }

            var accountLockedKey = accounts.First();

            try
            {
                return await accountLockedKey.ToEncryptionX25519KeyAsync();
            }
            catch
            {
                return null;
            }
        }

        public static async Task<EncryptionX25519Key?> GetX25519KeyNoAuthAsync()
        {
            var accounts = await KeysDatabase.GetAllKeysOfTypeAsync(KeyTypeEnum.EncryptionX25519);

            if (!accounts.Any())
            {
                return null;
            }

            var accountLockedKey = accounts.First();

            try
            {
                return await accountLockedKey.ToEncryptionX25519KeyNoAuthAsync();
            }
            catch
            {
                return null;
            }
        }

        public static AccountId32 GetAccountId32()
        {
            var accountId = new AccountId32();
            accountId.Create(GetPublicKeyBytes());

            return accountId;
        }

        /// <summary>
        /// Source: https://learn.microsoft.com/en-us/dotnet/communitytoolkit/maui/essentials/file-saver?tabs=macos 
        /// </summary>
        public static async Task ExportJsonFileAsync(string json, CancellationToken token)
        {
            using var stream = new MemoryStream(System.Text.Encoding.Default.GetBytes(json));
            var fileSaverResult = await FileSaver.Default.SaveAsync($"{AppInfo.Current.Name.ToLower()}.json", stream, token);

            if (fileSaverResult.IsSuccessful)
            {
                await Toast.Make($"Mnemonics successfully exported.").Show(token);
            }
            else
            {
                await Toast.Make($"Failed to export.").Show(token);
            }
        }

        public static Task<IEnumerable<GenericLockedKey>> GetKeysOfTypeAsync(KeyTypeEnum type) => KeysDatabase.GetAllKeysOfTypeAsync(type);

        public static async Task<GenericLockedKey?> GetKeyOfTypeAsync(KeyTypeEnum type) => (await KeysDatabase.GetAllKeysOfTypeAsync(type)).FirstOrDefault();

        public static void RemoveAccount(string accountVariant = "")
        {
            Preferences.Remove(PreferencesModel.PUBLIC_KEY + accountVariant);
            Preferences.Remove(PreferencesModel.PRIVATE_KEY_EXPAND_MODE + accountVariant);
            SecureStorage.Default.Remove(PreferencesModel.PRIVATE_KEY + accountVariant);
            SecureStorage.Default.Remove(PreferencesModel.MNEMONICS + accountVariant);
            SecureStorage.Default.Remove(PreferencesModel.JSON_ACCOUNT + accountVariant);
            SecureStorage.Default.Remove(PreferencesModel.PASSWORD + accountVariant);
        }

        public static Task SaveKeyAsync(
            string publicKey,
            string secret,
            string password,
            KeyTypeEnum type
        )
        {
            var secretStorageKey = $"secret-{publicKey}";
            var passwordStorageKey = $"password-{publicKey}";

            var lockedKey = new GenericLockedKey
            {
                Type = type,
                PublicKey = publicKey,
                SecretStorageKey = secretStorageKey,
                PasswordStorageKey = passwordStorageKey,
            };

            // Register the address as a main key on the notifications api. Signature-less
            // (the server records Polkadot unverified - no sr25519 verification yet), so
            // no prompt interrupts the save.
            if (type == KeyTypeEnum.Sr25519 || type == KeyTypeEnum.PolkadotJson)
            {
                _ = WalletLinkModel.LinkPolkadotAsync(KeysModel.GetSubstrateKey());
            }

            // The one moment a Solana link can be signed without an unlock prompt: the
            // phrase is right here in hand. SolanaMwa is deliberately absent - its saves
            // also happen mid-session when a signature refreshes the authorization, and
            // launching a second wallet trip from there would collide with the open one.
            // MWA links from the connect flow and after signature sessions instead
            // (WalletLinkModel.TryLinkSolanaMwaAsync).
            if (type == KeyTypeEnum.SolanaMnemonic)
            {
                _ = WalletLinkModel.LinkSolanaMnemonicAsync(publicKey, secret);
            }

            return Task.WhenAll(
                SecureStorage.SetAsync(secretStorageKey, secret),
                SecureStorage.SetAsync(passwordStorageKey, password),
                KeysDatabase.SaveKeyAsync(lockedKey)
            );
        }

        public static Task ClearAsync()
        {
            // A device that no longer holds these keys must stop receiving their
            // notifications. Uses the stored link list, so key deletion below can proceed.
            _ = WalletLinkModel.UnlinkAllAsync();

            Preferences.Remove(PreferencesModel.PUBLIC_KEY);
            Preferences.Remove(PreferencesModel.SOLANA_PUBLIC_KEY);

            // A choice of main key outliving both keys would silently apply to whatever the
            // next account turns out to be. Resolution tolerates that, but the next user of
            // this device should start from the default rather than the last one's preference.
            Preferences.Remove(PreferencesModel.SETTINGS_MAIN_KEY_CHAIN);

            return KeysDatabase.DeleteAllAsync();

        }

        public static async Task TempConvertMainKeysIntoDbAsync()
        {
            // Keys are saved in DB, no need to convert them twice
            if ((await KeysDatabase.GetAllKeysAsync()).Any())
            {
                return;
            }

            if (Preferences.ContainsKey(PreferencesModel.PUBLIC_KEY))
            {
                var accountType = (AccountType)Enum.Parse(typeof(AccountType), Preferences.Get("accountType", AccountType.None.ToString()));

                if (accountType == AccountType.Mnemonic)
                {
                    var substrateKey = Preferences.Get(PreferencesModel.PUBLIC_KEY, "");
                    var mnemonics = await SecureStorage.Default.GetAsync(PreferencesModel.MNEMONICS);
                    var password = await SecureStorage.Default.GetAsync(PreferencesModel.PASSWORD);

                    await SaveKeyAsync(
                        publicKey: substrateKey,
                        secret: mnemonics!,
                        password: password!,
                        type: KeyTypeEnum.Sr25519
                    );
                }

                if (accountType == AccountType.Json)
                {
                    var substrateKey = Preferences.Get(PreferencesModel.PUBLIC_KEY, "");
                    var json = await SecureStorage.Default.GetAsync(PreferencesModel.JSON_ACCOUNT);
                    var password = await SecureStorage.Default.GetAsync(PreferencesModel.PASSWORD);

                    await SaveKeyAsync(
                        publicKey: substrateKey,
                        secret: json!,
                        password: password!,
                        type: KeyTypeEnum.PolkadotJson
                    );
                }
            }
        }
    }
}

