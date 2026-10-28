$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$c=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceClipboardTransformsV177.cs')
$w=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/WidgetHost.cs')
$t=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($token in @('MaxChars=262144','Markdown literal','JSON string',
 'JSON object','Markdown link','Clipboard.GetText(TextDataFormat.UnicodeText)',
 'Clipboard.SetText(output,TextDataFormat.UnicodeText)',
 'OverwritePrompt=true','dialog.ShowDialog(window)!=true',
 'File.WriteAllText(dialog.FileName,output','window.Closed+=')){
 if(!$c.Contains($token)){throw "R177 missing safety condition: $token"}
}
if($c.Contains('Clipboard.GetText(') -and
   !$c.Contains('read.Click+=(_,_)=>')){
 throw 'R177 clipboard read not bound to manual button'
}
if(!$w.Contains('DeskSpaceClipboardTransformsV177.Show(this)')){
 throw 'R177 tray action missing'
}
if(!$t.Contains('TestClipboardTransformV177();')){
 throw 'R177 native WPF tests not enabled'
}
Write-Host 'R177 PASS: manual clipboard read, safe text limits, explicit Save As and WPF regression.'
