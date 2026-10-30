$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r200/DeskSpaceFeatureNavigationV200.cs' -Destination "$root/DeskSpaceFeatureNavigationV200.cs" -Force
$lf=[string][char]10;$crlf=[string][char]13+$lf
function Swap([string]$path,[string]$before,[string]$after,[string]$why)
{
    $before=$before.Replace($crlf,$lf);$after=$after.Replace($crlf,$lf)
    $s=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
    $n=([regex]::Matches($s,[regex]::Escape($before))).Count
    if($n -ne 1){throw ("R200 "+$why+" expected one anchor, found "+$n)}
    [IO.File]::WriteAllText($path,$s.Replace($before,$after),
        [Text.UTF8Encoding]::new($false))
}
$p="$root/DeskSpaceWorkspaceNavigationV49.cs"
Swap $p @'
            if(Environment.GetCommandLineArgs().Any(x=>
                x.Equals("--excel-mapping",StringComparison.OrdinalIgnoreCase)))
                ShowExcelMappingFromWheelV49();
'@ @'
            if(Environment.GetCommandLineArgs().Any(x=>
                x.Equals("--excel-mapping",StringComparison.OrdinalIgnoreCase)))
                ShowExcelMappingFromWheelV49();
            foreach(var arg in Environment.GetCommandLineArgs())
            {
                if(arg.Equals("--features",StringComparison.OrdinalIgnoreCase))
                    ShowFeatureDestinationV200("FEATURES");
                else if(arg.Equals("--notes",StringComparison.OrdinalIgnoreCase))
                    ShowFeatureDestinationV200("NOTES");
                else if(arg.Equals("--reminders",StringComparison.OrdinalIgnoreCase))
                    ShowFeatureDestinationV200("REMINDERS");
            }
'@ 'safe explicit command-line destination'
Swap $p @'
                if(string.Equals(command,"EXCEL_MAPPING",StringComparison.Ordinal))
                    await Dispatcher.InvokeAsync(ShowExcelMappingFromWheelV49);
'@ @'
                if(string.Equals(command,"EXCEL_MAPPING",StringComparison.Ordinal))
                    await Dispatcher.InvokeAsync(ShowExcelMappingFromWheelV49);
                else if(DeskSpaceFeatureNavigationV200.IsAllowed(command))
                    await Dispatcher.InvokeAsync(()=>
                        ShowFeatureDestinationV200(command!));
'@ 'same-user IPC whitelist'
$p="$root/DeskSpaceActionPaletteV197.cs"
Swap $p @'
    internal static readonly IReadOnlyList<Command> Commands=new Command[]{
        new("status",
'@ @'
    internal static readonly IReadOnlyList<Command> Commands=new Command[]{
        new("features","DeskSpace Features","Open the visible Features tab in Workspace","Workspace"),
        new("excel","Excel mappings","Open saved Excel mapping and format tools","Excel"),
        new("reminders","Reminders","Open the existing reminders tab","Workspace"),
        new("notes","Notes","Open the existing notes tab","Workspace"),
        new("status",
'@ 'all feature destinations in Search Palette'
Swap $p @'
        switch(command.Id)
        {
            case "status":
'@ @'
        switch(command.Id)
        {
            case "features":
                _=DeskSpaceFeatureNavigationV200.OpenAsync(owner,"FEATURES");
                break;
            case "excel":
                _=DeskSpaceFeatureNavigationV200.OpenExcelAsync(owner);
                break;
            case "reminders":
                _=DeskSpaceFeatureNavigationV200.OpenAsync(owner,"REMINDERS");
                break;
            case "notes":
                _=DeskSpaceFeatureNavigationV200.OpenAsync(owner,"NOTES");
                break;
            case "status":
'@ 'direct user-triggered action dispatch'
Write-Host 'R200: Widget and Workspace share approved feature destinations over existing same-user IPC, with no silent app mutation.'
