using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace DeskSpace;

// Independent format-formula editor. Header/value data grid never calls this
// store or these actions; capture/save/paste have separate explicit buttons.
internal sealed partial class DeskSpaceExcelV16
{
    private ListBox? formatLibraryV121;
    private TextBox? formatNameV121;
    private TextBlock? formatStatusV121;
    private DeskSpaceExcelTemplateV16? formatDraftV121;
    private Guid loadedFormatIdV121=Guid.Empty;
    private bool capturingFormatV121;

    private FrameworkElement BuildFormatControlsV121()
    {
        var area=new StackPanel{Margin=new Thickness(5,5,5,7)};
        area.Children.Add(HintV42(
            "Independent format/formula template. Saves cell styles, table "+
            "structure, number/date formats and original formulas; NEVER "+
            "stores or pastes transaction values. Existing header/value "+
            "mappings are not modified.",11));
        formatNameV121=Field("Name of saved format / formula layout");
        area.Children.Add(formatNameV121);
        formatLibraryV121=new ListBox{Height=70,
            DisplayMemberPath="Name",Margin=new Thickness(3,2,3,4)};
        formatLibraryV121.SelectionChanged+=(_,_)=>{
            pendingFormatReviewV109=null;
            formatTemplateIdV109=Guid.Empty;
            if(formatLibraryV121.SelectedItem is DeskSpaceExcelTemplateV16 item)
            {
                loadedFormatIdV121=item.Id;
                if(formatNameV121!=null)formatNameV121.Text=item.Name;
                if(formatStatusV121!=null)formatStatusV121.Text=
                    item.SourceSheet+" · "+item.SourceAddress+
                    " · format & formula only (no saved cell values)";
            }
        };
        area.Children.Add(formatLibraryV121);
        var saveActions=new WrapPanel();
        saveActions.Children.Add(RequiresExcel(
            "Capture selected formats & formulas",
            ()=>_ = CaptureFormatSnapshotV121(),true));
        saveActions.Children.Add(ButtonV42(
            "Save format template",()=>Run(SaveFormatSnapshotV121),true));
        saveActions.Children.Add(ButtonV42(
            "Import format from old mapping",
            ()=>Run(ImportOldFormatV121)));
        saveActions.Children.Add(ButtonV42(
            "Refresh formats",()=>Run(RefreshFormatsV121)));
        area.Children.Add(saveActions);
        var useActions=new WrapPanel();
        useActions.Children.Add(RequiresExcel(
            "Preview format destination",PreviewFormatV109));
        useActions.Children.Add(RequiresExcel(
            "Apply formats & formulas",ApplyFormatV109,true));
        area.Children.Add(useActions);
        formatStatusV121=HintV42(
            "Select one range in Excel, capture, save this format, then "+
            "select a destination and preview before applying.",11);
        area.Children.Add(formatStatusV121);
        var expander=new Expander{
            Header="Formats & Formulas — separate capture, save and paste",
            IsExpanded=false,Margin=new Thickness(3,3,3,5),
            Content=area
        };
        try{RefreshFormatsV121();}
        catch(Exception error)
        {
            // Corrupt DPAPI storage must be reported, never silently reset.
            formatStatusV121.Text="Saved format library unavailable: "+error.Message;
        }
        return expander;
    }
    private void RefreshFormatsV121()
    {
        if(formatLibraryV121==null)return;
        var id=(formatLibraryV121.SelectedItem as DeskSpaceExcelTemplateV16)?.Id
            ?? loadedFormatIdV121;
        var models=DeskSpaceExcelFormatStoreV121.Load()
            .OrderBy(x=>x.Name,StringComparer.OrdinalIgnoreCase).ToArray();
        formatLibraryV121.ItemsSource=models;
        formatLibraryV121.SelectedItem=models.FirstOrDefault(x=>x.Id==id);
        if(formatStatusV121!=null)
            formatStatusV121.Text=models.Length+
                " saved format-only templates. Value mappings are separate.";
    }
    private async Task CaptureFormatSnapshotV121()
    {
        if(capturingFormatV121)return;
        capturingFormatV121=true;
        try
        {
            string name=formatNameV121?.Text.Trim()??"";
            if(name.Length==0)
                throw new InvalidOperationException("Enter the format template name first.");
            if(formatStatusV121!=null)
                formatStatusV121.Text="Reading selected Excel cell styles and formulas…";
            var raw=await DeskSpaceExcelWorkerV41.CaptureAsync(name,"",true);
            // Stop all current cell data at this boundary. Only styling and
            // exact formula definitions survive in the separate snapshot.
            formatDraftV121=DeskSpaceExcelFormatStoreV121.OnlyFormatting(raw,name);
            loadedFormatIdV121=Guid.Empty;
            if(formatLibraryV121!=null)formatLibraryV121.SelectedItem=null;
            pendingFormatReviewV109=null;
            if(formatStatusV121!=null)
                formatStatusV121.Text="Captured "+raw.RowCount+" × "+
                    raw.ColumnCount+" cells; ONLY styles/formulas retained. "+
                    "Click Save format template to keep the snapshot.";
        }
        catch(Exception error)
        {
            formatDraftV121=null;
            if(formatStatusV121!=null)
                formatStatusV121.Text="Format capture failed: "+error.Message;
        }
        finally{capturingFormatV121=false;}
    }
    private void SaveFormatSnapshotV121()
    {
        if(formatDraftV121==null)
            throw new InvalidOperationException(
                "Capture selected Excel formats & formulas before saving.");
        string title=formatNameV121?.Text.Trim()??"";
        var savedTemplate=DeskSpaceExcelFormatStoreV121.Save(
            formatDraftV121,title);
        loadedFormatIdV121=savedTemplate.Id;
        formatDraftV121=null;
        RefreshFormatsV121();
        if(formatStatusV121!=null)
            formatStatusV121.Text="SAVED FORMAT ONLY: "+savedTemplate.Name+
                ". Original workbook and header/value mapping store unchanged.";
    }
    private void ImportOldFormatV121()
    {
        var source=editingTemplateV42 ??
            throw new InvalidOperationException(
                "Choose an old saved header/value mapping to extract its formatting.");
        if(source.SheetVisualV101==null)
            throw new InvalidOperationException(
                "This old mapping has no captured Excel formatting.");
        string name=formatNameV121?.Text.Trim();
        if(string.IsNullOrWhiteSpace(name))name=source.Name+" (format)";
        // Explicit import only. The old mapping remains unchanged and keeps
        // its stable ID in templates.dpapi; the new format gets a fresh ID.
        var copy=DeskSpaceExcelFormatStoreV121.OnlyFormatting(source,name);
        copy.Id=Guid.NewGuid();
        var savedTemplate=DeskSpaceExcelFormatStoreV121.Save(copy,name);
        loadedFormatIdV121=savedTemplate.Id;
        formatDraftV121=null;
        RefreshFormatsV121();
        if(formatStatusV121!=null)
            formatStatusV121.Text="Imported a separate format-only copy; "+
                "the original header/value mapping was not changed.";
    }
    private DeskSpaceExcelTemplateV16 SelectedFormatV121()
    {
        if(formatDraftV121!=null)
            throw new InvalidOperationException(
                "Save the new formatting capture before preview/apply.");
        if(formatLibraryV121?.SelectedItem is not DeskSpaceExcelTemplateV16 format)
            throw new InvalidOperationException(
                "Choose a saved FORMATS & FORMULAS template first.");
        if(format.Cells.Count>0 || format.Anchors.Count>0 ||
           format.SheetVisualV101==null)
            throw new InvalidDataException(
                "This is not a format-only snapshot. No paste was performed.");
        return format;
    }
}
