namespace CodexArcanum.Smaragdus.Storage;

public record HierarchyData(
    List<HierarchyRelation> BaseAndInterfaces,
    List<HierarchyRelation> DerivedClasses
);
