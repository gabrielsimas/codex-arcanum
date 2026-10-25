namespace CodexArcanum.Smaragdus.Models;

public sealed record DependencyModel(
    int Id,
    int SourceId,
    string TargetName,
    int? TargetId,
    string Relationship,
    string FilePath,
    int Line
);
