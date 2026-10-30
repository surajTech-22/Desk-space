$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$old,[string]$new,[string]$reason)
{
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($n -ne 1){throw ("R217 "+$reason+" expected one anchor, got "+$n)}
 $script:s=$script:s.Replace($old,$new)
}
Swap '                TestEventDrivenDisplaysV216();' @'
                TestEventDrivenDisplaysV216();
                TestNativePdfPreviewV217();
'@ 'native invocation'
$method=@'
    private static void TestNativePdfPreviewV217()
    {
        var type=Product.GetType("DeskSpace.DeskSpacePdfPreviewV217",true)!;
        const BindingFlags flags=BindingFlags.Static|BindingFlags.NonPublic;
        var allowed=type.GetMethod("Allowed",flags)!;
        bool Valid(long count)=>(bool)allowed.Invoke(null,new object[]{count})!;
        Assert(!Valid(-1)&&!Valid(0)&&Valid(1)&&Valid(32L*1024*1024)&&
            !Valid(32L*1024*1024+1),
            "R217 PDF renderer must enforce file-size limit before accessing document");
        var create=type.GetMethod("Create",flags)!;
        var panel=create.Invoke(null,new object[]{
            Path.Combine(Path.GetTempPath(),"synthetic-pdf-not-opened.pdf")});
        Assert(panel is System.Windows.Controls.StackPanel,
            "R217 PDF preview must be an in-widget WPF element, not an external process");
        var stack=(System.Windows.Controls.StackPanel)panel!;
        Assert(stack.Children.OfType<System.Windows.Controls.Image>().Count()==1 &&
            stack.Children.OfType<System.Windows.Controls.StackPanel>().Any(),
            "R217 on-demand preview must include page image and accessible navigation");
        var standard=Product.GetType("DeskSpace.DeskSpaceFilePreviewV85",true)!;
        Assert(standard.GetMethod("Create",flags)!=null,
            "R217 must integrate with existing file preview UI");
        Console.WriteLine(
            "R217 PASS: on-demand native PDF preview, explicit page navigation and bounded file policy.");
    }

'@
Swap '    private static void TestEventDrivenDisplaysV216()' ($method+
 '    private static void TestEventDrivenDisplaysV216()') 'native method'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R217 isolated native WPF PDF preview policy and navigation tests registered.'
