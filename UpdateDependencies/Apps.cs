using System.Diagnostics;
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
		    var restoreReturnCode = Run("dotnet", "restore src", streamOutput);
		    if (restoreReturnCode != 0)
		    {
			    Console.WriteLine("dotnet restore failed");
			    return false;
		    }
		
		    Thread.Sleep(1000);
		
		    // Can't use --no-restore or NuGet vulnerability warnings won't break build
		    var retCode = Run("dotnet", "build src -graph --configuration Release", streamOutput);
		    if (retCode == 0)
		    {
			    Console.WriteLine("Build successful");
			    return true;
		    }
		
		    Console.WriteLine("Build currently unsuccessful");
		    return false;
	    }
    
    	int Run(string cmd, string args, bool streamStdout = false)
    	{
    		var psi = new ProcessStartInfo(cmd, args)
    		{
    			WorkingDirectory = workingDirectory,
    			UseShellExecute = false,
    			RedirectStandardOutput = streamStdout,
    			RedirectStandardError = streamStdout,
    			CreateNoWindow = true
    		};
    
    		var p = Process.Start(psi);
    		if (streamStdout)
    		{
    			p.OutputDataReceived += (s, e) => Console.WriteLine(e.Data);
    			p.ErrorDataReceived += (s, e) => Console.Error.WriteLine(e.Data);
    			p.BeginOutputReadLine();
    			p.BeginErrorReadLine();
    		}
    		p.WaitForExit();
    		return p.ExitCode;
    	}
}