$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r63/DeskSpaceMiddleScrollV63.cs' (Join-Path $root 'DeskSpaceMiddleScrollV63.cs') -Force
function Read([string]$path){[IO.File]::ReadAllText($path).Replace([string][char]13+[char]10,[string][char]10)}
function Save([string]$path,[string]$s){[IO.File]::WriteAllText($path,$s,[Text.UTF8Encoding]::new($false))}
function One([string]$a,[string]$b){
 $a=$a.Replace([string][char]13+[char]10,[string][char]10)
 $b=$b.Replace([string][char]13+[char]10,[string][char]10)
 $n=([regex]::Matches($script:s,[regex]::Escape($a))).Count
 if($n -ne 1){throw "R63 anchor expected once, found $n : $a"}
 $script:s=$script:s.Replace($a,$b)
}

# Information: hover navigates/highlights only. A preview shelf must NEVER
# open without a deliberate click or Enter selection, and revisiting the
# information module clears an old selected preview before a new choice.
$p=Join-Path $root 'DeskSpaceInformationV36.cs';$s=Read $p
One '    private readonly DispatcherTimer pointerTimer;' @'
    private readonly DispatcherTimer pointerTimer;
    private readonly DeskSpaceMiddleScrollV63.Controller middleScroll;
'@
One '        arcCanvas.MouseMove+=OnArcMouseMove;' @'
        arcCanvas.MouseMove+=OnArcMouseMove;
        middleScroll=new DeskSpaceMiddleScrollV63.Controller(arcCanvas,
            ()=>all.Count>PageSize,
            ()=>arc.IsOpen&&!editorActive,
            dir=>Slide(dir));
'@
One @'
            int slot=hoveredChild;
            if(slot<0 || slot>=arcHitZones.Count)return;
            // An unchanging hover for 510ms commits just like a click.
            // Moving toward another child or an edge cancels the dwell.
            var target=arcHitZones[slot];
            ActivateChild(slot,target.id);
'@ @'
            // An unselected hover must never become a committed information
            // preview. Only mouse click, explicit drag-release or Enter can
            // call ActivateChild. Do not leave a shelf below the circle.
            EndArcHover();
'@
One @'
    public void Show()
    {
        if(disposed)return;
        try
'@ @'
    public void Show()
    {
        if(disposed)return;
        // Returning to Information starts with the wheel, not a previously
        // selected shelf. Never discard an active unsaved editor.
        if(shelf.IsOpen && !editorActive)CloseShelf();
        try
'@
One @'
        newHoverTimer.Stop();
        pointerTimer.Stop();
'@ @'
        newHoverTimer.Stop();
        middleScroll.Stop();
        pointerTimer.Stop();
'@
One @'
        // All child modules use identical hover semantics: highlight,
        // immediate safe preview, then commit after a stable dwell.
        // PW remains masked until the authenticated vault is unlocked.
        if(editorActive)return;
        if(sector<arcHitZones.Count-1 && id.HasValue)
            OpenCollection(id.Value);
        if(enteredNewSector && !capturedDragSelection &&
           edgeDirection==0 && Mouse.LeftButton!=MouseButtonState.Pressed)
        {
            // Preview is immediate; on an overflowing wheel, allow time
            // to travel to the edge without auto-collapsing the menu.
            newHoverTimer.Interval=TimeSpan.FromMilliseconds(
                all.Count>PageSize?1050:650);
            newHoverTimer.Start();
        }
'@ @'
        // Hover highlights collection names only. A preview appears solely
        // when the collection is explicitly activated. This prevents any
        // unselected collection shelf from surviving beneath the circle.
'@
One '        newHoverTimer.Stop();edgeTimer.Stop();pointerTimer.Stop();lockTimer.Stop();' @'
        newHoverTimer.Stop();edgeTimer.Stop();pointerTimer.Stop();lockTimer.Stop();
        middleScroll.Dispose();
'@
Save $p $s

# Excel and nested Excel Information inherit exactly the same middle-click
# autoscroll controller. Each deeper ring has its OWN anchor and lifecycle.
$p=Join-Path $root 'DeskSpaceExcelWheelV42.cs';$s=Read $p
One '    private readonly DispatcherTimer pointerTimer,edgeTimer,successTimer,drillTimer;' @'
    private readonly DispatcherTimer pointerTimer,edgeTimer,successTimer,drillTimer;
    private readonly DeskSpaceMiddleScrollV63.Controller middleScroll;
'@
One @'
        internal readonly Popup Popup;
        internal readonly Canvas Surface;
'@ @'
        internal readonly Popup Popup;
        internal readonly Canvas Surface;
        internal DeskSpaceMiddleScrollV63.Controller? MiddleScroll;
'@
One '        canvas.MouseWheel+=OnWheel;' @'
        canvas.MouseWheel+=OnWheel;
        middleScroll=new DeskSpaceMiddleScrollV63.Controller(canvas,
            ()=>Entries().Count>PerPage,
            ()=>arc.IsOpen&&!pasting,
            dir=>Page(dir));
'@
One @'
            ring.Popup.IsOpen=false;
            ring.Surface.Children.Clear();
'@ @'
            ring.MiddleScroll?.Dispose();
            ring.MiddleScroll=null;
            ring.Popup.IsOpen=false;
            ring.Surface.Children.Clear();
'@
One @'
        RenderRing(ring);
        ring.Surface.MouseMove+=
'@ @'
        RenderRing(ring);
        ring.MiddleScroll=new DeskSpaceMiddleScrollV63.Controller(
            ring.Surface,
            ()=>BranchItems(ring.Group).Count>PerPage,
            ()=>ring.Popup.IsOpen&&!disposed&&!pasting,
            dir=>PageRing(ring,dir));
        ring.Surface.MouseMove+=
'@
One '        pointerTimer.Stop();edgeTimer.Stop();drillTimer.Stop();successTimer.Stop();excelTimer.Stop();' @'
        pointerTimer.Stop();edgeTimer.Stop();drillTimer.Stop();successTimer.Stop();excelTimer.Stop();
        middleScroll.Dispose();
'@
One @'
    public void HideArc()
    {
'@ @'
    public void HideArc()
    {
        middleScroll.Stop();
'@
Save $p $s

# Applications also uses the universal middle-button autoscroll for its
# pinned group. No user file manager is opened and no parent wheel changes.
$p=Join-Path $root 'DeskSpaceApplicationsV55.cs';$s=Read $p
One '    private readonly Canvas canvas;' @'
    private readonly Canvas canvas;
    private readonly DeskSpaceMiddleScrollV63.Controller middleScroll;
'@
One '        canvas.MouseWheel+=Wheel;' @'
        canvas.MouseWheel+=Wheel;
        middleScroll=new DeskSpaceMiddleScrollV63.Controller(canvas,
            ()=>group=="pinned"&&Children().Count>
                DeskSpaceSubDonutStandardV62.Visible,
            ()=>arc.IsOpen&&!disposed,
            dir=>MoveChild(dir));
'@
One '    public void HideArc(){arc.IsOpen=false;group="";childPage=0;}' @'
    public void HideArc(){
        middleScroll.Stop();
        arc.IsOpen=false;group="";childPage=0;
    }
'@
One '        disposed=true;HideArc();' @'
        disposed=true;HideArc();
        middleScroll.Dispose();
'@
Save $p $s
Write-Host 'R63: unselected Information preview shelf suppressed; standard middle-click autoscroll across Applications, Information and all Excel subdonut depths.'
