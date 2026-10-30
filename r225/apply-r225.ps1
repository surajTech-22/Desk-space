$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r225/DeskSpaceSearchDeckLayoutV225.cs' -Destination "$root/DeskSpaceSearchDeckLayoutV225.cs" -Force
$lf=[string][char]10;$crlf=[string][char]13+$lf
function Swap([string]$path,[string]$old,[string]$new,[string]$reason)
{
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $s=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
 $count=([regex]::Matches($s,[regex]::Escape($old))).Count
 if($count -ne 1){throw ("R225 "+$reason+" expected one anchor, found "+$count)}
 [IO.File]::WriteAllText($path,$s.Replace($old,$new),[Text.UTF8Encoding]::new($false))
}
$host="$root/WidgetHost.cs"
Swap $host @'
        if(name=="Search")
        {
            var limits=ModuleArea();
            Width=Math.Min(limits.Width,CompactDiameter);Height=48;
            activeSearch=new DeskSpaceSearchV11(prefs,ReturnFromModule,ResizeLiveSearch);
            surface=activeSearch.Create();
        }
'@ @'
        if(name=="Search")
        {
            var limits=ModuleArea();
            // A 116-DIP circle is too small for Search, Actions and Close.
            Width=DeskSpaceSearchDeckLayoutV225.SearchWidth(limits.Width);
            Height=48;
            activeSearch=new DeskSpaceSearchV11(prefs,ReturnFromModule,ResizeLiveSearch);
            surface=activeSearch.Create();
        }
'@ 'expand Search bar'
Swap $host @'
        PositionModuleFromCircle(restoreLeft,restoreTop,CompactDiameter,CompactDiameter);
        Content=surface;
'@ @'
        // Search temporarily suppresses this widget's Deck popup so that
        // a full-width field and running-app drawer never visually overlap.
        // No held-file data or user Deck visibility setting is changed.
        attachedShelf?.SetSearchActiveV225(name=="Search");
        PositionModuleFromCircle(restoreLeft,restoreTop,CompactDiameter,CompactDiameter);
        Content=surface;
'@ 'suppress own Deck during Search'
Swap $host @'
                Width=Math.Min(ModuleArea().Width,height<=58?CompactDiameter:336);
'@ @'
        Width=DeskSpaceSearchDeckLayoutV225.SearchWidth(ModuleArea().Width);
'@ 'keep Search readable on empty and populated results'
Swap $host @'
        var search=activeSearch;activeSearch=null;
        var inline=activeInline;activeInline=null;
'@ @'
        var search=activeSearch;activeSearch=null;
        var inline=activeInline;activeInline=null;
        // Return to the normal shared Deck state after the original circle
        // has been restored, not during the Search-to-donut animation.
'@ 'document module cleanup'
Swap $host @'
        KeepOnVisibleDesktop();
        BuildCompact();SavePosition();UpdateCenter();
        Dispatcher.BeginInvoke(new Action(ResumeMediaMode),
'@ @'
        KeepOnVisibleDesktop();
        BuildCompact();SavePosition();UpdateCenter();
        attachedShelf?.SetSearchActiveV225(false);
        Dispatcher.BeginInvoke(new Action(ResumeMediaMode),
'@ 'restore Deck only after returning to circle'
$search="$root/DeskSpaceSearchV11.cs"
Swap $search @'
        top.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(22) });
'@ @'
        // The action button itself is 34 DIPs wide: reserve actual space
        // instead of allowing the purple launcher to overlap Search text.
        top.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(42) });
'@ 'nonoverlapping action icon column'
Swap $search @'
        top.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(25) });
'@ @'
        top.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(30) });
'@ 'close button column'
$deck="$root/DeskSpaceDropShelfV15.cs"
Swap $deck @'
    private bool deckMinimized,deckDismissed,appRenderPending;
'@ @'
    private bool deckMinimized,deckDismissed,appRenderPending;
    private bool searchActiveV225;
    // Foreground Search hides ONLY this widget's Deck HWND; source data,
    // retained staged files and other displays stay untouched.
    internal void SetSearchActiveV225(bool active)
    {
        if(disposed||searchActiveV225==active)return;
        searchActiveV225=active;
        if(active)
        {
            deckPreview?.Close();
            popup.IsOpen=false;
        }
        else Render();
    }
'@ 'temporary Deck suppression'
Swap $deck @'
    private void Render()
    {
        if(disposed)return;
'@ @'
    private void Render()
    {
        if(disposed)return;
        if(searchActiveV225)
        {
            // Two-second running-app refresh and shared Deck updates must
            // not resurrect the Popup in front of the Search field.
            deckPreview?.Close();
            popup.IsOpen=false;
            return;
        }
'@ 'guard every Deck Render path'
Swap $deck @'
    private void Visible(object sender,DependencyPropertyChangedEventArgs e)
    {if(disposed)return;popup.IsOpen=DeskSpaceDeckVisibilityV89.Visible &&
        !deckDismissed && (bool)e.NewValue &&
        (held.Count+detected.Count+runningApps.Count+
        excelMatchesV122.Count)>0 ||
        DeskSpaceMediaV18.ActiveV123?.DeckVisibleV123==true;} 
'@ @'
    private void Visible(object sender,DependencyPropertyChangedEventArgs e)
    {
        if(disposed)return;
        // Parentheses matter: Media presence must not reopen the Deck
        // while Search owns input or while the source widget is hidden.
        bool contents=held.Count+detected.Count+runningApps.Count+
            excelMatchesV122.Count>0 ||
            DeskSpaceMediaV18.ActiveV123?.DeckVisibleV123==true;
        popup.IsOpen=!deckDismissed &&
            DeskSpaceSearchDeckLayoutV225.ShowDeck(
                DeskSpaceDeckVisibilityV89.Visible,(bool)e.NewValue,
                searchActiveV225,contents);
    }
'@ 'prevent visibility and media resurrection of search overlay'
Swap $deck @'
        popup.IsOpen=DeskSpaceDeckVisibilityV89.Visible&&host.IsVisible;
'@ @'
        popup.IsOpen=DeskSpaceSearchDeckLayoutV225.ShowDeck(
            DeskSpaceDeckVisibilityV89.Visible,host.IsVisible,
            searchActiveV225,true);
'@ 'enforce Search suppression on final Deck opening'
Write-Host 'R225: real-width monitor-bound Search bar, nonoverlapping icon and Close, per-widget temporary Deck suppression and clean restoration.'
