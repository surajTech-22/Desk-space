$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r89/DeskSpaceDeckVisibilityV89.cs' "$root/DeskSpaceDeckVisibilityV89.cs" -Force
Copy-Item 'r88/DeskSpaceShelfGroupsV88.cs' "$root/DeskSpaceShelfGroupsV88.cs" -Force
function Patch([string]$path,[string]$label,[string]$old,[string]$new)
{
    $s=[IO.File]::ReadAllText($path).Replace([string][char]13+[char]10,[string][char]10)
    $old=$old.Replace([string][char]13+[char]10,[string][char]10)
    $new=$new.Replace([string][char]13+[char]10,[string][char]10)
    $n=([regex]::Matches($s,[regex]::Escape($old))).Count
    if($n -ne 1){throw "R89 $label expected exactly one integration point, got $n"}
    $s=$s.Replace($old,$new)
    [IO.File]::WriteAllText($path,$s,[Text.UTF8Encoding]::new($false))
}
$p="$root/DeskSpaceDropShelfV15.cs"
Patch $p 'deck visibility event lifecycle' @'
        popup=new Popup {
'@ @'
        DeskSpaceDeckVisibilityV89.Changed+=DeckVisibilityChanged;
        popup=new Popup {
'@
Patch $p 'start timer only if deck visible' @'
        runningAppsTimer.Start();
        RefreshRunningApps();
'@ @'
        if(DeskSpaceDeckVisibilityV89.Visible)
        {
            runningAppsTimer.Start();
            RefreshRunningApps();
        }
'@
Patch $p 'toggle deck at runtime' @'
    private void Position(object? sender,EventArgs e)
'@ @'
    private void DeckVisibilityChanged(bool visible)
    {
        if(disposed)return;
        if(!host.Dispatcher.CheckAccess())
        {
            host.Dispatcher.BeginInvoke(new Action(()=>DeckVisibilityChanged(visible)));
            return;
        }
        if(!visible)
        {
            popup.IsOpen=false;
            runningAppsTimer.Stop();
        }
        else
        {
            runningAppsTimer.Start();
            RefreshRunningApps();
            Render();
        }
    }
    private void Position(object? sender,EventArgs e)
'@
Patch $p 'visible window honors deck preference' @'
    {if(disposed)return;popup.IsOpen=(bool)e.NewValue&&
        (held.Count+detected.Count+runningApps.Count)>0;}
'@ @'
    {if(disposed)return;popup.IsOpen=DeskSpaceDeckVisibilityV89.Visible &&
        (bool)e.NewValue&&(held.Count+detected.Count+runningApps.Count)>0;}
'@
Patch $p 'hidden deck does not render' @'
    private void Render()
    {
        if(disposed)return;
        list.Children.Clear();
'@ @'
    private void Render()
    {
        if(disposed)return;
        list.Children.Clear();
        if(!DeskSpaceDeckVisibilityV89.Visible)
        {
            popup.IsOpen=false;
            return;
        }
'@
# Remove the two unrelated global commands and place each INSIDE its own
# expanded group (never delete original files).
$s=[IO.File]::ReadAllText($p).Replace([string][char]13+[char]10,[string][char]10)
$from=$s.IndexOf('        var title=new DockPanel {Margin=new Thickness(5,0,3,5)};',[StringComparison]::Ordinal)
$to=$s.IndexOf('        var appsGroup=DeskSpaceShelfGroupsV88.Add(', $from,[StringComparison]::Ordinal)
if($from -lt 0 -or $to -le $from){throw 'R89 current global action bar not found'}
$replacement=@'
        var title=new DockPanel {Margin=new Thickness(5,0,3,4)};
        var hide=new Button{Content="×",FontSize=13,
            Padding=new Thickness(4,1,4,1),MinWidth=22,
            ToolTip="Hide deck. Restore from Circle > Settings > Deck Visibility."};
        hide.Click+=(_,_)=>{
            if(!DeskSpaceDeckVisibilityV89.TrySet(false,out var problem))
                MessageBox.Show("Could not hide the deck: "+problem,
                    "DeskSpace deck visibility");
        };
        DockPanel.SetDock(hide,Dock.Right);
        title.Children.Add(hide);
        title.Children.Add(Text(busy?"Copying files…":
            "Deck · "+runningApps.Count+" apps / "+
            held.Count+" held / "+detected.Count+" recent",
            11,Brushes.White));
        list.Children.Add(title);
'@.Replace([string][char]13+[char]10,[string][char]10)
$s=$s.Substring(0,$from)+$replacement+$s.Substring($to)
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Patch $p 'clear held copies only in Held Files group' @'
        var recentGroup=DeskSpaceShelfGroupsV88.Add(list,"Recent Files",
'@ @'
        if(heldExpanded && held.Count>0)
        {
            var clearCopies=new Button{Content="Clear copies",FontSize=11,
                HorizontalAlignment=HorizontalAlignment.Right,
                Padding=new Thickness(5,2,5,2),Margin=new Thickness(2),
                ToolTip="Discard held copies only; originals remain untouched"};
            clearCopies.Click+=(_,_)=>ClearAll();
            heldGroup.Children.Add(clearCopies);
        }
        var recentGroup=DeskSpaceShelfGroupsV88.Add(list,"Recent Files",
'@
Patch $p 'dismiss recent suggestions only in Recent Files group' @'
        bool recentHeading=false;
        foreach(var item in
'@ @'
        if(recentExpanded && detected.Count>0)
        {
            var dismissRecent=new Button{Content="Dismiss new",FontSize=11,
                HorizontalAlignment=HorizontalAlignment.Right,
                Padding=new Thickness(5,2,5,2),Margin=new Thickness(2),
                ToolTip="Dismiss only recent suggestions; never delete original files"};
            dismissRecent.Click+=(_,_)=>{detected.Clear();Render();};
            recentGroup.Children.Add(dismissRecent);
        }
        bool recentHeading=false;
        foreach(var item in
'@
Patch $p 'keep deck hidden after renders' @'
        popup.IsOpen=host.IsVisible;
'@ @'
        popup.IsOpen=DeskSpaceDeckVisibilityV89.Visible&&host.IsVisible;
'@
Patch $p 'unsubscribe visibility on dispose' @'
        runningAppsTimer.Stop();
        newFiles?.Dispose();
'@ @'
        runningAppsTimer.Stop();
        DeskSpaceDeckVisibilityV89.Changed-=DeckVisibilityChanged;
        newFiles?.Dispose();
'@
# Circle > Settings: a visible Deck Visibility toggle. This also provides
# recovery when the deck has been hidden using its close control.
$p="$root/DeskSpaceModuleV10.cs"
Patch $p 'circle settings toggle' @'
    private void BuildSettings(StackPanel body)
    {
        body.Margin=new Thickness(9,4,9,4);
'@ @'
    private void BuildSettings(StackPanel body)
    {
        body.Margin=new Thickness(9,4,9,4);
        var deckVisible=new CheckBox{
            Content="Deck Visibility · show below the circle",
            IsChecked=DeskSpaceDeckVisibilityV89.Visible,
            Margin=new Thickness(0,7,0,9)
        };
        deckVisible.Checked+=(_,_)=>{
            if(!DeskSpaceDeckVisibilityV89.TrySet(true,out var problem))
            {
                deckVisible.IsChecked=false;
                MessageBox.Show(problem,"DeskSpace deck visibility");
            }
        };
        deckVisible.Unchecked+=(_,_)=>{
            if(!DeskSpaceDeckVisibilityV89.TrySet(false,out var problem))
            {
                deckVisible.IsChecked=true;
                MessageBox.Show(problem,"DeskSpace deck visibility");
            }
        };
        body.Children.Add(deckVisible);
        body.Children.Add(Label("Hide or restore the compact deck without clearing files.",12));
'@
# Also expose a context menu recovery route directly on the circle.
$p="$root/WidgetHost.cs"
Patch $p 'circle context menu fallback' @'
        menu.Items.Add("Universal command search · Ctrl+Alt+Space",
'@ @'
        var deckMenu=new Forms.ToolStripMenuItem("Deck Visibility");
        deckMenu.DropDownOpening+=(_,_)=>{
            deckMenu.DropDownItems.Clear();
            bool shown=DeskSpaceDeckVisibilityV89.Visible;
            deckMenu.DropDownItems.Add(shown?"Hide deck":"Show deck",
                null,(_,_)=>Dispatcher.Invoke(()=>{
                    if(!DeskSpaceDeckVisibilityV89.TrySet(!shown,out var problem))
                        MessageBox.Show("Cannot update deck visibility: "+problem,
                            "DeskSpace");
                }));
        };
        menu.Items.Add(deckMenu);
        menu.Items.Add("Universal command search · Ctrl+Alt+Space",
'@
Write-Host 'R89: exclude invisible/cloaked apps; separate group-owned actions; hide/restore deck from the circle with non-destructive saved state.'
