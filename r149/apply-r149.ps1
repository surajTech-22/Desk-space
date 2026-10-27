$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$widget="$root/WidgetHost.cs"
Copy-Item 'r149/DeskSpaceThemeV149.cs' "$root/DeskSpaceThemeV149.cs" -Force
Copy-Item 'r149/DeskSpaceThemeEditorV149.cs' "$root/DeskSpaceThemeEditorV149.cs" -Force
$lf=[string][char]10
$crlf=[string][char]13+$lf
function Swap([string]$file,[string]$before,[string]$after,[string]$label)
{
    $source=[IO.File]::ReadAllText($file).Replace($crlf,$lf)
    $old=$before.Replace($crlf,$lf)
    $new=$after.Replace($crlf,$lf)
    $n=([regex]::Matches($source,[regex]::Escape($old))).Count
    if($n -ne 1){throw "R149 $label: expected one anchor, found $n"}
    [IO.File]::WriteAllText($file,$source.Replace($old,$new),
        [Text.UTF8Encoding]::new($false))
}
Swap $widget @'
    private readonly Brush light = new SolidColorBrush(Color.FromRgb(238, 235, 251));
    private readonly Brush accent = new SolidColorBrush(Color.FromRgb(115, 100, 211));
'@ @'
    // Theme state is independent of widget-v3.json and the Excel library.
    private DeskSpaceThemeV149 themeV149=DeskSpaceThemeV149.Load();
    private Brush light = new SolidColorBrush(Color.FromRgb(238, 235, 251));
    private Brush accent = new SolidColorBrush(Color.FromRgb(115, 100, 211));
    private void RefreshThemeV149()
    {
        themeV149=DeskSpaceThemeV149.Load();
        light=themeV149.LightBrush;
        accent=themeV149.AccentBrush;
    }
    private void RedrawThemeV149()
    {
        if(activeModule!=null)return;
        if(ringExpanded)BuildDonut();
        else BuildCompact();
    }
'@ 'persisted theme and redraw hooks'
Swap $widget @'
    private void BuildCompact()
    {
        appsWheel?.HideArc();
'@ @'
    private void BuildCompact()
    {
        RefreshThemeV149();
        appsWheel?.HideArc();
'@ 'compact theme loaded before rendering'
Swap $widget @'
    private void BuildDonut()
    {
'@ @'
    private void BuildDonut()
    {
        RefreshThemeV149();
'@ 'expanded donut theme loaded before rendering'
Swap $widget @'
        var shadow = new Ellipse { Width = 282, Height = 282,
            Fill = new SolidColorBrush(Color.FromArgb(70, 25, 19, 58)) };
'@ @'
        var shadow = new Ellipse { Width = 282, Height = 282,
            Fill = new SolidColorBrush(Color.FromArgb(themeV149.ShadowAlpha, 25, 19, 58)) };
'@ 'compact softness shadow'
Swap $widget @'
        var circle = new Ellipse { Width = 272, Height = 272, Fill = ClockFace(),
            Stroke = Brushes.White, StrokeThickness = 7,
'@ @'
        var circle = new Ellipse { Width = 272, Height = 272, Fill = ClockFace(),
            Opacity=themeV149.Opacity,
            Stroke = Brushes.White, StrokeThickness = 7,
'@ 'compact-circle opacity'
Swap $widget @'
        var shadow=new Ellipse{Width=408,Height=408,Fill=new SolidColorBrush(Color.FromArgb(36,25,20,60)),IsHitTestVisible=false};
'@ @'
        var shadow=new Ellipse{Width=408,Height=408,Fill=new SolidColorBrush(Color.FromArgb(themeV149.ShadowAlpha,25,20,60)),IsHitTestVisible=false};
'@ 'expanded shadow softness'
Swap $widget @'
                Fill=i%2==0?light:new SolidColorBrush(Color.FromRgb(227,221,246)),
                Stroke=Brushes.White,StrokeThickness=2,IsHitTestVisible=false
'@ @'
                Fill=i%2==0?light:themeV149.AltBrush,
                Opacity=themeV149.Opacity,
                Stroke=Brushes.White,StrokeThickness=2,IsHitTestVisible=false
'@ 'ring opacity and contrasting sector colours'
Swap $widget @'
Foreground=new SolidColorBrush(Color.FromRgb(63,52,99)),IsHitTestVisible=false
'@ @'
Foreground=themeV149.TextBrush,IsHitTestVisible=false
'@ 'donut label legibility for dark/light themes'
Swap $widget @'
            ringPaths[i].Fill=i==selected?accent:
                (i%2==0?light:new SolidColorBrush(Color.FromRgb(227,221,246)));
'@ @'
            ringPaths[i].Fill=i==selected?accent:
                (i%2==0?light:themeV149.AltBrush);
'@ 'highlight restore in selected light/dark palette'
Swap $widget @'
        menu.Items.Add("Universal command search · Ctrl+Alt+Space",
'@ @'
        menu.Items.Add("Theme & donut appearance...",null,(_,_)=>Dispatcher.Invoke(()=>{
            new DeskSpaceThemeEditorV149(this,RedrawThemeV149).ShowDialog();
        }));
        menu.Items.Add("Universal command search · Ctrl+Alt+Space",
'@ 'add real theme editor in existing circle settings menu'
Write-Host 'R149 installed functional light/dark/system donut palette, accent, opacity, softness, atomic save and live preview. Preserved module order and Excel routes.'
