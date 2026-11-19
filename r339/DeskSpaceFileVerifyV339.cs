using System;
using System.Buffers;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace DeskSpace;

// R339-04 — manual, read-only local SHA-256, never sends file data to
// a network endpoint and never alters the selected file.
internal static class DeskSpaceFileVerifyV339
{
    internal static async Task<string> HashFileAsync(string filename,
        CancellationToken cancel=default,IProgress<long>? progress=null)
    {
        if(!File.Exists(filename))throw new FileNotFoundException("File not found.",filename);
        byte[] rented=ArrayPool<byte>.Shared.Rent(128*1024);
        using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        try
        {
            await using var stream=new FileStream(filename,FileMode.Open,FileAccess.Read,
                FileShare.Read,128*1024,FileOptions.SequentialScan|FileOptions.Asynchronous);
            int count;long total=0;
            while((count=await stream.ReadAsync(rented.AsMemory(0,128*1024),cancel))>0)
            {
                cancel.ThrowIfCancellationRequested();
                hash.AppendData(rented,0,count);
                total+=count;
                progress?.Report(total);
            }
            return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        }
        finally
        {
            Array.Clear(rented,0,rented.Length);
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    internal static bool Verify(string actual,string? expected)
    {
        string text=(expected??"").Trim().Replace(" ","");
        if(text.Length!=64||!System.Text.RegularExpressions.Regex.IsMatch(text,
            @"\A[0-9a-fA-F]{64}\z"))
            throw new ArgumentException("Expected SHA-256 must be 64 hexadecimal characters.");
        return CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(actual),Convert.FromHexString(text));
    }

    internal static Window Create(Window? owner=null)
    {
        var win=new Window{Title="DeskSpace · SHA-256 file verification",
            Width=670,Height=330,MinWidth=560,MinHeight=300,
            WindowStartupLocation=owner!=null&&owner.IsVisible
                ?WindowStartupLocation.CenterOwner:WindowStartupLocation.CenterScreen,
            Background=System.Windows.Media.Brushes.White,ShowInTaskbar=false};
        if(owner!=null&&owner.IsVisible)win.Owner=owner;
        var wrap=new StackPanel{Margin=new Thickness(18)};
        win.Content=wrap;
        wrap.Children.Add(new TextBlock{Text="Verify a local file",FontSize=20,
            FontWeight=FontWeights.Bold,Margin=new Thickness(0,0,0,6)});
        wrap.Children.Add(new TextBlock{Text="Choose a file. DeskSpace reads it only to calculate SHA-256; nothing is uploaded or changed.",
            TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,10)});
        var path=new TextBox{IsReadOnly=true,MinHeight=30,
            ToolTip="Selected local file"};wrap.Children.Add(path);
        var select=new Button{Content="Choose file",Margin=new Thickness(0,6,0,9),
            HorizontalAlignment=HorizontalAlignment.Left,
            Padding=new Thickness(12,6,12,6)};
        wrap.Children.Add(select);
        wrap.Children.Add(new TextBlock{Text="Actual SHA-256"});
        var result=new TextBox{IsReadOnly=true,MinHeight=29,Margin=new Thickness(0,3,0,9)};
        wrap.Children.Add(result);
        wrap.Children.Add(new TextBlock{Text="Expected SHA-256 (optional)"});
        var expected=new TextBox{MinHeight=29,Margin=new Thickness(0,3,0,8),
            MaxLength=80};
        wrap.Children.Add(expected);
        var actions=new StackPanel{Orientation=Orientation.Horizontal};
        var calculate=new Button{Content="Calculate",Padding=new Thickness(13,6,13,6),
            Margin=new Thickness(0,0,9,0)};
        var cancel=new Button{Content="Cancel",Padding=new Thickness(13,6,13,6),
            Margin=new Thickness(0,0,9,0),IsEnabled=false};
        var copy=new Button{Content="Copy hash",Padding=new Thickness(13,6,13,6),
            Margin=new Thickness(0,0,9,0),IsEnabled=false};
        var status=new TextBlock{Text="Ready",VerticalAlignment=VerticalAlignment.Center};
        actions.Children.Add(calculate);actions.Children.Add(cancel);
        actions.Children.Add(copy);actions.Children.Add(status);
        wrap.Children.Add(actions);
        CancellationTokenSource? pending=null;
        bool busy=false;
        void CancelCurrent(){pending?.Cancel();}
        select.Click+=(_,_)=>{
            if(busy)return;
            var dialog=new OpenFileDialog{Title="Choose file to verify",
                CheckFileExists=true,Multiselect=false};
            if(dialog.ShowDialog(win)==true){
                path.Text=dialog.FileName;result.Clear();copy.IsEnabled=false;
                status.Text="File selected";
            }
        };
        calculate.Click+=async (_,_)=>{
            if(busy||!File.Exists(path.Text)){status.Text="Choose an existing file.";return;}
            busy=true;calculate.IsEnabled=false;select.IsEnabled=false;
            cancel.IsEnabled=true;result.Clear();copy.IsEnabled=false;
            pending=new CancellationTokenSource();
            var ct=pending.Token;
            try{
                status.Text="Calculating locally…";
                var value=await HashFileAsync(path.Text,ct);
                if(ct.IsCancellationRequested)return;
                result.Text=value;copy.IsEnabled=true;
                status.Text=string.IsNullOrWhiteSpace(expected.Text)
                    ?"SHA-256 calculated":Verify(value,expected.Text)
                        ?"PASS — hashes match":"MISMATCH — hashes differ";
            }
            catch(OperationCanceledException){status.Text="Cancelled";}
            catch(Exception ex)when(ex is IOException or UnauthorizedAccessException
                or ArgumentException or System.Security.SecurityException){
                status.Text=ex.Message;
            }
            finally{
                busy=false;calculate.IsEnabled=true;select.IsEnabled=true;
                cancel.IsEnabled=false;pending?.Dispose();pending=null;
            }
        };
        cancel.Click+=(_,_)=>CancelCurrent();
        copy.Click+=(_,_)=>{
            if(result.Text.Length==64)Clipboard.SetText(result.Text);
        };
        win.Closed+=(_,_)=>CancelCurrent();
        return win;
    }
    internal static void Show(Window owner)=>Create(owner).Show();
}
