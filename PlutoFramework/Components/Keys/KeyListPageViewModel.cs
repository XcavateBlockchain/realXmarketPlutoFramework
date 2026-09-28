using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Model.SQLite;
using PlutoFrameworkCore.Keys;
using System.Collections.ObjectModel;

namespace PlutoFramework.Components.Keys
{
    public partial class KeyListPageViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<GenericLockedKey> keys = new ObservableCollection<GenericLockedKey>();

        /// <summary>Held while the add-key page is being pushed, so a double tap cannot stack two.</summary>
        private bool isNavigating = false;

        public KeyListPageViewModel()
        {
            // Load keys from storage or service
            _ = LoadKeysAsync();
        }

        [RelayCommand]
        public async Task Extra1Async()
        {
            if (isNavigating)
            {
                return;
            }

            isNavigating = true;

            try
            {
                await Shell.Current.Navigation.PushAsync(new CreateNewKeyPage());
            }
            finally
            {
                isNavigating = false;
            }
        }

        private async Task LoadKeysAsync()
        {
            var keys = await KeysDatabase.GetAllKeysAsync();

            Keys = new ObservableCollection<GenericLockedKey>(keys);
        }
    }
}
