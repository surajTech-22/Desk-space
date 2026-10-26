$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$ed="$root/DeskSpaceExcelEditorV42.cs"
$ctl="$root/DeskSpaceExcelUnifiedV126.cs"
function Swap([string]$path,[string]$old,[string]$new,[string]$label)
{
    $nl=[string][char]10;$crlf=[string][char]13+$nl
    $text=[IO.File]::ReadAllText($path).Replace($crlf,$nl)
    $old=$old.Replace($crlf,$nl);$new=$new.Replace($crlf,$nl)
    $count=([regex]::Matches($text,[regex]::Escape($old))).Count
    if($count -ne 1){throw "R128 $($label) expected one anchor, found $count"}
    [IO.File]::WriteAllText($path,$text.Replace($old,$new),
        [Text.UTF8Encoding]::new($false))
}
function ReplaceSection([string]$path,[string]$start,[string]$end,
    [string]$body,[string]$label)
{
    $nl=[string][char]10;$crlf=[string][char]13+$nl
    $text=[IO.File]::ReadAllText($path).Replace($crlf,$nl)
    $a=$text.IndexOf($start,[StringComparison]::Ordinal)
    if($a -lt 0 -or $text.IndexOf($start,$a+1,[StringComparison]::Ordinal) -ge 0)
    {throw "R128 $($label) start missing or ambiguous"}
    $b=$text.IndexOf($end,$a+$start.Length,[StringComparison]::Ordinal)
    if($b -lt 0){throw "R128 $($label) end missing"}
    [IO.File]::WriteAllText($path,$text.Substring(0,$a)+
        $body.Replace($crlf,$nl)+$text.Substring($b),
        [Text.UTF8Encoding]::new($false))
}
Swap $ed @'
        trash.SetValue(Button.ContentProperty,"×");
        trash.SetValue(Button.ToolTipProperty,"Delete saved mapping");
        trash.SetValue(Button.WidthProperty,27.0);
        trash.SetValue(Button.HeightProperty,25.0);
        trash.SetValue(Button.MarginProperty,new Thickness(4,1,0,1));
        trash.SetValue(Button.PaddingProperty,new Thickness(0));
'@ @'
        trash.SetValue(Button.ToolTipProperty,"Delete this saved mapping");
        trash.SetValue(Button.WidthProperty,20.0);
        trash.SetValue(Button.HeightProperty,20.0);
        trash.SetValue(Button.MarginProperty,new Thickness(1,0,1,0));
        trash.SetValue(Button.PaddingProperty,new Thickness(0));
        trash.SetValue(Button.BackgroundProperty,Brushes.Transparent);
        trash.SetValue(Button.BorderThicknessProperty,new Thickness(0));
        trash.SetValue(Button.FocusVisualStyleProperty,null);
        trash.SetValue(Button.HorizontalAlignmentProperty,HorizontalAlignment.Right);
        var iconStyle=new Style(typeof(Button));
        var hover=new Trigger{Property=Button.IsMouseOverProperty,Value=true};
        hover.Setters.Add(new Setter(Button.BackgroundProperty,
            new SolidColorBrush(Color.FromRgb(238,242,248))));
        iconStyle.Triggers.Add(hover);
        trash.SetValue(Button.StyleProperty,iconStyle);
        var bin=new FrameworkElementFactory(typeof(System.Windows.Shapes.Path));
        bin.SetValue(System.Windows.Shapes.Path.DataProperty,
            Geometry.Parse("M3,4 L13,4 M6,4 L6,2 L10,2 L10,4 M5,6 L5.6,13 L10.4,13 L11,6 M7,7 L7,11 M9,7 L9,11"));
        bin.SetValue(System.Windows.Shapes.Path.StrokeProperty,
            new SolidColorBrush(Color.FromRgb(103,117,139)));
        bin.SetValue(System.Windows.Shapes.Path.StrokeThicknessProperty,1.3);
        bin.SetValue(System.Windows.Shapes.Path.StrokeLineJoinProperty,PenLineJoin.Round);
        bin.SetValue(System.Windows.Shapes.Path.WidthProperty,15.0);
        bin.SetValue(System.Windows.Shapes.Path.HeightProperty,15.0);
        bin.SetValue(System.Windows.Shapes.Path.StretchProperty,Stretch.Uniform);
        trash.AppendChild(bin);
'@ 'small monochrome vector trash at far end of saved row'
Swap $ed @'
            if((sender as Button)?.DataContext is DeskSpaceExcelTemplateV16 item)
                Run(()=>DeleteUnifiedEntryV126(item.Id,formatModeV126));
'@ @'
            if((sender as Button)?.DataContext is DeskSpaceExcelTemplateV16 item)
            {
                bool fromFormats=formatLibraryV121.Any(x=>
                    object.ReferenceEquals(x,item));
                bool fromData=saved.Any(x=>object.ReferenceEquals(x,item));
                if(fromFormats==fromData)
                    throw new InvalidOperationException(
                        "Saved mapping library origin ambiguous. Refresh.");
                Run(()=>DeleteUnifiedEntryV126(item.Id,fromFormats));
            }
'@ 'delete by originating record rather than current top copy mode'
ReplaceSection $ed '    private void FilterLibraryV42()' '    private void ReloadSavedV42(' @'
    private void FilterLibraryV42()
    {
        if(templates==null || libraryInfoV42==null)return;
        var term=librarySearchV42?.Text.Trim()??"";
        // ONE permanent list spanning BOTH independent encrypted stores.
        // The two top copy choices affect capture and paste rules, never
        // which entries are displayed in the saved-list column.
        var display=saved.Select(x=>new {Entry=x,IsFormat=false})
            .Concat(formatLibraryV121.Select(x=>new {Entry=x,IsFormat=true}))
            .Where(x=>term.Length==0 ||
                x.Entry.Name.Contains(term,StringComparison.OrdinalIgnoreCase) ||
                x.Entry.SourceSheet.Contains(term,StringComparison.OrdinalIgnoreCase) ||
                x.Entry.GroupPath.Contains(term,StringComparison.OrdinalIgnoreCase))
            .OrderBy(x=>x.IsFormat?1:0)
            .ThenBy(x=>x.Entry.GroupPath,StringComparer.OrdinalIgnoreCase)
            .ThenBy(x=>x.Entry.Name,StringComparer.OrdinalIgnoreCase).ToArray();
        var selectedId=formatModeV126?selectedFormatV121?.Id:
            (editingIdV42==Guid.Empty?(Guid?)null:editingIdV42);
        selectionGuardV42=true;
        try
        {
            templates.ItemsSource=display.Select(x=>x.Entry).ToArray();
            templates.SelectedItem=display.FirstOrDefault(x=>
                x.IsFormat==formatModeV126 && x.Entry.Id==selectedId)?.Entry;
        }
        finally{selectionGuardV42=false;}
        libraryInfoV42.Text=(saved.Count+formatLibraryV121.Count)+
            " saved mappings · "+display.Length+" shown ("+
            saved.Count+" information, "+formatLibraryV121.Count+" formats)";
    }
'@ 'merged persistent saved list'
ReplaceSection $ctl '    private void OnUnifiedLibrarySelectedV126()' '    private void NewUnifiedMappingV126()' @'
    private void OnUnifiedLibrarySelectedV126()
    {
        if(selectionGuardV42)return;
        if(templates.SelectedItem is not DeskSpaceExcelTemplateV16 item)return;
        bool isFormat=formatLibraryV121.Any(x=>object.ReferenceEquals(x,item));
        bool isInformation=saved.Any(x=>object.ReferenceEquals(x,item));
        if(isFormat==isInformation)
            throw new InvalidOperationException(
                "Saved entry origin could not be resolved. Refresh mappings.");
        if(hasUnsavedChangesV42 && editingTemplateV42!=null &&
           (isFormat || item.Id!=editingIdV42))
        {
            selectionGuardV42=true;
            try{templates.SelectedItem=saved.FirstOrDefault(x=>x.Id==editingIdV42);}
            finally{selectionGuardV42=false;}
            if(status!=null)status.Text="Save the edited information mapping before selecting another.";
            return;
        }
        // Selecting a saved row switches only the editor and paste engine,
        // not the permanent list of saved items.
        formatModeV126=isFormat;
        if(dataHeadV126!=null)
            dataHeadV126.Visibility=isFormat?Visibility.Collapsed:Visibility.Visible;
        if(formatHeadV126!=null)
            formatHeadV126.Visibility=isFormat?Visibility.Visible:Visibility.Collapsed;
        if(unifiedTabsV126?.Items.Count==2 &&
           unifiedTabsV126.Items[1] is TabItem mapping)
        {
            mapping.Content=isFormat?formatMappingsPaneV126:dataMappingsPaneV126;
            unifiedTabsV126.SelectedIndex=0;
        }
        if(isFormat)
        {
            formatDraftV126=null;
            selectedFormatV121=item;
            if(formatPickerV121!=null)formatPickerV121.SelectedItem=item;
            if(formatNameV124!=null)formatNameV124.Text=item.Name;
            pendingFormatReviewV109=null;
            ShowFormatV126(item);
        }
        else
        {
            OnLibrarySelectedV42();
            if(sheetPreviewHostV101!=null)
                sheetPreviewHostV101.Content=DeskSpaceExcelSheetViewV101.Create(item);
        }
    }
'@ 'click any saved entry to load its correct capture and paste method'
Swap $ctl @'
        if(fromFormatLibrary!=formatModeV126)
            throw new InvalidOperationException("Library changed; reselect the saved mapping.");
'@ @'
        // Delete is scoped to the clicked row's storage, not the active editor.
'@ 'row-based cross-library deletion'
Swap $ctl @'
                ShowFormatV126(null);
            }
            pendingFormatReviewV109=null;
            ReloadFormatsV121();
'@ @'
                if(formatModeV126)ShowFormatV126(null);
            }
            pendingFormatReviewV109=null;
            ReloadFormatsV121();
'@ 'preserve other library preview on format deletion'
Swap $ctl @'
                if(sheetPreviewHostV101!=null)sheetPreviewHostV101.Content=null;
                latest=null;
'@ @'
                if(sheetPreviewHostV101!=null && !formatModeV126)
                    sheetPreviewHostV101.Content=null;
                latest=null;
'@ 'preserve other library preview on information deletion'
Write-Host 'R128 combined library always visible; 15px subtle trash icon; saved item determines paste and delete library.'
