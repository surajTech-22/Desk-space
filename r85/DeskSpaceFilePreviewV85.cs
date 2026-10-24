using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;

namespace DeskSpace;

// Lightweight preview opened on demand; never parses arbitrary binaries,
// invokes shell extensions, or reads document contents in the background.
internal static class DeskSpaceFilePreviewV85
{
    private static readonly string[] TextTypes={
        ".txt",".csv",".tsv",".json",".xml",".log",".md",".ini",
        ".yml",".yaml",".sql",".cs",".ps1",".py",".js",".css",".html"
    };
    private static readonly string[] Images={
        ".png",".jpg",".jpeg",".bmp",".gif",".webp",".tif",".tiff"
    };
    internal static FrameworkElement Create(string path)
    {
        var panel=new StackPanel{Margin=new Thickness(12)};
        var header=new TextBlock{FontSize=13,FontWeight=FontWeights.SemiBold,
            Text=Path.GetFileName(path),TextWrapping=TextWrapping.Wrap,
            Foreground=Brushes.White};
        panel.Children.Add(header);
        try
        {
            if(!File.Exists(path))throw new FileNotFoundException();
            var info=new FileInfo(path);
            panel.Children.Add(new TextBlock{Foreground=Brushes.LightSteelBlue,
                FontSize=11,Text=$"{info.Length:N0} bytes · "+
                    info.LastWriteTime.ToString("g"),Margin=new Thickness(0,5,0,7)});
            string extension=info.Extension.ToLowerInvariant();
            if(Images.Contains(extension) && info.Length<=40*1024*1024)
            {
                var bitmap=new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption=BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth=310;
                bitmap.UriSource=new Uri(path,UriKind.Absolute);
                bitmap.EndInit();bitmap.Freeze();
                panel.Children.Add(new Image{Source=bitmap,
                    MaxWidth=318,MaxHeight=235,Stretch=Stretch.Uniform});
            }
            else if(TextTypes.Contains(extension) && info.Length<=12*1024*1024)
            {
                // Preview at most 12KiB, not an entire large document.
                using var fs=new FileStream(path,FileMode.Open,FileAccess.Read,
                    FileShare.ReadWrite|FileShare.Delete);
                using var sr=new StreamReader(fs,detectEncodingFromByteOrderMarks:true);
                var chars=new char[12288];
                int read=sr.ReadBlock(chars,0,chars.Length);
                string snippet=new string(chars,0,read);
                if(snippet.Any(c=>c=='\0'))snippet="Binary content is not previewed.";
                var excerpt=new TextBox{Text=snippet,IsReadOnly=true,
                    TextWrapping=TextWrapping.Wrap,
                    VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
                    Height=195,Width=300,MaxLines=16,
                    Background=new SolidColorBrush(Color.FromRgb(32,41,65)),
                    Foreground=Brushes.White};
                panel.Children.Add(excerpt);
            }
            else panel.Children.Add(new TextBlock{
                Text="Preview is not available for this file type. "+
                    "Open it in its default application to view the full content.",
                TextWrapping=TextWrapping.Wrap,MaxWidth=300,
                Foreground=Brushes.LightSteelBlue});
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException)
        {
            panel.Children.Add(new TextBlock{Text="Cannot preview: "+ex.Message,
                Foreground=Brushes.LightSteelBlue,TextWrapping=TextWrapping.Wrap});
        }
        var open=new Button{Content="Open file",Padding=new Thickness(8,5),
            HorizontalAlignment=HorizontalAlignment.Right,
            Margin=new Thickness(0,7,0,0)};
        open.Click+=(_,_)=>{
            if(!File.Exists(path))return;
            try{Process.Start(new ProcessStartInfo(path){UseShellExecute=true});}
            catch(Exception e){MessageBox.Show(e.Message,"DeskSpace preview");}
        };
        panel.Children.Add(open);
        return new Border{Child=panel,
            Background=new SolidColorBrush(Color.FromRgb(24,32,52)),
            BorderBrush=Brushes.SlateBlue,BorderThickness=new Thickness(1),
            CornerRadius=new CornerRadius(12),MaxWidth=345};
    }
    internal static void Attach(Button trigger,string path)
    {
        var popup=new Popup{PlacementTarget=trigger,
            Placement=PlacementMode.Left,AllowsTransparency=true,
            StaysOpen=false,Child=Create(path)};
        trigger.Click+=(_,_)=>popup.IsOpen=!popup.IsOpen;
    }
}

internal static class DeskSpaceClipboardQuickActionsV85
{
    internal static string Detect(string text)
    {
        if(DeskSpaceReminderBrowserV80.TryUrl(text,out _))return "URL";
        if(text.Length<2048 && File.Exists(text))return "File";
        if(DateTime.TryParse(text,out var date) && date>DateTime.Now)
            return "Date";
        return "Text";
    }

    internal static void Open(string text)
    {
        if(string.IsNullOrWhiteSpace(text))return;
        var menu=new ContextMenu();
        var classification=Detect(text);
        void Entry(string label,Action action)
        {
            var item=new MenuItem{Header=label};
            item.Click+=(_,_)=>action();
            menu.Items.Add(item);
        }
        if(classification=="URL" &&
           DeskSpaceReminderBrowserV80.TryUrl(text,out var url))
            Entry("Open URL in default browser",()=>SafeOpen(url));
        if(classification=="File")
            Entry("Preview file",()=>{
                var popup=new Window{Title="DeskSpace file preview",
                    Width=365,Height=350,ShowInTaskbar=false,
                    WindowStartupLocation=WindowStartupLocation.CenterScreen,
                    SizeToContent=SizeToContent.Manual};
                popup.Content=DeskSpaceFilePreviewV85.Create(text);
                popup.Show();
            });
        if(classification=="Date" && DateTime.TryParse(text,out var due))
            Entry("Create reminder for this time",()=>{
                try
                {
                    var store=new WorkspaceStore();var ws=store.Load();
                    ws.Reminders.Add(new ReminderItem{Title="Reminder from clipboard",
                        Due=due,AlertAction="None"});
                    store.Save(ws);
                }
                catch(Exception ex){MessageBox.Show(ex.Message,"DeskSpace");}
            });
        Entry("Save copied text as note",()=>{
            try
            {
                var store=new WorkspaceStore();var ws=store.Load();
                string normalized=text.Length>10000?text[..10000]:text;
                ws.Notes.Add(new NoteItem{
                    Title=normalized.Split('\n').FirstOrDefault()?.Trim()
                        is string first && first.Length>0?
                        first[..Math.Min(45,first.Length)]:"Clipboard note",
                    Content=normalized,Updated=DateTime.Now
                });
                store.Save(ws);
            }
            catch(Exception ex){MessageBox.Show(ex.Message,"DeskSpace");}
        });
        Entry("Search in DeskSpace",()=>WidgetHostLauncher.OpenSearch());
        menu.Placement=PlacementMode.MousePoint;
        menu.IsOpen=true;
    }
    private static void SafeOpen(string target)
    {
        try{Process.Start(new ProcessStartInfo(target){UseShellExecute=true});}
        catch(Exception ex){MessageBox.Show(ex.Message,"DeskSpace");}
    }
}
