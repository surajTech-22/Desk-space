$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r273/DeskSpacePreviewChooserV273.cs' -Destination "$root/DeskSpacePreviewChooserV273.cs" -Force
$lf=[string][char]10;$crlf=[string][char]13+$lf
function Swap([string]$path,[string]$old,[string]$next,[string]$label){
 $source=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
 $old=$old.Replace($crlf,$lf);$next=$next.Replace($crlf,$lf)
 $count=([regex]::Matches($source,[regex]::Escape($old))).Count
 if($count -ne 1){throw ('R273 '+$label+' expected one anchor, saw '+$count)}
 [IO.File]::WriteAllText($path,$source.Replace($old,$next),
    [Text.UTF8Encoding]::new($false))
}
Swap "$root/DeskSpaceFeatureHubV199.cs" @'
        Section("Files");
        Add("Duplicate-file inspector",
'@ @'
        Section("Files");
        Add("Preview file or folder",
            "Choose a PDF, Word, ZIP, image, text file or folder for a safe read-only preview.",
            ()=>DeskSpacePreviewChooserV273.Show(this));
        Add("Duplicate-file inspector",
'@ 'direct preview UI wiring'
Swap "$root/DeskSpaceActionPaletteV197.cs" @'
        new("downloads","Open Downloads folder","Show the ordinary Windows Downloads folder","Files")
'@ @'
        new("previewfile","Preview file or folder",
            "Choose a local file or folder and inspect it without opening an external app","Files"),
        new("downloads","Open Downloads folder","Show the ordinary Windows Downloads folder","Files")
'@ 'direct preview UI wiring'
Swap "$root/DeskSpaceActionPaletteV197.cs" @'
        switch(command.Id)
        {
            case "universalsearch":
'@ @'
        switch(command.Id)
        {
            case "previewfile":
                DeskSpacePreviewChooserV273.Show(owner);
                break;
            case "universalsearch":
'@ 'direct preview UI wiring'
Write-Host 'R273: user chooses file or folder for existing bounded Deck preview; no background crawler.'