using PlutoFramework.Model.SQLite;
using PlutoFrameworkCore;
using PlutoFrameworkCore.Keys;

namespace PlutoFramework.Model
{
    public static class GenericLockedKeyExtensions
    {
        public static Task RemoveAsync(this GenericLockedKey key)
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

            return KeysDatabase.DeleteKeyAsync(key);
        }
    }
}
