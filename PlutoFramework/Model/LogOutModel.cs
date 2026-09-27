using PlutoFramework.Model.SQLite;

namespace PlutoFramework.Model
{
    public static class LogOutModel
    {
        public static async Task LogOutAsync()
        {
            ClearStateModel.Clear();

            await SQLiteModel.DeleteAllDatabasesAsync();

            await Shell.Current.GoToAsync("//LoggedOutPage");
        }
    }
}
