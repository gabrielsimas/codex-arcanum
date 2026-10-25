namespace CodexArcanum.Thoth.Commands;

public class GitIgnoreFilter
{
    private static readonly string[] DefaultIgnores =
    {
        "[Bb]in/", "[Oo]bj/", ".vs/", ".vscode/", "node_modules/",
        "packages/", "*.user", "*.suo", ".git/"
    };

    public static List<string> GetIgnorePatterns(string targetDir)
    {
        var patterns = new List<string>(DefaultIgnores);
        var gitignorePath = Path.Combine(targetDir, ".gitignore");

        if (File.Exists(gitignorePath))
        {
            var lines = File.ReadAllLines(gitignorePath);
            patterns.AddRange(lines
                .Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("#"))
                .Select(l => l.Trim()));
        }

        return patterns;
    }

    public static bool IsIgnored(string relativePath, List<string> patterns)
    {
        var normalizedPath = relativePath.Replace("\\", "/");
        return patterns.Any(pattern =>
        {
            var normalizedPattern = pattern.Replace("\\", "/");
            if (normalizedPattern.EndsWith("/"))
            {
                return normalizedPath.StartsWith(normalizedPattern, StringComparison.OrdinalIgnoreCase) ||
                       normalizedPath.Contains("/" + normalizedPattern, StringComparison.OrdinalIgnoreCase);
            }
            if (normalizedPattern.StartsWith("*"))
            {
                return normalizedPath.EndsWith(normalizedPattern[1..], StringComparison.OrdinalIgnoreCase);
            }
            return normalizedPath.Equals(normalizedPattern, StringComparison.OrdinalIgnoreCase) ||
                   normalizedPath.EndsWith("/" + normalizedPattern, StringComparison.OrdinalIgnoreCase);
        });
    }
}
