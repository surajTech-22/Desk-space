$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$a,[string]$b,[string]$why)
{
 $a=$a.Replace($crlf,$lf);$b=$b.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($a))).Count
 if($n -ne 1){throw ("R246 "+$why+": expected one anchor; got "+$n)}
 $script:s=$script:s.Replace($a,$b)
}
Swap '                TestClipboardPreviewClearV245();' @'
                TestClipboardPreviewClearV245();
                TestKeyboardShortcutsGuideV246();
'@ 'native keyboard help test registration'
$method=@'
    private static void TestKeyboardShortcutsGuideV246()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceKeyboardHelpV246",true)!;
        const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Static;
        var shortcuts=((System.Collections.IEnumerable)
            type.GetMethod("Supported",flags)!.Invoke(null,null)!)
            .Cast<object>().ToArray();
        string Get(object item,string property)=>
            (string)item.GetType().GetProperty(property)!.GetValue(item)!;
        bool Has(string scope,string key)=>shortcuts.Any(x=>
            Get(x,"Scope")==scope&&Get(x,"Keys")==key);
        Assert(shortcuts.Length>=10 &&
            Has("Workspace","F1") && Has("Workspace","Ctrl+K") &&
            Has("Search","Up / Down") &&
            Has("Search","Page Up / Page Down") &&
            Has("Search","Enter") && Has("Search","F8") &&
            Has("Root donut","Home / End") &&
            Has("This guide","Escape"),
            "R246 keyboard help must contain existing verified scoped shortcuts");
        Assert(shortcuts.All(x=>Get(x,"Scope").Length>0 &&
            Get(x,"Keys").Length>0 && Get(x,"Action").Length>0),
            "R246 all help entries require accessible action text");
        var owner=new Window{Width=120,Height=120};
        var dialog=(Window)type.GetMethod("Create",flags)!
            .Invoke(null,new object[]{owner})!;
        try
        {
            var root=(FrameworkElement)dialog.Content;
            root.Measure(new Size(525,580));
            root.Arrange(new Rect(0,0,525,580));
            root.UpdateLayout();
            var close=Walk(root).OfType<Button>()
                .Single(x=>x.Content is string text&&text=="Close guide");
            Assert(close.IsEnabled &&
                dialog.Title.Contains("Keyboard shortcuts"),
                "R246 keyboard guide must expose one accessible Close action");
            close.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert(!dialog.IsVisible,
                "R246 closing the shortcuts guide must not leave a visible popup");
            Console.WriteLine(
                "R246 PASS: 10 scoped verified shortcuts, native accessible WPF "+
                "guide, close interaction and no new system-wide hotkey.");
        }
        finally{dialog.Close();owner.Close();}
    }

'@
Swap '    private static void TestClipboardPreviewClearV245()' ($method+
 '    private static void TestClipboardPreviewClearV245()') 'native guide model and button check'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R246: native WPF scoped shortcut reference and Close test registered.'
