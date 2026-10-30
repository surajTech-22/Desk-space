$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r207/DeskSpaceDonutKeyboardV207.cs' -Destination "$root/DeskSpaceDonutKeyboardV207.cs" -Force
$p="$root/WidgetHost.cs"
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$before,[string]$after,[string]$reason)
{
    $before=$before.Replace($crlf,$lf);$after=$after.Replace($crlf,$lf)
    $count=([regex]::Matches($script:s,[regex]::Escape($before))).Count
    if($count -ne 1){throw ("R207 "+$reason+" needs exactly one anchor, got "+$count)}
    $script:s=$script:s.Replace($before,$after)
}
Swap @'
        menu.Items.Add("Expand / collapse ring", null, (_, _) => Dispatcher.Invoke(() => { Reveal(); ToggleRing(); }));
'@ @'
        menu.Items.Add("Expand / collapse ring", null, (_, _) => Dispatcher.Invoke(() => { Reveal(); ToggleRing(); }));
        menu.Items.Add("Navigate donut with keyboard (arrows + Enter)...", null, (_, _) =>
            Dispatcher.BeginInvoke(new Action(BeginKeyboardDonutV207)));
'@ 'explicit tray entry'
Swap @'
                DateTime.UtcNow > rightClickOpenedUntil) CollapseRing();
'@ @'
                DateTime.UtcNow > rightClickOpenedUntil && !keyboardDonutModeV207) CollapseRing();
'@ 'preserve visible donut while navigating with keyboard'
Swap @'
        menu.Items.Add("Export privacy-safe diagnostics...",null,(_,_)=>Dispatcher.Invoke(()=>{
'@ @'
        PreviewKeyDown+=KeyboardDonutKeyV207;
        Deactivated+=(_,_)=>{
            if(!keyboardDonutModeV207)return;
            EndKeyboardDonutV207();
            if(ringExpanded && activeModule==null)CollapseRing();
        };
        menu.Items.Add("Export privacy-safe diagnostics...",null,(_,_)=>Dispatcher.Invoke(()=>{
'@ 'one root-only keyboard listener and focus cleanup'
$methods=@'
    // Keyboard mode is activated only by the explicit tray item; the normal
    // widget retains WS_EX_NOACTIVATE and never steals focus from Excel.
    private bool keyboardDonutModeV207;
    private int keyboardDonutSectorV207=-1;

    private void BeginKeyboardDonutV207()
    {
        if(activeModule!=null)return;
        Reveal();
        if(!ringExpanded)ToggleRing();
        if(!ringExpanded)return;
        // Windows can now focus this widget only until the user selects,
        // presses Esc, or activates another application.
        DeskSpaceWindowBehavior.AllowInput(this,true);
        ShowActivated=true;
        keyboardDonutModeV207=true;
        keyboardDonutSectorV207=0;
        System.Windows.Automation.AutomationProperties.SetLiveSetting(
            content,System.Windows.Automation.AutomationLiveSetting.Polite);
        SetKeyboardDonutHighlightV207();
        Activate();
        Focus();
        System.Windows.Input.Keyboard.Focus(this);
    }

    private void SetKeyboardDonutHighlightV207()
    {
        if(!ringExpanded || activeModule!=null ||
           keyboardDonutSectorV207<0 ||
           keyboardDonutSectorV207>=Segments.Length)return;
        CloseInformationContext();
        if(appsWheel?.IsArcOpen==true)appsWheel.HideArc();
        if(excelWheel?.IsArcOpen==true)excelWheel.HideArc();
        highlightedSector=keyboardDonutSectorV207;
        for(int i=0;i<ringPaths.Count;i++)
        {
            ringPaths[i].Fill=i==highlightedSector?accent:
                (i%2==0?light:DeskSpaceAppearanceV148.SecondaryBrush);
            ringPaths[i].StrokeThickness=i==highlightedSector?4:2;
        }
        var name=Segments[keyboardDonutSectorV207];
        content.Text=SegmentIcons[keyboardDonutSectorV207]+" "+name+
            "\n← → select · ↑ ↓ ring · Enter open · Esc close";
        System.Windows.Automation.AutomationProperties.SetName(content,
            "DeskSpace donut sector "+(keyboardDonutSectorV207+1)+
            " of "+Segments.Length+": "+name);
        System.Windows.Automation.AutomationProperties.SetHelpText(this,
            "Use Left or Right to select a sector, Up or Down to switch rings, "+
            "Enter to open, Escape to close keyboard navigation.");
    }

    private void EndKeyboardDonutV207()
    {
        if(!keyboardDonutModeV207)return;
        keyboardDonutModeV207=false;
        keyboardDonutSectorV207=-1;
        // Return to the original non-activating widget behavior. The
        // module UI will explicitly enable input again if it needs it.
        DeskSpaceWindowBehavior.AllowInput(this,false);
        ShowActivated=false;
        System.Windows.Automation.AutomationProperties.SetName(content,
            "DeskSpace widget centre");
    }

    private void KeyboardDonutKeyV207(object sender,
        System.Windows.Input.KeyEventArgs e)
    {
        if(!keyboardDonutModeV207 || activeModule!=null || !ringExpanded)
            return;
        if(e.Key==System.Windows.Input.Key.Escape)
        {
            EndKeyboardDonutV207();
            CollapseRing();
            e.Handled=true;
            return;
        }
        if(e.Key==System.Windows.Input.Key.Enter)
        {
            var index=keyboardDonutSectorV207;
            EndKeyboardDonutV207();
            if(index>=0 && index<Segments.Length)
                ClickSegment(Segments[index]);
            e.Handled=true;
            return;
        }
        if(!DeskSpaceDonutKeyboardV207.IsNavigation(e.Key))return;
        keyboardDonutSectorV207=DeskSpaceDonutKeyboardV207.Next(
            keyboardDonutSectorV207,e.Key);
        SetKeyboardDonutHighlightV207();
        e.Handled=true;
    }

'@
Swap @'
    private void ToggleRing()
'@ ($methods+'    private void ToggleRing()') 'isolated keyboard-mode lifecycle'
Swap @'
        if(ringExpanded)
            CloseInformationContext();
'@ @'
        if(ringExpanded)
        {
            if(keyboardDonutModeV207)EndKeyboardDonutV207();
            CloseInformationContext();
        }
'@ 'reset keyboard-only focus if existing interaction closes ring'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R207: explicit keyboard root donut navigation, focus-managed single widget session and accessibility labels.'
