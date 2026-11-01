$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$a,[string]$b,[string]$why)
{
 $a=$a.Replace($crlf,$lf);$b=$b.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($a))).Count
 if($n -ne 1){throw ("R230 "+$why+" expected one anchor, got "+$n)}
 $script:s=$script:s.Replace($a,$b)
}
Swap '                TestIndependentDeckAndWidgetPositionsV228();' @'
                TestIndependentDeckAndWidgetPositionsV228();
                TestAllWindowsGracefulCloseV230();
'@ 'native test invocation'
$method=@'
    private static void TestAllWindowsGracefulCloseV230()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceGracefulExitV230",true)!;
        const BindingFlags f=BindingFlags.Static|BindingFlags.NonPublic;
        var signal=type.GetMethod("EventName",f)!;
        string Name(string p)=>(string)signal.Invoke(null,new object[]{p})!;
        var a=Name(@"C:\Program Files\DeskSpace\DeskSpace.exe");
        var b=Name(@"c:\PROGRAM FILES\DeskSpace\DeskSpace.exe");
        var other=Name(@"D:\DeskSpace\DeskSpace.exe");
        Assert(a==b && a!=other && a.StartsWith(@"Local\DeskSpace-UpdateExit-V230-"),
            "R230 IPC must be per-install and case-insensitive, not affect unrelated DeskSpace installs");
        var close=type.GetMethod("TryCloseWindows",f)!;
        bool Try(params Window[] windows)=>
            (bool)close.Invoke(null,new object[]{windows})!;
        var ordered=type.GetMethod("TryCloseInOrder",f)!;
        bool TryOrder(Window primary,params Window[] windows)=>
            (bool)ordered.Invoke(null,new object[]{windows,primary})!;
        var main=new Window{Width=120,Height=100,
            ShowInTaskbar=false,WindowStyle=WindowStyle.None};
        var widget=new Window{Width=90,Height=80,
            ShowInTaskbar=false,WindowStyle=WindowStyle.None};
        var cancel=true;
        System.ComponentModel.CancelEventHandler veto=(_,e)=>e.Cancel=cancel;
        widget.Closing+=veto;
        try
        {
            main.Show();widget.Show();
            bool accepted=TryOrder(main,main,widget);
            Assert(!accepted && widget.IsVisible && main.IsVisible,
                "R232 main WPF window must survive when a secondary vetoes unsaved-work close");
            cancel=false;
            Assert(TryOrder(main,main,widget),
                "R232 close children then main only after every WPF window permits exit");
            var third=new Window{Width=75,Height=75,
                ShowInTaskbar=false,WindowStyle=WindowStyle.None};
            third.Show();
            Assert(Try(third) && !third.IsVisible,
                "R230 normal WPF Close must work for additional windows without Process.Kill");
            Console.WriteLine(
                "R230 PASS: per-install signal, ordinary WPF multi-window close and cancelled unsaved exit.");
        }
        finally
        {
            widget.Closing-=veto;
            if(main.IsVisible)main.Close();
            if(widget.IsVisible)widget.Close();
        }
    }

'@
Swap '    private static void TestIndependentDeckAndWidgetPositionsV228()' ($method+
 '    private static void TestIndependentDeckAndWidgetPositionsV228()') 'native method'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R230 real WPF close/cancellation and per-install IPC tests registered.'
