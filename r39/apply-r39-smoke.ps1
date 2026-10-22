$ErrorActionPreference='Stop'
$path='r36/UiSmoke/Program.cs'
$s=[IO.File]::ReadAllText($path).Replace([string][char]13+[char]10,[string][char]10)
$anchor='            TestMediaPill();'
if(!$s.Contains($anchor)){throw 'R39 WPF smoke main runner anchor missing'}
$s=$s.Replace($anchor,'            TestExcelModule();'+[string][char]10+$anchor)
$anchor='    private static void TestMediaPill()'
if(!$s.Contains($anchor)){throw 'R39 WPF media test anchor missing'}
$method=@'
    private static void TestExcelModule()
    {
        int back=0;
        var model=Make("DeskSpace.DeskSpaceExcelV16",
            (Action)(()=>back++));
        var view=(FrameworkElement)(Call(model,"Create")
            ??throw new Exception("Excel module has no WPF view"));
        Render(view,710,720,"excel-review");
        foreach(string label in new[]{
            "Upload screenshot + OCR",
            "Analyze marked image region",
            "Verify screenshot against selected Excel range",
            "Copy and analyze selected Excel cells",
            "Copy range to Excel-only shelf",
            "Save checked values",
            "Preview matching cells",
            "Apply autofill",
            "← Circle"
        })ButtonWith(view,label);
        ButtonWith(view,"← Circle").RaiseEvent(
            new RoutedEventArgs(Button.ClickEvent));
        Assert(back==1,"Excel did not return to Circle");
        ((IDisposable)model).Dispose();

        // The Excel clipboard is separate from the Files shelf and
        // must construct without requiring Excel to be installed on CI.
        var host=new Window{Width=116,Height=116,
            ShowInTaskbar=false,WindowStyle=WindowStyle.None};
        var shelf=(IDisposable)Make(
            "DeskSpace.DeskSpaceExcelClipboardV39",host);
        shelf.Dispose();
        host.Close();
    }
'@
$method=$method.Replace([string][char]13+[char]10,[string][char]10)
$s=$s.Replace($anchor,$method+[string][char]10+$anchor)
[IO.File]::WriteAllText($path,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R39 WPF Excel review and independent Excel clipboard smoke prepared.'
