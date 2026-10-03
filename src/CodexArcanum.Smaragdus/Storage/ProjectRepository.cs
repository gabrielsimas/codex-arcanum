namespace CodexArcanum.Smaragdus.Storage;

public class ProjectRepository(SqliteConnection connection)
{
    private readonly SqliteConnection _connection = connection;

    public int Insert(ProjectModel project)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Projects (Name, Path, IndexedAt)
            VALUES (@Name, @Path, @IndexedAt);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@Name", project.Name);
        cmd.Parameters.AddWithValue("@Path", project.Path);
        cmd.Parameters.AddWithValue("@IndexedAt", project.IndexedAt);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public ProjectModel? GetByPath(string path)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Path, IndexedAt FROM Projects WHERE Path = @Path;";
        cmd.Parameters.AddWithValue("@Path", path);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return new ProjectModel(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3));
    }

    public List<ProjectModel> GetAll()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Path, IndexedAt FROM Projects ORDER BY Name;";
        using var reader = cmd.ExecuteReader();
        var projects = new List<ProjectModel>();
        while (reader.Read())
        {
            projects.Add(new ProjectModel(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3)));
        }
        return projects;
    }
}
