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
    'AddDividerCount(canvas,wedges[0],view.HiddenLeft,true',
    'AddDividerCount(canvas,wedges[count-1],view.HiddenRight,false',
    'Point midpoint=DividerMidpoint(wedge.Data,leading);',
    'Canvas.SetLeft(button,midpoint.X-width/2);',
    'Canvas.SetTop(button,midpoint.Y-height/2);',
    'Content=count,',
    'BorderThickness=new Thickness(0)',
    'SplitDividerStroke(canvas,wedges[0],true,false);',
    'button.Click+=(_,_)=>move();')){
 if(!$shared.Contains($t)){throw "R66 vertical divider numeral missing: $t"}
}
if($shared.Contains('AddEdgeTab(') -or
   $shared.Contains('double x=leading?area.Left+2')){
 throw 'R66 must not render rectangle-bound badges instead of real divider counts'
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
    'Hidden counts must be numerals embedded in radial end lines',
    'Hidden count not centered on actual vertical/radial end line')){
 if(!$test.Contains($t)){throw "R65 real Windows test missing: $t"}
}
Write-Host 'R65 PASS: universal wedge-integrated counts, app icon lookup and exact original donut position recovery.'
