$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$a='                TestExcelSuggestionRevalidationV263();'
if(([regex]::Matches($s,[regex]::Escape($a))).Count -ne 1){
 throw 'R264 test invocation missing R263 anchor'
}
$s=$s.Replace($a,$a+$lf+'                TestPinnedPickerMonitorV264();')
$anchor='    private static void TestPinnedRecentDocumentChoicesV262()'
$method=@'
    private static void TestPinnedPickerMonitorV264()
    {
        var type=Product.GetType("DeskSpace.DeskSpacePinnedRecentMonitorV264",true)!;
        var flags=BindingFlags.NonPublic|BindingFlags.Static;
        var centre=type.GetMethod("CentreOnOwner",flags)!;
        var clamp=type.GetMethod("ClampToMonitor",flags)!;
        System.Drawing.Point Center(System.Drawing.Rectangle popup,
            System.Drawing.Rectangle owner,System.Drawing.Rectangle work)=>
            (System.Drawing.Point)centre.Invoke(null,new object[]{popup,owner,work})!;
        System.Drawing.Point Fence(System.Drawing.Rectangle popup,
            System.Drawing.Rectangle work)=>
            (System.Drawing.Point)clamp.Invoke(null,new object[]{popup,work})!;
        var left=new System.Drawing.Rectangle(-1920,0,1920,1080);
        var right=new System.Drawing.Rectangle(0,0,1920,1080);
        var popup=new System.Drawing.Rectangle(0,0,570,495);
        var leftOwner=new System.Drawing.Rectangle(-100,900,80,80);
        var leftPos=Center(popup,leftOwner,left);
        Assert(leftPos.X>=left.Left &&
               leftPos.X+popup.Width<=left.Right &&
               leftPos.Y+popup.Height<=left.Bottom &&
               leftPos.X<0,
            "R264 negative-origin secondary monitor picker stays on that display");
        var rightOwner=new System.Drawing.Rectangle(1820,900,80,80);
        var rightPos=Center(popup,rightOwner,right);
        Assert(rightPos.X>=right.Left &&
               rightPos.X+popup.Width<=right.Right &&
               rightPos.Y+popup.Height<=right.Bottom,
            "R264 right-edge picker never crosses its owner monitor seam");
        var moved=Fence(new System.Drawing.Rectangle(1880,-40,570,495),right);
        Assert(moved.X==1350 && moved.Y==0,
            "R264 clamp blocks cross-monitor dragging and negative top overflow");
        var picker=Product.GetType("DeskSpace.DeskSpacePinnedRecentPickerV262",true)!;
        var owner=new Window{Width=160,Height=160,ShowInTaskbar=false};
        Window? dialog=null;
        try
        {
            owner.Show();
            dialog=(Window)picker.GetMethod("Create",flags)!
                .Invoke(null,new object[]{owner,
                    System.IO.Path.Combine(outdir,"missing-pinned.exe")})!;
            Assert(dialog.WindowStartupLocation==WindowStartupLocation.Manual,
                "R264 pinned picker must use native pixel placement, never CenterScreen");
            dialog.Show();dialog.UpdateLayout();
            Assert(dialog.IsVisible,
                "R264 owner-linked manually placed WPF picker renders");
        }
        finally{dialog?.Close();owner.Close();}
        Console.WriteLine("R264 PASS: origin monitor popup bounds, negative coordinates, "+
            "seam confinement and native WPF picker placement.");
    }

'@
if(([regex]::Matches($s,[regex]::Escape($anchor))).Count -ne 1){
 throw 'R264 native test method anchor missing'
}
$s=$s.Replace($anchor,$method.Replace($crlf,$lf)+$anchor)
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R264: negative origin/seam and real WPF owner-popup synthetic regression installed.'
