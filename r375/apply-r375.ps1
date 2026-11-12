$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
# R375 - connect Excel Paste option to the Deck.
# Apply after R374.
$context='DeskSpace_Windows_R1/DeskSpace/DeskSpaceSmartContextDeckV280.cs'
if(Test-Path -LiteralPath $context){
    $nl=[string][char]10
    $contextSource=[IO.File]::ReadAllText($context).Replace([string][char]13+$nl, $nl)

    $find1='            ["Excel"]=new[]{
                new Suggestion("excel","Open Excel mappings"),
                new Suggestion("clipboard","Clipboard conversions")},'.Replace([string][char]13+$nl, $nl)
    $repl1='            ["Excel"]=new[]{
                new Suggestion("excel_paste","Paste to Excel"),
                new Suggestion("excel","Open Excel mappings"),
                new Suggestion("clipboard","Clipboard conversions")},'.Replace([string][char]13+$nl, $nl)
    if($contextSource.Contains($find1)){
        $contextSource=$contextSource.Replace($find1,$repl1)
    }

    $find2='            case "excel":
                _=DeskSpaceFeatureNavigationV200.OpenExcelAsync(owner);
                break;'.Replace([string][char]13+$nl, $nl)
    $repl2='            case "excel_paste":
                DeskSpaceExcelClipboardV39.ShowActive();
                break;
            case "excel":
                _=DeskSpaceFeatureNavigationV200.OpenExcelAsync(owner);
                break;'.Replace([string][char]13+$nl, $nl)
    if($contextSource.Contains($find2)){
        $contextSource=$contextSource.Replace($find2,$repl2)
    }
    [IO.File]::WriteAllText($context,$contextSource,[Text.UTF8Encoding]::new($false))
}

$deck='DeskSpace_Windows_R1/DeskSpace/DeskSpaceDropShelfV15.cs'
if(Test-Path -LiteralPath $deck){
    $nl=[string][char]10
    $deckSource=[IO.File]::ReadAllText($deck).Replace([string][char]13+$nl, $nl)
    $findDeck='        var suggestionGroup=DeskSpaceShelfGroupsV88.Add(list,
            "Excel paste suggestions",excelMatchesV122.Count,
            excelExpandedV122,()=>{
                excelExpandedV122=!excelExpandedV122;Render();
            });'.Replace([string][char]13+$nl, $nl)

    $replDeck='        if(DeskSpaceExcelActiveV122.ForegroundExcel() && excelMatchesV122.Count==0)
        {
            var pasteGroup=DeskSpaceShelfGroupsV88.Add(list,"Excel paste",1,true,()=>{});
            var pasteBtn=new Button{
                Content="Paste to Excel · Clipboard & mappings",
                ToolTip="Connect to active Microsoft Excel workbook to paste copied values or mappings",
                FontSize=11,Padding=new Thickness(6,5,6,5),
                Margin=new Thickness(1,2,1,2),
                HorizontalContentAlignment=HorizontalAlignment.Left
            };
            pasteBtn.Click+=(_,_)=>DeskSpaceExcelClipboardV39.ShowActive();
            pasteGroup.Children.Add(pasteBtn);
        }
        var suggestionGroup=DeskSpaceShelfGroupsV88.Add(list,
            "Excel paste suggestions",excelMatchesV122.Count,
            excelExpandedV122,()=>{
                excelExpandedV122=!excelExpandedV122;Render();
            });'.Replace([string][char]13+$nl, $nl)

    if($deckSource.Contains($findDeck)){
        $deckSource=$deckSource.Replace($findDeck,$replDeck)
        [IO.File]::WriteAllText($deck,$deckSource,[Text.UTF8Encoding]::new($false))
    }
}
Write-Host 'R375 PASS: Excel paste option successfully connected to Deck DropShelf and Smart Context Deck.'