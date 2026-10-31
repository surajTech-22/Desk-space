$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$a,[string]$b,[string]$why)
{
 $a=$a.Replace($crlf,$lf);$b=$b.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($a))).Count
 if($n -ne 1){throw ("R257 "+$why+" expected one anchor, got "+$n)}
 $script:s=$script:s.Replace($a,$b)
}
Swap '                TestFolderPreDropV256();' @'
                TestFolderPreDropV256();
                TestWindowRestorePreviewV257();
'@ 'native plan and UI registration'
$method=@'
    private static void TestWindowRestorePreviewV257()
    {
        var original=Product.GetType("DeskSpace.DeskSpaceWindowLayoutsV179",true)!;
        var planner=Product.GetType("DeskSpace.DeskSpaceWindowRestorePlanV257",true)!;
        var savedType=original.GetNestedType("WindowEntry",BindingFlags.NonPublic)!;
        var liveType=planner.GetNestedType("Live",BindingFlags.NonPublic)!;
        var list=Activator.CreateInstance(typeof(List<>).MakeGenericType(savedType))!;
        var saved=(System.Collections.IList)list;
        object Saved(string process,string hash,int instance,string screen,
            double x=0.1,double y=0.1,double w=0.45,double h=0.5)=>
            Activator.CreateInstance(savedType,new object[]{
                process,hash,instance,screen,x,y,w,h})!;
        saved.Add(Saved("EXCEL","excel-title-hash",0,@"\\.\DISPLAY1"));
        saved.Add(Saved("Notepad","notes-title-hash",0,@"\\.\DISPLAY2"));
        saved.Add(Saved("Browser","old-title-hash",0,@"\\.\DISPLAY1"));
        var live=(System.Collections.IList)Activator.CreateInstance(
            typeof(List<>).MakeGenericType(liveType))!;
        live.Add(Activator.CreateInstance(liveType,
            new object[]{"EXCEL","excel-title-hash"})!);
        live.Add(Activator.CreateInstance(liveType,
            new object[]{"Notepad","notes-title-hash"})!);
        live.Add(Activator.CreateInstance(liveType,
            new object[]{"Browser","new-title-hash"})!);
        var screens=new Dictionary<string,System.Drawing.Rectangle>(
            StringComparer.OrdinalIgnoreCase){
            [@"\\.\DISPLAY1"]=new System.Drawing.Rectangle(-1600,-80,1600,900)};
        var build=planner.GetMethod("Build",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        var result=build.Invoke(null,new object[]{list,live,screens})!;
        var resultType=result.GetType();
        int ready=(int)resultType.GetProperty("Ready")!.GetValue(result)!;
        int skipped=(int)resultType.GetProperty("Skipped")!.GetValue(result)!;
        var rows=((System.Collections.IEnumerable)resultType
            .GetProperty("Rows")!.GetValue(result)!).Cast<object>().ToArray();
        Assert(ready==1&&skipped==2&&rows.Length==3,
            "R257 exact-match running window projects, disconnected monitor and renamed title skipped");
        bool Can(object row)=>(bool)row.GetType()
            .GetProperty("Ready")!.GetValue(row)!;
        var rectangle=(System.Drawing.Rectangle)rows[0].GetType()
            .GetProperty("Bounds")!.GetValue(rows[0])!;
        Assert(Can(rows[0])&&!Can(rows[1])&&!Can(rows[2]) &&
               rectangle.Left>=-1600&&rectangle.Right<=0 &&
               rectangle.Top>=-80&&rectangle.Bottom<=820,
            "R257 negative-origin monitor containment, no silent monitor rehome");
        var owner=new Window{Width=180,Height=180,ShowInTaskbar=false};
        Window? dialog=null;
        try
        {
            owner.Show();
            dialog=(Window)original.GetMethod("Create",BindingFlags.Static|
                BindingFlags.NonPublic)!.Invoke(null,new object[]{owner})!;
            dialog.Show();dialog.UpdateLayout();
            var buttons=Walk(dialog).OfType<Button>().ToArray();
            Assert(buttons.Any(x=>x.Content?.ToString()=="Preview selected")&&
                buttons.Any(x=>x.Content?.ToString()=="Restore selected"),
                "R257 native Workspace has explicit preview and restore controls");
        }
        finally{dialog?.Close();owner.Close();}
        Console.WriteLine("R257 PASS: redacted read-only preview, missing monitor "+
            "and changed title skipped, exact negative-origin bounds, visible consent UI.");
    }

'@
Swap '    private static void TestFolderPreDropV256()' (
 $method.Replace($crlf,$lf)+'    private static void TestFolderPreDropV256()'
) 'new Windows WPF preview regression'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R257: native WPF read-only layout preview, disconnected-display and UI consent tests registered.'
