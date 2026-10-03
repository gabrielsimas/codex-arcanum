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

    public List<DependencyModel> GetOutgoing(int sourceId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, SourceId, TargetName, TargetId, Relationship, FilePath, Line
            FROM Dependencies
            WHERE SourceId = @sourceId;";
        cmd.Parameters.AddWithValue("@sourceId", sourceId);

        return ReadDependencies(cmd);
    }

    public List<DependencyModel> GetIncoming(int targetId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT d.Id, d.SourceId, d.TargetName, d.TargetId, d.Relationship, d.FilePath, d.Line
            FROM Dependencies d
            INNER JOIN Symbols s ON d.SourceId = s.Id
            WHERE d.TargetId = @targetId
               OR d.TargetName = (SELECT FullName FROM Symbols WHERE Id = @targetId);";
        cmd.Parameters.AddWithValue("@targetId", targetId);

        return ReadDependencies(cmd);
    }

    public List<DependencyModel> GetHierarchy(int sourceId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, SourceId, TargetName, TargetId, Relationship, FilePath, Line
            FROM Dependencies
            WHERE SourceId = @sourceId
              AND Relationship IN ('Inherits', 'Implements');";
        cmd.Parameters.AddWithValue("@sourceId", sourceId);

        return ReadDependencies(cmd);
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
