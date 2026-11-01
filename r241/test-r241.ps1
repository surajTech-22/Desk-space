$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$s=[IO.File]::ReadAllText("$root/DeskSpaceDocxPreviewV241.cs")
$ui=[IO.File]::ReadAllText("$root/DeskSpaceFilePreviewV85.cs")
$test=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @(
 'internal const long MaxFileBytes=16L*1024*1024',
 'internal const long MaxXmlBytes=2L*1024*1024',
 'internal const int MaxCharacters=3000',
 'FileAttributes.ReparsePoint',
 'ZipArchiveMode.Read',
 'DtdProcessing=DtdProcessing.Prohibit',
 'XmlResolver=null',
 'MaxCharactersInDocument=MaxXmlBytes',
 'source.Read(buffer',
 'vbaProject.bin',
 'Task.Run(()=>ReadDocument(path),scope.Token)',
 'panel.Unloaded+=',
 'excerpt.Clear();'
)){
 if(!$s.Contains($needle)){throw ('R241 DOCX security bound missing '+$needle)}
}
foreach($forbidden in @('Process.Start','File.WriteAll','File.Delete(',
 'Directory.Delete','Clipboard.Set','Clipboard.Get','ExtractToDirectory',
 'Assembly.Load','HttpClient','WebClient')){
 if($s.Contains($forbidden)){throw ('R241 must never execute document/upload/change file: '+$forbidden)}
}
if(!$ui.Contains('DeskSpaceDocxPreviewV241.Create(path)') -or
   !$ui.Contains('DeskSpaceTextPreviewV240.Create(path)') -or
   !$ui.Contains('DeskSpacePdfPreviewV217.Create(path)') -or
   !$test.Contains('TestWordDeckPreviewV241();')){
 throw 'R241 Deck preview integration or native test missing'
}
Write-Host 'R241 STATIC PASS: DOCX only, no macros, DTD or ZIP extraction; file and clipboard remain untouched.'
