namespace CodexArcanum.Smaragdus.Models;

public sealed record SymbolModel(int Id,
    int FileId,
    string Name,
    string Kind,
    string FullName,
    string? ReturnType,
    int LineStart,
    int LineEnd,
    string? Modifiers,
    string? Attributes,
    string? XmlDoc,
    bool IsDynamicEntrypoint);
