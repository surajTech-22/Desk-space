using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace DeskSpace;

// Clear, opt-in manual check across Windows PCs. The original updater retains
// verified stable-release downloads, SHA-256 gate and supervisor installation.
// R42 private test releases are DISCOVERED here and opened for human review;
// never run unauthenticated EXEs or skip checksums.
internal static class DeskSpaceUpdateCheckV42
{
    private const string Repo="surajTech-22/Desk-space";
    private static readonly string Releases="https://github.com/"+Repo+"/releases";
    private static string? FindCli()
    {
        var names=new List<string>();
        var path=Environment.GetEnvironmentVariable("PATH")??"";
        foreach(var dir in path.Split(Path.PathSeparator,
                    StringSplitOptions.RemoveEmptyEntries))
            names.Add(Path.Combine(dir.Trim().Trim('"'),"gh.exe"));
        // Installed systems must not depend on the build PC's PATH.
        var app=AppContext.BaseDirectory;
        names.Add(Path.Combine(app,"gh.exe"));
        names.Add(Path.Combine(app,"Tools","gh.exe"));
        names.Add(Path.Combine(app,"GitHubCLI","gh.exe"));
        names.Add(Path.Combine(app,"Tools","GitHubCLI","gh.exe"));
        names.Add(Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.ProgramFiles),"GitHub CLI","gh.exe"));
        names.Add(Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.ProgramFilesX86),"GitHub CLI","gh.exe"));
        names.Add(Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData),
            "Programs","GitHub CLI","gh.exe"));
        var tools=Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData),
            "DeskSpace","BuildTools","GitHubCLI");
        if(Directory.Exists(tools))
        {
            try { names.AddRange(Directory.EnumerateFiles(tools,"gh.exe",
                    SearchOption.AllDirectories).Take(4)); }
            catch(IOException) {}
            catch(UnauthorizedAccessException) {}
        }
        return names.FirstOrDefault(File.Exists);
    }

    private static async Task<(int Exit,string Output)> Run(string executable,
        params string[] arguments)
    {
        var info=new ProcessStartInfo(executable){
            UseShellExecute=false,RedirectStandardOutput=true,
            RedirectStandardError=true,CreateNoWindow=true
        };
        foreach(var arg in arguments)info.ArgumentList.Add(arg);
        using var process=Process.Start(info)
            ?? throw new IOException("Cannot start GitHub CLI.");
        var output=process.StandardOutput.ReadToEndAsync();
        var errors=process.StandardError.ReadToEndAsync();
        using var timeout=new System.Threading.CancellationTokenSource(
            TimeSpan.FromSeconds(18));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch(OperationCanceledException){
            try { process.Kill(true); }catch(InvalidOperationException){}
            throw new TimeoutException("GitHub update check timed out.");
        }
        var stdout=await output;
        var stderr=await errors;
        return (process.ExitCode,process.ExitCode==0?stdout:stderr+"\n"+stdout);
    }

    private static void OpenReleases()
    {
        Process.Start(new ProcessStartInfo(Releases){UseShellExecute=true});
    }

    public static async Task CheckAsync(Window owner)
    {
        try
        {
            string? cli=FindCli();
            if(cli==null)
            {
                if(MessageBox.Show(owner,
                   "Check for Updates is enabled, but this PC has no "+
                   "GitHub CLI (gh.exe). DeskSpace releases are private. "+
                   "Open the GitHub Releases page in your signed-in browser?",
                   "DeskSpace update access",MessageBoxButton.YesNo,
                   MessageBoxImage.Information)==MessageBoxResult.Yes)
                    OpenReleases();
                return;
            }
            // The portable CLI installed by manual-build scripts is not on
            // other DeskSpace processes' PATH by default. Make it discoverable.
            var directory=Path.GetDirectoryName(cli)!;
            var search=Environment.GetEnvironmentVariable("PATH")??"";
            if(!search.Split(Path.PathSeparator).Any(x=>
                string.Equals(x.Trim('"'),directory,
                    StringComparison.OrdinalIgnoreCase)))
                Environment.SetEnvironmentVariable("PATH",
                    directory+Path.PathSeparator+search);
            var auth=await Run(cli,"auth","status","--hostname","github.com");
            if(auth.Exit!=0)
            {
                if(MessageBox.Show(owner,
                   "GitHub CLI is present, but this PC is not signed in. "+
                   "This is a private DeskSpace repository; no access "+
                   "or update data can be retrieved without authorization.\n\n"+
                   "Open the GitHub releases/sign-in page?",
                   "DeskSpace update access",MessageBoxButton.YesNo,
                   MessageBoxImage.Warning)==MessageBoxResult.Yes)
                    OpenReleases();
                return;
            }
            var result=await Run(cli,"release","list","--repo",Repo,
                "--json","tagName,isPrerelease,publishedAt","--limit","50");
            if(result.Exit!=0)
            {
                if(MessageBox.Show(owner,
                   "GitHub authentication succeeded, but the releases "+
                   "cannot be read. This PC may not have permission to the "+
                   "private repository.\n\nOpen its releases page?",
                   "DeskSpace update access",MessageBoxButton.YesNo,
                   MessageBoxImage.Warning)==MessageBoxResult.Yes)
                    OpenReleases();
                return;
            }
            var installed=Version.Parse(DeskSpaceUpdater.CurrentVersion);
            string? newerTag=null;
            Version? newerVersion=null;
            using(var json=JsonDocument.Parse(result.Output.Trim()))
            foreach(var release in json.RootElement.EnumerateArray())
            {
                if(!release.TryGetProperty("isPrerelease",out var pre)||
                   pre.ValueKind!=JsonValueKind.True)continue;
                var tag=release.GetProperty("tagName").GetString()??"";
                var match=Regex.Match(tag,
                    @"^r42-test-[0-9a-f]{12}-v(?<v>\d+\.\d+\.\d+)$",
                    RegexOptions.IgnoreCase);
                if(!match.Success ||
                   !Version.TryParse(match.Groups["v"].Value,out var ver)||
                   ver<=installed || newerVersion!=null && ver<=newerVersion)
                    continue;
                newerVersion=ver;newerTag=tag;
            }
            if(newerTag!=null)
            {
                if(MessageBox.Show(owner,
                   "A newer private DeskSpace R42 TEST release is available: "+
                   newerVersion+". Installed: "+installed+
                   ".\n\nOpen the verified test release in GitHub? "+
                   "Test builds require explicit review and will not "+
                   "replace a stable installation automatically.",
                   "DeskSpace tester update",MessageBoxButton.YesNo,
                   MessageBoxImage.Information)==MessageBoxResult.Yes)
                    Process.Start(new ProcessStartInfo(
                       Releases+"/tag/"+Uri.EscapeDataString(newerTag)){
                           UseShellExecute=true
                       });
                return;
            }
            // Normal updates continue through the original supervised,
            // SHA-256-verified stable-release updater, unchanged.
            await DeskSpaceUpdater.CheckAsync(owner);
        }
        catch(Exception ex)
        {
            MessageBox.Show(owner,
                "Check for Updates could not finish: "+ex.Message+
                "\n\nThis does not change the installed application. "+
                "Your system may be offline, the private repository may "+
                "require GitHub access, or the remote service may be unavailable.",
                "DeskSpace update check",MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
