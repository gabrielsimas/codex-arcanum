namespace CodexArcanum.Smaragdus.Models;

public sealed record ParameterModel(
    int Id,
    int SymbolId,
    string Name,
    string Type,
    bool IsOptional,
    int Position);