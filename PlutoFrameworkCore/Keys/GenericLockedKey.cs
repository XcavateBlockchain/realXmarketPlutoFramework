using PlutoFramework.Model;
using System.Text.Json;

namespace PlutoFrameworkCore.Keys
{
    public enum KeyTypeEnum
    {
        None,

        Sr25519,
        PolkadotJson,
        Did,
        EncryptionX25519,

        // Appended: this enum is persisted by name in the SQLite Serialized column,
        // so members must not be reordered or renamed.
        SolanaMnemonic,
        SolanaMwa,
    }

    public static class KeyTypeEnumExtensions
    {
        public static string GetName(this KeyTypeEnum type) => type switch
        {
            KeyTypeEnum.EncryptionX25519 => "X25519 key",
            KeyTypeEnum.PolkadotJson => "Json key",
            KeyTypeEnum.Sr25519 => "Sr25519 key",
            KeyTypeEnum.Did => "DID key",
            KeyTypeEnum.SolanaMnemonic => "Solana key",
            KeyTypeEnum.SolanaMwa => "Solana wallet",
            _ => "Key",
        };

        /// <summary>
        /// The Solana account key types. Only one may exist at a time, so that there is a
        /// single unambiguous Solana address, mirroring how Sr25519 and PolkadotJson are
        /// treated as one logical Polkadot account slot.
        /// </summary>
        public static bool IsSolanaAccountType(this KeyTypeEnum type) =>
            type == KeyTypeEnum.SolanaMnemonic || type == KeyTypeEnum.SolanaMwa;

        /// <summary>
        /// The Polkadot account key types, which likewise occupy a single slot.
        /// </summary>
        public static bool IsPolkadotAccountType(this KeyTypeEnum type) =>
            type == KeyTypeEnum.Sr25519 || type == KeyTypeEnum.PolkadotJson;
    }

    public record GenericLockedKey
    {
        public required KeyTypeEnum Type { get; set; }

        public required string PublicKey { get; set; }

        public required string SecretStorageKey { get; set; }

        public required string PasswordStorageKey { get; set; } = PreferencesModel.PASSWORD;

        public string Name => $"{Type} Key {PublicKey}";

        public async Task<Sr25519Key> ToSr25519KeyAsync(string reason)
        {
            if (Type != KeyTypeEnum.Sr25519)
            {
                throw new InvalidOperationException($"Cannot convert key of type {Type} to Sr25519Key");
            }

            var mnemonics = await PlutoConfigurationModel.SecureStorage.GetAsync(SecretStorageKey, reason);

            if (mnemonics == null)
            {
                throw new InvalidOperationException("Mnemonics not found in secure storage");
            }

            return new Sr25519Key
            {
                Mnemonics = mnemonics,
            };
        }

        public async Task<PolkadotJsonKey> ToPolkadotJsonKeyAsync(string reason)
        {
            if (Type != KeyTypeEnum.PolkadotJson)
            {
                throw new InvalidOperationException($"Cannot convert key of type {Type} to PolkadotJsonKey");
            }

            var result = await PlutoConfigurationModel.SecureStorage.GetWithPasswordAsync(SecretStorageKey, PasswordStorageKey, reason);

            if (result.Value == null)
            {
                throw new InvalidOperationException("Json not found in secure storage");
            }

            return new PolkadotJsonKey
            {
                Json = result.Value,
                Password = result.Password,
            };
        }

        public async Task<DidKey> ToDidKeyAsync()
        {
            if (Type != KeyTypeEnum.Did)
            {
                throw new InvalidOperationException($"Cannot convert key of type {Type} to DidKey");
            }

            var mnemonics = await PlutoConfigurationModel.SecureStorage.GetAsync(SecretStorageKey, "Get access to DID key");

            if (mnemonics == null)
            {
                throw new InvalidOperationException("Mnemonics not found in secure storage");
            }

            return new DidKey
            {
                Mnemonics = mnemonics,
            };
        }

        public async Task<SolanaMnemonicKey> ToSolanaMnemonicKeyAsync(string reason)
        {
            if (Type != KeyTypeEnum.SolanaMnemonic)
            {
                throw new InvalidOperationException($"Cannot convert key of type {Type} to SolanaMnemonicKey");
            }

            var mnemonics = await PlutoConfigurationModel.SecureStorage.GetAsync(SecretStorageKey, reason);

            if (mnemonics == null)
            {
                throw new InvalidOperationException("Mnemonics not found in secure storage");
            }

            return new SolanaMnemonicKey
            {
                Mnemonics = mnemonics,
            };
        }

        /// <summary>
        /// The stored secret is the JSON-serialized <see cref="SolanaMwaKey"/>, since the
        /// authorization token it carries is itself sensitive.
        /// </summary>
        public async Task<SolanaMwaKey> ToSolanaMwaKeyAsync(string reason)
        {
            if (Type != KeyTypeEnum.SolanaMwa)
            {
                throw new InvalidOperationException($"Cannot convert key of type {Type} to SolanaMwaKey");
            }

            var json = await PlutoConfigurationModel.SecureStorage.GetAsync(SecretStorageKey, reason);

            if (json == null)
            {
                throw new InvalidOperationException("Mobile Wallet Adapter authorization not found in secure storage");
            }

            var key = JsonSerializer.Deserialize<SolanaMwaKey>(json);

            if (key == null)
            {
                throw new InvalidOperationException("Mobile Wallet Adapter authorization could not be deserialized");
            }

            return key;
        }

        public async Task<EncryptionX25519Key> ToEncryptionX25519KeyAsync()
        {
            if (Type != KeyTypeEnum.EncryptionX25519)
            {
                throw new InvalidOperationException($"Cannot convert key of type {Type} to EncryptionX25519Key");
            }

            var secretKey = await PlutoConfigurationModel.SecureStorage.GetAsync(SecretStorageKey, "Get access to X25519 encryption key");

            if (secretKey == null)
            {
                throw new InvalidOperationException("Mnemonics not found in secure storage");
            }

            return new EncryptionX25519Key
            {
                SecretKey = Convert.FromBase64String(secretKey),
            };
        }

        public async Task<EncryptionX25519Key> ToEncryptionX25519KeyNoAuthAsync()
        {
            if (Type != KeyTypeEnum.EncryptionX25519)
            {
                throw new InvalidOperationException($"Cannot convert key of type {Type} to EncryptionX25519Key");
            }

            var secretKey = await PlutoConfigurationModel.SecureStorage.GetAsyncNoAuthAsync(SecretStorageKey);

            if (secretKey == null)
            {
                throw new InvalidOperationException("Mnemonics not found in secure storage");
            }

            return new EncryptionX25519Key
            {
                SecretKey = Convert.FromBase64String(secretKey),
            };
        }
    }
}
