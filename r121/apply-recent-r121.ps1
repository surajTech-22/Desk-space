$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r121/DeskSpacePinnedRecentV122.cs' "$r/DeskSpacePinnedRecentV122.cs" -Force
$p="$r/DeskSpaceApplicationsV55.cs"
function Swap([string]$before,[string]$after,[string]$label)
{
  $lf=[string][char]10;$crlf=[string][char]13+$lf
  $s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
  $before=$before.Replace($crlf,$lf);$after=$after.Replace($crlf,$lf)
  $n=([regex]::Matches($s,[regex]::Escape($before))).Count
  if($n -ne 1){throw "Pinned Recent $($label): expected once, got $n"}
  [IO.File]::WriteAllText($p,$s.Replace($before,$after),
      [Text.UTF8Encoding]::new($false))
}
Swap '    private string group="";' @'
    private string group="";
    // Hovering a pinned app exposes its Windows-recent documents in the next
    // radial band. The file opens only when its own document wedge is clicked.
    private string recentAppV121="";
'@ 'recent file context'
Swap @'
        var shapes=items.Select(x=>
'@ @'
        if(group=="pinned" && recentAppV121.Length>0)
        {
            var children=Children();
            int i=children.FindIndex(x=>string.Equals(x.Path,recentAppV121,
                StringComparison.OrdinalIgnoreCase));
            if(i>=currentChildView.Start &&
               i<currentChildView.Start+currentChildView.Count)
            {
                int slot=i-currentChildView.Start;
                double centreAngle=(currentChildView.From(slot)+
                    currentChildView.To(slot))/2;
                var files=DeskSpacePinnedRecentV122.FilesFor(recentAppV121,5);
                var sub=DeskSpaceSubDonutStandardV62.Plan(files.Count,0,
                    centreAngle,currentChildView.Step);
                if(files.Count==0)
                    Add("No recent files","emptyRecent","",
                        centreAngle-9,centreAngle+9,2);
                for(int j=0;j<sub.Count;j++)
                    Add(System.IO.Path.GetFileName(files[j]),"recent",files[j],
                        sub.From(j),sub.To(j),2);
            }
        }
        var shapes=items.Select(x=>
'@ 'nested recent file wedges'
Swap @'
        if(kind=="pinned"&&group!=kind)
        {group=kind;childPage=0;Render();}
'@ @'
        if(kind=="pinned"&&group!=kind)
        {group=kind;childPage=0;recentAppV121="";Render();return;}
        if(kind=="launch"&&group=="pinned" &&
           recentAppV121!=hitZones[i].Path)
        {
            recentAppV121=hitZones[i].Path;
            Render();
        }
'@ 'app hover reveals recent doc submenu'
Swap '    public void HideArc(){arc.IsOpen=false;group="";childPage=0;}' @'
    public void HideArc(){arc.IsOpen=false;group="";childPage=0;recentAppV121="";}
'@ 'reset hover'
Swap @'
            case "launch":
                if(IsLaunchable(item.Path))
'@ @'
            case "recent":
                try
                {
                    if(!DeskSpacePinnedRecentV122.OpenDocumentForPinnedApp(
                        recentAppV121,item.Path))
                        MessageBox.Show("Recent document is no longer associated "+
                            "with the pinned app.","DeskSpace Applications");
                    else HideArc();
                }
                catch(Exception ex){MessageBox.Show(ex.Message,"DeskSpace Applications");}
                return true;
            case "launch":
                if(IsLaunchable(item.Path))
'@ 'explicit recent document launch'
Write-Host 'R121 pinned apps: hover shows recent documents; clicking a document opens it using matching pinned app. No arbitrary command execution.'
