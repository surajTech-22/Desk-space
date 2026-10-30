$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$before='                TestKeyboardDonutV207();'
$n=([regex]::Matches($s,[regex]::Escape($before))).Count
if($n -ne 1){throw ("R208 isolated native invocation expected one, got "+$n)}
$s=$s.Replace($before,$before+$lf+'                TestHighContrastDonutV208();')
$before='    private static void TestKeyboardDonutV207()'
$method=@'
    private static void TestHighContrastDonutV208()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceHighContrastV208",true)!;
        var wedge=type.GetMethod("Wedge",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        System.Windows.Media.Brush W(bool contrast,int index,bool selected)=>
            (System.Windows.Media.Brush)wedge.Invoke(null,
                new object[]{
                    contrast,index,selected,
                    System.Windows.Media.Brushes.Red,
                    System.Windows.Media.Brushes.Blue,
                    System.Windows.Media.Brushes.Green
                })!;
        Assert(ReferenceEquals(W(false,0,false),
                System.Windows.Media.Brushes.Red) &&
            ReferenceEquals(W(false,1,false),
                System.Windows.Media.Brushes.Blue) &&
            ReferenceEquals(W(false,2,true),
                System.Windows.Media.Brushes.Green),
            "R208 standard appearance must preserve prior primary/secondary/accent colors");
        string Color(System.Windows.Media.Brush b)=>b.ToString()!;
        Assert(Color(W(true,0,false))==
                Color(System.Windows.SystemColors.WindowBrush) &&
            Color(W(true,1,false))==
                Color(System.Windows.SystemColors.ControlBrush) &&
            Color(W(true,5,true))==
                Color(System.Windows.SystemColors.HighlightBrush),
            "R208 high contrast must respect native Windows window, control and selection colors");
        var label=type.GetMethod("Label",BindingFlags.Static|
            BindingFlags.NonPublic)!;
        var high=label.Invoke(null,new object[]{
            true,System.Windows.Media.Brushes.Red});
        var normal=label.Invoke(null,new object[]{
            false,System.Windows.Media.Brushes.Red});
        Assert(Color((System.Windows.Media.Brush)high!)==
                Color(System.Windows.SystemColors.WindowTextBrush) &&
            ReferenceEquals(normal,System.Windows.Media.Brushes.Red),
            "R208 accessible labels must be readable without modifying normal appearance");
        Console.WriteLine("R208 PASS: Windows native high-contrast colors and unchanged normal donut colors.");
    }

'@
if(([regex]::Matches($s,[regex]::Escape($before))).Count -ne 1){
 throw 'R208 native method anchor missing'
}
$s=$s.Replace($before,$method.Replace($crlf,$lf)+$before)
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R208 Windows high-contrast color routing tested against original donut colors.'
