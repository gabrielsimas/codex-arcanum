namespace CodexArcanum.Grimoire.Middleware;

public class PathValidator
{
    public static (bool IsValid, string? ErrorMessage) Validate(string path, string solutionRoot)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return (false, "Caminho não pode ser vazio.");
        }

        // Rejeita caminho absoluto
        if (Path.IsPathRooted(path))
        {
            return (false, $"Caminho absoluto não é permitido. Use caminho relativo à raiz da solução.");
        }

        // Rejeita path traversal (..)
        var normalized = path.Replace('\\', '/');
        if (normalized.Contains("../") || normalized.Contains("/..") || normalized == "..")
        {
            return (false, "Path traversal (..) não é permitido por motivos de segurança.");
        }

        // Valida que o caminho resolvido está dentro do solutionRoot
        try
        {
            var fullPath = Path.GetFullPath(Path.Combine(solutionRoot, normalized));
            var fullRoot = Path.GetFullPath(solutionRoot);

            if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            {
                return (false, "Caminho resolve para fora do diretório da solução.");
            }
        }
        catch
        {
            return (false, "Caminho inválido.");
        }

        return (true, null);
    }

    public static string Resolve(string relativePath, string solutionRoot)
    {
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(solutionRoot, normalized));
    }
}
