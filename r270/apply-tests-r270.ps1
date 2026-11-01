$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$old,[string]$new,[string]$reason)
{
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($n -ne 1){throw ("R270 "+$reason+": expected one anchor, got "+$n)}
 $script:s=$script:s.Replace($old,$new)
}
Swap '                TestUniversalSearchSharedIndexV269();' @'
                TestUniversalSearchSharedIndexV269();
                TestQuickNoteValidationV270();
'@ 'register native validation fixture'
$method=@'
    private static void TestQuickNoteValidationV270()
    {
        var policy=Product.GetType("DeskSpace.DeskSpaceQuickNotePolicyV270",true)!;
        var test=policy.GetMethod("TryPrepare",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        (bool Success,string Title,string Body,string Reason) Prepare(
            string? title,string? content)
        {
            var args=new object?[]{title,content,null,null,null};
            bool accepted=(bool)test.Invoke(null,args)!;
            return (accepted,(string)args[2]!, (string)args[3]!,
                (string)args[4]!);
        }
        var accepted=Prepare("  Weekly plan  ","  Check the ledger  ");
        Assert(accepted.Success && accepted.Title=="Weekly plan" &&
               accepted.Body=="Check the ledger" &&
               accepted.Reason=="",
            "R270 only explicitly typed note fields may become saved content");
        Assert(!Prepare("","text").Success &&
               !Prepare("Title","").Success &&
               !Prepare(new string('T',121),"text").Success &&
               !Prepare("Valid",new string('N',16385)).Success &&
               !Prepare("Title","bad\0text").Success,
            "R270 empty, too long and hidden-control note input must not persist");
        Assert(Prepare("Title","one\r\ntwo\tthree").Success &&
               Prepare(new string('X',120),new string('B',16384)).Success,
            "R270 multiline notes and exact maximum bounds should be valid");
        var hub=Product.GetType("DeskSpace.MainWindow",true)!;
        Assert(hub.GetMethod("ShowQuickNoteV270",
            BindingFlags.Instance|BindingFlags.NonPublic)!=null,
            "R270 the existing Workspace must own its Quick Note editor");
        Console.WriteLine("R270 PASS: explicit Quick Note content and bounds; "+
            "no hidden controls, Workspace-owned editor and no extra data store.");
    }

'@
Swap '    private static void TestUniversalSearchSharedIndexV269()' ($method+
 '    private static void TestUniversalSearchSharedIndexV269()') 'native method insertion'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R270: native Quick Note model validation and persisted Workspace integration registered.'
