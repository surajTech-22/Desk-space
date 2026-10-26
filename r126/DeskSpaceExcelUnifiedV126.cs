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
        if(!formatModeV126){OnLibrarySelectedV42();return;}
        if(templates.SelectedItem is not DeskSpaceExcelTemplateV16 item)return;
        var original=formatLibraryV121.FirstOrDefault(x=>x.Id==item.Id);
        if(original==null)return;
        formatDraftV126=null;
        selectedFormatV121=original;
        if(formatPickerV121!=null)formatPickerV121.SelectedItem=original;
        if(formatNameV124!=null)formatNameV124.Text=original.Name;
        pendingFormatReviewV109=null;
        ShowFormatV126(original);
    }
    private void NewUnifiedMappingV126()
    {
        if(formatModeV126)
        {
            formatDraftV126=null;
            selectedFormatV121=null;
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
        // Only the structural visual survives in the format library.
        snapshot.Cells.Clear();
        snapshot.Anchors.Clear();
        formatDraftV126=snapshot;
        selectedFormatV121=null;
        if(formatPickerV121!=null)formatPickerV121.SelectedItem=null;
        if(formatNameV124!=null)formatNameV124.Text=title;
        pendingFormatReviewV109=null;
        ShowFormatV126(snapshot);
        FilterLibraryV42();
        if(unifiedTabsV126!=null)unifiedTabsV126.SelectedIndex=0;
        if(status!=null)status.Text=
            "Format copied into an unsaved draft. Inspect it, then Save Mapping.";
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
        edited.Cells.Clear();edited.Anchors.Clear();
        if(records.Any(x=>x.Id!=edited.Id &&
           x.Name.Equals(title,StringComparison.OrdinalIgnoreCase)))
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
        formatDraftV126=null;
        ReloadFormatsV121(edited.Id);
        selectedFormatV121=formatLibraryV121.FirstOrDefault(x=>x.Id==edited.Id);
        FilterLibraryV42();
        ShowFormatV126(selectedFormatV121);
        if(status!=null)status.Text="Saved '"+title+"' to the format mapping library.";
    }
    private void DeleteUnifiedEntryV126(Guid id,bool fromFormatLibrary)
    {
        if(fromFormatLibrary!=formatModeV126)
            throw new InvalidOperationException("Library changed; reselect the saved mapping.");
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
                ShowFormatV126(null);
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
                if(sheetPreviewHostV101!=null)sheetPreviewHostV101.Content=null;
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
