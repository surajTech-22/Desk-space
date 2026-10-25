using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace DeskSpace;

// Active Excel suggestions are read-only until a deliberate click, a fresh
// preview and a confirmation. They live inside the existing Deck, not as a
// separate popup, and NEVER inspect browser tabs, usernames or passwords.
internal sealed class DeskSpaceExcelDeckSuggestionsV123 : IDisposable
{
    [DllImport("ole32.dll",CharSet=CharSet.Unicode,PreserveSig=false)]
    private static extern void CLSIDFromProgID(string progId,out Guid clsid);
    [DllImport("oleaut32.dll",PreserveSig=false)]
    private static extern void GetActiveObject(ref Guid clsid,IntPtr reserved,
        [MarshalAs(UnmanagedType.IUnknown)]out object application);
    private sealed record Suggestion(Guid Id,string Kind,string Name,
        string Book,string Sheet,int Items,string Preview);
    private readonly Window owner;
    private readonly DispatcherTimer timer;
    private StackPanel? container;
    private List<Suggestion> suggestions=new();
    private bool running,disposed,applying;
    internal bool HasSuggestions=>suggestions.Count>0;
    internal DeskSpaceExcelDeckSuggestionsV123(Window owner)
    {
        this.owner=owner;
        timer=new DispatcherTimer(DispatcherPriority.Background,owner.Dispatcher){
            Interval=TimeSpan.FromSeconds(12)
        };
        timer.Tick+=(_,_)=>_ = Refresh();
        timer.Start();
        _=Refresh();
    }
    internal void Bind(StackPanel? panel)
    {
        container=panel;
        Draw();
    }
    private static bool HasLiveExcel()
    {
        try
        {
            CLSIDFromProgID("Excel.Application",out Guid clsid);
            GetActiveObject(ref clsid,IntPtr.Zero,out object app);
            dynamic excel=app;
            return excel.ActiveWorkbook!=null && excel.ActiveSheet!=null;
        }
        catch{return false;}
    }
    private static List<Suggestion> Scan()
    {
        var found=new List<Suggestion>();
        if(!HasLiveExcel())return found;
        List<DeskSpaceExcelTemplateV16> mappings,formats;
        try{mappings=DeskSpaceExcelStoreV16.Load();}
        catch{return found;} // corrupt mapping must NOT be reset or altered
        try{formats=DeskSpaceExcelFormatStoreV121.Load();}
        catch{formats=new();}
        foreach(var mapping in mappings.Where(x=>x.CoordinatesVerified &&
                    x.Cells.Any(c=>c.Role=="Value" &&
                        c.ValueKind!="blank" && c.ValueKind!="formula"))
                    .Take(10))
        {
            try
            {
                var preview=DeskSpaceExcelBridgeV16.Preview(mapping,false,false);
                if(!preview.Verified || preview.Writable<1)continue;
                string sample=string.Join(", ",preview.Changes
                    .Where(x=>x.CanApply).Take(2).Select(x=>
                        x.CellAddress+" = "+x.NewValue));
                found.Add(new(mapping.Id,"Values",mapping.Name,
                    preview.WorkbookFullName,preview.SheetName,
                    preview.Writable,sample));
            }
            catch(Exception ex) when(ex is COMException or
                InvalidOperationException or IOException){}
            if(found.Count>=6)break;
        }
        foreach(var format in formats.Take(8))
        {
            try
            {
                var preview=DeskSpaceExcelFormatApplyV109.Preview(format);
                if(!preview.Eligible)continue;
                found.Add(new(format.Id,"Format",format.Name,
                    preview.Workbook,preview.Sheet,
                    format.SheetVisualV101?.Cells.Count??0,
                    "Cell styles, tables and approved formulas; no old values"));
            }
            catch(Exception ex) when(ex is COMException or
                InvalidOperationException or InvalidDataException or IOException){}
            if(found.Count>=8)break;
        }
        return found;
    }
    private static Task<List<Suggestion>> ScanSta()
    {
        var completion=new TaskCompletionSource<List<Suggestion>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var thread=new Thread(()=>{
            try{completion.TrySetResult(Scan());}
            catch{completion.TrySetResult(new());}
        }){IsBackground=true,Name="DeskSpace active Excel suggestion scan"};
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }
    private async Task Refresh()
    {
        if(disposed || running || applying || !owner.IsVisible)return;
        running=true;
        try
        {
            var incoming=await ScanSta();
            if(disposed)return;
            suggestions=incoming;
            Draw();
        }
        finally{running=false;}
    }
    private void Draw()
    {
        if(container==null || disposed)return;
        container.Children.Clear();
        if(suggestions.Count==0)return;
        container.Children.Add(new TextBlock{
            Text="Excel matches · click to review paste",
            FontSize=11,FontWeight=FontWeights.SemiBold,
            Foreground=Brushes.White,
            Margin=new Thickness(5,4,3,2)
        });
        foreach(var item in suggestions.Take(5))
        {
            var control=new Button{
                Content=(item.Kind=="Values"?"↳ Data · ":"▤ Formats · ")+
                    item.Name+" ("+item.Items+")",
                HorizontalContentAlignment=HorizontalAlignment.Left,
                ToolTip=item.Sheet+" · "+item.Preview+
                    Environment.NewLine+"No changes until you confirm.",
                FontSize=11,
                Padding=new Thickness(5,3,5,3),
                Margin=new Thickness(2,1,2,2),
                Background=new SolidColorBrush(Color.FromRgb(49,66,103)),
                Foreground=Brushes.White,
                BorderThickness=new Thickness(0)
            };
            control.Click+=async (_,_)=>await ApplyClicked(item);
            container.Children.Add(control);
        }
    }
    private async Task ApplyClicked(Suggestion selected)
    {
        if(disposed || applying)return;
        applying=true;
        try
        {
            if(selected.Kind=="Values")
            {
                var mapping=DeskSpaceExcelStoreV16.Load()
                    .FirstOrDefault(x=>x.Id==selected.Id);
                if(mapping==null)throw new InvalidOperationException(
                    "This mapping was removed. Refresh the suggestions.");
                var review=await DeskSpaceExcelWorkerV41.PreviewAsync(
                    mapping,false,false);
                if(!review.Verified || review.Writable==0 ||
                   review.SheetName!=selected.Sheet ||
                   !string.Equals(review.WorkbookFullName,selected.Book,
                       StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        "Excel changed since the match. No paste attempted.");
                string summary=string.Join(Environment.NewLine,
                    review.Changes.Where(x=>x.CanApply).Take(8).Select(x=>
                        x.CellAddress+" : "+x.NewValue));
                if(MessageBox.Show(owner,
                    "Apply "+review.Writable+" mapped values to "+review.SheetName+
                    "?"+Environment.NewLine+summary+
                    Environment.NewLine+
                    "Existing formulas and values are protected. A backup is "+
                    "created. The workbook will NOT be auto-saved.",
                    "DeskSpace — confirm header/value paste",
                    MessageBoxButton.YesNo,MessageBoxImage.Question)!=
                    MessageBoxResult.Yes)return;
                await DeskSpaceExcelWorkerV41.ApplyAsync(review,mapping,false);
            }
            else
            {
                var format=DeskSpaceExcelFormatStoreV121.Load()
                    .FirstOrDefault(x=>x.Id==selected.Id);
                if(format==null)throw new InvalidOperationException(
                    "This saved formatting template no longer exists.");
                var review=await DeskSpaceExcelWorkerV41.PreviewFormatAsync(format);
                if(!review.Eligible ||
                   !string.Equals(review.Workbook,selected.Book,
                       StringComparison.OrdinalIgnoreCase) ||
                   review.Sheet!=selected.Sheet)
                    throw new InvalidOperationException(
                        "Active Excel formatting target changed. No changes.");
                if(MessageBox.Show(owner,
                    "Apply format/formula template '"+format.Name+"' to "+
                    review.Destination+"?"+Environment.NewLine+
                    review.PlannedActions+Environment.NewLine+
                    "Existing values are protected and the workbook is backed up.",
                    "DeskSpace — confirm formats & formulas",
                    MessageBoxButton.YesNo,MessageBoxImage.Question)!=
                    MessageBoxResult.Yes)return;
                await DeskSpaceExcelWorkerV41.ApplyFormatAsync(format,review);
            }
            MessageBox.Show(owner,
                "Applied to Excel successfully. Review changes and save "+
                "the workbook in Excel when ready.","DeskSpace Excel");
        }
        catch(Exception ex)
        {
            MessageBox.Show(owner,
                "No Excel changes were approved: "+ex.Message,
                "DeskSpace Excel suggestions");
        }
        finally{applying=false; _=Refresh();}
    }
    public void Dispose()
    {
        disposed=true;
        timer.Stop();
        if(container!=null)container.Children.Clear();
        container=null;
    }
}
