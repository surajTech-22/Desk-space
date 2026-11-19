using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DeskSpace;

// R329-09: read-only, explicit approval for the EXACT verified Excel
// destination proposal. This is IN ADDITION to the existing COM readback,
// backup receipt, and final Yes/No paste confirmation, never a replacement.
internal static class DeskSpaceExcelPreflightV345
{
    internal const int MaximumCells=20000;

    internal static string Proof(DeskSpaceExcelProposalV16 proposal)
    {
        if(proposal==null || !proposal.Verified || proposal.Writable<=0 ||
            proposal.Changes==null || proposal.Changes.Count==0 ||
            proposal.Changes.Count>MaximumCells ||
            string.IsNullOrWhiteSpace(proposal.WorkbookFullName) ||
            string.IsNullOrWhiteSpace(proposal.SheetName) ||
            proposal.StartRow<1||proposal.StartColumn<1)
            throw new InvalidOperationException(
                "An exact, verified live Excel destination preview is required.");
        var bytes=new StringBuilder();
        void Field(string? value)
        {
            value??="";
            bytes.Append(value.Length).Append(':').Append(value);
        }
        Field(proposal.WorkbookFullName.ToUpperInvariant());
        Field(proposal.SheetName.ToUpperInvariant());
        Field(proposal.StartRow.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Field(proposal.StartColumn.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Field(proposal.UsedReferenceSignatureV120?"verified-signature":"manual");
        foreach(var item in proposal.Changes)
        {
            if(item==null||item.Row<1||item.Column<1)
                throw new InvalidOperationException("Invalid target cell in preview.");
            Field(item.Row.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Field(item.Column.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Field(item.CellAddress);
            Field(item.Previous);
            Field(item.PreviousRaw);
            Field(item.NewValue);
            Field(item.Decision);
        }
        return Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(bytes.ToString())));
    }

    internal static string Describe(DeskSpaceExcelProposalV16 proposal)
    {
        var writable=proposal.Changes.Where(x=>x.CanApply).ToArray();
        var blocked=proposal.Changes.Where(x=>!x.CanApply).ToArray();
        return "Workbook: "+proposal.WorkbookFullName+
            Environment.NewLine+"Sheet: "+proposal.SheetName+
            Environment.NewLine+"Start cell: "+proposal.StartRow+
            ", "+proposal.StartColumn+
            Environment.NewLine+"Approved value cells: "+writable.Length+
            Environment.NewLine+"Skipped / protected cells: "+blocked.Length+
            Environment.NewLine+"Header signature: "+
                (proposal.UsedReferenceSignatureV120?"Verified":"Manual placement")+
            Environment.NewLine+
            "Existing formulas are not overwritten; a workbook backup "+
            "and fresh destination readback are required before a write."+
            Environment.NewLine+Environment.NewLine+
            string.Join(Environment.NewLine,
                proposal.Changes.Take(350).Select(item=>
                    (item.CanApply?"WRITE  ":"SKIP   ")+
                    item.CellAddress+" | before: "+item.Previous+
                    " | after: "+item.NewValue+" | "+item.Decision))+
            (proposal.Changes.Count>350?
                Environment.NewLine+"... additional cells are included in the approval proof":"");
    }

    internal static Window Create(Window? owner,
        DeskSpaceExcelProposalV16 proposal)
    {
        string proof=Proof(proposal);
        var win=new Window{
            Title="DeskSpace · Excel paste safety review",
            Width=830,Height=600,MinWidth=620,MinHeight=440,
            ShowInTaskbar=false,Background=Brushes.White,
            WindowStartupLocation=owner!=null&&owner.IsVisible
                ?WindowStartupLocation.CenterOwner:WindowStartupLocation.CenterScreen};
        if(owner!=null&&owner.IsVisible)win.Owner=owner;
        var root=new DockPanel{Margin=new Thickness(16)};win.Content=root;
        var header=new StackPanel();
        header.Children.Add(new TextBlock{
            Text="Review the exact Excel paste plan",FontSize=21,
            FontWeight=FontWeights.SemiBold,
            Margin=new Thickness(0,0,0,8)});
        header.Children.Add(new TextBlock{
            Text="Read-only review: no workbook changes occur in this window. "+
                 "Approving is valid only for this exact preview; the existing "+
                 "backup, live recheck, and final confirmation still apply.",
            TextWrapping=TextWrapping.Wrap,
            Foreground=Brushes.DimGray,
            Margin=new Thickness(0,0,0,10)});
        DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
        var bottom=new StackPanel{Orientation=Orientation.Horizontal,
            HorizontalAlignment=HorizontalAlignment.Right,
            Margin=new Thickness(0,10,0,0)};
        var reject=new Button{Content="Cancel",Padding=new Thickness(14,7,14,7),
            Margin=new Thickness(0,0,9,0)};
        var accept=new Button{Content="Approve this exact preview",
            Padding=new Thickness(14,7,14,7)};
        reject.Click+=(_,_)=>{win.DialogResult=false;win.Close();};
        accept.Click+=(_,_)=>{
            if(Proof(proposal)!=proof){win.DialogResult=false;win.Close();return;}
            win.DialogResult=true;win.Close();
        };
        bottom.Children.Add(reject);bottom.Children.Add(accept);
        DockPanel.SetDock(bottom,Dock.Bottom);root.Children.Add(bottom);
        root.Children.Add(new TextBox{
            Text=Describe(proposal),IsReadOnly=true,
            AcceptsReturn=true,TextWrapping=TextWrapping.NoWrap,
            VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,
            Padding=new Thickness(9),FontSize=12,
            FontFamily=new FontFamily("Consolas")});
        return win;
    }

    internal static string? Review(Window? owner,
        DeskSpaceExcelProposalV16 proposal)
    {
        var dialog=Create(owner,proposal);
        return dialog.ShowDialog()==true?Proof(proposal):null;
    }
}
