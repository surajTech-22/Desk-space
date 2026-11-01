$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$window=[IO.File]::ReadAllText("$root/DeskSpaceQuickNoteWindowV270.cs")
$policy=[IO.File]::ReadAllText("$root/DeskSpaceQuickNotePolicyV270.cs")
$hub=[IO.File]::ReadAllText("$root/DeskSpaceFeatureHubV199.cs")
$palette=[IO.File]::ReadAllText("$root/DeskSpaceActionPaletteV197.cs")
$native=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($part in @(
 'ShowQuickNoteV270()',
 'MaxBodyChars=16384',
 'TryPrepare(',
 'Clipboard.ContainsText(TextDataFormat.UnicodeText)',
 'read.Click+=',
 'if(readOnlyRecoveryMode)',
 'data.Notes.Add(note);',
 'store.Save(data);',
 'data.Notes.Remove(note);',
 'catch(Exception error)when(error is IOException',
 'NotesList.SelectedItem=note;',
 'Add("Quick note",',
 'new("quicknote","Quick note"',
 'case "quicknote":',
 'TestQuickNoteValidationV270();'
)){
 if(!$window.Contains($part) -and !$policy.Contains($part) -and
    !$hub.Contains($part) -and !$palette.Contains($part) -and
    !$native.Contains($part)){
  throw ('R270 Notes quick capture missing: '+$part)
 }
}
if($window.Contains('Clipboard.GetText(') -and
   $window.IndexOf('Clipboard.GetText(') -lt $window.IndexOf('read.Click+=')){
 throw 'R270 clipboard must never read before explicit user interaction'
}
foreach($unwanted in @('Clipboard.','File.','Directory.','HttpClient',
 'Task.Run','FileSystemWatcher','Process.Start')){
 if($policy.Contains($unwanted)){
  throw ('R270 pure Notes validator must not access host state: '+$unwanted)
 }
}
Write-Host 'R270 STATIC PASS: explicit note edit/save, clipboard only on click, recovery mode block, failed save rollback and original store.'
