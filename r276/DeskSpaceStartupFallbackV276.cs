using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Forms=System.Windows.Forms;

namespace DeskSpace;

// Minimal UI when WidgetShellWindow cannot be constructed or shown.
// This avoids showing an empty desktop/tray without an explanation.
// Never reads/deletes/repairs Workspace files or starts another widget.
internal static class DeskSpaceStartupFallbackV276
{
    internal static Window Create(string reason)
    {
        var message=string.IsNullOrWhiteSpace(reason)?
            "Unknown startup error":reason;
        var description=new TextBlock{
            Text="The DeskSpace widget could not start ("+message+"). "+
                "Your notes, files, profiles, Excel mappings, and settings "+
                "have not been changed by this recovery window. "+
                "Open Workspace to inspect Component Status, or close "+
                "this window. Subsequent short failed starts activate "+
                "single-widget recovery mode.",
            TextWrapping=TextWrapping.Wrap,FontSize=13,
            Margin=new Thickness(0,7,0,13)};
        var status=new TextBlock{Text="Nothing has been changed.",
            TextWrapping=TextWrapping.Wrap,
            Foreground=Brushes.DimGray};
        var open=new Button{Content="Open DeskSpace Workspace",
            Padding=new Thickness(12,8,12,8),
            Margin=new Thickness(0,0,9,6)};
        AutomationProperties.SetName(open,
            "Open Workspace for startup recovery");
        var close=new Button{Content="Close recovery window",
            Padding=new Thickness(12,8,12,8),
            Margin=new Thickness(0,0,0,6)};
        var actions=new WrapPanel();
        actions.Children.Add(open);actions.Children.Add(close);
        var content=new StackPanel{Margin=new Thickness(22)};
        content.Children.Add(new TextBlock{
            Text="DeskSpace startup recovery",FontSize=22,
            FontWeight=FontWeights.SemiBold});
        content.Children.Add(description);
        content.Children.Add(actions);
        content.Children.Add(status);
        var window=new Window{
            Title="DeskSpace — Widget recovery",
            Width=540,Height=330,MinWidth=380,MinHeight=270,
            ShowInTaskbar=true,
            WindowStartupLocation=WindowStartupLocation.CenterScreen,
            Content=new ScrollViewer{Content=content,
                VerticalScrollBarVisibility=ScrollBarVisibility.Auto}
        };
        open.Click+=(_,_)=>{
            try
            {
                string? exe=Environment.ProcessPath;
                if(string.IsNullOrWhiteSpace(exe)||!File.Exists(exe))
                {status.Text="Installed DeskSpace executable not found.";return;}
                // User-requested Workspace, NOT --widget-host, so this
                // cannot create a second broken widget.
                Process.Start(new ProcessStartInfo(exe){
                    UseShellExecute=false
                });
                status.Text="Workspace launch requested.";
            }
            catch(Exception ex) when(ex is IOException or
                UnauthorizedAccessException or InvalidOperationException or
                System.ComponentModel.Win32Exception)
            {status.Text="Unable to open Workspace ("+ex.GetType().Name+").";}
        };
        close.Click+=(_,_)=>window.Close();
        return window;
    }

    internal static void Show(string reason)
    {
        var window=Create(reason);
        // The fallback itself owns the only tray icon for a widget-host
        // that failed construction; it never relaunches itself.
        Forms.NotifyIcon? icon=null;
        try
        {
            icon=new Forms.NotifyIcon{
                Icon=System.Drawing.SystemIcons.Warning,
                Text="DeskSpace recovery",
                Visible=true
            };
            var menu=new Forms.ContextMenuStrip();
            menu.Items.Add("Show recovery",null,(_,_)=>window.Dispatcher.BeginInvoke(
                new Action(()=>{window.Show();window.Activate();})));
            menu.Items.Add("Exit",null,(_,_)=>window.Dispatcher.BeginInvoke(
                new Action(window.Close)));
            icon.ContextMenuStrip=menu;
            icon.DoubleClick+=(_,_)=>window.Dispatcher.BeginInvoke(
                new Action(()=>{window.Show();window.Activate();}));
            window.Closed+=(_,_)=>{
                icon.Visible=false;
                icon.Dispose();
                menu.Dispose();
            };
        }
        catch(Exception ex) when(ex is InvalidOperationException or
            System.ComponentModel.Win32Exception or
            System.Runtime.InteropServices.ExternalException)
        {
            icon?.Dispose(); // recovery window still stays usable
        }
        Application.Current.MainWindow=window;
        window.Show();
    }
}
