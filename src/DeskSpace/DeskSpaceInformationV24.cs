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

// The existing information-items-v22.json schema and location remain intact.
// Selecting Password in the parent Information Manager invokes the encrypted
// Security Vault: this component is only for ordinary, non-secret information.
internal sealed class DeskSpaceInformationV24
{
    internal sealed class Record
    {
        public Guid Id {get;set;}=Guid.NewGuid();
        public string Title {get;set;}="";
        public string Category {get;set;}="General";
        public string Value {get;set;}="";
        public string Notes {get;set;}="";
        public DateTime UpdatedUtc {get;set;}=DateTime.UtcNow;
        public override string ToString()=>Title+"\n"+Category;
    }
    private static readonly string Location=Path.Combine(WorkspaceStore.Root,"information-items-v22.json");
    private static readonly SolidColorBrush Ink=new(Color.FromRgb(36,44,72));
    private static readonly SolidColorBrush Muted=new(Color.FromRgb(108,116,145));
    private static readonly SolidColorBrush Accent=new(Color.FromRgb(105,84,202));
    private static List<Record> Read()
    {
        try
        {
            if(!File.Exists(Location))return new();
            return JsonSerializer.Deserialize<List<Record>>(File.ReadAllText(Location))??new();
        }
        catch(Exception ex)
        {
            throw new InvalidDataException(
                "Stored information could not be read. The original file has not been changed.",ex);
        }
    }
    private static void Write(List<Record> data)
    {
        Directory.CreateDirectory(WorkspaceStore.Root);
        string temp=Location+".pending";
        File.WriteAllText(temp,JsonSerializer.Serialize(data,
            new JsonSerializerOptions{WriteIndented=true}),new UTF8Encoding(false));
        if(File.Exists(Location))
            File.Replace(temp,Location,Location+".previous",true);
        else
            File.Move(temp,Location);
    }

    public FrameworkElement Create()
    {
        var root=new StackPanel { Margin=new Thickness(0,4,0,8) };
        root.Children.Add(new TextBlock { Text="Your information",FontSize=19,
            FontWeight=FontWeights.SemiBold,Foreground=Ink,Margin=new Thickness(1,0,0,3) });
        root.Children.Add(new TextBlock {
            Text="Find, edit and copy saved details. Passwords belong only in Security Vault.",
            FontSize=12,Foreground=Muted,TextWrapping=TextWrapping.Wrap,
            Margin=new Thickness(1,0,0,11) });

        var split=new Grid();
        split.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(208)});
        split.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
        root.Children.Add(split);

        var left=new StackPanel {Margin=new Thickness(0,0,13,0)};
        var leftCard=PanelCard(left);split.Children.Add(leftCard);
        left.Children.Add(Caption("Saved records"));
        var search=Field("Search title, category or value");
        search.ToolTip="Search any saved information, case-insensitive";
        left.Children.Add(search);
        var filter=new ComboBox { MinHeight=32,Margin=new Thickness(0,1,0,9) };
        left.Children.Add(filter);
        var records=new ListBox { Height=275,Padding=new Thickness(3),
            BorderBrush=new SolidColorBrush(Color.FromRgb(220,217,237)),
            Background=Brushes.White,Margin=new Thickness(0,0,0,6) };
        left.Children.Add(records);
        var count=new TextBlock {Foreground=Muted,FontSize=11,
            Margin=new Thickness(2,2,0,1) };
        left.Children.Add(count);

        var editor=new StackPanel();
        var editCard=PanelCard(editor);
        Grid.SetColumn(editCard,1);split.Children.Add(editCard);
        editor.Children.Add(Caption("Details"));
        var nameLabel=Tiny("Title");
        editor.Children.Add(nameLabel);
        var title=Field("Title of the information");
        editor.Children.Add(title);
        editor.Children.Add(Tiny("Category / custom tab"));
        var category=Field("General, Contact, Reference, Document...");
        category.Text="General";
        editor.Children.Add(category);
        editor.Children.Add(Tiny("Value · copy exactly"));
        var value=Field("Saved text");
        value.AcceptsReturn=true;value.TextWrapping=TextWrapping.Wrap;
        value.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;
        value.Height=105;
        editor.Children.Add(value);
        editor.Children.Add(Tiny("Notes"));
        var notes=Field("Optional notes");
        notes.AcceptsReturn=true;notes.TextWrapping=TextWrapping.Wrap;
        notes.Height=61;
        editor.Children.Add(notes);
        var actions=new WrapPanel {Margin=new Thickness(0,6,0,0)};
        editor.Children.Add(actions);
        var status=new TextBlock {Foreground=Muted,FontSize=11,
            TextWrapping=TextWrapping.Wrap,Margin=new Thickness(2,9,2,0)};
        editor.Children.Add(status);

        Guid? editing=null;
        bool refreshing=false;
        void Safe(Action act)
        {
            try {act();}
            catch(Exception ex){MessageBox.Show(ex.Message,"DeskSpace information",
                MessageBoxButton.OK,MessageBoxImage.Warning);}
        }
        Button ActionButton(string name,Action a,bool primary=false)
        {
            var b=new Button {Content=name,MinHeight=31,
                Padding=new Thickness(10,5,10,5),
                BorderThickness=new Thickness(0),
                Background=primary?Accent:new SolidColorBrush(Color.FromRgb(235,232,247)),
                Foreground=primary?Brushes.White:Ink,
                Margin=new Thickness(0,0,5,6)};
            b.Click+=(_,_)=>Safe(a);actions.Children.Add(b);return b;
        }
        void Clear()
        {
            editing=null;records.SelectedItem=null;
            title.Clear();category.Text="General";value.Clear();notes.Clear();
            status.Text="New ordinary information";
        }
        void Reload()
        {
            refreshing=true;
            try
            {
                var all=Read();
                string savedFilter=filter.SelectedItem as string??"All";
                var categories=new []{"All","General","Contact","Reference","Document","Custom"}
                    .Concat(all.Select(r=>r.Category))
                    .Where(x=>!string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                filter.ItemsSource=categories;
                filter.SelectedItem=categories.FirstOrDefault(x=>
                    x.Equals(savedFilter,StringComparison.OrdinalIgnoreCase))??"All";
                string q=search.Text.Trim();
                var hits=all.Where(r=>
                    (savedFilter=="All" || r.Category.Equals(savedFilter,StringComparison.OrdinalIgnoreCase)) &&
                    (q.Length==0 || r.Title.Contains(q,StringComparison.OrdinalIgnoreCase) ||
                    r.Category.Contains(q,StringComparison.OrdinalIgnoreCase) ||
                    r.Value.Contains(q,StringComparison.OrdinalIgnoreCase) ||
                    r.Notes.Contains(q,StringComparison.OrdinalIgnoreCase)))
                    .OrderByDescending(r=>r.UpdatedUtc).ToList();
                records.ItemsSource=hits;
                count.Text=hits.Count+" records";
                records.SelectedItem=hits.FirstOrDefault(x=>x.Id==editing);
            }
            finally{refreshing=false;}
        }
        records.SelectionChanged+=(_,_)=>{
            if(refreshing||records.SelectedItem is not Record item)return;
            editing=item.Id;
            title.Text=item.Title;category.Text=item.Category;
            value.Text=item.Value;notes.Text=item.Notes;
            status.Text="Editing existing record";
        };
        ActionButton("Save",()=>{
            if(string.IsNullOrWhiteSpace(title.Text))
                throw new ArgumentException("Enter a title.");
            if(string.IsNullOrWhiteSpace(category.Text))
                throw new ArgumentException("Enter a category.");
            if(category.Text.Trim().Equals("Password",StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Select Password / Security Vault above for passwords.");
            var all=Read();
            var record=all.FirstOrDefault(x=>x.Id==editing);
            if(record==null){record=new Record();all.Add(record);}
            record.Title=title.Text.Trim();record.Category=category.Text.Trim();
            record.Value=value.Text;record.Notes=notes.Text;
            record.UpdatedUtc=DateTime.UtcNow;
            Write(all);editing=record.Id;Reload();
            status.Text="Saved locally · existing encrypted passwords unchanged.";
        },true);
        ActionButton("New",()=>{Clear();});
        ActionButton("Copy value",()=>{
            if(value.Text.Length==0)return;
            Clipboard.SetText(value.Text);status.Text="Copied exactly to clipboard.";
        });
        ActionButton("Delete",()=>{
            if(editing==null)return;
            if(MessageBox.Show("Delete this information record?","DeskSpace",
                MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
            var all=Read();
            all.RemoveAll(x=>x.Id==editing.Value);
            Write(all);Clear();Reload();
        });
        search.TextChanged+=(_,_)=>{if(!refreshing)Safe(Reload);};
        filter.SelectionChanged+=(_,_)=>{if(!refreshing)Safe(Reload);};
        try{Reload();}
        catch(Exception ex)
        {
            status.Text=ex.Message;
            records.IsEnabled=false;
            title.IsEnabled=false;category.IsEnabled=false;
            value.IsEnabled=false;notes.IsEnabled=false;
        }
        return root;
    }

    private static Border PanelCard(UIElement content)=>new(){
        Child=content,CornerRadius=new CornerRadius(13),
        BorderThickness=new Thickness(1),
        BorderBrush=new SolidColorBrush(Color.FromRgb(224,222,237)),
        Background=Brushes.White,Padding=new Thickness(12,12,12,12)
    };
    private static TextBlock Caption(string title)=>new(){
        Text=title,FontSize=14,FontWeight=FontWeights.SemiBold,
        Foreground=Ink,Margin=new Thickness(0,0,0,8)
    };
    private static TextBlock Tiny(string title)=>new(){
        Text=title,FontSize=11,Foreground=Muted,Margin=new Thickness(1,6,0,3)
    };
    private static TextBox Field(string hint)=>new(){
        MinHeight=33,Padding=new Thickness(8,5,8,5),
        BorderBrush=new SolidColorBrush(Color.FromRgb(215,214,234)),
        Background=new SolidColorBrush(Color.FromRgb(251,250,254)),
        FontSize=13,ToolTip=hint
    };
}
