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

Write-Host 'R158 fixed Information hidden-count placement and label clearance.'
