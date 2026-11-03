$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$universal=Join-Path $root 'UniversalSearch.cs'
$donut=Join-Path $root 'DeskSpaceSearchV11.cs'
foreach($p in @($universal,$donut)){if(!(Test-Path $p)){throw "R342 shipping Search source missing: $p"}}
Copy-Item 'r342/DeskSpaceUnifiedResultMenuV342.cs' (Join-Path $root 'DeskSpaceUnifiedResultMenuV342.cs') -Force
$lf=[string][char]10
$crlf=[string][char]13+$lf
function Once([string]$path,[string]$old,[string]$new,[string]$reason)
{
 $s=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $hits=([regex]::Matches($s,[regex]::Escape($old))).Count
 if($hits -ne 1){throw "R342 $reason expected 1 source match, found $hits"}
 [IO.File]::WriteAllText($path,$s.Replace($old,$new),
     [Text.UTF8Encoding]::new($false))
}
Once $universal @'
        list.MouseDoubleClick+=(_,_)=>Selected(Launch);Loaded+=(_,_)=>query.Focus();
'@ @'
        list.MouseDoubleClick+=(_,_)=>Selected(Launch);Loaded+=(_,_)=>query.Focus();
        // Context menu is the SAME policy as the donut's actions. Right
        // clicking an item selects THAT result rather than a stale selection.
        list.ContextMenuOpening+=(_,e)=>{
            if(e.OriginalSource is not DependencyObject origin){
                e.Handled=true;return;
            }
            var container=ItemsControl.ContainerFromElement(list,origin)
                as ListBoxItem;
            if(container==null || container.DataContext is not SearchHit hit){
                e.Handled=true;return;
            }
            list.SelectedItem=hit;
            var options=DeskSpaceUnifiedResultMenuV342.Available(hit);
            if(options.Count==0){e.Handled=true;return;}
            list.ContextMenu=DeskSpaceUnifiedResultMenuV342.Create(hit,
                (action,result)=>{
                    switch(action)
                    {
                        case "open":Launch(result);break;
                        case "folder":OpenFolder(result);break;
                        case "path":CopyPath(result);break;
                        case "file":CopyFile(result);break;
                        case "pin":Pin(result);break;
                    }
                });
        };
'@ 'right-click selected Workspace search result'
# The R337 timer patch does not touch the original result menu; replace
# only its action specification with the common policy.
Once $donut @'
                if(appPinMode)
                    AddAction("Pin application","pin");
                else
                {
                    AddAction("Open","open");
                    AddAction("Copy path","path",hit.Category!="Notes");
                    AddAction("Copy file","file",File.Exists(hit.OpenTarget));
                    AddAction("Pin to DeskSpace","pin",
                        hit.Category=="Files"||hit.Category=="Applications");
                    AddAction("Show in Explorer","folder",
                        File.Exists(hit.OpenTarget)||Directory.Exists(hit.OpenTarget));
                }
'@ @'
                foreach(var choice in DeskSpaceUnifiedResultMenuV342.Available(
                    hit,appPinMode))
                    AddAction(choice.Label,choice.Id);
'@ 'same action policy in donut search menu'
$u=Get-Content $universal -Raw
$d=Get-Content $donut -Raw
if(!$u.Contains('DeskSpaceUnifiedResultMenuV342.Create(hit') -or
   !$d.Contains('DeskSpaceUnifiedResultMenuV342.Available(')){
 throw 'R342 missing shared policy on donut or Universal Search surface'
}
Write-Host 'R342: both donut and Workspace Search now use one revalidated result action policy; files/paths never deleted.'
