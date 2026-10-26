$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$ed=[IO.File]::ReadAllText("$r/DeskSpaceExcelEditorV42.cs")
$ctrl=[IO.File]::ReadAllText("$r/DeskSpaceExcelUnifiedV126.cs")
$wheel=[IO.File]::ReadAllText("$r/DeskSpaceExcelWheelV42.cs")
$format=[IO.File]::ReadAllText("$r/DeskSpaceExcelFormatApplyV109.cs")
$store=[IO.File]::ReadAllText("$r/DeskSpaceExcelStoreV16.cs")
$worker=[IO.File]::ReadAllText("$r/DeskSpaceExcelFormatWorkerV130.cs")
$bridge=[IO.File]::ReadAllText("$r/DeskSpaceExcelBridgeV16.cs")
function Required([string]$where,[string]$text,[string]$file)
{
    if(!$where.Contains($text)){throw "R130 $($file) missing: $text"}
}
foreach($s in @('Text="Collection name (group related format mappings)"',
    'formatHeadV126.Children.Add(formatGroupV130);',
    'grouped.GroupDescriptions.Add(new PropertyGroupDescription("GroupPath"))',
    'templates.GroupStyle.Add(new GroupStyle',
    'saved.Select(x=>new {Entry=x,IsFormat=false})',
    'formatLibraryV121.Select(x=>new {Entry=x,IsFormat=true})',
    'Header="Captured Sheet"','Header="Mappings"'))
{Required $ed $s 'editor'}
foreach($s in @(
    'snapshot.GroupPath=NormalizeGroupV130',
    'edited.GroupPath=NormalizeGroupV130',
    'string.Equals(NormalizeGroupV130(x.GroupPath),edited.GroupPath',
    'if(formatGroupV130!=null)formatGroupV130.Clear();',
    'if(formatGroupV130!=null)formatGroupV130.Text=item.GroupPath;',
    'DeskSpaceExcelStoreV16.SaveFormats(records)',
    'DeskSpaceExcelStoreV16.Save(records)'))
{Required $ctrl $s 'group-save'}
foreach($s in @(
    'DeskSpaceExcelStoreV16.Load()',
    'DeskSpaceExcelStoreV16.LoadFormats()',
    'entries.Add(("Information","information","",null));',
    'entries.Add(("Formats & formulas","formats","",null));',
    'formatTemplatesV130',
    'formatBranch?"format":"mapping"',
    'formatBranch?FormatRootV130+"/"+path:path',
    'if(item.Command=="information" || item.Command=="formats")',
    'if(formatOnly)_=PasteFormatV130Async(template);',
    'if(formatOnly)_=PasteFormatV130Async(entry);',
    'DeskSpaceExcelFormatWorkerV130.PreviewAsync(entry)',
    'DeskSpaceExcelFormatWorkerV130.ApplyAsync(',
    'MessageBoxButton.YesNo,MessageBoxImage.Warning',
    'selection:false,overwrite:true'))
{Required $wheel $s 'donut'}
foreach($s in @('DeskSpaceExcelFormatApplyV109.Preview(template)',
    'DeskSpaceExcelFormatApplyV109.Apply(template,approval)',
    'thread.SetApartmentState(ApartmentState.STA);'))
{Required $worker $s 'format STA'}
foreach($s in @('DeskSpaceExcelFullRangeGateV107.Evaluate(from,to)',
    'book.SaveCopyAs(backup)','if(!live.Review.Eligible)'))
{Required $format $s 'format safety'}
Required $bridge 'Information paste requires verified matching headers.' 'strict data'
Required $store 'formats.dpapi' 'format library'
Required $store 'templates.dpapi' 'information library'
Write-Host 'R130 PASS: both Collection groupings and nested donut groups; format records never rendered as information, format preview/approval/backup path isolated, header-matched data unchanged.'
