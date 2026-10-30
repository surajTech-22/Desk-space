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
# R24's method signature has been refactored by later patches. Match
# the actual named method's opening brace, not the obsolete whitespace
# and StopMorphWindowAnimations() location.
function InsertAtMethodStart([string]$file,[string]$method,
    [string]$body)
{
 $s=[IO.File]::ReadAllText($file).Replace($crlf,$lf)
 $pattern='\bprivate\s+void\s+'+[regex]::Escape($method)+
     '\s*\([^)]*\)\s*\{'
 $matches=[regex]::Matches($s,$pattern)
 if($matches.Count -ne 1){
     $mentions=[regex]::Matches($s,'(?m)^.*'+[regex]::Escape($method)+'.*$')|
          Select-Object -First 4|ForEach-Object {$_.Value.Trim()}
     throw ("R209 "+$method+" expected one method, got "+$matches.Count+
       "; possible signatures: "+($mentions -join '; '))
 }
 $match=$matches[0]
 $insert=$lf+$body.Replace($crlf,$lf).TrimEnd()
 $s=$s.Insert($match.Index+$match.Length,$insert)
 [IO.File]::WriteAllText($file,$s,[Text.UTF8Encoding]::new($false))
}
InsertAtMethodStart "$root/WidgetHost.cs" 'AnimateModuleOpenFrom' @'
        if(!DeskSpaceReducedMotionV209.Enabled())
        {
            StopMorphWindowAnimations();
            DeskSpaceReducedMotionV209.Reset(surface);
            return;
        }
'@
InsertAtMethodStart "$root/WidgetHost.cs" 'AnimateModuleClose' @'
        if(!DeskSpaceReducedMotionV209.Enabled())
        {
            StopMorphWindowAnimations();
            if(Content is FrameworkElement immediate)
                DeskSpaceReducedMotionV209.Reset(immediate);
            return;
        }
'@
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
