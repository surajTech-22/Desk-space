$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$old=[IO.File]::ReadAllText("$root/DeskSpaceDesignV283.cs")
$panels=[IO.File]::ReadAllText("$root/WidgetPanels.cs")
$hover=[IO.File]::ReadAllText("$root/DeskSpaceStableHoverV93.cs")
$context=[IO.File]::ReadAllText("$root/DeskSpaceSmartContextDeckV280.cs")
foreach($token in @(
 'internal static void Button(Button button,bool primary=false,',
 'var template=new ControlTemplate(typeof(Button))')){
 if(!$old.Contains($token)){throw ('R284 modern design base absent: '+$token)}
}
if(!$panels.Contains('public sealed class WidgetOptionsWindow') -or
   !$panels.Contains('public sealed class AppPinsWindow') -or
   !$panels.Contains('public sealed class WidgetDetailsWindow')){
 throw 'R284 legacy widget panel baseline missing'
}
if(!$hover.Contains('internal static Border Add(StackPanel root,') -or
   !$hover.Contains('var windowCard=new Border{')){
 throw 'R284 Running Apps stationary-hover behavior unexpectedly changed'
}
if(!$context.Contains('internal static Border CreatePanel(') -or
   !$context.Contains('internal static void Stop()')){
 throw 'R284 must preserve Smart Context start/stop and audit'
}
Copy-Item 'r284/DeskSpaceDesignV284.cs' "$root/DeskSpaceDesignV283.cs" -Force
Copy-Item 'r284/WidgetPanelsV284.cs' "$root/WidgetPanels.cs" -Force
Copy-Item 'r284/DeskSpaceSmartContextDeckV284.cs' "$root/DeskSpaceSmartContextDeckV280.cs" -Force
Copy-Item 'r284/DeskSpaceStableHoverV284.cs' "$root/DeskSpaceStableHoverV93.cs" -Force
Write-Host 'R284: modernized widget options, pinned apps, detail panels, context cards, hover flyouts and keyboard focus.'
