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
    internal static Border Add(StackPanel root,
        IReadOnlyList<DeskSpaceRunningAppsV88.Entry> apps,
        IReadOnlyList<DeskSpaceRunningAppsV88.Entry> windows)
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
        var windowPopup=new Popup{
            Placement=PlacementMode.Right,HorizontalOffset=0,
            VerticalOffset=0,StaysOpen=true,AllowsTransparency=true,
            PopupAnimation=PopupAnimation.Fade,Child=windowCard
        };
        var closeDelay=new DispatcherTimer{
            Interval=TimeSpan.FromMilliseconds(340)
        };
        void Hide()
        {
            closeDelay.Stop();
            windowPopup.IsOpen=false;
            iconPopup.IsOpen=false;
        }
        bool Hovered()=>heading.IsMouseOver ||
            iconCard.IsMouseOver || windowCard.IsMouseOver;
        closeDelay.Tick+=(_,_)=>{
            if(Hovered())closeDelay.Stop();
            else Hide();
        };
        void KeepOpen()=>closeDelay.Stop();
        void CloseLater(){closeDelay.Stop();closeDelay.Start();}
        heading.MouseEnter+=(_,_)=>{KeepOpen();iconPopup.IsOpen=true;};
        heading.MouseLeave+=(_,_)=>CloseLater();
        iconCard.MouseEnter+=(_,_)=>KeepOpen();
        iconCard.MouseLeave+=(_,_)=>CloseLater();
        windowCard.MouseEnter+=(_,_)=>KeepOpen();
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
                TextTrimming=TextTrimming.CharacterEllipsis,
                ToolTip=app.Name
            });
            if(matches.Length>1)picture.Children.Add(new TextBlock{
                Text=matches.Length+" windows",FontSize=9,
                Foreground=Brushes.LightSteelBlue,
                TextAlignment=TextAlignment.Center
            });
            var appButton=new Button{
                Content=picture,Width=76,MinHeight=49,
                Margin=new Thickness(1),Padding=new Thickness(2),
                Foreground=Brushes.White,
                Background=new SolidColorBrush(Color.FromRgb(45,61,94)),
                BorderThickness=new Thickness(0),
                ToolTip=matches.Length>1?
                    "Hover to select a window":"Switch or minimise "+app.Name
            };
            var choices=matches;
            appButton.MouseEnter+=(_,_)=>{
                KeepOpen();
                windowPopup.IsOpen=false;
                windowItems.Children.Clear();
                if(choices.Length<=1)return;
                windowPopup.PlacementTarget=appButton;
                windowItems.Children.Add(new TextBlock{
                    Text=app.Name+" · "+choices.Length+" windows",
                    FontSize=11,Foreground=Brushes.LightSteelBlue,
                    FontWeight=FontWeights.Medium,
                    Margin=new Thickness(6,3,6,6)
                });
                foreach(var window in choices)
                {
                    var selected=window;
                    var choice=new Button{
                        Content=selected.WindowTitle,FontSize=11,
                        Foreground=Brushes.White,
                        Background=new SolidColorBrush(Color.FromRgb(45,61,94)),
                        BorderThickness=new Thickness(0),
                        HorizontalContentAlignment=HorizontalAlignment.Left,
                        Padding=new Thickness(7,5,7,5),
                        Margin=new Thickness(2,1,2,1),MaxWidth=258,
                        ToolTip=selected.WindowTitle
                    };
                    choice.Click+=(_,_)=>{
                        Hide();
                        DeskSpaceRunningAppsV88.Focus(selected);
                    };
                    windowItems.Children.Add(choice);
                }
                windowPopup.IsOpen=true;
            };
            appButton.Click+=(_,_)=>{
                if(choices.Length!=1)return;
                Hide();
                DeskSpaceRunningAppsV88.Focus(choices[0]);
            };
            iconRow.Children.Add(appButton);
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
