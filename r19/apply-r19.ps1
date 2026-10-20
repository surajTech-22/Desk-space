$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r19/DeskSpaceMediaV19.cs' "$root/DeskSpaceMediaV18.cs" -Force
function Read([string]$path){
    [IO.File]::ReadAllText($path).Replace([string][char]13+[char]10,[string][char]10)
}
function Save([string]$path,[string]$value){
    [IO.File]::WriteAllText($path,$value,[Text.UTF8Encoding]::new($false))
}
function Must([string]$old,[string]$new) {
    $old=$old.Replace([string][char]13+[char]10,[string][char]10)
    $new=$new.Replace([string][char]13+[char]10,[string][char]10)
    if(!$script:s.Contains($old)){throw "R19 required source anchor missing: $old"}
    $script:s=$script:s.Replace($old,$new)
}
$p="$root/WidgetHost.cs";$s=Read $p
# Replace the entire old media-only circle logic with a true below-circle Popup.
$from=$s.IndexOf('    private void OnMediaVisibility(bool canDisplay)',[StringComparison]::Ordinal)
$until=if($from -ge 0){$s.IndexOf('    private void OpenInWidget(string name)', $from,[StringComparison]::Ordinal)}else{-1}
if($from -lt 0 -or $until -le $from){throw 'R19 media method boundary missing'}
$replacement=@'
    // The original circle remains unchanged. Media floats BELOW it.
    // The header is the actual current module text, not its module name.
    private void OnMediaVisibility(bool active)
    {
        if(!Dispatcher.CheckAccess()){
            Dispatcher.BeginInvoke(new Action(()=>OnMediaVisibility(active)));
            return;
        }
        if(!active || activeModule!=null || ringExpanded || hiddenAtEdge){
            LeaveMediaMode();
            return;
        }
        media.ShowBelow(this,()=>content.Text);
        attachedShelf?.SetMediaOffset(218);
    }
    private void LeaveMediaMode()
    {
        media.CloseBelow();
        attachedShelf?.SetMediaOffset(5);
    }
    private void ResumeMediaMode()
    {
        if(media.Visible && activeModule==null && !ringExpanded && !hiddenAtEdge)
            OnMediaVisibility(true);
    }
'@
$s=$s.Substring(0,$from)+$replacement+[Environment]::NewLine+$s.Substring($until)
Must '    private bool mediaDisplayed;' @'
    // Playback is rendered in a separately anchored Popup, not over Clock.
'@
Must '    private object? surfaceBeforeMedia;' ''
# The circle must not be hidden by a module-switch, and a hidden media circle
# is always restored when its session ends.
Must 'Width=Math.Min(ModuleArea().Width,height<=58?CompactDiameter:Math.Max(270,CompactDiameter*2.5));' 'Width=Math.Min(ModuleArea().Width,height<=58?CompactDiameter:336);'
Save $p $s

# The existing temporary file list can coexist below the media controls.
$p="$root/DeskSpaceDropShelfV15.cs";$s=Read $p
Must '    public void Enable(){if(!disposed)Render();}' @'
    public void Enable(){if(!disposed)Render();}
    public void SetMediaOffset(double offset)
    {
        if(disposed)return;
        popup.VerticalOffset=Math.Max(5,offset);
    }
'@
Save $p $s

$p="$root/DeskSpaceSearchV11.cs";$s=Read $p
# A one-result list was clipped because height=60+43 while actual WPF
# header/status/row take approximately 125 DIP. Scroll only the results.
Must '? 60+Math.Min(260,Math.Max(1,count)*43):48);' '? 80+Math.Min(260,Math.Max(1,count)*44):48);'
Must 'Margin=new Thickness(12,2,0,6)' 'Margin=new Thickness(8,2,0,3)'
Must 'Margin=new Thickness(3,2,3,2),Padding=new Thickness(10,3,8,3),MinHeight=40' 'Margin=new Thickness(3,1,3,1),Padding=new Thickness(7,2,6,2),MinHeight=40'
# Replacing the five hover action chips with a single contextual menu makes
# room for filenames and full paths even inside a 336-DIP result area.
$from=$s.IndexOf('                chips.Children.Add(Chip("↗","Open",hit,"open"));',[StringComparison]::Ordinal)
$until=if($from -ge 0){$s.IndexOf('                DockPanel.SetDock(chips,Dock.Right);layout.Children.Add(chips);',$from,[StringComparison]::Ordinal)}else{-1}
if($from -lt 0 -or $until -le $from){throw 'R19 search action strip boundary missing'}
$action=@'
                var more=new Button {
                    Content="⋯",ToolTip="File and app actions",
                    Width=28,Height=27,Padding=new Thickness(0),
                    FontSize=17,BorderThickness=new Thickness(0),
                    Background=new SolidColorBrush(Color.FromRgb(236,232,249)),
                    Foreground=new SolidColorBrush(Color.FromRgb(66,52,113))
                };
                var actions=new ContextMenu();
                void AddAction(string caption,string code,bool available=true)
                {
                    if(!available)return;
                    var action=new MenuItem{Header=caption};
                    action.Click+=(_,_)=>Do(hit,code);
                    actions.Items.Add(action);
                }
                AddAction("Open","open");
                AddAction("Copy path","path",hit.Category!="Notes");
                AddAction("Copy file","file",File.Exists(hit.OpenTarget));
                AddAction("Pin to DeskSpace","pin",
                    hit.Category=="Files"||hit.Category=="Applications");
                AddAction("Show in Explorer","folder",
                    File.Exists(hit.OpenTarget)||Directory.Exists(hit.OpenTarget));
                more.Click+=(_,e)=>{
                    e.Handled=true;
                    more.ContextMenu=actions;
                    actions.PlacementTarget=more;
                    actions.IsOpen=true;
                };
                chips.Children.Add(more);
'@
$s=$s.Substring(0,$from)+$action+[Environment]::NewLine+$s.Substring($until)
Must @'
                    Text=(hit.Category=="Applications"?"▣  ":hit.Category=="Settings"?"⚙  ":
                        hit.Category=="Notes"?"✎  ":"▤  ")+hit.Name,
'@ @'
                    Text=hit.Name,
'@
Must 'status.Text=found.Count+" matches · files, apps, settings"+' 'status.Text=found.Count+" results"+'
Save $p $s
Write-Host 'R19: source circle remains; media panel anchored below with real module data, transport/seek/volume/hide; search rows fully visible with compact actions.'
