using System;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using Forms=System.Windows.Forms;

namespace DeskSpace;

// User-invoked, bounded preview of a file or a directory selected in a
// native dialog. Reuses the existing Deck format handlers, never watches,
// extracts, opens or uploads documents without another explicit click.
internal static class DeskSpacePreviewChooserV273
{
    internal const long MaxPreviewFileBytes=256L*1024*1024;
    internal static bool CanPreview(string? selected,out string reason)
    {
        reason="Select a file or folder first.";
        if(string.IsNullOrWhiteSpace(selected))return false;
        try
        {
            var flags=File.GetAttributes(selected);
            if((flags&(FileAttributes.ReparsePoint|FileAttributes.System))!=0)
            {
                reason="Linked or system-protected paths are not previewed.";
                return false;
            }
            if((flags&FileAttributes.Directory)!=0)
            {
                reason="";
                return true;
            }
            var info=new FileInfo(selected);
            if(info.Length>MaxPreviewFileBytes)
            {
                reason="File exceeds the 256 MB preview safety limit.";
                return false;
            }
            if(info.Length<0)
            {
                reason="File size is not available.";
                return false;
            }
            reason="";
            return true;
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or
            ArgumentException or NotSupportedException or
            System.Security.SecurityException)
        {
            reason="Cannot access selected path ("+ex.GetType().Name+").";
            return false;
        }
    }

    internal static Window Create(Window owner)
    {
        string selected="";
        var body=new StackPanel{Margin=new Thickness(16)};
        body.Children.Add(new TextBlock{
            Text="File & folder preview",FontSize=20,
            FontWeight=FontWeights.SemiBold,
            Margin=new Thickness(0,0,0,6)});
        body.Children.Add(new TextBlock{
            Text="Choose one local file or folder. Preview is read-only and opens "+
                 "only after your selection. Large, linked and system paths are blocked. "+
                 "The existing PDF, Word, image, ZIP and text preview engines are reused.",
            TextWrapping=TextWrapping.Wrap,Foreground=Brushes.DimGray,
            Margin=new Thickness(0,0,0,12)});
        var pathLabel=new TextBlock{
            Text="No file or folder selected.",TextWrapping=TextWrapping.Wrap,
            Margin=new Thickness(0,3,0,8)};
        AutomationProperties.SetName(pathLabel,"Current preview selection");
        var file=new Button{Content="Choose file",
            Padding=new Thickness(9,6,9,6),Margin=new Thickness(0,0,8,5)};
        var folder=new Button{Content="Choose folder",
            Padding=new Thickness(9,6,9,6),Margin=new Thickness(0,0,8,5)};
        var clear=new Button{Content="Clear preview",
            Padding=new Thickness(9,6,9,6),Margin=new Thickness(0,0,8,5)};
        var close=new Button{Content="Close",
            Padding=new Thickness(9,6,9,6),Margin=new Thickness(0,0,8,5)};
        var commands=new WrapPanel();
        commands.Children.Add(file);
        commands.Children.Add(folder);
        commands.Children.Add(clear);
        commands.Children.Add(close);
        body.Children.Add(commands);
        body.Children.Add(pathLabel);
        var previewHost=new ContentControl{
            MinHeight=140,HorizontalContentAlignment=HorizontalAlignment.Left};
        AutomationProperties.SetName(previewHost,"Read-only selected file preview");
        body.Children.Add(previewHost);
        var dialog=new Window{
            Title="DeskSpace — Preview file or folder",
            Width=565,Height=575,MinWidth=400,MinHeight=365,
            Owner=owner.IsVisible?owner:null,
            ShowInTaskbar=false,
            WindowStartupLocation=owner.IsVisible?
                WindowStartupLocation.CenterOwner:
                WindowStartupLocation.CenterScreen,
            Content=new ScrollViewer{
                Content=body,
                VerticalScrollBarVisibility=ScrollBarVisibility.Auto}
        };
        void Preview(string path)
        {
            previewHost.Content=null;
            selected="";
            if(!CanPreview(path,out var issue))
            {
                pathLabel.Text=issue;
                return;
            }
            try
            {
                var element=DeskSpaceFilePreviewV85.Create(path);
                previewHost.Content=element;
                selected=path;
                // Do not expose a full private path in the dialogue title
                // or its status. Existing preview shows only the file name.
                pathLabel.Text="Previewing: "+Path.GetFileName(
                    path.TrimEnd(Path.DirectorySeparatorChar));
            }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or
                ArgumentException or NotSupportedException or
                System.Security.SecurityException)
            {
                pathLabel.Text="Preview unavailable ("+ex.GetType().Name+").";
            }
        }
        file.Click+=(_,_)=>{
            var chooser=new OpenFileDialog{
                Title="Select file to preview",
                CheckFileExists=true,Multiselect=false,
                Filter="All files (*.*)|*.*"};
            if(chooser.ShowDialog(dialog)==true)
                Preview(chooser.FileName);
        };
        folder.Click+=(_,_)=>{
            using var chooser=new Forms.FolderBrowserDialog{
                Description="Select folder to preview without scanning its contents",
                ShowNewFolderButton=false};
            if(chooser.ShowDialog()==Forms.DialogResult.OK)
                Preview(chooser.SelectedPath);
        };
        clear.Click+=(_,_)=>{
            selected="";previewHost.Content=null;
            pathLabel.Text="Preview cleared; no files changed.";
        };
        close.Click+=(_,_)=>dialog.Close();
        dialog.Closed+=(_,_)=>{
            selected="";previewHost.Content=null;
        };
        return dialog;
    }
    internal static void Show(Window owner)
    {
        var dialog=Create(owner);
        dialog.Show();
    }
}
