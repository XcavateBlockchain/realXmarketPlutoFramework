namespace PlutoFramework.Model
{
    /// <summary>
    /// The cached answer behind <see cref="Components.Keys.X25519MissingWarningView"/>:
    /// whether the device holds an account but no X25519 encryption key. Cached because the
    /// check reads the key database, which views cannot await while they lay out - they read
    /// <see cref="IsActive"/> synchronously and subscribe to <see cref="AvailabilityChanged"/>
    /// instead. Every path that creates, imports or deletes an X25519 key, or wipes the
    /// account, must call <see cref="RefreshAsync"/> so the cache and the banners follow.
    /// </summary>
    public static class X25519WarningModel
    {
        private static bool _isActive;

        public static bool IsActive => _isActive;

        public static event EventHandler? AvailabilityChanged;

        public static async Task RefreshAsync()
        {
            var hasAccount = KeysModel.HasSolanaKey() || KeysModel.HasSubstrateKey();

            var active = hasAccount && !await KeysModel.HasEncryptionX25519KeyAsync();

            if (active == _isActive)
            {
                return;
            }

            _isActive = active;

            AvailabilityChanged?.Invoke(null, EventArgs.Empty);
        }
    }
}
