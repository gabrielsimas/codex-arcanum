namespace CodexArcanum.Smaragdus.Storage;

public class SymbolRepository
{
    private readonly SqliteConnection _connection;

    public SymbolRepository(SqliteConnection connection)
    {
        _connection = connection;
    }

    public int Insert(SymbolModel symbol)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Symbols (FileId, Name, Kind, FullName, ReturnType, LineStart, LineEnd, Modifiers, Attributes, XmlDoc, IsDynamicEntrypoint)
            VALUES (@FileId, @Name, @Kind, @FullName, @ReturnType, @LineStart, @LineEnd, @Modifiers, @Attributes, @XmlDoc, @IsDynamicEntrypoint);
            SELECT last_insert_rowid();";

        cmd.Parameters.AddWithValue("@FileId", symbol.FileId);
        cmd.Parameters.AddWithValue("@Name", symbol.Name);
        cmd.Parameters.AddWithValue("@Kind", symbol.Kind);
        cmd.Parameters.AddWithValue("@FullName", symbol.FullName);
        cmd.Parameters.AddWithValue("@ReturnType", (object?)symbol.ReturnType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@LineStart", symbol.LineStart);
        cmd.Parameters.AddWithValue("@LineEnd", symbol.LineEnd);
        cmd.Parameters.AddWithValue("@Modifiers", (object?)symbol.Modifiers ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Attributes", (object?)symbol.Attributes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@XmlDoc", (object?)symbol.XmlDoc ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@IsDynamicEntrypoint", symbol.IsDynamicEntrypoint ? 1 : 0);

        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public List<SymbolSearchResultModel> Search(string query, string? type, int limit, int offset)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT
                s.Id AS symbolId,
                s.FullName,
                s.Kind,
                f.Path AS filePath,
                f.LastWriteUtc,
                f.FileSize,
                s.LineStart,
                s.LineEnd,
                s.XmlDoc AS xmlDocSummary
            FROM Symbols s
            INNER JOIN Files f ON s.FileId = f.Id
            WHERE (s.Name LIKE @query OR s.FullName LIKE @query)
              AND (@type IS NULL OR s.Kind = @type)
            ORDER BY s.FullName
            LIMIT @limit OFFSET @offset;";

        cmd.Parameters.AddWithValue("@query", $"%{query}%");
        cmd.Parameters.AddWithValue("@type", (object?)type ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@limit", limit);
        cmd.Parameters.AddWithValue("@offset", offset);

        var results = new List<SymbolSearchResultModel>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new SymbolSearchResultModel(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetInt64(5),
                reader.GetInt32(6),
                reader.GetInt32(7),
                reader.IsDBNull(8) ? null : reader.GetString(8)
            ));
        }
        return results;
    }

    public SymbolModel? GetById(int symbolId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, FileId, Name, Kind, FullName, ReturnType, LineStart, LineEnd, Modifiers, Attributes, XmlDoc, IsDynamicEntrypoint
            FROM Symbols WHERE Id = @symbolId;";
        cmd.Parameters.AddWithValue("@symbolId", symbolId);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        return new SymbolModel(
            reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3),
            reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.GetInt32(6), reader.GetInt32(7),
            reader.IsDBNull(8) ? null : reader.GetString(8),
            reader.IsDBNull(9) ? null : reader.GetString(9),
            reader.IsDBNull(10) ? null : reader.GetString(10),
            reader.GetInt32(11) == 1
        );
    }
}
