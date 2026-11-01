using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace DeskSpace;

// Excel-style expansion belongs to its own formula cell, not to a detached
// mapping editor panel. Read-only; no original workbook values are exposed.
internal static class DeskSpaceExcelFormulaExpanderV144
{
    internal static void Attach(Border cell,Grid cellSurface,
        string address,string formula)
    {
        if(string.IsNullOrWhiteSpace(formula))return;
        var box=new TextBox{
            Text=formula,
            IsReadOnly=true,
            TextWrapping=TextWrapping.Wrap,
            AcceptsReturn=true,
            VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,
            MinHeight=90,MaxHeight=320,
            MinWidth=240,Width=500,
            Padding=new Thickness(8,6,8,6),
            BorderThickness=new Thickness(0),
            FontFamily=new FontFamily("Consolas"),
            FontSize=13,
            Background=Brushes.White,
            Foreground=new SolidColorBrush(Color.FromRgb(25,39,57)),
            ToolTip="Read-only actual Excel formula. Select and Ctrl+C to copy."
        };
        var title=new TextBlock{
            Text=address+" · Formula (full expression)",
            FontSize=12,
            FontWeight=FontWeights.SemiBold,
            Foreground=new SolidColorBrush(Color.FromRgb(32,60,97)),
            VerticalAlignment=VerticalAlignment.Center,
            Margin=new Thickness(0,0,8,0)
        };
        var popup=new Popup{
            PlacementTarget=cell,
            Placement=PlacementMode.Bottom,
            HorizontalOffset=2,VerticalOffset=2,
            AllowsTransparency=true,StaysOpen=false,
            PopupAnimation=PopupAnimation.Fade
        };
        var header=new DockPanel{LastChildFill=true,Margin=new Thickness(7,5,7,4)};
        var close=new Button{
            Content="×",
            Width=25,Height=23,
            FontSize=13,FontWeight=FontWeights.Bold,
            Background=Brushes.Transparent,
            BorderThickness=new Thickness(0),
            ToolTip="Collapse formula"
        };
        DockPanel.SetDock(close,Dock.Right);
        header.Children.Add(close);
        header.Children.Add(title);
        var contents=new DockPanel{LastChildFill=true};
        DockPanel.SetDock(header,Dock.Top);
        contents.Children.Add(header);
        contents.Children.Add(box);
        popup.Child=new Border{
            Background=Brushes.White,
            BorderBrush=new SolidColorBrush(Color.FromRgb(147,164,187)),
            BorderThickness=new Thickness(1),
            CornerRadius=new CornerRadius(4),
            Padding=new Thickness(4),
            Child=contents
        };
        var expand=new Button{
            Content="⤢",
            Width=17,Height=17,
            Padding=new Thickness(0),
            FontSize=10,
            HorizontalAlignment=HorizontalAlignment.Right,
            VerticalAlignment=VerticalAlignment.Top,
            Background=new SolidColorBrush(Color.FromArgb(230,241,247,253)),
            Foreground=new SolidColorBrush(Color.FromRgb(33,70,111)),
            BorderThickness=new Thickness(0),
            ToolTip="Expand and read the full formula (also double-click cell)"
        };
        Panel.SetZIndex(expand,20);
        cellSurface.Children.Add(expand);
        // Keeping Popup in the visual tree ensures it remains alive. It
        // opens outside the clipped Excel cell using WPF's popup window.
        cellSurface.Children.Add(popup);
        expand.Click+=(_,e)=>{popup.IsOpen=!popup.IsOpen;e.Handled=true;};
        close.Click+=(_,e)=>{popup.IsOpen=false;e.Handled=true;};
        cell.MouseLeftButtonDown+=(_,e)=>{
            if(e.ClickCount>=2){popup.IsOpen=true;e.Handled=true;}
        };
        popup.Opened+=(_,_)=>{box.Focus();box.CaretIndex=0;};
    }
}
