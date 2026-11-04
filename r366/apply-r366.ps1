$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
# Patch only the verified shipping source tree. Never touch installed data.
$root='DeskSpace_Windows_R1/DeskSpace'
$hostPath=Join-Path $root 'WidgetHost.cs'
$clockPath=Join-Path $root 'DeskSpaceWeatherContrastV64.cs'
if(!(Test-Path -LiteralPath $hostPath) -or
   !(Test-Path -LiteralPath $clockPath)){
    throw 'R366 shipping widget host / clock style source missing'
}
$crlf=[string][char]13+[string][char]10
$lf=[string][char]10
$host=[IO.File]::ReadAllText($hostPath).Replace($crlf,$lf)
$old=@'
        content.Foreground=hasSky && sky!.IsDay && sky.Code<=1
            ? new SolidColorBrush(Color.FromRgb(24,31,60)) : Brushes.White;
        content.Effect=hasSky
            ? new System.Windows.Media.Effects.DropShadowEffect{
                Color=sky!.IsDay && sky.Code<=1
                    ? Color.FromArgb(160,255,250,233):Color.FromArgb(200,10,17,40),
                BlurRadius=4,ShadowDepth=0,Opacity=.85}
            : null;

        var progress=DeskSpaceUpdateProgress.Snapshot();
        if (progress.Active)
        {
'@
$new=@'
        var progress=DeskSpaceUpdateProgress.Snapshot();
        if (progress.Active)
        {
            // R366: normal clock styling is applied below by
            // ApplyWeatherContrast, so skip its redundant temporary effect.
            // Preserve exact temporary progress-display visual treatment.
            content.Foreground=hasSky && sky!.IsDay && sky.Code<=1
                ? new SolidColorBrush(Color.FromRgb(24,31,60)) : Brushes.White;
            content.Effect=hasSky
                ? new System.Windows.Media.Effects.DropShadowEffect{
                    Color=sky!.IsDay && sky.Code<=1
                        ? Color.FromArgb(160,255,250,233):Color.FromArgb(200,10,17,40),
                    BlurRadius=4,ShadowDepth=0,Opacity=.85}
                : null;
'@
$old=$old.Replace($crlf,$lf)
$new=$new.Replace($crlf,$lf)
$hits=([regex]::Matches($host,[regex]::Escape($old))).Count
if($hits -ne 1){throw "R366 idle widget effect expected exactly one anchor; found $hits"}
$host=$host.Replace($old,$new)
$clockSource=[IO.File]::ReadAllText('r64/DeskSpaceWeatherContrastV64.cs')
foreach($guard in @('FrozenInk(','FrozenOutline(','ReferenceEquals(text.Foreground,ink)')){
    if(!$clockSource.Contains($guard)){throw "R366 clock resource cache missing: $guard"}
}
[IO.File]::WriteAllText($hostPath,$host,[Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText($clockPath,$clockSource,[Text.UTF8Encoding]::new($false))
Write-Host 'R366 shipping host clock styling caches visuals and preserves update progress.'
