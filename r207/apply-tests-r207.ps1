$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$old='                TestCancellableFolderZipV206();'
$n=([regex]::Matches($s,[regex]::Escape($old))).Count
if($n -ne 1){throw ("R207 native smoke call expected one, found "+$n)}
$s=$s.Replace($old,$old+$lf+'                TestKeyboardDonutV207();')
$old='    private static void TestCancellableFolderZipV206()'
$method=@'
    private static void TestKeyboardDonutV207()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceDonutKeyboardV207",true)!;
        var next=type.GetMethod("Next",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        var navigates=type.GetMethod("IsNavigation",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        int Select(int at,System.Windows.Input.Key key)=>
            (int)next.Invoke(null,new object[]{at,key})!;
        bool IsNavigation(System.Windows.Input.Key key)=>
            (bool)navigates.Invoke(null,new object[]{key})!;
        Assert(Select(0,System.Windows.Input.Key.Right)==1 &&
            Select(5,System.Windows.Input.Key.Right)==6 &&
            Select(11,System.Windows.Input.Key.Right)==0 &&
            Select(0,System.Windows.Input.Key.Left)==11 &&
            Select(6,System.Windows.Input.Key.Left)==5,
            "R207 left/right must move across both rings without skipping sectors");
        Assert(Select(0,System.Windows.Input.Key.Down)==6 &&
            Select(5,System.Windows.Input.Key.Down)==11 &&
            Select(6,System.Windows.Input.Key.Up)==0 &&
            Select(11,System.Windows.Input.Key.Up)==5 &&
            Select(1,System.Windows.Input.Key.Up)==1 &&
            Select(8,System.Windows.Input.Key.Down)==8,
            "R207 up/down must retain slot while switching ring");
        Assert(Select(7,System.Windows.Input.Key.Home)==0 &&
            Select(1,System.Windows.Input.Key.End)==11 &&
            Select(1,System.Windows.Input.Key.F2)==1 &&
            IsNavigation(System.Windows.Input.Key.Left) &&
            !IsNavigation(System.Windows.Input.Key.Enter) &&
            !IsNavigation(System.Windows.Input.Key.Escape),
            "R207 only dedicated navigation keys may move root selection");
        var host=Product.GetType("DeskSpace.WidgetShellWindow",true)!;
        foreach(var method in new[]{"BeginKeyboardDonutV207",
            "EndKeyboardDonutV207","KeyboardDonutKeyV207",
            "SetKeyboardDonutHighlightV207"})
            Assert(host.GetMethod(method,BindingFlags.Instance|
                BindingFlags.NonPublic)!=null,
                "R207 widget root keyboard lifecycle missing "+method);
        var owned=host.GetField("keyboardDonutModeV207",BindingFlags.Instance|
            BindingFlags.NonPublic);
        Assert(owned!=null && owned.FieldType==typeof(bool),
            "R207 keyboard focus state must be per-widget instance");
        Console.WriteLine(
            "R207 PASS: root donut arrow navigation, ring switching and per-widget focus lifecycle.");
    }

'@
if(([regex]::Matches($s,[regex]::Escape($old))).Count -ne 1){
 throw 'R207 insertion method anchor missing'
}
$s=$s.Replace($old,$method.Replace($crlf,$lf)+$old)
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R207 native WPF keyboard selection and focus lifecycle regression included.'
