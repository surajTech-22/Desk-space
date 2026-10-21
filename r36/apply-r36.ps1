$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r36/DeskSpaceWeatherSceneV36.cs' "$root/DeskSpaceWeatherSceneV11.cs" -Force
Copy-Item 'r36/DeskSpaceMediaV36.cs' "$root/DeskSpaceMediaV18.cs" -Force
Copy-Item 'r36/DeskSpaceCompactCalendarV36.cs' "$root/DeskSpaceCompactCalendarV36.cs" -Force
Copy-Item 'r36/DeskSpaceInlineV36.cs' "$root/DeskSpaceInlineV36.cs" -Force
function Read([string]$path) {
    return [IO.File]::ReadAllText($path).Replace([string][char]13+[char]10,[string][char]10)
}
function Save([string]$path,[string]$content) {
    [IO.File]::WriteAllText($path,$content,[Text.UTF8Encoding]::new($false))
}
function Must([string]$a,[string]$b) {
    if(!$script:s.Contains($a)){throw "R36 live source anchor missing: $a"}
    $script:s=$script:s.Replace($a,$b)
}
$p="$root/WidgetHost.cs"
$s=Read $p
Must 'DeskSpaceInlineV34? activeInline' 'DeskSpaceInlineV36? activeInline'
Must 'new DeskSpaceInlineV34(name,previousCenter,ReturnFromModule,quickAddRequested)' 'new DeskSpaceInlineV36(name,previousCenter,ReturnFromModule,quickAddRequested)'
Must 'new DeskSpaceCompactCalendarV34().Create(OpenCalendarExpanded,ReturnFromModule)' 'new DeskSpaceCompactCalendarV36().Create(OpenCalendarExpanded,ReturnFromModule,OpenCalendarQuickAdd)'
Must 'double size=Math.Min(OpenDiameter,Math.Min(bounds.Width,bounds.Height));' 'double size=Math.Min(342,Math.Min(bounds.Width,bounds.Height));'
Must '        AddWeatherScene(53,6,185);' '        AddWeatherScene(10,9,272);'
# Avoid adding another layer over the clock text. Changing foreground and a
# matching glow makes dynamic day/night weather readable on scene background.
$buildStart=$s.IndexOf('    private void BuildCompact()', [StringComparison]::Ordinal)
$buildEnd=if($buildStart -ge 0){$s.IndexOf('    private void ', $buildStart+26,[StringComparison]::Ordinal)}else{-1}
if($buildStart -lt 0 -or $buildEnd -le $buildStart){throw 'R36 compact clock method boundaries missing'}
$compact=$s.Substring($buildStart,$buildEnd-$buildStart)
$old='        canvas.Children.Add(content);'+[string][char]10+'        UpdateCenter();'
$new=@'
        canvas.Children.Add(content);
        UpdateCenter();
        ApplyWeatherContrast();
'@
$new=$new.Replace([string][char]13+[char]10,[string][char]10)
if(!$compact.Contains($old)){throw 'R36 compact weather overlay hook missing'}
$compact=$compact.Replace($old,$new)
$s=$s.Substring(0,$buildStart)+$compact+$s.Substring($buildEnd)
$contrast=@'
    private void ApplyWeatherContrast()
    {
        var weather=DeskSpaceWeather.Latest;
        if(!prefs.ShowWeather || weather==null ||
           !weather.City.Equals(prefs.WeatherCity,StringComparison.OrdinalIgnoreCase))
        {
            content.Effect=null;
            content.Foreground=Brushes.White;
            return;
        }
        // The weather itself determines backdrop brightness. Pick the
        // opposite ink and a matching outline for legibility at all times.
        bool bright=weather.IsDay && weather.Code<=3;
        content.Foreground=bright?
            new SolidColorBrush(Color.FromRgb(19,31,57)):
            Brushes.White;
        content.Effect=new System.Windows.Media.Effects.DropShadowEffect{
            Color=bright?Colors.White:Colors.Black,
            BlurRadius=4,ShadowDepth=0,Opacity=.90
        };
    }

    private void OpenCalendarQuickAdd()
    {
        OpenCalendarExpanded();
        Dispatcher.BeginInvoke(new Action(()=>activeModule?.StartNewItem()),
            System.Windows.Threading.DispatcherPriority.Input);
    }

'@
$contrast=$contrast.Replace([string][char]13+[char]10,[string][char]10)
$pos=$s.IndexOf('    private void BuildCompact()', [StringComparison]::Ordinal)
$s=$s.Substring(0,$pos)+$contrast+$s.Substring($pos)
# Make calculator an explicit in-app route rather than relying on the
# legacy module registry branch or ever launching Windows calc.exe.
$anchor='        else if(name=="Excel")'
$calculator=@'
        else if(name=="Calculator")
        {
            var space=ModuleArea();
            Width=Math.Min(space.Width,324);
            Height=Math.Min(space.Height,395);
            surface=new Border{
                Background=new SolidColorBrush(Color.FromRgb(248,249,253)),
                CornerRadius=new CornerRadius(19),
                ClipToBounds=true,
                Child=new DeskSpaceCalculatorV22().Create()
            };
        }
'@
$calculator=$calculator.Replace([string][char]13+[char]10,[string][char]10)
if(!$s.Contains($anchor)){throw 'R36 calculator inline insertion anchor missing'}
$s=$s.Replace($anchor,$calculator+[string][char]10+$anchor)
$old=@'
            Height=Math.Min(limits.Height,Math.Clamp(limits.Height*
                (name=="Notes"?.40:name=="Reminders"?.41:.43),250,390));
'@
$new=@'
            // Compact modules should not reserve 390px when the item list
            // is empty; inner ScrollViewer still grows on demand.
            Height=Math.Min(limits.Height,
                name=="Notes"?256:name=="Reminders"?278:291);
'@
$old=$old.Replace([string][char]13+[char]10,[string][char]10)
$new=$new.Replace([string][char]13+[char]10,[string][char]10)
if(!$s.Contains($old)){throw 'R36 live inline panel sizing changed'}
$s=$s.Replace($old,$new)
Save $p $s
Write-Host 'R36: full 272px weather inside circle + responsive text contrast; true compact calendar and + event; inbuilt calculator; ← Circle reminder and reduced empty list; usable media pill.'
