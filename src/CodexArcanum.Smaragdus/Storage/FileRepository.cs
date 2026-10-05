namespace CodexArcanum.Smaragdus.Storage;

public class FileRepository(SqliteConnection connection)
{
    private readonly SqliteConnection _connection = connection;

    public int Insert(FileModel file)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Files (ProjectId, Path, LastWriteUtc, FileSize, Hash, IndexedAt, ReindexCount, IndexStatus)
            VALUES (@ProjectId, @Path, @LastWriteUtc, @FileSize, @Hash, @IndexedAt, @ReindexCount, @IndexStatus);
            SELECT last_insert_rowid();";

        cmd.Parameters.AddWithValue("@ProjectId", file.ProjectId);
        cmd.Parameters.AddWithValue("@Path", file.Path);
        cmd.Parameters.AddWithValue("@LastWriteUtc", file.LastWriteUtc);
        cmd.Parameters.AddWithValue("@FileSize", file.FileSize);
        cmd.Parameters.AddWithValue("@Hash", file.Hash);
        cmd.Parameters.AddWithValue("@IndexedAt", file.IndexedAt);
        cmd.Parameters.AddWithValue("@ReindexCount", file.ReindexCount);
        cmd.Parameters.AddWithValue("@IndexStatus", file.IndexStatus);

        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public FileModel? GetByPath(string path)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, ProjectId, Path, LastWriteUtc, FileSize, Hash, IndexedAt, ReindexCount, IndexStatus
            FROM Files WHERE Path = @Path;";
        cmd.Parameters.AddWithValue("@Path", path);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        return new FileModel(
            reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3),
            reader.GetInt64(4), reader.GetString(5), reader.GetString(6), reader.GetInt32(7), reader.GetString(8));
    }

    public int DeleteByPath(string path)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Files WHERE Path = @Path;";
        cmd.Parameters.AddWithValue("@Path", path);
        return cmd.ExecuteNonQuery();
    }

    public void UpdateIndexStatus(int fileId, string status)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "UPDATE Files SET IndexStatus = @status WHERE Id = @id;";
        cmd.Parameters.AddWithValue("@status", status);
        cmd.Parameters.AddWithValue("@id", fileId);
        cmd.ExecuteNonQuery();
    }

    public List<FileModel> GetAll()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, ProjectId, Path, LastWriteUtc, FileSize, Hash, IndexedAt, ReindexCount, IndexStatus
            FROM Files;";

        using var reader = cmd.ExecuteReader();
        var files = new List<FileModel>();
        while (reader.Read())
        {
            files.Add(new FileModel(
                reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3),
                reader.GetInt64(4), reader.GetString(5), reader.GetString(6), reader.GetInt32(7), reader.GetString(8)));
        }
        return files;
    }

    public FileModel? GetById(int id)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, ProjectId, Path, LastWriteUtc, FileSize, Hash, IndexedAt, ReindexCount, IndexStatus
            FROM Files WHERE Id = @id;";
        cmd.Parameters.AddWithValue("@id", id);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        return new FileModel(
            reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3),
            reader.GetInt64(4), reader.GetString(5), reader.GetString(6), reader.GetInt32(7), reader.GetString(8));
    }
}
