$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
function Read([string]$path){[IO.File]::ReadAllText($path).Replace([string][char]13+[char]10,[string][char]10)}
function Save([string]$path,[string]$source){[IO.File]::WriteAllText($path,$source,[Text.UTF8Encoding]::new($false))}
function Swap([string]$old,[string]$new)
{
    $old=$old.Replace([string][char]13+[char]10,[string][char]10)
    $new=$new.Replace([string][char]13+[char]10,[string][char]10)
    $count=([regex]::Matches($script:s,[regex]::Escape($old))).Count
    if($count -ne 1){throw "R56 preload anchor found $count times: $old"}
    $script:s=$script:s.Replace($old,$new)
}

$p=Join-Path $root 'DeskSpaceExcelStoreV16.cs';$s=Read $p
Swap '    public static List<DeskSpaceExcelTemplateV16> Load()' @'
    public static DateTime LastChangeUtc()
    {
        try{return File.Exists(PathName)?File.GetLastWriteTimeUtc(PathName):
            DateTime.MinValue;}
        catch(IOException){return DateTime.MinValue;}
        catch(UnauthorizedAccessException){return DateTime.MinValue;}
    }
    public static List<DeskSpaceExcelTemplateV16> Load()
'@
Save $p $s

$p=Join-Path $root 'DeskSpaceExcelWheelV42.cs';$s=Read $p
Swap '    private List<DeskSpaceExcelTemplateV16> formats=new();' @'
    private List<DeskSpaceExcelTemplateV16> formats=new();
    private bool warmed,warming;
    private DateTime warmStamp=DateTime.MinValue;
    // Decryption and IO occur in a background worker, not in a hover.
    public void WarmStart()
    {
        if(disposed||warming)return;
        warming=true;
        _=System.Threading.Tasks.Task.Run(()=>{
            try
            {
                var stamp=DeskSpaceExcelStoreV16.LastChangeUtc();
                var snapshot=DeskSpaceExcelStoreV16.Load()
                    .OrderBy(x=>x.GroupPath,StringComparer.OrdinalIgnoreCase)
                    .ThenBy(x=>x.Name,StringComparer.OrdinalIgnoreCase).ToList();
                owner.Dispatcher.BeginInvoke(new Action(()=>{
                    if(disposed)return;
                    formats=snapshot;warmStamp=stamp;warmed=true;warming=false;
                    if(arc.IsOpen)Render();
                }));
            }
            catch(Exception ex)
            {
                owner.Dispatcher.BeginInvoke(new Action(()=>{
                    if(disposed)return;
                    warming=false;
                    if(arc.IsOpen)Status("Unable to load Excel formats",ex.Message);
                }));
            }
        });
    }
'@
Swap @'
            formats=DeskSpaceExcelStoreV16.Load()
                .OrderBy(x=>x.GroupPath,StringComparer.OrdinalIgnoreCase)
                .ThenBy(x=>x.Name,StringComparer.OrdinalIgnoreCase).ToList();
'@ @'
            if(!warmed || DeskSpaceExcelStoreV16.LastChangeUtc()!=warmStamp)
                WarmStart(); // paint the cached data; refresh asynchronously
'@
Save $p $s

$p=Join-Path $root 'DeskSpaceInformationV36.cs';$s=Read $p
Swap '    private List<Collection> all=new();' @'
    private List<Collection> all=new();
    private bool warmed,warming;
    private DateTime warmStamp=DateTime.MinValue;
    private static DateTime CurrentStamp()
    {
        var target=File.Exists(CollectionsPath)?CollectionsPath:LegacyPath;
        try{return File.Exists(target)?File.GetLastWriteTimeUtc(target):
            DateTime.MinValue;}
        catch(IOException){return DateTime.MinValue;}
        catch(UnauthorizedAccessException){return DateTime.MinValue;}
    }
    public void WarmStart()
    {
        if(disposed||warming)return;
        warming=true;
        _=System.Threading.Tasks.Task.Run(()=>{
            try
            {
                var stamp=CurrentStamp();
                var snapshot=Read().OrderByDescending(x=>x.UpdatedUtc).ToList();
                owner.Dispatcher.BeginInvoke(new Action(()=>{
                    if(disposed)return;
                    all=snapshot;warmStamp=stamp;warmed=true;warming=false;
                    if(arc.IsOpen){RenderArc();PositionArc();changed();}
                }));
            }
            catch(Exception ex)
            {
                owner.Dispatcher.BeginInvoke(new Action(()=>{
                    if(disposed)return;
                    warming=false;
                    if(arc.IsOpen)warning.Text=
                        "Saved information cannot be read: "+ex.Message;
                }));
            }
        });
    }
'@
Swap @'
    public void Show()
    {
        if(disposed)return;
        try
        {
            all=Read().OrderByDescending(x=>x.UpdatedUtc).ToList();
'@ @'
    public void Show()
    {
        if(disposed)return;
        try
        {
            if(!warmed || CurrentStamp()!=warmStamp)
                WarmStart(); // immediately paint cache, refresh if stale
'@
Save $p $s

$p=Join-Path $root 'WidgetHost.cs';$s=Read $p
Swap '            DeskSpaceSearchIndexV13.WarmStart();' @'
            DeskSpaceSearchIndexV13.WarmStart();
            // Pre-create and pre-read both expensive child modules. Hover
            // only renders; the original persisted files remain unchanged.
            excelWheel??=new DeskSpaceExcelWheelV42(this,()=>{
                if(ringExpanded)CollapseRing();
            });
            excelWheel.WarmStart();
            informationOverlay??=new DeskSpaceInformationV36(this,()=>{
                if(informationOverlay?.IsOpen==true)
                    content.Text="Information"+Environment.NewLine+
                        informationOverlay.SavedCount+" saved";
                else UpdateCenter();
            });
            informationOverlay.WarmStart();
'@
Save $p $s
Write-Host 'R56: Excel mappings and Information collections warmed at startup without blocking their sub-donut navigation.'
