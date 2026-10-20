using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Windows.Media.Control;

namespace DeskSpace;

// Native Windows Global System Media Transport Controls (GSMTC).
// Only apps which advertise a media session are controlled. No sound recording,
// browser injection, history tracking, or extension is installed.
internal sealed class DeskSpaceMediaV18 : IDisposable
{
    private GlobalSystemMediaTransportControlsSessionManager? manager;
    private GlobalSystemMediaTransportControlsSession? current;
    private readonly DispatcherTimer clock;
    private bool polling, disposed, suppressed;
    private DateTime lastPlaying=DateTime.MinValue;
    private bool visible;
    private string title="",artist="",source="";
    private bool playing;
    private TextBlock? titleText, artistText, playText;
    public bool Visible => visible && !suppressed;
    public event Action<bool>? VisibilityChanged;

    public DeskSpaceMediaV18()
    {
        clock=new DispatcherTimer(DispatcherPriority.Background) {
            Interval=TimeSpan.FromSeconds(2.2)
        };
        clock.Tick+=async (_,_)=>await PollAsync();
    }
    public void Start()
    {
        if(disposed||clock.IsEnabled)return;
        clock.Start();
        _=PollAsync();
    }
    private static string Clean(string? text,int max)
    {
        if(string.IsNullOrWhiteSpace(text))return "";
        var result=text.Trim().Replace('\n',' ').Replace('\r',' ');
        return result.Length>max?result.Substring(0,max-1)+"…":result;
    }
    private async Task PollAsync()
    {
        if(disposed || polling)return;
        polling=true;
        try
        {
            manager??=await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            var sessions=manager.GetSessions();
            var session=sessions.FirstOrDefault(x=>x.GetPlaybackInfo()?.PlaybackStatus==
                    GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                ??manager.GetCurrentSession();
            bool isPlaying=session?.GetPlaybackInfo()?.PlaybackStatus ==
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            if(isPlaying)lastPlaying=DateTime.UtcNow;
            // Retain paused controls briefly for resumability, then restore clock.
            bool shouldShow=session!=null&&(isPlaying||
                visible&&DateTime.UtcNow-lastPlaying<TimeSpan.FromSeconds(35));
            if(!shouldShow)
            {
                current=null;
                if(visible){visible=false;VisibilityChanged?.Invoke(false);}
                return;
            }
            current=session;
            playing=isPlaying;
            var name=Clean(session!.SourceAppUserModelId,28);
            string label=name;
            string artistName="";
            try{
                var meta=await session.TryGetMediaPropertiesAsync();
                label=Clean(meta?.Title,38);
                artistName=Clean(meta?.Artist,28);
                if(string.IsNullOrEmpty(label))label=name;
            }catch(Exception){ /* player declined metadata; controls still work */ }
            title=string.IsNullOrEmpty(label)?"Media playing":label;
            artist=artistName;
            source=name;
            RefreshText();
            if(!visible)visible=true;
            VisibilityChanged?.Invoke(Visible);
        }
        catch(Exception)
        {
            // Unsupported Windows builds/players simply retain clock mode.
            current=null;
            if(visible){visible=false;VisibilityChanged?.Invoke(false);}
        }
        finally{polling=false;}
    }
    private void RefreshText()
    {
        if(titleText!=null)titleText.Text=title;
        if(artistText!=null)artistText.Text=string.IsNullOrEmpty(artist) ?
            (string.IsNullOrEmpty(source)?"Now playing":source) : artist;
        if(playText!=null)playText.Text=playing?"Ⅱ":"▶";
    }
    private async Task Command(int action)
    {
        var session=current;
        if(session==null)return;
        try
        {
            if(action<0)await session.TrySkipPreviousAsync();
            else if(action>0)await session.TrySkipNextAsync();
            else await session.TryTogglePlayPauseAsync();
            await PollAsync();
        }catch(Exception){ /* unsupported transport action; never crash widget */ }
    }
    public void DismissUntilNextTrack()
    {
        suppressed=true;
        VisibilityChanged?.Invoke(false);
    }
    public FrameworkElement CreateView()
    {
        var grad=new LinearGradientBrush(
            Color.FromRgb(34,45,86),Color.FromRgb(86,63,151),70);
        var shell=new Border {
            Background=grad,
            BorderBrush=new SolidColorBrush(Color.FromArgb(230,221,220,247)),
            BorderThickness=new Thickness(2),CornerRadius=new CornerRadius(60),
            Width=116,Height=116,ClipToBounds=true,
            ToolTip="Windows media controls · Right-click for DeskSpace menu"
        };
        var panel=new StackPanel {
            VerticalAlignment=VerticalAlignment.Center,
            HorizontalAlignment=HorizontalAlignment.Stretch,
            Margin=new Thickness(5,10,5,10)
        };
        panel.Children.Add(new TextBlock {
            Text="NOW PLAYING",FontSize=8,FontWeight=FontWeights.SemiBold,
            Foreground=new SolidColorBrush(Color.FromRgb(196,207,251)),
            HorizontalAlignment=HorizontalAlignment.Center
        });
        titleText=new TextBlock {
            Text=title,FontSize=11,FontWeight=FontWeights.SemiBold,
            Foreground=Brushes.White,TextAlignment=TextAlignment.Center,
            TextTrimming=TextTrimming.CharacterEllipsis,MaxHeight=15,
            Margin=new Thickness(2,2,2,0)
        };
        panel.Children.Add(titleText);
        artistText=new TextBlock {
            FontSize=9,Foreground=new SolidColorBrush(Color.FromRgb(224,221,250)),
            TextAlignment=TextAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis,
            MaxHeight=13
        };
        panel.Children.Add(artistText);
        var controls=new StackPanel {
            Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,
            Margin=new Thickness(0,3,0,0)
        };
        Button Make(string content,int direction,string hint)
        {
            var b=new Button{
                Content=content,FontSize=15,FontWeight=FontWeights.SemiBold,
                Width=29,Height=27,Padding=new Thickness(0),
                Background=new SolidColorBrush(Color.FromArgb(42,255,255,255)),
                Foreground=Brushes.White,BorderThickness=new Thickness(0),
                Margin=new Thickness(2,0,2,0),Cursor=System.Windows.Input.Cursors.Hand,
                ToolTip=hint
            };
            b.Click+=async (_,e)=>{e.Handled=true;await Command(direction);};
            return b;
        }
        controls.Children.Add(Make("⏮",-1,"Previous track"));
        var toggle=Make(playing?"Ⅱ":"▶",0,"Play / Pause");
        // A TextBlock inside the button lets the icon update without rebuilding.
        var playGlyph=new TextBlock{Text=playing?"Ⅱ":"▶",FontSize=15,
            Foreground=Brushes.White,TextAlignment=TextAlignment.Center};
        toggle.Content=playGlyph;
        playText=playGlyph;
        controls.Children.Add(toggle);
        controls.Children.Add(Make("⏭",1,"Next track"));
        panel.Children.Add(controls);
        shell.Child=panel;
        RefreshText();
        return shell;
    }
    public void Dispose()
    {
        disposed=true;
        clock.Stop();current=null;manager=null;
        titleText=null;artistText=null;playText=null;
    }
}
