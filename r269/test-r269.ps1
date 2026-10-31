$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$search=[IO.File]::ReadAllText("$root/UniversalSearch.cs")
$policy=[IO.File]::ReadAllText("$root/DeskSpaceUniversalSearchPolicyV269.cs")
$palette=[IO.File]::ReadAllText("$root/DeskSpaceActionPaletteV197.cs")
$features=[IO.File]::ReadAllText("$root/DeskSpaceFeatureHubV199.cs")
$tests=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($must in @(
 'DeskSpaceSearchIndexV13.Search(term,snapshot,token)',
 'DeskSpaceUniversalSearchPolicyV269.Filter(indexed,selected)',
 'DeskSpaceUniversalSearchPolicyV269.Accept(',
 'searchCancel?.Cancel();debounce.Stop();debounce.Start();',
 'var actions=new Button{Content="Commands"',
 'DeskSpaceActionPaletteV197.Show(this,false)',
 'category.ItemsSource=DeskSpaceUniversalSearchPolicyV269.Categories;',
 'WidgetSettingsV3.Load()',
 'new("universalsearch","Universal search"',
 'case "universalsearch":',
 'Add("Universal search",',
 'TestUniversalSearchSharedIndexV269();'
)){
 if(!$search.Contains($must) -and !$policy.Contains($must) -and
    !$palette.Contains($must) -and !$features.Contains($must) -and
    !$tests.Contains($must)){
  throw ('R269 search integration missing: '+$must)
 }
}
foreach($old in @(
 'SafeFiles(drive.RootDirectory.FullName,350000,token)',
 'Directory.GetFiles(dir)',
 'Directory.GetDirectories(dir)',
 'CollectSystemResults(term,selected,token)',
 'new[]{"All","Files","Applications","Settings","Notes","Commands"}')){
 if($search.Contains($old)){throw ('R269 obsolete slow or nonfunctional search behavior remains: '+$old)}
}
foreach($bad in @('Directory.Get','File.Read','File.Write','Process.Start','Clipboard.',
 'Task.Run','HttpClient','FileSystemWatcher')){
 if($policy.Contains($bad)){throw ('R269 pure search policy contains system activity: '+$bad)}
}
Write-Host 'R269 STATIC PASS: shared multi-volume index, no per-query disk traversal, usable categories, Search access and stale-request protection.'
