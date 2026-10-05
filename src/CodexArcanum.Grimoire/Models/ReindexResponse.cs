namespace CodexArcanum.Grimoire.Models;

public record ReindexResponse(List<ReindexSuccess> Success, List<ReindexFailure> Failed);
