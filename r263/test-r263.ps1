$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$guard=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceExcelSuggestionReviewV263.cs')
$deck=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceDropShelfV15.cs')
$native=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @(
 'internal static bool SameTarget(',
 'shown.Verified||!current.Verified',
 'shown.WorkbookFullName,current.WorkbookFullName',
 'shown.SheetName,current.SheetName',
 'shown.Changes.Count!=current.Changes.Count',
 'a.PreviousRaw,b.PreviousRaw',
 'a.NewValue,b.NewValue',
 'a.CellAddress,b.CellAddress',
 'var currentProposalV263=await DeskSpaceExcelWorkerV41.PreviewAsync(',
 'if(!DeskSpaceExcelSuggestionReviewV263.SameTarget(',
 'currentProposalV263,currentMappingV263,false',
 'TestExcelSuggestionRecheckV263();'
)){
 if(!$guard.Contains($needle) -and !$deck.Contains($needle) -and
   !$native.Contains($needle)){throw ('R263 stale Excel target guard missing '+$needle)}
}
$preview=$deck.IndexOf('var currentProposalV263=await DeskSpaceExcelWorkerV41.PreviewAsync(')
$apply=$deck.IndexOf('currentProposalV263,currentMappingV263,false')
if($preview -lt 0 -or $apply -lt $preview){
 throw 'R263 must obtain confirmed fresh read-only Excel plan BEFORE ApplyAsync'
}
foreach($bad in @('File.Write','Process.Start','HttpClient',
 'Clipboard.','DispatcherTimer','Excel()','Task.Run(')){
 if($guard.Contains($bad)){
  throw ('R263 comparison must remain pure, non-mutating '+$bad)}
}
Write-Host 'R263 STATIC PASS: exact pre-write Excel proposal revalidation; no stale workbook/row/cell auto-paste.'
