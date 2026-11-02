$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$ui=Join-Path $root 'DeskSpaceSearchV11.cs'
$universal=Join-Path $root 'UniversalSearch.cs'
foreach($path in @($ui,$universal)){
 if(!(Test-Path -LiteralPath $path)){throw "R337 actual shipping source missing: $path"}
}
Copy-Item 'r337/DeskSpaceSearchGateV337.cs' (Join-Path $root 'DeskSpaceSearchGateV337.cs') -Force
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($ui).Replace($crlf,$lf)
function Once([string]$old,[string]$new,[string]$label){
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $hits=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($hits -ne 1){throw "R337 $label must match exactly once; saw $hits"}
 $script:s=$script:s.Replace($old,$new)
}
Once @'
        debounce=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(180)};
'@ @'
        var searchGateV337=new DeskSpaceSearchGateV337();
        var slowNoticeV337=new DispatcherTimer{
            Interval=TimeSpan.FromSeconds(7)};
        slowNoticeV337.Tick+=(_,_)=>{
            slowNoticeV337.Stop();
            if(!disposed && searchGateV337.Active){
                status.Text="Search is delayed. Keeping results while the drive index responds…";
                pending?.Cancel();
                if(hits.Count==0){
                    list.Children.Clear();
                    list.Children.Add(new TextBlock{
                        Text="Search timed out. Try a specific filename or folder; DeskSpace will retry once the current request ends.",
                        Margin=new Thickness(15),TextWrapping=TextWrapping.Wrap});
                    below.Visibility=Visibility.Visible;SetSize();
                }
            }
        };
        debounce=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(180)};
'@ 'single-flight timer and visible timeout'
Once @'
        debounce.Tick+=async (_,_)=>{
            debounce.Stop();pending?.Cancel();pending?.Dispose();
'@ @'
        debounce.Tick+=async (_,_)=>{
            debounce.Stop();
            // Index refresh and typing used to cancel the SAME query every
            // 1.7 seconds. Never start a second disk query until the first
            // finishes; keep only one pending refresh.
            if(!searchGateV337.Begin()){
                searchGateV337.QueueUserChange();
                pending?.Cancel();
                return;
            }
            pending?.Cancel();pending?.Dispose();
'@ 'do not cancel active query every refresh'
Once @'
            {hits.Clear();list.Children.Clear();below.Visibility=Visibility.Collapsed;SetSize();return;}
            status.Text="Searching…";below.Visibility=Visibility.Visible;SetSize();
'@ @'
            {
                hits.Clear();list.Children.Clear();below.Visibility=Visibility.Collapsed;
                SetSize();searchGateV337.Finish();return;
            }
            status.Text="Searching…";below.Visibility=Visibility.Visible;SetSize();
            slowNoticeV337.Stop();slowNoticeV337.Start();
'@ 'reset gate for empty search and watchdog'
$catchEnd=@'
            }
        };
                indexRefresh=new DispatcherTimer {Interval=TimeSpan.FromSeconds(1.7)};
'@
$newEnd=@'
            }
            finally
            {
                slowNoticeV337.Stop();
                bool queued=searchGateV337.Finish();
                if(queued && !disposed){
                    debounce.Stop();debounce.Start();
                }
            }
        };
                indexRefresh=new DispatcherTimer {Interval=TimeSpan.FromSeconds(3.5)};
'@
Once $catchEnd $newEnd 'finish work and coalesce refreshes'
# R324 already changed this timer to a revision-based implementation;
# replacing the actual timer block is safer than assuming the R221 text.
$refreshBegin=$s.IndexOf('        indexRefresh.Tick+=(_,_)=>{')
$refreshFinish=$s.IndexOf('        field.TextChanged+=(_,_)=>{',$refreshBegin)
if($refreshBegin -lt 0 -or $refreshFinish -le $refreshBegin){
 throw 'R337 cannot locate indexRefresh handler boundaries in shipping source'
}
$handler=@'
        indexRefresh.Tick+=(_,_)=>{
            if(disposed || string.IsNullOrWhiteSpace(field.Text))
            {indexRefresh.Stop();return;}
            // Never restart an in-flight search because a file index batch
            // completed. All pending index revisions coalesce into one query.
            if(searchGateV337.RefreshTick(DeskSpaceSearchIndexV13.RevisionV324))
            {
                debounce.Stop();debounce.Start();
            }
            if(!DeskSpaceSearchIndexV13.IsIndexing &&
                !searchGateV337.Active)indexRefresh.Stop();
        };
'@
$s=$s.Substring(0,$refreshBegin)+$handler.Replace($crlf,$lf)+
    $s.Substring($refreshFinish)

Once @'
        field.TextChanged+=(_,_)=>{
            debounce.Stop();debounce.Start();
            if(!indexRefresh.IsEnabled)indexRefresh.Start();
        };
'@ @'
        field.TextChanged+=(_,_)=>{
            if(searchGateV337.Active){
                searchGateV337.QueueUserChange();
                pending?.Cancel();
            }
            debounce.Stop();debounce.Start();
            if(!indexRefresh.IsEnabled)indexRefresh.Start();
        };
'@ 'user input queues newest query only'
Once @'
        debounce?.Stop();pending?.Cancel();pending?.Dispose();
'@ @'
        debounce?.Stop();indexRefresh?.Stop();pending?.Cancel();pending?.Dispose();
'@ 'stop timers on dispose'
# SlowNotice is local to Create; stop via Loaded/unloaded when the whole
# Search panel is released to avoid retaining WPF child controls.
Once @'
        return frame;
    }
    public void Dispose()
'@ @'
        frame.Unloaded+=(_,_)=>{
            slowNoticeV337.Stop();
            indexRefresh?.Stop();
            pending?.Cancel();
        };
        return frame;
    }
    public void Dispose()
'@ 'stop search watchdog when widget search closes'
[IO.File]::WriteAllText($ui,$s,[Text.UTF8Encoding]::new($false))
# The donut spinner is the observed failure. Older source snapshots have
# different UniversalSearch implementations. Do not risk a second unrelated
# UI patch in this urgent release.
Write-Host 'R337: Index refresh no longer cancels a running donut query; one coalesced refresh, 7s visible watchdog, dispose cleanup.'
