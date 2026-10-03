namespace CodexArcanum.Smaragdus.Tests.Storage;

public class DatabaseInitializerTests
{
    [Fact]
    public void InitializeDatabase_ShouldCreateAllTables()
    {
        // Arrange
        var connectionString = "Data Source=:memory:";
        using var connection = SqliteConnectionFactory.CreateConnection(connectionString);

        // Act
        DatabaseInitializer.Initialize(connection);

        using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
            SELECT name FROM sqlite_master
            WHERE type='table' AND name IN ('Projects', 'Files', 'Symbols', 'Parameters', 'Dependencies', 'IndexRuns')
            ORDER BY name;";

        var tables = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            tables.Add(reader.GetString(0));
        }

        Assert.Contains("Projects", tables);
        Assert.Contains("Files", tables);
        Assert.Contains("Symbols", tables);
        Assert.Contains("Parameters", tables);
        Assert.Contains("Dependencies", tables);
        Assert.Contains("IndexRuns", tables);
    }

    [Fact]
    public void InitializeDatabase_ShouldCreateViews()
    {
        // Arrange
        var connectionString = "Data Source=:memory:";
        using var connection = SqliteConnectionFactory.CreateConnection(connectionString);

        // Act
        DatabaseInitializer.Initialize(connection);

        // Assert - Verifica se as views auxiliares foram criadas
        using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
            SELECT name FROM sqlite_master
            WHERE type='view' AND name IN ('vw_symbol_hierarchy', 'vw_symbol_dependencies')
            ORDER BY name;";

        var views = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            views.Add(reader.GetString(0));
        }

        Assert.Contains("vw_symbol_hierarchy", views);
        Assert.Contains("vw_symbol_dependencies", views);
    }
}
