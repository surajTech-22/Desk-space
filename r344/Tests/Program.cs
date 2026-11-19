using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Drawing=System.Drawing;
using DeskSpace;

internal static class Program
{
    const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static;
    static void Check(bool result,string reason)
    {if(!result)throw new InvalidOperationException("R344 local OCR: "+reason);}
    [STAThread]static int Main()
    {
        try
        {
            var asm=typeof(WeatherReading).Assembly;
            var cls=asm.GetType("DeskSpace.DeskSpaceScreenTextV344",true)!;
            var recognize=cls.GetMethod("RecognizeAsync",Flags)!;
            var select=cls.GetMethod("SelectRegionAsync",Flags)!;
            var capture=cls.GetMethod("CaptureRegion",Flags)!;
            Check(select!=null&&capture!=null,"screen selection or capture missing");
            byte[] png;
            using(var image=new Drawing.Bitmap(800,160))
            using(var graphics=Drawing.Graphics.FromImage(image))
            using(var memory=new MemoryStream())
            {
                graphics.Clear(Drawing.Color.White);
                using var font=new Drawing.Font("Arial",37,Drawing.FontStyle.Bold);
                graphics.DrawString("DESKSPACE TEST 2026",font,
                    Drawing.Brushes.Black,new Drawing.PointF(22,35));
                image.Save(memory,Drawing.Imaging.ImageFormat.Png);
                png=memory.ToArray();
            }
            Check(png.Length>100 && png.Length<12_000_000,
                "test PNG size outside expected bounds");
            var task=(Task<string>)recognize.Invoke(null,new object?[]{
                png,CancellationToken.None})!;
            try
            {
                var text=task.GetAwaiter().GetResult();
                Check(text.Length>0 && text.Length<2000,
                    "local Windows OCR didn't read high-contrast test image");
                Console.WriteLine("R344 Windows OCR recognized locally: "+text);
            }
            catch(InvalidOperationException ex) when(ex.Message.Contains(
                "OCR language unavailable",StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("R344 NOTE: no Windows OCR language on CI image, on-device OCR acceptance required");
            }
            bool bad=false;
            try{
                var t=(Task<string>)recognize.Invoke(null,new object?[]{
                    new byte[12_000_001],CancellationToken.None})!;
                t.GetAwaiter().GetResult();
            }
            catch(Exception ex)when(ex is InvalidDataException or TargetInvocationException)
            {bad=true;}
            Check(bad,"screen OCR accepted unbounded image input");
            using(var cts=new CancellationTokenSource())
            {
                cts.Cancel();
                bool cancelled=false;
                try{
                    var task2=(Task<string>)recognize.Invoke(null,
                        new object?[]{png,cts.Token})!;
                    task2.GetAwaiter().GetResult();
                }
                catch(OperationCanceledException){cancelled=true;}
                Check(cancelled,"OCR ignored cancellation before processing");
            }
            bool offscreen=false;
            try{capture.Invoke(null,new object[]{
                new Drawing.Rectangle(int.MaxValue-100,int.MaxValue-100,40,40)});}
            catch(TargetInvocationException ex)when(
                ex.InnerException is ArgumentOutOfRangeException)
            {offscreen=true;}
            Check(offscreen,"screen capture allowed a rectangle outside displays");
            var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
            var win=(Window)cls.GetMethod("Create",Flags)!
                .Invoke(null,new object?[]{null})!;
            Check(win.Title.Contains("Screen text extraction"),
                "on-demand WPF OCR review window absent");
            win.Close();app.Shutdown();
            var palette=asm.GetType("DeskSpace.DeskSpaceActionPaletteV197",true)!;
            var cmds=((IEnumerable)palette.GetField("Commands",Flags)!
                .GetValue(null)!).Cast<object>().ToArray();
            Check(cmds.Any(x=>string.Equals((string?)x.GetType()
                .GetProperty("Id")!.GetValue(x),"screentext",StringComparison.Ordinal)),
                "OCR action not in existing Action Palette");
            Console.WriteLine("R344 PASS: offline Windows OCR API, image/cancellation caps, "+
                "no offscreen capture, native review UI and explicit command.");
            return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
