namespace CodexArcanum.Smaragdus.Storage;

public class SqliteConnectionFactory
{
    public static SqliteConnection CreateConnection(string connectionString)
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();

        cmd.CommandText = "PRAGMA journal_mode=WAL;";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "PRAGMA busy_timeout=5000;";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "PRAGMA cache_size=-10000;";
        cmd.ExecuteNonQuery();

        return connection;
    }
}
