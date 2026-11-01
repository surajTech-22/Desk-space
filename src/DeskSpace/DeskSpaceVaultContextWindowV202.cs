using System;
using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace DeskSpace;

// R202 UI is intentionally MANUAL. No browser inspection, password autofill,
// secret clipboard copy, hidden website detection or background unlock.
internal static class DeskSpaceVaultContextWindowV202
{
    internal static Window Create(Window owner)
    {
        var window=new Window
        {
            Title="DeskSpace vault site lookup",Owner=owner,
            Width=460,Height=420,MinWidth=380,MinHeight=350,
            WindowStartupLocation=WindowStartupLocation.CenterOwner,
            ShowInTaskbar=false,ResizeMode=ResizeMode.CanResize
        };
        var stack=new StackPanel{Margin=new Thickness(18)};
        var pane=new ScrollViewer
        {
            VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
            Content=stack
        };
        window.Content=pane;
        var intro=new TextBlock
        {
            Text="Manual HTTPS website matching. Only labels are shown after "+
                 "you unlock the vault. No passwords are filled or copied.",
            TextWrapping=TextWrapping.Wrap,
            Margin=new Thickness(0,0,0,12)
        };
        stack.Children.Add(intro);
        var vault=new DeskSpaceVaultV10();
        window.Closed+=(_,_)=>vault.Dispose();
        var notice=new TextBlock{Text=vault.IsInitialized?
            "Unlock your existing DeskSpace vault:":
            "No existing vault was found. This lookup will not create one.",
            TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,8)};
        stack.Children.Add(notice);
        var password=new PasswordBox
        {
            Margin=new Thickness(0,0,0,8),MinHeight=32,
            IsEnabled=vault.IsInitialized
        };
        AutomationProperties.SetName(password,"Vault master password");
        stack.Children.Add(password);
        var unlock=new Button
        {
            Content="Unlock for this window",MinHeight=34,
            IsEnabled=vault.IsInitialized,Margin=new Thickness(0,0,0,10)
        };
        stack.Children.Add(unlock);
        var website=new TextBox
        {
            Text="https://",MinHeight=34,
            Margin=new Thickness(0,0,0,8)
        };
        AutomationProperties.SetName(website,"Website HTTPS URL to match");
        stack.Children.Add(website);
        var lookup=new Button
        {
            Content="Find matching labels",MinHeight=34,IsEnabled=false,
            Margin=new Thickness(0,0,0,9)
        };
        stack.Children.Add(lookup);
        var results=new ListBox{MinHeight=105,MaxHeight=165};
        AutomationProperties.SetName(results,"Matching vault item labels");
        stack.Children.Add(results);
        unlock.Click+=(_,_)=>{
            if(!vault.IsInitialized || vault.IsUnlocked)return;
            try
            {
                vault.Unlock(password.Password);
                password.Clear();
                password.IsEnabled=false;
                unlock.IsEnabled=false;
                lookup.IsEnabled=true;
                notice.Text="Vault unlocked for this window only.";
            }
            catch(Exception ex)when(ex is CryptographicException or
                FormatException or ArgumentException or IOException or
                UnauthorizedAccessException)
            {
                password.Clear();
                notice.Text="Unlock unsuccessful: "+ex.GetType().Name;
            }
        };
        lookup.Click+=(_,_)=>{
            results.Items.Clear();
            foreach(var item in DeskSpaceVaultContextV202.Match(vault,website.Text))
                results.Items.Add(new ListBoxItem
                {
                    Content=item.Label,
                    ToolTip="Metadata match only; no password retrieved"
                });
            notice.Text=results.Items.Count==0?
                "No exact HTTPS hostname match. Nothing was copied.":
                $"{results.Items.Count} exact-site label(s); no passwords shown.";
        };
        return window;
    }
    internal static void Show(Window owner)
    {
        var window=Create(owner);
        window.Show();
        window.Activate();
    }
}
