using PlutoFramework.Model.SQLite;
using PlutoFrameworkCore;
using PlutoFrameworkCore.Keys;

namespace PlutoFramework.Model
{
    public static class GenericLockedKeyExtensions
    {
        public static async Task RemoveAsync(this GenericLockedKey key)
        {
            PlutoConfigurationModel.SecureStorage.Remove(key.SecretStorageKey);

            if (key.PasswordStorageKey != PreferencesModel.PASSWORD)
            {
                PlutoConfigurationModel.SecureStorage.Remove(key.PasswordStorageKey);
            }

            // Both Solana detail pages delete through here, so this is the one place that
            // has to stay in step with the preference the save methods write.
            if (key.Type == KeyTypeEnum.SolanaMnemonic || key.Type == KeyTypeEnum.SolanaMwa)
            {
                Preferences.Remove(PreferencesModel.SOLANA_PUBLIC_KEY);
            }

            await KeysDatabase.DeleteKeyAsync(key);

            // The X25519-missing banner reads a cached answer, and deleting the key flips it.
            if (key.Type == KeyTypeEnum.EncryptionX25519)
            {
                await X25519WarningModel.RefreshAsync();
            }
        }
    }
}
