$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$crlf=[string][char]13+[char]10;$lf=[string][char]10
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$old,[string]$new,[string]$why)
{
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($n -ne 1){throw ('R279 '+$why+' anchor='+$n)}
 $script:s=$script:s.Replace($old,$new)
}
Swap '                TestIndependentMonitorPositionStoreV278();' @'
                TestIndependentMonitorPositionStoreV278();
                TestRunningWindowControlsV279();
'@ 'register Windows flyout test'
$method=@'
    private static void TestRunningWindowControlsV279()
    {
        var apps=Product.GetType("DeskSpace.DeskSpaceRunningAppsV88",true)!;
        var hover=Product.GetType("DeskSpace.DeskSpaceStableHoverV93",true)!;
        var entryType=apps.GetNestedType("Entry",BindingFlags.NonPublic)!;
        const BindingFlags f=BindingFlags.NonPublic|BindingFlags.Static;
        var guard=apps.GetMethod("CanControlV279",f)!;
        var command=apps.GetMethod("RequestStateV279",f)!;
        var make=hover.GetMethod("MakeStateButtonV279",f)!;
        var dummy=Activator.CreateInstance(entryType,new object[]{
            "Test app","",new IntPtr(7654321),"Original title"})!;
        bool Accept(object selected,bool exists,string title)=>
            (bool)guard.Invoke(null,new object[]{selected,exists,title})!;
        Assert(Accept(dummy,true,"Original title") &&
               !Accept(dummy,true,"Changed title") &&
               !Accept(dummy,false,"Original title") &&
               !Accept(dummy,true,"") &&
               !(bool)command.Invoke(null,new object[]{dummy,true})!,
            "R279 invalid or reused HWND may not be acted on");
        var minimize=(Button)make.Invoke(null,new object[]{dummy,true})!;
        var restore=(Button)make.Invoke(null,new object[]{dummy,false})!;
        Assert(minimize.ToolTip?.ToString()=="Minimize this window" &&
               restore.ToolTip?.ToString()=="Restore this window" &&
               System.Windows.Automation.AutomationProperties.GetName(minimize)
                   =="Minimize selected application window" &&
               System.Windows.Automation.AutomationProperties.GetName(restore)
                   =="Restore selected application window",
            "R279 existing Running Apps flyout exposes two accessible "+
            "separate window controls");
        minimize.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert(minimize.ToolTip?.ToString()?.
                   Contains("Window changed or closed")==true,
            "R279 stale window click must not trigger any other process action");
        var actual=new Window{Title="R279 native window",
            Width=320,Height=190,ShowInTaskbar=true};
        try
        {
            actual.Show();
            var hwnd=new System.Windows.Interop.WindowInteropHelper(actual).Handle;
            var live=Activator.CreateInstance(entryType,new object[]{
                "WPF Smoke","",hwnd,actual.Title})!;
            Assert(Accept(live,true,actual.Title) &&
                   !Accept(live,true,"R279 changed native title"),
                "R279 compares exact original title before state changes");
            Assert((bool)command.Invoke(null,new object[]{live,true})! &&
                   (bool)command.Invoke(null,new object[]{live,false})!,
                "R279 live WPF HWND accepts explicit minimize and restore requests");
            actual.Title="Another app now owns this HWND";
            Assert(!(bool)command.Invoke(null,new object[]{live,true})!,
                "R279 changing a window's live title invalidates stale control actions");
        }
        finally{actual.Close();}
        Console.WriteLine("R279 PASS: guarded live WPF Minimize/Restore, "+
            "stale/reused HWND refusal, and accessible per-window controls.");
    }

'@
Swap '    private static void TestIndependentMonitorPositionStoreV278()' ($method+
 '    private static void TestIndependentMonitorPositionStoreV278()') 'native WPF method'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R279 native WPF running-app direct window controls test registered.'
