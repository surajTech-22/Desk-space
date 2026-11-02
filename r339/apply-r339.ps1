$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$palette=Join-Path $root 'DeskSpaceActionPaletteV197.cs'
$universal=Join-Path $root 'UniversalSearch.cs'
foreach($file in @($palette,$universal)){
 if(!(Test-Path -LiteralPath $file)){throw "R339 missing shipping source: $file"}
}
foreach($name in @('DeskSpaceSavedSearchesV339.cs','DeskSpaceFileVerifyV339.cs',
 'DeskSpaceDuplicateInspectorV339.cs')){
 Copy-Item ("r339/"+$name) (Join-Path $root $name) -Force
}
$lf=[string][char]10
$crlf=[string][char]13+$lf
function ReplaceOne([string]$path,[string]$old,[string]$new,[string]$label)
{
 $source=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $hits=([regex]::Matches($source,[regex]::Escape($old))).Count
 if($hits -ne 1){throw "R339 $label expected 1 match; found $hits"}
 [IO.File]::WriteAllText($path,$source.Replace($old,$new),
     [Text.UTF8Encoding]::new($false))
}
ReplaceOne $palette @'
        new("downloads","Open Downloads folder","Show the ordinary Windows Downloads folder","Files")
'@ @'
        new("filehash","Verify SHA-256 of file",
            "Select a file and compare a SHA-256 checksum locally, without modifying it","Files"),
        new("duplicatefiles","Inspect duplicate files",
            "Compare same-size files by SHA-256, preview duplicate groups, never delete","Files"),
        new("downloads","Open Downloads folder","Show the ordinary Windows Downloads folder","Files")
'@ 'new commands in existing Action Palette'
ReplaceOne $palette @'
            case "previewfile":
                DeskSpacePreviewChooserV273.Show(owner);
                break;
'@ @'
            case "previewfile":
                DeskSpacePreviewChooserV273.Show(owner);
                break;
            case "filehash":
                DeskSpaceFileVerifyV339.Show(owner);
                break;
            case "duplicatefiles":
                DeskSpaceDuplicateInspectorV339.Show(owner);
                break;
'@ 'implement palette command routing'
# Insert saved searches immediately following existing R326 guidance and
# before status. No new search engine, no repeated index or query history.
ReplaceOne $universal @'
        top.Children.Add(filtersHelpV326);top.Children.Add(status);
'@ @'
        top.Children.Add(filtersHelpV326);
        // User-initiated saved searches only. Never record queries silently.
        var savedPresetsV339=new DeskSpaceSavedSearchesV339();
        var presetLineV339=new WrapPanel{Margin=new Thickness(12,0,12,8)};
        var presetPickerV339=new ComboBox{MinWidth=180,MaxWidth=230,
            Margin=new Thickness(0,0,8,0),DisplayMemberPath="Name",
            ToolTip="Choose an explicitly saved search preset"};
        var presetNameV339=new TextBox{MinWidth=140,MaxWidth=190,
            Margin=new Thickness(0,0,8,0),MaxLength=70,
            ToolTip="Name for your saved search"};
        var presetSaveV339=new Button{Content="Save search",
            Padding=new Thickness(8,4,8,4),Margin=new Thickness(0,0,8,0)};
        var presetDeleteV339=new Button{Content="Delete preset",
            Padding=new Thickness(8,4,8,4)};
        presetLineV339.Children.Add(presetPickerV339);
        presetLineV339.Children.Add(presetNameV339);
        presetLineV339.Children.Add(presetSaveV339);
        presetLineV339.Children.Add(presetDeleteV339);
        top.Children.Add(presetLineV339);
        top.Children.Add(status);
        void ReloadPresetsV339()
        {
            try{presetPickerV339.ItemsSource=savedPresetsV339.Load();}
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException
                or InvalidDataException or System.Security.SecurityException)
            {status.Text="Cannot read saved searches: "+ex.GetType().Name;}
        }
        presetPickerV339.SelectionChanged+=(_,_)=>{
            if(presetPickerV339.SelectedItem is DeskSpaceSavedSearchesV339.Preset item)
            {
                presetNameV339.Text=item.Name;
                query.Text=item.Query;
            }
        };
        presetSaveV339.Click+=(_,_)=>{
            try
            {
                savedPresetsV339.Save(presetNameV339.Text,query.Text);
                ReloadPresetsV339();status.Text="Search saved locally.";
            }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException
                or ArgumentException or InvalidOperationException
                or InvalidDataException or System.Security.SecurityException)
            {status.Text="Save failed: "+ex.Message;}
        };
        presetDeleteV339.Click+=(_,_)=>{
            if(presetPickerV339.SelectedItem is not DeskSpaceSavedSearchesV339.Preset item)
            {status.Text="Choose a saved search first.";return;}
            if(MessageBox.Show(this,"Delete saved search "+item.Name+"?",
               "DeskSpace",MessageBoxButton.YesNo,MessageBoxImage.Question)
                !=MessageBoxResult.Yes)return;
            try
            {
                savedPresetsV339.Delete(item.Name);
                ReloadPresetsV339();status.Text="Preset deleted.";
            }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException
                or InvalidDataException or System.Security.SecurityException)
            {status.Text="Delete failed: "+ex.GetType().Name;}
        };
        ReloadPresetsV339();
'@ 'saved search UI in existing Workspace Universal Search'
$paletteContent=Get-Content $palette -Raw
$searchContent=Get-Content $universal -Raw
if(!$paletteContent.Contains('DeskSpaceFileVerifyV339.Show(owner)') -or
   !$paletteContent.Contains('DeskSpaceDuplicateInspectorV339.Show(owner)') -or
   !$searchContent.Contains('savedPresetsV339.Save(')){
 throw 'R339 feature UI not connected in shipping source'
}
Write-Host 'R339: Three independent manual productivity features integrated: persisted search presets, SHA-256 verification, read-only duplicate inspector.'
