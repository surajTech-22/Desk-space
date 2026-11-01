$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r252/DeskSpaceExcelBackupReceiptV252.cs' -Destination "$root/DeskSpaceExcelBackupReceiptV252.cs" -Force
$p="$root/DeskSpaceExcelFormatApplyV109.cs"
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
# Call only after Excel COM SaveCopyAs succeeds and before ANY range reads,
# merge/table changes, formatting writes, or formula reconstruction.
$anchor='        DeskSpaceExcelSheetVisualV101 before='
$new=@'
        // Keep the original SaveCopyAs authoritative. Record and verify
        // its SHA-256 BEFORE touching any destination cell or table.
        // Never overwrite a previous backup receipt silently.
        string backupReceiptV252=
            DeskSpaceExcelBackupReceiptV252.Create(backup);
        if(!DeskSpaceExcelBackupReceiptV252.Verify(backup,backupReceiptV252))
            throw new IOException(
                "Safety copy checksum receipt invalid; Excel was not modified.");
        DeskSpaceExcelSheetVisualV101 before=
'@.Replace($crlf,$lf).TrimEnd()
$n=([regex]::Matches($s,[regex]::Escape($anchor))).Count
if($n -ne 1){throw ("R252 Excel backup-before-write anchor expected 1, got "+$n)}
[IO.File]::WriteAllText($p,$s.Replace($anchor,$new),[Text.UTF8Encoding]::new($false))
Write-Host 'R252: exact pre-change Excel safety copy now has verified adjacent SHA-256 receipt before writes.'
