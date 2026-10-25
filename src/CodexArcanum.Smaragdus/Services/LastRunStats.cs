namespace CodexArcanum.Smaragdus.Services;

public record LastRunStats(string StartedAt, string FinishedAt, int DurationMs, int FilesProcessed, int FilesSkipped, string Status);
