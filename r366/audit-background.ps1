$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
# R366 exhaustive static inventory of checked-in application C# sources.
# This classifies recurring activity for review; a match is NOT proof of a bug.
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\src\DeskSpace'))
$files=@(Get-ChildItem -LiteralPath $root -Recurse -File -Filter '*.cs' |
  Where-Object { $_.FullName -notmatch '[\\/](obj|bin)[\\/]' })
$patterns=[ordered]@{
  Timers='\b(?:new\s+(?:DispatcherTimer|Timer)\b|new\s+System\.Threading\.Timer\b)'
  FileWatchers='\bnew\s+FileSystemWatcher\b'
  WaitLoops='\bwhile\s*\(|\bTask\.Delay\s*\(|\bThread\.Sleep\s*\('
  DirectoryTraversal='\b(?:Directory\.(?:EnumerateFiles|GetFiles|GetFileSystemEntries|EnumerateDirectories|EnumerateFileSystemEntries)|DirectoryInfo\.EnumerateFiles)\s*\('
  ProcessEnumeration='\b(?:Process\.GetProcesses|SnapshotWindows|EnumWindows)\s*\('
  FileWrites='\b(?:File\.(?:WriteAllText|AppendAllText|AppendAllLines|WriteAllBytes|Move|Copy|Delete)|\.ExecuteNonQuery)\s*\('
}
$results=@(foreach($file in $files){
  $source=[IO.File]::ReadAllText($file.FullName)
  $entry=[ordered]@{File=$file.FullName.Substring($root.Length+1).Replace('\','/')}
  $total=0
  foreach($category in $patterns.Keys){
    $n=[regex]::Matches($source,$patterns[$category]).Count
    $entry[$category]=$n
    $total+=$n
  }
  $entry.Total=$total
  [pscustomobject]$entry
})
$counts=[ordered]@{}
foreach($category in $patterns.Keys){
  $counts[$category]=[int](($results | Measure-Object -Property $category -Sum).Sum)
}
$ranking=@($results | Sort-Object Total -Descending | Select-Object -First 35)
$report=[ordered]@{
  Schema='DeskSpace-background-static-audit-r366'
  SourceFiles=$files.Count
  PatternCounts=$counts
  RankedFiles=$ranking
  Findings=@(
    'CONFIRMED: R330 duplicate changed-path events and unnecessary SQLite writes',
    'CONFIRMED: R330 index path exclusion invoked Directory.CreateDirectory on each event',
    'CONFIRMED: R330 overflow recovery could request repeated full-drive crawls',
    'CONFIRMED: clock weather styling allocated and reapplied WPF resources once per idle tick',
    'REVIEW: R324 full-drive crawl starts at every search index startup; keep full PC coverage',
    'REVIEW: recent-file watcher uses bounded recursive recovery every 45 seconds per instance',
    'REVIEW: Running Apps deck uses 5-second window snapshot while Deck is visible',
    'EXPECTED: widget clock, reminders, media, updater, monitors and Excel controls have timed work'
  )
  Note='Counts classify static source patterns, not the number of defects or runtime calls.'
}
$out=Join-Path $PSScriptRoot 'background-static-inventory.json'
$report|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $out -Encoding utf8
Write-Host "R366 inventory: $($files.Count) checked-in C# source files"
foreach($category in $patterns.Keys){Write-Host ("  {0}: {1}" -f $category,$counts[$category])}
Write-Host 'Top recurring-work source candidates:'
$ranking|Format-Table -AutoSize|Out-String|Write-Host
if($files.Count -lt 150){throw 'R366 audited too few source files'}
if($counts.Timers -lt 10){throw 'R366 timer inventory suspiciously incomplete'}
Write-Host 'R366 static source inventory PASS (classification, not performance proof)'
