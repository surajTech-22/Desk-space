using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace DeskSpace;

// Non-modal in-app release notice. Windows notification settings can hide
// NotifyIcon balloons; this visible WPF notice does not depend on Action Center.
// This is called ONLY after official GitHub metadata has been checked, not
// from untrusted ntfy messages.
internal static class DeskSpaceReleaseNoticeV137
{
    private static string lastNotice="";
    internal static void Show(string channel,string tag)
    {
        if(lastNotice==channel+"|"+tag)return;
        lastNotice=channel+"|"+tag;
        try
        {
            var area=SystemParameters.WorkArea;
            var window=new Window {
                Width=350,Height=98,
                WindowStyle=WindowStyle.None,
                ResizeMode=ResizeMode.NoResize,
                AllowsTransparency=true,
                Background=Brushes.Transparent,
                Topmost=true,ShowInTaskbar=false,ShowActivated=false,
                Left=Math.Max(area.Left,area.Right-370),
                Top=Math.Max(area.Top,area.Bottom-123),
                Focusable=false,
                Title="DeskSpace verified update"
            };
            var border=new Border {
                Background=new SolidColorBrush(Color.FromRgb(30,45,70)),
                BorderBrush=new SolidColorBrush(Color.FromRgb(93,156,213)),
                BorderThickness=new Thickness(1),
                CornerRadius=new CornerRadius(9),
                Padding=new Thickness(14,10,12,10)
            };
            var stack=new StackPanel();
            stack.Children.Add(new TextBlock {
                Text="DeskSpace update available — "+tag,
                FontWeight=FontWeights.SemiBold,
                FontSize=14,Foreground=Brushes.White,
                TextWrapping=TextWrapping.Wrap
            });
            stack.Children.Add(new TextBlock {
                Text="Verified "+channel+" release. Automatic installation is starting.",
                FontSize=11,
                Margin=new Thickness(0,5,0,0),
                TextWrapping=TextWrapping.Wrap,
                Foreground=Brushes.LightSteelBlue
            });
            border.Child=stack;
            window.Content=border;
            var timeout=new DispatcherTimer {Interval=TimeSpan.FromSeconds(18)};
            timeout.Tick+=(_,_)=>{
                timeout.Stop();
                try{window.Close();}catch(InvalidOperationException){}
            };
            window.Closed+=(_,_)=>timeout.Stop();
            window.Show();
            timeout.Start();
        }
        catch(Exception)
        {
            // UI failure must NEVER prevent verified update installation.
        }
    }
}
