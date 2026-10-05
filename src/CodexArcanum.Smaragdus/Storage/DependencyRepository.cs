namespace CodexArcanum.Smaragdus.Storage;

public class DependencyRepository
{
    private readonly SqliteConnection _connection;

    public DependencyRepository(SqliteConnection connection)
    {
        _connection = connection;
    }

    public int Insert(DependencyModel dependency)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Dependencies (SourceId, TargetName, TargetId, Relationship, FilePath, Line)
            VALUES (@SourceId, @TargetName, @TargetId, @Relationship, @FilePath, @Line);
            SELECT last_insert_rowid();";

        cmd.Parameters.AddWithValue("@SourceId", dependency.SourceId);
        cmd.Parameters.AddWithValue("@TargetName", dependency.TargetName);
        cmd.Parameters.AddWithValue("@TargetId", (object?)dependency.TargetId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Relationship", dependency.Relationship);
        cmd.Parameters.AddWithValue("@FilePath", dependency.FilePath);
        cmd.Parameters.AddWithValue("@Line", dependency.Line);

        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public List<OutgoingDependency> GetOutgoing(int sourceId)
    {
        using var cmd = _connection.CreateCommand();
        // Query exata da Spec 3.4 (com COALESCE para evitar null no C# e Relationship incluído)
        cmd.CommandText = @"
        SELECT
            COALESCE(d.TargetId, 0) AS SymbolId,
            d.TargetName AS CalleeName,
            d.Relationship,
            d.FilePath,
            d.Line
        FROM Dependencies d
        WHERE d.SourceId = @sourceId;";
        cmd.Parameters.AddWithValue("@sourceId", sourceId);

        var results = new List<OutgoingDependency>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new OutgoingDependency(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetInt32(4)
            ));
        }
        return results;
    }

    public List<IncomingDependency> GetIncoming(int targetId)
    {
        using var cmd = _connection.CreateCommand();
        // Query exata da Spec 3.4 para Incoming
        cmd.CommandText = @"
        SELECT
            d.SourceId AS SymbolId,
            s.FullName AS CallerName,
            d.Relationship,
            d.FilePath,
            d.Line
        FROM Dependencies d
        INNER JOIN Symbols s ON d.SourceId = s.Id
        WHERE d.TargetId = @targetId
           OR d.TargetName = (SELECT FullName FROM Symbols WHERE Id = @targetId);";
        cmd.Parameters.AddWithValue("@targetId", targetId);

        var results = new List<IncomingDependency>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new IncomingDependency(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetInt32(4)
            ));
        }
        return results;
    }

    public HierarchyData GetHierarchy(int symbolId)
    {
        var baseAndInterfaces = new List<HierarchyRelation>();
        var derivedClasses = new List<HierarchyRelation>();

        // Query 1: Base Class e Interfaces (quem eu herdo/implemento)
        using var cmd1 = _connection.CreateCommand();
        cmd1.CommandText = @"
        SELECT
            COALESCE(TargetId, 0) AS SymbolId,
            TargetName AS Name,
            Relationship
        FROM Dependencies
        WHERE SourceId = @symbolId
          AND Relationship IN ('Inherits', 'Implements');";
        cmd1.Parameters.AddWithValue("@symbolId", symbolId);

        using var reader1 = cmd1.ExecuteReader();
        while (reader1.Read())
        {
            baseAndInterfaces.Add(new HierarchyRelation(
                reader1.GetInt32(0),
                reader1.GetString(1),
                reader1.GetString(2)
            ));
        }

        // Query 2: Derived Classes (quem herda de mim)
        using var cmd2 = _connection.CreateCommand();
        cmd2.CommandText = @"
        SELECT
            d.SourceId AS SymbolId,
            s.Name
        FROM Dependencies d
        INNER JOIN Symbols s ON d.SourceId = s.Id
        WHERE (d.TargetId = @symbolId OR d.TargetName = (SELECT FullName FROM Symbols WHERE Id = @symbolId))
          AND d.Relationship = 'Inherits';";
        cmd2.Parameters.AddWithValue("@symbolId", symbolId);

        using var reader2 = cmd2.ExecuteReader();
        while (reader2.Read())
        {
            derivedClasses.Add(new HierarchyRelation(
                reader2.GetInt32(0),
                reader2.GetString(1),
                "Inherits" // Explicitamente definido, pois a query já filtra por isso
            ));
        }

        return new HierarchyData(baseAndInterfaces, derivedClasses);
    }

    private List<DependencyModel> ReadDependencies(SqliteCommand cmd)
    {
        var results = new List<DependencyModel>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new DependencyModel(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetInt32(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetInt32(6)));
        }
        return results;
    }
}
