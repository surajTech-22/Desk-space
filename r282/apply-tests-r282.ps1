$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$old,[string]$after,[string]$why){
 $old=$old.Replace($crlf,$lf);$after=$after.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($n -ne 1){throw ('R282 '+$why+' anchor='+$n)}
 $script:s=$script:s.Replace($old,$after)
}
Swap '                TestWorkspaceProfileStorageV281();' @'
                TestWorkspaceProfileStorageV281();
                TestRunningAppCountAndDeckClearanceV282();
'@ 'native register'
$method=@'
    private static void TestRunningAppCountAndDeckClearanceV282()
    {
        var apps=Product.GetType("DeskSpace.DeskSpaceRunningAppsV88",true)!;
        var appType=apps.GetNestedType("Entry",BindingFlags.NonPublic)!;
        var dock=Product.GetType("DeskSpace.DeskSpaceMonitorDockV190",true)!;
        var header=Product.GetType("DeskSpace.DeskSpaceDeckHeaderV226",true)!;
        const BindingFlags f=BindingFlags.Static|BindingFlags.NonPublic;
        var normalize=apps.GetMethod("DistinctLabelsV282",f)!;
        var groups=apps.GetMethod("GroupVisible",f)!;
        var untitled=apps.GetMethod("UntitledTaskbarWindowV282",f)!;
        var control=apps.GetMethod("CanControlV282",f)!;
        var display=apps.GetMethod("DisplayTitleV282",f)!;
        var avoid=dock.GetMethod("AvoidWidgetV282",f)!;
        var viewport=dock.GetMethod("ScrollHeightV282",f)!;
        var calculate=dock.GetMethod("Calculate",f)!;
        var title=header.GetMethod("Format",f)!;
        object Make(string label,string path,long handle,string windowTitle)=>
            Activator.CreateInstance(appType,new object[]{
                label,path,new IntPtr(handle),windowTitle})!;
        var three=new[]{
            Make("Common title",@"C:\App\Alpha.exe",11,"Alpha one"),
            Make("Common title",@"C:\App\Alpha.exe",12,"Alpha two"),
            Make("Common title",@"C:\Other\Beta.exe",13,"Beta window"),
            Make("Notes",@"C:\App\Notes.exe",14,"Notes window")
        };
        var input=Array.CreateInstance(appType,three.Length);
        for(int i=0;i<three.Length;i++)input.SetValue(three[i],i);
        var normalized=((System.Collections.IEnumerable)normalize.Invoke(
            null,new object[]{input})!).Cast<object>().ToArray();
        var groupArray=Array.CreateInstance(appType,normalized.Length);
        for(int i=0;i<normalized.Length;i++)
            groupArray.SetValue(normalized[i],i);
        var visible=((System.Collections.IEnumerable)groups.Invoke(null,
            new object[]{groupArray})!).Cast<object>().ToArray();
        var names=normalized.Select(x=>
            (string)appType.GetProperty("Name")!.GetValue(x)!).ToArray();
        Assert(visible.Length==3 &&
            names[0]==names[1] && names[0]!=names[2] &&
            names[3]=="Notes" &&
            visible.Select(x=>appType.GetProperty("Name")!.GetValue(x))
                .Distinct().Count()==3 &&
            (string)title.Invoke(null,new object[]{false,3,0,0})!
                =="Deck · 3 apps",
            "R282 two distinct executables with same friendly name must "+
            "display as three app groups (not two) while same-app windows remain grouped");
        Assert((bool)untitled.Invoke(null,new object[]{"",0x40000L,"HwndWrapper"})! &&
            !(bool)untitled.Invoke(null,new object[]{"",0L,"HwndWrapper"})! &&
            !(bool)untitled.Invoke(null,new object[]{"",0x40000L,"WorkerW"})!,
            "R282 an untitled explicit taskbar app must be included without "+
            "including tool/desktop windows");
        var noTitle=Make("Unnamed app",@"C:\Apps\Unnamed.exe",221,"");
        appType.GetProperty("ProcessId",
            BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)!
            .SetValue(noTitle,1234);
        appType.GetProperty("WindowClass",
            BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)!
            .SetValue(noTitle,"HwndWrapper");
        bool Safe(string value,int pid,string klass)=>(bool)control.Invoke(
            null,new object[]{noTitle,true,value,pid,klass})!;
        Assert(Safe("",1234,"HwndWrapper") &&
            !Safe("",5678,"HwndWrapper") &&
            !Safe("Changed",1234,"HwndWrapper") &&
            !Safe("",1234,"OtherClass") &&
            (string)display.Invoke(null,new object[]{noTitle})=="Unnamed app",
            "R282 untitled window controls must recheck its HWND PID and class");
        var work=new System.Drawing.Rectangle(0,0,340,255);
        var widget=new System.Drawing.Rectangle(90,78,160,145);
        var overlapping=new System.Drawing.Rectangle(80,85,180,54);
        var fixedRect=(System.Drawing.Rectangle)avoid.Invoke(null,
            new object[]{overlapping,widget,work,12})!;
        Assert(!fixedRect.IntersectsWith(
            System.Drawing.Rectangle.Inflate(widget,12,12)) &&
            work.Contains(fixedRect),
            "R282 a Deck painted onto the clock must move fully outside "+
            "the 12-pixel gap while staying on its monitor");
        double height=(double)viewport.Invoke(null,
            new object[]{widget,work,1.0,180,280d})!;
        Assert(height>=34 && height<=58,
            "R282 cramped monitor corner must shrink scrollable Deck "+
            "viewport before painting over the circle");
        int small=(int)Math.Ceiling(height)+2;
        var compact=calculate.Invoke(null,new object[]{
            widget,work,180,small,12})!;
        var chosen=compact.GetType();
        int x=(int)chosen.GetProperty("X")!.GetValue(compact)!;
        int y=(int)chosen.GetProperty("Y")!.GetValue(compact)!;
        var actual=new System.Drawing.Rectangle(x,y,180,small);
        Assert(!actual.IntersectsWith(
             System.Drawing.Rectangle.Inflate(widget,8,8)) &&
            work.Contains(actual),
            "R282 compact popup must be physically separate from circle");
        var otherWork=new System.Drawing.Rectangle(-1920,0,1920,1080);
        var otherWidget=new System.Drawing.Rectangle(-1900,450,150,150);
        var over=new System.Drawing.Rectangle(-1850,470,230,120);
        var corrected=(System.Drawing.Rectangle)avoid.Invoke(null,
            new object[]{over,otherWidget,otherWork,12})!;
        Assert(otherWork.Contains(corrected) &&
            !corrected.IntersectsWith(
                System.Drawing.Rectangle.Inflate(otherWidget,12,12)),
            "R282 negative-origin secondary monitor must preserve Deck gap");
        Console.WriteLine("R282 PASS: 3 distinct apps counted from 2 "+
            "colliding labels, untitled taskbar eligibility and HWND guard, "+
            "12px Deck-clock separation, compact scroll and negative-origin monitor.");
    }

'@
Swap '    private static void TestWorkspaceProfileStorageV281()' ($method+
 '    private static void TestWorkspaceProfileStorageV281()') 'native app count and geometry'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R282 native Windows app count, untitled windows, popup-clock separation and compact Deck tests added.'
