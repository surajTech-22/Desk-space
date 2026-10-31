$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r208/DeskSpaceHighContrastV208.cs' -Destination "$root/DeskSpaceHighContrastV208.cs" -Force
$p="$root/WidgetHost.cs"
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$old,[string]$new,[string]$reason)
{
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($n -ne 1){throw ("R208 "+$reason+" anchor expected 1, found "+$n)}
 $script:s=$script:s.Replace($old,$new)
}
Swap @'
            ringPaths[i].Fill=i==selected?accent:
                (i%2==0?light:DeskSpaceAppearanceV148.SecondaryBrush);
'@ @'
            ringPaths[i].Fill=DeskSpaceHighContrastV208.Wedge(
                SystemParameters.HighContrast,i,i==selected,
                light,DeskSpaceAppearanceV148.SecondaryBrush,accent);
'@ 'mouse hover contrast'
Swap @'
            ringPaths[i].Fill=i==highlightedSector?accent:
                (i%2==0?light:DeskSpaceAppearanceV148.SecondaryBrush);
'@ @'
            ringPaths[i].Fill=DeskSpaceHighContrastV208.Wedge(
                SystemParameters.HighContrast,i,i==highlightedSector,
                light,DeskSpaceAppearanceV148.SecondaryBrush,accent);
'@ 'keyboard selected contrast'
Swap @'
                Fill=i%2==0?light:DeskSpaceAppearanceV148.SecondaryBrush,
                Stroke=Brushes.White,StrokeThickness=2,IsHitTestVisible=false
'@ @'
                Fill=DeskSpaceHighContrastV208.Wedge(
                    SystemParameters.HighContrast,i,false,
                    light,DeskSpaceAppearanceV148.SecondaryBrush,accent),
                Stroke=DeskSpaceHighContrastV208.Outline(SystemParameters.HighContrast),
                StrokeThickness=2,IsHitTestVisible=false
'@ 'root ring segments'
Swap @'
                Foreground=DeskSpaceAppearanceV148.InkBrush,IsHitTestVisible=false
'@ @'
                Foreground=DeskSpaceHighContrastV208.Label(
                    SystemParameters.HighContrast,DeskSpaceAppearanceV148.InkBrush),
                IsHitTestVisible=false
'@ 'root sector labels'
Swap @'
                    Width=20,Height=20,Fill=accent,
                    Stroke=Brushes.White,StrokeThickness=1.4,
'@ @'
                    Width=20,Height=20,
                    Fill=DeskSpaceHighContrastV208.QuickAction(
                        SystemParameters.HighContrast,accent),
                    Stroke=DeskSpaceHighContrastV208.Outline(SystemParameters.HighContrast),
                    StrokeThickness=1.4,
'@ 'quick action badges'
Swap @'
                    Foreground=Brushes.White,TextAlignment=TextAlignment.Center,
                    Width=20,Height=20,IsHitTestVisible=false
'@ @'
                    Foreground=DeskSpaceHighContrastV208.QuickActionLabel(
                        SystemParameters.HighContrast),
                    TextAlignment=TextAlignment.Center,
                    Width=20,Height=20,IsHitTestVisible=false
'@ 'quick action text'
Swap @'
        var centre=new Ellipse{Width=130,Height=130,Fill=ClockFace(),Stroke=Brushes.White,StrokeThickness=3,IsHitTestVisible=false};
'@ @'
        var centre=new Ellipse{Width=130,Height=130,Fill=ClockFace(),
            Stroke=DeskSpaceHighContrastV208.Outline(SystemParameters.HighContrast),
            StrokeThickness=3,IsHitTestVisible=false};
'@ 'clock centre circle outline'
Swap @'
        var circle = new Ellipse { Width = 272, Height = 272, Fill = ClockFace(),
            Stroke = Brushes.White, StrokeThickness = 7,
'@ @'
        var circle = new Ellipse { Width = 272, Height = 272, Fill = ClockFace(),
            Stroke = DeskSpaceHighContrastV208.Outline(SystemParameters.HighContrast),
            StrokeThickness = 7,
'@ 'compact circle outline'
Swap @'
    private Brush ClockFace()
    {
'@ @'
    private Brush ClockFace()
    {
        if(SystemParameters.HighContrast)return SystemColors.WindowBrush;
'@ 'high contrast centre fill'
Swap @'
    private void ApplyWeatherContrast()
    {
'@ @'
    private void ApplyWeatherContrast()
    {
        if(SystemParameters.HighContrast)
        {
            content.Foreground=SystemColors.WindowTextBrush;
            return;
        }
'@ 'prevent weather from overriding high contrast text'
# Both root and compact draw paths independently; update centre
# foreground at the start of each root drawing function.
foreach($signature in @('    private void BuildCompact()','    private void BuildDonut()'))
{
 $new=$signature+$lf+'    {'+$lf+
  '        content.Foreground=SystemParameters.HighContrast?'+
  'SystemColors.WindowTextBrush:Brushes.White;'
 $old=$signature+$lf+'    {'
 Swap $old $new "centre text contrast for $signature"
}
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R208: native Windows high-contrast colors for root and compact donut; unchanged normal theme.'
