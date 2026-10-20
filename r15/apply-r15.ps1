$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r15/DeskSpaceDropShelfV15.cs' "$root/DeskSpaceDropShelfV15.cs" -Force
Copy-Item 'r15/DeskSpaceSearchIndexV15.cs' "$root/DeskSpaceSearchIndexV13.cs" -Force
Copy-Item 'r15/DeskSpaceWeatherSceneV15.cs' "$root/DeskSpaceWeatherSceneV11.cs" -Force
function ReadSource([string]$path){
    [IO.File]::ReadAllText($path).Replace([string][char]13+[char]10,[string][char]10)
}
function SaveSource([string]$path,[string]$value){
    [IO.File]::WriteAllText($path,$value,[Text.UTF8Encoding]::new($false))
}
function ReplaceOnce([string]$old,[string]$new) {
    $old=$old.Replace([string][char]13+[char]10,[string][char]10)
    $new=$new.Replace([string][char]13+[char]10,[string][char]10)
    if(!$script:source.Contains($old)){throw "R15 anchor missing: $old"}
    $script:source=$script:source.Replace($old,$new)
}
$path="$root/WidgetHost.cs"
$source=ReadSource $path
ReplaceOnce '    private DeskSpaceSearchV11? activeSearch;' @'
    private DeskSpaceSearchV11? activeSearch;
    private DeskSpaceDropShelfV15? attachedShelf;
'@
ReplaceOnce '            DeskSpaceWeather.Updated += OnWeatherUpdated;' @'
            DeskSpaceWeather.Updated += OnWeatherUpdated;
            attachedShelf ??= new DeskSpaceDropShelfV15(this);
            attachedShelf.Enable();
'@
ReplaceOnce '        Closing += (_,_)=>activeModule?.Dispose();' '        Closing += (_,_)=>{activeModule?.Dispose();attachedShelf?.Dispose();};'
SaveSource $path $source

# Keep the circle-sized search morph but replace the dated default gray button,
# add an actual placeholder and keep the weather/date legible behind it.
$path="$root/DeskSpaceSearchV11.cs"
$source=ReadSource $path
ReplaceOnce 'var field=new TextBox { FontSize=13,BorderThickness=new Thickness(0),Background=Brushes.Transparent,' @'
        var field=new TextBox { FontSize=13,BorderThickness=new Thickness(0),Background=Brushes.Transparent,
'@
ReplaceOnce 'var close=new Button { Content="×",FontSize=16,MinWidth=23,Padding=new Thickness(0),' @'
        var close=new Button { Content="×",FontSize=16,MinWidth=23,Padding=new Thickness(0),
            Background=Brushes.Transparent,BorderThickness=new Thickness(0),
            Foreground=new SolidColorBrush(Color.FromRgb(100,81,151)),
            Cursor=Cursors.Hand,
'@
ReplaceOnce 'var frame = new Border { Background=new SolidColorBrush(Color.FromRgb(250,248,255)),' @'
        var frame = new Border { Background=new SolidColorBrush(Color.FromRgb(250,248,255)),
            Effect=new System.Windows.Media.Effects.DropShadowEffect{
                BlurRadius=9,ShadowDepth=1,Opacity=.13,
                Color=Color.FromRgb(49,31,98)
            },
'@
ReplaceOnce 'Grid.SetColumn(field,1);top.Children.Add(field);' @'
        Grid.SetColumn(field,1);top.Children.Add(field);
        var placeholder=new TextBlock {
            Text="Search",FontSize=12,IsHitTestVisible=false,
            VerticalAlignment=VerticalAlignment.Center,
            Margin=new Thickness(1,0,0,0),
            Foreground=new SolidColorBrush(Color.FromRgb(153,146,172))
        };
        Grid.SetColumn(placeholder,1);top.Children.Add(placeholder);
        field.TextChanged+=(_,_)=>placeholder.Visibility=
            field.Text.Length==0?Visibility.Visible:Visibility.Collapsed;
'@
SaveSource $path $source
Write-Host 'R15: persistent drag/drop temporary file shelf, animated night sky, parallel drive discovery, deduplicated real apps, polished small search.'
