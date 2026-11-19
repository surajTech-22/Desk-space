using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

internal static class Program
{
    static readonly BindingFlags F=BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Static|BindingFlags.Instance;
    static void Check(bool ok,string label)
    {
        if(!ok)throw new InvalidOperationException("R341 image conversion: "+label);
    }
    [STAThread]static int Main()
    {
        string root=Path.Combine(Path.GetTempPath(),
            "DeskSpaceR341-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var input=Path.Combine(root,"input");
            var output=Path.Combine(root,"output");
            Directory.CreateDirectory(input);Directory.CreateDirectory(output);
            var png=Path.Combine(input,"sample.png");
            var pixels=new byte[4*4*4];
            for(int i=0;i<pixels.Length;i+=4)
            {
                pixels[i]=30;pixels[i+1]=80;pixels[i+2]=190;pixels[i+3]=255;
            }
            var bitmap=BitmapSource.Create(4,4,96,96,PixelFormats.Bgra32,
                null,pixels,16);
            var encoder=new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using(var file=File.Create(png))encoder.Save(file);
            var original=SHA256.HashData(File.ReadAllBytes(png));
            var asm=typeof(DeskSpace.WeatherReading).Assembly;
            var cls=asm.GetType("DeskSpace.DeskSpaceImageConvertV341",true)!;
            var preview=cls.GetMethod("Preview",F)!;
            var convert=cls.GetMethod("ConvertAsync",F)!;
            var sources=new string[]{png};
            object plan=preview.Invoke(null,new object[]{sources,output,"jpg"})!;
            int count=((IEnumerable)plan).Cast<object>().Count();
            Check(count==1,"preview did not include one planned output");
            var task=(Task<int>)convert.Invoke(null,new object[]{plan,CancellationToken.None})!;
            Check(task.GetAwaiter().GetResult()==1,"conversion did not finish");
            var jpg=Path.Combine(output,"sample_converted.jpg");
            Check(File.Exists(jpg)&&new FileInfo(jpg).Length>0,
                "JPG encoder output missing or empty");
            var decoder=BitmapDecoder.Create(new Uri(jpg),
                BitmapCreateOptions.None,BitmapCacheOption.OnLoad);
            Check(decoder.Frames[0].PixelWidth==4&&
                decoder.Frames[0].PixelHeight==4,
                "converted image dimensions changed unexpectedly");
            Check(File.Exists(png)&&File.ReadAllBytes(png).Length>0 &&
                SHA256.HashData(File.ReadAllBytes(png)).SequenceEqual(original),
                "source image modified by converter");
            bool collision=false;
            try{preview.Invoke(null,new object[]{sources,output,"jpg"});}
            catch(TargetInvocationException ex)when(ex.InnerException is IOException)
            {collision=true;}
            Check(collision,"existing JPG was not protected against overwrite");
            bool denied=false;
            try{preview.Invoke(null,new object[]{sources,output,"gif"});}
            catch(TargetInvocationException ex)when(ex.InnerException is ArgumentException)
            {denied=true;}
            Check(denied,"unsupported conversion output accepted");
            var planPng=preview.Invoke(null,new object[]{sources,output,"png"})!;
            using(var cts=new CancellationTokenSource())
            {
                cts.Cancel();
                var canceled=(Task<int>)convert.Invoke(null,new object[]{planPng,cts.Token})!;
                try{
                    canceled.GetAwaiter().GetResult();
                    throw new Exception("Cancellation did not stop conversion");
                }
                catch(OperationCanceledException){}
            }
            Check(!File.Exists(Path.Combine(output,"sample_converted.png")),
                "cancelled work unexpectedly published output");
            Check(!Directory.EnumerateFiles(output,"*.tmp").Any(),
                "temporary partially encoded output leaked");
            var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
            var win=(Window)cls.GetMethod("Create",F)!.Invoke(null,new object?[]{null})!;
            Check(win.Title.Contains("image conversion"),"manual WPF conversion UI absent");
            win.Close();app.Shutdown();
            var palette=asm.GetType("DeskSpace.DeskSpaceActionPaletteV197",true)!;
            var commands=((IEnumerable)palette.GetField("Commands",F)!
                .GetValue(null)!).Cast<object>().ToArray();
            Check(commands.Any(x=>string.Equals((string?)x.GetType().GetProperty("Id")!
                .GetValue(x),"imageconvert",StringComparison.Ordinal)),
                "conversion not accessible in Action Palette");
            Console.WriteLine("R341 PASS: preview, real PNG to JPG, source checksum unchanged, "+
                "no overwrite, canceled output not published, WPF action available.");
            return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
        finally{try{Directory.Delete(root,true);}catch(Exception){}}
    }
}
