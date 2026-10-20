$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$path="$root/WidgetHost.cs"
Copy-Item -LiteralPath 'r12/DeskSpaceWeatherSceneV12.cs' -Destination "$root/DeskSpaceWeatherSceneV11.cs" -Force
$text=[IO.File]::ReadAllText($path).Replace([string][char]13+[char]10,[string][char]10)
function ReplaceOnce([string]$old,[string]$new)
{
    $old=$old.Replace([string][char]13+[char]10,[string][char]10)
    $new=$new.Replace([string][char]13+[char]10,[string][char]10)
    if(!$script:text.Contains($old)){throw "R12 widget anchor missing: $old"}
    $script:text=$script:text.Replace($old,$new)
}
function ReplaceMethod([string]$from,[string]$until,[string]$body)
{
    $a=$script:text.IndexOf($from,[StringComparison]::Ordinal)
    $b=if($a -ge 0){$script:text.IndexOf($until,$a,[StringComparison]::Ordinal)}else{-1}
    if($a -lt 0 -or $b -le $a){throw "R12 widget method boundaries missing $from / $until"}
    $script:text=$script:text.Substring(0,$a)+$body+[Environment]::NewLine+$script:text.Substring($b)
}

# Right-click expands on DOWN instead of UP, so it still works when Windows
# tooltip or mouse-capture interactions would consume the corresponding release.
# The old Up handler must be removed: otherwise it immediately closes the ring.
$oldRightStart='        PreviewMouseRightButtonUp += (_, e) =>'
$oldRightEnd='        PreviewMouseLeftButtonDown += OnGestureDown;'
$a=$text.IndexOf($oldRightStart,[StringComparison]::Ordinal)
$b=$text.IndexOf($oldRightEnd,$a,[StringComparison]::Ordinal)
if($a -lt 0 -or $b -le $a){throw 'R12 old right-click event subscription missing'}
$text=$text.Substring(0,$a)+@'
        PreviewMouseRightButtonDown += OnShortcutRightMouseDown;
        PreviewMouseRightButtonUp += (_,e) => { if(activeModule==null)e.Handled=true; };
'@+[Environment]::NewLine+$text.Substring($b)
ReplaceOnce '    private bool draggingGesture;' @'
    private bool draggingGesture;
    private DateTime rightClickOpenedUntil = DateTime.MinValue;
    private bool shortcutMoveInProgress;
'@
# A "both mouse buttons" gesture must work in either press order;
# SHIFT+LEFT is the second unambiguous drag shortcut.
$shortcut=@'
    private void OnShortcutRightMouseDown(object sender,MouseButtonEventArgs e)
    {
        if(activeModule!=null)return;
        if(Mouse.LeftButton==MouseButtonState.Pressed)
        {
            e.Handled=true;
            StartShortcutDrag();
            return;
        }
        e.Handled=true;
        if(hiddenAtEdge)Reveal();
        if(ringExpanded)CollapseRing();
        else
        {
            ToggleRing();
            rightClickOpenedUntil=DateTime.UtcNow.AddMilliseconds(850);
        }
    }

    private void StartShortcutDrag()
    {
        if(activeModule!=null || shortcutMoveInProgress ||
            Mouse.LeftButton!=MouseButtonState.Pressed)return;
        shortcutMoveInProgress=true;
        holdTimer.Stop();
        holdPending=false;
        ringGesture=false;
        draggingGesture=true;
        highlightedSector=-1;
        try
        {
            if(IsMouseCaptured)ReleaseMouseCapture();
            CollapseRing();
            // Native Windows dragging remains active until left-mouse release.
            // Moving outside the ring without either shortcut NEVER drags.
            DragMove();
        }
        catch(InvalidOperationException){}
        finally
        {
            KeepOnVisibleDesktop();
            if(IsNearScreenEdge())DockToEdge(manual:true);
            else SavePosition();
            shortcutMoveInProgress=false;
            draggingGesture=false;
        }
    }
'@
ReplaceOnce '    private void OnGestureDown(object sender, MouseButtonEventArgs e)' ($shortcut+[Environment]::NewLine+'    private void OnGestureDown(object sender, MouseButtonEventArgs e)')
ReplaceOnce @'
        if (activeModule!=null || e.ChangedButton != MouseButton.Left || hiddenAtEdge || !CanDragFrom(e)) return;
'@ @'
        if(activeModule!=null || hiddenAtEdge || e.ChangedButton!=MouseButton.Left)return;
        if((Keyboard.Modifiers & ModifierKeys.Shift)!=0 ||
            Mouse.RightButton==MouseButtonState.Pressed)
        {
            e.Handled=true;
            StartShortcutDrag();
            return;
        }
        if(ringExpanded && SectorAt(e.GetPosition(this))>=0)
        {
            holdTimer.Stop();
            holdPending=false;
            ringGesture=true;
            draggingGesture=false;
            CaptureMouse();
            e.Handled=true;
            return;
        }
        if(!CanDragFrom(e))return;
'@
# Remove automatic motion-to-drag entirely. Movement outside of ring during
# held selection just clears highlight, and dropping outside runs no action.
$move=@'
    private void OnGestureMove(object sender,MouseEventArgs e)
    {
        if(activeModule!=null || draggingGesture || shortcutMoveInProgress)return;
        var p=e.GetPosition(this);
        if(Mouse.LeftButton!=MouseButtonState.Pressed)
        {
            if(ringExpanded && !ringGesture &&
                Mouse.RightButton!=MouseButtonState.Pressed &&
                DateTime.UtcNow>rightClickOpenedUntil)
            {
                double radius=Math.Min(Width,Height)*196.0/420.0;
                if((p-new Point(Width/2,Height/2)).Length>radius+4)
                    CollapseRing();
            }
            return;
        }
        if(!ringGesture)return;
        UpdateHighlightedSector(p);
        e.Handled=true;
    }
'@
ReplaceMethod '    private void OnGestureMove(object sender, MouseEventArgs e)' '    private void OnGestureUp(' $move

# A right-click should not immediately collapse due to Windows generating a
# leave event during the circle->donut size transition.
ReplaceOnce @'
            if (ringExpanded && !ringGesture && !draggingGesture &&
                Mouse.LeftButton != MouseButtonState.Pressed) CollapseRing();
'@ @'
            if (ringExpanded && !ringGesture && !draggingGesture &&
                Mouse.LeftButton != MouseButtonState.Pressed &&
                Mouse.RightButton != MouseButtonState.Pressed &&
                DateTime.UtcNow > rightClickOpenedUntil) CollapseRing();
'@
# Remove long tooltip associated with the old outside-to-drag behavior.
ReplaceOnce 'ToolTip = "Hold to open donut • release over option to select • move outside ring to drag"' 'ToolTip = null'
ReplaceOnce 'Cursor = Cursors.SizeAll' 'Cursor = Cursors.Hand'

# Render moving weather above the main dial, with clock / temperature / date
# inside a distinct translucent lower text plate. Shapes cannot obscure text.
ReplaceOnce @'
        content.Width = 244; content.Height = 176;
        content.IsHitTestVisible = false;
        Canvas.SetLeft(content, 24); Canvas.SetTop(content, 74);
'@ @'
        content.IsHitTestVisible=false;
        bool liveClock=prefs.CenterMode=="Clock" && prefs.ShowWeather &&
            DeskSpaceWeather.Latest!=null &&
            DeskSpaceWeather.Latest.City.Equals(prefs.WeatherCity,StringComparison.OrdinalIgnoreCase);
        if(liveClock)
        {
            var plate=new Border {
                Width=240,Height=122,CornerRadius=new CornerRadius(45),
                Background=new LinearGradientBrush(
                    Color.FromArgb(202,18,35,66),Color.FromArgb(225,39,33,83),90),
                IsHitTestVisible=false
            };
            Canvas.SetLeft(plate,26);Canvas.SetTop(plate,145);
            canvas.Children.Add(plate);
            content.Width=226;content.Height=114;
            Canvas.SetLeft(content,33);Canvas.SetTop(content,147);
        }
        else
        {
            content.Width=244;content.Height=176;
            Canvas.SetLeft(content,24);Canvas.SetTop(content,74);
        }
'@
# Specific FontSize is still owned by UpdateCenter; tune weather content for
# the lower legible plate rather than shrinking all other module displays.
ReplaceOnce @'
        content.FontSize = ringExpanded ? 18 : (prefs.CenterMode == "Notes" ? 30 : 32);
'@ @'
        content.FontSize = ringExpanded ? 18 :
            (prefs.CenterMode=="Clock" && prefs.ShowWeather &&
             DeskSpaceWeather.Latest!=null ? 27 : (prefs.CenterMode=="Notes"?30:32));
'@
# Keep weather drawing BEHIND the clock text in the expanded donut too.
# Remove redundant "weather is plain" tooltip from any ring display.
[IO.File]::WriteAllText($path,$text,[Text.UTF8Encoding]::new($false))
Write-Host 'R12: right-click-down opens ring; only Shift-left or both-buttons drag; weather artwork is vivid and text is unobstructed.'
