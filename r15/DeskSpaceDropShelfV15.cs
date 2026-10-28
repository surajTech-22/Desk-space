using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace DeskSpace;

// Owned by the actual donut window. A Popup is anchored to the parent widget:
// no taskbar entry, no independent application and no Clipboard-module dependency.
internal sealed class DeskSpaceDropShelfV15 : IDisposable
{
    private sealed record Held(string Name,string StagedPath);
    private readonly Window host;
    private readonly Popup popup;
    private readonly Border panel;
    private readonly StackPanel list;
    private readonly List<Held> held=new();
    private readonly string staging=Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DeskSpace","TemporaryShelf",Guid.NewGuid().ToString("N"));
    private bool busy, disposed;
    private static DeskSpaceDropShelfV15? active;
    public static bool IsActive=>active!=null;
    public static IReadOnlyList<string> ActivePaths=>
        active?.held.Select(x=>x.StagedPath).ToArray()??Array.Empty<string>();
    public static void ImportClipboardFiles()=>active?.ImportFromClipboard();
    public static void ClearActive()=>active?.ClearAll();
    private int placementNudge;
    public DeskSpaceDropShelfV15(Window window)
    {
        host=window;
        active=this;
        host.AllowDrop=true;
        host.PreviewDragOver+=DragOver;
        host.PreviewDrop+=Receive;
        host.LocationChanged+=Position;
        host.SizeChanged+=Position;
        host.IsVisibleChanged+=Visible;
        host.Closed+=Closed;
        list=new StackPanel {Margin=new Thickness(7,6,7,6)};
        panel=new Border{
            CornerRadius=new CornerRadius(15),
            Background=new SolidColorBrush(Color.FromArgb(204,20,28,52)),
            BorderBrush=new SolidColorBrush(Color.FromArgb(170,187,201,245)),
            BorderThickness=new Thickness(1.1),
            MinWidth=180,MaxWidth=310,
            Child=list,AllowDrop=true
        };
        panel.PreviewDragOver+=DragOver;
        panel.PreviewDrop+=Receive;
        popup=new Popup {
            PlacementTarget=host,Placement=PlacementMode.Bottom,
            HorizontalOffset=0,VerticalOffset=5,StaysOpen=true,
            AllowsTransparency=true,PopupAnimation=PopupAnimation.Fade,
            Child=panel
        };
        // Held-file popup belongs to the physical display hosting this widget.
        DeskSpaceMonitorFenceV153.TrackPopup(host,popup);
    }
    public void Enable(){if(!disposed)Render();}
    private void Position(object? sender,EventArgs e)
    {
        // A placement update is required on some WPF builds after moving a Window.
        if(!popup.IsOpen)return;
        popup.HorizontalOffset=(++placementNudge%2==0)?0:0.01;
    }
    private void Visible(object sender,DependencyPropertyChangedEventArgs e)
    {if(disposed)return;popup.IsOpen=(bool)e.NewValue&&held.Count>0;}
    private void Closed(object? sender,EventArgs e)=>Dispose();
    private static bool IsOwnPath(string path,string basePath)
    {
        try{return Path.GetFullPath(path).StartsWith(
            Path.GetFullPath(basePath)+Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);}
        catch{return false;}
    }
    private void DragOver(object sender,DragEventArgs e)
    {
        if(!e.Data.GetDataPresent(DataFormats.FileDrop))return;
        e.Effects=busy?DragDropEffects.None:DragDropEffects.Copy;
        e.Handled=true;
    }
    private void Receive(object sender,DragEventArgs e)
    {
        if(!e.Data.GetDataPresent(DataFormats.FileDrop))return;
        e.Effects=DragDropEffects.Copy;
        e.Handled=true;
        var files=e.Data.GetData(DataFormats.FileDrop) as string[];
        if(files?.Length>0)_=StageFilesAsync(files);
    }
    private void ImportFromClipboard()
    {
        try{
            if(!Clipboard.ContainsFileDropList())return;
            _=StageFilesAsync(Clipboard.GetFileDropList().Cast<string>().ToArray());
        }catch(Exception){}
    }
    private async Task StageFilesAsync(string[] files)
    {
        if(busy||disposed||files.Length==0)return;
        busy=true;
        Render();
        try
        {
            foreach(var source in files)
            {
                if(disposed||held.Count>=25)break;
                if(!File.Exists(source)&&!Directory.Exists(source))continue;
                if(IsOwnPath(source,staging))continue;
                var id=Guid.NewGuid().ToString("N");
                var destination=Path.Combine(staging,id,Path.GetFileName(source.TrimEnd(
                    Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)));
                await Task.Run(()=>{
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    if(File.Exists(source))File.Copy(source,destination,false);
                    else CopyFolder(source,destination);
                });
                if(disposed){DeletePath(destination);break;}
                held.Add(new Held(Path.GetFileName(destination),destination));
                Render();
            }
        }
        catch(Exception ex)
        {
            MessageBox.Show("Could not hold the file: "+ex.Message,"DeskSpace temporary shelf",
                MessageBoxButton.OK,MessageBoxImage.Warning);
        }
        finally{busy=false;if(!disposed)Render();}
    }
    private void ClearAll()
    {
        foreach(var item in held.ToArray())Remove(item);
    }
    private static void CopyFolder(string source,string destination)
    {
        // Never traverse junctions or symbolic links into unrelated trees.
        Directory.CreateDirectory(destination);
        var pending=new Stack<(string Source,string Destination)>();
        pending.Push((source,destination));
        while(pending.Count>0)
        {
            var (folder,target)=pending.Pop();
            foreach(var file in Directory.EnumerateFiles(folder))
            {
                if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)continue;
                File.Copy(file,Path.Combine(target,Path.GetFileName(file)),false);
            }
            foreach(var dir in Directory.EnumerateDirectories(folder))
            {
                if((File.GetAttributes(dir)&FileAttributes.ReparsePoint)!=0)continue;
                var child=Path.Combine(target,Path.GetFileName(dir));
                Directory.CreateDirectory(child);
                pending.Push((dir,child));
            }
        }
    }
    private static void DeletePath(string path)
    {
        try {
            if(File.Exists(path))File.Delete(path);
            else if(Directory.Exists(path))Directory.Delete(path,true);
            var parent=Path.GetDirectoryName(path);
            if(parent!=null&&Directory.Exists(parent)&&!Directory.EnumerateFileSystemEntries(parent).Any())
                Directory.Delete(parent);
        }catch(IOException){}catch(UnauthorizedAccessException){}
    }
    private void Remove(Held item)
    {
        held.Remove(item);
        DeletePath(item.StagedPath);
        Render();
    }
    private void CopyToClipboard(Held item)
    {
        try{
            var paths=new StringCollection();paths.Add(item.StagedPath);
            Clipboard.SetFileDropList(paths);
            // Windows gives no reliable confirmation of a Ctrl+V into another app.
            // Leave the temporary copy until manually cleared or dragged successfully.
        }catch(Exception ex){MessageBox.Show(ex.Message,"DeskSpace temporary shelf");}
    }
    private static TextBlock Text(string text,int size,Brush foreground)=>new(){
        Text=text,FontSize=size,Foreground=foreground,
        TextTrimming=TextTrimming.CharacterEllipsis,
        VerticalAlignment=VerticalAlignment.Center
    };
    private void Render()
    {
        if(disposed)return;
        list.Children.Clear();
        if(held.Count==0 && !busy){popup.IsOpen=false;return;}
        var title=new DockPanel {Margin=new Thickness(5,0,3,5)};
        var clear=new Button{Content="Clear",FontSize=11,Padding=new Thickness(5,2,5,2),
            ToolTip="Discard staged copies only; never delete original files"};
        clear.Click+=(_,_)=>{
            foreach(var h in held.ToArray())Remove(h);
        };
        DockPanel.SetDock(clear,Dock.Right);title.Children.Add(clear);
        title.Children.Add(Text(busy?"Copying files…":"Temporary files · "+held.Count,
            12,Brushes.White));
        list.Children.Add(title);
        foreach(var item in held.ToArray())
        {
            var row=new Border{
                CornerRadius=new CornerRadius(8),
                Background=new SolidColorBrush(Color.FromArgb(145,39,50,84)),
                Padding=new Thickness(6,5,3,5),
                Margin=new Thickness(1,2,1,2),
                ToolTip="Drag to another application to copy · click copy for Ctrl+V"
            };
            var content=new DockPanel {LastChildFill=true};
            var remove=new Button{Content="×",FontSize=13,MinWidth=22,Padding=new Thickness(1),
                ToolTip="Discard temporary copy"};
            remove.Click+=(_,_)=>Remove(item);
            DockPanel.SetDock(remove,Dock.Right);content.Children.Add(remove);
            var copy=new Button{Content="⧉",FontSize=13,MinWidth=25,Padding=new Thickness(1),
                ToolTip="Copy this file for pasting"};
            copy.Click+=(_,_)=>CopyToClipboard(item);
            DockPanel.SetDock(copy,Dock.Right);content.Children.Add(copy);
            var fileInfo=new StackPanel{Orientation=Orientation.Horizontal};
            var ico=DeskSpaceShellIconV14.Get(item.StagedPath);
            if(ico!=null)fileInfo.Children.Add(new Image{Source=ico,Width=18,Height=18,
                Margin=new Thickness(0,0,6,0)});
            fileInfo.Children.Add(Text("⋮⋮ "+item.Name,12,Brushes.White));
            content.Children.Add(fileInfo);row.Child=content;
            row.MouseEnter+=(_,_)=>row.Background=new SolidColorBrush(
                Color.FromArgb(217,67,82,137));
            row.MouseLeave+=(_,_)=>row.Background=new SolidColorBrush(
                Color.FromArgb(145,39,50,84));
            Point origin=new();
            row.PreviewMouseLeftButtonDown+=(_,e)=>origin=e.GetPosition(row);
            row.PreviewMouseMove+=(_,e)=>{
                if(busy||e.LeftButton!=MouseButtonState.Pressed)return;
                var point=e.GetPosition(row);
                if(Math.Abs(point.X-origin.X)<SystemParameters.MinimumHorizontalDragDistance &&
                   Math.Abs(point.Y-origin.Y)<SystemParameters.MinimumVerticalDragDistance)return;
                var child=e.OriginalSource as DependencyObject;
                while(child!=null){
                    if(child is Button)return;
                    child=VisualTreeHelper.GetParent(child);
                }
                var data=new DataObject(DataFormats.FileDrop,new[]{item.StagedPath});
                var effect=DragDrop.DoDragDrop(row,data,DragDropEffects.Copy);
                if(effect==DragDropEffects.Copy)Remove(item);
            };
            list.Children.Add(row);
        }
        popup.IsOpen=host.IsVisible;
    }
    public void Dispose()
    {
        if(disposed)return;
        disposed=true;
        if(ReferenceEquals(active,this))active=null;
        popup.IsOpen=false;
        host.PreviewDragOver-=DragOver;
        host.PreviewDrop-=Receive;
        host.LocationChanged-=Position;
        host.SizeChanged-=Position;
        host.IsVisibleChanged-=Visible;
        host.Closed-=Closed;
        try { if(Directory.Exists(staging))Directory.Delete(staging,true); }
        catch(IOException){}catch(UnauthorizedAccessException){}
    }
}
