$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$before='                TestHighContrastDonutV208();'
$n=([regex]::Matches($s,[regex]::Escape($before))).Count
if($n -ne 1){throw ("R209 native invocation expected one, got "+$n)}
$s=$s.Replace($before,$before+$lf+'                TestReducedMotionV209();')
$before='    private static void TestHighContrastDonutV208()'
$method=@'
    private static void TestReducedMotionV209()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceReducedMotionV209",true)!;
        var policy=type.GetMethod("ShouldAnimate",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        bool Enabled(bool client,bool menu,bool contrast)=>
            (bool)policy.Invoke(null,new object[]{client,menu,contrast})!;
        Assert(Enabled(true,true,false) &&
            !Enabled(false,true,false) &&
            !Enabled(true,false,false) &&
            !Enabled(true,true,true) &&
            !Enabled(false,false,true),
            "R209 animation policy must honor Windows client/menu motion and contrast settings");
        var reset=type.GetMethod("Reset",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        var canvas=new System.Windows.Controls.Canvas
        {
            Opacity=.25,
            RenderTransform=new System.Windows.Media.TranslateTransform(4,9)
        };
        reset.Invoke(null,new object[]{canvas});
        Assert(Math.Abs(canvas.Opacity-1)<.0001 &&
            canvas.RenderTransform==System.Windows.Media.Transform.Identity,
            "R209 motion-disabled submenus must become fully visible and stationary");
        var panel=new System.Windows.Controls.Border
        {
            Opacity=.1,
            RenderTransform=new System.Windows.Media.ScaleTransform(.9,.9)
        };
        reset.Invoke(null,new object[]{panel});
        Assert(Math.Abs(panel.Opacity-1)<.0001 &&
            panel.RenderTransform==System.Windows.Media.Transform.Identity,
            "R209 no-motion module panels must avoid scale/fade artifacts");
        var donut=Product.GetType("DeskSpace.DeskSpaceSubDonutStandardV62",true)!;
        Assert(donut.GetMethod("Animate",BindingFlags.Static|
            BindingFlags.NonPublic)!=null,
            "R209 must reuse the existing child-ring animation owner");
        Console.WriteLine("R209 PASS: native OS reduced-motion policy and static fully-visible transitions.");
    }

'@
$n=([regex]::Matches($s,[regex]::Escape($before))).Count
if($n -ne 1){throw ("R209 native method anchor expected one, got "+$n)}
$s=$s.Replace($before,$method.Replace($crlf,$lf)+$before)
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R209: native Windows reduced-motion toggle matrix and opacity/transform reset tests registered.'
