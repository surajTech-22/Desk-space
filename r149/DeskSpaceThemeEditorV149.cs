using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace DeskSpace;

// Draft settings never modify widget-v3.json, saved Excel mappings or
// application profiles. Save & apply is the sole persistent write path.
internal sealed class DeskSpaceThemeEditorV149 : Window
{
    private readonly DeskSpaceThemeV149 draft=DeskSpaceThemeV149.Load();
    private readonly Canvas donut=new(){Width=320,Height=320};
    private readonly TextBlock previewTitle=new(){FontSize=12,Margin=new Thickness(10),Text="LIVE PREVIEW"};
    private readonly TextBox colorInput=new(){Width=100,Height=28,VerticalContentAlignment=VerticalAlignment.Center};
    private readonly TextBlock opacityValue=new(){MinWidth=40};
    private readonly TextBlock softnessValue=new(){MinWidth=40};
    private readonly Action applied;
    private bool changing;
    private static readonly string[] Vibrant={"#7364D3","#1A9F7D","#E35E32","#3488D9","#CE4B82","#BD7B14","#619A2A","#656565"};
    private static readonly (string Name,string Hex)[] Pastels={
        ("Monochrome","#222222"),("Lavender","#8374E7"),
        ("Peach","#E87F72"),("Mint","#52A28A"),
        ("Rose","#DE618F"),("Sky","#5989E9"),
        ("Amber","#D18C30"),("Sage","#629A61")};

    internal DeskSpaceThemeEditorV149(Window owner,Action onApplied)
    {
        applied=onApplied;
        Title="DeskSpace · Theme and donut appearance";
        Owner=owner;
        Width=970;Height=700;MinWidth=800;MinHeight=560;
        ShowInTaskbar=false;WindowStartupLocation=WindowStartupLocation.CenterScreen;
        Background=Brushes.White;
        var shell=new DockPanel{Margin=new Thickness(16)};
        Content=shell;
        var bottom=new StackPanel{Orientation=Orientation.Horizontal,
            HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,12,0,0)};
        DockPanel.SetDock(bottom,Dock.Bottom);shell.Children.Add(bottom);
        var cancel=ActionButton("Cancel",()=>Close());
        var apply=ActionButton("Save & apply",Save);
        bottom.Children.Add(cancel);bottom.Children.Add(apply);

        var main=new Grid();main.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(2,GridUnitType.Star)});
        main.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(340)});
        shell.Children.Add(main);
        var pages=new TabControl{Margin=new Thickness(0,0,14,0)};
        Grid.SetColumn(pages,0);main.Children.Add(pages);
        pages.Items.Add(new TabItem{Header="Theme",Content=Scrolled(BuildTheme())});
        pages.Items.Add(new TabItem{Header="Donut",Content=Scrolled(BuildDonutOptions())});
        pages.Items.Add(new TabItem{Header="Presets",Content=Scrolled(BuildPresets())});
        var preview=new Border{BorderBrush=new SolidColorBrush(Color.FromRgb(219,219,237)),
            BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(14),Padding=new Thickness(8),
            Background=Brushes.White,Margin=new Thickness(4,0,0,0)};
        Grid.SetColumn(preview,1);main.Children.Add(preview);
        var display=new StackPanel{HorizontalAlignment=HorizontalAlignment.Center};
        display.Children.Add(previewTitle);display.Children.Add(donut);
        display.Children.Add(new TextBlock{Text="Preview only · live widget changes after Save & apply",
            TextWrapping=TextWrapping.Wrap,Margin=new Thickness(12,5,12,5),
            Foreground=Brushes.Gray,FontSize=11});
        preview.Child=display;
        colorInput.Text=draft.AccentHex;
        colorInput.TextChanged+=(_,_)=>{
            if(changing)return;
            if(DeskSpaceThemeV149.TryColor(colorInput.Text,out _))
            {draft.AccentHex=colorInput.Text;Redraw();}
        };
        Redraw();
    }
    private static Button ActionButton(string name,Action action)
    {
        var b=new Button{Content=name,MinWidth=100,Padding=new Thickness(10,6,10,6),
            Margin=new Thickness(5,0,0,0)};
        b.Click+=(_,_)=>action();return b;
    }
    private static ScrollViewer Scrolled(UIElement child)
        =>new(){Content=child,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
    private static TextBlock Heading(string title)=>new(){Text=title,FontSize=16,
        FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,14,0,9)};
    private static TextBlock Help(string text)=>new(){Text=text,TextWrapping=TextWrapping.Wrap,
        Foreground=new SolidColorBrush(Color.FromRgb(92,96,110)),
        Margin=new Thickness(0,2,0,10)};
    private StackPanel BuildTheme()
    {
        var panel=new StackPanel{Margin=new Thickness(12)};
        panel.Children.Add(Heading("Theme style"));
        var style=new ComboBox{Width=220,HorizontalAlignment=HorizontalAlignment.Left};
        style.Items.Add("Soft");style.Items.Add("Adaptive");
        style.SelectedItem=draft.Style;
        style.SelectionChanged+=(_,_)=>{draft.Style=style.SelectedItem?.ToString()??"Soft";Redraw();};
        panel.Children.Add(style);
        panel.Children.Add(Heading("Appearance"));
        var mode=new ComboBox{Width=220,HorizontalAlignment=HorizontalAlignment.Left};
        foreach(var value in new[]{"System","Light","Dark"})mode.Items.Add(value);
        mode.SelectedItem=draft.Appearance;
        mode.SelectionChanged+=(_,_)=>{draft.Appearance=mode.SelectedItem?.ToString()??"System";Redraw();};
        panel.Children.Add(mode);
        panel.Children.Add(Heading("Accent colour"));
        panel.Children.Add(Help("Choose an accent or enter a #RRGGBB colour. The saved colour is used by the real DeskSpace donut."));
        var swatches=new WrapPanel{Margin=new Thickness(0,0,0,12)};
        foreach(var hex in Vibrant)
        {
            var button=new Button{Width=35,Height=35,Margin=new Thickness(2),
                Background=new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!),
                ToolTip=hex,BorderThickness=new Thickness(1),BorderBrush=Brushes.LightGray};
            button.Click+=(_,_)=>SetHex(hex);swatches.Children.Add(button);
        }
        panel.Children.Add(swatches);
        var colorRow=new StackPanel{Orientation=Orientation.Horizontal};
        colorRow.Children.Add(new TextBlock{Text="Custom",VerticalAlignment=VerticalAlignment.Center,
            Margin=new Thickness(0,0,10,0)});
        colorRow.Children.Add(colorInput);
        panel.Children.Add(colorRow);
        return panel;
    }
    private StackPanel BuildDonutOptions()
    {
        var panel=new StackPanel{Margin=new Thickness(12)};
        panel.Children.Add(Heading("Donut appearance"));
        panel.Children.Add(Help("The existing module order, Excel shortcuts, and ring hit targets are retained."));
        panel.Children.Add(new TextBlock{Text="Softness / shadow"});
        var shadow=new Slider{Minimum=0,Maximum=24,Value=draft.Softness,TickFrequency=1,
            IsSnapToTickEnabled=true,Margin=new Thickness(0,5,0,0)};
        shadow.ValueChanged+=(_,_)=>{draft.Softness=(int)shadow.Value;
            softnessValue.Text=draft.Softness.ToString();Redraw();};
        panel.Children.Add(shadow);softnessValue.Text=draft.Softness.ToString();panel.Children.Add(softnessValue);
        panel.Children.Add(Heading("Donut toolbar opacity"));
        var opacity=new Slider{Minimum=55,Maximum=100,Value=draft.DonutOpacity,TickFrequency=1,
            IsSnapToTickEnabled=true,Margin=new Thickness(0,5,0,0)};
        opacity.ValueChanged+=(_,_)=>{draft.DonutOpacity=(int)opacity.Value;
            opacityValue.Text=draft.DonutOpacity+"%";Redraw();};
        panel.Children.Add(opacity);opacityValue.Text=draft.DonutOpacity+"%";panel.Children.Add(opacityValue);
        panel.Children.Add(Heading("Ring geometry"));
        panel.Children.Add(Help("Ring size and centre-hole geometry stay fixed until matching hit-testing, sub-donut placement, and drag behaviour can be changed together. This prevents invisible clickable areas."));
        return panel;
    }
    private StackPanel BuildPresets()
    {
        var panel=new StackPanel{Margin=new Thickness(12)};
        panel.Children.Add(Heading("Accent presets"));
        panel.Children.Add(Help("Presets update this preview; click Save & apply to change DeskSpace. Existing Workspace profiles and pinned apps remain untouched."));
        var items=new WrapPanel();
        foreach(var (name,hex) in Pastels)
        {
            var b=new Button{Content=name,MinWidth=100,Margin=new Thickness(4),
                Padding=new Thickness(8,6,8,6)};
            b.Click+=(_,_)=>SetHex(hex);items.Children.Add(b);
        }
        panel.Children.Add(items);
        return panel;
    }
    private void SetHex(string hex)
    {
        draft.AccentHex=hex;
        changing=true;colorInput.Text=hex;changing=false;
        Redraw();
    }
    private void Redraw()
    {
        donut.Children.Clear();
        var accent=draft.Accent;
        var light=draft.LightColor;
        var alt=draft.AltColor;
        var dark=draft.IsDark;
        var canvasBrush=new SolidColorBrush(dark?Color.FromRgb(36,39,61):Color.FromRgb(245,245,250));
        var background=new Ellipse{Width=300,Height=300,Fill=canvasBrush,
            Effect=new DropShadowEffect{Color=draft.Accent,
                BlurRadius=draft.Softness,ShadowDepth=1,Opacity=.28}};
        Canvas.SetLeft(background,10);Canvas.SetTop(background,10);donut.Children.Add(background);
        for(int i=0;i<12;i++)
        {
            double start=-90+(i%6)*60-29;
            double end=start+58;
            bool outer=i>=6;
            var wedge=new System.Windows.Shapes.Path{Data=Sector(160,160,outer?102:54,outer?148:97,start,end),
                Fill=new SolidColorBrush(i==0?accent:(i%2==0?light:alt)),
                Opacity=draft.Opacity,Stroke=Brushes.White,StrokeThickness=1.8};
            donut.Children.Add(wedge);
        }
        var centre=new Ellipse{Width=103,Height=103,Fill=canvasBrush,
            Stroke=new SolidColorBrush(accent),StrokeThickness=2};
        Canvas.SetLeft(centre,108.5);Canvas.SetTop(centre,108.5);donut.Children.Add(centre);
        var label=new TextBlock{Text="DeskSpace",FontSize=14,FontWeight=FontWeights.SemiBold,
            Foreground=new SolidColorBrush(draft.TextColor),Width=100,
            TextAlignment=TextAlignment.Center};
        Canvas.SetLeft(label,110);Canvas.SetTop(label,150);donut.Children.Add(label);
        previewTitle.Foreground=new SolidColorBrush(accent);
    }
    private static Geometry Sector(double cx,double cy,double inner,double outer,double a,double b)
    {
        Point At(double r,double d){double rad=d*Math.PI/180;
            return new Point(cx+r*Math.Cos(rad),cy+r*Math.Sin(rad));}
        var figure=new PathFigure{StartPoint=At(inner,a),IsClosed=true,IsFilled=true};
        figure.Segments.Add(new LineSegment(At(outer,a),true));
        figure.Segments.Add(new ArcSegment(At(outer,b),new Size(outer,outer),0,false,SweepDirection.Clockwise,true));
        figure.Segments.Add(new LineSegment(At(inner,b),true));
        figure.Segments.Add(new ArcSegment(At(inner,a),new Size(inner,inner),0,false,SweepDirection.Counterclockwise,true));
        return new PathGeometry(new[]{figure});
    }
    private void Save()
    {
        if(!DeskSpaceThemeV149.TryColor(colorInput.Text,out _))
        {
            MessageBox.Show(this,"Enter a valid accent colour in #RRGGBB format.","DeskSpace",
                MessageBoxButton.OK,MessageBoxImage.Warning);
            colorInput.Focus();return;
        }
        draft.AccentHex=colorInput.Text;
        try{draft.Save();applied();Close();}
        catch(Exception ex){MessageBox.Show(this,"Theme could not be saved: "+ex.Message,
            "DeskSpace",MessageBoxButton.OK,MessageBoxImage.Error);}
    }
}
