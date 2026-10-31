$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$s=[IO.File]::ReadAllText("$root/DeskSpaceTextPreviewV240.cs")
$ui=[IO.File]::ReadAllText("$root/DeskSpaceFilePreviewV85.cs")
$test=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @(
 'internal const long MaxFileBytes=2L*1024*1024',
 'internal const int MaxCharacters=3600',
 'Supports(string? extension)',
 'FileAttributes.ReparsePoint',
 'FileMode.Open,',
 'FileAccess.Read,FileShare.ReadWrite',
 'new UTF8Encoding(false,true)',
 'char.IsControl(c)',
 'Task.Run(()=>ReadSnippet(path),scope.Token)',
 'panel.Unloaded+=',
 'preview.Clear();'
)){
 if(!$s.Contains($needle)){throw ('R240 missing file preview guard: '+$needle)}
}
foreach($forbidden in @('Process.Start','File.WriteAll','File.Delete(',
 'Directory.Delete','Clipboard.Set','Clipboard.Get','ZipFile.Extract','PowerShell.Create',
 'Registry.','WebClient','HttpClient')){
 if($s.Contains($forbidden)){throw ('R240 text preview must not mutate files, execute content or access private state: '+$forbidden)}
}
if(!$ui.Contains('DeskSpaceTextPreviewV240.Create(path)') -or
   !$ui.Contains('DeskSpacePdfPreviewV217.Create(path)') -or
   !$test.Contains('TestTextDeckPreviewV240();')){
 throw 'R240 missing existing Deck integration, PDF preservation or Windows test'
}
Write-Host 'R240 STATIC PASS: literal, user-opened and bounded plaintext preview with strict format policy; existing PDF/ZIP branch retained.'
