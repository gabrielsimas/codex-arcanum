namespace CodexArcanum.Smaragdus.Services;

public record StaleFile(
    string Path,
    string LastIndexed,
    string IndexedLastWrite,
    string DiskLastWrite,
    long IndexedSize,
    long DiskSize);
