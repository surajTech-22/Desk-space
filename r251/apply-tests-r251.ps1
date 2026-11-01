$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$a='                TestMonitorResumePolicyV247();'
$b=$a+$lf+'                TestExcelR1C1SourceIntegrityV251();'
$n=([regex]::Matches($s,[regex]::Escape($a))).Count
if($n -ne 1){throw ("R251 smoke call expected once, got "+$n)}
$s=$s.Replace($a,$b)
$anchor='    private static void TestMonitorResumePolicyV247()'
$method=@'
    private static void TestExcelR1C1SourceIntegrityV251()
    {
        var policy=Product.GetType(
            "DeskSpace.DeskSpaceFormulaMetadataV251",true)!;
        var valid=policy.GetMethod("Valid",BindingFlags.NonPublic|
            BindingFlags.Static)!;
        bool IsGood(string? storage,string? formula)=>
            (bool)valid.Invoke(null,new object?[]{storage,formula})!;
        Assert(IsGood("Formula","=SUM(R[-1]C:R[-8]C)") &&
               IsGood("formula","=RC[-1]*RC[-2]") &&
               IsGood("Blank","") && IsGood("Text",null) &&
               IsGood("Number",""),
            "R251 valid R1C1 formulas and empty nonformula fields accepted");
        Assert(!IsGood("Formula","")&&!IsGood("Formula",null) &&
               !IsGood("Formula","SUM(RC[-1])") &&
               !IsGood("Formula","=") &&
               !IsGood("Text","=RC[1]") &&
               !IsGood("Blank","=1+2") &&
               !IsGood("Formula","=1"+'\0'+"+2") &&
               !IsGood("Formula","="+new string('A',8192)),
            "R251 missing, mislabeled, malformed, oversized and control formulas rejected");
        Console.WriteLine(
            "R251 PASS: source R1C1 formula completeness, 8192-character "+
            "cap, non-formula contradictions and literal formula preservation.");
    }

'@
$n=([regex]::Matches($s,[regex]::Escape($anchor))).Count
if($n -ne 1){throw ("R251 test insertion expected 1, got "+$n)}
$s=$s.Replace($anchor,$method.Replace($crlf,$lf)+$anchor)
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R251: native Windows test for source R1C1 formula consistency registered.'
