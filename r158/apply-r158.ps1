$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$lf=[string][char]10
$crlf=[string][char]13+$lf
function ReplaceOnce([string]$file,[string]$before,[string]$after,[string]$reason){
 $s=[IO.File]::ReadAllText($file).Replace($crlf,$lf)
 $before=$before.Replace($crlf,$lf);$after=$after.Replace($crlf,$lf)
 $count=([regex]::Matches($s,[regex]::Escape($before))).Count
 if($count -ne 1){throw "R158 $reason expected 1 anchor found $count"}
 [IO.File]::WriteAllText($file,$s.Replace($before,$after),
   [Text.UTF8Encoding]::new($false))
}
$info="$root/DeskSpaceInformationV36.cs"
# Only the SAVED wedge is a pagination divider. Previously we passed
# count=visible saved+1, so the "2 hidden" marker split the +New action's
# outer edge. Screenshot showed a detached, tilted 2 beside +New.
ReplaceOnce $info @'
        var countView=new DeskSpaceSubDonutStandardV62.View(
            all.Count+1,startIndex,count,startIndex,
            Math.Max(0,all.Count-startIndex-visible.Length),
            -90,halfSweep,step);
'@ @'
        // + New is an action and is never the hidden-items divider.
        // Keep hidden counts on the last SAVED wedge's radial edge.
        var countView=new DeskSpaceSubDonutStandardV62.View(
            all.Count,startIndex,visible.Length,startIndex,
            Math.Max(0,all.Count-startIndex-visible.Length),
            -90,halfSweep,step);
'@ 'hidden counter on last saved item not +New'
ReplaceOnce $info @'
                Text=name,
                Width=78,
                TextAlignment=TextAlignment.Center,
'@ @'
                Text=name,
                Width=62,
                TextAlignment=TextAlignment.Center,
'@ 'reserve room between item text and radial divider counter'
ReplaceOnce $info '            Canvas.SetLeft(label,x-39);' '            Canvas.SetLeft(label,x-31);' 'center narrower label on saved wedge'

# Per-HWND and per-screen mouse input routing; only shared application DATA
# may cross two monitor widgets, never their input gesture/toggle state.
Copy-Item -LiteralPath 'r158/DeskSpaceMonitorInputV158.cs' -Destination "$root/DeskSpaceMonitorInputV158.cs" -Force
$widget="$root/WidgetHost.cs"
ReplaceOnce $widget @'
    private void OnShortcutRightMouseDown(object sender,MouseButtonEventArgs e)
    {
        if(activeModule!=null)return;
'@ @'
    private void OnShortcutRightMouseDown(object sender,MouseButtonEventArgs e)
    {
        // Ignore right clicks whose physical HWND belongs to the other widget.
        if(!DeskSpaceMonitorInputV158.OwnsRightClick(this))return;
        if(activeModule!=null)return;
'@ 'right click belongs to this window only'
ReplaceOnce $widget @'
    private void SampleMainWheel()
    {
'@ @'
    private void SampleMainWheel()
    {
        if(!DeskSpaceMonitorInputV158.IsPointerOnAssignedMonitor(this))return;
'@ 'main wheel polling remains local to one assigned monitor'
ReplaceOnce $info @'
    private void PollPointer()
    {
'@ @'
    private void PollPointer()
    {
        if(!DeskSpaceMonitorInputV158.IsPointerOnAssignedMonitor(owner))
        {
            newHoverTimer.Stop();
            return;
        }
'@ 'Information hover polling cannot process cursor on other display'

# Real WPF regression for the exact screenshot state: six saved collections,
# four visible data wedges, and permanent + New at the end.
$smoke='r36/UiSmoke/Program.cs'
$call='            TestMonitorAwareSubmenusV157();'
ReplaceOnce $smoke $call ($call+$lf+'            TestInformationSixSavedV158();') 'six-item regression caller'
$method='    private static void TestIndependentMonitorsV154()'
$test=@'
    private static void TestInformationSixSavedV158()
    {
        var host=new Window{Width=420,Height=420,ShowInTaskbar=false,
            WindowStyle=WindowStyle.None};
        var infoType=Product.GetType("DeskSpace.DeskSpaceInformationV36",true)!;
        var info=Activator.CreateInstance(infoType,
            BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Public,
            null,new object[]{host,(Action)(()=>{}),(Action)(()=>{})},null)!;
        try
        {
            var collections=(System.Collections.IList)infoType.GetField("all",
                BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(info)!;
            var collectionType=infoType.GetNestedType("Collection",
                BindingFlags.NonPublic|BindingFlags.Public)!;
            for(int i=0;i<6;i++)
            {
                var item=Activator.CreateInstance(collectionType,true)!;
                collectionType.GetProperty("Name")!.SetValue(item,(6-i).ToString());
                collections.Add(item);
            }
            infoType.GetMethod("RenderArc",
                BindingFlags.NonPublic|BindingFlags.Instance)!
                .Invoke(info,null);
            var board=(Canvas)infoType.GetField("arcCanvas",
                BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(info)!;
            var wedges=board.Children.OfType<System.Windows.Shapes.Path>()
                .Where(p=>p.Tag is int).ToArray();
            Assert(wedges.Length==5,
                "R158 6 saved Information items must show 4 plus permanent New");
            Assert(wedges[3].Stroke==null&&wedges[4].Stroke!=null,
                "R158 count 2 must split LAST SAVED divider, not the New action");
            var inline=board.Children.OfType<System.Windows.Shapes.Path>()
                .Where(p=>(p.Tag as string)=="DeskSpaceHiddenCountInline")
                .ToArray();
            Assert(inline.Length==1,
                "R158 six saved should draw exactly one end count 2");
            var labels=board.Children.OfType<TextBlock>()
                .Where(x=>x.Text=="+ New"||new[]{"6","5","4","3"}.Contains(x.Text))
                .ToArray();
            Assert(labels.Length==5&&labels.All(x=>x.Width<=62),
                "R158 Information labels must leave room around count divider");
            Console.WriteLine("R158 PASS: 6 saved, 4 visible plus New, "+
                "hidden 2 draws on saved wedge without splitting New");
        }
        finally {((IDisposable)info).Dispose();host.Close();}
    }

'@
ReplaceOnce $smoke $method ($test.Replace($crlf,$lf)+$method) 'six-item WPF regression method'

$call='            TestInformationSixSavedV158();'
ReplaceOnce $smoke $call ($call+$lf+'            TestMonitorInputIsolationV158();') 'two-widget mouse-routing regression caller'
$test=@'
    private static void TestMonitorInputIsolationV158()
    {
        var input=Product.GetType("DeskSpace.DeskSpaceMonitorInputV158",true)!;
        var accepts=input.GetMethod("Allows",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        bool Allows(System.Drawing.Rectangle owner,System.Drawing.Rectangle screen,
            System.Drawing.Point pointer,IntPtr actual,IntPtr window)=>
            (bool)accepts.Invoke(null,new object[]{owner,screen,pointer,actual,window})!;
        var left=new System.Drawing.Rectangle(0,0,1920,1040);
        var right=new System.Drawing.Rectangle(1920,0,1600,900);
        var windowA=new System.Drawing.Rectangle(80,80,420,420);
        var windowB=new System.Drawing.Rectangle(2010,70,420,420);
        IntPtr a=new IntPtr(101),b=new IntPtr(202);
        Assert(Allows(windowA,left,new System.Drawing.Point(120,140),a,a),
            "R158 primary must accept its own right-click HWND");
        Assert(!Allows(windowB,right,new System.Drawing.Point(120,140),a,b),
            "R158 secondary must ignore right-click on primary screen");
        Assert(Allows(windowB,right,new System.Drawing.Point(2050,140),b,b),
            "R158 secondary must accept its own right-click");
        Assert(!Allows(windowB,right,new System.Drawing.Point(2050,140),a,b),
            "R158 overlapping WPF widgets must reject other native HWND");
        Assert(!Allows(windowA,left,new System.Drawing.Point(2050,140),a,a),
            "R158 main widget must ignore a right-click on second monitor");
        var negative=new System.Drawing.Rectangle(-1600,-200,1600,900);
        Assert(Allows(new System.Drawing.Rectangle(-400,100,300,300),negative,
            new System.Drawing.Point(-300,160),b,b),
            "R158 monitor to left of primary must keep native negative coordinates");
        // Also resolve the Win32 cursor API under a real WPF native window.
        var physical=System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea;
        var host=new Window{Left=physical.Left+55,Top=physical.Top+55,
            Width=130,Height=130,ShowInTaskbar=false};
        try
        {
            host.Show();
            input.GetMethod("IsPointerOnAssignedMonitor",
                BindingFlags.Static|BindingFlags.NonPublic)!
                .Invoke(null,new object[]{host});
        }
        finally {host.Close();}
        Console.WriteLine("R158 PASS: input ownership isolated across two widget HWNDs, "+
            "both monitor directions, wrong root and real WPF Win32 cursor API.");
    }

'@
ReplaceOnce $smoke $method ($test.Replace($crlf,$lf)+$method) 'two-widget input WPF regression method'


Write-Host 'R158 fixed Information hidden-count placement and label clearance.'
