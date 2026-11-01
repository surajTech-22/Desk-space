$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$a='                TestExcelR1C1SourceIntegrityV251();'
$n=([regex]::Matches($s,[regex]::Escape($a))).Count
if($n -ne 1){throw ("R252 native registration missing, got "+$n)}
$s=$s.Replace($a,$a+$lf+'                TestExcelBackupReceiptV252();')
$anchor='    private static void TestExcelR1C1SourceIntegrityV251()'
$method=@'
    private static void TestExcelBackupReceiptV252()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceExcelBackupReceiptV252",true)!;
        const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Static;
        var create=type.GetMethod("Create",flags)!;
        var verify=type.GetMethod("Verify",flags)!;
        string Create(string file)=>(string)create.Invoke(null,new object[]{file})!;
        bool Verify(string file,string receipt)=>(bool)verify.Invoke(
            null,new object[]{file,receipt})!;
        var root=Path.Combine(outdir,"r252-backup-receipt");
        Directory.CreateDirectory(root);
        try
        {
            var backup=Path.Combine(root,"PreChange.xlsx");
            File.WriteAllBytes(backup,System.Text.Encoding.UTF8.GetBytes(
                "synthetic pre-change workbook data, never Office data"));
            var receipt=Create(backup);
            Assert(Verify(backup,receipt) && File.Exists(receipt),
                "R252 create and verify exact pre-change workbook backup checksum");
            Assert(!Verify(backup,receipt+".wrong"),
                "R252 unrelated or redirected receipt cannot approve Excel mutation");
            var original=File.ReadAllBytes(backup);
            File.WriteAllText(backup,"synthetic changed workbook bytes");
            Assert(!Verify(backup,receipt),
                "R252 tampered backup must invalidate pre-change receipt");
            File.WriteAllBytes(backup,original);
            Assert(Verify(backup,receipt),"R252 restore bytes must recover receipt");
            File.AppendAllText(receipt,"0");
            Assert(!Verify(backup,receipt),
                "R252 tampered receipt must not be treated as a verified backup");
            bool blocked=false;
            try{Create(backup);}
            catch(TargetInvocationException ex)
            {
                blocked=ex.InnerException is IOException;
            }
            Assert(blocked && File.Exists(backup),
                "R252 cannot overwrite a prior receipt and must keep source file");
            Console.WriteLine(
                "R252 PASS: real SHA-256 receipt readback, tamper detection, "+
                "name scope, collision rejection, unchanged workbook backup bytes.");
        }
        finally{Directory.Delete(root,true);}
    }

'@
$n=([regex]::Matches($s,[regex]::Escape($anchor))).Count
if($n -ne 1){throw ("R252 smoke method anchor expected 1, got "+$n)}
$s=$s.Replace($anchor,$method.Replace($crlf,$lf)+$anchor)
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R252: native Excel backup SHA-256 tampering/verification fixture registered.'
