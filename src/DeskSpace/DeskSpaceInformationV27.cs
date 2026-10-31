using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace DeskSpace;

// A contextual PARTIAL arc and one compact shelf; never a new application
// window. Password rows reference the existing authenticated AES-GCM vault.
// Original V22/V25 information remains unmodified and is imported read-only.
internal sealed class DeskSpaceInformationV27 : IDisposable
{
    internal sealed class Item
    {
        public Guid Id {get;set;}=Guid.NewGuid();
        public string Label {get;set;}="";
        public string Value {get;set;}="";
        public Guid? VaultId {get;set;}
    }
    internal sealed class Collection
    {
        public Guid Id {get;set;}=Guid.NewGuid();
        public string Name {get;set;}="";
        public DateTime UpdatedUtc {get;set;}=DateTime.UtcNow;
        public List<Item> Rows {get;set;}=new();
    }
    private sealed class LegacyRow
    {
        public Guid Id {get;set;}
        public string Title {get;set;}="";
        public string Category {get;set;}="";
        public string Value {get;set;}="";
        public string Notes {get;set;}="";
        public DateTime UpdatedUtc {get;set;}
    }
    private sealed class RowEditor
    {
        public ComboBox Choice=null!;
        public TextBox Custom=null!;
        public TextBox Value=null!;
        public PasswordBox Password=null!;
        public Guid? VaultId;
    }

    private readonly Window owner;
    private readonly Action changed;
    private readonly DeskSpaceVaultV10 vault=new();
    private readonly DispatcherTimer lockTimer;
    private readonly Popup arc;
    private readonly Popup shelf;
    private readonly Canvas arcCanvas;
    private readonly Border shelfBorder;
    private readonly StackPanel shelfBody;
    private readonly TextBlock shelfTitle;
    private readonly TextBlock warning;
    private readonly PasswordBox master;
    private readonly StackPanel unlockBar;
    private static readonly string Folder=WorkspaceStore.Root;
    private static readonly string CollectionsPath=Path.Combine(Folder,"information-collections-v26.json");
    private static readonly string LegacyPath=Path.Combine(Folder,"information-items-v22.json");
    private static readonly Brush Ink=new SolidColorBrush(Color.FromRgb(41,48,78));
    private static readonly Brush Accent=new SolidColorBrush(Color.FromRgb(97,82,180));
    private static readonly Brush Pale=new SolidColorBrush(Color.FromRgb(239,237,251));
    private int startIndex;
    private DateTime lastVaultAccess=DateTime.UtcNow;
    private bool disposed;
    private List<Collection> all=new();
    private readonly List<RowEditor> editorRows=new();
    private readonly List<TextBlock> revealedValues=new();
    private bool addingRow;
    private Guid? editingId;

    public bool IsOpen => arc.IsOpen || shelf.IsOpen;
    public int SavedCount => all.Count;
    public DeskSpaceInformationV27(Window window,Action onChanged)
    {
        owner=window;changed=onChanged;
        arcCanvas=new Canvas{Width=488,Height=211};
        arc=new Popup{
            PlacementTarget=owner,Placement=PlacementMode.Relative,
            HorizontalOffset=-39,VerticalOffset=-102,
            AllowsTransparency=true,StaysOpen=true,Child=arcCanvas};
        shelfBody=new StackPanel{Margin=new Thickness(11,6,11,10)};
        shelfTitle=new TextBlock{FontSize=14,FontWeight=FontWeights.SemiBold,
            Foreground=Ink,VerticalAlignment=VerticalAlignment.Center};
        var header=new DockPanel{
            LastChildFill=true,Background=Pale,Height=35,
            Margin=new Thickness(0,0,0,3)};
        var x=SmallButton("×",()=>{shelf.IsOpen=false;MaskRevealed();vault.Lock();});
        x.FontSize=16;x.MinWidth=30;
        x.Margin=new Thickness(4,3,6,3);
        DockPanel.SetDock(x,Dock.Right);
        header.Children.Add(x);
        var h=new Border{Padding=new Thickness(10,7,0,0),Child=shelfTitle};
        header.Children.Add(h);
        var scroll=new ScrollViewer{
            VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,
            MaxHeight=285,Content=shelfBody};
        warning=new TextBlock{Foreground=new SolidColorBrush(Color.FromRgb(163,64,63)),
            FontSize=11,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(8,2,8,0)};
        var frame=new DockPanel{LastChildFill=true};
        DockPanel.SetDock(header,Dock.Top);frame.Children.Add(header);
        DockPanel.SetDock(warning,Dock.Bottom);frame.Children.Add(warning);
        frame.Children.Add(scroll);
        shelfBorder=new Border{Width=398,MaxHeight=356,
            Background=Brushes.White,BorderBrush=new SolidColorBrush(Color.FromRgb(201,208,230)),
            BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(13),
            Child=frame,Effect=new System.Windows.Media.Effects.DropShadowEffect{
                BlurRadius=14,Opacity=.17,ShadowDepth=3}};
        shelf=new Popup{
            PlacementTarget=owner,Placement=PlacementMode.Relative,
            HorizontalOffset=5,VerticalOffset=owner.Height+7,
            AllowsTransparency=true,StaysOpen=true,Child=shelfBorder};
        master=new PasswordBox{MinWidth=155,Height=29,Padding=new Thickness(6,3,6,3)};
        unlockBar=new StackPanel{Visibility=Visibility.Collapsed,
            Margin=new Thickness(2,5,2,7)};
        lockTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(15)};
        lockTimer.Tick+=(_,_)=>{
            if(vault.IsUnlocked && DateTime.UtcNow-lastVaultAccess>TimeSpan.FromMinutes(3)){
                vault.Lock();MaskRevealed();master.Clear();
                if(shelf.IsOpen)warning.Text="Vault locked after inactivity.";
            }
        };
        lockTimer.Start();
    }
    private static Button SmallButton(string text,Action action)
    {
        var b=new Button{
            Content=text,MinHeight=27,Padding=new Thickness(8,3,8,3),
            Margin=new Thickness(3,2,3,2),
            Background=Pale,Foreground=Ink,
            BorderThickness=new Thickness(0),
            Cursor=Cursors.Hand,FontSize=12};
        b.Click+=(_,_)=>action();
        return b;
    }
    private static List<Collection> Read()
    {
        if(File.Exists(CollectionsPath))
        {
            try{
                var data=JsonSerializer.Deserialize<List<Collection>>(File.ReadAllText(CollectionsPath))
                    ??throw new InvalidDataException("Invalid information collections.");
                foreach(var c in data)c.Rows??=new List<Item>();
                return data;
            }
            catch(Exception ex){throw new InvalidDataException(
                "Saved information cannot be read. Existing files were not changed.",ex);}
        }
        // Read-only, lossless backward compatibility for one-record-per-value
        // schema. Do not overwrite, delete or relocate V22/V25 entries.
        if(!File.Exists(LegacyPath))return new();
        try
        {
            var legacy=JsonSerializer.Deserialize<List<LegacyRow>>(File.ReadAllText(LegacyPath))
                ??new List<LegacyRow>();
            return legacy.Select(x=>new Collection{
                Id=x.Id==Guid.Empty?Guid.NewGuid():x.Id,
                Name=string.IsNullOrWhiteSpace(x.Title)?"Imported information":x.Title,
                UpdatedUtc=x.UpdatedUtc,
                Rows=new List<Item>{
                    new(){Label="Value",Value=x.Value},
                    new(){Label="Category",Value=x.Category},
                    new(){Label="Notes",Value=x.Notes}
                }.Where(row=>!string.IsNullOrWhiteSpace(row.Value)).ToList()
            }).ToList();
        }
        catch(Exception ex){throw new InvalidDataException(
            "Legacy information cannot be read. Original data was not changed.",ex);}
    }
    private static void Write(List<Collection> data)
    {
        Directory.CreateDirectory(Folder);
        string temp=CollectionsPath+".pending";
        try
        {
            File.WriteAllText(temp,JsonSerializer.Serialize(data,new JsonSerializerOptions{
                WriteIndented=true}),new UTF8Encoding(false));
            if(File.Exists(CollectionsPath))
                File.Replace(temp,CollectionsPath,CollectionsPath+".previous",true);
            else File.Move(temp,CollectionsPath);
        }
        finally{if(File.Exists(temp))File.Delete(temp);}
    }
    public void Show()
    {
        if(disposed)return;
        try
        {
            all=Read().OrderByDescending(x=>x.UpdatedUtc).ToList();
            startIndex=Math.Min(startIndex,Math.Max(0,all.Count-4));
            RenderArc();
            arc.IsOpen=true;
            changed();
        }
        catch(Exception ex){MessageBox.Show(ex.Message,"DeskSpace information",
            MessageBoxButton.OK,MessageBoxImage.Warning);}
    }
    public void Hide()
    {
        arc.IsOpen=false;shelf.IsOpen=false;
        MaskRevealed();vault.Lock();master.Clear();
        warning.Text="";
        changed();
    }
    // Radially connected wedges instead of R26's detached horizontal
    // arc. Both radii exceed the parent donut's 195-DIP outer edge.
    private static Geometry ChildSector(double first, double last)
    {
        const double cx=244,cy=306,r0=203,r1=265;
        static Point At(double x,double y,double radius,double angle)
        {
            double a=angle*Math.PI/180;
            return new Point(x+radius*Math.Cos(a),y+radius*Math.Sin(a));
        }
        var figure=new PathFigure{
            StartPoint=At(cx,cy,r1,first),IsClosed=true};
        figure.Segments.Add(new ArcSegment{
            Point=At(cx,cy,r1,last),Size=new Size(r1,r1),
            SweepDirection=SweepDirection.Clockwise});
        figure.Segments.Add(new LineSegment(At(cx,cy,r0,last),true));
        figure.Segments.Add(new ArcSegment{
            Point=At(cx,cy,r0,first),Size=new Size(r0,r0),
            SweepDirection=SweepDirection.Counterclockwise});
        return new PathGeometry(new[]{figure});
    }
    private void RenderArc()
    {
        arcCanvas.Children.Clear();
        const int count=5;
        const double start=-153,finish=-27,span=(finish-start)/count;
        var visible=all.Skip(startIndex).Take(4).ToArray();
        for(int i=0;i<count;i++)
        {
            int sector=i;
            var color=i==count-1?Accent:
                new SolidColorBrush(Color.FromRgb(220,229,251));
            var wedge=new System.Windows.Shapes.Path{
                Data=ChildSector(start+i*span,start+(i+1)*span),
                Stroke=Brushes.White,StrokeThickness=2,
                Fill=color,IsHitTestVisible=true,Cursor=Cursors.Hand};
            wedge.MouseLeftButtonUp+=(_,e)=>{
                if(sector==4)NewCollection();
                else if(sector<visible.Length)OpenCollection(visible[sector].Id);
                e.Handled=true;
            };
            wedge.MouseEnter+=(_,_)=>wedge.Opacity=.78;
            wedge.MouseLeave+=(_,_)=>wedge.Opacity=1;
            arcCanvas.Children.Add(wedge);
        }
        for(int i=0;i<5;i++)
        {
            int slot=i;
            double mid=(start+(i+.5)*span)*Math.PI/180;
            double x=244+235*Math.Cos(mid);
            double y=306+235*Math.Sin(mid);
            bool add=i==4;
            var name=add?"+ New":i<visible.Length?visible[i].Name:"";
            if(name.Length==0)continue;
            var b=SmallButton(name,()=>{
                if(add)NewCollection();
                else OpenCollection(visible[slot].Id);
            });
            b.Width=80;b.Height=30;
            b.FontSize=11;
            b.ToolTip=add?"Create a named information collection":name;
            b.Padding=new Thickness(2,3,2,3);
            b.Background=Brushes.Transparent;
            b.Foreground=add?Brushes.White:Ink;
            b.BorderThickness=new Thickness(0);
            Canvas.SetLeft(b,x-40);Canvas.SetTop(b,y-15);
            arcCanvas.Children.Add(b);
        }
        var left=SmallButton("‹",()=>Slide(-1));
        left.Width=29;left.Height=28;left.FontSize=18;
        left.IsEnabled=startIndex>0;
        left.ToolTip="Previous saved collections";
        Canvas.SetLeft(left,5);Canvas.SetTop(left,174);
        arcCanvas.Children.Add(left);
        var right=SmallButton("›",()=>Slide(1));
        right.Width=29;right.Height=28;right.FontSize=18;
        right.IsEnabled=startIndex+4<all.Count;
        right.ToolTip="Next saved collections";
        Canvas.SetLeft(right,454);Canvas.SetTop(right,174);
        arcCanvas.Children.Add(right);
        arcCanvas.MouseWheel-=ScrollArc;
        arcCanvas.MouseWheel+=ScrollArc;
    }
    private void ScrollArc(object sender,MouseWheelEventArgs e)
    {
        Slide(e.Delta<0?1:-1);e.Handled=true;
    }
    private void Slide(int delta)
    {
        startIndex=Math.Clamp(startIndex+delta,0,Math.Max(0,all.Count-4));
        RenderArc();
    }
    private void Shelf(string title)
    {
        MaskRevealed();
        shelfTitle.Text=title;
        shelfBody.Children.Clear();
        warning.Text="";
        shelf.VerticalOffset=owner.Height+7;
        shelf.IsOpen=true;
    }
    private void NewCollection()
    {
        editingId=null;
        OpenEditor(new Collection());
    }
    private void OpenCollection(Guid id)
    {
        var collection=all.FirstOrDefault(x=>x.Id==id);
        if(collection==null)return;
        Shelf(collection.Name);
        var edit=SmallButton("Edit",()=>OpenEditor(collection));
        edit.HorizontalAlignment=HorizontalAlignment.Right;
        shelfBody.Children.Add(edit);
        foreach(var row in collection.Rows)
        {
            var line=new Grid{Margin=new Thickness(0,2,0,3)};
            line.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(106)});
            line.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
            line.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
            var label=new TextBlock{Text=row.Label,FontSize=12,
                FontWeight=FontWeights.SemiBold,Foreground=Ink,
                VerticalAlignment=VerticalAlignment.Center,
                TextTrimming=TextTrimming.CharacterEllipsis};
            line.Children.Add(label);
            var actual=row.VaultId!=null?"••••••••":row.Value;
            var val=new TextBlock{Text=actual,FontSize=12,Foreground=Ink,
                TextTrimming=TextTrimming.CharacterEllipsis,
                VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(4,0,4,0)};
            Grid.SetColumn(val,1);line.Children.Add(val);
            var button=SmallButton(row.VaultId!=null?"◉":"⧉",()=>{
                if(row.VaultId!=null)
                {
                    if(!vault.IsUnlocked){OfferVaultUnlock();return;}
                    var s=vault.Entries.FirstOrDefault(z=>z.Id==row.VaultId);
                    if(s==null){warning.Text="The linked vault entry was not found.";return;}
                    TouchVault();
                    if(val.Text=="••••••••") {val.Text=s.Password;revealedValues.Add(val);}
                    else {val.Text="••••••••";revealedValues.Remove(val);}
                }
                else if(row.Value.Length>0)Clipboard.SetText(row.Value);
            });
            button.ToolTip=row.VaultId!=null?"Reveal after vault unlock":"Copy exact value";
            Grid.SetColumn(button,2);line.Children.Add(button);
            shelfBody.Children.Add(line);
        }
    }
    private void OfferVaultUnlock()
    {
        if(vault.IsUnlocked)return;
        if(unlockBar.Parent is Panel p)p.Children.Remove(unlockBar);
        unlockBar.Children.Clear();
        unlockBar.Children.Add(new TextBlock{Text=vault.IsInitialized?
            "Unlock Security Vault to use PW":"Create Security Vault (master password, 12+ characters)",
            Foreground=Ink,FontSize=11,Margin=new Thickness(0,0,0,3)});
        if(master.Parent is Panel previous)previous.Children.Remove(master);
        unlockBar.Children.Add(master);
        unlockBar.Children.Add(SmallButton("Unlock vault",()=>{
            try
            {
                vault.Unlock(master.Password);master.Clear();
                TouchVault();unlockBar.Visibility=Visibility.Collapsed;
                warning.Text="";
            }
            catch(Exception){warning.Text="Unable to unlock vault. Check the master password.";}
        }));
        unlockBar.Visibility=Visibility.Visible;
        shelfBody.Children.Insert(0,unlockBar);
    }
    private void TouchVault()=>lastVaultAccess=DateTime.UtcNow;
    private void OpenEditor(Collection collection)
    {
        editingId=all.Any(x=>x.Id==collection.Id)?collection.Id:null;
        Shelf(editingId==null?"New information":"Edit information");
        var name=new TextBox{Text=collection.Name,Height=30,
            Padding=new Thickness(7,4,7,4),Margin=new Thickness(0,0,0,8),
            ToolTip="Name this information collection"};
        shelfBody.Children.Add(new TextBlock{Text="Name",FontSize=11,Foreground=Ink});
        shelfBody.Children.Add(name);
        var rowsHost=new StackPanel();
        shelfBody.Children.Add(rowsHost);
        editorRows.Clear();
        void AddRow(Item? old=null)
        {
            if(addingRow)return;
            addingRow=true;
            try
            {
                var item=new RowEditor{VaultId=old?.VaultId};
                var wrap=new Grid{Margin=new Thickness(0,1,0,5)};
                wrap.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(93)});
                wrap.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
                item.Choice=new ComboBox{
                    ItemsSource=new[]{"UI","PW","Other"},SelectedIndex=0,
                    FontSize=11,Height=29,Margin=new Thickness(0,0,4,0)};
                wrap.Children.Add(item.Choice);
                var fields=new StackPanel();Grid.SetColumn(fields,1);wrap.Children.Add(fields);
                item.Custom=new TextBox{Height=28,Visibility=Visibility.Collapsed,
                    Padding=new Thickness(5,3,5,3),ToolTip="Custom label",
                    Margin=new Thickness(0,0,0,4)};
                item.Value=new TextBox{Height=29,Padding=new Thickness(5,3,5,3),
                    ToolTip="Value"};
                item.Password=new PasswordBox{Height=29,Padding=new Thickness(5,3,5,3),
                    Visibility=Visibility.Collapsed,ToolTip="Stored encrypted in Security Vault"};
                fields.Children.Add(item.Custom);fields.Children.Add(item.Value);
                fields.Children.Add(item.Password);
                rowsHost.Children.Add(wrap);
                editorRows.Add(item);
                if(old!=null)
                {
                    if(old.VaultId!=null || old.Label=="PW"){
                        item.Choice.SelectedItem="PW";
                    }
                    else if(old.Label=="UI")item.Choice.SelectedItem="UI";
                    else {item.Choice.SelectedItem="Other";item.Custom.Text=old.Label;}
                    item.Value.Text=old.VaultId==null?old.Value:"";
                    if(old.VaultId!=null)item.Password.ToolTip="Existing password preserved unless replaced";
                }
                void MaybeAppend()
                {
                    if(addingRow||editorRows.Count==0||editorRows[^1]!=item)return;
                    bool typed=!string.IsNullOrWhiteSpace(item.Custom.Text)||
                        !string.IsNullOrWhiteSpace(item.Value.Text)||
                        item.Password.Password.Length>0;
                    if(typed)AddRow();
                }
                item.Custom.TextChanged+=(_,_)=>MaybeAppend();
                item.Value.TextChanged+=(_,_)=>MaybeAppend();
                item.Password.PasswordChanged+=(_,_)=>MaybeAppend();
                item.Choice.SelectionChanged+=(_,_)=>{
                    var selected=item.Choice.SelectedItem as string;
                    item.Custom.Visibility=selected=="Other"?Visibility.Visible:Visibility.Collapsed;
                    item.Value.Visibility=selected=="PW"?Visibility.Collapsed:Visibility.Visible;
                    item.Password.Visibility=selected=="PW"?Visibility.Visible:Visibility.Collapsed;
                    if(selected=="PW")OfferVaultUnlock();
                };
            }
            finally{addingRow=false;}
        }
        foreach(var row in collection.Rows)AddRow(row);
        AddRow(); // always exactly one trailing empty row
        var commands=new WrapPanel{Margin=new Thickness(0,5,0,2)};
        commands.Children.Add(SmallButton("Save",()=>SaveEdited(name.Text,collection)));
        commands.Children.Add(SmallButton("Cancel",()=>{
            if(editingId!=null)OpenCollection(editingId.Value);
            else shelf.IsOpen=false;
        }));
        shelfBody.Children.Add(commands);
    }
    private void SaveEdited(string title,Collection source)
    {
        try
        {
            title=title.Trim();
            if(title.Length==0){warning.Text="Enter a collection name.";return;}
            var rows=new List<Item>();
            var secrets=new List<(Guid id,string password)>();
            foreach(var entry in editorRows)
            {
                string type=entry.Choice.SelectedItem as string??"UI";
                string label=type=="Other"?entry.Custom.Text.Trim():type;
                bool secret=type=="PW";
                string value=secret?"":entry.Value.Text;
                bool hasPassword=secret&&entry.Password.Password.Length>0;
                if(label.Length==0&&string.IsNullOrWhiteSpace(value)&&!hasPassword)continue;
                if(label.Length==0){warning.Text="Enter a label for the custom row.";return;}
                if(secret)
                {
                    if(entry.VaultId==null&&!hasPassword)continue;
                    if(hasPassword && !vault.IsUnlocked)
                    {
                        warning.Text="Unlock the Security Vault to save the PW row.";
                        OfferVaultUnlock();return;
                    }
                    var id=entry.VaultId??Guid.NewGuid();
                    rows.Add(new Item{Label="PW",Value="",VaultId=id});
                    if(hasPassword)secrets.Add((id,entry.Password.Password));
                }
                else
                {
                    if(value.Length==0)continue;
                    rows.Add(new Item{Label=label,Value=value});
                }
            }
            if(rows.Count==0){warning.Text="Enter at least one information value.";return;}
            // Commit PW only to the encrypted vault before writing references.
            foreach(var entry in secrets)
                vault.Upsert(new DeskSpaceVaultV10.Secret{
                    Id=entry.id,Label="DeskSpace information / "+title,
                    Password=entry.password});
            if(secrets.Count>0)TouchVault();
            var updated=new Collection{
                Id=editingId??source.Id,
                Name=title,Rows=rows,UpdatedUtc=DateTime.UtcNow};
            var data=Read();
            int index=data.FindIndex(x=>x.Id==updated.Id);
            if(index>=0)data[index]=updated;else data.Add(updated);
            Write(data);
            all=Read().OrderByDescending(x=>x.UpdatedUtc).ToList();
            RenderArc();changed();
            OpenCollection(updated.Id);
            warning.Text="";
        }
        catch(Exception ex){warning.Text=ex.Message;}
    }
    private void MaskRevealed()
    {
        foreach(var value in revealedValues)value.Text="••••••••";
        revealedValues.Clear();
    }
    public void Dispose()
    {
        if(disposed)return;
        disposed=true;
        arc.IsOpen=false;shelf.IsOpen=false;
        MaskRevealed();
        arcCanvas.MouseWheel-=ScrollArc;
        lockTimer.Stop();
        vault.Dispose();
    }
}
