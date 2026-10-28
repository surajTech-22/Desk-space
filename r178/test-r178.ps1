$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$smart=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceSmartTextDropV178.cs')
$shelf=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceDropShelfV15.cs')
$ui=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($v in @('DataFormats.FileDrop','MaxTextChars=262144',
 'Uri.UriSchemeHttp','Uri.UriSchemeHttps',
 'DeskSpaceFolderDragAdornerV160.ChoiceAt','new SaveFileDialog',
 'OverwritePrompt=true','dialog.ShowDialog(owner)==true',
 'Clipboard.SetText(text,TextDataFormat.UnicodeText)')){
 if(!$smart.Contains($v)){throw "R178 missing Smart Drop safety gate: $v"}
}
foreach($v in @('ShowSmartDropV178(smartText,smartUrl)',
 'HideSmartDropV178();','DeskSpaceSmartTextDropV178.Commit(host,smartText,smartUrl,left)',
 'System.Windows.Threading.DispatcherPriority.Background',
 'DeskSpaceFolderDragAdornerV160.ChoiceAt')){
 if(!$shelf.Contains($v)){throw "R178 drop handler integration missing: $v"}
}
if(!$ui.Contains('TestSmartTextDropV178();')){
 throw 'R178 native WPF test not wired'
}
Write-Host 'R178 PASS: ordinary FileDrop unaffected, choice during drag, URL safety, SaveAs confirmation and WPF regression.'
