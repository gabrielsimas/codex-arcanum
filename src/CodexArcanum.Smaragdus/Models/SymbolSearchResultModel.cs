namespace CodexArcanum.Smaragdus.Models;

public sealed record SymbolSearchResultModel(int SymbolId,
    string FullName,
    string Kind,
    string FilePath,
    string LastWriteUtc,
    long FileSize,
    int LineStart,
    int LineEnd,
    string? XmlDocSummary);
