using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace DeskSpace;

// Popups live OUTSIDE the main deck StackPanel. Hover cannot resize
// or reposition the deck, even when an app has dozens of windows.
internal static class DeskSpaceStableHoverV93
{
    private static Popup? activeIconPopup;
    internal static bool IsHoverActive=>activeIconPopup?.IsOpen==true;

    // Pure filter shared by the UI timer and Windows smoke tests.
    // Do not remove windows merely because WM_CLOSE was requested:
    // the app may be displaying an unsaved-work confirmation.
    internal static IReadOnlyList<DeskSpaceRunningAppsV88.Entry> RemainingWindows(
        IReadOnlyList<DeskSpaceRunningAppsV88.Entry> windows,
        Func<IntPtr,bool> isAlive)=>
        windows.Where(x=>isAlive(x.Handle)).ToArray();

    // Required space checks are deliberately pure, so the side selection
    // is tested without relying on an active desktop session.
    internal static bool PreferRight(double roomLeft,double roomRight,
        double iconFraction,double neededWidth)
    {
        if(roomLeft>=neededWidth && roomRight>=neededWidth)
            return iconFraction>=0.5;
        if(roomRight>=neededWidth)return true;
        if(roomLeft>=neededWidth)return false;
        return roomRight>=roomLeft;
    }

    // Shared per-window close control. Separate from the switch button, so
    // clicking X never activates or minimises a window.
    internal static Button MakeCloseButton(DeskSpaceRunningAppsV88.Entry window)
    {
        var button=new Button{
            Content="×",
            Width=22,MinWidth=22,Height=23,MinHeight=23,
            FontSize=14,FontWeight=FontWeights.Medium,
            Foreground=Brushes.LightSteelBlue,
            Background=new SolidColorBrush(Color.FromRgb(45,61,94)),
            BorderThickness=new Thickness(0),
            Padding=new Thickness(0),
            VerticalAlignment=VerticalAlignment.Center,
            Margin=new Thickness(1,1,2,1),
            ToolTip="Close this window normally (unsaved work may prompt)"
        };
        button.Click+=(_,e)=>{
            e.Handled=true;
            if(DeskSpaceRunningAppsV88.RequestClose(window))
            {
                button.IsEnabled=false;
                button.ToolTip="Waiting for the application to close or ask to save";
                // A user can cancel an unsaved-work dialog. Do not leave
                // its close button permanently disabled in that case.
                var retry=new DispatcherTimer{
                    Interval=TimeSpan.FromSeconds(2)
                };
                retry.Tick+=(_,_)=>{
                    retry.Stop();
                    if(DeskSpaceRunningAppsV88.IsActiveWindow(window.Handle))
                    {
                        button.IsEnabled=true;
                        button.ToolTip="Close this window normally (unsaved work may prompt)";
                    }
                };
                retry.Start();
            }
        };
        return button;
    }

    internal static Border Add(StackPanel root,
        IReadOnlyList<DeskSpaceRunningAppsV88.Entry> apps,
        IReadOnlyList<DeskSpaceRunningAppsV88.Entry> windows,
        Action<IReadOnlyList<DeskSpaceRunningAppsV88.Entry>,
            IReadOnlyList<DeskSpaceRunningAppsV88.Entry>>? onLiveChanged=null,
        Action? onHoverClosed=null)
    {
        var heading=new Border{
            Background=new SolidColorBrush(Color.FromRgb(39,53,83)),
            Padding=new Thickness(7,5,6,5),Margin=new Thickness(1,2,1,2),
            CornerRadius=new CornerRadius(4),Cursor=Cursors.Hand
        };
        heading.Child=new TextBlock{
            Text="▸  Running Apps · "+apps.Count,FontSize=11,
            Foreground=Brushes.White,FontWeight=FontWeights.SemiBold
        };
        // No running apps means no category header; other shelf groups stay.
        if(apps.Count==0)return heading;
        var iconRow=new WrapPanel{Margin=new Thickness(3)};
        var appTiles=new Dictionary<string,FrameworkElement>(
            StringComparer.OrdinalIgnoreCase);
        var appBadges=new Dictionary<string,TextBlock>(
            StringComparer.OrdinalIgnoreCase);
        int liveCount=windows.Count;
        var iconCard=new Border{
            Background=new SolidColorBrush(Color.FromRgb(32,44,71)),
            BorderBrush=new SolidColorBrush(Color.FromRgb(71,85,121)),
            BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(7),
            Padding=new Thickness(5),
            Child=new ScrollViewer{
                MaxHeight=230,MaxWidth=250,
                HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
                Content=iconRow
            }
        };
        var iconPopup=new Popup{
            PlacementTarget=heading,Placement=PlacementMode.Right,
            HorizontalOffset=0,VerticalOffset=0,
            StaysOpen=true,AllowsTransparency=true,
            PopupAnimation=PopupAnimation.Fade,Child=iconCard
        };
        var windowItems=new StackPanel{MinWidth=180,MaxWidth=268};
        var windowCard=new Border{
            Background=new SolidColorBrush(Color.FromRgb(32,44,71)),
            BorderBrush=new SolidColorBrush(Color.FromRgb(71,85,121)),
            BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(7),
            Padding=new Thickness(5),
            Child=new ScrollViewer{
                MaxHeight=292,MaxWidth=280,
                HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
                Content=windowItems
            }
        };
        // Use the closest safe SIDE of the entire icon grid. Never
        // overlay any other application's hit target or force the
        // pointer through a second row of application buttons.
        var windowPopup=new Popup{
            PlacementTarget=iconCard,
            Placement=PlacementMode.Right,
            HorizontalOffset=4,VerticalOffset=0,
            StaysOpen=true,AllowsTransparency=true,
            PopupAnimation=PopupAnimation.None,Child=windowCard
        };
        iconCard.Tag=windowPopup;
        var hoverIntent=new DispatcherTimer{
            Interval=TimeSpan.FromMilliseconds(170)
        };
        var closeDelay=new DispatcherTimer{
            Interval=TimeSpan.FromMilliseconds(340)
        };
        var liveTimer=new DispatcherTimer{
            Interval=TimeSpan.FromMilliseconds(220)
        };
        void StopIntent(){hoverIntent.Stop();}
        // Both sides available: prefer the side of the hovered tile.
        // One side available: choose that side, never inside the grid.
        bool pendingRight=true;
        FrameworkElement? pendingIcon=null;
        DeskSpaceRunningAppsV88.Entry[] pendingWindows=Array.Empty<DeskSpaceRunningAppsV88.Entry>();
        string pendingName="";
        void OpenPending()
        {
            hoverIntent.Stop();
            var anchor=pendingIcon;
            if(anchor==null || !anchor.IsMouseOver ||
                !iconCard.IsMouseOver)return;
            KeepOpen();
            windowPopup.IsOpen=false;
            windowItems.Children.Clear();
            pendingWindows=pendingWindows.Where(x=>
                DeskSpaceRunningAppsV88.IsActiveWindow(x.Handle)).ToArray();
            if(pendingWindows.Length<=1)return;
            windowPopup.Placement= pendingRight ?
                PlacementMode.Right:PlacementMode.Left;
            windowPopup.HorizontalOffset=pendingRight?4:-4;
            windowPopup.VerticalOffset=0;
            // Align vertically near the selected tile; WPF keeps the
            // detached window popup inside the visible monitor.
            try
            {
                var top=anchor.TranslatePoint(new Point(0,0),iconCard).Y;
                windowPopup.VerticalOffset=Math.Max(0,top);
            }
            catch(InvalidOperationException){}
            windowItems.Children.Add(new TextBlock{
                Text=pendingName+" · "+pendingWindows.Length+" windows",
                FontSize=11,Foreground=Brushes.LightSteelBlue,
                FontWeight=FontWeights.Medium,
                Margin=new Thickness(6,3,6,6)
            });
            foreach(var window in pendingWindows)
            {
                var selected=window;
                var row=new DockPanel{LastChildFill=true,
                    Tag=selected.Handle,
                    Background=new SolidColorBrush(Color.FromRgb(45,61,94)),
                    Margin=new Thickness(2,1,2,1)};
                var close=MakeCloseButton(selected);
                DockPanel.SetDock(close,Dock.Right);
                row.Children.Add(close);
                var choice=new Button{
                    Content=selected.WindowTitle,FontSize=11,
                    Foreground=Brushes.White,
                    Background=Brushes.Transparent,
                    BorderThickness=new Thickness(0),
                    HorizontalContentAlignment=HorizontalAlignment.Left,
                    Padding=new Thickness(7,5,7,5),
                    MaxWidth=229,ToolTip=selected.WindowTitle
                };
                choice.Click+=(_,_)=>{
                    Hide();
                    DeskSpaceRunningAppsV88.Focus(selected);
                };
                row.Children.Add(choice);
                windowItems.Children.Add(row);
            }
            windowPopup.IsOpen=true;
        }
        hoverIntent.Tick+=(_,_)=>OpenPending();
        void Hide()
        {
            bool wasOpen=iconPopup.IsOpen;
            liveTimer.Stop();
            hoverIntent.Stop();
            closeDelay.Stop();
            windowPopup.IsOpen=false;
            iconPopup.IsOpen=false;
            if(ReferenceEquals(activeIconPopup,iconPopup))
                activeIconPopup=null;
            if(wasOpen)onHoverClosed?.Invoke();
        }

        // Reconcile only real HWNDs: a posted WM_CLOSE is not proof that
        // an application has closed. This is deliberately lightweight
        // (no Process.GetProcesses, I/O, or main-deck rerender).
        void Reconcile()
        {
            var live=RemainingWindows(windows,
                DeskSpaceRunningAppsV88.IsActiveWindow);
            if(live.Count==liveCount)return;
            liveCount=live.Count;
            var groups=DeskSpaceRunningAppsV88.GroupVisible(live);
            foreach(var app in apps)
            {
                var matches=DeskSpaceRunningAppsV88.WindowsFor(app,live);
                if(!appTiles.TryGetValue(app.Name,out var tile))continue;
                if(matches.Count==0)
                {
                    iconRow.Children.Remove(tile);
                    appTiles.Remove(app.Name);
                    appBadges.Remove(app.Name);
                }
                else if(appBadges.TryGetValue(app.Name,out var badge))
                    badge.Text=matches.Count>1?
                        matches.Count+" windows":"";
            }
            foreach(var row in windowItems.Children.OfType<DockPanel>().
                Where(x=>x.Tag is IntPtr hwnd &&
                    !live.Any(w=>w.Handle==hwnd)).ToArray())
                windowItems.Children.Remove(row);
            var activeMatches=live.Where(x=>string.Equals(
                x.Name,pendingName,StringComparison.OrdinalIgnoreCase)).
                ToArray();
            pendingWindows=activeMatches;
            if(windowItems.Children.OfType<TextBlock>().FirstOrDefault()
                is TextBlock counter)
                counter.Text=pendingName+" · "+activeMatches.Length+" windows";
            if(activeMatches.Length==0)windowPopup.IsOpen=false;
            if(heading.Child is TextBlock label)
                label.Text="▸  Running Apps · "+groups.Count;
            // Update the deck count without destroying the hovered popup.
            if(groups.Count==0)Hide();
            onLiveChanged?.Invoke(groups,live);
        }
        liveTimer.Tick+=(_,_)=>Reconcile();
        bool Hovered()=>heading.IsMouseOver ||
            iconCard.IsMouseOver || windowCard.IsMouseOver;
        closeDelay.Tick+=(_,_)=>{
            if(Hovered())closeDelay.Stop();
            else Hide();
        };
        void KeepOpen()=>closeDelay.Stop();
        void CloseLater(){closeDelay.Stop();closeDelay.Start();}
        heading.MouseEnter+=(_,_)=>{
            KeepOpen();
            iconPopup.IsOpen=true;
            activeIconPopup=iconPopup;
            Reconcile();
            if(iconPopup.IsOpen)liveTimer.Start();
        };
        heading.MouseLeave+=(_,_)=>CloseLater();
        iconCard.MouseEnter+=(_,_)=>KeepOpen();
        iconCard.MouseLeave+=(_,_)=>{StopIntent();CloseLater();};
        windowCard.MouseEnter+=(_,_)=>{StopIntent();KeepOpen();};
        windowCard.MouseLeave+=(_,_)=>CloseLater();

        foreach(var app in apps)
        {
            var matches=DeskSpaceRunningAppsV88.WindowsFor(app,windows).ToArray();
            if(matches.Length==0)matches=new[]{app};
            var picture=new StackPanel{Width=68};
            var source=DeskSpaceApplicationIconsV65.Get(app.Path);
            if(source!=null)picture.Children.Add(new Image{
                Source=source,Width=22,Height=22,
                Margin=new Thickness(0,0,0,2),
                HorizontalAlignment=HorizontalAlignment.Center
            });
            else picture.Children.Add(new TextBlock{
                Text="▣",FontSize=18,Foreground=Brushes.LightSteelBlue,
                TextAlignment=TextAlignment.Center
            });
            picture.Children.Add(new TextBlock{
                Text=app.Name,FontSize=10,Foreground=Brushes.White,
                TextAlignment=TextAlignment.Center,
                TextTrimming=TextTrimming.CharacterEllipsis
            });
            if(matches.Length>1)
            {
                var badge=new TextBlock{
                    Text=matches.Length+" windows",FontSize=9,
                    Foreground=Brushes.LightSteelBlue,
                    TextAlignment=TextAlignment.Center
                };
                picture.Children.Add(badge);
                appBadges[app.Name]=badge;
            }
            var appButton=new Button{
                Content=picture,Width=76,MinHeight=49,
                Margin=new Thickness(1),Padding=new Thickness(2),
                Foreground=Brushes.White,
                Background=new SolidColorBrush(Color.FromRgb(45,61,94)),
                BorderThickness=new Thickness(0)
            };
            var choices=matches;
            appButton.MouseEnter+=(_,_)=>{
                KeepOpen();
                StopIntent();
                var remaining=choices.Where(x=>
                    DeskSpaceRunningAppsV88.IsActiveWindow(x.Handle)).ToArray();
                if(remaining.Length<=1)
                {
                    windowPopup.IsOpen=false;
                    return;
                }
                pendingIcon=appButton;
                pendingWindows=remaining;
                pendingName=app.Name;
                // Screen-aware placement considers the panel boundaries
                // AND which side of the panel the app tile occupies.
                try
                {
                    var left=iconCard.PointToScreen(new Point(0,0));
                    var right=iconCard.PointToScreen(
                        new Point(iconCard.ActualWidth,0));
                    var tile=appButton.PointToScreen(
                        new Point(appButton.ActualWidth/2,0));
                    var work=SystemParameters.WorkArea;
                    pendingRight=PreferRight(
                        left.X-work.Left,work.Right-right.X,
                        (tile.X-left.X)/Math.Max(1,right.X-left.X),280);
                }
                catch(InvalidOperationException)
                {
                    pendingRight=true;
                }
                // A pointer quickly crossing another app toward an open
                // window list must not select that intervening app.
                hoverIntent.Start();
            };
            appButton.Click+=(_,_)=>{
                var remaining=choices.Where(x=>
                    DeskSpaceRunningAppsV88.IsActiveWindow(x.Handle)).ToArray();
                if(remaining.Length!=1)return;
                Hide();
                DeskSpaceRunningAppsV88.Focus(remaining[0]);
            };
            if(choices.Length==1)
            {
                // A one-window app can be closed directly from its icon.
                // Multi-window apps expose separate X controls per window.
                var tile=new Grid{Width=78};
                tile.Children.Add(appButton);
                var close=MakeCloseButton(choices[0]);
                close.Width=17;close.MinWidth=17;
                close.Height=17;close.MinHeight=17;
                close.FontSize=12;
                close.HorizontalAlignment=HorizontalAlignment.Right;
                close.VerticalAlignment=VerticalAlignment.Top;
                close.Margin=new Thickness(0,0,3,0);
                tile.Children.Add(close);
                iconRow.Children.Add(tile);
                appTiles[app.Name]=tile;
            }
            else
            {
                iconRow.Children.Add(appButton);
                appTiles[app.Name]=appButton;
            }
        }
        // Separate Popup HWNDs must close when the old deck is hidden
        // or its contents are regenerated.
        heading.Unloaded+=(_,_)=>Hide();
        heading.IsVisibleChanged+=(_,_)=>{
            if(!heading.IsVisible)Hide();
        };
        heading.Tag=iconPopup; // WPF test instrumentation; no layout effect.
        root.Children.Add(heading);
        return heading;
    }
}
