$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$source="$root/DeskSpacePublicUpdaterV44.cs"
Copy-Item -LiteralPath 'r222/DeskSpaceAtomicInstallerV222.cs' -Destination "$root/DeskSpaceAtomicInstallerV222.cs" -Force
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($source).Replace($crlf,$lf)
$old=@'
        await using var input=await response.Content.ReadAsStreamAsync(ct);
        await using var output=new FileStream(file,FileMode.Create,FileAccess.Write,
            FileShare.None,128*1024,true);
        byte[] buffer=new byte[128*1024];long length=0;
        while(true)
        {
            int n=await input.ReadAsync(buffer.AsMemory(),ct);
            if(n==0)break;
            length+=n;
            if(length>limit)throw new InvalidDataException("Oversized installer");
            await output.WriteAsync(buffer.AsMemory(0,n),ct);
        }
        await output.FlushAsync(ct);
        if(length<5_000_000)throw new InvalidDataException("Truncated installer");
'@.Replace($crlf,$lf)
$new=@'
        await using var input=await response.Content.ReadAsStreamAsync(ct);
        // R222: never overwrite the cached installer while downloading.
        // Promote a completed local transfer in one same-folder file rename;
        // R219 then verifies official SHA256, metadata digest and PE signature.
        await DeskSpaceAtomicInstallerV222.StageFromStreamAsync(input,file,ct);
'@.Replace($crlf,$lf)
$count=([regex]::Matches($s,[regex]::Escape($old))).Count
if($count -ne 1){throw ("R222 existing installer download anchor expected one, got "+$count)}
[IO.File]::WriteAllText($source,$s.Replace($old,$new),
    [Text.UTF8Encoding]::new($false))
Write-Host 'R222: installer bytes stage under a unique same-folder partial, then atomic promote; original cache never truncated.'
