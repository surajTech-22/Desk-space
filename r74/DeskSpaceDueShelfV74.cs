using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace DeskSpace;

// Scheduled reminders and tasks share the existing Workspace.Reminders list.
// No second database, no Windows tray balloon and no independent editor.
// This small owned, no-taskbar surface follows the circular widget itself.
internal sealed class DeskSpaceDueShelfV74 : IDisposable
{
    internal readonly record struct DueAlert(Guid Id,string Title,DateTime Due);
    private readonly Window owner;
    private readonly Window shelf;
    private readonly WorkspaceStore store=new();
    private readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromSeconds(1)};
    private readonly List<DueAlert> awaiting=new();
    private readonly HashSet<Guid> accepted=new();
    private long lastScan;
    private bool disposed;

    internal DeskSpaceDueShelfV74(Window owner)
    {
        this.owner=owner;
        shelf=new Window{
            Title="DeskSpace due alerts",
            Width=298,Height=170,
            WindowStyle=WindowStyle.None,
            AllowsTransparency=true,Background=Brushes.Transparent,
            ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,
            ShowActivated=false,Topmost=true,Focusable=false
        };
        shelf.Owner=owner;
        owner.LocationChanged+=Moved;
        owner.SizeChanged+=Sized;
        owner.IsVisibleChanged+=VisibilityChanged;
        owner.Closed+=OwnerClosed;
        timer.Tick+=(_,_)=>CheckSystemClock();
    }

    private void Moved(object? sender,EventArgs e)=>Reposition();
    private void Sized(object? sender,SizeChangedEventArgs e)=>Reposition();
    private void VisibilityChanged(object sender,DependencyPropertyChangedEventArgs e)
    {
        if(!owner.IsVisible)shelf.Hide();
        else if(awaiting.Count>0)ShowShelf();
    }
    private void OwnerClosed(object? sender,EventArgs e)=>Dispose();

    internal void Start()
    {
        if(disposed)return;
        timer.Start();
        CheckSystemClock(force:true);
    }

    internal static IReadOnlyList<DueAlert> FindDue(Workspace workspace,DateTime localSystemTime)=>
        workspace.Reminders
            .Where(x=>!x.Done && x.LastAlert==null && x.Due<=localSystemTime)
            .OrderBy(x=>x.Due)
            .Select(x=>new DueAlert(x.Id,x.Title,x.Due))
            .ToArray();

    // Environment.TickCount64 throttles disk reads independently of user
    // clock changes. DateTime.Now provides the actual Windows local due time.
    private void CheckSystemClock(bool force=false)
    {
        if(disposed)return;
        long monotonic=Environment.TickCount64;
        if(!force && monotonic-lastScan<2000)return;
        lastScan=monotonic;
        try
        {
            var workspace=store.Load();
            var now=DateTime.Now;
            var changed=false;
            // Existing records remain the single source of truth.
            var due=FindDue(workspace,now);
            foreach(var alert in due)
            {
                if(!accepted.Add(alert.Id))continue;
                var item=workspace.Reminders.First(x=>x.Id==alert.Id);
                item.LastAlert=now;
                changed=true;
                awaiting.Add(alert);
            }
            if(changed)
            {
                try{store.Save(workspace);}
                catch{
                    // Never claim an alarm was consumed if persistence failed.
                    foreach(var alert in due)accepted.Remove(alert.Id);
                    awaiting.RemoveAll(x=>due.Any(d=>d.Id==x.Id));
                    throw;
                }
                SystemSounds.Exclamation.Play();
                Render();
            }
            // Completion/deletion in another module immediately removes
            // obsolete alerts, even when the main widget is displaying an editor.
            if(awaiting.RemoveAll(x=>!workspace.Reminders.Any(r=>
                r.Id==x.Id && !r.Done))>0)Render();
        }
        catch(Exception exception)
        {
            Debug.WriteLine("DeskSpace due-alert scan: "+exception);
        }
    }

    internal static Rect PlaceAtCircle(Rect circle,Rect work,double width,double height)
    {
        double x=Math.Clamp(circle.Left+(circle.Width-width)/2,
            work.Left,Math.Max(work.Left,work.Right-width));
        double below=circle.Bottom+8;
        double above=circle.Top-height-8;
        double y=below+height<=work.Bottom?below:
            above>=work.Top?above:
            Math.Clamp(below,work.Top,Math.Max(work.Top,work.Bottom-height));
        return new Rect(x,y,width,height);
    }

    private void Reposition()
    {
        if(disposed || !shelf.IsVisible)return;
        var work=SystemParameters.WorkArea;
        var bounds=PlaceAtCircle(new Rect(owner.Left,owner.Top,
            owner.ActualWidth>0?owner.ActualWidth:owner.Width,
            owner.ActualHeight>0?owner.ActualHeight:owner.Height),
            work,shelf.Width,shelf.Height);
        shelf.Left=bounds.Left;shelf.Top=bounds.Top;
    }

    private void ShowShelf()
    {
        if(disposed||awaiting.Count==0 || !owner.IsVisible)return;
        if(!shelf.IsVisible)shelf.Show();
        Reposition();
    }

    private void Complete(Guid id)
    {
        try
        {
            var workspace=store.Load();
            var item=workspace.Reminders.FirstOrDefault(x=>x.Id==id);
            if(item!=null){item.Done=true;store.Save(workspace);}
            Dismiss(id);
        }
        catch(Exception error){Debug.WriteLine("DeskSpace complete due: "+error);}
    }
    private void Dismiss(Guid id)
    {
        awaiting.RemoveAll(x=>x.Id==id);
        Render();
    }

    internal static FrameworkElement Preview(IReadOnlyList<DueAlert> due,
        DateTime localSystemTime,Action<Guid> complete,Action<Guid> dismiss)
    {
        var ink=new SolidColorBrush(Color.FromRgb(32,39,67));
        var muted=new SolidColorBrush(Color.FromRgb(90,98,128));
        var panel=new StackPanel{Margin=new Thickness(12,10,12,10)};
        var header=new DockPanel{LastChildFill=true};
        var clock=new TextBlock{
            Text=localSystemTime.ToString("HH:mm"),
            FontSize=11,Foreground=muted,
            VerticalAlignment=VerticalAlignment.Center};
        DockPanel.SetDock(clock,Dock.Right);header.Children.Add(clock);
        header.Children.Add(new TextBlock{Text="Due now  •  DeskSpace",
            FontSize=14,FontWeight=FontWeights.SemiBold,Foreground=ink});
        panel.Children.Add(header);
        foreach(var alert in due.Take(3))
        {
            var line=new DockPanel{Margin=new Thickness(0,8,0,0),
                LastChildFill=true};
            var actions=new StackPanel{Orientation=Orientation.Horizontal};
            Button Make(string title,Action action)
            {
                var b=new Button{Content=title,
                    Padding=new Thickness(6,3,6,3),
                    Margin=new Thickness(3,0,0,0),
                    MinWidth=32,MinHeight=26,
                    FontSize=11,Cursor=System.Windows.Input.Cursors.Hand};
                b.Click+=(_,_)=>action();
                return b;
            }
            actions.Children.Add(Make("✓",()=>complete(alert.Id)));
            actions.Children.Add(Make("×",()=>dismiss(alert.Id)));
            DockPanel.SetDock(actions,Dock.Right);
            line.Children.Add(actions);
            var content=new StackPanel();
            content.Children.Add(new TextBlock{
                Text=string.IsNullOrWhiteSpace(alert.Title)?"Untitled reminder":alert.Title,
                FontSize=12.5,FontWeight=FontWeights.SemiBold,
                TextTrimming=TextTrimming.CharacterEllipsis,
                MaxWidth=183,Foreground=ink,
                ToolTip=alert.Title});
            content.Children.Add(new TextBlock{
                Text=alert.Due.ToString("ddd dd MMM • h:mm tt"),
                FontSize=10.5,Foreground=muted});
            line.Children.Add(content);
            panel.Children.Add(line);
        }
        if(due.Count>3)panel.Children.Add(new TextBlock{
            Text=$"+{due.Count-3} more due",
            Foreground=muted,FontSize=11,Margin=new Thickness(0,5,0,0)});
        return new Border{
            Width=292,
            Background=new SolidColorBrush(Color.FromArgb(250,247,246,255)),
            BorderBrush=new SolidColorBrush(Color.FromRgb(187,182,226)),
            BorderThickness=new Thickness(1),
            CornerRadius=new CornerRadius(13),
            Child=panel
        };
    }

    private void Render()
    {
        if(disposed)return;
        if(awaiting.Count==0){shelf.Hide();return;}
        shelf.Content=Preview(awaiting,DateTime.Now,Complete,Dismiss);
        shelf.Height=awaiting.Count>2?216:awaiting.Count==2?165:106;
        ShowShelf();
    }

    public void Dispose()
    {
        if(disposed)return;
        disposed=true;timer.Stop();
        owner.LocationChanged-=Moved;
        owner.SizeChanged-=Sized;
        owner.IsVisibleChanged-=VisibilityChanged;
        owner.Closed-=OwnerClosed;
        shelf.Close();
    }
}
