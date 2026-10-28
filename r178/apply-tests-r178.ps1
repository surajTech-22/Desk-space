$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function One([string]$a,[string]$b,[string]$label)
{
 $a=$a.Replace($crlf,$lf);$b=$b.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($a))).Count
 if($n -ne 1){throw "R178 WPF $label one anchor required, found $n"}
 $script:s=$script:s.Replace($a,$b)
}
One '            TestClipboardTransformV177();' @'
            TestClipboardTransformV177();
            TestSmartTextDropV178();
'@ 'wire real WPF text and URL Smart Drop smoke'
$method=@'
    private static void TestSmartTextDropV178()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceSmartTextDropV178",true)!;
        var extract=type.GetMethod("TryExtract",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        var web=type.GetMethod("IsWebUrl",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        bool IsWeb(string text)=>(bool)web.Invoke(null,new object[]{text})!;
        Assert(IsWeb("https://example.com/report") &&
            !IsWeb("file:///C:/private.txt") &&
            !IsWeb("javascript:alert(1)"),
            "R178 only real HTTP(S) addresses offer Open");
        object?[] input={new DataObject(DataFormats.UnicodeText,
            "https://example.com/report"),null,false};
        Assert((bool)extract.Invoke(null,input)! &&
            (string)input[1]!="",
            "R178 drag must recognize browser URL text");
        Assert((bool)input[2]!,
            "R178 HTTP(S) drop must use Open/Copy choices");
        input=new object?[]{new DataObject(DataFormats.UnicodeText,
            "Draft summary"),null,false};
        Assert((bool)extract.Invoke(null,input)! &&
            !(bool)input[2]!,
            "R178 ordinary text must offer Save/Copy");
        input=new object?[]{new DataObject(DataFormats.FileDrop,
            new[]{"C:\\Windows\\notepad.exe"}),null,false};
        Assert(!(bool)extract.Invoke(null,input)!,
            "R178 ordinary FileDrop must stay on existing file holding route");
        var shelfType=Product.GetType("DeskSpace.DeskSpaceDropShelfV15",true)!;
        var show=shelfType.GetMethod("ShowSmartDropV178",
            BindingFlags.Instance|BindingFlags.NonPublic)!;
        var hide=shelfType.GetMethod("HideSmartDropV178",
            BindingFlags.Instance|BindingFlags.NonPublic)!;
        var adorned=shelfType.GetField("smartAdornerV178",
            BindingFlags.Instance|BindingFlags.NonPublic)!;
        var host=new Window{Width=116,Height=116,
            ShowInTaskbar=false,Content=new Border{
                Width=102,Height=102,
                Background=Brushes.Transparent}};
        var shelf=Activator.CreateInstance(shelfType,new object[]{host})!;
        try
        {
            host.Show();host.UpdateLayout();
            show.Invoke(shelf,new object[]{"A local note",false});
            Assert(adorned.GetValue(shelf)!=null,
                "R178 visible text drag must render an in-window WPF Adorner");
            var actual=adorned.GetValue(shelf)!;
            Assert(actual.GetType().Name.Contains("SmartTextAdorner"),
                "R178 drag actions cannot use separate Popup HWND");
            var getChoice=Product.GetType(
                "DeskSpace.DeskSpaceFolderDragAdornerV160",true)!
                .GetMethod("ChoiceAt",
                    BindingFlags.Static|BindingFlags.NonPublic)!;
            bool? Choose(double x)=>(bool?)getChoice.Invoke(null,
                new object[]{new Size(116,116),new Point(x,58)});
            Assert(Choose(40)==true&&Choose(76)==false,
                "R178 must require explicit left/right drop area");
            hide.Invoke(shelf,null);
            Assert(adorned.GetValue(shelf)==null,
                "R178 drag adorner must be cleared when leaving widget");
            Console.WriteLine("R178 PASS: real in-widget Smart Drop overlay, "+
                "HTTP(S) URL and text choices, FileDrop isolation and cleanup.");
        }
        finally
        {
            hide.Invoke(shelf,null);
            ((IDisposable)shelf).Dispose();
            host.Close();
        }
    }

'@
One '    private static void TestIndependentMonitorsV154()' (
 $method.Replace($crlf,$lf)+'    private static void TestIndependentMonitorsV154()'
) 'insert real WPF text drop regression'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R178: native WPF Smart Drop adorner geometry and file-route isolation acceptance installed.'
