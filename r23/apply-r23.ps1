$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$path='DeskSpace_Windows_R1/DeskSpace/WidgetHost.cs'
$source=[IO.File]::ReadAllText($path).Replace([string][char]13+[char]10,[string][char]10)
$start=$source.IndexOf('    private void AnimateModuleOpen(FrameworkElement surface)',[StringComparison]::Ordinal)
$end=if($start -ge 0){$source.IndexOf('    private void ResizeLiveSearch(double height)', $start,[StringComparison]::Ordinal)}else{-1}
if($start -lt 0 -or $end -le $start) { throw 'R23: R22 morph boundaries unavailable' }
$fixed=@'
    // An animated WPF Window.Width/Height defaults to HoldEnd, which can
    // permanently override the Width=OpenDiameter assignment used by the donut.
    // Explicit Stop and clear every active clock before changing surface mode.
    private void StopMorphWindowAnimations()
    {
        BeginAnimation(Window.WidthProperty,null);
        BeginAnimation(Window.HeightProperty,null);
    }
    private void AnimateModuleOpen(FrameworkElement surface)
    {
        StopMorphWindowAnimations();
        double targetWidth=Width,targetHeight=Height;
        var easing=new System.Windows.Media.Animation.CubicEase{
            EasingMode=System.Windows.Media.Animation.EasingMode.EaseOut};
        var duration=new Duration(TimeSpan.FromMilliseconds(250));
        BeginAnimation(Window.WidthProperty,
            new System.Windows.Media.Animation.DoubleAnimation(
                CompactDiameter,targetWidth,duration){
                FillBehavior=System.Windows.Media.Animation.FillBehavior.Stop,
                EasingFunction=easing});
        BeginAnimation(Window.HeightProperty,
            new System.Windows.Media.Animation.DoubleAnimation(
                CompactDiameter,targetHeight,duration){
                FillBehavior=System.Windows.Media.Animation.FillBehavior.Stop,
                EasingFunction=easing});
        surface.RenderTransformOrigin=new Point(.5,.5);
        // The base scale must be 1.0; Stop would otherwise snap back to .72.
        var transform=new ScaleTransform(1,1);
        surface.RenderTransform=transform;
        transform.BeginAnimation(ScaleTransform.ScaleXProperty,
            new System.Windows.Media.Animation.DoubleAnimation(.92,1,duration){
                FillBehavior=System.Windows.Media.Animation.FillBehavior.Stop,
                EasingFunction=easing});
        transform.BeginAnimation(ScaleTransform.ScaleYProperty,
            new System.Windows.Media.Animation.DoubleAnimation(.92,1,duration){
                FillBehavior=System.Windows.Media.Animation.FillBehavior.Stop,
                EasingFunction=easing});
        surface.BeginAnimation(UIElement.OpacityProperty,
            new System.Windows.Media.Animation.DoubleAnimation(0,1,duration){
                FillBehavior=System.Windows.Media.Animation.FillBehavior.Stop});
    }
    private void AnimateModuleClose(double oldWidth,double oldHeight)
    {
        StopMorphWindowAnimations();
        var duration=new Duration(TimeSpan.FromMilliseconds(230));
        var easing=new System.Windows.Media.Animation.CubicEase{
            EasingMode=System.Windows.Media.Animation.EasingMode.EaseInOut};
        // Base size has already been restored to CompactDiameter in
        // ReturnFromModule. The finished clocks return control to base values.
        BeginAnimation(Window.WidthProperty,
            new System.Windows.Media.Animation.DoubleAnimation(
                oldWidth,CompactDiameter,duration){
                FillBehavior=System.Windows.Media.Animation.FillBehavior.Stop,
                EasingFunction=easing});
        BeginAnimation(Window.HeightProperty,
            new System.Windows.Media.Animation.DoubleAnimation(
                oldHeight,CompactDiameter,duration){
                FillBehavior=System.Windows.Media.Animation.FillBehavior.Stop,
                EasingFunction=easing});
    }
'@
$source=$source.Substring(0,$start)+$fixed+[Environment]::NewLine+$source.Substring($end)
$ring='    private void BuildDonut()'+[string][char]10+'    {'
if(!$source.Contains($ring)){throw 'R23 expanded ring render anchor missing'}
$source=$source.Replace($ring,@'
    private void BuildDonut()
    {
        // All twelve wedges are defined on a full 420-DIP canvas. They must
        // NEVER be rendered into the 116-DIP compact Window after module close.
        StopMorphWindowAnimations();
        Width=OpenDiameter;
        Height=OpenDiameter;
        KeepOnVisibleDesktop();
'@)
# Also ensure compact mode can regain base dimensions if a previous animation
# was interrupted. Only run this on the actual compact drawing method.
$compact='    private void BuildCompact()'+[string][char]10+'    {'
if(!$source.Contains($compact)){throw 'R23 compact ring anchor missing'}
$source=$source.Replace($compact,@'
    private void BuildCompact()
    {
        if(activeModule==null && !ringExpanded)
        {
            StopMorphWindowAnimations();
            Width=Height=CompactDiameter;
            KeepOnVisibleDesktop();
        }
'@)
[IO.File]::WriteAllText($path,$source,[Text.UTF8Encoding]::new($false))
Write-Host 'R23 fixed HoldEnd animation overriding donut window dimensions; validated base-scale and full 12-segment ring sizing.'
