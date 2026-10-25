namespace CodexArcanum.Smaragdus.Services;

public class StatsService
{
    private readonly SqliteConnection _connection;

    public StatsService(SqliteConnection connection)
    {
        _connection = connection;
    }

    public async Task<CompleteStats> GetCompleteStatsAsync()
    {
        return new CompleteStats(
            await GetOverallStatsAsync(),
            await GetHotspotsAsync(),
            await GetXmlDocHealthAsync(),
            await GetIsfetStatsAsync(),
            await GetLastRunStatsAsync(),
            await GetHotFilesAsync()
        );
    }

    private async Task<OverallStats> GetOverallStatsAsync()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT
                (SELECT COUNT(*) FROM Projects) AS TotalProjects,
                (SELECT COUNT(*) FROM Files) AS TotalFiles,
                (SELECT COUNT(*) FROM Symbols) AS TotalSymbols,
                (SELECT COALESCE(SUM(FileSize), 0) FROM Files) AS TotalSizeBytes;";

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new OverallStats(
                reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt64(3));
        }
        return new OverallStats(0, 0, 0, 0);
    }

    private async Task<List<HotspotStats>> GetHotspotsAsync()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT f.Path, COUNT(d.SourceId) AS IncomingCalls
            FROM Files f
            INNER JOIN Symbols s ON s.FileId = f.Id
            INNER JOIN Dependencies d ON s.Id = d.TargetId OR s.FullName = d.TargetName
            WHERE d.Relationship = 'Calls'
            GROUP BY f.Id
            ORDER BY IncomingCalls DESC
            LIMIT 5;";

        var results = new List<HotspotStats>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new HotspotStats(reader.GetString(0), reader.GetInt32(1)));
        }
        return results;
    }

    private async Task<List<XmlDocHealthStats>> GetXmlDocHealthAsync()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT
                Kind,
                COUNT(CASE WHEN XmlDoc IS NOT NULL AND XmlDoc != '' THEN 1 END) AS ComDoc,
                COUNT(CASE WHEN XmlDoc IS NULL OR XmlDoc = '' THEN 1 END) AS SemDoc,
                COUNT(*) AS Total,
                ROUND(100.0 * COUNT(CASE WHEN XmlDoc IS NOT NULL AND XmlDoc != '' THEN 1 END) / COUNT(*), 2) AS Percentual
            FROM Symbols
            WHERE Kind IN ('Class', 'Interface', 'Method')
            GROUP BY Kind;";

        var results = new List<XmlDocHealthStats>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new XmlDocHealthStats(
                reader.GetString(0), reader.GetInt32(1), reader.GetInt32(2),
                reader.GetInt32(3), reader.GetDouble(4)));
        }
        return results;
    }

    private async Task<List<IsfetStats>> GetIsfetStatsAsync()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT IndexStatus, COUNT(*) AS Quantidade FROM Files GROUP BY IndexStatus;";

        var results = new List<IsfetStats>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new IsfetStats(reader.GetString(0), reader.GetInt32(1)));
        }
        return results;
    }

    private async Task<LastRunStats?> GetLastRunStatsAsync()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT StartedAt, FinishedAt, DurationMs, FilesProcessed, FilesSkipped, Status
            FROM IndexRuns
            ORDER BY StartedAt DESC
            LIMIT 1;";

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new LastRunStats(
                reader.GetString(0), reader.GetString(1), reader.GetInt32(2),
                reader.GetInt32(3), reader.GetInt32(4), reader.GetString(5));
        }
        return null;
    }

    private async Task<List<HotFilesStats>> GetHotFilesAsync()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Path, ReindexCount
            FROM Files
            WHERE ReindexCount > 0
            ORDER BY ReindexCount DESC
            LIMIT 5;";

        var results = new List<HotFilesStats>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new HotFilesStats(reader.GetString(0), reader.GetInt32(1)));
        }
        return results;
    }
}
