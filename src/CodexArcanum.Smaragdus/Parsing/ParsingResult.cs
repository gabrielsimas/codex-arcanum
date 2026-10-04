namespace CodexArcanum.Smaragdus.Parsing;

public sealed record ParsingResult(List<SymbolModel> Symbols,
    List<ParameterModel> Parameters,
    List<DependencyModel> Dependencies,
    string Status); // "Success", "Partial", "Unparseable";