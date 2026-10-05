namespace CodexArcanum.Smaragdus.Storage;

public record OutgoingDependency(int SymbolId, string CalleeName, string Relationship, string FilePath, int Line);
