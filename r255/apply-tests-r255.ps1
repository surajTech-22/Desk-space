$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$a='                TestBatchRenamePreviewV254();'
if(([regex]::Matches($s,[regex]::Escape($a))).Count -ne 1){
 throw 'R255 native smoke call anchor missing'
}
$s=$s.Replace($a,$a+$lf+'                TestFeatureActionGuardV255();')
$anchor='    private static void TestBatchRenamePreviewV254()'
$method=@'
    private static void TestFeatureActionGuardV255()
    {
        var type=Product.GetType(
            "DeskSpace.DeskSpaceFeatureActionGuardV255",true)!;
        var method=type.GetMethod("Execute",BindingFlags.Static|
            BindingFlags.NonPublic)!;
        bool Run(string caption,Action act,Action<string> onWarning)=>
            (bool)method.Invoke(null,new object[]{caption,act,onWarning})!;
        int run=0;
        var warnings=new List<string>();
        Assert(Run("Settings",()=>run++,warnings.Add)&&
               run==1 && warnings.Count==0,
            "R255 ordinary settings navigation preserved");
        bool okay=Run("Settings",()=>throw new InvalidOperationException(
            "Sensitive path C:\\Users\\Secret\\profile.json"),warnings.Add);
        Assert(!okay && warnings.Count==1 &&
               warnings[0].Contains("Settings") &&
               warnings[0].Contains("InvalidOperationException") &&
               !warnings[0].Contains("Secret"),
            "R255 recoverable WPF settings error must not close Workspace or leak details");
        Assert(!Run("Other",()=>throw new System.IO.IOException(
            "Private file name"),warnings.Add)&&
               warnings.Count==2 && !warnings[1].Contains("Private"),
            "R255 ordinary file errors are contained and redact sensitive text");
        Assert(Run("Settings",()=>run++,warnings.Add) && run==2,
            "R255 after recovery other settings operations remain available");
        bool fatalPropagated=false;
        try
        {
            Run("Settings",()=>throw new OutOfMemoryException(
                "Synthetic fatal exception"),warnings.Add);
        }
        catch(TargetInvocationException e)
        {
            fatalPropagated=e.InnerException is OutOfMemoryException;
        }
        Assert(fatalPropagated &&
               warnings.Count==2,
            "R255 fatal runtime exceptions cannot be swallowed");
        Console.WriteLine(
            "R255 PASS: recoverable settings/tool launch failures stay local "+
            "and redact messages, actions remain available, fatal failures propagate.");
    }

'@
if(([regex]::Matches($s,[regex]::Escape($anchor))).Count -ne 1){
 throw 'R255 smoke insertion anchor missing'
}
$s=$s.Replace($anchor,$method.Replace($crlf,$lf)+$anchor)
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R255 native settings/feature crash recovery and secret redaction test registered.'
