$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r134/DeskSpaceExcelShortcutsV134.cs' "$r/DeskSpaceExcelShortcutsV134.cs" -Force
$editor="$r/DeskSpaceExcelEditorV42.cs"
$worker="$r/DeskSpaceExcelFormatApplyV109.cs"
function Swap([string]$file,[string]$before,[string]$after,[string]$label){
 $lf=[string][char]10;$crlf=[string][char]13+$lf
 $source=[IO.File]::ReadAllText($file).Replace($crlf,$lf)
 $a=$before.Replace($crlf,$lf);$b=$after.Replace($crlf,$lf)
 $hits=([regex]::Matches($source,[regex]::Escape($a))).Count
 if($hits -ne 1){throw "R134 $($label): required one anchor, found $hits"}
 [IO.File]::WriteAllText($file,$source.Replace($a,$b),[Text.UTF8Encoding]::new($false))
}
Swap $editor @'
        shortcutArea.Children.Add(HintV42(
            "Search Windows Excel shortcuts. Select a saved cell row, then "+
            "double-click Ctrl+; or Ctrl+Shift+; to map its paste-time action. "+
            "Other shortcuts are reference-only for safety.",11));
'@ @'
        shortcutArea.Children.Add(HintV42(
            "Search Excel for Windows keyboard shortcuts, including Ctrl+T "+
            "to create a table in Excel. Category and search work together. "+
            "Only fixed date/time shortcuts can be used as automatic data "+
            "mapping actions; other shortcuts are for manual Excel use.",11));
        shortcutArea.Children.Add(HintV42(
            "For automatic format paste, DeskSpace creates real Excel "+
            "ListObjects through its verified Excel interface, NOT by "+
            "sending Ctrl+T. Old mappings showing System._ComObject must "+
            "be recaptured from the original Excel range.",10));
'@ 'clear table shortcut and legacy capture limitation'
Swap $editor @'
        shortcutArea.Children.Add(shortcutFilter);
        var shortcutList=new ListBox{Height=130,FontSize=11};
        shortcutArea.Children.Add(shortcutList);
        void PopulateShortcutList()
        {
            var q=shortcutFilter.Text.Trim();
            shortcutList.ItemsSource=DeskSpaceExcelActionsV46.Shortcuts
                .Where(x=>q.Length==0 ||
                    x.Keys.Contains(q,StringComparison.OrdinalIgnoreCase) ||
                    x.Operation.Contains(q,StringComparison.OrdinalIgnoreCase) ||
                    x.Category.Contains(q,StringComparison.OrdinalIgnoreCase))
                .Select(x=>x.Keys+" — "+x.Operation+" ["+x.Category+"]")
                .ToArray();
        }
        shortcutFilter.TextChanged+=(_,_)=>PopulateShortcutList();
'@ @'
        shortcutArea.Children.Add(shortcutFilter);
        var shortcutCategoriesV134=new ComboBox{
            MinWidth=160,Margin=new Thickness(0,5,0,3),
            ItemsSource=new[]{"All"}.Concat(
                DeskSpaceExcelShortcutsV134.Categories).ToArray(),
            SelectedIndex=0
        };
        shortcutArea.Children.Add(shortcutCategoriesV134);
        var shortcutCountV134=HintV42("",10);
        shortcutArea.Children.Add(shortcutCountV134);
        var shortcutList=new ListBox{Height=210,FontSize=11};
        shortcutArea.Children.Add(shortcutList);
        void PopulateShortcutList()
        {
            var matches=DeskSpaceExcelShortcutsV134.Find(
                shortcutFilter.Text,
                shortcutCategoriesV134.SelectedItem as string??"All");
            shortcutList.ItemsSource=matches.Select(x=>
                x.Keys+" — "+x.Operation+" ["+x.Category+"]").ToArray();
            shortcutCountV134.Text=matches.Length+" matching shortcuts of "+
                DeskSpaceExcelShortcutsV134.Shortcuts.Length+
                " total. Read-only reference unless safely mapped below.";
        }
        shortcutFilter.TextChanged+=(_,_)=>PopulateShortcutList();
        shortcutCategoriesV134.SelectionChanged+=(_,_)=>PopulateShortcutList();
'@ 'category picker, full searchable catalog and match count'
Swap $editor @'
        shortcutArea.Children.Add(ButtonV42(
            "Full Microsoft Excel Windows shortcut documentation",()=>{
'@ @'
        shortcutArea.Children.Add(ButtonV42(
            "Copy selected shortcut",()=>{
                string selected=shortcutList.SelectedItem as string??"";
                if(selected.Length==0){
                    if(status!=null)status.Text="Select a shortcut to copy first.";
                    return;
                }
                string key=selected.Split(" — ",StringSplitOptions.None)[0];
                System.Windows.Clipboard.SetText(key);
                if(status!=null)status.Text="Copied "+key+
                    ". Use the shortcut in the active Microsoft Excel window.";
            }));
        shortcutArea.Children.Add(ButtonV42(
            "Full Microsoft Excel Windows shortcut documentation",()=>{
'@ 'copy shortcut for explicit manual use, no blind keyboard injections'
Swap $worker @'
                problems.Add("Saved table style is invalid or missing. Recapture this format from Excel; existing saved mappings are preserved.");
'@ @'
                problems.Add("This saved mapping has an invalid legacy Excel table-style reference (System._ComObject). Open the original source workbook, select the original cells, then use Format / Formula COPY and Save Mapping to recapture. Ctrl+T alone will not repair saved table metadata. No workbook cells were changed.");
'@ 'actionable legacy table-style failure instead of unsafe keyboard simulation'
Write-Host 'R134: comprehensive category-searchable Excel shortcut reference, copy support, manual Ctrl+T guidance, explicit recapture for legacy COM data. Original COM table creation / preflight and rollback unchanged.'