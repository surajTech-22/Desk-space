$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$a,[string]$b,[string]$why)
{
 $a=$a.Replace($crlf,$lf);$b=$b.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($a))).Count
 if($n -ne 1){throw ("R245 "+$why+": expected 1 anchor, got "+$n)}
 $script:s=$script:s.Replace($a,$b)
}
Swap '                TestFileDuplicatesV244();' @'
                TestFileDuplicatesV244();
                TestClipboardPreviewClearV245();
'@ 'register native clipboard-clear WPF test'
$method=@'
    private static void TestClipboardPreviewClearV245()
    {
        var type=Product.GetType(
            "DeskSpace.DeskSpaceClipboardTransformsV177",true)!;
        var create=type.GetMethod("Create",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        var owner=new Window{Width=160,Height=160};
        var dialog=(Window)create.Invoke(null,new object[]{owner})!;
        try
        {
            var buttons=Walk((DependencyObject)dialog.Content)
                .OfType<System.Windows.Controls.Button>().ToArray();
            var clear=buttons.Single(x=>x.Content?.ToString()=="Clear local preview");
            var boxes=Walk((DependencyObject)dialog.Content)
                .OfType<System.Windows.Controls.TextBox>().ToArray();
            Assert(boxes.Length==2,
                "R245 clipboard converter has local source/result views");
            boxes[0].Text="private clipboard test value";
            boxes[1].Text="private transformed value";
            clear.RaiseEvent(new RoutedEventArgs(
                System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Assert(boxes.All(x=>x.Text.Length==0),
                "R245 clear must zero both local WPF text boxes immediately");
            Assert(buttons.Single(x=>x.Content?.ToString()=="Copy transformed text")
                .IsEnabled==false &&
                buttons.Single(x=>x.Content?.ToString()=="Save As...").IsEnabled==false,
                "R245 clear must disable copy/save until explicit next read");
            Console.WriteLine("R245 PASS: explicit WPF local clipboard preview clear, "+
                "output/actions cleared without accessing Windows clipboard.");
        }
        finally
        {
            dialog.Close();owner.Close();
        }
    }

'@
Swap '    private static void TestFileDuplicatesV244()' ($method+
 '    private static void TestFileDuplicatesV244()') 'WPF control behavior test'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R245: native WPF button test for local clipboard preview clear registered.'
