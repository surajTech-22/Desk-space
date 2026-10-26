$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
function Swap([string]$path,[string]$before,[string]$after,[string]$label) {
    $lf=[string][char]10
    $crlf=[string][char]13+$lf
    $body=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
    $before=$before.Replace($crlf,$lf)
    $after=$after.Replace($crlf,$lf)
    $count=([regex]::Matches($body,[regex]::Escape($before))).Count
    if($count -ne 1){throw "R124 $label expected exactly one anchor; found $count"}
    [IO.File]::WriteAllText($path,$body.Replace($before,$after),
        [Text.UTF8Encoding]::new($false))
}
$editor="$root/DeskSpaceExcelEditorV42.cs"
Swap $editor @'
    private DeskSpaceExcelTemplateV16? selectedFormatV121;
    private DeskSpaceExcelFormatApplyV109.Review? pendingFormatReviewV109;
'@ @'
    private DeskSpaceExcelTemplateV16? selectedFormatV121;
    // R124: two actual Workspace routes with different data stores and writers.
    private TabControl? routesV124;
    private TextBox? formatNameV124;
    private TextBlock? formatSummaryV124;
    private DeskSpaceExcelFormatApplyV109.Review? pendingFormatReviewV109;
'@ 'independent Workspace routes'
Swap $editor @'
        mainActions.Children.Add(RequiresExcel(
            "Copy and analyze selected Excel cells",()=>Run(Capture),true));
'@ @'
        mainActions.Children.Add(ButtonV42(
            "Data mappings · copy / paste",()=>{
                if(routesV124!=null)routesV124.SelectedIndex=0;
            },true));
        mainActions.Children.Add(ButtonV42(
            "Formats & formulas · copy / paste",()=>{
                if(routesV124!=null)routesV124.SelectedIndex=1;
            },true));
'@ 'top-level distinct copy/paste routes'
Swap $editor @'
        pasteButtons.Children.Add(RequiresExcel("Preview matching cells",()=>Run(Preview)));
        pasteButtons.Children.Add(RequiresExcel("Apply autofill",()=>Run(Apply),true));
'@ @'
        // Header-based paste and user-anchored paste are two explicit
        // destination modes, sharing only the safe VALUES writer. The
        // formatting gate and formats library are never consulted here.
        pasteButtons.Children.Add(RequiresExcel(
            "Preview saved-header match",()=>{
                if(manual!=null)manual.IsChecked=false;
                Run(Preview);
            }));
        pasteButtons.Children.Add(RequiresExcel(
            "Preview at selected Excel cell",()=>{
                if(manual!=null)manual.IsChecked=true;
                Run(Preview);
            },true));
        pasteButtons.Children.Add(RequiresExcel(
            "Paste reviewed mapping values",()=>Run(Apply),true));
        footer.Children.Add(HintV42(
            "Direct paste: select one start cell in Excel, then Preview at selected Excel cell. "+
            "Only saved value fields are eligible; existing formulas and values "+
            "remain protected unless you explicitly permit replacement.",11));
'@ 'primary manual mapping paste without hidden settings'
Swap $editor @'
        formatPickerV121.SelectionChanged+=(_,_)=>{
            selectedFormatV121=formatPickerV121.SelectedItem
                as DeskSpaceExcelTemplateV16;
            pendingFormatReviewV109=null;
            formatTemplateIdV109=Guid.Empty;
        };
'@ @'
        formatPickerV121.SelectionChanged+=(_,_)=>{
            selectedFormatV121=formatPickerV121.SelectedItem
                as DeskSpaceExcelTemplateV16;
            pendingFormatReviewV109=null;
            formatTemplateIdV109=Guid.Empty;
            if(selectedFormatV121!=null) {
                if(formatNameV124!=null)formatNameV124.Text=selectedFormatV121.Name;
                if(formatSummaryV124!=null)formatSummaryV124.Text=
                    "Saved format "+selectedFormatV121.Name+" · "+
                    selectedFormatV121.SheetVisualV101?.Rows+" rows × "+
                    selectedFormatV121.SheetVisualV101?.Columns+
                    " columns. No source values stored in the format library.";
            }
        };
'@ 'separate format selection and summary'
Swap $editor @'
        footer.Children.Add(formatGroup);
        ReloadFormatsV121();
'@ @'
        // The format controls must NOT live in the data-mapping editor
        // footer. They are an independent top-level Workspace page.
        var formatPageV124=new StackPanel{Margin=new Thickness(16,16,16,8)};
        formatPageV124.Children.Add(new TextBlock{
            Text="Copy formats & formulas",FontSize=20,
            FontWeight=FontWeights.SemiBold,Foreground=NavyV42,
            Margin=new Thickness(1,2,1,12)
        });
        formatPageV124.Children.Add(HintV42(
            "Select cells in Microsoft Excel, name this format, then Copy. "+
            "It saves cell-specific formatting, formula structure, tables, "+
            "dimensions and input types to a separate encrypted library. "+
            "Mapping values and reference headers are never copied into it.",12));
        formatPageV124.Children.Add(new TextBlock{
            Text="Format name (independent of mapping data name)",
            FontWeight=FontWeights.SemiBold,Foreground=NavyV42,
            Margin=new Thickness(1,5,1,4)
        });
        formatNameV124=Field("New format name");
        formatNameV124.Margin=new Thickness(0,0,0,12);
        formatPageV124.Children.Add(formatNameV124);
        formatPageV124.Children.Add(formatGroup);
        formatSummaryV124=HintV42(
            "Select a saved format, or copy selected Excel cells to create one. "+
            "Preview and approval are required before any formatting is written.",12);
        formatPageV124.Children.Add(formatSummaryV124);
        formatPageV124.Children.Add(HintV42(
            "Paste rule: whole destination must be empty or contain a full "+
            "matching footprint. Mixed filled/empty layouts are blocked. "+
            "Existing workbook content and formulas are protected.",11));
        ReloadFormatsV121();
'@ 'separate formats main page and independent name'
Swap $editor @'
        var title=mappingNameV42?.Text.Trim();
        if(string.IsNullOrWhiteSpace(title))
            throw new InvalidOperationException(
                "Enter a format template name in Data name first.");
'@ @'
        var title=formatNameV124?.Text.Trim();
        if(string.IsNullOrWhiteSpace(title))
            throw new InvalidOperationException(
                "Enter a Format name on the Formats & formulas page first.");
'@ 'no dependency on mapping editor name'
Swap $editor @'
        ReloadFormatsV121(captured.Id);
        if(status!=null)status.Text=
'@ @'
        ReloadFormatsV121(captured.Id);
        if(formatSummaryV124!=null)formatSummaryV124.Text=
            "Saved "+title+" to the independent encrypted format library ("+
            captured.SheetVisualV101.Rows+" × "+
            captured.SheetVisualV101.Columns+
            "). Original cell values were not copied.";
        if(status!=null)status.Text=
'@ 'format capture visible confirmation'
Swap $editor @'
        root.Children.Add(content);

        root.IsVisibleChanged+=(_,e)=>{
'@ @'
        // The data-mapping editor and the format-only library now occupy
        // DIFFERENT top-level tabs. Neither uses the other's capture button.
        routesV124=new TabControl{Margin=new Thickness(5,0,5,4)};
        routesV124.Items.Add(new TabItem{
            Header="Data mappings · values",Content=content
        });
        routesV124.Items.Add(new TabItem{
            Header="Formats & formulas · separate library",
            Content=new ScrollViewer{
                VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
                Content=formatPageV124
            }
        });
        routesV124.SelectionChanged+=(_,e)=>{
            if(!ReferenceEquals(e.Source,routesV124))return;
            var mappings=routesV124.SelectedIndex==0;
            advanced.Visibility=mappings?Visibility.Visible:Visibility.Collapsed;
            shortcutPane.Visibility=mappings?Visibility.Visible:Visibility.Collapsed;
        };
        root.Children.Add(routesV124);

        root.IsVisibleChanged+=(_,e)=>{
'@ 'real top-level tabs rather than nested mapping-format controls'
# Separate formatting preview/apply status from the now-hidden value mapping footer.
Swap $editor @'
            pendingFormatReviewV109=checkedTarget;
            formatTemplateIdV109=entry.Id;
'@ @'
            pendingFormatReviewV109=checkedTarget;
            formatTemplateIdV109=entry.Id;
            if(formatSummaryV124!=null)formatSummaryV124.Text=
                checkedTarget.Eligible?
                    "READY: "+checkedTarget.ToString()+
                    " · "+checkedTarget.Workbook+" / "+checkedTarget.Destination:
                    "BLOCKED: "+checkedTarget.ToString()+
                    " · No workbook modifications.";
'@ 'visible format-preview result on format page'
Swap $editor @'
            string outcome=await DeskSpaceExcelWorkerV41.ApplyFormatAsync(entry,approved);
            if(status!=null)status.Text=outcome;
'@ @'
            string outcome=await DeskSpaceExcelWorkerV41.ApplyFormatAsync(entry,approved);
            if(status!=null)status.Text=outcome;
            if(formatSummaryV124!=null)formatSummaryV124.Text=outcome;
'@ 'visible format result on format page'
# R124 fixes the widget route that always passed selection:false and overwrote
# existing cells. Never infer a destination from a click on the widget.
$wheel="$root/DeskSpaceExcelWheelV42.cs"
Swap $wheel @'
            // Automatic matching is strictly required. Never assume the
            // currently selected Excel cell is the right destination.
            var preview=await DeskSpaceExcelWorkerV41.PreviewAsync(entry,
                selection:false,overwrite:true);
            if(!preview.Verified)
                throw new InvalidOperationException(
                    "The open Excel sheet did not uniquely match the "+
                    "saved reference cells. No changes were made. "+
                    preview.Reason);
'@ @'
            // First try the verified header/value mapping signature.
            // When labels differ, offer a DIFFERENT explicit selected-cell
            // paste route; never silently guess a new destination or write.
            var preview=await DeskSpaceExcelWorkerV41.PreviewAsync(entry,
                selection:false,overwrite:true);
            bool selectedCellPasteV124=false;
            if(!preview.Verified)
            {
                var consent=MessageBox.Show(owner,
                    "No unique saved header signature was found. "+
                    "Would you like to preview the mapping at the cell "+
                    "you explicitly selected in Excel?\\n\\n"+
                    "This will NOT copy original headers or formatting. "+
                    "Existing cell values and formulas will be protected. "+
                    "You will confirm the destination before pasting.",
                    "DeskSpace · use selected-cell value mapping",
                    MessageBoxButton.YesNo,MessageBoxImage.Question);
                if(consent!=MessageBoxResult.Yes)
                    throw new InvalidOperationException(
                        "Header match was not unique and selected-cell paste "+
                        "was not approved. Nothing changed.");
                selectedCellPasteV124=true;
                preview=await DeskSpaceExcelWorkerV41.PreviewAsync(entry,
                    selection:true,overwrite:false);
                if(!preview.Verified)
                    throw new InvalidOperationException(
                        "The selected Excel target could not be verified: "+
                        preview.Reason);
            }
'@ 'ask before falling back to explicitly selected cell'
Swap $wheel @'
            Status("Excel paste · "+entry.Name,
                "Matched "+preview.SheetName+". Backing up the workbook and "+
                "pasting "+ready+" values…");
            applying=true;
            var backup=await DeskSpaceExcelWorkerV41.ApplyAsync(
                preview,entry,overwrite:true);
'@ @'
            if(selectedCellPasteV124)
            {
                var accept=MessageBox.Show(owner,
                    "Paste "+ready+" mapping values into "+
                    preview.SheetName+" starting at row "+
                    preview.StartRow+", column "+preview.StartColumn+
                    "?\\n\\nExisting destination values and formulas "+
                    "will be skipped. A workbook backup is made first.",
                    "DeskSpace · confirm selected-cell paste",
                    MessageBoxButton.YesNo,MessageBoxImage.Warning);
                if(accept!=MessageBoxResult.Yes)
                {
                    Status("Excel paste cancelled",
                        "No destination cells were changed.");
                    return;
                }
            }
            Status("Excel paste · "+entry.Name,
                "Verified "+preview.SheetName+". Backing up the workbook and "+
                "pasting "+ready+" values…");
            applying=true;
            var backup=await DeskSpaceExcelWorkerV41.ApplyAsync(
                preview,entry,overwrite:!selectedCellPasteV124);
'@ 'manual destination preview confirmation and no overwrite'
Write-Host 'R124 routes: separate main copy/paste pages, independent format naming/library, explicit selected-cell value paste, safe widget fallback.'
