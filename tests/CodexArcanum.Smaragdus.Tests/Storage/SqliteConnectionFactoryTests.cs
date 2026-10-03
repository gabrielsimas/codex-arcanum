using CodexArcanum.Smaragdus.Storage;

namespace CodexArcanum.Smaragdus.Tests.Storage;

public class SqliteConnectionFactoryTests
{
    [Fact]
    public void CreateConnection_ShouldConfigureMaatPragmas()
    {
        // Arrange
        var connectionString = "Data Source=:memory:";

        // Act
        using var connection = SqliteConnectionFactory.CreateConnection(connectionString);
        connection.Open();

        Assert.NotNull(connection);
        Assert.Equal(System.Data.ConnectionState.Open, connection.State);

        using var cmd = connection.CreateCommand();

        cmd.CommandText = "PRAGMA journal_mode;";
        var journalMode = cmd.ExecuteScalar()?.ToString();
        Assert.Equal("wal", journalMode, ignoreCase: true);

        cmd.CommandText = "PRAGMA busy_timeout;";
        var busyTimeout = cmd.ExecuteScalar()?.ToString();
        Assert.Equal("5000", busyTimeout);

        cmd.CommandText = "PRAGMA cache_size;";
        var cacheSize = cmd.ExecuteScalar()?.ToString();
        Assert.Equal("-100000", cacheSize);

    }
}
