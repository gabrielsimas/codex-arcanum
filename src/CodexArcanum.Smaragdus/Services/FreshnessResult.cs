namespace CodexArcanum.Smaragdus.Services;

public sealed record FreshnessResult(
    string Path,
    bool IsStale,
    string LastIndexed,
    string? DiskLastWrite,
    long? DiskSize,
    long IndexedSize,
    bool HashVerified,
    string? Hash
);
