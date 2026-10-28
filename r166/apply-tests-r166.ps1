$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function One([string]$old,[string]$new,[string]$description)
{
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $count=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($count -ne 1){throw "R166/R167 WPF $description: expected one anchor, found $count"}
 $script:s=$script:s.Replace($old,$new)
}
One '            TestInterruptedCopyRecoveryV165();' @'
            TestInterruptedCopyRecoveryV165();
            TestTrayRecoveryV166();
'@ 'tray test entry point'
$method=@'
    private static void TestTrayRecoveryV166()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceTrayRecoveryV166",true)!;
        var watch=type.GetMethod("Watch",BindingFlags.Static|BindingFlags.NonPublic)!;
        var restore=type.GetMethod("RestoreForTest",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        var message=(uint)type.GetField("TaskbarCreated",
            BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
        Assert(message!=0,"R166 Explorer restart registration must succeed");
        var host=new Window{Width=116,Height=116,ShowInTaskbar=false};
        using var icon=new System.Windows.Forms.NotifyIcon{
            Icon=System.Drawing.SystemIcons.Application,Visible=false};
        IDisposable? watcher=null;
        try
        {
            host.Show();
            watcher=(IDisposable)watch.Invoke(null,new object[]{host,icon})!;
            Assert(icon.Visible,"R166 primary tray must be visible after widget startup");
            host.Hide();
            Assert(icon.Visible,"R166 tray must remain active while circle is hidden");
            icon.Visible=false;
            Assert((bool)restore.Invoke(null,new object[]{icon,true})!,
                "R166 Explorer recovery must re-register the SAME tray icon");
            Assert(!host.IsVisible,
                "R166 Explorer restart must never foreground a hidden widget");
            var hook=watcher.GetType().GetMethod("OnMessage",
                BindingFlags.NonPublic|BindingFlags.Instance)!;
            var values=new object?[]{IntPtr.Zero,unchecked((int)message),
                IntPtr.Zero,IntPtr.Zero,false};
            hook.Invoke(watcher,values);
            host.Dispatcher.Invoke(()=>{},
                System.Windows.Threading.DispatcherPriority.ContextIdle);
            Assert(icon.Visible&&!host.IsVisible,
                "R166 real registered taskbar message must restore tray invisibly");
            Console.WriteLine("R166 PASS: Explorer restart, one tray icon, "+
                "no foreground activation, hidden window stays hidden.");
        }
        finally
        {
            watcher?.Dispose();
            host.Close();
        }
    }

'@
One '    private static void TestIndependentMonitorsV154()' (
    $method.Replace($crlf,$lf)+'    private static void TestIndependentMonitorsV154()'
) 'real tray test method'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R166 WPF: real NotifyIcon recovery and TaskbarCreated, hidden widget remains hidden.'
