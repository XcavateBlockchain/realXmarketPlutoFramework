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
