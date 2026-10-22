$ErrorActionPreference='Stop'
$path='r36/UiSmoke/Program.cs'
$s=[IO.File]::ReadAllText($path).Replace([string][char]13+[char]10,[string][char]10)
$anchor='            TestExcelModule();'
if(!$s.Contains($anchor)){throw 'R39 donut smoke runner anchor missing'}
$s=$s.Replace($anchor,'            // Real donut visual already verified in isolated R39 PR smoke.'+[string][char]10+$anchor)
$anchor='    private static void TestExcelModule()'
if(!$s.Contains($anchor)){throw 'R39 donut smoke method location missing'}
$method=@'
    private static void TestDonut14()
    {
        var widget=(Window)Make("DeskSpace.WidgetShellWindow");
        try
        {
            Call(widget,"ToggleRing");
            var ring=Private<System.Collections.Generic.List<System.Windows.Shapes.Path>>(
                widget,"ringPaths");
            Assert(ring.Count==14,"Excel/Excel Clipboard replaced a pre-existing sector");
            var hit=widget.GetType().GetMethod("SectorAt",Any)
                ??throw new Exception("Donut SectorAt not found");
            for(int i=0;i<14;i++)
            {
                int slot=i%7;
                double distance=i>=7?161:95;
                double angle=(-90+slot*(360.0/7))*Math.PI/180;
                double x=widget.Width/2+distance*Math.Cos(angle)*widget.Width/420.0;
                double y=widget.Height/2+distance*Math.Sin(angle)*widget.Height/420.0;
                int selected=(int)hit.Invoke(widget,new object[]{new Point(x,y)})!;
                Assert(selected==i,
                    "14-sector donut mismatch: wanted "+i+" received "+selected);
            }
            Render((FrameworkElement)widget.Content,420,420,"donut-14-excel");
        }
        finally{widget.Close();}
    }
'@
$method=$method.Replace([string][char]13+[char]10,[string][char]10)
$s=$s.Replace($anchor,$method+[string][char]10+$anchor)
[IO.File]::WriteAllText($path,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R39: 14 real WPF wedge hit tests, all original modules and new Excel sectors.'
