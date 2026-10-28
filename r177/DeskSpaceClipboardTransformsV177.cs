using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace DeskSpace;

// Explicit opt-in clipboard conversions. No background reading, persistence,
// network access, or file writes except one user-approved Save As action.
internal static class DeskSpaceClipboardTransformsV177
{
    internal const int MaxChars=262144;
    internal static readonly string[] Modes={
        "Plain text","Markdown literal","JSON string","JSON object","Markdown link"
    };
    private static readonly Dictionary<Window,Window> Windows=new();

    internal static string Transform(string text,string mode)
    {
        ArgumentNullException.ThrowIfNull(text);
        if(text.Length>MaxChars)
            throw new ArgumentException("Selected text is too large for preview.");
        return mode switch
        {
            "Plain text"=>text,
            "Markdown literal"=>MarkdownLiteral(text),
            "JSON string"=>JsonSerializer.Serialize(text),
            "JSON object"=>JsonSerializer.Serialize(
                new Dictionary<string,string>{{"text",text}},
                new JsonSerializerOptions{WriteIndented=true}),
            "Markdown link"=>MarkdownLink(text),
            _=>throw new ArgumentOutOfRangeException(nameof(mode))
        };
    }
    private static string MarkdownLiteral(string text)
    {
        var result=new StringBuilder(text.Length+32);
        foreach(char c in text)
        {
            if(c is '\\' or '*' or '_' or '{' or '}' or '[' or ']' or
                '(' or ')' or '#' or '+' or '-' or '.' or '!' or '>' or '|' or '~'
                || c==(char)96)
                result.Append('\\');
            result.Append(c);
        }
        return result.ToString();
    }
    private static string MarkdownLink(string input)
    {
        var url=input.Trim();
        if(url.Length>2048 ||
            !Uri.TryCreate(url,UriKind.Absolute,out var parsed) ||
            (parsed.Scheme!="https" && parsed.Scheme!="http") ||
            string.IsNullOrWhiteSpace(parsed.Host) ||
            !url.StartsWith(parsed.Scheme+"://",StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Markdown link requires one HTTP(S) URL.");
        var target=parsed.AbsoluteUri.Replace("(","%28").Replace(")","%29");
        return "["+MarkdownLiteral(parsed.Host)+"]("+target+")";
    }
    private static string Extension(string mode)=>mode switch
    {
        "JSON string" or "JSON object"=>".json",
        "Markdown literal" or "Markdown link"=>".md",
        _=>".txt"
    };
    private static string SafeDownloads()
    {
        var home=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var downloads=Path.Combine(home,"Downloads");
        return Directory.Exists(downloads)?downloads:home;
    }
    internal static Window Create(Window owner)
    {
        string source="";
        string output="";
        var caption=new TextBlock{
            Text="Clipboard conversions",FontSize=19,
            FontWeight=FontWeights.SemiBold,Foreground=Brushes.White,
            Margin=new Thickness(0,0,0,6)};
        var privacy=new TextBlock{
            Text="Clipboard text is read only after you press Read clipboard. "+
                 "No background capture, cloud access or automatic files.",
            Foreground=Brushes.Gainsboro,FontSize=11,
            TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12)};
        var mode=new ComboBox{ItemsSource=Modes,SelectedIndex=0,
            Margin=new Thickness(0,6,0,6),MinHeight=29};
        var input=new TextBox{
            IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,
            VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Height=110,
            Background=Brushes.White,Foreground=Brushes.Black};
        var result=new TextBox{
            IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,
            VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Height=140,
            Background=Brushes.White,Foreground=Brushes.Black};
        var status=new TextBlock{
            Text="Nothing read. Choose Read clipboard to begin.",
            Foreground=Brushes.Gainsboro,FontSize=11,
            Margin=new Thickness(0,9,0,3),TextWrapping=TextWrapping.Wrap};
        var copy=new Button{Content="Copy transformed text",
            MinWidth=140,Margin=new Thickness(0,0,8,0),
            Padding=new Thickness(8,5,8,5),IsEnabled=false};
        var save=new Button{Content="Save As...",
            MinWidth=95,Padding=new Thickness(8,5,8,5),IsEnabled=false};
        void Refresh()
        {
            output="";result.Text="";copy.IsEnabled=false;save.IsEnabled=false;
            if(source.Length==0)
            {
                status.Text="No nonempty clipboard text loaded.";
                return;
            }
            try
            {
                output=Transform(source,(string)mode.SelectedItem);
                result.Text=output;copy.IsEnabled=true;save.IsEnabled=true;
                status.Text="Preview ready. No clipboard or file changed.";
            }
            catch(ArgumentException ex){status.Text=ex.Message;}
        }
        mode.SelectionChanged+=(_,_)=>Refresh();
        var read=new Button{Content="Read clipboard (on demand)",
            Padding=new Thickness(9,6,9,6),
            Margin=new Thickness(0,0,0,12)};
        read.Click+=(_,_)=>{
            source="";output="";input.Clear();result.Clear();
            try
            {
                if(!Clipboard.ContainsText(TextDataFormat.UnicodeText))
                {
                    status.Text="Clipboard has no Unicode text.";Refresh();return;
                }
                var value=Clipboard.GetText(TextDataFormat.UnicodeText);
                if(value.Length>MaxChars)
                {
                    status.Text="Text exceeds 256K characters.";Refresh();return;
                }
                source=value;input.Text=source;Refresh();
            }
            catch(System.Runtime.InteropServices.COMException){
                status.Text="Clipboard in use; retry.";
            }
        };
        copy.Click+=(_,_)=>{
            if(output.Length==0)return;
            try
            {
                Clipboard.SetText(output,TextDataFormat.UnicodeText);
                status.Text="Copied by request.";
            }
            catch(System.Runtime.InteropServices.COMException){
                status.Text="Clipboard in use; retry.";
            }
        };
        Window? window=null;
        save.Click+=(_,_)=>{
            if(output.Length==0)return;
            string ext=Extension((string)mode.SelectedItem);
            var dialog=new SaveFileDialog{
                Title="Save converted text",
                InitialDirectory=SafeDownloads(),
                FileName="DeskSpace-converted"+ext,
                DefaultExt=ext,AddExtension=true,OverwritePrompt=true,
                Filter=ext switch{
                    ".json"=>"JSON document (*.json)|*.json",
                    ".md"=>"Markdown document (*.md)|*.md",
                    _=>"Text document (*.txt)|*.txt"
                }
            };
            if(dialog.ShowDialog(window)!=true)return;
            try
            {
                File.WriteAllText(dialog.FileName,output,new UTF8Encoding(false));
                status.Text="Saved by request.";
            }
            catch(Exception ex)when(ex is IOException or UnauthorizedAccessException)
            {
                status.Text="Could not save ("+ex.GetType().Name+").";
            }
        };
        var actions=new StackPanel{Orientation=Orientation.Horizontal,
            Margin=new Thickness(0,10,0,0)};
        actions.Children.Add(copy);actions.Children.Add(save);
        var layout=new StackPanel{Margin=new Thickness(16)};
        layout.Children.Add(caption);layout.Children.Add(privacy);
        layout.Children.Add(read);
        layout.Children.Add(new TextBlock{Text="Source (read-only)",
            Foreground=Brushes.White});
        layout.Children.Add(input);
        layout.Children.Add(new TextBlock{
            Text="Conversion mode",Foreground=Brushes.White,
            Margin=new Thickness(0,10,0,0)});
        layout.Children.Add(mode);
        layout.Children.Add(new TextBlock{Text="Result preview",
            Foreground=Brushes.White});
        layout.Children.Add(result);layout.Children.Add(actions);
        layout.Children.Add(status);
        window=new Window{
            Title="DeskSpace · Clipboard transformations",
            Width=510,Height=580,MinWidth=405,MinHeight=425,
            Background=new SolidColorBrush(Color.FromRgb(24,33,60)),
            ShowInTaskbar=true,
            Content=new ScrollViewer{Content=layout,
                VerticalScrollBarVisibility=ScrollBarVisibility.Auto},
            WindowStartupLocation=WindowStartupLocation.CenterScreen
        };
        if(owner.IsVisible)
        {
            window.Owner=owner;
            window.WindowStartupLocation=WindowStartupLocation.CenterOwner;
        }
        window.Closed+=(_,_)=>{
            source="";output="";input.Clear();result.Clear();
        };
        return window;
    }
    internal static void Show(Window owner)
    {
        if(Windows.TryGetValue(owner,out var prior))
        {
            if(prior.IsLoaded){prior.Activate();return;}
            Windows.Remove(owner);
        }
        var window=Create(owner);Windows[owner]=window;
        window.Closed+=(_,_)=>Windows.Remove(owner);
        window.Show();
    }
}
