using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DeskSpace;

// A four-function calculator contained in the same widget surface.
// No external calc.exe window or dynamic code execution.
internal sealed class DeskSpaceCalculatorV22
{
    private decimal? accumulator;
    private string operation="";
    private string entry="0";
    private bool fresh=true;
    private TextBlock? display,history;
    private static readonly Brush Ink=new SolidColorBrush(Color.FromRgb(40,43,73));
    private void Refresh()
    {
        if(display!=null)display.Text=entry;
        if(history!=null)history.Text=accumulator?.ToString(CultureInfo.CurrentCulture)+
            (string.IsNullOrEmpty(operation)?"":" "+operation);
    }
    private static decimal Parse(string text)=>
        decimal.Parse(text,CultureInfo.CurrentCulture);
    private void Digit(string digit)
    {
        if(fresh){entry=digit=="."?"0.":digit;fresh=false;}
        else if(digit=="."&&!entry.Contains('.'))entry+=".";
        else if(digit!="." && entry.Length<20)
            entry=entry=="0"?digit:entry+digit;
        Refresh();
    }
    private void Choose(string op)
    {
        try
        {
            if(accumulator.HasValue&&!fresh)Result(false);
            else accumulator=Parse(entry);
            operation=op;
            fresh=true;Refresh();
        }catch(Exception){Reset();}
    }
    private void Result(bool finish)
    {
        if(!accumulator.HasValue||operation.Length==0)return;
        decimal result=operation switch
        {
            "+"=>accumulator.Value+Parse(entry),
            "−"=>accumulator.Value-Parse(entry),
            "×"=>accumulator.Value*Parse(entry),
            "÷"=>Parse(entry)==0?throw new DivideByZeroException():accumulator.Value/Parse(entry),
            _=>Parse(entry)
        };
        entry=result.ToString(CultureInfo.CurrentCulture);
        accumulator=finish?null:result;
        operation=finish?"":operation;
        fresh=finish;Refresh();
    }
    private void Reset(){entry="0";operation="";accumulator=null;fresh=true;Refresh();}
    private void Press(string key)
    {
        try
        {
            if(key.Length==1&&(char.IsDigit(key[0])||key=="."))Digit(key);
            else if(key is "+" or "−" or "×" or "÷")Choose(key);
            else if(key=="=")Result(true);
            else if(key=="C")Reset();
            else if(key=="⌫")
            {
                entry=entry.Length<=1?"0":entry.Substring(0,entry.Length-1);
                fresh=false;Refresh();
            }
            else if(key=="±")
            {
                entry=(-Parse(entry)).ToString(CultureInfo.CurrentCulture);
                Refresh();
            }
            else if(key=="%")
            {
                entry=(Parse(entry)/100).ToString(CultureInfo.CurrentCulture);
                Refresh();
            }
        }
        catch(DivideByZeroException){Reset();if(history!=null)history.Text="Cannot divide by zero";}
        catch(OverflowException){Reset();if(history!=null)history.Text="Number too large";}
        catch(FormatException){Reset();}
    }
    public FrameworkElement Create()
    {
        var root=new StackPanel{Margin=new Thickness(5,8,5,8)};
        root.Children.Add(new TextBlock{
            Text="Calculator",FontSize=18,FontWeight=FontWeights.SemiBold,Foreground=Ink,
            Margin=new Thickness(0,0,0,9)
        });
        var face=new Border{
            Background=Brushes.White,CornerRadius=new CornerRadius(15),
            Padding=new Thickness(13,10,13,11),
            Margin=new Thickness(0,0,0,10)
        };
        var screen=new StackPanel{HorizontalAlignment=HorizontalAlignment.Stretch};
        history=new TextBlock{TextAlignment=TextAlignment.Right,
            Foreground=new SolidColorBrush(Color.FromRgb(132,136,152)),FontSize=12};
        display=new TextBlock{Text="0",FontSize=32,FontWeight=FontWeights.SemiBold,
            Foreground=Ink,TextAlignment=TextAlignment.Right,
            TextTrimming=TextTrimming.CharacterEllipsis};
        screen.Children.Add(history);screen.Children.Add(display);face.Child=screen;
        root.Children.Add(face);
        var keypad=new Grid();
        for(int i=0;i<4;i++)
            keypad.ColumnDefinitions.Add(new ColumnDefinition{
                Width=new GridLength(1,GridUnitType.Star)});
        for(int i=0;i<5;i++)
            keypad.RowDefinitions.Add(new RowDefinition{Height=new GridLength(47)});
        string[,] buttons={
            {"C","⌫","%","÷"},
            {"7","8","9","×"},
            {"4","5","6","−"},
            {"1","2","3","+"},
            {"±","0",".","="}
        };
        for(int row=0;row<5;row++)
        for(int col=0;col<4;col++)
        {
            string label=buttons[row,col];
            var button=new Button{
                Content=label,FontSize=18,FontWeight=FontWeights.Medium,
                Margin=new Thickness(3),
                Foreground=Ink,BorderThickness=new Thickness(0),
                Background=(col==3)?new SolidColorBrush(Color.FromRgb(215,207,247)):
                    new SolidColorBrush(Color.FromRgb(249,250,253))
            };
            button.Click+=(_,_)=>Press(label);
            Grid.SetRow(button,row);Grid.SetColumn(button,col);
            keypad.Children.Add(button);
        }
        root.Children.Add(keypad);
        return root;
    }
}
