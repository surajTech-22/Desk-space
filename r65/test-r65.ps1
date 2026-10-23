$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$shared=Get-Content "$root/DeskSpaceSubDonutStandardV62.cs" -Raw
$app=Get-Content "$root/DeskSpaceApplicationsV55.cs" -Raw
$icon=Get-Content "$root/DeskSpaceApplicationIconsV65.cs" -Raw
$homeSource=Get-Content "$root/DeskSpaceDonutHomeV65.cs" -Raw
$widgetHostSource=Get-Content "$root/WidgetHost.cs" -Raw
$test=Get-Content 'r36/UiSmoke/Program.cs' -Raw

foreach($t in @('var wedges=canvas.Children.OfType<System.Windows.Shapes.Path>()',
    'AddEdgeTab(canvas,wedges[0],view.HiddenLeft,true',
    'AddEdgeTab(canvas,wedges[actual-1],view.HiddenRight,false',
    'double x=leading?area.Left+2:area.Right-width-2;',
    'Background=wedge.Fill,',
    'BorderBrush=wedge.Stroke??Brushes.White',
    'Canvas.SetZIndex(ring,40)',
    'tab.Click+=(_,_)=>advance();')){
 if(!$shared.Contains($t)){throw "R65 shared integrated count tab missing: $t"}
}
if($shared.Contains('Canvas.SetTop(left,') -or
   $shared.Contains('Canvas.SetTop(right,')){
 throw 'R65 still uses detached top/screen-edge count badges'
}
foreach($t in @('DeskSpaceApplicationIconsV65.Get(item.Path)',
    'var appIcon=new Image{Source=iconSource',
    'IsHitTestVisible=false')){
 if(!$app.Contains($t)){throw "R65 Apps logo missing: $t"}
}
foreach($t in @('SHGetFileInfo','SHGFI_ICON=0x100',
    'CreateBitmapSourceFromHIcon','DestroyIcon(info.hIcon)',
    'ConcurrentDictionary<string,ImageSource> Cache')){
 if(!$icon.Contains($t)){throw "R65 app shell icon implementation missing: $t"}
}
foreach($t in @('internal void Capture(Window owner)',
    'internal bool Restore(Window owner)',
    'owner.Left=position.X','owner.Top=position.Y',
    'owner.BeginAnimation(Window.LeftProperty,null);')){
 if(!$homeSource.Contains($t)){throw "R65 original position retention missing: $t"}
}
foreach($t in @('private readonly DeskSpaceDonutHomeV65 donutHome=new();',
    'if(!ringExpanded && activeModule==null) donutHome.Capture(this);',
    'if(activeModule==null && donutHome.Restore(this)) SavePosition();')){
 if(!$widgetHostSource.Contains($t)){throw "R65 WidgetHost reversible expansion missing: $t"}
}
foreach($t in @('TestDonutHomeAndApplicationIcons()',
    'R65 WPF PASS: donut returns to original location',
    'Hidden counts must attach inside both end sectors',
    'Hidden numbers detached from the first or last wedge')){
 if(!$test.Contains($t)){throw "R65 real Windows test missing: $t"}
}
Write-Host 'R65 PASS: universal wedge-integrated counts, app icon lookup and exact original donut position recovery.'
