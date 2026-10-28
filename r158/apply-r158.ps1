$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$lf=[string][char]10
$crlf=[string][char]13+$lf
function ReplaceOnce([string]$file,[string]$before,[string]$after,[string]$reason){
 $s=[IO.File]::ReadAllText($file).Replace($crlf,$lf)
 $before=$before.Replace($crlf,$lf);$after=$after.Replace($crlf,$lf)
 $count=([regex]::Matches($s,[regex]::Escape($before))).Count
 if($count -ne 1){throw "R158 $reason expected 1 anchor found $count"}
 [IO.File]::WriteAllText($file,$s.Replace($before,$after),
   [Text.UTF8Encoding]::new($false))
}
$info="$root/DeskSpaceInformationV36.cs"
# Only the SAVED wedge is a pagination divider. Previously we passed
# count=visible saved+1, so the "2 hidden" marker split the +New action's
# outer edge. Screenshot showed a detached, tilted 2 beside +New.
ReplaceOnce $info @'
        var countView=new DeskSpaceSubDonutStandardV62.View(
            all.Count+1,startIndex,count,startIndex,
            Math.Max(0,all.Count-startIndex-visible.Length),
            -90,halfSweep,step);
'@ @'
        // + New is an action and is never the hidden-items divider.
        // Keep hidden counts on the last SAVED wedge's radial edge.
        var countView=new DeskSpaceSubDonutStandardV62.View(
            all.Count,startIndex,visible.Length,startIndex,
            Math.Max(0,all.Count-startIndex-visible.Length),
            -90,halfSweep,step);
'@ 'hidden counter on last saved item not +New'
ReplaceOnce $info @'
                Text=name,
                Width=78,
                TextAlignment=TextAlignment.Center,
'@ @'
                Text=name,
                Width=62,
                TextAlignment=TextAlignment.Center,
'@ 'reserve room between item text and radial divider counter'
ReplaceOnce $info '            Canvas.SetLeft(label,x-39);' '            Canvas.SetLeft(label,x-31);' 'center narrower label on saved wedge'
Write-Host 'R158 fixed Information hidden-count placement and label clearance.'
