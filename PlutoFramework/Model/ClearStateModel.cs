using PlutoFramework.Model.Xcavate;

namespace PlutoFramework.Model
{
    public class ClearStateModel
    {
        public static void Clear()
        {
            // Remove accounts
            KeysModel.RemoveAccount();
            KeysModel.RemoveAccount("kilt1");

            // KeysModel.RemoveAccount only clears the Substrate preferences. Without this the
            // key database is wiped but SOLANA_PUBLIC_KEY survives, so HasSolanaKey() keeps
            // reporting an account that no longer exists and app start routes a logged-out
            // user into the app shell with no key.
            Preferences.Remove(PreferencesModel.SOLANA_PUBLIC_KEY);

            // Other
            SecureStorage.Default.Remove(PreferencesModel.PASSWORD);
            Preferences.Remove(PreferencesModel.BIOMETRICS_ENABLED);
            OnboardingModel.Clear();

            // Models
            AssetsModel.Clear();
            WhitelistModel.Clear();

            // Files
            XcavateFileModel.DeleteAll();
        }
    }
}
