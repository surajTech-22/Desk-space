$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Once([string]$from,[string]$to,[string]$why){
 $from=$from.Replace($crlf,$lf);$to=$to.Replace($crlf,$lf)
 $hits=([regex]::Matches($script:s,[regex]::Escape($from))).Count
 if($hits -ne 1){throw ("R190 "+$why+" expected one anchor, got "+$hits)}
 $script:s=$script:s.Replace($from,$to)
}
Once '            TestDocumentMetadataV189();' @'
            TestDocumentMetadataV189();
            TestMonitorDockV190();
'@ 'register native monitor dock regression'
$test=@'
    private static void TestMonitorDockV190()
    {
        var dock=Product.GetType("DeskSpace.DeskSpaceMonitorDockV190",true)!;
        var calc=dock.GetMethod("Calculate",
            BindingFlags.NonPublic|BindingFlags.Static)!;
        (System.Drawing.Point Point,string Side,int W,int H) Fit(
            System.Drawing.Rectangle owner,System.Drawing.Rectangle work,
            int width,int height)
        {
            var result=calc.Invoke(null,new object[]{owner,work,width,height,6})!;
            var type=result.GetType();
            int x=(int)type.GetProperty("X")!.GetValue(result)!;
            int y=(int)type.GetProperty("Y")!.GetValue(result)!;
            int w=(int)type.GetProperty("Width")!.GetValue(result)!;
            int h=(int)type.GetProperty("Height")!.GetValue(result)!;
            var side=type.GetProperty("Side")!.GetValue(result)!.ToString()!;
            Assert(x>=work.Left&&y>=work.Top&&x+w<=work.Right&&
                y+h<=work.Bottom,
                "R190 every dock must fit entirely in ITS assigned monitor");
            return (new System.Drawing.Point(x,y),side,w,h);
        }
        var first=new System.Drawing.Rectangle(0,0,1920,1040);
        var a=Fit(new System.Drawing.Rectangle(40,60,130,130),
            first,270,310);
        Assert(a.Side=="Below"&&a.Point.Y==196,
            "R190 primary dock must remain below the widget when space allows");
        var right=new System.Drawing.Rectangle(1920,0,1600,900);
        var b=Fit(new System.Drawing.Rectangle(3450,650,100,100),
            right,300,330);
        Assert(b.Side=="Above"&&b.Point.X>=1920,
            "R190 rightmost secondary dock must flip above, never cross seam");
        var negative=new System.Drawing.Rectangle(-1440,-180,1440,900);
        var c=Fit(new System.Drawing.Rectangle(-1350,300,130,130),
            negative,240,400);
        Assert(c.Side=="Above"&&c.Point.X<0,
            "R190 left-side monitor must retain negative virtual coordinates");
        // Different display sizes and 150% DPI (300 DIP dock -> 450 px).
        var mixed=Fit(new System.Drawing.Rectangle(2430,500,140,140),
            new System.Drawing.Rectangle(1920,0,1100,740),450,520);
        Assert(mixed.Point.X>=1920,
            "R190 popup size at 150% DPI must be measured in native pixels");
        var tiny=Fit(new System.Drawing.Rectangle(120,80,110,100),
            new System.Drawing.Rectangle(0,0,350,260),250,220);
        Assert(tiny.Side=="Contained"&&tiny.W==250,
            "R190 compact screen must keep the independently scrollable dock contained");

        var attach=dock.GetMethod("Attach",
            BindingFlags.NonPublic|BindingFlags.Static)!;
        var popupFence=Product.GetType("DeskSpace.DeskSpaceMonitorFenceV153",true)!;
        var track=popupFence.GetMethod("TrackPopup",
            BindingFlags.NonPublic|BindingFlags.Static)!;
        var popupBounds=popupFence.GetMethod("PopupBounds",
            BindingFlags.NonPublic|BindingFlags.Static)!;
        var actual=System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea;
        var one=new Window{WindowStartupLocation=WindowStartupLocation.Manual,
            Left=actual.Left+25,Top=actual.Top+30,Width=125,Height=125,
            ShowInTaskbar=false,WindowStyle=WindowStyle.None};
        var two=new Window{WindowStartupLocation=WindowStartupLocation.Manual,
            Left=actual.Right-145,Top=actual.Bottom-155,Width=125,Height=125,
            ShowInTaskbar=false,WindowStyle=WindowStyle.None};
        System.Windows.Controls.Primitives.Popup Build(Window host,
            out ScrollViewer scroll)
        {
            var list=new StackPanel();
            for(int n=0;n<45;n++)
                list.Children.Add(new TextBlock{Text="Held file "+n,
                    Height=24,Foreground=Brushes.White});
            var frame=new Border{Child=list,MinWidth=180,MaxWidth=310};
            var floating=new System.Windows.Controls.Primitives.Popup{
                PlacementTarget=host,Placement=
                    System.Windows.Controls.Primitives.PlacementMode.Bottom,
                StaysOpen=true,AllowsTransparency=true,Child=frame};
            attach.Invoke(null,new object[]{host,floating,frame});
            track.Invoke(null,new object[]{host,floating});
            scroll=(ScrollViewer)frame.Child;
            Assert(scroll.Content==list&&
                scroll.VerticalScrollBarVisibility==ScrollBarVisibility.Auto &&
                floating.Placement==
                    System.Windows.Controls.Primitives.PlacementMode.Custom,
                "R190 independent native dock must retain file rows inside scroll viewer");
            return floating;
        }
        System.Windows.Controls.Primitives.Popup? popupA=null,popupB=null;
        try
        {
            one.Show();two.Show();
            // The advanced real Deck already contains a ScrollViewer on
            // some generations. Never discard/reparent its content.
            var savedList=new StackPanel();
            var oldScroll=new ScrollViewer{Content=savedList};
            var oldFrame=new Border{Child=oldScroll};
            var reusePopup=new System.Windows.Controls.Primitives.Popup{
                PlacementTarget=one,Child=oldFrame};
            attach.Invoke(null,new object[]{one,reusePopup,oldFrame});
            Assert(ReferenceEquals(oldFrame.Child,oldScroll)&&
                ReferenceEquals(oldScroll.Content,savedList),
                "R190 existing advanced Deck scroll controls must not be replaced");
            // Grouped Decks contain a Grid rather than a bare StackPanel.
            var advanced=new Grid();
            var advancedFrame=new Border{Child=advanced};
            var advancedPopup=new System.Windows.Controls.Primitives.Popup{
                PlacementTarget=two,Child=advancedFrame};
            attach.Invoke(null,new object[]{two,advancedPopup,advancedFrame});
            Assert(advancedFrame.Child is ScrollViewer wrapper &&
                ReferenceEquals(wrapper.Content,advanced),
                "R190 grouped advanced Deck must preserve its complete visual tree");
            popupA=Build(one,out var scrollA);
            popupB=Build(two,out var scrollB);
            Assert(!ReferenceEquals(scrollA,scrollB),
                "R190 each monitor must have its OWN deck scrolling state");
            popupA.IsOpen=true;popupB.IsOpen=true;
            one.Dispatcher.Invoke(()=>{},
                System.Windows.Threading.DispatcherPriority.ContextIdle);
            var nativeA=popupBounds.Invoke(null,new object[]{popupA});
            var nativeB=popupBounds.Invoke(null,new object[]{popupB});
            Assert(nativeA is System.Drawing.Rectangle &&
                nativeB is System.Drawing.Rectangle,
                "R190 both dock popups must have independent native HWNDs");
            foreach(var native in new[]{(System.Drawing.Rectangle)nativeA!,
                                      (System.Drawing.Rectangle)nativeB!})
                Assert(native.Left>=actual.Left&&native.Top>=actual.Top &&
                    native.Right<=actual.Right&&native.Bottom<=actual.Bottom,
                    "R190 live WPF dock popup must remain inside its monitor bounds");
            Assert(scrollA.MaxHeight>0&&scrollB.MaxHeight>0 &&
                scrollA.MaxHeight<=520&&scrollB.MaxHeight<=520,
                "R190 dock must scroll when longer than the monitor work area");
            Console.WriteLine("R190 PASS: physical WPF dock HWNDs, independent "+
                "scroll views, edge flips, negative monitor, 150% DPI and small screens.");
        }
        finally
        {
            if(popupA!=null)popupA.IsOpen=false;
            if(popupB!=null)popupB.IsOpen=false;
            one.Close();two.Close();
        }
    }

'@
Once '    private static void TestIndependentMonitorsV154()' (
  $test.Replace($crlf,$lf)+'    private static void TestIndependentMonitorsV154()'
) 'add real HWND and synthetic monitor bounds regression'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R190 native WPF two dock popups and monitor geometry regression added.'
