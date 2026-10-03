namespace CodexArcanum.Smaragdus.Tests.Storage;

public class SqliteConnectionFactoryTests
{
    [Fact]
    public void CreateConnection_ShouldConfigureMaatPragmas()
    {
        // Arrange
        var connectionString = "Data Source=:memory:";

        // Act
        using var connection = new SqliteConnectionFactory(connectionString).CreateConnection();
        connection.Open();

        // Assert (Red: Falhará até a implmentação do Factory)
        Assert.NotNull(connection);
        //TODO: Validar PRAGMA journal_mode = WAL, busy_timeout=5000 e cache_size=10000

    }
}
