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
            this.centralPkgMgmtPath = cpmPath;
        }
    }
	
    public bool Update(bool tryRemoveTransitiveRefs = true)
    {
        var updateResult = UpdateInternal(true);
        if (!tryRemoveTransitiveRefs)
        {
            return updateResult;
        }
		
        Console.WriteLine("Attempting to remove unnecessary transitive dependencies");
        var report = apps.RunOutdated(0);
		
        return updateResult;
    }
	
    public bool UpdateInternal(bool streamOutput)
    {
        for (var transDepth = 0; transDepth <= 10; transDepth++)
        {
            if (apps.TryBuild(streamOutput))
            {
                return true;
            }
			
            var report = apps.RunOutdated(transDepth);
            var changesMade = UpdateFor(report, transDepth);
			
            if (changesMade == 0)
            {
                Console.WriteLine("No changes could be made at this transitive depth");
            }
        }
		
        if (apps.TryBuild(streamOutput))
        {
            return true;
        }
		
        Console.WriteLine("Reached max depth without a successful build");
        return false;
    }

    int UpdateFor(OutdatedReport report, int depth)
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
			
            return UpdateProject(cpmProject, depth);
        }
        else
        {
            var changesMade = 0;
            foreach (var project in report.Projects)
            {
                changesMade += UpdateProject(project, depth);
            }
            return changesMade;
        }
    }

    int UpdateProject(OutdatedProject project, int depth)
    {
        Console.WriteLine($"Updating project {project.Name} for transitive depth {depth}");

        var elements = new ProjectFile(project.FilePath);
		
        int changesMade = 0;
		
        foreach(var fw in project.TargetFrameworks)
        {
            foreach (var dep in fw.Dependencies.Where(d => ShouldUpdateDependency(d.Name)))
            {
                if (elements.TryUpdate(dep.Name, dep.LatestVersion))
                {
                    Console.WriteLine($"  - Updated {dep.Name} to {dep.LatestVersion}");
                    changesMade++;
                }
                else
                {
                    elements.AddTransitiveReference(dep.Name, dep.LatestVersion);
                    Console.WriteLine($"  - Added {dep.Name} {dep.LatestVersion} as a pinned transitive dependency");
                    changesMade++;
                }
            }
        }
		
        if (changesMade > 0)
        {
            elements.Save();
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