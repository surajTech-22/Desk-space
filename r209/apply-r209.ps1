$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r209/DeskSpaceReducedMotionV209.cs' -Destination "$root/DeskSpaceReducedMotionV209.cs" -Force
$lf=[string][char]10;$crlf=[string][char]13+$lf
function ReplaceOne([string]$file,[string]$before,[string]$after,[string]$reason)
{
 $s=[IO.File]::ReadAllText($file).Replace($crlf,$lf)
 $before=$before.Replace($crlf,$lf);$after=$after.Replace($crlf,$lf)
 $n=([regex]::Matches($s,[regex]::Escape($before))).Count
 if($n -ne 1){throw ("R209 "+$reason+" expected one exact anchor; got "+$n)}
 [IO.File]::WriteAllText($file,$s.Replace($before,$after),
     [Text.UTF8Encoding]::new($false))
}
ReplaceOne "$root/WidgetHost.cs" @'
    private void AnimateModuleOpenFrom(FrameworkElement surface,double startW,double startH)
    {
        StopMorphWindowAnimations();
'@ @'
    private void AnimateModuleOpenFrom(FrameworkElement surface,double startW,double startH)
    {
        StopMorphWindowAnimations();
        if(!DeskSpaceReducedMotionV209.Enabled())
        {
            DeskSpaceReducedMotionV209.Reset(surface);
            return;
        }
'@ 'module open reduced motion'
ReplaceOne "$root/WidgetHost.cs" @'
    private void AnimateModuleClose(double oldWidth,double oldHeight)
    {
        StopMorphWindowAnimations();
'@ @'
    private void AnimateModuleClose(double oldWidth,double oldHeight)
    {
        StopMorphWindowAnimations();
        if(!DeskSpaceReducedMotionV209.Enabled())
        {
            if(Content is FrameworkElement immediate)
                DeskSpaceReducedMotionV209.Reset(immediate);
            return;
        }
'@ 'module close reduced motion'
ReplaceOne "$root/DeskSpaceSubDonutStandardV62.cs" @'
    internal static void Animate(Canvas canvas,int direction,
        double centerAngle)
    {
        if(direction==0)return;
        canvas.BeginAnimation(UIElement.OpacityProperty,null);
'@ @'
    internal static void Animate(Canvas canvas,int direction,
        double centerAngle)
    {
        if(direction==0)return;
        if(!DeskSpaceReducedMotionV209.Enabled())
        {
            DeskSpaceReducedMotionV209.Reset(canvas);
            return;
        }
        canvas.BeginAnimation(UIElement.OpacityProperty,null);
'@ 'nested Information/Excel subdonut reduced motion'
Write-Host 'R209: Windows motion preference applied to module morphs and shared nested donut transitions.'
