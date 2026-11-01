$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$s=[IO.File]::ReadAllText('DeskSpace_Windows_R1/DeskSpace/DeskSpaceClipboardTransformsV177.cs')
$t=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($needle in @(
 'Content="Clear local preview"',
 'source="";output="";',
 'input.Clear();result.Clear();',
 'copy.IsEnabled=false;save.IsEnabled=false;',
 'Windows clipboard unchanged.',
 'clipboardControls.Children.Add(clear);',
 'window.Closed+=(_,_)=>{',
 'TestClipboardPreviewClearV245();'
)){
 if(!$s.Contains($needle) -and !$t.Contains($needle))
 {throw ("R245 clipboard privacy/local clear missing "+$needle)}
}
$block=$s.Substring($s.IndexOf('clear.Click+='),
 $s.IndexOf('read.Click+=')-$s.IndexOf('clear.Click+='))
foreach($unsafe in @('Clipboard.Clear','Clipboard.Set','Clipboard.Get',
 'File.Delete','File.WriteAll','Process.Start','HttpClient')){
 if($block.Contains($unsafe)){throw 'R245 clear must only erase local buffers: '+$unsafe}
}
Write-Host 'R245 STATIC PASS: local clipboard clear leaves Windows clipboard intact and blocks prior output actions.'
