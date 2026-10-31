$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='DeskSpace_Windows_R1/DeskSpace/DeskSpaceClipboardTransformsV177.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$old,[string]$new,[string]$label)
{
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($n -ne 1){throw ("R245 "+$label+": expected one anchor, got "+$n)}
 $script:s=$script:s.Replace($old,$new)
}
Swap @'
        read.Click+=(_,_)=>{
'@ @'
        // Clear only this window's private conversion buffers; never read
        // or empty the global Windows clipboard during this action.
        var clear=new Button{Content="Clear local preview",
            ToolTip="Forget loaded input/output without changing the Windows clipboard",
            Padding=new Thickness(9,6,9,6),
            Margin=new Thickness(0,0,0,12)};
        System.Windows.Automation.AutomationProperties.SetName(clear,
            "Clear DeskSpace clipboard conversion preview");
        clear.Click+=(_,_)=>{
            source="";output="";
            input.Clear();result.Clear();
            copy.IsEnabled=false;save.IsEnabled=false;
            status.Text="Local preview cleared. Windows clipboard unchanged.";
        };
        read.Click+=(_,_)=>{
'@ 'add explicit privacy clear action'
Swap @'
        layout.Children.Add(read);
'@ @'
        var clipboardControls=new StackPanel{
            Orientation=Orientation.Horizontal};
        clipboardControls.Children.Add(read);
        clipboardControls.Children.Add(clear);
        layout.Children.Add(clipboardControls);
'@ 'show clear alongside manual read'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R245: on-demand clipboard conversion preview now has explicit local clear without touching system clipboard.'
