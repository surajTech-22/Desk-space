$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='DeskSpace_Windows_R1/DeskSpace/WidgetHost.cs'
$s=[IO.File]::ReadAllText($p).Replace([string][char]13+[char]10,[string][char]10)

# This fixes the reverse-morph race: R24 replaced the expanded content with
# the clock BEFORE the window shrink animation began, causing a visible jump
# and then a blank white panel. The actual outgoing surface must remain alive
# until the last animation frame; only then do we restore the compact circle.
$start=$s.IndexOf('    private void ReturnFromModule()', [StringComparison]::Ordinal)
$end=if($start -ge 0){$s.IndexOf('    private bool CanDragFrom(', $start,[StringComparison]::Ordinal)}else{-1}
if($start -lt 0 -or $end -le $start){throw 'R25 cannot find complete module return method'}
$original=$s.Substring($start,$end-$start)
foreach($must in @('previousSurface','activeExcel','activeSearch','activeInline','ResumeMediaMode','restoreLeft','BuildCompact()')){
    if(!$original.Contains($must)){throw "R25 loss-of-feature guard: $must missing from return method"}
}
$replacement=@'
    private bool closingMorph;

    // The outgoing module remains visible DURING shrink. This avoids the R24
    // flash where the clock and full module abruptly exchanged content.
    private void ReturnFromModule()
    {
        if(activeModule==null||closingMorph)return;
        closingMorph=true;
        StopMorphWindowAnimations();
        double fromWidth=Width,fromHeight=Height;
        double fromLeft=Left,fromTop=Top;
        double toWidth=CompactDiameter,toHeight=CompactDiameter;
        double toLeft=restoreLeft,toTop=restoreTop;
        // Establish base dimensions without painting them: active animation
        // clocks immediately supply the from-values for the first frame.
        Width=toWidth;Height=toHeight;
        Left=toLeft;Top=toTop;
        var duration=new Duration(TimeSpan.FromMilliseconds(265));
        var easing=new System.Windows.Media.Animation.CubicEase{
            EasingMode=System.Windows.Media.Animation.EasingMode.EaseInOut};
        if(Content is FrameworkElement leaving)
        {
            var fade=new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames{
                Duration=duration,
                FillBehavior=System.Windows.Media.Animation.FillBehavior.Stop};
            fade.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(
                1,System.Windows.Media.Animation.KeyTime.FromPercent(0)));
            fade.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(
                1,System.Windows.Media.Animation.KeyTime.FromPercent(.28)));
            fade.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(
                0,System.Windows.Media.Animation.KeyTime.FromPercent(.90)){EasingFunction=easing});
            leaving.BeginAnimation(UIElement.OpacityProperty,fade);
        }
        var width=new System.Windows.Media.Animation.DoubleAnimation(
            fromWidth,toWidth,duration){
            FillBehavior=System.Windows.Media.Animation.FillBehavior.Stop,
            EasingFunction=easing};
        // One completion owner, so rapid clicking cannot dispose a surface
        // twice or resurrect a previous module after another view is opened.
        width.Completed+=(_,_)=>CompleteModuleReturn();
        BeginAnimation(Window.WidthProperty,width);
        BeginAnimation(Window.HeightProperty,
            new System.Windows.Media.Animation.DoubleAnimation(fromHeight,toHeight,duration){
                FillBehavior=System.Windows.Media.Animation.FillBehavior.Stop,EasingFunction=easing});
        BeginAnimation(Window.LeftProperty,
            new System.Windows.Media.Animation.DoubleAnimation(fromLeft,toLeft,duration){
                FillBehavior=System.Windows.Media.Animation.FillBehavior.Stop,EasingFunction=easing});
        BeginAnimation(Window.TopProperty,
            new System.Windows.Media.Animation.DoubleAnimation(fromTop,toTop,duration){
                FillBehavior=System.Windows.Media.Animation.FillBehavior.Stop,EasingFunction=easing});
    }
    private void CompleteModuleReturn()
    {
        if(!closingMorph)return;
        closingMorph=false;
        StopMorphWindowAnimations();
        var old=activeModule;activeModule=null;
        var search=activeSearch;activeSearch=null;
        var inline=activeInline;activeInline=null;
        // Restore the original circle BEFORE disposing the outgoing view.
        var restore=previousSurface ??
            (canvas.Parent as Viewbox ?? new Viewbox{Child=canvas,Stretch=Stretch.Uniform});
        Content=restore;previousSurface=null;
        search?.Dispose();inline?.Dispose();
        activeExcel?.Dispose();activeExcel=null;
        old?.Dispose();
        DeskSpaceWindowBehavior.AllowInput(this,false);
        ShowActivated=false;
        Width=Height=CompactDiameter;
        Left=restoreLeft;Top=restoreTop;
        KeepOnVisibleDesktop();
        BuildCompact();SavePosition();UpdateCenter();
        Dispatcher.BeginInvoke(new Action(ResumeMediaMode),
            System.Windows.Threading.DispatcherPriority.Background);
    }

'@
$s=$s.Substring(0,$start)+$replacement+$s.Substring($end)
# If a module is requested via tray or pipe during close, finish old close
# immediately rather than trying to open into a still-animating surface.
$open='    private void OpenInWidget(string name)'+[string][char]10+'    {'
if(!$s.Contains($open)){throw 'R25 open module guard anchor absent'}
$s=$s.Replace($open,$open+[string][char]10+'        if(closingMorph)CompleteModuleReturn();')
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R25 reverse morph now keeps outgoing module surface until animated completion and preserves all prior module resources.'
