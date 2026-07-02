using PlutoFrameworkCore;

namespace PlutoFramework.Model
{
    public class PlutoSecureStorage : IPlutoSecureStorage
    {
        public async Task<SecretResult> GetWithPasswordAsync(string key, string passwordKey, string reason)
        {
            var authentication = await RequirementsModel.CheckAuthenticationAsync(passwordKey, reason);

            if (!authentication.Value)
            {
                return new SecretResult
                {
                    Password = "-",
                    Value = null
                };
            }

            return new SecretResult
            {
                Password = authentication.Password,
                Value = await SecureStorage.Default.GetAsync(key).ConfigureAwait(false)
            };
        }

        public async Task<string?> GetAsync(string key, string reason)
        {
            var result = await GetWithPasswordAsync(key, PreferencesModel.PASSWORD, reason);

            return result.Value;
        }


        public bool Remove(string key) => SecureStorage.Default.Remove(key);

        public void RemoveAll() => SecureStorage.Default.RemoveAll();

        public Task SetAsync(string key, string value) => SecureStorage.Default.SetAsync(key, value);
    }
}
