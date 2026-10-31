$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$q="$root/DeskSpaceVerifiedUpdateQueueV191.cs"
$updater="$root/DeskSpacePublicUpdaterV44.cs"
$lf=[string][char]10;$crlf=[string][char]13+$lf
function Swap([string]$path,[string]$old,[string]$new,[string]$reason)
{
 $s=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $count=([regex]::Matches($s,[regex]::Escape($old))).Count
 if($count -ne 1){throw ("R219 "+$reason+" expected one anchor, got "+$count)}
 [IO.File]::WriteAllText($path,$s.Replace($old,$new),
     [Text.UTF8Encoding]::new($false))
}
Swap $q @'
    internal static void Stage(string installer,string folder,string tag,
'@ @'
    // GitHub's officially published checksum is NOT a bare digest:
    // BUILD_DESKSPACE_SETUP.ps1 writes "<SHA256>  DeskSpace_Setup.exe".
    // Verify previously accepted that line, while R191.Stage rejected it.
    // Accept exactly the bare SHA256 or an official single-file sha256sum
    // record, with optional CRLF, then retain ONLY the canonical 64 digits.
    // Reject other filenames, extra lines and unexpected trailing fields.
    internal static string NormalizeOfficialChecksumV219(string checksum)
    {
        if(string.IsNullOrWhiteSpace(checksum) || checksum.Length>4096)
            throw new InvalidDataException("Invalid DeskSpace installer SHA-256 checksum.");
        var match=Regex.Match(checksum,
            @"\A[ \t]*(?<hash>[0-9a-fA-F]{64})(?:[ \t]+(?:\*?DeskSpace_Setup\.exe))?[ \t]*(?:\r?\n)?\z",
            RegexOptions.CultureInvariant|RegexOptions.IgnoreCase);
        if(!match.Success)
            throw new InvalidDataException("Invalid DeskSpace installer SHA-256 checksum.");
        return match.Groups["hash"].Value.ToUpperInvariant();
    }

    internal static void Stage(string installer,string folder,string tag,
'@ 'single strict official checksum format'
Swap $q @'
        if(!TagMatches(tag,channel) || !Regex.IsMatch(checksum,
            "^[0-9a-fA-F]{64}$") || !Directory.Exists(folder) ||
'@ @'
        string normalized=NormalizeOfficialChecksumV219(checksum);
        if(!TagMatches(tag,channel) || !Directory.Exists(folder) ||
'@ 'normalize official SHA record before staging'
Swap $q @'
        lock(Sync)pending=new Candidate(installer,folder,tag,checksum,channel);
'@ @'
        // The cache stores only a normalized SHA-256, not a line containing
        // an installer filename, so delayed revalidation stays deterministic.
        lock(Sync)pending=new Candidate(installer,folder,tag,normalized,channel);
'@ 'canonical digest for deferred installer rechecks'
Swap $updater @'
        var match=Regex.Match(checksum,@"^\s*(?<sha>[0-9a-fA-F]{64})(?:\s|$)");
        if(!match.Success)throw new InvalidDataException("Invalid checksum format");
        using var data=File.OpenRead(installer);
        var sha=Convert.ToHexString(SHA256.HashData(data));
        if(!sha.Equals(match.Groups["sha"].Value,StringComparison.OrdinalIgnoreCase))
'@ @'
        // Use the SAME official checksum parser as the verified staging queue.
        // A valid "hash  DeskSpace_Setup.exe" line must be accepted by both.
        var verifiedDigest=DeskSpaceVerifiedUpdateQueueV191.NormalizeOfficialChecksumV219(
            checksum);
        using var data=File.OpenRead(installer);
        var sha=Convert.ToHexString(SHA256.HashData(data));
        if(!sha.Equals(verifiedDigest,StringComparison.OrdinalIgnoreCase))
'@ 'align download verification and R191 staging without bypassing hashes'
Write-Host 'R219: SHA256 verification and R191 verified-stage queue share strict official checksum parser; no integrity checks removed.'
