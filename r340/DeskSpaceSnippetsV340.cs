using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DeskSpace;

// R329-07: Explicitly saved local text snippets. No clipboard listening,
// automatic capture, sync, secret vault or background access. Unencrypted
// local text is disclosed in UI; do not store passwords or sensitive data.
internal sealed class DeskSpaceSnippetsV340
{
    internal sealed record Snippet(string Id,string Title,string Content,bool Pinned);
    internal const int MaxSnippets=100;
    internal const int MaxCharacters=8192;
    private readonly object gate=new();
    private readonly string file;
    internal DeskSpaceSnippetsV340(string? testFile=null)
    {
        file=testFile??Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData),"DeskSpace",
            "Snippets","saved-v340.json");
    }
    internal IReadOnlyList<Snippet> Load()
    {
        lock(gate)
        {
            if(!File.Exists(file))return Array.Empty<Snippet>();
            try
            {
                var entries=JsonSerializer.Deserialize<List<Snippet>>(
                    File.ReadAllText(file))??new();
                return entries.Where(x=>x.Id.Length==32 &&
                    x.Title.Length<=80 && x.Content.Length<=MaxCharacters)
                    .GroupBy(x=>x.Id,StringComparer.Ordinal)
                    .Select(x=>x.First()).Take(MaxSnippets).ToArray();
            }
            catch(JsonException ex)
            {
                throw new InvalidDataException("Snippet data was not changed; JSON could not be read.",ex);
            }
        }
    }
    internal Snippet Save(string title,string content)
    {
        title=(title??"").Trim();
        content=content??"";
        if(title.Length<1||title.Length>80||content.Length<1 ||
            content.Length>MaxCharacters)
            throw new ArgumentException("Use a title (1–80 characters) and text (1–8192 characters).");
        lock(gate)
        {
            var saved=Load().ToList();
            if(saved.Count>=MaxSnippets)
                throw new InvalidOperationException("Maximum of 100 snippets reached.");
            var item=new Snippet(Guid.NewGuid().ToString("N"),title,content,false);
            saved.Add(item);Write(saved);return item;
        }
    }
    internal bool Delete(string id)
    {
        lock(gate)
        {
            var saved=Load().ToList();
            int removed=saved.RemoveAll(x=>string.Equals(x.Id,id,StringComparison.Ordinal));
            if(removed>0)Write(saved);
            return removed>0;
        }
    }
    internal bool TogglePinned(string id)
    {
        lock(gate)
        {
            var saved=Load().ToList();
            int index=saved.FindIndex(x=>x.Id==id);
            if(index<0)return false;
            saved[index]=saved[index] with {Pinned=!saved[index].Pinned};
            Write(saved);
            return true;
        }
    }
    private void Write(IReadOnlyList<Snippet> items)
    {
        string directory=Path.GetDirectoryName(file)
            ??throw new InvalidOperationException("No snippet directory.");
        Directory.CreateDirectory(directory);
        string scratch=file+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            File.WriteAllText(scratch,JsonSerializer.Serialize(items,
                new JsonSerializerOptions{WriteIndented=true}));
            File.Move(scratch,file,true);
        }
        finally{if(File.Exists(scratch))File.Delete(scratch);}
    }

    internal static Window Create(Window? owner=null,DeskSpaceSnippetsV340? storage=null)
    {
        storage??=new DeskSpaceSnippetsV340();
        var window=new Window{Title="DeskSpace · Saved text snippets",
            Width=730,Height=570,MinWidth=540,MinHeight=460,
            Background=Brushes.White,ShowInTaskbar=false,
            WindowStartupLocation=owner!=null&&owner.IsVisible
                ?WindowStartupLocation.CenterOwner:WindowStartupLocation.CenterScreen};
        if(owner!=null&&owner.IsVisible)window.Owner=owner;
        var root=new DockPanel{Margin=new Thickness(16)};window.Content=root;
        var heading=new StackPanel();
        heading.Children.Add(new TextBlock{Text="Saved snippets",
            FontSize=21,FontWeight=FontWeights.Bold});
        heading.Children.Add(new TextBlock{Text="Save only text you choose. Stored locally as ordinary readable text; do not save passwords or private credentials. No automatic clipboard capture.",
            TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,3,0,9),
            Foreground=Brushes.DimGray});
        DockPanel.SetDock(heading,Dock.Top);root.Children.Add(heading);
        var form=new StackPanel{Margin=new Thickness(0,0,0,10)};
        form.Children.Add(new TextBlock{Text="Snippet name"});
        var title=new TextBox{MaxLength=80,MinHeight=28,
            Margin=new Thickness(0,3,0,7)};form.Children.Add(title);
        form.Children.Add(new TextBlock{Text="Text (manual entry or paste)"});
        var editor=new TextBox{AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,
            VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Height=108,
            MaxLength=MaxCharacters,Padding=new Thickness(5)};
        form.Children.Add(editor);
        var buttons=new StackPanel{Orientation=Orientation.Horizontal,
            Margin=new Thickness(0,9,0,7)};
        var save=new Button{Content="Save",Padding=new Thickness(11,5,11,5),
            Margin=new Thickness(0,0,7,0)};
        var copy=new Button{Content="Copy selected",Padding=new Thickness(11,5,11,5),
            Margin=new Thickness(0,0,7,0)};
        var pin=new Button{Content="Pin/unpin",Padding=new Thickness(11,5,11,5),
            Margin=new Thickness(0,0,7,0)};
        var remove=new Button{Content="Delete selected",Padding=new Thickness(11,5,11,5)};
        foreach(var button in new[]{save,copy,pin,remove})buttons.Children.Add(button);
        form.Children.Add(buttons);
        DockPanel.SetDock(form,Dock.Top);root.Children.Add(form);
        var footer=new TextBlock{Text="No clipboard history is collected.",
            Foreground=Brushes.DimGray,Margin=new Thickness(0,7,0,0)};
        DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
        var filter=new TextBox{MinHeight=28,Margin=new Thickness(0,0,0,6),
            ToolTip="Search saved snippet titles and text"};
        DockPanel.SetDock(filter,Dock.Top);root.Children.Add(filter);
        var list=new ListBox{DisplayMemberPath="Title"};
        root.Children.Add(list);
        void Reload()
        {
            var term=filter.Text.Trim();
            var items=storage.Load()
                .Where(x=>term.Length==0 ||
                    x.Title.Contains(term,StringComparison.OrdinalIgnoreCase) ||
                    x.Content.Contains(term,StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x=>x.Pinned).ThenBy(x=>x.Title,
                    StringComparer.OrdinalIgnoreCase).ToArray();
            list.ItemsSource=items;
        }
        filter.TextChanged+=(_,_)=>Reload();
        save.Click+=(_,_)=>{
            try{storage.Save(title.Text,editor.Text);title.Clear();editor.Clear();
                Reload();footer.Text="Snippet saved locally.";}
            catch(Exception ex)when(ex is IOException or UnauthorizedAccessException
                or ArgumentException or InvalidOperationException
                or System.Security.SecurityException){footer.Text=ex.Message;}
        };
        copy.Click+=(_,_)=>{
            if(list.SelectedItem is Snippet item)
            {
                Clipboard.SetText(item.Content);
                footer.Text="Selected snippet copied by request.";
            }
        };
        pin.Click+=(_,_)=>{
            if(list.SelectedItem is Snippet item){storage.TogglePinned(item.Id);Reload();}
        };
        remove.Click+=(_,_)=>{
            if(list.SelectedItem is not Snippet item)return;
            if(MessageBox.Show(window,"Delete saved snippet '"+item.Title+"'?",
                "DeskSpace",MessageBoxButton.YesNo,MessageBoxImage.Question)
                !=MessageBoxResult.Yes)return;
            storage.Delete(item.Id);Reload();footer.Text="Snippet deleted.";
        };
        Reload();
        return window;
    }
    internal static void Show(Window owner)=>Create(owner).Show();
}
