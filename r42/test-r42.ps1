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
 'Excel Mapping','SAVED MAPPINGS','Header / Reference',
 'Value / Input','Value cell','ReferenceCell','Save mapping changes',
 'Rename mapping','Refresh saved mappings','LoadEditorV42(',
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
 'if(selected==6 && ringExpanded',
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
# WPF donut index 6 is Excel and index 7 is Information. Inspect the
# actual updated selector rather than accepting loose tokens elsewhere:
# R29 and R39 changed its structure and older comment-based anchors failed.
$paintStart=$hostSource.IndexOf('    private void UpdateHighlightedSector(',[StringComparison]::Ordinal)
$paintEnd=$hostSource.IndexOf('    private void BuildCompact()', [Math]::Max(0,$paintStart),[StringComparison]::Ordinal)
if($paintStart -lt 0 -or $paintEnd -le $paintStart) {
    throw 'R42 cannot locate updated WPF hover selector'
}
$paint=$hostSource.Substring($paintStart,$paintEnd-$paintStart)
foreach($required in @('if(selected>=0 && selected!=7)',
    'else if(selected==7 && ringExpanded && activeModule==null)',
    'OpenInformationArc();',
    'if(selected==6 && ringExpanded && activeModule==null)',
    'OpenExcelArc();',
    'if(selected>=0 && selected!=6 && excelWheel?.IsArcOpen==true)',
    'excelWheel.HideArc();'))
{
    if(!$paint.Contains($required)){throw "R42 Excel/Information ring-index regression: $required"}
}
if($paint.Contains('if(selected>=0 && selected!=6)')) {
    throw 'R42: R29 still mistakes Excel for Information'
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
# The main wheel must have 13 sectors: seven inside including Excel Functions,
# six outside including the original Information; never an independent Excel
# Clipboard main wedge.
$seg=$hostSource.IndexOf('private static readonly string[] Segments')
$icons=$hostSource.IndexOf('private static readonly string[] SegmentIcons',$seg)
if($seg -lt 0 -or $icons -le $seg){throw 'R42 main wheel labels unavailable'}
$main=$hostSource.Substring($seg,$icons-$seg)
foreach($check in @('"Excel Functions"','"Information"','"Clipboard"')){
    if(!$main.Contains($check)){throw "R42 missing main sector: $check"}
}
if($main.Contains('"Excel Clipboard"')){throw 'R42 duplicate main Excel Clipboard sector remains'}
if(!$hostSource.Contains('double sectors=outer?6:7;')){
    throw 'R42 13-sector input geometry missing'
}
if(!$hostSource.Contains('double step=360.0/(outer?6:7);')){
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
            "Rename mapping",
            "Copy and analyze selected Excel cells",
            "Save mapping changes",
            "Delete mapping",
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
            "Save mapping changes",
            "Preview matching cells"
'@
if(!$s.Contains($old2)){throw 'R42 cannot update R40 main Workspace Excel buttons'}
$s=$s.Replace($old2,$new2)
$anchor='            TestExcelWorkspaceAndShelf();'
if(!$s.Contains($anchor)){throw 'R42 missing WPF smoke caller'}
$s=$s.Replace($anchor,$anchor+[string][char]10+'            TestExcelDirectPasteWheel();')
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
            Call(wheel,"HideArc");
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
