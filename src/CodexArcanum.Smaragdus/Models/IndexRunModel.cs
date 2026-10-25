namespace CodexArcanum.Smaragdus.Models;

public sealed record IndexRunModel(
    int Id,
    string StartedAt,
    string FinishedAt,
    int DurationMs,
    int FilesProcessed,
    int FilesSkipped,
    string Status
);
