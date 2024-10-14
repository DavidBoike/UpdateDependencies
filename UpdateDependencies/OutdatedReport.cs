public record class OutdatedReport(OutdatedProject[] Projects);
public record class OutdatedProject(string Name, string FilePath, OutdatedTfm[] TargetFrameworks, bool IsCentralPackageMgmt = false);
public record class OutdatedTfm(string Name, OutdatedDependency[] Dependencies);
public record class OutdatedDependency(string Name, string ResolvedVersion, string LatestVersion, string UpgradeSeverity);
