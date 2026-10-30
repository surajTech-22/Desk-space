$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function One([string]$a,[string]$b,[string]$why)
{
 $a=$a.Replace($crlf,$lf);$b=$b.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($a))).Count
 if($n -ne 1){throw "R179 WPF $why requires one anchor, got $n"}
 $script:s=$script:s.Replace($a,$b)
}
One '            TestSmartTextDropV178();' @'
            TestSmartTextDropV178();
            TestWindowLayoutsV179();
'@ 'window layout unit and WPF smoke'
$method=@'
    private static void TestWindowLayoutsV179()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceWindowLayoutsV179",true)!;
        var name=type.GetMethod("ValidName",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        bool Valid(string value)=>(bool)name.Invoke(null,new object[]{value})!;
        Assert(Valid("Accounting Office") && Valid("Gaming_2") &&
            !Valid("../sensitive") && !Valid(""),
            "R179 profile names cannot be paths or empty strings");
        var entryType=type.GetNestedType("WindowEntry",
            BindingFlags.NonPublic)!;
        var make=type.GetMethod("MakeEntry",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        var restore=type.GetMethod("RestoreBounds",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        var sourceWork=new System.Drawing.Rectangle(0,0,1920,1040);
        var saved=(object)make.Invoke(null,new object[]{
            "EXCEL","af03",0,@"\\.\DISPLAY1",
            new System.Drawing.Rectangle(192,104,960,520),sourceWork})!;
        var newMonitor=new System.Drawing.Rectangle(-1280,-90,1280,720);
        var projected=(System.Drawing.Rectangle)restore.Invoke(null,
            new object[]{saved,newMonitor})!;
        Assert(projected.Left>=newMonitor.Left &&
            projected.Top>=newMonitor.Top &&
            projected.Right<=newMonitor.Right &&
            projected.Bottom<=newMonitor.Bottom,
            "R179 restore must remain within the assigned negative-origin monitor");
        Assert(projected.Width==640 && projected.Height==360,
            "R179 normalized layout must scale to new monitor work area");
        var privacy=type.GetMethod("TitleHash",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        var title=(string)privacy.Invoke(null,
            new object[]{"Private Customer Invoice.xlsx"})!;
        Assert(title.Length==64 &&
            !title.Contains("Customer",StringComparison.OrdinalIgnoreCase),
            "R179 must not store plaintext window/document titles");
        var create=type.GetMethod("Create",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        var owner=new Window{Width=116,Height=116,ShowInTaskbar=false};
        Window? panel=null;
        try
        {
            owner.Show();
            panel=(Window)create.Invoke(null,new object[]{owner})!;
            panel.Show();panel.UpdateLayout();
            var buttons=Walk(panel).OfType<Button>().ToArray();
            Assert(buttons.Any(b=>b.Content?.ToString()=="Capture current layout")&&
                buttons.Any(b=>b.Content?.ToString()=="Restore selected"),
                "R179 native WPF layout panel must expose explicit capture and restore");
            Console.WriteLine("R179 PASS: normalized mixed-DPI, negative-origin layout "+
                "bounds, privacy-safe identifiers and actual WPF profile UI.");
        }
        finally{panel?.Close();owner.Close();}
    }

'@
One '    private static void TestIndependentMonitorsV154()' (
 $method.Replace($crlf,$lf)+'    private static void TestIndependentMonitorsV154()'
) 'insert native WPF window layout regression'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R179: real WPF window profiles and multi-display normalization acceptance installed.'
