using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DeskSpace;

// One small label/value manager. No new store or vault migration: previous
// information-items-v22.json records remain readable in their original format.
// PW is never written to the ordinary information file.
internal sealed class DeskSpaceInformationV25
{
    private sealed class Record
    {
        public Guid Id {get;set;}=Guid.NewGuid();
        public string Title {get;set;}="";
        public string Category {get;set;}="General";
        public string Value {get;set;}="";
        public string Notes {get;set;}="";
        public DateTime UpdatedUtc {get;set;}=DateTime.UtcNow;
    }
    private static readonly string Location=Path.Combine(WorkspaceStore.Root,"information-items-v22.json");
    private static readonly Brush Ink=new SolidColorBrush(Color.FromRgb(39,43,64));
    private static readonly Brush Subtle=new SolidColorBrush(Color.FromRgb(108,111,135));
    private static readonly Brush Accent=new SolidColorBrush(Color.FromRgb(96,80,181));
    private static List<Record> Read()
    {
        if(!File.Exists(Location))return new();
        try{return JsonSerializer.Deserialize<List<Record>>(File.ReadAllText(Location))??new();}
        catch(Exception ex){throw new InvalidDataException(
            "Saved information cannot be read. The original file was not modified.",ex);}
    }
    private static void Write(List<Record> entries)
    {
        Directory.CreateDirectory(WorkspaceStore.Root);
        var temp=Location+".pending";
        File.WriteAllText(temp,JsonSerializer.Serialize(entries,new JsonSerializerOptions{
            WriteIndented=true}),new UTF8Encoding(false));
        if(File.Exists(Location))
            File.Replace(temp,Location,Location+".previous",true);
        else File.Move(temp,Location);
    }
    private static Button Button(string label,Action action,bool accent=false)
    {
        var button=new Button{Content=label,MinHeight=28,Padding=new Thickness(9,3,9,3),
            Background=accent?Accent:new SolidColorBrush(Color.FromRgb(239,237,250)),
            Foreground=accent?Brushes.White:Ink,
            BorderThickness=new Thickness(0),Margin=new Thickness(0,0,5,0)};
        button.Click+=(_,_)=>action();
        return button;
    }
    public FrameworkElement Create(Action openPasswordVault)
    {
        var panel=new DockPanel{LastChildFill=true,Background=Brushes.White};
        var header=new Grid{Margin=new Thickness(0,0,0,7)};
        header.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
        header.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
        header.Children.Add(new TextBlock{Text="Saved information",FontSize=17,Foreground=Ink,
            FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center});
        DockPanel.SetDock(header,Dock.Top);panel.Children.Add(header);

        var body=new Grid();
        panel.Children.Add(body);
        var listSurface=new StackPanel();
        var editor=new StackPanel{Visibility=Visibility.Collapsed};
        body.Children.Add(listSurface);body.Children.Add(editor);
        var list=new StackPanel();
        var viewer=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,MaxHeight=205,
            Content=list};
        listSurface.Children.Add(viewer);

        var error=new TextBlock{TextWrapping=TextWrapping.Wrap,FontSize=11,Foreground=Subtle,
            Margin=new Thickness(0,7,0,0)};
        DockPanel.SetDock(error,Dock.Bottom);panel.Children.Add(error);

        Guid? editing=null;
        var selection=new ComboBox{ItemsSource=new[]{"UI","PW","Other"},
            SelectedIndex=0,MinHeight=31};
        var other=new TextBox{MinHeight=31,Padding=new Thickness(7,4,7,4),
            Visibility=Visibility.Collapsed,ToolTip="Enter the label"};
        var value=new TextBox{MinHeight=32,Padding=new Thickness(7,5,7,5),
            TextWrapping=TextWrapping.Wrap,AcceptsReturn=true,MaxHeight=90,
            ToolTip="Value"};
        editor.Children.Add(new TextBlock{Text="Label",Foreground=Subtle,FontSize=12,
            Margin=new Thickness(0,0,0,4)});
        editor.Children.Add(selection);
        other.Margin=new Thickness(0,5,0,0);
        editor.Children.Add(other);
        editor.Children.Add(new TextBlock{Text="Value",Foreground=Subtle,FontSize=12,
            Margin=new Thickness(0,9,0,4)});
        editor.Children.Add(value);
        var plus=Button("+",()=>ShowEditor(null),true);
        plus.MinWidth=32;plus.FontSize=18;
        Grid.SetColumn(plus,1);header.Children.Add(plus);
        var buttons=new WrapPanel{Margin=new Thickness(0,11,0,0)};
        editor.Children.Add(buttons);
        buttons.Children.Add(Button("Save",Save,true));
        buttons.Children.Add(Button("Cancel",()=>{editor.Visibility=Visibility.Collapsed;
            listSurface.Visibility=Visibility.Visible;}));
        selection.SelectionChanged+=(_,_)=>{
            var type=selection.SelectedItem as string??"UI";
            other.Visibility=type=="Other"?Visibility.Visible:Visibility.Collapsed;
            if(type=="PW")
            {
                // Password contents MUST be managed by the existing encrypted
                // Security Vault, never in the ordinary information store.
                editor.Visibility=Visibility.Collapsed;
                listSurface.Visibility=Visibility.Visible;
                openPasswordVault();
            }
        };

        void Safe(Action action)
        {
            try{action();error.Text="";}
            catch(Exception e){error.Text=e.Message;}
        }
        void Render()
        {
            var data=Read();
            list.Children.Clear();
            // Intentionally empty if nothing has been saved: no placeholders,
            // fake examples or always-visible editor.
            foreach(var item in data.OrderByDescending(x=>x.UpdatedUtc))
            {
                var row=new Grid{Margin=new Thickness(0,1,0,3),
                    Background=new SolidColorBrush(Color.FromRgb(250,249,254))};
                row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(82)});
                row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
                row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
                row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
                var label=new TextBlock{Text=item.Title,FontSize=12,FontWeight=FontWeights.SemiBold,
                    TextTrimming=TextTrimming.CharacterEllipsis,Foreground=Ink,
                    VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(7,4,4,4)};
                row.Children.Add(label);
                var text=new TextBlock{Text=item.Value,FontSize=12,
                    TextTrimming=TextTrimming.CharacterEllipsis,Foreground=Ink,
                    VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(2,4,4,4)};
                Grid.SetColumn(text,1);row.Children.Add(text);
                var copy=Button("⧉",()=>Safe(()=>Clipboard.SetText(item.Value)));
                copy.Padding=new Thickness(4,2,4,2);copy.ToolTip="Copy value";
                Grid.SetColumn(copy,2);row.Children.Add(copy);
                var edit=Button("✎",()=>ShowEditor(item));
                edit.Padding=new Thickness(4,2,4,2);edit.ToolTip="Edit label/value";
                Grid.SetColumn(edit,3);row.Children.Add(edit);
                list.Children.Add(row);
            }
        }
        void ShowEditor(Record? record)
        {
            editing=record?.Id;
            selection.SelectedIndex=0;
            var label=record?.Title??"UI";
            if(label.Equals("UI",StringComparison.OrdinalIgnoreCase))
                selection.SelectedItem="UI";
            else
            {
                selection.SelectedItem="Other";
                other.Text=label;
            }
            value.Text=record?.Value??"";
            listSurface.Visibility=Visibility.Collapsed;
            editor.Visibility=Visibility.Visible;
            error.Text="";
        }
        void Save()
        {
            Safe(()=>{
                var type=selection.SelectedItem as string??"UI";
                if(type=="PW"){openPasswordVault();return;}
                string label=type=="Other"?other.Text.Trim():"UI";
                if(label.Length==0)throw new ArgumentException("Enter a label.");
                if(label.Equals("PW",StringComparison.OrdinalIgnoreCase) ||
                   label.Equals("Password",StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Use Security Vault for passwords.");
                var all=Read();
                var current=all.FirstOrDefault(x=>x.Id==editing);
                if(current==null){current=new Record();all.Add(current);}
                // Preserve legacy Category/Notes on edit; only update requested
                // label/value pair. Newly created pairs have General category.
                current.Title=label;current.Value=value.Text;
                current.UpdatedUtc=DateTime.UtcNow;
                Write(all);
                editor.Visibility=Visibility.Collapsed;
                listSurface.Visibility=Visibility.Visible;
                Render();
            });
        }
        try{Render();}
        catch(Exception ex){error.Text=ex.Message;plus.IsEnabled=false;}
        return panel;
    }
}
