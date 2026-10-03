namespace CodexArcanum.Smaragdus.Storage;

public class ParameterRepository
{
    private readonly SqliteConnection _connection;

    public ParameterRepository(SqliteConnection connection)
    {
        _connection = connection;
    }

    public int Insert(ParameterModel parameter)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Parameters (SymbolId, Name, Type, IsOptional, Position)
            VALUES (@SymbolId, @Name, @Type, @IsOptional, @Position);
            SELECT last_insert_rowid();";

        cmd.Parameters.AddWithValue("@SymbolId", parameter.SymbolId);
        cmd.Parameters.AddWithValue("@Name", parameter.Name);
        cmd.Parameters.AddWithValue("@Type", parameter.Type);
        cmd.Parameters.AddWithValue("@IsOptional", parameter.IsOptional ? 1 : 0);
        cmd.Parameters.AddWithValue("@Position", parameter.Position);

        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public List<ParameterModel> GetBySymbolId(int symbolId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, SymbolId, Name, Type, IsOptional, Position
            FROM Parameters
            WHERE SymbolId = @symbolId
            ORDER BY Position ASC;";
        cmd.Parameters.AddWithValue("@symbolId", symbolId);

        var results = new List<ParameterModel>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new ParameterModel(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetInt32(4) == 1,
                reader.GetInt32(5)));
        }
        return results;
    }

    public int DeleteBySymbolId(int symbolId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Parameters WHERE SymbolId = @symbolId;";
        cmd.Parameters.AddWithValue("@symbolId", symbolId);
        return cmd.ExecuteNonQuery();
    }
}
