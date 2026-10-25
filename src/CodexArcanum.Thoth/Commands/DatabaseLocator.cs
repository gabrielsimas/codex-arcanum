namespace CodexArcanum.Thoth.Commands;

public class DatabaseLocator
{
    public static (string DbPath, string SolutionName)? Locate(string? caminho)
    {
        var targetDir = Directory.GetCurrentDirectory();

        if (!string.IsNullOrEmpty(caminho))
        {
            if (Directory.Exists(caminho)) targetDir = Path.GetFullPath(caminho);
            else if (File.Exists(caminho)) targetDir = Path.GetDirectoryName(Path.GetFullPath(caminho))!;
            else return null;
        }

        var slnFile = Directory.GetFiles(targetDir, "*.sln").FirstOrDefault()
                   ?? Directory.GetFiles(targetDir, "*.slnx").FirstOrDefault();
        var csprojFile = Directory.GetFiles(targetDir, "*.csproj").FirstOrDefault();

        string solutionName;
        if (slnFile != null) solutionName = Path.GetFileNameWithoutExtension(slnFile);
        else if (csprojFile != null) solutionName = Path.GetFileNameWithoutExtension(csprojFile);
        else return null;

        var dbPath = Path.Combine(targetDir, ".codex-arcanum", "db", $"{solutionName}.db");
        if (!File.Exists(dbPath)) return null;

        return (dbPath, solutionName);
    }
}
