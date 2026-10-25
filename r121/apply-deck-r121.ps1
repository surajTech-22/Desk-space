$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r121/DeskSpaceExcelDeckSuggestionsV123.cs' "$r/DeskSpaceExcelDeckSuggestionsV123.cs" -Force
$p="$r/DeskSpaceDropShelfV15.cs"
function Swap([string]$before,[string]$after,[string]$label)
{
 $lf=[string][char]10;$crlf=[string][char]13+$lf
 $s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
 $before=$before.Replace($crlf,$lf);$after=$after.Replace($crlf,$lf)
 $n=([regex]::Matches($s,[regex]::Escape($before))).Count
 if($n -ne 1){throw "R121 deck $($label): anchor count $n"}
 [IO.File]::WriteAllText($p,$s.Replace($before,$after),
    [Text.UTF8Encoding]::new($false))
}
Swap @'
    private DeskSpaceDeckPreviewV99? deckPreview;
'@ @'
    private DeskSpaceDeckPreviewV99? deckPreview;
    private DeskSpaceExcelDeckSuggestionsV123? excelDeckSuggestionsV123;
'@ 'deck suggest manager field'
Swap @'
        host.Closed+=Closed;
'@ @'
        host.Closed+=Closed;
        excelDeckSuggestionsV123=new DeskSpaceExcelDeckSuggestionsV123(host);
'@ 'read-only local active Excel scanner'
Swap @'
        list.Children.Add(title);
        if(deckMinimized)
        {
            popup.IsOpen=host.IsVisible;
            return;
        }
'@ @'
        list.Children.Add(title);
        if(deckMinimized)
        {
            excelDeckSuggestionsV123?.Bind(null);
            popup.IsOpen=host.IsVisible;
            return;
        }
'@ 'keep suggestions out of minimized header'
Swap @'
        popup.IsOpen=host.IsVisible;
    }
    public void Dispose()
'@ @'
        // Existing Deck section, never a second floating window.
        // Suggestions update their own small StackPanel without rebuilding
        // the running-app tiles or resetting hovered multi-window flyouts.
        var excelSuggestionsHost=new StackPanel{
            Margin=new Thickness(2,2,2,1)};
        list.Children.Add(excelSuggestionsHost);
        excelDeckSuggestionsV123?.Bind(excelSuggestionsHost);
        popup.IsOpen=host.IsVisible;
    }
    public void Dispose()
'@ 'suggestions in existing deck'
Swap @'
        deckPreview?.Dispose();
        deckPreview=null;
'@ @'
        excelDeckSuggestionsV123?.Dispose();
        excelDeckSuggestionsV123=null;
        deckPreview?.Dispose();
        deckPreview=null;
'@ 'cleanup suggestions'
Write-Host 'R121 live Excel suggestions in Deck, read-only on detection and approval-required for either paste type.'
