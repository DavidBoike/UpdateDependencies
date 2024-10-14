public record class OutdatedReport(OutdatedProject[] Projects);
public record class OutdatedProject(string Name, string FilePath, OutdatedTfm[] TargetFrameworks);
public record class OutdatedTfm(string Name, OutdatedDependency[] Dependencies);
public record class OutdatedDependency(string Name, string ResolvedVersion, string LatestVersion, string UpgradeSeverity);
