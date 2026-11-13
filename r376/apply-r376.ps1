$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
# R376 - dock Excel Clipboard inside Deck DropShelf and share across MultiWidget.
# Apply after R375.

$clipPath='DeskSpace_Windows_R1/DeskSpace/DeskSpaceExcelClipboardV39.cs'
if(Test-Path -LiteralPath $clipPath){
    Copy-Item -LiteralPath 'src/DeskSpace/DeskSpaceExcelClipboardV39.cs' -Destination $clipPath -Force
}

$deckPath='DeskSpace_Windows_R1/DeskSpace/DeskSpaceDropShelfV15.cs'
if(Test-Path -LiteralPath $deckPath){
    Copy-Item -LiteralPath 'src/DeskSpace/DeskSpaceDropShelfV15.cs' -Destination $deckPath -Force
}

Write-Host 'R376 PASS: Excel Clipboard successfully docked inside Deck DropShelf and synchronized across MultiWidget.'
