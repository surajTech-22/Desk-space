$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$tests='DeskSpace_Windows_R1/DeskSpace.Tests/Program.cs'
if(!(Test-Path $tests)){throw 'R10 tests project missing'}
$s=[IO.File]::ReadAllText($tests)
if($s.Contains('R10 encrypted vault')){throw 'Duplicate vault test patch'}
$extra=@'
// R10 encrypted vault tests use a unique temp path; never touch actual user data.
var r10TestFolder = Path.Combine(Path.GetTempPath(),"DeskSpaceR10Vault_"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(r10TestFolder);
try
{
    var encryptedPath=Path.Combine(r10TestFolder,"vault.enc");
    const string masterPassword="R10-security-test-password-2026";
    const string testSecret="confidential-R10-nonplaintext-12345";
    Guid testEntryId;
    using(var secure=new DeskSpaceVaultV10(encryptedPath))
    {
        secure.Unlock(masterPassword);
        var entry=new DeskSpaceVaultV10.Secret{Label="R10 Test Account",Username="demo-account",Password=testSecret};
        testEntryId=entry.Id;
        secure.Upsert(entry);
        Check(secure.IsUnlocked && secure.Entries.Count==1,"R10 encrypted vault stores one authenticated secret");
        secure.Lock();
        Check(!secure.IsUnlocked && secure.Entries.Count==0,"R10 vault locks and erases active entries");
    }
    var disk=File.ReadAllText(encryptedPath);
    Check(!disk.Contains(testSecret) && !disk.Contains("demo-account"),"R10 vault file does not expose password or username");
    using(var secure=new DeskSpaceVaultV10(encryptedPath))
    {
        var rejected=false;
        try{secure.Unlock("wrong-master-password-123456");}
        catch(System.Security.Cryptography.CryptographicException){rejected=true;}
        Check(rejected && !secure.IsUnlocked,"R10 wrong master password cannot decrypt");
        secure.Unlock(masterPassword);
        Check(secure.Entries.Count==1 && secure.Entries[0].Id==testEntryId &&
            secure.Entries[0].Password==testSecret,"R10 encrypted vault round-trip read");
    }
}
finally { try{Directory.Delete(r10TestFolder,true);}catch{} }
Console.WriteLine("R10 encrypted vault tests passed.");
'@
[IO.File]::WriteAllText($tests, $s+[Environment]::NewLine+$extra,[Text.UTF8Encoding]::new($false))
Write-Host 'R10 vault crypto and data-isolation regression tests added.'
