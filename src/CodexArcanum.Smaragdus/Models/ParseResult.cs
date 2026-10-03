namespace CodexArcanum.Smaragdus.Models;

public sealed record ParseResult(List<SymbolModel> Symbols,
    List<ParameterModel> Parameters,
    List<DependencyModel> Dependencies,
    string Status); // "Success", "Partial", "Unparseable";