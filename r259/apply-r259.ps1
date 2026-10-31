$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r259/DeskSpacePaletteQueryV259.cs' -Destination "$root/DeskSpacePaletteQueryV259.cs" -Force
$p="$root/DeskSpaceActionPaletteV197.cs"
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$a,[string]$b,[string]$why){
 $a=$a.Replace($crlf,$lf);$b=$b.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($a))).Count
 if($n -ne 1){throw ("R259 "+$why+": expected one anchor, got "+$n)}
 $script:s=$script:s.Replace($a,$b)
}
# Preserve all existing commands and dispatch routes; modify only query.
$pattern='(?s)    internal static IReadOnlyList<Command> Filter\(string\? query\)\s*\{.*?\n    \}\n    private static void Run\('
$matches=[regex]::Matches($s,$pattern)
if($matches.Count -ne 1){throw ('R259 exact palette filter boundary expected 1; got '+$matches.Count)}
$replacement=@'
    internal static IReadOnlyList<Command> Filter(string? query)=>
        DeskSpacePaletteQueryV259.Filter(Commands,query);

    private static void Run(
'@.Replace($crlf,$lf)
$s=[regex]::Replace($s,$pattern,[System.Text.RegularExpressions.MatchEvaluator]{
    param($match)
    return $replacement
},1)
Swap @'
            Text="Type to filter · ↑/↓ select · Enter open · Esc close",
'@ @'
            Text="Type words or @Windows/@System/@Files · ↑/↓, PgUp/PgDn, Home/End · Enter open · Esc close",
'@ 'explain category search in the existing visible hint'
Swap @'
                box.Children.Add(new TextBlock{
                    Text=command.Name,FontSize=14,FontWeight=FontWeights.SemiBold});
'@ @'
                box.Children.Add(new TextBlock{
                    Text=command.Name,FontSize=14,FontWeight=FontWeights.SemiBold});
                box.Children.Add(new TextBlock{
                    Text=command.Group+" · "+(command.Confirm?"Confirmation required":"Manual action"),
                    FontSize=10,Foreground=Brushes.DimGray});
'@ 'accessible group and safety metadata per result'
Swap @'
            status.Text=list.Items.Count+" available actions. Execution requires selection.";
'@ @'
            status.Text=list.Items.Count==0?
                "No matching actions. Try @Windows, @System or clear the filter.":
                list.Items.Count+" matching actions. Select an action to run.";
'@ 'truthful no-result status'
Swap @'
            if(e.Key==Key.Down || e.Key==Key.Up)
            {
                if(list.Items.Count>0)
                    list.SelectedIndex=Math.Clamp(list.SelectedIndex+
                        (e.Key==Key.Down?1:-1),0,list.Items.Count-1);
                e.Handled=true;
            }
'@ @'
            if(e.Key==Key.Down || e.Key==Key.Up ||
               e.Key==Key.PageDown || e.Key==Key.PageUp ||
               e.Key==Key.Home || e.Key==Key.End)
            {
                if(list.Items.Count>0)
                {
                    int next=e.Key switch {
                        Key.Home=>0, Key.End=>list.Items.Count-1,
                        _=>DeskSpacePaletteQueryV259.Navigate(
                            list.SelectedIndex,list.Items.Count,
                            e.Key==Key.Down?1:e.Key==Key.Up?-1:
                            e.Key==Key.PageDown?7:-7)
                    };
                    list.SelectedIndex=next;
                    list.ScrollIntoView(list.SelectedItem);
                }
                e.Handled=true;
            }
'@ 'keyboard-only palette navigation'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R259: in-memory group search, predictable ranking, keyboard page/home/end and visible result state.'
