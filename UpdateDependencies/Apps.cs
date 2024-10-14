using System.Diagnostics;
using System.Text;
using System.Text.Json;

public class AppRunner(string workingDirectory)
{
    public OutdatedReport RunOutdated(int transitiveDepth)
    {
    	Console.WriteLine($"Running dotnet-outdated with transitive depth of {transitiveDepth}");
    	
    	var tmpPath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());

    	try
    	{
    		var args = $"src --output-format Json --output {tmpPath} --transitive --transitive-depth {transitiveDepth} --version-lock Minor";
    		Run("dotnet-outdated", args);

    		var json = File.ReadAllText(tmpPath);
    		var report = JsonSerializer.Deserialize<OutdatedReport>(json);
    		return report;
    	}
    	finally
    	{
    		if (File.Exists(tmpPath))
    		{
    			File.Delete(tmpPath);
    		}
    	}
    }
    
    public bool TryBuild(bool streamOutput = false)
    {
	    // Must force re-restore or weird things happen
	    var res = Run("dotnet", "restore src", streamOutput);
	    if (res.ExitCode != 0)
	    {
		    Console.WriteLine("dotnet restore failed");
		    return false;
	    }
	
	    Thread.Sleep(1000);
	
	    // Can't use --no-restore or NuGet vulnerability warnings won't break build
	    res = Run("dotnet", "build src -graph --configuration Release", streamOutput);
	    if (res.ExitCode == 0)
	    {
		    Console.WriteLine("Build successful");
		    return true;
	    }
	
	    Console.WriteLine("Build currently unsuccessful");
	    return false;
    }

    Result Run(string cmd, string args, bool streamStdout = false)
    {
    	var psi = new ProcessStartInfo(cmd, args)
    	{
    		WorkingDirectory = workingDirectory,
    		UseShellExecute = false,
    		RedirectStandardOutput = true,
    		RedirectStandardError = true,
    		CreateNoWindow = true
    	};

	    var outBuilder = new StringBuilder();
	    var errBuilder = new StringBuilder();

    	var p = Process.Start(psi);
	    p.OutputDataReceived += (s, e) => outBuilder.AppendLine(e.Data);
	    p.ErrorDataReceived += (s, e) => outBuilder.AppendLine(e.Data);
	    
    	if (streamStdout)
    	{
    		p.OutputDataReceived += (s, e) => Console.WriteLine(e.Data);
    		p.ErrorDataReceived += (s, e) => Console.Error.WriteLine(e.Data);
    	}
    	
	    p.BeginOutputReadLine();
	    p.BeginErrorReadLine();
	    p.WaitForExit();
    	return new Result(p.ExitCode, outBuilder.ToString(), errBuilder.ToString());
    }
    
    public record Result(int ExitCode, string Stdout, string Stderr);
}