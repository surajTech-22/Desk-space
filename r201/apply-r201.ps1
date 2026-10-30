$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r201/DeskSpaceProfilePortabilityV201.cs' -Destination "$root/DeskSpaceProfilePortabilityV201.cs" -Force
$lf=[string][char]10;$crlf=[string][char]13+$lf
function Patch([string]$path,[string]$before,[string]$after,[string]$reason)
{
 $before=$before.Replace($crlf,$lf);$after=$after.Replace($crlf,$lf)
 $s=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
 $n=([regex]::Matches($s,[regex]::Escape($before))).Count
 if($n -ne 1){throw ("R201 "+$reason+" expected one anchor, got "+$n)}
 [IO.File]::WriteAllText($path,$s.Replace($before,$after),
     [Text.UTF8Encoding]::new($false))
}
$insert=@'
    // Import profiles without applying a single setting. Existing profile
    // names are retained, and the same data lock as Save() is used.
    internal static int ImportNewProfilesV201(
        IReadOnlyList<DeskSpaceProfileV85> incoming)
    {
        if(incoming==null || incoming.Count>24)
            throw new FormatException("Invalid profile import count.");
        foreach(var profile in incoming)
            DeskSpaceProfilePortabilityV201.Validate(profile);
        if(incoming.Select(x=>x.Name).Distinct(
            StringComparer.OrdinalIgnoreCase).Count()!=incoming.Count)
            throw new FormatException("Duplicate imported profile names.");
        lock(Gate)
        {
            var current=Load().ToList();
            var known=new HashSet<string>(current.Select(x=>x.Name),
                StringComparer.OrdinalIgnoreCase);
            var fresh=incoming.Where(x=>!known.Contains(x.Name)).ToList();
            if(fresh.Count==0)return 0;
            if(current.Count+fresh.Count>24)
                throw new FormatException("Profile limit is 24; no changes made.");
            Directory.CreateDirectory(WorkspaceStore.Root);
            var temp=Filename+"."+Guid.NewGuid().ToString("N")+".partial";
            try
            {
                File.WriteAllText(temp,
                    JsonSerializer.Serialize(current.Concat(fresh).ToList()));
                // Keep a separate recovery copy of the previous profile
                // database. This never touches Notes, Excel or secrets.
                if(File.Exists(Filename))
                {
                    var backup=Filename+".before-import-"+
                        DateTime.UtcNow.ToString("yyyyMMddHHmmss")+"-"+
                        Guid.NewGuid().ToString("N")+".bak";
                    File.Copy(Filename,backup,false);
                }
                File.Move(temp,Filename,true);
                return fresh.Count;
            }
            finally{if(File.Exists(temp))File.Delete(temp);}
        }
    }

'@
Patch "$root/DeskSpaceWorkspaceProfilesV85.cs" @'
    internal static DeskSpaceProfileV85 Capture(string name,WidgetSettingsV3 source)
'@ ($insert+'    internal static DeskSpaceProfileV85 Capture(string name,WidgetSettingsV3 source)') 'atomic profile import under saved-profile lock'
Patch "$root/DeskSpaceFeatureHubV199.cs" @'
        Add("Window layout profiles",
            "Capture and restore layouts of already-running application windows.",
            ()=>DeskSpaceWindowLayoutsV179.Show(this));
'@ @'
        Add("Window layout profiles",
            "Capture and restore layouts of already-running application windows.",
            ()=>DeskSpaceWindowLayoutsV179.Show(this));
        Add("Export widget profiles",
            "Create a profile-only JSON file; no notes, credentials or Excel data.",
            ()=>DeskSpaceProfilePortabilityV201.ExportWithDialog(this));
        Add("Import widget profiles",
            "Preview a validated JSON import; existing names are not overwritten.",
            ()=>DeskSpaceProfilePortabilityV201.ImportWithDialog(this));
'@ 'visible Workspace profile import/export actions'
Write-Host 'R201: explicit profile-only import/export, no auto-apply, same-lock atomic merge, saved profile recovery copy.'
