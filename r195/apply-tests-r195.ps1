$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Once([string]$from,[string]$to,[string]$why)
{
 $from=$from.Replace($crlf,$lf);$to=$to.Replace($crlf,$lf)
 $count=([regex]::Matches($script:s,[regex]::Escape($from))).Count
 if($count -ne 1){throw ("R195 "+$why+" expected one anchor, got "+$count)}
 $script:s=$script:s.Replace($from,$to)
}
Once '            TestComponentSignalsV194();' @'
            TestComponentSignalsV194();
            TestPerMonitorDpiV195();
'@ 'native DPI smoke call'
$method=@'
    private static void TestPerMonitorDpiV195()
    {
        var dpi=Product.GetType("DeskSpace.DeskSpaceDpiBoundsV195",true)!;
        var clean=dpi.GetMethod("CleanScale",
            BindingFlags.NonPublic|BindingFlags.Static)!;
        var pixels=dpi.GetMethod("Pixels",
            BindingFlags.NonPublic|BindingFlags.Static)!;
        double Clean(double v)=>(double)clean.Invoke(null,new object[]{v})!;
        int Pixel(double dip,double scale,int work)=>(int)pixels.Invoke(
            null,new object[]{dip,scale,work})!;
        Assert(Clean(double.NaN)==1 && Clean(double.PositiveInfinity)==1 &&
            Clean(-1)==1 && Clean(1.75)==1.75 && Clean(0.75)==0.75,
            "R195 invalid monitor DPI matrix must not reach native pixel geometry");
        Assert(Pixel(200,1.75,1000)==350 &&
            Pixel(250,1.5,350)==350 &&
            Pixel(double.NaN,1.25,1920)==1 &&
            Pixel(200,double.NaN,1920)==200 &&
            Pixel(double.MaxValue,2,400)==400,
            "R195 physical pixel conversion should handle 100-175 percent and extremes");

        var dock=Product.GetType("DeskSpace.DeskSpaceMonitorDockV190",true)!;
        var calc=dock.GetMethod("Calculate",
            BindingFlags.NonPublic|BindingFlags.Static)!;
        var monitors=new []{
            new System.Drawing.Rectangle(0,0,1920,1040),
            new System.Drawing.Rectangle(-2560,0,2560,1400),
            new System.Drawing.Rectangle(1920,-240,1366,728),
            new System.Drawing.Rectangle(-1280,-768,1280,720),
            new System.Drawing.Rectangle(0,0,860,520)
        };
        foreach(var work in monitors)
        {
            foreach(double scale in new[]{1d,1.25d,1.5d,1.75d})
            {
                foreach(var top in new[]{true,false})
                {
                    int x=work.Right-130;
                    int y=top?work.Top+12:work.Bottom-138;
                    var widget=new System.Drawing.Rectangle(x,y,115,115);
                    var result=calc.Invoke(null,new object[]{
                        widget,work,Pixel(300,scale,work.Width),
                        Pixel(350,scale,work.Height),6})!;
                    var type=result.GetType();
                    int left=(int)type.GetProperty("X")!.GetValue(result)!;
                    int upper=(int)type.GetProperty("Y")!.GetValue(result)!;
                    int w=(int)type.GetProperty("Width")!.GetValue(result)!;
                    int h=(int)type.GetProperty("Height")!.GetValue(result)!;
                    Assert(left>=work.Left&&upper>=work.Top&&
                        left+w<=work.Right&&upper+h<=work.Bottom,
                        "R195 every synthetic 100-175% dock must fit its OWN monitor work area");
                }
            }
        }
        Console.WriteLine("R195 PASS: 40 monitor/origin/DPI/window-edge placements, bounded conversions.");
    }

'@
Once '    private static void TestComponentSignalsV194()' (
 $method.Replace($crlf,$lf)+'    private static void TestComponentSignalsV194()'
) 'native mixed-DPI method'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R195: native DPI matrix covers multiple monitor origins, screen sizes and 100-175 percent.'
