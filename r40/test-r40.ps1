$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
./r40/apply-r40.ps1
$root='DeskSpace_Windows_R1/DeskSpace'
$xaml=[IO.File]::ReadAllText("$root/MainWindow.xaml")
$main=[IO.File]::ReadAllText("$root/MainWindow.xaml.cs")
$excel=[IO.File]::ReadAllText("$root/DeskSpaceExcelV16.cs")
$shelf=[IO.File]::ReadAllText("$root/DeskSpaceExcelClipboardV39.cs")
foreach($word in @(
 'x:Name="ExcelMainTab" Tag="Excel" Header="▦  Excel Mapping"',
 'x:Name="ExcelMainHost"',
 'Loaded="ExcelMainHost_Loaded"',
 'Content="▦  Excel Mapping" Click="ExcelModule_Click"',
 'Content="Show Live Donut Widget" Click="ShowWidget_Click"',
 'TabControl x:Name="MainTabs"'))
{if(!$xaml.Contains($word)){throw "R40 Excel workspace navigation missing: $word"}}
foreach($word in @(
 'private DeskSpaceExcelV16? workspaceExcel;',
 'workspaceExcel=new DeskSpaceExcelV16(',
 'ExcelMainHost.Children.Add(workspaceExcel.Create());',
 'MainTabs.SelectedItem=ExcelMainTab;',
 'inMainWorkspace:true'))
{if(!$main.Contains($word)){throw "R40 main Excel mapping not embedded: $word"}}
if($main.Contains('private void ExcelModule_Click(object sender,RoutedEventArgs e)'+
[Environment]::NewLine+'    {'+[Environment]::NewLine+
'        WidgetHostLauncher.OpenExcel();'))
{throw 'R40 old Excel button still routes to floating widget rather than main Workspace'}
foreach($word in @(
 'public DeskSpaceExcelV16(Action exit):this(exit,false)',
 'public DeskSpaceExcelV16(Action exit,bool inMainWorkspace)',
 'inWorkspace?"← Workspace":"← Circle"',
 'Upload screenshot + OCR',
 'Verify screenshot against selected Excel range',
 'Copy and analyze selected Excel cells',
 'private void SaveCheckedCells()'))
{if(!$excel.Contains($word)){throw "R40 existing Excel review broke: $word"}}
foreach($word in @(
 'private void HideShelf()',
 'popup.IsOpen=false; // Hide only.',
 'private void DragStart(',
 'private void DragMove(',
 'private void DragEnd(',
 'dragStart=e.GetPosition(host)',
 'customPosition=true;',
 'title.MouseLeftButtonDown+=DragStart;',
 'title.MouseMove+=DragMove;',
 'title.MouseLeftButtonUp+=DragEnd;',
 'close.ToolTip="Close Excel clipboard (copied cells remain available)"',
 'collapse.ToolTip=collapsed?',
 'active.userHidden=false;',
 'popup.IsOpen=!userHidden && host.IsVisible;'))
{if(!$shelf.Contains($word)){throw "R40 Excel Clipboard interaction missing: $word"}}
if(!(Test-Path "$root/DeskSpaceDropShelfV15.cs")){throw 'R40 original files shelf lost'}
if(!(Test-Path "$root/DeskSpaceVaultV10.cs")){throw 'R40 existing encrypted vault lost'}
if(!(Test-Path "$root/DeskSpaceUpdater.cs")){throw 'R40 authenticated updater lost'}
Write-Host 'R40 in-workspace Excel mapping, independently draggable/closable Excel clipboard, original data vault and updater source gates PASS'

# Extend the real Windows WPF UI suite. The same original engine must render
# inside both main workspace and donut (not an unlinked mockup).
$testPath='r36/UiSmoke/Program.cs'
$smoke=[IO.File]::ReadAllText($testPath)
$anchor='            TestExcelModule();'
if(!$smoke.Contains($anchor)){throw 'R40 cannot find WPF Excel smoke insertion point'}
$smoke=$smoke.Replace($anchor,$anchor+[string][char]10+'            TestExcelWorkspaceAndShelf();')
$anchor='    private static void TestExcelModule()'
if(!$smoke.Contains($anchor)){throw 'R40 cannot locate WPF Excel method'}
$method=@'
    private static void TestExcelWorkspaceAndShelf()
    {
        int returned=0;
        var engine=Make("DeskSpace.DeskSpaceExcelV16",
            (Action)(()=>returned++),true);
        var surface=(FrameworkElement)(Call(engine,"Create")
            ??throw new Exception("Main Excel workspace has no WPF view"));
        Render(surface,810,690,"excel-main-workspace");
        ButtonWith(surface,"← Workspace").RaiseEvent(
            new RoutedEventArgs(Button.ClickEvent));
        Assert(returned==1,"Main workspace Excel back did not return Home");
        foreach(var title in new[]{
            "Upload screenshot + OCR",
            "Copy and analyze selected Excel cells",
            "Verify screenshot against selected Excel range",
            "Save checked values",
            "Preview matching cells"
        })ButtonWith(surface,title);
        ((IDisposable)engine).Dispose();
        var host=new Window { Width=100,Height=100,
            WindowStyle=WindowStyle.None,ShowInTaskbar=false};
        var shelf=Make("DeskSpace.DeskSpaceExcelClipboardV39",host);
        try
        {
            var show=shelf.GetType().GetMethod("ShowActive",
                BindingFlags.Public|BindingFlags.Static)!;
            show.Invoke(null,null);
            var list=Private<StackPanel>(shelf,"list");
            var close=ButtonWith(list,"×");
            ButtonWith(list,"−");
            close.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert(Private<bool>(shelf,"userHidden"),
                "Close must hide Excel clipboard without disposing copies");
            show.Invoke(null,null);
            Assert(!Private<bool>(shelf,"userHidden"),
                "Reopening clipboard must restore without losing copied cells");
        }
        finally
        {
            ((IDisposable)shelf).Dispose();
            host.Close();
        }
    }
'@
$smoke=$smoke.Replace($anchor,$method+[string][char]10+$anchor)
[IO.File]::WriteAllText($testPath,$smoke,[Text.UTF8Encoding]::new($false))
Write-Host 'R40 real WPF tests installed for main Excel workspace, back navigation, clipboard Close and reopen.'
