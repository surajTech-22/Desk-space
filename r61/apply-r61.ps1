$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
function Read([string]$p){[IO.File]::ReadAllText($p).Replace([string][char]13+[char]10,[string][char]10)}
function Save([string]$p,[string]$s){[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))}
function One([string]$a,[string]$b){
 $a=$a.Replace([string][char]13+[char]10,[string][char]10)
 $b=$b.Replace([string][char]13+[char]10,[string][char]10)
 $count=([regex]::Matches($script:s,[regex]::Escape($a))).Count
 if($count -ne 1){throw "R61 search patch anchor $count : $a"}
 $script:s=$script:s.Replace($a,$b)
}
# The app search already converts the circle into its native Search text bar.
# Reuse that module, not a new floating popup unable to retain keyboard focus.
$p=Join-Path $root 'DeskSpaceSearchV11.cs';$s=Read $p
One '    private bool disposed;' @'
    private bool disposed;
    private static int nextApplicationPinMode;
    private DispatcherTimer? appRefresh;
    // Applied to the next native Search module created from + Pin.
    internal static void RequestApplicationPinMode()=>
        Interlocked.Exchange(ref nextApplicationPinMode,1);
'@
One @'
    public FrameworkElement Create()
    {
        var root = new DockPanel {LastChildFill=true};
'@ @'
    public FrameworkElement Create()
    {
        bool appPinMode=Interlocked.Exchange(ref nextApplicationPinMode,0)==1;
        if(appPinMode)DeskSpaceSearchIndexV13.WarmStart();
        var root = new DockPanel {LastChildFill=true};
'@
One 'Grid.SetColumn(field,1);top.Children.Add(field);' @'
        field.ToolTip=appPinMode
            ?"Search installed applications to pin"
            :"Search files, apps, Windows Settings and notes";
        Grid.SetColumn(field,1);top.Children.Add(field);
'@
One '        var below=new StackPanel {Visibility=Visibility.Collapsed,Margin=new Thickness(7,0,7,8)};' @'
        if(appPinMode)top.ToolTip="Applications · type to find and pin an installed app";
        var below=new StackPanel {Visibility=Visibility.Collapsed,Margin=new Thickness(7,0,7,8)};
'@
One @'
        void Do(SearchHit hit,string operation)
        {
            try {
                switch(operation)
'@ @'
        void Do(SearchHit hit,string operation)
        {
            try {
                // Application-only searches are PIN operations, never launch.
                if(appPinMode)
                {
                    if(hit.Category!="Applications")return;
                    if(!settings.PinnedApps.Contains(hit.OpenTarget))
                    {
                        settings.PinnedApps.Add(hit.OpenTarget);
                        settings.Save();
                    }
                    status.Text="Pinned application: "+hit.Name;
                    exit();
                    return;
                }
                switch(operation)
'@
One @'
            foreach(var hit in found.Take(20))
            {
'@ @'
            foreach(var hit in found.Take(20))
            {
                if(appPinMode && hit.Category!="Applications")continue;
'@
One @'
                chips.Children.Add(Chip("↗","Open",hit,"open"));
'@ @'
                if(appPinMode)
                    chips.Children.Add(Chip("＋","Pin application",hit,"pin"));
                else
                    chips.Children.Add(Chip("↗","Open",hit,"open"));
'@
One @'
                if(hit.Category!="Notes")
                    chips.Children.Add(Chip("⧉","Copy path",hit,"path"));
'@ @'
                if(!appPinMode && hit.Category!="Notes")
                    chips.Children.Add(Chip("⧉","Copy path",hit,"path"));
'@
One @'
                if(File.Exists(hit.OpenTarget))
                    chips.Children.Add(Chip("▤","Copy file",hit,"file"));
'@ @'
                if(!appPinMode && File.Exists(hit.OpenTarget))
                    chips.Children.Add(Chip("▤","Copy file",hit,"file"));
'@
One @'
                if(hit.Category=="Files"||hit.Category=="Applications")
'@ @'
                if(!appPinMode && (hit.Category=="Files"||hit.Category=="Applications"))
'@
One @'
                if(File.Exists(hit.OpenTarget)||Directory.Exists(hit.OpenTarget))
'@ @'
                if(!appPinMode && (File.Exists(hit.OpenTarget)||Directory.Exists(hit.OpenTarget)))
'@
One 'if(!button){Do(hit,"open");e.Handled=true;}' 'if(!button){Do(hit,appPinMode?"pin":"open");e.Handled=true;}'
One 'else if(e.Key==Key.Enter && hits.Count>0){Do(hits[0],"open");e.Handled=true;}' 'else if(e.Key==Key.Enter && hits.Count>0){Do(hits[0],appPinMode?"pin":"open");e.Handled=true;}'
One '            if(term.Length==0) {hits.Clear();list.Children.Clear();below.Visibility=Visibility.Collapsed;SetSize();return;}' @'
            if(term.Length==0 && !appPinMode)
            {hits.Clear();list.Children.Clear();below.Visibility=Visibility.Collapsed;SetSize();return;}
'@
One @'
                var found=await Task.Run(()=>DeskSpaceModuleV10.SearchNow(term,store.Load(),token),token);
'@ @'
                var found=appPinMode
                    ?DeskSpaceSearchIndexV13.SnapshotApplications()
                        .Where(h=>term.Length==0 ||
                            h.Name.Contains(term,StringComparison.OrdinalIgnoreCase))
                        .GroupBy(h=>h.OpenTarget,StringComparer.OrdinalIgnoreCase)
                        .Select(g=>g.First())
                        .OrderBy(h=>h.Name,StringComparer.OrdinalIgnoreCase)
                        .Take(40).ToList()
                    :await Task.Run(()=>DeskSpaceModuleV10.SearchNow(term,store.Load(),token),token);
'@
One '                status.Text=found.Count+" results · all categories"+' @'
                status.Text=appPinMode
                    ?found.Count+" installed applications · select one to pin"
                    :found.Count+" results · all categories"+
'@
One @'
        frame.Loaded+=(_,_)=>{resize(72);field.Focus();Keyboard.Focus(field);};
'@ @'
        frame.Loaded+=(_,_)=>{
            resize(72);field.Focus();Keyboard.Focus(field);
            if(appPinMode)
            {
                // Discovery of Start menu apps is asynchronous. Refresh on
                // the UI dispatcher while keeping the text caret focused.
                appRefresh=new DispatcherTimer{
                    Interval=TimeSpan.FromMilliseconds(700)};
                appRefresh.Tick+=(_,_)=>{
                    if(disposed){appRefresh?.Stop();return;}
                    debounce.Stop();debounce.Start();
                };
                appRefresh.Start();
                debounce.Start();
            }
        };
'@
One '        disposed=true;debounce?.Stop();pending?.Cancel();pending?.Dispose();' @'
        disposed=true;appRefresh?.Stop();
        debounce?.Stop();pending?.Cancel();pending?.Dispose();
'@
Save $p $s
Write-Host 'R61: + Pin reuses the in-circle Search text bar with live app-only filtering, keyboard focus and pin-on-selection.'
