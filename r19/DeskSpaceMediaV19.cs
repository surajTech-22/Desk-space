using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Windows.Media.Control;

namespace DeskSpace;

// R19: Keep DeskSpace's original circle intact. Present native Windows transport
// controls in a separate, taskbar-less translucent Popup anchored BELOW it.
internal sealed class DeskSpaceMediaV18 : IDisposable
{
    private GlobalSystemMediaTransportControlsSessionManager? manager;
    private GlobalSystemMediaTransportControlsSession? current;
    private readonly DispatcherTimer poller;
    private Popup? popup;
    private Window? host;
    private Func<string>? moduleSummary;
    private TextBlock? contextText,titleText,artistText,playText,elapsedText,durationText;
    private Slider? timeline;
    private Button? hideCircleButton;
    private bool disposed,polling,visible,playing,updatingTimeline,draggingTimeline,circleHidden;
    private DateTime lastPlaying=DateTime.MinValue;
    private string title="",artist="",source="";
    private TimeSpan beginning,ending,position;
    private int moveTick;
    public bool Visible=>visible;
    public bool PanelOpen=>popup?.IsOpen==true;
    public event Action<bool>? VisibilityChanged;

    [DllImport("user32.dll")] private static extern void keybd_event(byte key,byte scan,uint flags,UIntPtr extra);
    private static void Volume(byte key)
    {
        try{keybd_event(key,0,0,UIntPtr.Zero);keybd_event(key,0,2,UIntPtr.Zero);}
        catch(Exception){}
    }
    public DeskSpaceMediaV18()
    {
        poller=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromSeconds(2)};
        poller.Tick+=async (_,_)=>await PollAsync();
    }
    public void Start()
    {
        if(disposed||poller.IsEnabled)return;
        poller.Start();
        _=PollAsync();
    }
    private static string Short(string? text,int max)
    {
        var s=(text??"").Replace('\r',' ').Replace('\n',' ').Trim();
        return s.Length>max?s.Substring(0,max-1)+"…":s;
    }
    private static string Clock(TimeSpan time)=>time.TotalHours>=1
        ?time.ToString(@"h\:mm\:ss"):time.ToString(@"m\:ss");

    private async Task PollAsync()
    {
        if(disposed||polling)return;
        polling=true;
        try
        {
            manager??=await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            var selected=manager.GetSessions().FirstOrDefault(x=>
                x.GetPlaybackInfo()?.PlaybackStatus==
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                ??manager.GetCurrentSession();
            var active=selected?.GetPlaybackInfo()?.PlaybackStatus==
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            if(active)lastPlaying=DateTime.UtcNow;
            var show=selected!=null&&(active||
                visible&&DateTime.UtcNow-lastPlaying<TimeSpan.FromSeconds(35));
            if(!show)
            {
                current=null;
                if(visible){visible=false;VisibilityChanged?.Invoke(false);}
                return;
            }
            current=selected;
            playing=active;
            source=Short(selected!.SourceAppUserModelId,42);
            try
            {
                var meta=await selected.TryGetMediaPropertiesAsync();
                title=Short(meta?.Title,90);
                artist=Short(meta?.Artist,65);
            }
            catch(Exception){title="";artist="";}
            if(string.IsNullOrEmpty(title))title=string.IsNullOrEmpty(source)?"Media":source;
            try
            {
                var times=selected.GetTimelineProperties();
                beginning=times.StartTime;ending=times.EndTime;position=times.Position;
            }catch(Exception){beginning=ending=position=TimeSpan.Zero;}
            visible=true;
            RefreshView();
            // Safe on every update: the host only toggles the Popup state.
            VisibilityChanged?.Invoke(true);
        }
        catch(Exception)
        {
            current=null;
            if(visible){visible=false;VisibilityChanged?.Invoke(false);}
        }
        finally{polling=false;}
    }
    private async Task Transport(int direction)
    {
        var session=current;
        if(session==null)return;
        try
        {
            if(direction<0)await session.TrySkipPreviousAsync();
            else if(direction>0)await session.TrySkipNextAsync();
            else await session.TryTogglePlayPauseAsync();
            await PollAsync();
        }catch(Exception){/* Not all players support every transport action. */}
    }
    private async Task Seek()
    {
        if(current==null||timeline==null||ending<=beginning)return;
        var target=beginning+TimeSpan.FromSeconds(timeline.Value);
        try{await current.TryChangePlaybackPositionAsync(target.Ticks);}
        catch(Exception){/* Not all browser/player sessions allow seeking. */}
    }
    private void RefreshView()
    {
        if(titleText!=null)titleText.Text=title;
        if(artistText!=null)artistText.Text=string.IsNullOrEmpty(artist)?source:artist;
        if(playText!=null)playText.Text=playing?"Ⅱ":"▶";
        var previous=Short(moduleSummary?.Invoke(),65);
        if(contextText!=null)contextText.Text=string.IsNullOrEmpty(previous)
            ?DateTime.Now.ToString("hh:mm tt") :previous;
        if(timeline==null)return;
        updatingTimeline=true;
        try
        {
            var seconds=Math.Max(0,(ending-beginning).TotalSeconds);
            timeline.IsEnabled=seconds>1;
            timeline.Maximum=Math.Max(1,seconds);
            if(!draggingTimeline)
                timeline.Value=Math.Clamp((position-beginning).TotalSeconds,0,timeline.Maximum);
            if(elapsedText!=null)elapsedText.Text=Clock(position-beginning<TimeSpan.Zero?
                TimeSpan.Zero:position-beginning);
            if(durationText!=null)durationText.Text=seconds>0?
                Clock(ending-beginning):"–:––";
        }
        finally{updatingTimeline=false;}
    }
    private static Button Button(string label,string tooltip,Action click,int width=34)
    {
        var b=new Button{
            Content=label,ToolTip=tooltip,Width=width,MinHeight=28,
            FontSize=13,FontWeight=FontWeights.SemiBold,
            Background=new SolidColorBrush(Color.FromArgb(47,255,255,255)),
            Foreground=Brushes.White,BorderThickness=new Thickness(0),
            Padding=new Thickness(2,3,2,3),Margin=new Thickness(2,0,2,0),
            Cursor=Cursors.Hand
        };
        b.Click+=(_,e)=>{e.Handled=true;click();};
        return b;
    }
    public void ShowBelow(Window window,Func<string> previousModuleData)
    {
        if(disposed||!Visible)return;
        if(popup==null||host!=window)
        {
            CloseBelow();
            host=window;
            moduleSummary=previousModuleData;
            var root=new StackPanel{Margin=new Thickness(10,7,10,9)};
            contextText=new TextBlock{
                FontSize=11,FontWeight=FontWeights.Medium,
                Foreground=new SolidColorBrush(Color.FromRgb(232,236,255)),
                TextTrimming=TextTrimming.CharacterEllipsis,TextAlignment=TextAlignment.Center,
                MaxHeight=30,Margin=new Thickness(5,0,5,5),
                ToolTip="Current DeskSpace module data"
            };
            root.Children.Add(contextText);
            var divider=new Border{
                Height=1,Background=new SolidColorBrush(Color.FromArgb(65,255,255,255)),
                Margin=new Thickness(4,0,4,5)
            };
            root.Children.Add(divider);
            titleText=new TextBlock{FontSize=12,FontWeight=FontWeights.SemiBold,
                Foreground=Brushes.White,TextTrimming=TextTrimming.CharacterEllipsis,
                TextAlignment=TextAlignment.Center,Margin=new Thickness(0,1,0,0),
                ToolTip="Currently playing media title"};
            artistText=new TextBlock{FontSize=10,TextAlignment=TextAlignment.Center,
                Foreground=new SolidColorBrush(Color.FromRgb(209,215,244)),
                TextTrimming=TextTrimming.CharacterEllipsis,
                Margin=new Thickness(0,2,0,4)};
            root.Children.Add(titleText);root.Children.Add(artistText);
            var transport=new StackPanel{Orientation=Orientation.Horizontal,
                HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,1,0,4)};
            transport.Children.Add(Button("⏮","Previous",()=>_=Transport(-1)));
            var play=Button("▶","Play or pause",()=>_=Transport(0),48);
            var glyph=new TextBlock{Text="▶",Foreground=Brushes.White,FontSize=17};
            play.Content=glyph;playText=glyph;
            transport.Children.Add(play);
            transport.Children.Add(Button("⏭","Next",()=>_=Transport(1)));
            root.Children.Add(transport);
            var seekRow=new Grid{Margin=new Thickness(3,0,3,4)};
            seekRow.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
            seekRow.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
            seekRow.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
            elapsedText=new TextBlock{Foreground=Brushes.White,FontSize=10,
                MinWidth=35,VerticalAlignment=VerticalAlignment.Center};
            durationText=new TextBlock{Foreground=Brushes.White,FontSize=10,
                MinWidth=35,TextAlignment=TextAlignment.Right,
                VerticalAlignment=VerticalAlignment.Center};
            timeline=new Slider{Minimum=0,Maximum=1,Height=20,
                Margin=new Thickness(5,0,5,0),
                ToolTip="Seek position when supported by the player"};
            timeline.PreviewMouseLeftButtonDown+=(_,_)=>draggingTimeline=true;
            timeline.PreviewMouseLeftButtonUp+=async (_,_)=>{
                draggingTimeline=false;
                if(!updatingTimeline)await Seek();
            };
            Grid.SetColumn(timeline,1);Grid.SetColumn(durationText,2);
            seekRow.Children.Add(elapsedText);seekRow.Children.Add(timeline);
            seekRow.Children.Add(durationText);root.Children.Add(seekRow);
            var footer=new StackPanel{Orientation=Orientation.Horizontal,
                HorizontalAlignment=HorizontalAlignment.Center};
            footer.Children.Add(Button("Vol −","System volume down",()=>Volume(0xAE),47));
            footer.Children.Add(Button("Mute","Toggle system mute",()=>Volume(0xAD),45));
            footer.Children.Add(Button("Vol +","System volume up",()=>Volume(0xAF),47));
            hideCircleButton=Button("Hide circle","Hide circle but keep controls visible",
                ToggleCircle,95);
            footer.Children.Add(hideCircleButton);
            root.Children.Add(footer);
            var frame=new Border{
                Background=new SolidColorBrush(Color.FromArgb(231,35,37,76)),
                BorderBrush=new SolidColorBrush(Color.FromArgb(175,189,196,240)),
                BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(13),
                Child=root,Width=316,SnapsToDevicePixels=true
            };
            popup=new Popup{
                PlacementTarget=host,Placement=PlacementMode.Bottom,
                HorizontalOffset=0,VerticalOffset=6,
                StaysOpen=true,AllowsTransparency=true,PopupAnimation=PopupAnimation.Fade,
                Child=frame
            };
            host.LocationChanged+=Position;
        }
        popup.IsOpen=host.IsVisible;
        RefreshView();
    }
    private void Position(object? sender,EventArgs args)
    {
        if(popup?.IsOpen==true)popup.HorizontalOffset=(++moveTick%2==0)?0:0.01;
    }
    private void ToggleCircle()
    {
        if(host==null)return;
        circleHidden=!circleHidden;
        // Keep the actual WPF owner at its original location for Popup anchoring,
        // but avoid intercepting clicks when the circle is visually hidden.
        host.Opacity=circleHidden?0:1;
        host.IsHitTestVisible=!circleHidden;
        if(hideCircleButton!=null)
            hideCircleButton.Content=circleHidden?"Show circle":"Hide circle";
    }
    private void RestoreCircle()
    {
        if(host!=null){
            host.Opacity=1;host.IsHitTestVisible=true;
        }
        circleHidden=false;
    }
    public void CloseBelow()
    {
        RestoreCircle();
        if(host!=null)host.LocationChanged-=Position;
        if(popup!=null)popup.IsOpen=false;
        popup=null;host=null;moduleSummary=null;
        contextText=titleText=artistText=playText=elapsedText=durationText=null;
        timeline=null;hideCircleButton=null;draggingTimeline=false;
    }
    public void Dispose()
    {
        disposed=true;poller.Stop();
        CloseBelow();current=null;manager=null;
        VisibilityChanged=null;
    }
}
