$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$hover=[IO.File]::ReadAllText("$root/DeskSpaceStableHoverV93.cs")
$view=[IO.File]::ReadAllText("$root/DeskSpaceExcelSheetViewV101.cs")
$editor=[IO.File]::ReadAllText("$root/DeskSpaceExcelEditorV42.cs")
$smoke=[IO.File]::ReadAllText('r36/UiSmoke/Program.cs')
foreach($phrase in @('OrderAppNamesV115','ChooseFourWayPlacement',
   'WindowFlyoutFourWay','WindowDirectionV115.Up','WindowDirectionV115.Down',
   'OrderIconTiles(false,windows)','OrderIconTiles(PreferOutwardRight(),live)',
   'CustomPopupPlacementCallback')){
 if(!$hover.Contains($phrase)){throw "R115 missing running-app rule: $phrase"}
}
foreach($phrase in @('grid.LayoutTransform=new ScaleTransform(1.0,1.0)',
   'MinHeight=240','HorizontalScrollBarVisibility=ScrollBarVisibility.Auto')){
 if(!$view.Contains($phrase)){throw "R115 missing native-sheet preview behavior $phrase"}
}
foreach($bad in @('Sheet zoom','zoom.SelectionChanged','zoom.SelectedIndex')){
 if($view.Contains($bad)){throw "R115 unnecessary zoom UI remains: $bad"}
}
foreach($phrase in @('collectionAndData.Children.Add(collectionColumn)',
    'collectionAndData.Children.Add(dataColumn)',
    'editorHead.Children.Add(collectionAndData)',
    'mappingPane.Children.Add(footer)',
    'Content=previewPane',
    'new GridLength(168)')){
 if(!$editor.Contains($phrase)){throw "R115 missing compact Excel editor $phrase"}
}
foreach($phrase in @('TestRunningAppsFourWayV115();',
    'TestRunningAppsFourWayV115()',
    'R115 all single-window apps must be alphabetical',
    'R115 above grid should be available',
    'R115 below grid should be available')){
 if(!$smoke.Contains($phrase)){throw "R115 missing WPF regression $phrase"}
}
Write-Host 'R115 PASS: window-count edge ordering, four-direction menus, fixed 100% preview, horizontal name fields.'
