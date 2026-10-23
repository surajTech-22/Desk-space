$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r64/DeskSpaceWeatherContrastV64.cs' (Join-Path $root 'DeskSpaceWeatherContrastV64.cs') -Force
function Read([string]$path){[IO.File]::ReadAllText($path).Replace([string][char]13+[char]10,[string][char]10)}
function Save([string]$path,[string]$s){[IO.File]::WriteAllText($path,$s,[Text.UTF8Encoding]::new($false))}
function One([string]$old,[string]$new){
 $old=$old.Replace([string][char]13+[char]10,[string][char]10)
 $new=$new.Replace([string][char]13+[char]10,[string][char]10)
 $n=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($n -ne 1){throw "R64 expected once, got $n : $old"}
 $script:s=$script:s.Replace($old,$new)
}

# Information: restore hover preview, but only explicit click/Enter or
# released drag commits a collection. A temporary preview closes when leaving
# a wedge, paging, closing the arc or returning to the Information module.
$p=Join-Path $root 'DeskSpaceInformationV36.cs';$s=Read $p
One '    private bool addingRow;' @'
    private bool addingRow;
    private bool transientPreview;
    private void CloseTransientPreview()
    {
        if(!transientPreview || editorActive)return;
        transientPreview=false;
        if(shelf.IsOpen)CloseShelf();
    }
'@
One @'
        newHoverTimer.Stop();
        middleScroll.Stop();
        pointerTimer.Stop();
'@ @'
        newHoverTimer.Stop();
        middleScroll.Stop();
        pointerTimer.Stop();
        CloseTransientPreview();
'@
One @'
    private void EndArcHover()
    {
        newHoverTimer.Stop();
'@ @'
    private void EndArcHover()
    {
        newHoverTimer.Stop();
        CloseTransientPreview();
'@
One @'
        // Hover highlights collection names only. A preview appears solely
        // when the collection is explicitly activated. This prevents any
        // unselected collection shelf from surviving beneath the circle.
'@ @'
        if(editorActive)return;
        if(sector<arcHitZones.Count-1 && id.HasValue)
        {
            // Immediately preview the selected wedge, but do not collapse the
            // donut: the shelf is temporary until a deliberate selection.
            transientPreview=true;
            OpenCollection(id.Value);
        }
        else
            CloseTransientPreview();
'@
One @'
        if(id.HasValue && !editorActive)
        {
            OpenCollection(id.Value);
            CommitChildSelection();
        }
'@ @'
        if(id.HasValue && !editorActive)
        {
            transientPreview=false; // an explicit selection persists
            OpenCollection(id.Value);
            CommitChildSelection();
        }
'@
One @'
        if(sector==arcHitZones.Count-1)
        {
            newHoverTimer.Stop();
'@ @'
        if(sector==arcHitZones.Count-1)
        {
            newHoverTimer.Stop();
            CloseTransientPreview();
'@
One @'
    private void Slide(int delta)
    {
        if(!CanPage(delta)){StopEdgeScroll();return;}
'@ @'
    private void Slide(int delta)
    {
        CloseTransientPreview();
        if(!CanPage(delta)){StopEdgeScroll();return;}
'@
One @'
        if(direction!=0)
        {
            newHoverTimer.Stop();
            EndArcHover();
'@ @'
        // Actual wedges take priority over edge corridors. A user pointing
        // at item 4 must be able to scroll, not trigger edge-page polling.
        int overItem=SlotAtPoint(point);
        if(overItem>=0)
        {
            StopEdgeScroll();
            if(overItem!=hoveredChild)
                HoverChild(overItem,arcHitZones[overItem].id);
            return;
        }
        if(direction!=0)
        {
            newHoverTimer.Stop();
            EndArcHover();
'@
# Don't close a temporary shelf when committing to the compact circle.
# CloseShelf() will clean the flag for user-initiated dismissal.
One @'
    private void CloseShelf()
    {
        editorActive=false;shownCollectionId=null;
'@ @'
    private void CloseShelf()
    {
        transientPreview=false;
        editorActive=false;shownCollectionId=null;
'@
Save $p $s

# Fix the actual weather-clock text, not merely a parent color that local
# text runs can override. The state follows live/cached scene luminance,
# even when Windows location and selected city labels don't match exactly.
$p=Join-Path $root 'WidgetHost.cs';$s=Read $p
$from=$s.IndexOf('    private void ApplyWeatherContrast()', [StringComparison]::Ordinal)
$to=if($from -ge 0){$s.IndexOf('    private void OpenCalendarQuickAdd()', $from)}else{-1}
if($from -lt 0 -or $to -le $from){throw 'R64 cannot find weather-clock contrast routine'}
$replacement=@'
    private void ApplyWeatherContrast()
    {
        var weather=DeskSpaceWeather.Latest;
        // Location-based and cached city labels must not disable contrast:
        // the current scene already uses this exact WeatherReading.
        DeskSpaceWeatherContrastV64.Apply(content,weather,prefs.ShowWeather);
    }

'@
$replacement=$replacement.Replace([string][char]13+[char]10,[string][char]10)
$s=$s.Substring(0,$from)+$replacement+$s.Substring($to)
# The original BuildCompact hook remains, but UpdateCenter may recreate the
# TextBlock's local colors. Re-apply after each clock refresh as well.
$from=$s.IndexOf('    private void UpdateCenter()', [StringComparison]::Ordinal)
if($from -lt 0){throw 'R64 missing dynamic clock update routine'}
$to=$s.IndexOf([string][char]10+'    private ', $from+23,[StringComparison]::Ordinal)
if($to -lt 0){throw 'R64 cannot isolate dynamic clock update routine'}
$part=$s.Substring($from,$to-$from)
$end=$part.LastIndexOf([string][char]10+'    }',[StringComparison]::Ordinal)
if($end -lt 0){throw 'R64 missing clock method close brace'}
if(!$part.Contains('ApplyWeatherContrast();')){
  $part=$part.Insert($end,[string][char]10+'        ApplyWeatherContrast();')
  $s=$s.Substring(0,$from)+$part+$s.Substring($to)
}
Save $p $s
Write-Host 'R64: transient Information previews restored with leave/scroll cleanup, on-wedge wheel priority and clock weather foreground re-evaluated after each update.'
