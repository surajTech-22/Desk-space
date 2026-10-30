$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r228/DeskSpaceDeckOffsetV228.cs' -Destination "$root/DeskSpaceDeckOffsetV228.cs" -Force
$lf=[string][char]10;$crlf=[string][char]13+$lf
function Swap([string]$path,[string]$old,[string]$new,[string]$label)
{
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $s=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
 $n=([regex]::Matches($s,[regex]::Escape($old))).Count
 if($n -ne 1){throw ("R228 "+$label+" expected one anchor, got "+$n)}
 [IO.File]::WriteAllText($path,$s.Replace($old,$new),[Text.UTF8Encoding]::new($false))
}
$mon="$root/DeskSpaceMultiMonitorV151.cs"
Swap $mon '                else PlaceIfOffscreen(replica,screen);' @'
                else
                {
                    // Never reapply a shared WPF startup location to an
                    // existing satellite: it yanked a user-positioned widget
                    // toward the physical seam on every reconciliation.
                    // Use the owning display's native fence instead.
                    DeskSpaceMonitorFenceV152.ClampNow(replica);
                }
'@ 'no repeated magnetic secondary placements'
$dock="$root/DeskSpaceMonitorDockV190.cs"
Swap $dock @'
        popup.PlacementTarget=owner;
        popup.Placement=PlacementMode.Custom;
'@ @'
        // Each Deck owns its position and drag state independently. Only
        // staged held-file DATA is shared with the other physical display.
        string Device()
        {
            var widget=owner as WidgetShellWindow;
            return widget==null?
                Forms.Screen.FromHandle(new WindowInteropHelper(owner).Handle).DeviceName:
                DeskSpaceMonitorSessionsV154.Device(widget) ??
                DeskSpaceMonitorFenceV152.BoundScreen(widget)?.DeviceName ??
                Forms.Screen.FromHandle(new WindowInteropHelper(owner).Handle).DeviceName;
        }
        var shift=DeskSpaceDeckOffsetV228.Read(Device());
        bool dragging=false;
        System.Drawing.Point startCursor=default;
        DeskSpaceDeckOffsetV228.Offset startOffset=new(0,0);
        void Reposition()
        {
            // A tiny alternating horizontal offset requests WPF's Custom
            // placement callback again, without relocating the OTHER Deck.
            popup.HorizontalOffset=popup.HorizontalOffset==0?0.001:0;
        }
        border.PreviewMouseLeftButtonDown+=(_,e)=>{
            if(e.GetPosition(border).Y>34)return;
            var child=e.OriginalSource as DependencyObject;
            while(child!=null)
            {
                if(child is Button)return;
                child=VisualTreeHelper.GetParent(child);
            }
            dragging=true;
            startCursor=Forms.Cursor.Position;
            startOffset=shift;
            System.Windows.Input.Mouse.Capture(border);
            e.Handled=true;
        };
        border.PreviewMouseMove+=(_,e)=>{
            if(!dragging)return;
            if(e.LeftButton!=System.Windows.Input.MouseButtonState.Pressed)return;
            var now=Forms.Cursor.Position;
            shift=DeskSpaceDeckOffsetV228.Clamp(new(
                startOffset.X+now.X-startCursor.X,
                startOffset.Y+now.Y-startCursor.Y));
            Reposition();
            e.Handled=true;
        };
        border.PreviewMouseLeftButtonUp+=(_,e)=>{
            if(!dragging)return;
            dragging=false;
            System.Windows.Input.Mouse.Capture(null);
            DeskSpaceDeckOffsetV228.Save(Device(),shift);
            e.Handled=true;
        };
        border.LostMouseCapture+=(_,_)=>{
            if(!dragging)return;
            dragging=false;
            DeskSpaceDeckOffsetV228.Save(Device(),shift);
        };
        popup.PlacementTarget=owner;
        popup.Placement=PlacementMode.Custom;
'@ 'each Deck has independent drag-to-place state'
Swap $dock @'
            var result=Calculate(ownerBounds,screen,
                (int)Math.Ceiling(popupSize.Width*sx),
                (int)Math.Ceiling(popupSize.Height*sy));
'@ @'
            var result=Calculate(ownerBounds,screen,
                (int)Math.Ceiling(popupSize.Width*sx),
                (int)Math.Ceiling(popupSize.Height*sy));
            // Monitor-local user Deck offset, never a virtual-desktop or
            // global/static offset. Clamp the whole physical popup so it
            // cannot wrap over another monitor when monitors have mixed DPI.
            var width=result.Width;var height=result.Height;
            var deckX=Math.Clamp(result.X+shift.X,screen.Left,
                Math.Max(screen.Left,screen.Right-width));
            var deckY=Math.Clamp(result.Y+shift.Y,screen.Top,
                Math.Max(screen.Top,screen.Bottom-height));
'@ 'bound monitor-specific Deck position'
Swap $dock @'
                (result.X-ownerBounds.Left)/sx,
                (result.Y-ownerBounds.Top)/sy);
'@ @'
                (deckX-ownerBounds.Left)/sx,
                (deckY-ownerBounds.Top)/sy);
'@ 'use independent Deck geometry in popup callback'
Write-Host 'R228: existing secondary monitor position no longer snaps to a shared startup edge; both Decks drag independently, persist by monitor, and clamp natively.'
