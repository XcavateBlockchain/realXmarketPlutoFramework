namespace PlutoFramework.Model.SQLite
{
    public class SQLiteModel
    {
        public static Task DeleteAllDatabasesAsync()
        {
            return Task.WhenAll(
                XcavatePropertyDatabase.DeleteAllAsync(),
                BalancesDatabase.DeleteAllAsync(),
                XcavateUserDatabase.DeleteAllAsync(),
                KeysDatabase.DeleteAllAsync()
            );
        }
    }
}
