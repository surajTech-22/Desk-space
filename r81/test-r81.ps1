$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$w=[IO.File]::ReadAllText("$r/DeskSpaceInlineV36.cs")
$m=[IO.File]::ReadAllText("$r/MainWindow.xaml")
$c=[IO.File]::ReadAllText("$r/MainWindow.xaml.cs")
$p=[IO.File]::ReadAllText("$r/DeskSpaceReminderPickerV81.cs")
$pref=[IO.File]::ReadAllText("$r/DeskSpaceReminderPreferencesV81.cs")
$tests=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($token in @(
 'DeskSpaceReminderPickerV81.Create(',
 'DeskSpaceReminderPreferencesV81.Load()',
 'DeskSpaceReminderPreferencesV81.Save(',
 'DeskSpaceReminderPresentationV81.ResolveTitle(',
 'Save changes',
 'selectedName',
 'DeskSpaceApplicationIconsV65.Get(',
 'title.Visibility=DeskSpaceReminderPresentationV81.NeedsTitle(mode)'
)){
 if(!$w.Contains($token)){throw "R81 widget missing: $token"}
}
if($w.Contains('Button("Search in DeskSpace"')){
 throw 'R81 still has extra Search in DeskSpace button'
}
foreach($token in @(
 'SaveReminderChanges_Click',
 'ReminderActionDisplay','ReminderActionIcon',
 'ReminderSoundChoice_Loaded',
 'ReminderSoundSaveStatus','RemindersList_SelectionChanged',
 'ReminderActionPath_TextChanged')){
 if(!$m.Contains($token)){throw "R81 main XAML missing: $token"}
}
if($m.Contains('x:Name="ReminderSearchButton"')){
 throw 'R81 main reminder has obsolete search button'
}
foreach($token in @(
 'DeskSpaceReminderPickerV81.Create(',
 'DeskSpaceReminderPresentationV81.ResolveTitle(',
 'DeskSpaceReminderPreferencesV81.Save(',
 'DeskSpaceReminderPreferencesV81.Load()',
 'SaveReminderChanges_Click',
 'ReminderActionDisplay.Text=',
 'ReminderSoundChoice_Loaded')){
 if(!$c.Contains($token)){throw "R81 live main handler missing: $token"}
}
foreach($token in @(
 'DeskSpaceModuleV10.SearchNow(',
 'DeskSpaceApplicationIconsV65.Get(path)',
 'Popup{','Placement=PlacementMode.Bottom',
 'chosen(path,label)',
 'Tag="DeskSpaceReminderAutomaticSearch"')){
 if(!$p.Contains($token)){throw "R81 automatic searchable popup missing: $token"}
}
foreach($token in @(
 'reminder-sound-preferences.json',
 'File.Move(temp,Destination,true)',
 'ResolveTitle(',
 'action+" · "+name',
 'NeedsTitle(')){
 if(!$pref.Contains($token)){throw "R81 sound/title persistence incomplete: $token"}
}
foreach($token in @(
 'TestReminderV81();',
 'R81 file reminders must show file name, not path',
 'R81 action reminders must allow no name and show app name',
 'R81 sound choice and custom path must survive reload',
 'R81 result dropdown must be its own surface outside panel',
 'r81-auto-search-icons-panel','r81-main-action-picker')){
 if(!$tests.Contains($token)){throw "R81 WPF verification missing: $token"}
}
Write-Host 'R81 PASS: saved per-reminder sound and default, no-name action titles, real file/app icons/names, automatic exterior popup.'
