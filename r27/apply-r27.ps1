$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r27/DeskSpaceInformationV27.cs' "$root/DeskSpaceInformationV27.cs" -Force
$p="$root/WidgetHost.cs"
$s=[IO.File]::ReadAllText($p).Replace([string][char]13+[char]10,[string][char]10)
function Must([string]$before,[string]$after)
{
    $before=$before.Replace([string][char]13+[char]10,[string][char]10)
    $after=$after.Replace([string][char]13+[char]10,[string][char]10)
    if(!$script:s.Contains($before)) {throw "R27 source anchor missing: $before"}
    $script:s=$script:s.Replace($before,$after)
}
Must 'DeskSpaceInformationV26? informationOverlay' 'DeskSpaceInformationV27? informationOverlay'
$begin=$s.IndexOf('    private void OpenInformationArc()', [StringComparison]::Ordinal)
$end=if($begin -ge 0){$s.IndexOf('    private void ClickSegment(string name)', $begin,[StringComparison]::Ordinal)}else{-1}
if($begin -lt 0 -or $end -le $begin){throw 'R27 cannot isolate previous Information arc'}
$old=$s.Substring($begin,$end-$begin)
if(!$old.Contains('informationOverlay.Show()') -or !$old.Contains('DeskSpaceInformationV26'))
{throw 'R27 previous arc lifecycle was unexpectedly changed'}
$fixed=@'
    private void OpenInformationArc()
    {
        if(activeModule!=null)return;
        // Information is a CHILD of the live two-ring donut, not a new
        // clock-mode menu. A second selection only closes its child sectors.
        if(informationOverlay?.IsOpen==true)
        {
            informationOverlay.Hide();
            return;
        }
        // Input release can close the parent donut AFTER ClickSegment.
        // Defer until the entire gesture completes, then make sure that
        // the donut is really expanded before showing its curved sectors.
        Dispatcher.BeginInvoke(new Action(()=>{
            if(activeModule!=null)return;
            if(!ringExpanded)ToggleRing();
            if(!ringExpanded)return;
            var work=SystemParameters.WorkArea;
            // Space for the attached band beyond the 408-DIP outer ring.
            // Do not put its popup outside the visible desktop.
            Left=Math.Clamp(Left,work.Left+42,
                Math.Max(work.Left+42,work.Right-OpenDiameter-44));
            Top=Math.Clamp(Top,work.Top+103,
                Math.Max(work.Top+103,work.Bottom-OpenDiameter));
            KeepOnVisibleDesktop();
            informationOverlay??=new DeskSpaceInformationV27(this,()=>{
                if(informationOverlay?.IsOpen==true)
                    content.Text="Information"+Environment.NewLine+
                        informationOverlay.SavedCount+" saved";
                else UpdateCenter();
            });
            informationOverlay.Show();
            if(informationOverlay.IsOpen)
                content.Text="Information"+Environment.NewLine+
                    informationOverlay.SavedCount+" saved";
        }),System.Windows.Threading.DispatcherPriority.Background);
    }

'@
$s=$s.Substring(0,$begin)+$fixed+$s.Substring($end)
# Collapse the attached subdonut atomically with its parent ring. This also
# protects against detached arcs when the circle is minimized or resized.
$togg='    private void ToggleRing()'+[string][char]10+'    {'
if(!$s.Contains($togg)){throw 'R27 ToggleRing state anchor not found'}
$s=$s.Replace($togg,$togg+[string][char]10+
    '        if(ringExpanded && informationOverlay?.IsOpen==true)informationOverlay.Hide();')
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R27: child donut sectors are attached to expanded parent donut, and ring collapse hides its submenus.'
