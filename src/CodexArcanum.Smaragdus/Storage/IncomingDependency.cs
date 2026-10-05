namespace CodexArcanum.Smaragdus.Storage;

public record IncomingDependency(int SymbolId, string CallerName, string Relationship, string FilePath, int Line);
