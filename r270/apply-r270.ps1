$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r270/DeskSpaceQuickNotePolicyV270.cs' -Destination "$root/DeskSpaceQuickNotePolicyV270.cs" -Force
Copy-Item -LiteralPath 'r270/DeskSpaceQuickNoteWindowV270.cs' -Destination "$root/DeskSpaceQuickNoteWindowV270.cs" -Force
$lf=[string][char]10;$crlf=[string][char]13+$lf
function Swap([string]$path,[string]$old,[string]$new,[string]$why)
{
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $source=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
 $n=([regex]::Matches($source,[regex]::Escape($old))).Count
 if($n -ne 1){throw "R270 $($why): expected one anchor, got $n"}
 [IO.File]::WriteAllText($path,$source.Replace($old,$new),
     [Text.UTF8Encoding]::new($false))
}
Swap "$root/DeskSpaceFeatureHubV199.cs" @'
        Add("Notes","Open the existing notes tab, when present.",
            ()=>SelectExistingFeatureTabV199("Notes"));
'@ @'
        Add("Notes","Open the existing notes tab, when present.",
            ()=>SelectExistingFeatureTabV199("Notes"));
        Add("Quick note",
            "Write a new note or explicitly paste clipboard text, preview it, then save locally.",
            ShowQuickNoteV270);
'@ 'DeskSpaceFeatureHubV199.cs Quick Note route'
Swap "$root/DeskSpaceActionPaletteV197.cs" @'
        new("notes","Notes","Open the existing notes tab","Workspace"),
'@ @'
        new("notes","Notes","Open the existing notes tab","Workspace"),
        new("quicknote","Quick note","Write and save a new DeskSpace note with explicit consent","Workspace"),
'@ 'DeskSpaceActionPaletteV197.cs Quick Note route'
Swap "$root/DeskSpaceActionPaletteV197.cs" @'
            case "notes":
                _=DeskSpaceFeatureNavigationV200.OpenAsync(owner,"NOTES");
                break;
'@ @'
            case "notes":
                _=DeskSpaceFeatureNavigationV200.OpenAsync(owner,"NOTES");
                break;
            case "quicknote":
                var main=Application.Current.Windows.OfType<MainWindow>()
                    .FirstOrDefault(w=>w.IsVisible);
                if(main!=null)main.ShowQuickNoteV270();
                else _=DeskSpaceFeatureNavigationV200.OpenAsync(owner,"NOTES");
                break;
'@ 'DeskSpaceActionPaletteV197.cs Quick Note route'
Write-Host 'R270: Quick Note explicitly edits/persists a Workspace note, on-demand optional clipboard read, read-only recovery guard and original data rollback.'
