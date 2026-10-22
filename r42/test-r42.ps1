$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
./r42/apply-r42-editor.ps1
./r42/apply-r42-circle.ps1
$root='DeskSpace_Windows_R1/DeskSpace'
$hostSource=[IO.File]::ReadAllText("$root/WidgetHost.cs")
$editor=[IO.File]::ReadAllText("$root/DeskSpaceExcelEditorV42.cs")
$wheel=[IO.File]::ReadAllText("$root/DeskSpaceExcelWheelV42.cs")
$base=[IO.File]::ReadAllText("$root/DeskSpaceExcelV16.cs")
$bridge=[IO.File]::ReadAllText("$root/DeskSpaceExcelBridgeV16.cs")
foreach($check in @(
 'internal sealed partial class DeskSpaceExcelV16',
 'await PresentCandidateAsync(candidate);',
 'ShowCapturedV42(candidate);',
 'private async void Capture()', 'private async void Apply()'
)){if(!$base.Contains($check)){throw "R42 main editor integration missing: $check"}}
foreach($check in @(
 'Excel Mapping','SAVED MAPPINGS','Data field name',
 'Data value','Value cell','ReferenceCell','Save collection & data',
 'Collection name (groups related data together)',
 'Data name (saved Excel mapping)',
 'Capture selected Excel cells','Refresh saved mappings','LoadEditorV42(',
 'SaveEditorV42()','DeskSpaceExcelStoreV16.Load()',
 'DeskSpaceExcelStoreV16.Save(disk)','ShowCapturedV42(',
 'existing>=0)disk[existing]=edited',
 'SourceWorkbook','CoordinatesVerified','GroupPath','mappingGroupV42'))
{if(!$editor.Contains($check)){throw "R42 saved mapping editor missing: $check"}}
foreach($check in @(
 'class DeskSpaceExcelWheelV42',
 'DeskSpaceExcelStoreV16.Load()',
 'TryCapturedDragAtScreen(', 'OnWheel(', 'Hover(', 'Preview(',
 'private void OpenExcelArc()', 'if(name=="Excel Functions")',
 'Excel is not open', 'ExcelIsOpen()',
 'if(selected==12 && ringExpanded',
 'private async System.Threading.Tasks.Task PasteAsync(',
 'DeskSpaceExcelWorkerV41.PreviewAsync(entry,',
 'selection:false,overwrite:true',
 'DeskSpaceExcelWorkerV41.ApplyAsync(',
 'collapseParent();','Excel paste blocked','CoordinatesVerified',
 '"Clipboard","clipboard"','"Information","information"',
 'groupPath','successTimer.Start();'))
{
 if(!$wheel.Contains($check) -and !$hostSource.Contains($check)){
     throw "R42 direct-paste Excel circle missing: $check"
 }
}
# Information remains outer ring index 6; Excel Functions is outer 12.
# Test the live selector, not comments in other methods.
$paintStart=$hostSource.IndexOf('    private void UpdateHighlightedSector(',[StringComparison]::Ordinal)
$paintEnd=$hostSource.IndexOf('    private void BuildCompact()', [Math]::Max(0,$paintStart),[StringComparison]::Ordinal)
if($paintStart -lt 0 -or $paintEnd -le $paintStart) {
    throw 'R42 cannot locate updated WPF hover selector'
}
$paint=$hostSource.Substring($paintStart,$paintEnd-$paintStart)
foreach($required in @('if(selected>=0 && selected!=6)',
    'else if(selected==6 && ringExpanded && activeModule==null)',
    'OpenInformationArc();',
    'if(selected==12 && ringExpanded && activeModule==null)',
    'OpenExcelArc();',
    'if(selected>=0 && selected!=12 && excelWheel?.IsArcOpen==true)',
    'excelWheel.HideArc();'))
{
    if(!$paint.Contains($required)){throw "R42 Excel/Information ring-index regression: $required"}
}
if($paint.Contains('else if(selected==7 && ringExpanded')) {
    throw 'R42: Information was moved from its original outer sector 6'
}
# Reject the compiler-reported ambiguous Path token before dotnet build.
foreach($bad in @('List<Path>', 'new Path{')){
    if($wheel.Contains($bad)) {
        throw "R42 WPF Path CS0104 ambiguity remains: $bad"
    }
}
if(!$wheel.Contains('System.Windows.Shapes.Path')){
    throw 'R42 Excel child geometry is missing explicitly qualified WPF Path'
}

if(!$hostSource.Contains('excelWheel?.Dispose();')){throw 'Excel popup lifetime leak'}
# The wheel must have 13 sectors: SIX original inner and SEVEN outer,
# with Excel Functions last, and no main Excel Clipboard wedge.
$seg=$hostSource.IndexOf('private static readonly string[] Segments')
$icons=$hostSource.IndexOf('private static readonly string[] SegmentIcons',$seg)
if($seg -lt 0 -or $icons -le $seg){throw 'R42 main wheel labels unavailable'}
$main=$hostSource.Substring($seg,$icons-$seg)
foreach($check in @('"Excel Functions"','"Information"','"Clipboard"')){
    if(!$main.Contains($check)){throw "R42 missing main sector: $check"}
}
if($main.Contains('"Excel Clipboard"')){throw 'R42 duplicate main Excel Clipboard sector remains'}
$mainSegments=[regex]::Matches($main,'"[^"]+"') |
    ForEach-Object {$_.Value.Trim('"')}
if($mainSegments.Count -ne 13 -or
   $mainSegments[5] -ne 'Settings' -or
   $mainSegments[6] -ne 'Information' -or
   $mainSegments[12] -ne 'Excel Functions'){
    throw 'R42 Excel must be last OUTER sector; six original inner sectors preserved'
}
if(!$hostSource.Contains('double sectors=outer?7:6;')){
    throw 'R42 13-sector input geometry missing'
}
if(!$hostSource.Contains('double step=360.0/(outer?7:6);')){
    throw 'R42 13-sector rendered geometry missing'
}
if(!$wheel.Contains('enum ExcelLevel { Functions, Information }') -or
   !$wheel.Contains('entries.Add(("▸ "+group,"group"') -or
   !$wheel.Contains('entries.Add((entry.Name,"mapping"')){
    throw 'R42 Excel Information group hierarchy unavailable'
}
if(!$wheel.Contains('successTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(3)}')){
    throw 'R42 successful Excel paste-only toast timer unavailable'
}
if(!$wheel.Contains('suppressNextUp=true;') -or
   !$wheel.Contains('if(suppressNextUp)')){
    throw 'R42 child navigation could accidentally paste on the same mouse up'
}
foreach($check in @('x.Source.Role=="Value"','ValueKind!="formula"',
 'ValueKind!="blank"','cell.Value2=TypedValue(change.Source);',
 'SaveCopyAs(backup)','if(!preview.Verified || !entry.CoordinatesVerified)'))
{if(!$bridge.Contains($check)){throw "R42 Paste Special Values safety gate missing: $check"}}
# Keep all original wheel modules and the isolated Excel Clipboard.
foreach($check in @('Excel Clipboard','Information','Notes','Reminders',
 'Weather','Calculator','Files','Tasks','DeskSpaceExcelClipboardV39'))
{if(!$hostSource.Contains($check)){throw "R42 regression: original module $check was removed"}}
foreach($file in @('DeskSpaceExcelStoreV16.cs','DeskSpaceVaultV10.cs',
 'DeskSpaceUpdater.cs','DeskSpaceDropShelfV15.cs','DeskSpaceWeatherSceneV11.cs')){
 if(!(Test-Path "$root/$file")){throw "R42 missing preserved file $file"}
}
Write-Host 'R42 source safety gate PASS: contextual Excel child arc, direct values-only paste, mapping editor, original data.'

# Data-format regression: GroupPath is optional and does not rename or
# reidentify historical encrypted Excel mappings.
$store=[IO.File]::ReadAllText("$root/DeskSpaceExcelStoreV16.cs")
if(!$store.Contains('public string GroupPath {get;set;}=""')){
    throw 'R42 optional group classification missing in saved mapping model'
}
# Update previously correct WPF screenshot acceptance tests to the new
# simplified editor. Advanced-only controls are intentionally collapsed;
# tests must validate primary visible actions.
$smoke='r36/UiSmoke/Program.cs'
$s=[IO.File]::ReadAllText($smoke)
$old=@'
            "Analyze marked image region",
            "Verify screenshot against selected Excel range",
            "Copy and analyze selected Excel cells",
            "Copy range to Excel-only shelf",
            "Save checked values",
'@
$new=@'
            "Refresh saved mappings",
            "Capture selected Excel cells",
            "Copy and analyze selected Excel cells",
            "Save collection & data",
            "Delete selected data",
'@
$old=$old.Replace([string][char]13+[char]10,[string][char]10)
$new=$new.Replace([string][char]13+[char]10,[string][char]10)
$s=$s.Replace([string][char]13+[char]10,[string][char]10)
if(!$s.Contains($old)){throw 'R42 cannot update legacy R39 screenshot Excel buttons'}
$s=$s.Replace($old,$new)
# The compact editor deliberately labels screenshot import more briefly.
$s=$s.Replace('"Upload screenshot + OCR"','"Screenshot"')
# R40 main Workspace smoke checked legacy deep advanced actions.
$old2=@'
            "Verify screenshot against selected Excel range",
            "Save checked values",
            "Preview matching cells"
'@
$new2=@'
            "Refresh saved mappings",
            "Save collection & data",
            "Preview matching cells"
'@
if($s.Contains($old2)){
    $s=$s.Replace($old2,$new2)
}else{
    # The R39 button rewrite above already changes these shared labels.
    # Do not fail solely because the legacy R40 exact text is gone.
    foreach($label in @('"Refresh saved mappings"','"Save collection & data"')){
        if(!$s.Contains($label)){throw "R42 Workspace smoke missing revised action $label"}
    }
}
# R40's separate Workspace test still contains legacy labels not adjacent to
# the R39 block. Match the actual revised UI in every smoke assertion.
$s=$s.Replace('"Verify screenshot against selected Excel range"','"Refresh saved mappings"')
$s=$s.Replace('"Save checked values"','"Save collection & data"')
if($s.Contains('"Verify screenshot against selected Excel range"') -or
   $s.Contains('"Save checked values"')){
    throw 'R42 smoke retained outdated Excel Workspace actions'
}
# The original 14-wedge smoke source was created by R39. Replace its
# unreachable extra outer wedge assertion and execute the 13-wedge test.
if(!$s.Contains('    private static void TestDonut14()')){
    throw 'R42 cannot locate original 14-sector donut WPF smoke test'
}
$s=$s.Replace('    private static void TestDonut14()',
              '    private static void TestDonut13()')
$s=$s.Replace('ring.Count==14','ring.Count==13')
$s=$s.Replace('i<14;i++','i<13;i++')
$s=$s.Replace('int slot=i%7;','int slot=i>=6?i-6:i;')
$s=$s.Replace('double distance=i>=7?161:95;',
              'double distance=i>=6?161:95;')
$s=$s.Replace('(-90+slot*(360.0/7))*Math.PI/180',
              '(-90+slot*(360.0/(i>=6?7:6)))*Math.PI/180')
$s=$s.Replace('14-sector donut mismatch','13-sector donut mismatch')
$s=$s.Replace('donut-14-excel','donut-13-excel-functions')
$s=$s.Replace('Excel/Excel Clipboard replaced a pre-existing sector',
              'Excel Functions removed an original 12-sector feature')
$anchor='            TestExcelWorkspaceAndShelf();'
if(!$s.Contains($anchor)){throw 'R42 missing WPF smoke caller'}
$s=$s.Replace($anchor,$anchor+[string][char]10+
    '            TestDonut13();'+[string][char]10+
    '            TestExcelDirectPasteWheel();')
$anchor='    private static void TestExcelWorkspaceAndShelf()'
if(!$s.Contains($anchor)){throw 'R42 missing WPF test insertion'}
$method=@'
    private static void TestExcelDirectPasteWheel()
    {
        // The floating Excel sector must be a child wheel, not a second
        // heavyweight editor window. Drawing/hover never calls Excel COM.
        var owner=new Window { Width=420,Height=420,ShowInTaskbar=false,
            WindowStyle=WindowStyle.None };
        var wheel=Make("DeskSpace.DeskSpaceExcelWheelV42",
            owner,(Action)(()=>{}));
        try
        {
            Call(wheel,"Show");
            var canvas=Private<Canvas>(wheel,"canvas");
            Render(canvas,650,650,"excel-child-wheel");
            Assert((bool)wheel.GetType().GetProperty("IsArcOpen")!
                    .GetValue(wheel)!,
                "Excel child wheel was not shown");
            // An in-memory example must open successive attached rings,
            // retaining all ancestors instead of redrawing the same arc.
            var record=Make("DeskSpace.DeskSpaceExcelTemplateV16");
            var model=record.GetType();
            model.GetProperty("Name")!.SetValue(record,"Client Ledger");
            model.GetProperty("GroupPath")!.SetValue(record,"Clients/Audit");
            var formats=(System.Collections.IList)Private<object>(wheel,"formats");
            formats.Add(record);
            Call(wheel,"Activate",1); // Information child of Excel Functions
            var levels=(System.Collections.IList)Private<object>(wheel,"childRings");
            Assert(levels.Count==1,"Information replaced parent instead of opening a ring");
            var first=levels[0]!;
            var activate=wheel.GetType().GetMethod("ActivateRing",Any)!;
            activate.Invoke(wheel,new object[]{first,0}); // Clients
            Assert(levels.Count==2,"Clients group must open a second attached ring");
            var second=levels[1]!;
            activate.Invoke(wheel,new object[]{second,0}); // Audit
            Assert(levels.Count==3,"Audit group must open a third attached ring");
            Assert((bool)wheel.GetType().GetProperty("IsArcOpen")!
                    .GetValue(wheel)!,"Opening nested rings closed original Excel ring");
            for(int i=0;i<3;i++)
            {
                var ring=levels[i]!;
                var surface=(Canvas)ring.GetType().GetField("Surface",Any)!
                    .GetValue(ring)!;
                Render(surface,Math.Max(200,(int)surface.Width),
                    Math.Max(200,(int)surface.Height),"excel-nested-level-"+(i+1));
            }
            Call(wheel,"HideArc");
            Assert(levels.Count==0,"Collapsed wheel retained detached sub-donuts");
            Assert(!(bool)wheel.GetType().GetProperty("IsArcOpen")!
                    .GetValue(wheel)!,
                "Excel child wheel did not close");
        }
        finally
        {
            ((IDisposable)wheel).Dispose();
            owner.Close();
        }
    }
'@
$s=$s.Replace($anchor,$method+[string][char]10+$anchor)
[IO.File]::WriteAllText($smoke,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R42 real WPF tests: simplified saved editor in main Workspace and contextual Excel saved-format child wheel.'
