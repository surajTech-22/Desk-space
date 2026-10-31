$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$preview=[IO.File]::ReadAllText("$root/DeskSpacePreviewChooserV273.cs")
$hub=[IO.File]::ReadAllText("$root/DeskSpaceFeatureHubV199.cs")
$palette=[IO.File]::ReadAllText("$root/DeskSpaceActionPaletteV197.cs")
$native=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @(
 'internal const long MaxPreviewFileBytes=256L*1024*1024;',
 'FileAttributes.ReparsePoint|FileAttributes.System',
 'DeskSpaceFilePreviewV85.Create(path)',
 'new OpenFileDialog{',
 'new Forms.FolderBrowserDialog{',
 'Content="Choose file"',
 'Content="Choose folder"',
 'previewHost.Content=null;',
 'Add("Preview file or folder",',
 'new("previewfile","Preview file or folder"',
 'case "previewfile":',
 'TestExplicitPreviewChooserV273();'
)){
 if(!$preview.Contains($needle) -and !$hub.Contains($needle) -and
    !$palette.Contains($needle) -and !$native.Contains($needle)){
  throw ('R273 missing file preview requirement: '+$needle)
 }
}
foreach($unsafe in @('Task.Run','Directory.GetFiles','ZipFile.Extract',
 'Process.Start','HttpClient','Clipboard.Get','File.Write')){
 if($preview.Contains($unsafe)){
  throw ('R273 chooser must only delegate to existing preview: '+$unsafe)
 }
}
Write-Host 'R273 STATIC PASS: no automatic file read, explicit file/folder chooser, 256MB/reparse preflight, native test.'
