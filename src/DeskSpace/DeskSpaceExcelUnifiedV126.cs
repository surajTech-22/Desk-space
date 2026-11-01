using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Data;

namespace DeskSpace;

// R126: One Excel Workspace. The active COPY route chooses the source
// library, display, save/delete operations and (critically) paste engine.
// Never combine header-matched value paste with format-footprint paste.
internal sealed partial class DeskSpaceExcelV16
{
    private bool formatModeV126;
    private DeskSpaceExcelTemplateV16? formatDraftV126;
    private TabControl? unifiedTabsV126;
    private StackPanel? formatHeadV126,dataHeadV126;
    private DockPanel? dataMappingsPaneV126,formatMappingsPaneV126;
    private DataGrid? formatDetailsV126;

    private static string NormalizeGroupV130(string raw)
    {
        var parts=(raw??"").Replace('\\','/')
            .Split('/',StringSplitOptions.RemoveEmptyEntries|
                       StringSplitOptions.TrimEntries);
        if(parts.Length>8 || parts.Any(p=>p.Length>60))
            throw new InvalidOperationException(
                "Collections allow up to eight levels of 60 characters.");
        return string.Join("/",parts);
    }
    private void ActivateLibraryV126(bool formats)
    {
        if(formats!=formatModeV126 &&
           hasUnsavedChangesV42 && editingTemplateV42!=null)
            throw new InvalidOperationException(
                "Save information mapping edits before switching libraries.");
        formatModeV126=formats;
        if(dataHeadV126!=null)
            dataHeadV126.Visibility=formats?Visibility.Collapsed:Visibility.Visible;
        if(formatHeadV126!=null)
            formatHeadV126.Visibility=formats?Visibility.Visible:Visibility.Collapsed;
        if(unifiedTabsV126?.Items.Count==2 &&
           unifiedTabsV126.Items[1] is TabItem map)
        {
            map.Content=formats?formatMappingsPaneV126:dataMappingsPaneV126;
            unifiedTabsV126.SelectedIndex=0;
        }
        FilterLibraryV42();
        if(formats)ShowFormatV126(formatDraftV126??selectedFormatV121);
        else if(sheetPreviewHostV101!=null)
            sheetPreviewHostV101.Content=editingTemplateV42==null?null:
                DeskSpaceExcelSheetViewV101.Create(editingTemplateV42);
    }
    private void ShowFormatV126(DeskSpaceExcelTemplateV16? item)
    {
        if(sheetPreviewHostV101!=null)
            sheetPreviewHostV101.Content=item==null?null:
                DeskSpaceExcelSheetViewV101.Create(item);
        if(formatDetailsV126!=null)
            formatDetailsV126.ItemsSource=item?.SheetVisualV101?.Cells
                .OrderBy(x=>x.Row).ThenBy(x=>x.Column).ToArray();
        if(formatSummaryV124!=null)
            formatSummaryV124.Text=item==null?
                "Choose a saved format or copy selected Excel cells to inspect.":
                "Viewing "+item.Name+" · "+item.SheetVisualV101?.Rows+" × "+
                item.SheetVisualV101?.Columns+" · formatting/formulas only.";
    }
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
            if(formatGroupV130!=null)formatGroupV130.Text=item.GroupPath;
            pendingFormatReviewV109=null;
            ShowFormatV126(item);
        }
        else
        {
            OnLibrarySelectedV42();
            if(sheetPreviewHostV101!=null)
                sheetPreviewHostV101.Content=DeskSpaceExcelSheetViewV101.Create(item);
        }
    }    private void NewUnifiedMappingV126()
    {
        if(formatModeV126)
        {
            formatDraftV126=null;
            selectedFormatV121=null;
            if(formatGroupV130!=null)formatGroupV130.Clear();
            if(formatPickerV121!=null)formatPickerV121.SelectedItem=null;
            if(formatNameV124!=null)formatNameV124.Clear();
            pendingFormatReviewV109=null;
            ShowFormatV126(null);
        }
        else
        {
            if(hasUnsavedChangesV42 && editingTemplateV42!=null)
                throw new InvalidOperationException(
                    "Save current information edits before making a new mapping.");
            editingTemplateV42=null;editingIdV42=Guid.Empty;
            editingDraftV42=true;hasUnsavedChangesV42=false;
            mappingRowsV42.Clear();
            if(mappingGridV42!=null)mappingGridV42.ItemsSource=null;
            mappingNameV42?.Clear();mappingGroupV42?.Clear();
            if(sheetPreviewHostV101!=null)sheetPreviewHostV101.Content=null;
            latest=null;
        }
        FilterLibraryV42();
        if(unifiedTabsV126!=null)unifiedTabsV126.SelectedIndex=0;
        if(status!=null)status.Text="New mapping draft. Copy selected Excel cells above.";
    }
    private void CaptureFormatDraftV126()
    {
        string title=formatNameV124?.Text.Trim()??"";
        if(title.Length==0)title="New format mapping";
        var snapshot=DeskSpaceExcelBridgeV16.Capture(
            title,range?.Text??"",true);
        if(snapshot.SheetVisualV101==null)
            throw new InvalidOperationException(
                "Capture did not return a complete native Excel format snapshot.");
        DeskSpaceExcelFormulaIntegrityV142.ValidateCapture(snapshot);
        // Only the structural visual survives in the format library.
        snapshot.Cells.Clear();
        snapshot.Anchors.Clear();
        formatDraftV126=snapshot;
        selectedFormatV121=null;
        if(formatPickerV121!=null)formatPickerV121.SelectedItem=null;
        if(formatNameV124!=null)formatNameV124.Text=title;
        snapshot.GroupPath=NormalizeGroupV130(formatGroupV130?.Text??"");
        pendingFormatReviewV109=null;
        ShowFormatV126(snapshot);
        FilterLibraryV42();
        if(unifiedTabsV126!=null)unifiedTabsV126.SelectedIndex=0;
        if(status!=null)status.Text=
            "UNSAVED DRAFT · "+snapshot.SheetVisualV101.Cells.Count(c=>
                c.StoredType=="Formula")+
            " actual formulas captured; 0 source values saved. "+
            "Select Save Mapping to persist and verify.";
    }
    // A real, independent Excel formula-only paste operation.
    // Values, formatting and formula preview remain separate features.
    private async void PasteSpecialFormulasV144()
    {
        try
        {
            if(!formatModeV126)
                throw new InvalidOperationException(
                    "Select Format / Formula mappings and a saved format first.");
            var entry=FormatSourceV109();
            if(status!=null)
                status.Text="Checking selected Excel range for saved formulas…";
            var result=await DeskSpaceExcelWorkerV41.PasteSpecialFormulasAsync(entry);
            if(status!=null)status.Text=result;
        }
        catch(Exception error)
        {
            if(status!=null)
                status.Text="FORMULA PASTE BLOCKED: "+error.Message;
        }
    }

    private void SaveUnifiedMappingV126()
    {
        if(!formatModeV126){SaveEditorV42();return;}
        var source=formatDraftV126??selectedFormatV121;
        if(source?.SheetVisualV101==null)
            throw new InvalidOperationException(
                "Copy Excel formats/formulas or select a saved format first.");
        string title=formatNameV124?.Text.Trim()??"";
        if(title.Length==0||title.Length>140)
            throw new InvalidOperationException("Enter a format name of 1–140 characters.");
        var records=DeskSpaceExcelStoreV16.LoadFormats();
        var edited=CloneV42(source);
        edited.Name=title;
        edited.GroupPath=NormalizeGroupV130(formatGroupV130?.Text??"");
        edited.Cells.Clear();edited.Anchors.Clear();
        DeskSpaceExcelFormulaIntegrityV142.ValidateCapture(edited);
        if(records.Any(x=>x.Id!=edited.Id &&
           x.Name.Equals(title,StringComparison.OrdinalIgnoreCase) &&
           string.Equals(NormalizeGroupV130(x.GroupPath),edited.GroupPath,
               StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException(
                "A saved format has this name already. Choose it to edit.");
        int index=records.FindIndex(x=>x.Id==edited.Id);
        if(formatDraftV126==null && index<0)
            throw new InvalidOperationException(
                "Saved format was changed or removed. Refresh the library.");
        if(formatDraftV126!=null && index>=0)
            throw new InvalidOperationException("New draft ID conflicts with a saved entry.");
        if(index<0)records.Add(edited);else records[index]=edited;
        DeskSpaceExcelStoreV16.SaveFormats(records);
        var persistedV142=DeskSpaceExcelStoreV16.LoadFormats()
            .FirstOrDefault(x=>x.Id==edited.Id);
        DeskSpaceExcelFormulaIntegrityV142.ValidateRoundtrip(
            edited,persistedV142);
        formatDraftV126=null;
        ReloadFormatsV121(edited.Id);
        selectedFormatV121=formatLibraryV121.FirstOrDefault(x=>x.Id==edited.Id);
        FilterLibraryV42();
        ShowFormatV126(selectedFormatV121);
        if(status!=null)
        {
            var verified=selectedFormatV121?.SheetVisualV101 ??
                throw new InvalidDataException(
                    "Saved mapping could not be reloaded. No success reported.");
            status.Text="SAVED & RELOADED '"+title+"' · "+
                verified.Cells.Count(c=>c.StoredType=="Formula")+
                " complete formula expressions; 0 source input values saved. "+
                "Use Mappings → Paste Special · Formulas to paste formulas "+
                "(different from formatting-only paste).";
        }
    }
    private void DeleteUnifiedEntryV126(Guid id,bool fromFormatLibrary)
    {
        // Delete is scoped to the clicked row's storage, not the active editor.
        if(fromFormatLibrary)
        {
            var records=DeskSpaceExcelStoreV16.LoadFormats();
            var item=records.FirstOrDefault(x=>x.Id==id) ??
                throw new InvalidOperationException("Saved format no longer exists.");
            if(MessageBox.Show("Delete saved format '"+item.Name+"'?",
                "DeskSpace – Delete Mapping",MessageBoxButton.YesNo,
                MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;
            records.Remove(item);
            DeskSpaceExcelStoreV16.SaveFormats(records);
            if(selectedFormatV121?.Id==id)
            {
                selectedFormatV121=null;
                formatDraftV126=null;
                if(formatPickerV121!=null)formatPickerV121.SelectedItem=null;
                if(formatNameV124!=null)formatNameV124.Clear();
                if(formatModeV126)ShowFormatV126(null);
            }
            pendingFormatReviewV109=null;
            ReloadFormatsV121();
        }
        else
        {
            if(hasUnsavedChangesV42)
                throw new InvalidOperationException(
                    "Save edited information before deleting a saved entry.");
            var records=DeskSpaceExcelStoreV16.Load();
            var item=records.FirstOrDefault(x=>x.Id==id) ??
                throw new InvalidOperationException("Saved information no longer exists.");
            if(MessageBox.Show("Delete saved information mapping '"+item.Name+"'?",
                "DeskSpace – Delete Mapping",MessageBoxButton.YesNo,
                MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;
            records.Remove(item);
            DeskSpaceExcelStoreV16.Save(records);
            saved.Clear();saved.AddRange(records);
            if(editingIdV42==id)
            {
                editingTemplateV42=null;editingIdV42=Guid.Empty;
                mappingRowsV42.Clear();
                if(mappingGridV42!=null)mappingGridV42.ItemsSource=null;
                if(sheetPreviewHostV101!=null && !formatModeV126)
                    sheetPreviewHostV101.Content=null;
                latest=null;
            }
        }
        FilterLibraryV42();
        if(status!=null)status.Text="Deleted saved mapping from its own library. Excel unchanged.";
    }
    private void RefreshUnifiedV126()
    {
        ReloadSavedV42(true);
        ReloadFormatsV121();
        FilterLibraryV42();
    }
}
