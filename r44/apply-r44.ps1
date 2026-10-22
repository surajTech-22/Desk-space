$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$update=Join-Path $root 'DeskSpaceUpdater.cs'
$old=[IO.File]::ReadAllText($update).Replace([string][char]13+[char]10,[string][char]10)
$start=$old.IndexOf('    public static void StartAutomaticChecks(Window host)',[StringComparison]::Ordinal)
$end=if($start -ge 0){$old.IndexOf('    public static async Task CheckAsync(',$start,[StringComparison]::Ordinal)}else{-1}
if($start -lt 0 -or $end -le $start){throw 'R44 existing updater lifecycle missing'}
$bridge=@'
    // R44: all widget auto checks use the anonymous stable update channel.
    // Legacy per-PC manual preference never blocks approved automatic updates.
    public static void StartAutomaticChecks(Window host)
    {
        DeskSpacePublicUpdaterV44.StartAutomaticChecks(host);
    }

'@
$bridge=$bridge.Replace([string][char]13+[char]10,[string][char]10)
$old=$old.Substring(0,$start)+$bridge+$old.Substring($end)
$manual='if (automatic && !Load().AutomaticUpdates) return;'
if(!$old.Contains($manual)){throw 'R44 old manual update gate missing'}
$old=$old.Replace($manual,
    '// Legacy manual-only preference no longer prevents stable auto-updates.')
[IO.File]::WriteAllText($update,$old,[Text.UTF8Encoding]::new($false))

Copy-Item -LiteralPath 'r44/DeskSpacePublicUpdaterV44.cs' -Destination (Join-Path $root 'DeskSpacePublicUpdaterV44.cs') -Force
$facade=Join-Path $root 'DeskSpaceUpdateCheckV42.cs'
$s=[IO.File]::ReadAllText($facade)
$from='DeskSpaceUpdater.CheckAsync(owner)'
if(!$s.Contains($from)){throw 'R44 existing stable check route missing'}
$s=$s.Replace($from,'DeskSpacePublicUpdaterV44.CheckAsync(owner)')
[IO.File]::WriteAllText($facade,$s,[Text.UTF8Encoding]::new($false))

# Do not require the floating widget to be opened before updates work.
$main=Join-Path $root 'MainWindow.xaml.cs'
$s=[IO.File]::ReadAllText($main)
$init='InitializeComponent();'
if(([regex]::Matches($s,[regex]::Escape($init))).Count -ne 1){
    throw 'R44 Workspace startup has no unique initialization anchor'
}
$s=$s.Replace($init,$init+[Environment]::NewLine+
    '        Loaded += (_,_) => DeskSpacePublicUpdaterV44.StartAutomaticChecks(this);')
[IO.File]::WriteAllText($main,$s,[Text.UTF8Encoding]::new($false))

$xaml=Join-Path $root 'MainWindow.xaml'
$s=[IO.File]::ReadAllText($xaml)
$before='Automatic updates use your authorized private GitHub releases; no password or GitHub token is stored in DeskSpace.'
$after='Automatic updates enabled: public stable GitHub Releases, SHA-256 verified, no sign-in required. Check for Updates also works manually.'
if(!$s.Contains($before)){throw 'R44 Settings channel description missing'}
$s=$s.Replace($before,$after)
[IO.File]::WriteAllText($xaml,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R44: anonymous stable-release auto checking enabled for both widget and Workspace; manual preferences no longer gate updates.'
