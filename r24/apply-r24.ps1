$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r24/DeskSpaceCompactCalendarV24.cs' "$root/DeskSpaceCompactCalendarV24.cs" -Force
Copy-Item 'r24/DeskSpaceInformationV24.cs' "$root/DeskSpaceInformationV24.cs" -Force
function Read([string]$p) {
    [IO.File]::ReadAllText($p).Replace([string][char]13+[char]10,[string][char]10)
}
function Save([string]$p,[string]$s) {
    [IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
}
function RequiredReplace([string]$old,[string]$new) {
    if(!$script:s.Contains($old)) {throw "R24 source anchor missing: $old"}
    $script:s=$script:s.Replace($old,$new)
}

# Reuse V22 JSON and AES-GCM vault unchanged. Remove title-based password
# detection; only the selected storage type chooses the vault.
$p="$root/DeskSpaceModuleV10.cs"
$s=Read $p
RequiredReplace 'new DeskSpaceInformationV22().Create()' 'new DeskSpaceInformationV24().Create()'
RequiredReplace '    private readonly Action done;' @'
    private readonly Action done;
    public Action? CalendarCompactRequested {get;set;}
'@
RequiredReplace '        var row=Row();body.Children.Add(row);' '        var row=Row();body.Children.Add(row);'
$start=$s.IndexOf('    private void BuildInformation(StackPanel body)',[StringComparison]::Ordinal)
$end=if($start -ge 0){$s.IndexOf('    private void BuildPassword(StackPanel body)', $start,[StringComparison]::Ordinal)}else{-1}
if($start -lt 0 -or $end -le $start){throw 'R24 information manager method boundary missing'}
$information=@'
    private void BuildInformation(StackPanel body)
    {
        body.Children.Add(Label("Information Manager",21));
        var types=new WrapPanel{Margin=new Thickness(0,4,0,8)};
        body.Children.Add(types);
        var details=new StackPanel();
        body.Children.Add(details);
        var ordinary=Btn("Information",()=>Switch(false));
        var secure=Btn("Password · Security Vault",()=>Switch(true));
        types.Children.Add(ordinary);types.Children.Add(secure);
        bool? current=null;
        void Switch(bool password)
        {
            if(current==password)return;
            current=password;
            vaultTimeout?.Stop();vaultTimeout=null;
            vault?.Dispose();vault=null;
            details.Children.Clear();
            ordinary.Background=password?Brushes.White:Purple;
            ordinary.Foreground=password?Ink:Brushes.White;
            secure.Background=password?Purple:Brushes.White;
            secure.Foreground=password?Brushes.White:Ink;
            if(password)
            {
                details.Children.Add(Label("Encrypted Security Vault",16));
                BuildPassword(details);
            }
            else
            {
                details.Children.Add(new DeskSpaceInformationV24().Create());
            }
        }
        Switch(false);
    }

'@
$s=$s.Substring(0,$start)+$information+$s.Substring($end)
$anchor='        var row=Row();body.Children.Add(row);'
$calendar=$s.IndexOf('    private void BuildCalendar(StackPanel body)',[StringComparison]::Ordinal)
$after=if($calendar -ge 0){$s.IndexOf($anchor,$calendar,[StringComparison]::Ordinal)}else{-1}
if($after -lt 0){throw 'R24 Calendar toolbar missing'}
$replace=$anchor+[string][char]10+'        if(CalendarCompactRequested!=null)row.Children.Add(Btn("◉ Compact",CalendarCompactRequested));'
$s=$s.Substring(0,$after)+$replace+$s.Substring($after+$anchor.Length)
Save $p $s

$p="$root/WidgetHost.cs"
$s=Read $p
# The host remains the SAME window. Calendar has its own true circular layout.
$anchor='        activeModule=new DeskSpaceModuleV10(prefs,previousCenter,ReturnFromModule);'
RequiredReplace $anchor ($anchor+[string][char]10+
    '        activeModule.CalendarCompactRequested=OpenCalendarCompact;')
$calendarBranch=@'
        else if(name=="Calendar")
        {
            var bounds=ModuleArea();
            double size=Math.Min(OpenDiameter,Math.Min(bounds.Width,bounds.Height));
            Width=size;Height=size;
            surface=new Viewbox{Stretch=Stretch.Uniform,
                Child=new DeskSpaceCompactCalendarV24().Create(OpenCalendarExpanded,ReturnFromModule)};
        }
'@
$excel=$s.IndexOf('        else if(name=="Excel")',[StringComparison]::Ordinal)
if($excel -lt 0){throw 'R24 Excel branch insertion point missing'}
$s=$s.Insert($excel,$calendarBranch+[string][char]10)

$start=$s.IndexOf('    private void StopMorphWindowAnimations()', [StringComparison]::Ordinal)
$end=if($start -ge 0){$s.IndexOf('    private void ResizeLiveSearch(double height)', $start,[StringComparison]::Ordinal)}else{-1}
if($start -lt 0 -or $end -le $start){throw 'R24 R23 animation methods not found'}
$morph=@'
    // One transition controller owns the animation clocks. Previous clocks
    // are cleared before a different module/window state is shown.
    private void StopMorphWindowAnimations()
    {
        BeginAnimation(Window.WidthProperty,null);
        BeginAnimation(Window.HeightProperty,null);
    }
    private void AnimateModuleOpen(FrameworkElement surface)
    {
        AnimateModuleOpenFrom(surface,CompactDiameter,CompactDiameter);
    }
    private void AnimateModuleOpenFrom(FrameworkElement surface,double startW,double startH)
    {
        StopMorphWindowAnimations();
        double targetW=Width,targetH=Height;
        var easing=new System.Windows.Media.Animation.CubicEase{
            EasingMode=System.Windows.Media.Animation.EasingMode.EaseOut};
        var duration=new Duration(TimeSpan.FromMilliseconds(340));
        BeginAnimation(Window.WidthProperty,
            new System.Windows.Media.Animation.DoubleAnimation(startW,targetW,duration){
                FillBehavior=System.Windows.Media.Animation.FillBehavior.Stop,EasingFunction=easing});
        BeginAnimation(Window.HeightProperty,
            new System.Windows.Media.Animation.DoubleAnimation(startH,targetH,duration){
                FillBehavior=System.Windows.Media.Animation.FillBehavior.Stop,EasingFunction=easing});
        // A small entry scale plus delayed opacity removes the harsh
        // content-swap flash as the window expands from the centre circle.
        surface.RenderTransformOrigin=new Point(.5,.5);
        var transform=new ScaleTransform(1,1);
        surface.RenderTransform=transform;
        transform.BeginAnimation(ScaleTransform.ScaleXProperty,
            new System.Windows.Media.Animation.DoubleAnimation(.96,1,duration){
                FillBehavior=System.Windows.Media.Animation.FillBehavior.Stop,EasingFunction=easing});
        transform.BeginAnimation(ScaleTransform.ScaleYProperty,
            new System.Windows.Media.Animation.DoubleAnimation(.96,1,duration){
                FillBehavior=System.Windows.Media.Animation.FillBehavior.Stop,EasingFunction=easing});
        var fade=new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames{
            Duration=duration,FillBehavior=System.Windows.Media.Animation.FillBehavior.Stop};
        fade.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(
            0,System.Windows.Media.Animation.KeyTime.FromPercent(0)));
        fade.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(
            0,System.Windows.Media.Animation.KeyTime.FromPercent(.17)));
        fade.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(
            1,System.Windows.Media.Animation.KeyTime.FromPercent(1)){EasingFunction=easing});
        surface.BeginAnimation(UIElement.OpacityProperty,fade);
    }
    private void AnimateModuleClose(double oldWidth,double oldHeight)
    {
        StopMorphWindowAnimations();
        var duration=new Duration(TimeSpan.FromMilliseconds(285));
        var easing=new System.Windows.Media.Animation.CubicEase{
            EasingMode=System.Windows.Media.Animation.EasingMode.EaseInOut};
        BeginAnimation(Window.WidthProperty,
            new System.Windows.Media.Animation.DoubleAnimation(oldWidth,CompactDiameter,duration){
                FillBehavior=System.Windows.Media.Animation.FillBehavior.Stop,EasingFunction=easing});
        BeginAnimation(Window.HeightProperty,
            new System.Windows.Media.Animation.DoubleAnimation(oldHeight,CompactDiameter,duration){
                FillBehavior=System.Windows.Media.Animation.FillBehavior.Stop,EasingFunction=easing});
        if(Content is FrameworkElement circle)
        {
            // Keep the clock invisible while the outgoing large panel shrinks,
            // then reveal it only when almost back at actual compact size.
            circle.RenderTransformOrigin=new Point(.5,.5);
            var t=new ScaleTransform(1,1);
            circle.RenderTransform=t;
            t.BeginAnimation(ScaleTransform.ScaleXProperty,
                new System.Windows.Media.Animation.DoubleAnimation(.96,1,duration){
                    FillBehavior=System.Windows.Media.Animation.FillBehavior.Stop});
            t.BeginAnimation(ScaleTransform.ScaleYProperty,
                new System.Windows.Media.Animation.DoubleAnimation(.96,1,duration){
                    FillBehavior=System.Windows.Media.Animation.FillBehavior.Stop});
            var fade=new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames{
                Duration=duration,FillBehavior=System.Windows.Media.Animation.FillBehavior.Stop};
            fade.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(
                0,System.Windows.Media.Animation.KeyTime.FromPercent(0)));
            fade.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(
                0,System.Windows.Media.Animation.KeyTime.FromPercent(.69)));
            fade.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(
                1,System.Windows.Media.Animation.KeyTime.FromPercent(1)){EasingFunction=easing});
            circle.BeginAnimation(UIElement.OpacityProperty,fade);
        }
    }
    private void OpenCalendarExpanded()
    {
        if(activeModule==null)return;
        double previousWidth=Width,previousHeight=Height;
        StopMorphWindowAnimations();
        var available=ModuleArea();
        Width=Math.Min(available.Width,602);
        Height=Math.Min(available.Height,686);
        KeepOnVisibleDesktop();
        var surface=activeModule.Build("Calendar");
        Content=surface;
        AnimateModuleOpenFrom(surface,previousWidth,previousHeight);
    }
    private void OpenCalendarCompact()
    {
        if(activeModule==null)return;
        double previousWidth=Width,previousHeight=Height;
        StopMorphWindowAnimations();
        var available=ModuleArea();
        double diameter=Math.Min(OpenDiameter,Math.Min(available.Width,available.Height));
        Width=diameter;Height=diameter;
        KeepOnVisibleDesktop();
        FrameworkElement surface=new Viewbox{Stretch=Stretch.Uniform,
            Child=new DeskSpaceCompactCalendarV24().Create(OpenCalendarExpanded,ReturnFromModule)};
        Content=surface;
        AnimateModuleOpenFrom(surface,previousWidth,previousHeight);
    }

'@
$s=$s.Substring(0,$start)+$morph+$s.Substring($end)
Save $p $s
Write-Host 'R24: in-circle Calendar with full planner, improved window morph, modern information editor, unchanged vault and existing event data.'
