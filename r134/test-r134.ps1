$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$c=[IO.File]::ReadAllText("$r/DeskSpaceExcelShortcutsV134.cs")
$e=[IO.File]::ReadAllText("$r/DeskSpaceExcelEditorV42.cs")
$w=[IO.File]::ReadAllText("$r/DeskSpaceExcelFormatApplyV109.cs")
$rows=[regex]::Matches($c,'(?m)^\s*\("[^"]+","[^"]+","[^"]+"\),\s*$')
if($rows.Count -lt 160){throw "R134 not comprehensive enough: $($rows.Count)"}
$keys=@($rows | ForEach-Object {
    if($_.Value -match '"([^"]+)","([^"]+)","([^"]+)"') {
        [pscustomobject]@{Keys=$Matches[1];Category=$Matches[3]}
    }
})
$duplicate=@($keys|Group-Object Keys,Category|Where-Object Count -gt 1)
if($duplicate.Count -gt 0){throw 'R134 catalog has duplicated shortcut/category pairs'}
foreach($name in @('Ctrl+T','Ctrl+L','Ctrl+Alt+V','Ctrl+Alt+V, T, Enter',
'Ctrl+1','Ctrl+Shift+L','Shift+F11','Alt+=','Ctrl+F3','F2',
'Ctrl+;','Ctrl+Shift+;','Ctrl+B','Ctrl+S','Ctrl+PageDown')){
    if($keys.Keys -notcontains $name){throw "R134 important shortcut missing: $name"}
}
foreach($str in @('DeskSpaceExcelShortcutsV134.Find(','shortcutCategoriesV134',
 'shortcutCountV134.Text=','Copy selected shortcut',
 'Clipboard.SetText(key)','Full Microsoft Excel Windows shortcut documentation',
 'DeskSpaceExcelActionsV46.FromShortcut(key)')){
    if(!$e.Contains($str)){throw "R134 editor integration missing: $str"}
}
foreach($str in @('PreflightTableStylesV131(','SetTableStyleV131(',
 'DeskSpaceExcelFullRangeGateV107.Evaluate(from,to)',
 'RestoreAfterFailure(','ListObjects.Add(',
 'System._ComObject). Open the original source workbook')){
    if(!$w.Contains($str)){throw "R134 Excel COM safety/repair guidance missing: $str"}
}
if($c.Contains('SendKeys(') -or $c.Contains('Process.Start(') -or
 $c.Contains('ListObjects.Add(')){
    throw 'R134 shortcut catalog must be reference-only, without executable effects.'
}
Write-Host "R134 PASS: $($rows.Count) keyboard shortcut entries, searchable category picker, copy action, Ctrl+T and paste-special, safe mapping whitelist, real Excel table COM path still guarded."