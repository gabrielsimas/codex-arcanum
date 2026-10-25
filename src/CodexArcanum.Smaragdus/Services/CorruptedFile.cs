namespace CodexArcanum.Smaragdus.Services;

public record CorruptedFile(
    string Path,
    string LastIndexed,
    long Size,
    string IndexedHash,
    string DiskHash);
