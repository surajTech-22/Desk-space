$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r14/DeskSpaceShellIconV14.cs' "$root/DeskSpaceShellIconV14.cs" -Force
function Load([string]$p){[IO.File]::ReadAllText($p).Replace([string][char]13+[char]10,[string][char]10)}
function Save([string]$p,[string]$s){[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))}
function Must([string]$old,[string]$new){
 $old=$old.Replace([string][char]13+[char]10,[string][char]10)
 $new=$new.Replace([string][char]13+[char]10,[string][char]10)
 if(!$script:s.Contains($old)){throw "R14 anchor not found: $old"}
 $script:s=$script:s.Replace($old,$new)
}
$p="$root/WidgetHost.cs";$s=Load $p
Must 'Width=Math.Min(limits.Width,Math.Clamp(limits.Width*.32,270,390));Height=58;' 'Width=Math.Min(limits.Width,CompactDiameter);Height=48;'
Must 'Height=Math.Min(ModuleArea().Height,Math.Clamp(height,58,390));' @'
        Width=Math.Min(ModuleArea().Width,height<=58?CompactDiameter:Math.Max(270,CompactDiameter*2.5));
        Height=Math.Min(ModuleArea().Height,Math.Clamp(height,48,340));
'@
Must @'
            var plate=new Border {
                Width=216,Height=116,CornerRadius=new CornerRadius(52),
                Background=new LinearGradientBrush(
                    Color.FromArgb(202,18,35,66),Color.FromArgb(225,39,33,83),90),
                IsHitTestVisible=false
            };
            Canvas.SetLeft(plate,38);Canvas.SetTop(plate,139);
            canvas.Children.Add(plate);
'@ @'
            // Weather text overlays the circle directly without an extra plate.
'@
Must @'
        content.FontSize = ringExpanded ? 18 :
            (prefs.CenterMode=="Clock" && prefs.ShowWeather &&
             DeskSpaceWeather.Latest!=null ? 29 : (prefs.CenterMode=="Notes"?30:32));
'@ @'
        content.FontSize = ringExpanded ? 18 :
            (prefs.CenterMode=="Clock" && prefs.ShowWeather &&
             DeskSpaceWeather.Latest!=null ? 29 : (prefs.CenterMode=="Notes"?30:32));
        var sky=DeskSpaceWeather.Latest;
        var hasSky=prefs.CenterMode=="Clock" && prefs.ShowWeather && sky!=null;
        content.Foreground=hasSky && sky!.IsDay && sky.Code<=1
            ? new SolidColorBrush(Color.FromRgb(24,31,60)) : Brushes.White;
        content.Effect=hasSky
            ? new System.Windows.Media.Effects.DropShadowEffect{
                Color=sky!.IsDay && sky.Code<=1
                    ? Color.FromArgb(160,255,250,233):Color.FromArgb(200,10,17,40),
                BlurRadius=4,ShadowDepth=0,Opacity=.85}
            : null;
'@
Save $p $s

$p="$root/DeskSpaceSearchV11.cs";$s=Load $p
Must 'var top=new Grid { Height=54,Margin=new Thickness(8,1,5,1) };' 'var top=new Grid { Height=46,Margin=new Thickness(3,0,3,0) };'
Must 'top.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(38) });' 'top.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(22) });'
Must 'top.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(47) });' 'top.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(25) });'
Must 'FontSize=32,Foreground=' 'FontSize=21,Foreground='
Must 'var field=new TextBox { FontSize=16,' 'var field=new TextBox { FontSize=13,'
Must 'var close=new Button { Content="×",FontSize=23,MinWidth=38,Padding=new Thickness(0),' 'var close=new Button { Content="×",FontSize=16,MinWidth=23,Padding=new Thickness(0),'
Must 'CornerRadius=new CornerRadius(19),' 'CornerRadius=new CornerRadius(23),'
Must 'var scroller=new ScrollViewer {MaxHeight=310,' 'var scroller=new ScrollViewer {MaxHeight=260,'
Must '? 70+Math.Min(310,Math.Max(1,count)*49):58);' '? 60+Math.Min(260,Math.Max(1,count)*43):48);'
Must 'foreach(var hit in found.Take(8))' 'foreach(var hit in found.Take(7))'
Must 'MinHeight=44};' 'MinHeight=40};'
Must '                layout.Children.Add(text);item.Child=layout;' @'
                var rowText=new StackPanel{Orientation=Orientation.Horizontal};
                if(hit.Category=="Applications" || hit.Category=="Files"){
                    var logo=DeskSpaceShellIconV14.Get(hit.OpenTarget);
                    if(logo!=null)rowText.Children.Add(new Image{
                        Source=logo,Width=20,Height=20,
                        Margin=new Thickness(0,1,7,0),
                        VerticalAlignment=VerticalAlignment.Center});
                }
                rowText.Children.Add(text);
                layout.Children.Add(rowText);item.Child=layout;
'@
Must 'frame.Loaded+=(_,_)=>{resize(58);field.Focus();Keyboard.Focus(field);};' 'frame.Loaded+=(_,_)=>{resize(48);field.Focus();Keyboard.Focus(field);};'
Save $p $s
$p="$root/DeskSpaceInlineV11.cs";$s=Load $p
Must 'var header=new Grid{Height=52,Margin=new Thickness(12,3,8,3)};' 'var header=new Grid{Height=44,Margin=new Thickness(8,2,6,2)};'
Must 'FontSize=18,FontWeight=FontWeights.SemiBold,Foreground=TextInk,' 'FontSize=15,FontWeight=FontWeights.SemiBold,Foreground=TextInk,'
Must 'var body=new StackPanel{Margin=new Thickness(12,2,12,12)};' 'var body=new StackPanel{Margin=new Thickness(9,1,9,8)};'
Must 'var memo=Field("Edit note",(int)Math.Clamp(SystemParameters.WorkArea.Height*.18,95,165));' 'var memo=Field("Edit note",(int)Math.Clamp(SystemParameters.WorkArea.Height*.16,90,150));'
Must 'var items=new ListBox{MaxHeight=122,MinHeight=70,' 'var items=new ListBox{MaxHeight=110,MinHeight=65,'
Save $p $s
Write-Host 'R14: compact search, system icons, no clock text plate, small notes and reminders'
