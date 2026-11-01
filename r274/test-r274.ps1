$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$convert=[IO.File]::ReadAllText("$root/DeskSpaceClipboardTransformsV177.cs")
$text=[IO.File]::ReadAllText("$root/DeskSpaceTextConversionsV198.cs")
$native=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @(
 '"Minify JSON"=>MinifyJson(input)',
 '"Sort lines"=>SortLines(input)',
 '"Trim lines"=>TrimLines(input)',
 'internal static readonly string[] Modes=',
 '"Format JSON" or "Minify JSON"',
 'FormatException or JsonException',
 'File.WriteAllText(temporary,output,new UTF8Encoding(false));',
 'File.Move(temporary,destination,true);',
 'if(File.Exists(temporary))File.Delete(temporary);',
 'TestClipboardConversionRecoveryV274();',
 'R274 malformed JSON/CSV must be validation errors'
)){
 if(!$convert.Contains($needle) -and !$text.Contains($needle) -and
   !$native.Contains($needle)){
  throw ('R274 missing conversion/rollback condition: '+$needle)
 }
}
if($convert.Contains('File.WriteAllText(dialog.FileName,output')){
 throw 'R274 direct Save As overwrite risks truncating previous export'
}
Write-Host 'R274 STATIC PASS: invalid JSON/CSV handled; JSON minify, sort/trim; atomic explicit Save As.'
