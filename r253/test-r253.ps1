$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$s=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceResourceBudgetV253.cs')
$health=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceComponentHealthV175.cs')
$t=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @(
 'internal const long PrivateMemoryBudget=1024L*1024*1024',
 'internal const int HandleBudget=5000',
 'internal const int ThreadBudget=256',
 'Process.GetCurrentProcess()',
 'process.PrivateMemorySize64',
 'process.Threads.Count',
 'DeskSpaceResourceBudgetV253.Current()',
 'TestResourceBudgetV253();'
)){
 if(!$s.Contains($needle) -and !$health.Contains($needle) -and !$t.Contains($needle)){
  throw ('R253 advisory process budget missing '+$needle)
 }
}
foreach($unsafe in @('DispatcherTimer','Task.Run','Thread.Sleep','Process.Kill',
 'File.WriteAll','HttpClient','Process.Start','SystemEvents')){
 if($s.Contains($unsafe)){throw ('R253 on-demand budget cannot collect or change in background '+$unsafe)}
}
Write-Host 'R253 STATIC PASS: on-demand memory, handle and thread advisory budgets; no background poll.'
