using System.Security.Cryptography;
using System.Text;

namespace CodexArcanum.Smaragdus.Services;

public class IntegrityService(FileRepository fileRepo)
{
    private readonly FileRepository _fileRepo = fileRepo;

    public async Task<FreshnessResult?> CheckFreshnessAsync(string relativePath, string solutionRoot, bool verifyHash)
    {
        var file = _fileRepo.GetByPath(relativePath);
        if (file == null) return null;

        var absolutePath = Path.Combine(solutionRoot, relativePath.Replace("/", Path.DirectorySeparatorChar.ToString()));
        if (!File.Exists(absolutePath))
        {
            return new FreshnessResult(relativePath, true, file.IndexedAt, null, null, file.FileSize, false, null);
        }

        var fileInfo = new FileInfo(absolutePath);
        var diskLastWrite = fileInfo.LastWriteTimeUtc.ToString("O");
        var diskSize = fileInfo.Length;

        var isStale = !string.Equals(file.LastWriteUtc, diskLastWrite, StringComparison.Ordinal) ||
                      file.FileSize != diskSize;

        string? hash = null;
        var hashVerified = false;

        if (verifyHash && !isStale)
        {
            var content = await File.ReadAllTextAsync(absolutePath);
            hash = ComputeSha256(content);
            hashVerified = true;
            isStale = !string.Equals(file.Hash, hash, StringComparison.Ordinal);
        }

        return new FreshnessResult(relativePath, isStale, file.IndexedAt, diskLastWrite, diskSize, file.FileSize, hashVerified, hash);
    }

    private static string ComputeSha256(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }
}
