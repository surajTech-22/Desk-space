$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$a='                TestPinnedPickerMonitorV264();'
if(([regex]::Matches($s,[regex]::Escape($a))).Count -ne 1){
 throw 'R265 native test invocation missing R264 anchor'
}
$s=$s.Replace($a,$a+$lf+'                TestDeckSaveRecoveryV265();')
$anchor='    private static void TestPinnedPickerMonitorV264()'
$method=@'
    private static void TestDeckSaveRecoveryV265()
    {
        var guard=Product.GetType("DeskSpace.DeskSpaceDeckSaveGuardV265",true)!;
        var trySave=guard.GetMethod("TrySave",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        bool Save(Action action)=>(bool)trySave.Invoke(null,new object[]{action})!;
        int writes=0;
        Assert(Save(()=>writes++)&&writes==1,
            "R265 successful Deck persistence invokes action once");
        Assert(!Save(()=>throw new IOException("synthetic mutex busy")) &&
               !Save(()=>throw new InvalidDataException("synthetic corrupt map")) &&
               !Save(()=>throw new UnauthorizedAccessException("synthetic denied")) &&
               writes==1,
            "R265 expected write/lock/file corruption errors do not crash WPF or modify prior state");
        bool unexpectedRethrown=false;
        try{Save(()=>throw new InvalidOperationException("programming error"));}
        catch(TargetInvocationException ex)when(
            ex.InnerException is InvalidOperationException)
        {unexpectedRethrown=true;}
        Assert(unexpectedRethrown,
            "R265 unexpected programming errors must not be masked by persistence guard");
        Console.WriteLine("R265 PASS: drag/reset survives I/O, corrupt map and access "+
            "failures; succeeds exactly once without swallowing programming errors.");
    }

'@
if(([regex]::Matches($s,[regex]::Escape($anchor))).Count -ne 1){
 throw 'R265 native test method anchor missing'
}
$s=$s.Replace($anchor,$method.Replace($crlf,$lf)+$anchor)
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R265: native read-only synthetic transient storage and WPF save callback regression registered.'
