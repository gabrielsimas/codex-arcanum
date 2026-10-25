namespace CodexArcanum.Smaragdus.Services;

public record CompleteStats(
    OverallStats Overall,
    List<HotspotStats> Hotspots,
    List<XmlDocHealthStats> XmlDocHealth,
    List<IsfetStats> Isfet,
    LastRunStats? LastRun,
    List<HotFilesStats> HotFiles);