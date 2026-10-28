$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r165/DeskSpaceInterruptedCopyV165.cs' "$root/DeskSpaceInterruptedCopyV165.cs" -Force
$p="$root/DeskSpaceDropShelfV15.cs"
$nl=[string][char]10
$crlf=[string][char]13+$nl
$s=[IO.File]::ReadAllText($p).Replace($crlf,$nl)
function Swap([string]$old,[string]$new,[string]$purpose){
    $old=$old.Replace($crlf,$nl);$new=$new.Replace($crlf,$nl)
    $count=([regex]::Matches($script:s,[regex]::Escape($old))).Count
    if($count -ne 1){throw "R165 $purpose expected 1 anchor, found $count"}
    $script:s=$script:s.Replace($old,$new)
}
Swap @'
                try
                {
                    // The copy worker writes only to a GUID-owned *.partial
'@ @'
                try
                {
                    // A tiny exclusive marker lets next startup distinguish
                    // a crashed copy from a real file named *.partial.
                    // Dispose always removes it on success or cancellation.
                    using var copyLease=DeskSpaceInterruptedCopyV165.Begin(destination);
                    // The copy worker writes only to a GUID-owned *.partial
'@ 'lock only the current copy transaction'

Swap @'
            try{DeskSpaceFolderZipV162.PruneAbandoned(
'@ @'
            try
            {
                // Recover only app-marked unfinished staged copies. Never
                // delete complete held files, source files, or another live
                // DeskSpace process's exclusive copy lock.
                DeskSpaceInterruptedCopyV165.PruneAbandoned(
                    Path.Combine(Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                        "DeskSpace","TemporaryShelf"),
                    TimeSpan.FromHours(24));
            }
            catch(Exception error){System.Diagnostics.Debug.WriteLine(
                "Copy temporary recovery skipped: "+error.GetType().Name);}
            try{DeskSpaceFolderZipV162.PruneAbandoned(
'@ 'safe asynchronous cleanup at app initialization'

[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R165: exclusive copy lease and startup cleanup of only stale interrupted app-owned partial files.'
