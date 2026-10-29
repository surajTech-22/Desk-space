$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='DeskSpace_Windows_R1/DeskSpace/DeskSpaceWindowLayoutsV179.cs'
$nl=[string][char]10
$crlf=[string][char]13+$nl
$s=[IO.File]::ReadAllText($p).Replace($crlf,$nl)
function Swap([string]$before,[string]$after,[string]$name){
    $before=$before.Replace($crlf,$nl);$after=$after.Replace($crlf,$nl)
    $count=([regex]::Matches($script:s,[regex]::Escape($before))).Count
    if($count -ne 1){throw "R181 $name anchor mismatch: $count"}
    $script:s=$script:s.Replace($before,$after)
}
Swap @'
        return profile.Windows.All(e=>
            e!=null && e.Process.Length is >=1 and <=128 &&
            e.Process.All(c=>char.IsLetterOrDigit(c)||c=='_'||c=='-'||c=='.') &&
            e.TitleHash.Length==64 &&
            e.TitleHash.All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f') &&
            e.Instance is >=0 and <=31 && e.Monitor.Length is >=1 and <=128 &&
            double.IsFinite(e.X)&&double.IsFinite(e.Y) &&
            double.IsFinite(e.W)&&double.IsFinite(e.H) &&
            e.X is >=-10 and <=10 && e.Y is >=-10 and <=10 &&
            e.W is >0 and <=10 && e.H is >0 and <=10);
'@ @'
        return profile.Windows.All(e=>
            e!=null &&
            e.Process is {Length: >=1 and <=128} &&
            e.Process.All(c=>char.IsLetterOrDigit(c)||c=='_'||
                c=='-'||c=='.'||c==' ') &&
            e.TitleHash is {Length: 64} &&
            e.TitleHash.All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f') &&
            e.Instance is >=0 and <=31 &&
            e.Monitor is {Length: >=1 and <=128} &&
            e.Monitor.All(c=>!char.IsControl(c)) &&
            double.IsFinite(e.X)&&double.IsFinite(e.Y) &&
            double.IsFinite(e.W)&&double.IsFinite(e.H) &&
            e.X is >=-10 and <=10 && e.Y is >=-10 and <=10 &&
            e.W is >0 and <=10 && e.H is >0 and <=10);
'@ 'null-safe schema fields and process names'

Swap @'
            return result.Where(p=>p!=null&&ValidName(p.Name)&&
                p.Windows!=null&&p.Windows.Count<=32).Take(12).ToList();
'@ @'
            return result.Where(ValidProfile).Take(12).ToList();
'@ 'validate all persisted profiles on load'

Swap @'
    private static void Save(Profile profile)
    {
        var profiles=Load();
'@ @'
    private static void Save(Profile profile)
    {
        if(!ValidProfile(profile))
            throw new ArgumentException("Invalid window layout profile.");
        var profiles=Load();
'@ 'reject invalid local saves'

Swap @'
    internal static (int Restored,int Skipped) Restore(Profile profile)
    {
        var screens=Forms.Screen.AllScreens;
'@ @'
    internal static (int Restored,int Skipped) Restore(Profile profile)
    {
        if(!ValidProfile(profile))
            return (0,profile?.Windows?.Count ?? 0);
        var screens=Forms.Screen.AllScreens;
'@ 'reject malformed restore profiles'

Swap @'
            var screen=screens.FirstOrDefault(s=>
                s.DeviceName==saved.Monitor)??Forms.Screen.PrimaryScreen;
            if(screen==null){skipped++;continue;}
'@ @'
            // Do not silently send a window to a different monitor if a
            // display was unplugged or its device identifier changed.
            var screen=screens.FirstOrDefault(s=>
                s.DeviceName.Equals(saved.Monitor,
                    StringComparison.OrdinalIgnoreCase));
            if(screen==null){skipped++;continue;}
'@ 'monitor-preserving restore'

[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R181: reject malformed imported/local profiles and never relocate to a missing monitor.'
