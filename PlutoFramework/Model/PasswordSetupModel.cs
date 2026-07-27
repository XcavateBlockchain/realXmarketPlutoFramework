namespace PlutoFramework.Model
{
    /// <summary>
    /// Commits the app password chosen during setup.
    /// </summary>
    /// <remarks>
    /// Both setup screens go through here so that "the password is stored" and "biometrics
    /// are registered" cannot come apart on one screen and not the other.
    /// </remarks>
    public static class PasswordSetupModel
    {
        public static async Task SaveNewPasswordAsync(string password)
        {
            await SecureStorage.Default.SetAsync(PreferencesModel.PASSWORD, password);

            await KeysModel.RegisterBiometricAuthenticationAsync();
        }
    }
}
