$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r74/DeskSpaceDueShelfV74.cs' "$root/DeskSpaceDueShelfV74.cs" -Force
$p="$root/WidgetHost.cs"
$s=[IO.File]::ReadAllText($p).Replace([string][char]13+[char]10,[string][char]10)
$anchor='    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };'
if(([regex]::Matches($s,[regex]::Escape($anchor))).Count -ne 1){throw 'R74 missing one widget timer field'}
$s=$s.Replace($anchor,$anchor+[string][char]10+'    private DeskSpaceDueShelfV74? dueShelf;')
$anchor='        timer.Tick += (_, _) => Tick(); timer.Start();'
if(([regex]::Matches($s,[regex]::Escape($anchor))).Count -ne 1){throw 'R74 missing widget timer start'}
$replacement=@'
        timer.Tick += (_, _) => Tick(); timer.Start();
        // The scheduled shelf has its own system-clock checks, including
        // while a module editor is open, and follows this widget's location.
        dueShelf=new DeskSpaceDueShelfV74(this);
        Loaded+=(_,_)=>dueShelf?.Start();
        Closing+=(_,_)=>{dueShelf?.Dispose();dueShelf=null;};
'@.Replace([string][char]13+[char]10,[string][char]10).TrimEnd()
$s=$s.Replace($anchor,$replacement)
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R74: one system-clock DueShelf anchored below circle, no new store or tray bubble.'
