$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r255/DeskSpaceFeatureActionGuardV255.cs' -Destination "$root/DeskSpaceFeatureActionGuardV255.cs" -Force
$p="$root/DeskSpaceFeatureHubV199.cs"
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$a=@'
            button.Click+=(_,_)=>{
                try{action();}
                catch(Exception ex)when(ex is InvalidOperationException or
                     System.IO.IOException or UnauthorizedAccessException)
                {
                    MessageBox.Show(this,caption+" could not open: "+
                        ex.GetType().Name,"DeskSpace features",
                        MessageBoxButton.OK,MessageBoxImage.Warning);
                }
            };
'@.Replace($crlf,$lf)
$b=@'
            // One failing tool/settings action cannot close the entire
            // Workspace. Fatal runtime failures still propagate normally.
            button.Click+=(_,_)=>{
                DeskSpaceFeatureActionGuardV255.Execute(caption,action,
                    warning=>MessageBox.Show(this,warning,
                        "DeskSpace features",MessageBoxButton.OK,
                        MessageBoxImage.Warning));
            };
'@.Replace($crlf,$lf)
$n=([regex]::Matches($s,[regex]::Escape($a))).Count
if($n -ne 1){throw ("R255 feature action guard expected one anchor; got "+$n)}
$s=$s.Replace($a,$b)
$a=@'
        Section("Windows and workspace");
'@.Replace($crlf,$lf)
$b=@'
        Section("Windows and workspace");
        Add("Settings",
            "Open the existing Settings tab without closing Workspace.",
            ()=>SelectExistingFeatureTabV199("Settings"));
'@.Replace($crlf,$lf)
$n=([regex]::Matches($s,[regex]::Escape($a))).Count
if($n -ne 1){throw ("R255 Settings entry expected one section; got "+$n)}
$s=$s.Replace($a,$b)
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R255: safe feature navigation and recoverable Settings/tool errors in Workspace.'
