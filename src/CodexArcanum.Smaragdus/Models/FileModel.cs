namespace CodexArcanum.Smaragdus.Models;

public sealed record FileModel(int Id,
    int ProjectId,
    string Path,
    string LastWriteUtc,
    long FileSize,
    string Hash,
    string IndexedAt,
    int ReindexCount,
    string IndexStatus = "Success");