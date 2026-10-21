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

// Ordinary non-password data only. Any password selected in the Information
// Manager is routed to the existing AES-GCM vault, NEVER persisted here.
internal sealed class DeskSpaceInformationV22
{
    internal sealed class Record
    {
        public Guid Id {get;set;}=Guid.NewGuid();
        public string Title {get;set;}="";
        public string Category {get;set;}="General";
        public string Value {get;set;}="";
        public string Notes {get;set;}="";
        public DateTime UpdatedUtc {get;set;}=DateTime.UtcNow;
        public override string ToString()=>Category+" · "+Title;
    }
    private static readonly string Location=Path.Combine(WorkspaceStore.Root,
        "information-items-v22.json");
    internal static List<Record> Read()
    {
        try{
            if(!File.Exists(Location))return new();
            return JsonSerializer.Deserialize<List<Record>>(File.ReadAllText(Location))??new();
        }catch(Exception){return new();}
    }
    private static void Write(List<Record> records)
    {
        Directory.CreateDirectory(WorkspaceStore.Root);
        var tmp=Location+".pending";
        File.WriteAllText(tmp,JsonSerializer.Serialize(records,new JsonSerializerOptions{
            WriteIndented=true}),new UTF8Encoding(false));
        File.Move(tmp,Location,true);
    }
    public FrameworkElement Create()
    {
        var ink=new SolidColorBrush(Color.FromRgb(37,45,71));
        var root=new StackPanel{Margin=new Thickness(0,5,0,7)};
        var status=new TextBlock{Foreground=new SolidColorBrush(Color.FromRgb(105,110,132)),
            FontSize=12,TextWrapping=TextWrapping.Wrap,
            Margin=new Thickness(0,1,0,8),
            Text="Ordinary information is stored locally, unencrypted. Choose Password above for the encrypted security vault."};
        root.Children.Add(status);
        var filter=new TextBox{MinHeight=31,Padding=new Thickness(8,5,8,5),
            ToolTip="Search saved information by title or category (case-insensitive)",
            Margin=new Thickness(0,2,0,7)};
        root.Children.Add(filter);
        var records=new ListBox{MinHeight=90,MaxHeight=180,Margin=new Thickness(0,1,0,8)};
        root.Children.Add(records);
        TextBox Field(string hint,int height=34)
        {
            return new TextBox{MinHeight=height,Padding=new Thickness(8,5,8,5),
                ToolTip=hint,Margin=new Thickness(0,2,0,7)};
        }
        root.Children.Add(new TextBlock{Text="Category",Foreground=ink,FontSize=12});
        var category=new ComboBox{
            ItemsSource=new[]{"General","Contact","Reference","Document","Custom"},
            SelectedIndex=0,MinHeight=32,Margin=new Thickness(0,2,0,7)};
        root.Children.Add(category);
        var title=Field("Information title");root.Children.Add(title);
        var value=Field("Information / copyable value",74);
        value.AcceptsReturn=true;value.TextWrapping=TextWrapping.Wrap;
        root.Children.Add(value);
        var notes=Field("Notes (optional)",55);
        notes.AcceptsReturn=true;notes.TextWrapping=TextWrapping.Wrap;
        root.Children.Add(notes);
        var controls=new WrapPanel{Margin=new Thickness(0,1,0,1)};
        root.Children.Add(controls);
        Guid? editing=null;
        void Refresh()
        {
            string q=filter.Text.Trim();
            var items=Read().Where(x=>q.Length==0 ||
                x.Title.Contains(q,StringComparison.OrdinalIgnoreCase)||
                x.Category.Contains(q,StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x=>x.UpdatedUtc).ToList();
            records.ItemsSource=items;
        }
        Button Button(string caption,Action action)
        {
            var b=new Button{Content=caption,Padding=new Thickness(12,6,12,6),
                MinHeight=32,Margin=new Thickness(0,2,7,3)};
            b.Click+=(_,_)=>{
                try{action();}
                catch(Exception e){MessageBox.Show(e.Message,"DeskSpace information",
                    MessageBoxButton.OK,MessageBoxImage.Warning);}
            };
            controls.Children.Add(b);return b;
        }
        records.SelectionChanged+=(_,_)=>{
            if(records.SelectedItem is not Record item)return;
            editing=item.Id;
            category.SelectedItem=item.Category;
            title.Text=item.Title;value.Text=item.Value;notes.Text=item.Notes;
        };
        Button("Save",()=>{
            if(string.IsNullOrWhiteSpace(title.Text))
                throw new ArgumentException("Enter an information title.");
            if(title.Text.Contains("password",StringComparison.OrdinalIgnoreCase) ||
                (category.SelectedItem as string)=="Password")
                throw new InvalidOperationException(
                    "Password entries must be saved in Security Vault. Select Password above.");
            var all=Read();
            var rec=all.FirstOrDefault(x=>x.Id==editing);
            if(rec==null){rec=new Record();all.Add(rec);}
            rec.Title=title.Text.Trim();rec.Category=category.SelectedItem as string??"General";
            rec.Value=value.Text;rec.Notes=notes.Text;rec.UpdatedUtc=DateTime.UtcNow;
            Write(all);editing=rec.Id;Refresh();
            status.Text="Saved ordinary information locally. Passwords remain in Security Vault.";
        });
        Button("New",()=>{editing=null;records.SelectedItem=null;title.Clear();
            value.Clear();notes.Clear();category.SelectedIndex=0;});
        Button("Copy value",()=>{
            if(records.SelectedItem is not Record item)return;
            Clipboard.SetText(item.Value);status.Text="Value copied.";
        });
        Button("Delete",()=>{
            if(records.SelectedItem is not Record item)return;
            if(MessageBox.Show("Delete selected information?", "DeskSpace",
                MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
            var all=Read();all.RemoveAll(x=>x.Id==item.Id);Write(all);
            editing=null;Refresh();
        });
        filter.TextChanged+=(_,_)=>Refresh();
        Refresh();
        return root;
    }
}
