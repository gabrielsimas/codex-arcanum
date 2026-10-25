namespace CodexArcanum.Smaragdus.Tests.Storage;

public class SqliteConnectionFactoryTests
{
    [Fact]
    public void CreateConnection_ShouldConfigureMaatPragmas()
    {
        // Arrange
        var tempDbFile = $"{Guid.NewGuid()}.db";
        var connectionString = $"Data Source={tempDbFile}";

        try
        {
            // Act
            using (var connection = SqliteConnectionFactory.CreateConnection(connectionString))
            {
                // Assert
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
                Assert.Equal("-10000", cacheSize);
            }

        }
        finally
        {
            SqliteConnection.ClearAllPools();

            // Cleanup: Remove o arquivo de banco de dados e seus auxiliares de WAL
            if (File.Exists(tempDbFile)) File.Delete(tempDbFile);
            if (File.Exists($"{tempDbFile}-wal")) File.Delete($"{tempDbFile}-wal");
            if (File.Exists($"{tempDbFile}-shm")) File.Delete($"{tempDbFile}-shm");
        }
    }
}
