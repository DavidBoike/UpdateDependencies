using System.Text.RegularExpressions;
using System.Xml.Linq;

public class Updater
{
    readonly AppRunner apps;
    readonly string centralPkgMgmtPath;
	
    public Updater(string workingDirectory)
    {
        apps = new AppRunner(workingDirectory);
        var cpmPath = Path.Combine(workingDirectory, "src", "Directory.Packages.props");
        if (File.Exists(cpmPath))
        {
            centralPkgMgmtPath = cpmPath;
        }
    }
	
    public bool Update()
    {
        var report = apps.RunOutdated(20);
        
        int changesMade = 1;
        string[] buildLogs = null;
        
        while (changesMade > 0)
        {
            if (apps.TryBuild(out buildLogs))
            {
                return true;
            }
			
            changesMade = UpdateFor(report, buildLogs);
			
            if (changesMade == 0)
            {
                break;
            }
        }
		
        if (apps.TryBuild(out buildLogs))
        {
            return true;
        }
		
        Console.WriteLine("Reached max depth without a successful build");
        return false;
    }

    int UpdateFor(OutdatedReport report, string[] buildLogs)
    {
        if (centralPkgMgmtPath is not null)
        {
            var allDeps = report.Projects.SelectMany(p => p.TargetFrameworks)
                .SelectMany(fw => fw.Dependencies)
                .GroupBy(dep => dep.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToArray();
			
            var anyFramework = new OutdatedTfm("any", allDeps);
				
            var cpmProject = new OutdatedProject("Directory.Packages.props", centralPkgMgmtPath, [anyFramework], true);
			
            return UpdateProject(cpmProject, buildLogs);
        }
        else
        {
            var changesMade = 0;
            foreach (var project in report.Projects)
            {
                changesMade += UpdateProject(project, buildLogs);
            }
            return changesMade;
        }
    }

    int UpdateProject(OutdatedProject project, string[] buildLogs)
    {
        Console.WriteLine($"Updating project {project.Name}");

        var file = new ProjectFile(project.FilePath);
		
        int changesMade = 0;
		
        foreach(var fw in project.TargetFrameworks)
        {
            foreach (var dep in fw.Dependencies.Where(d => ShouldUpdateDependency(d.Name)))
            {
                bool shouldUpdate = buildLogs.Any(line =>
                {
                    if (project.IsCentralPackageMgmt || line.Contains(Path.GetFileName(project.FilePath), OIC))
                    {
                        if (line.Contains(dep.Name, OIC))
                        {
                            var pattern = $@"[' ]{dep.Name}[' ]";
                            if (Regex.IsMatch(line, pattern, RegexOptions.IgnoreCase))
                            {
                                return true;
                            }
                        }
                    }

                    return false;
                });

                if (shouldUpdate)
                {
                    if (file.TryUpdate(dep.Name, dep.LatestVersion))
                    {
                        Console.WriteLine($"  - Updated {dep.Name} to {dep.LatestVersion} in {project.Name}");
                        changesMade++;
                    }
                    else
                    {
                        file.AddTransitiveReference(dep.Name, dep.LatestVersion);
                        Console.WriteLine(
                            $"  - Added {dep.Name} {dep.LatestVersion} as a pinned transitive dependency in {project.Name}");
                        changesMade++;
                    }
                }
            }
        }
		
        if (changesMade > 0)
        {
            file.Save();
        }
		
        return changesMade;
    }
	
    const StringComparison OIC = StringComparison.OrdinalIgnoreCase;
	
    static bool ShouldUpdateDependency(string name)
    {
        if (DoNotUpgradePrefixes.Any(prefix => name.StartsWith(prefix, OIC)))
        {
            return false;
        }
		
        return DoUpgradePrefixes.Any(prefix => name.StartsWith(prefix, OIC));
    }
	
    static string[] DoNotUpgradePrefixes = [
        "Microsoft.NETCore",
        "runtime"
    ];
	
    static string[] DoUpgradePrefixes = [
        "System.",
        "Microsoft.",
        "Azure.Identity"
    ];
	

	




}