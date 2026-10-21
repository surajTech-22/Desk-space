using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Windows.Media;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace DeskSpace;

// In-widget GSMTC controller: compact, independently draggable and minimizable.
// Artwork and supported commands come from Windows; animated ambience is not
// a microphone capture or a decoded video stream.
internal sealed class DeskSpaceMediaV18 : IDisposable
{
    private GlobalSystemMediaTransportControlsSessionManager? manager;
    private GlobalSystemMediaTransportControlsSession? current;
    private readonly DispatcherTimer poller, visualTimer;
    private Popup? popup;
    private Window? host;
    private Func<string>? summary;
    private Border? frame, dragHeader;
    private Grid? playerRoot;
    private StackPanel? details;
    private Grid? artLayer;
    private LinearGradientBrush? dynamicGradient;
    private Image? artwork;
    private TextBlock? contextText,titleText,artistText,playGlyph,elapsedText,durationText,headerCaption;
    private Slider? timeline;
    private enum Layout { Compact, Advanced, Pill }
    private Layout view=Layout.Compact;
    private StackPanel? advancedContent, pillContent;
    private Border? mainHeader, pillHeader;
    private Image? miniArt, albumArt;
    private TextBlock? pillTitle, pillPlay;
    private Slider? pillSeek;
    private Button? pillPreviousButton,pillNextButton;
    private Button? expandButton;
    private bool customPosition;

    private Button? minimizedButton,circleButton,previousButton,nextButton,stopButton,shuffleButton,repeatButton,rewindButton,fastButton,speedButton;
    private bool disposed,polling,visible,playing,draggingTimeline,updatingTimeline,minimized,circleMinimized,dragging;
    private DateTime lastPlaying=DateTime.MinValue;
    private string title="",artist="",source="",artIdentity="",mediaKind="Media";
    private TimeSpan beginning,ending,position;
    private Point dragStart;
    private double dragOriginX,dragOriginY,offsetX=-65,offsetY=122;
    private int moveTick,animationStep;
    private Color accent=Color.FromRgb(97,88,178);
    private BitmapSource? image;
    private GlobalSystemMediaTransportControlsSessionPlaybackControls? capabilities;
    private bool? shuffle;
    private MediaPlaybackAutoRepeatMode repeat=MediaPlaybackAutoRepeatMode.None;
    private double speed=1;
    public bool Visible=>visible;
    public bool PanelOpen=>popup?.IsOpen==true;
    public double ShelfOffset=>view==Layout.Pill?92:view==Layout.Advanced?260:176;
    public event Action<bool>? VisibilityChanged;

    [DllImport("user32.dll")] private static extern void keybd_event(byte vk,byte scan,uint flags,UIntPtr extra);
    private static void Volume(byte vk)
    {
        try{keybd_event(vk,0,0,UIntPtr.Zero);keybd_event(vk,0,2,UIntPtr.Zero);}catch{}
    }
    public DeskSpaceMediaV18()
    {
        poller=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromSeconds(1.7)};
        poller.Tick+=async (_,_)=>await PollAsync();
        visualTimer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(110)};
        visualTimer.Tick+=(_,_)=>Animate();
    }
    public void Start()
    {
        if(disposed||poller.IsEnabled)return;
        poller.Start();
        _=PollAsync();
    }
    private static string Short(string? text,int limit)
    {
        var s=(text??"").Replace('\r',' ').Replace('\n',' ').Trim();
        return s.Length>limit?s.Substring(0,limit-1)+"…":s;
    }
    private static string Time(TimeSpan x)=>x.TotalHours>=1?
        x.ToString(@"h\:mm\:ss"):x.ToString(@"m\:ss");
    private async Task PollAsync()
    {
        if(disposed||polling)return;
        polling=true;
        try
        {
            manager??=await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            var session=manager.GetSessions().FirstOrDefault(x=>
                x.GetPlaybackInfo()?.PlaybackStatus==
                    GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                ?? manager.GetCurrentSession();
            bool active=session?.GetPlaybackInfo()?.PlaybackStatus==
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            if(active)lastPlaying=DateTime.UtcNow;
            bool show=session!=null&&(active||
                visible&&DateTime.UtcNow-lastPlaying<TimeSpan.FromSeconds(35));
            if(!show)
            {
                current=null;
                if(visible){visible=false;VisibilityChanged?.Invoke(false);}
                return;
            }
            current=session;
            playing=active;
            source=Short(session!.SourceAppUserModelId,42);
            var info=session.GetPlaybackInfo();
            capabilities=info?.Controls;
            shuffle=info?.IsShuffleActive;
            repeat=info?.AutoRepeatMode??MediaPlaybackAutoRepeatMode.None;
            speed=info?.PlaybackRate??1;
            string newTitle="";
            try
            {
                var meta=await session.TryGetMediaPropertiesAsync();
                newTitle=Short(meta.Title,92);
                artist=Short(meta.Artist,72);
                mediaKind=meta.PlaybackType?.ToString()??"Media";
                string identity=source+"|"+newTitle;
                if(artIdentity!=identity)
                {
                    artIdentity=identity;
                    _=LoadArtAsync(meta.Thumbnail,identity);
                }
            }
            catch(Exception){artist="";mediaKind="Media";}
            title=string.IsNullOrWhiteSpace(newTitle)?
                (string.IsNullOrWhiteSpace(source)?"Media playing":source):newTitle;
            try
            {
                var timelineInfo=session.GetTimelineProperties();
                beginning=timelineInfo.StartTime;ending=timelineInfo.EndTime;
                position=timelineInfo.Position;
            }catch(Exception){beginning=ending=position=TimeSpan.Zero;}
            visible=true;
            UpdateView();
            VisibilityChanged?.Invoke(true);
        }
        catch(Exception)
        {
            current=null;
            if(visible){visible=false;VisibilityChanged?.Invoke(false);}
        }
        finally{polling=false;}
    }
    private async Task LoadArtAsync(IRandomAccessStreamReference? thumbnail,string identity)
    {
        BitmapSource? candidate=null;
        Color newAccent=Color.FromRgb(97,88,178);
        try
        {
            if(thumbnail!=null)
            {
                using var stream=await thumbnail.OpenReadAsync();
                var count=(uint)Math.Min(stream.Size,2_000_000UL);
                if(count>0)
                {
                    using var reader=new DataReader(stream.GetInputStreamAt(0));
                    var loaded=await reader.LoadAsync(count);
                    var bytes=new byte[(int)loaded];
                    reader.ReadBytes(bytes);
                    using var memory=new System.IO.MemoryStream(bytes);
                    var bitmap=new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption=BitmapCacheOption.OnLoad;
                    bitmap.DecodePixelWidth=250;
                    bitmap.StreamSource=memory;
                    bitmap.EndInit();bitmap.Freeze();
                    candidate=bitmap;
                    var converted=new FormatConvertedBitmap(bitmap,PixelFormats.Bgra32,null,0);
                    converted.Freeze();
                    int w=converted.PixelWidth,h=converted.PixelHeight;
                    var pixels=new byte[w*h*4];
                    converted.CopyPixels(pixels,w*4,0);
                    long red=0,green=0,blue=0,counted=0;
                    for(int y=0;y<h;y+=8)
                        for(int x=0;x<w;x+=8)
                        {
                            int p=(y*w+x)*4;
                            blue+=pixels[p];green+=pixels[p+1];red+=pixels[p+2];counted++;
                        }
                    if(counted>0)
                        newAccent=Color.FromRgb((byte)(red/counted),
                            (byte)(green/counted),(byte)(blue/counted));
                }
            }
        }catch(Exception){ /* Thumbnail not provided or not decodable. */ }
        if(disposed||identity!=artIdentity)return;
        image=candidate;
        accent=newAccent;
        if(artwork!=null)
        {
            artwork.Source=image;
            artwork.Visibility=image==null?Visibility.Collapsed:Visibility.Visible;
        }
    }
    private void Animate()
    {
        if(dynamicGradient==null||!visible||view==Layout.Pill)return;
        animationStep++;
        double wave=(Math.Sin(animationStep*.065)+1)/2;
        byte a=(byte)(170+40*wave);
        var tint=Color.FromArgb(a,(byte)Math.Clamp(accent.R*.62+36,0,255),
            (byte)Math.Clamp(accent.G*.61+27,0,255),
            (byte)Math.Clamp(accent.B*.75+43,0,255));
        dynamicGradient.GradientStops[0].Color=tint;
        dynamicGradient.GradientStops[1].Color=
            Color.FromArgb(241,(byte)(30+wave*18),(byte)(34+wave*12),
                (byte)(70+wave*30));
        dynamicGradient.StartPoint=new Point(.5+.3*Math.Sin(animationStep*.016),0);
        dynamicGradient.EndPoint=new Point(.5-.3*Math.Sin(animationStep*.013),1);
    }
    private async Task Control(string action)
    {
        var s=current;
        if(s==null)return;
        try
        {
            switch(action)
            {
                case "previous":await s.TrySkipPreviousAsync();break;
                case "next":await s.TrySkipNextAsync();break;
                case "toggle":await s.TryTogglePlayPauseAsync();break;
                case "stop":await s.TryStopAsync();break;
                case "rewind":await s.TryRewindAsync();break;
                case "forward":await s.TryFastForwardAsync();break;
                case "shuffle":await s.TryChangeShuffleActiveAsync(!(shuffle??false));break;
                case "repeat":
                    var next=repeat==MediaPlaybackAutoRepeatMode.None?
                        MediaPlaybackAutoRepeatMode.List:
                        repeat==MediaPlaybackAutoRepeatMode.List?
                            MediaPlaybackAutoRepeatMode.Track:
                            MediaPlaybackAutoRepeatMode.None;
                    await s.TryChangeAutoRepeatModeAsync(next);break;
                case "speed":
                    var rate=speed<1.25?1.25:speed<1.5?1.5:speed<2?2:1;
                    await s.TryChangePlaybackRateAsync(rate);break;
            }
            await PollAsync();
        }
        catch(Exception){ /* The target player may revoke a control mid-session. */ }
    }
    private async Task SeekAsync()
    {
        if(current==null||timeline==null||ending<=beginning)return;
        try{await current.TryChangePlaybackPositionAsync(
            (beginning+TimeSpan.FromSeconds(timeline.Value)).Ticks);}
        catch(Exception){}
    }
    private static Button Make(string label,string tooltip,Action onClick,double width=31)
    {
        var b=new Button{
            Content=label,ToolTip=tooltip,Width=width,Height=26,
            FontSize=12,FontWeight=FontWeights.SemiBold,
            Foreground=Brushes.White,Background=new SolidColorBrush(Color.FromArgb(65,255,255,255)),
            BorderThickness=new Thickness(0),Padding=new Thickness(1,1,1,1),
            Margin=new Thickness(2,0,2,0),Cursor=Cursors.Hand
        };
        b.Click+=(_,e)=>{e.Handled=true;onClick();};
        return b;
    }
    private void UpdateView()
    {
        if(titleText!=null)titleText.Text=title;
        if(pillTitle!=null)pillTitle.Text=string.IsNullOrWhiteSpace(title)?"Media":title;
        if(pillPlay!=null)pillPlay.Text=playing?"Ⅱ":"▶";
        if(pillPreviousButton!=null)pillPreviousButton.IsEnabled=capabilities?.IsPreviousEnabled==true;
        if(pillNextButton!=null)pillNextButton.IsEnabled=capabilities?.IsNextEnabled==true;
        if(albumArt!=null)albumArt.Source=image;
        if(miniArt!=null)miniArt.Source=image;
        if(artistText!=null)artistText.Text=string.IsNullOrEmpty(artist)?source:artist;
        if(contextText!=null)contextText.Text=Short(summary?.Invoke(),60);
        if(headerCaption!=null)headerCaption.Text=Short(summary?.Invoke(),55);
        if(playGlyph!=null)playGlyph.Text=playing?"Ⅱ":"▶";
        if(artwork!=null&&artwork.Source!=image)
        {
            artwork.Source=image;artwork.Visibility=image==null?Visibility.Collapsed:Visibility.Visible;
        }
        var caps=capabilities;
        if(previousButton!=null)previousButton.Visibility=caps?.IsPreviousEnabled==true?Visibility.Visible:Visibility.Collapsed;
        if(nextButton!=null)nextButton.Visibility=caps?.IsNextEnabled==true?Visibility.Visible:Visibility.Collapsed;
        if(stopButton!=null)stopButton.Visibility=caps?.IsStopEnabled==true?Visibility.Visible:Visibility.Collapsed;
        if(shuffleButton!=null)
        {
            shuffleButton.Visibility=caps?.IsShuffleEnabled==true?Visibility.Visible:Visibility.Collapsed;
            shuffleButton.Content=shuffle==true?"🔀 ✓":"🔀";
        }
        if(repeatButton!=null)
        {
            repeatButton.Visibility=caps?.IsRepeatEnabled==true?Visibility.Visible:Visibility.Collapsed;
            repeatButton.Content=repeat==MediaPlaybackAutoRepeatMode.None?"↻":
                repeat==MediaPlaybackAutoRepeatMode.List?"↻ ✓":"↻ 1";
        }
        if(rewindButton!=null)rewindButton.Visibility=caps?.IsRewindEnabled==true?Visibility.Visible:Visibility.Collapsed;
        if(fastButton!=null)fastButton.Visibility=caps?.IsFastForwardEnabled==true?Visibility.Visible:Visibility.Collapsed;
        if(speedButton!=null)
        {
            speedButton.Visibility=caps?.IsPlaybackRateEnabled==true?Visibility.Visible:Visibility.Collapsed;
            speedButton.Content=speed.ToString("0.##")+"×";
        }
        if(timeline==null)return;
        var seconds=Math.Max(0,(ending-beginning).TotalSeconds);
        timeline.IsEnabled=seconds>1 && caps?.IsPlaybackPositionEnabled==true;
        timeline.Maximum=Math.Max(1,seconds);
        if(pillSeek!=null)
        {
            pillSeek.Maximum=Math.Max(1,seconds);
            pillSeek.IsEnabled=seconds>1 && caps?.IsPlaybackPositionEnabled==true;
            if(!draggingTimeline)
                pillSeek.Value=Math.Clamp((position-beginning).TotalSeconds,0,pillSeek.Maximum);
        }
        if(!draggingTimeline)timeline.Value=Math.Clamp((position-beginning).TotalSeconds,0,timeline.Maximum);
        if(elapsedText!=null)elapsedText.Text=Time(position<beginning?TimeSpan.Zero:position-beginning);
        if(durationText!=null)durationText.Text=seconds>0?Time(ending-beginning):"–:––";
    }
    private void TogglePanel()
    {
        view=view==Layout.Pill?Layout.Compact:Layout.Pill;
        UpdateLayout();
    }
    private void ToggleAdvanced()
    {
        view=view==Layout.Advanced?Layout.Compact:Layout.Advanced;
        UpdateLayout();
    }
    private void UpdateLayout()
    {
        bool pill=view==Layout.Pill;
        if(details!=null)details.Visibility=pill?Visibility.Collapsed:Visibility.Visible;
        if(advancedContent!=null)advancedContent.Visibility=view==Layout.Advanced?Visibility.Visible:Visibility.Collapsed;
        if(pillContent!=null)pillContent.Visibility=pill?Visibility.Visible:Visibility.Collapsed;
        if(mainHeader!=null)mainHeader.Visibility=pill?Visibility.Collapsed:Visibility.Visible;
        if(pillHeader!=null)pillHeader.Visibility=pill?Visibility.Visible:Visibility.Collapsed;
        dragHeader=pill?pillHeader:mainHeader;
        if(frame!=null){
            frame.Width=pill?298:view==Layout.Advanced?292:256;
            frame.Height=pill?76:view==Layout.Advanced?240:156;
        }
        // Without an explicit height, large album art forces the Root Grid
        // to reserve 250px even when all full-player controls are hidden.
        if(playerRoot!=null)playerRoot.Height=frame?.Height??76;
        if(expandButton!=null)expandButton.Content=view==Layout.Advanced?"⌃":"⌄";
        if(pill)visualTimer.Stop();else if(!visualTimer.IsEnabled)visualTimer.Start();
        KeepVisible();
    }
    private void KeepVisible()
    {
        if(host==null||popup==null)return;
        var work=SystemParameters.WorkArea;
        double width=frame?.Width??256;
        double height=view==Layout.Pill?76:view==Layout.Advanced?240:156;
        offsetX=Math.Clamp(offsetX,work.Left-host.Left+7,
            Math.Max(work.Left-host.Left+7,work.Right-host.Left-width-7));
        offsetY=Math.Clamp(offsetY,work.Top-host.Top+7,
            Math.Max(work.Top-host.Top+7,work.Bottom-host.Top-height-7));
        popup.HorizontalOffset=offsetX;popup.VerticalOffset=offsetY;
    }
    private void ToggleCircle()
    {
        if(host==null)return;
        circleMinimized=!circleMinimized;
        host.Opacity=circleMinimized?0:1;
        host.IsHitTestVisible=!circleMinimized;
        if(circleButton!=null)circleButton.Content=circleMinimized?"◉":"○";
    }
    private void RestoreCircle()
    {
        if(host!=null){host.Opacity=1;host.IsHitTestVisible=true;}
        circleMinimized=false;
    }
    private void DragDown(object sender,MouseButtonEventArgs e)
    {
        if(popup==null||dragHeader==null||e.ClickCount>1)return;
        DependencyObject? child=e.OriginalSource as DependencyObject;
        while(child!=null)
        {
            if(child is Button)return;
            if(ReferenceEquals(child,dragHeader))break;
            child=VisualTreeHelper.GetParent(child);
        }
        dragging=true;
        dragStart=dragHeader.PointToScreen(e.GetPosition(dragHeader));
        dragOriginX=offsetX;dragOriginY=offsetY;
        dragHeader.CaptureMouse();e.Handled=true;
    }
    private void DragMove(object sender,MouseEventArgs e)
    {
        if(!dragging||popup==null||dragHeader==null)return;
        if(e.LeftButton!=MouseButtonState.Pressed){DragUp(sender,e);return;}
        var now=dragHeader.PointToScreen(e.GetPosition(dragHeader));
        var dpi=VisualTreeHelper.GetDpi(dragHeader);
        offsetX=dragOriginX+(now.X-dragStart.X)/dpi.DpiScaleX;
        offsetY=dragOriginY+(now.Y-dragStart.Y)/dpi.DpiScaleY;
        customPosition=true;
        KeepVisible();
    }
    private void DragUp(object sender,MouseEventArgs e)
    {
        dragging=false;dragHeader?.ReleaseMouseCapture();
    }
    public void ShowBelow(Window window,Func<string> previousModuleData)
    {
        if(disposed||!visible)return;
        if(popup==null||host!=window)
        {
            CloseBelow();
            host=window;summary=previousModuleData;
            var root=new Grid();
            playerRoot=root;
            artLayer=new Grid{ClipToBounds=true};
            artwork=new Image{
                Stretch=Stretch.UniformToFill,
                Source=image,Opacity=.42,
                Visibility=image==null?Visibility.Collapsed:Visibility.Visible,
                IsHitTestVisible=false,
                Effect=new BlurEffect{Radius=11}
            };
            artLayer.Children.Add(artwork);
            dynamicGradient=new LinearGradientBrush{
                StartPoint=new Point(0,0),EndPoint=new Point(1,1)
            };
            dynamicGradient.GradientStops.Add(new GradientStop(Color.FromArgb(197,65,61,130),0));
            dynamicGradient.GradientStops.Add(new GradientStop(Color.FromArgb(240,24,29,65),1));
            artLayer.Children.Add(new Border{Background=dynamicGradient,IsHitTestVisible=false});
            root.Children.Add(artLayer);

            var content=new StackPanel{Margin=new Thickness(8,5,8,6)};
            mainHeader=new Border{Background=Brushes.Transparent,
                Cursor=Cursors.SizeAll,Padding=new Thickness(0,0,0,4),
                ToolTip="Drag this header to move the player anywhere"};
            var header=new DockPanel{LastChildFill=true};
            circleButton=Make("○","Minimize/restore the circle",ToggleCircle,24);
            DockPanel.SetDock(circleButton,Dock.Left);header.Children.Add(circleButton);
            minimizedButton=Make("−","Minimize to compact pill",TogglePanel,26);
            DockPanel.SetDock(minimizedButton,Dock.Right);header.Children.Add(minimizedButton);
            expandButton=Make("⌄","Show advanced playback controls",ToggleAdvanced,26);
            DockPanel.SetDock(expandButton,Dock.Right);header.Children.Add(expandButton);
            headerCaption=new TextBlock{Foreground=Brushes.White,FontSize=10,
                FontWeight=FontWeights.SemiBold,
                VerticalAlignment=VerticalAlignment.Center,
                TextTrimming=TextTrimming.CharacterEllipsis};
            header.Children.Add(headerCaption);
            mainHeader.Child=header;
            mainHeader.MouseLeftButtonDown+=DragDown;
            mainHeader.MouseMove+=DragMove;
            mainHeader.MouseLeftButtonUp+=DragUp;
            dragHeader=mainHeader;
            content.Children.Add(mainHeader);
            details=new StackPanel();
            contextText=new TextBlock{FontSize=10,Foreground=new SolidColorBrush(
                Color.FromRgb(230,230,250)),TextAlignment=TextAlignment.Center,
                TextTrimming=TextTrimming.CharacterEllipsis,MaxHeight=16};
            details.Children.Add(contextText);
            titleText=new TextBlock{FontSize=11,FontWeight=FontWeights.SemiBold,
                Foreground=Brushes.White,TextAlignment=TextAlignment.Center,
                TextTrimming=TextTrimming.CharacterEllipsis,MaxHeight=16,
                Margin=new Thickness(1,2,1,0)};
            artistText=new TextBlock{FontSize=9,Foreground=Brushes.White,
                Opacity=.9,TextAlignment=TextAlignment.Center,
                TextTrimming=TextTrimming.CharacterEllipsis,MaxHeight=14};
            var mediaRow=new Grid{Margin=new Thickness(0,3,0,3)};
            mediaRow.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
            mediaRow.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
            albumArt=new Image{Source=image,Stretch=Stretch.UniformToFill};
            mediaRow.Children.Add(new Border{Width=46,Height=46,CornerRadius=new CornerRadius(11),
                ClipToBounds=true,Background=new SolidColorBrush(Color.FromRgb(88,86,126)),
                Child=albumArt,Margin=new Thickness(0,0,8,0)});
            var captions=new StackPanel{VerticalAlignment=VerticalAlignment.Center};
            titleText.TextAlignment=TextAlignment.Left;
            artistText.TextAlignment=TextAlignment.Left;
            captions.Children.Add(titleText);captions.Children.Add(artistText);
            Grid.SetColumn(captions,1);mediaRow.Children.Add(captions);
            details.Children.Add(mediaRow);
            var transport=new StackPanel{Orientation=Orientation.Horizontal,
                HorizontalAlignment=HorizontalAlignment.Center,
                Margin=new Thickness(0,3,0,3)};
            previousButton=Make("«","Previous",()=>_=Control("previous"));
            transport.Children.Add(previousButton);
            rewindButton=Make("≪","Rewind",()=>_=Control("rewind"));
            transport.Children.Add(rewindButton);
            var play=Make("▶","Play/Pause",()=>_=Control("toggle"),41);
            playGlyph=new TextBlock{Text="▶",Foreground=Brushes.White,FontSize=15};
            play.Content=playGlyph;transport.Children.Add(play);
            fastButton=Make("≫","Fast forward",()=>_=Control("forward"));
            transport.Children.Add(fastButton);
            nextButton=Make("»","Next",()=>_=Control("next"));
            transport.Children.Add(nextButton);
            // R37: the accessible mute switch must not be hidden behind
            // Advanced. Reuse the existing Windows master-audio toggle.
            transport.Children.Add(Make("Mute","Mute or unmute system sound",
                ()=>Volume(0xAD),44));
            details.Children.Add(transport);
            var seekRow=new Grid{Margin=new Thickness(2,0,2,4)};
            seekRow.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
            seekRow.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
            seekRow.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
            elapsedText=new TextBlock{Foreground=Brushes.White,FontSize=9,MinWidth=34};
            durationText=new TextBlock{Foreground=Brushes.White,FontSize=9,
                MinWidth=34,TextAlignment=TextAlignment.Right};
            timeline=new Slider{Minimum=0,Maximum=1,Height=19,Margin=new Thickness(2,0,2,0)};
            timeline.PreviewMouseLeftButtonDown+=(_,_)=>draggingTimeline=true;
            timeline.PreviewMouseLeftButtonUp+=async (_,_)=>{draggingTimeline=false;await SeekAsync();};
            Grid.SetColumn(timeline,1);Grid.SetColumn(durationText,2);
            seekRow.Children.Add(elapsedText);seekRow.Children.Add(timeline);
            seekRow.Children.Add(durationText);details.Children.Add(seekRow);
            advancedContent=new StackPanel{Visibility=Visibility.Collapsed,Margin=new Thickness(0,7,0,0)};
            advancedContent.Children.Add(new Border{Height=1,
                Background=new SolidColorBrush(Color.FromArgb(65,255,255,255)),
                Margin=new Thickness(0,0,0,7)});
            var advanced=new StackPanel{Orientation=Orientation.Horizontal,
                HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,0,0,3)};
            shuffleButton=Make("🔀","Shuffle",()=>_=Control("shuffle"));
            repeatButton=Make("↻","Repeat",()=>_=Control("repeat"));
            speedButton=Make("1×","Playback speed",()=>_=Control("speed"),35);
            stopButton=Make("■","Stop",()=>_=Control("stop"));
            advanced.Children.Add(shuffleButton);advanced.Children.Add(repeatButton);
            advanced.Children.Add(speedButton);advanced.Children.Add(stopButton);
            advancedContent.Children.Add(advanced);
            var volume=new StackPanel{Orientation=Orientation.Horizontal,
                HorizontalAlignment=HorizontalAlignment.Center};
            volume.Children.Add(Make("−","Volume down",()=>Volume(0xAE),38));
            volume.Children.Add(Make("Mute","Mute or unmute system sound",()=>Volume(0xAD),47));
            volume.Children.Add(Make("+","Volume up",()=>Volume(0xAF),38));
            advancedContent.Children.Add(volume);
            details.Children.Add(advancedContent);
            content.Children.Add(details);
            pillContent=new StackPanel{Visibility=Visibility.Collapsed};
            pillHeader=new Border{Background=Brushes.Transparent,Cursor=Cursors.SizeAll,
                ToolTip="Drag the title to move minimized player"};
            var pillRow=new DockPanel{LastChildFill=true};
            var restore=Make("▢","Restore full media player",TogglePanel,26);
            DockPanel.SetDock(restore,Dock.Right);pillRow.Children.Add(restore);
            // R37: visible even in the 76px minimized media player.
            var pillMute=Make("Mute","Mute or unmute system sound",
                ()=>Volume(0xAD),37);
            DockPanel.SetDock(pillMute,Dock.Right);pillRow.Children.Add(pillMute);
            pillNextButton=Make("»","Next track",()=>_=Control("next"),27);
            DockPanel.SetDock(pillNextButton,Dock.Right);pillRow.Children.Add(pillNextButton);
            var miniPlayButton=Make("▶","Play or pause",()=>_=Control("toggle"),29);
            pillPlay=new TextBlock{Text="▶",FontSize=16,Foreground=Brushes.White};
            miniPlayButton.Content=pillPlay;
            DockPanel.SetDock(miniPlayButton,Dock.Right);pillRow.Children.Add(miniPlayButton);
            pillPreviousButton=Make("«","Previous track",()=>_=Control("previous"),27);
            DockPanel.SetDock(pillPreviousButton,Dock.Right);pillRow.Children.Add(pillPreviousButton);
            miniArt=new Image{Source=image,Stretch=Stretch.UniformToFill};
            var smallArt=new Border{Width=27,Height=27,CornerRadius=new CornerRadius(7),
                Background=new SolidColorBrush(Color.FromRgb(90,85,125)),
                ClipToBounds=true,Child=miniArt,Margin=new Thickness(0,0,7,0)};
            DockPanel.SetDock(smallArt,Dock.Left);pillRow.Children.Add(smallArt);
            pillTitle=new TextBlock{FontSize=11,Foreground=Brushes.White,
                FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center,
                TextTrimming=TextTrimming.CharacterEllipsis};
            pillRow.Children.Add(pillTitle);pillHeader.Child=pillRow;
            pillHeader.MouseLeftButtonDown+=DragDown;
            pillHeader.MouseMove+=DragMove;
            pillHeader.MouseLeftButtonUp+=DragUp;
            pillContent.Children.Add(pillHeader);
            pillSeek=new Slider{Minimum=0,Maximum=1,Height=17,
                Margin=new Thickness(4,1,4,0),
                ToolTip="Seek media"};
            pillSeek.PreviewMouseLeftButtonDown+=(_,_)=>draggingTimeline=true;
            pillSeek.PreviewMouseLeftButtonUp+=async (_,_)=>{
                if(pillSeek!=null && timeline!=null)timeline.Value=pillSeek.Value;
                draggingTimeline=false;await SeekAsync();
            };
            pillContent.Children.Add(pillSeek);
            content.Children.Add(pillContent);
            root.Children.Add(content);
            frame=new Border{Background=new SolidColorBrush(Color.FromArgb(246,18,25,52)),
                BorderBrush=new SolidColorBrush(Color.FromArgb(160,221,218,247)),
                BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(14),
                Width=256,Child=root,ClipToBounds=true,
                Effect=new DropShadowEffect{Color=Colors.Black,Opacity=.25,
                    BlurRadius=18,ShadowDepth=4}};
            if(!customPosition){offsetX=(window.Width-256)/2;offsetY=window.Height+5;}
            popup=new Popup{
                PlacementTarget=host,Placement=PlacementMode.Relative,
                HorizontalOffset=offsetX,VerticalOffset=offsetY,
                StaysOpen=true,AllowsTransparency=true,PopupAnimation=PopupAnimation.Fade,
                Child=frame
            };
            host.LocationChanged+=Reposition;
            UpdateLayout();
        }
        popup.IsOpen=host.IsVisible;
        KeepVisible();
        if(view!=Layout.Pill&&!visualTimer.IsEnabled)visualTimer.Start();
        UpdateView();
    }
    private void Reposition(object? sender,EventArgs e)
    {
        if(popup?.IsOpen==true)popup.HorizontalOffset=offsetX+(++moveTick%2==0?0:.01);
    }
    public void CloseBelow()
    {
        RestoreCircle();
        visualTimer.Stop();dragging=false;
        if(host!=null)host.LocationChanged-=Reposition;
        if(popup!=null)popup.IsOpen=false;
        popup=null;host=null;summary=null;frame=null;dragHeader=null;details=null;
        playerRoot=null;
        artLayer=null;dynamicGradient=null;artwork=null;
        contextText=titleText=artistText=playGlyph=elapsedText=durationText=headerCaption=null;
        previousButton=nextButton=stopButton=shuffleButton=repeatButton=rewindButton=fastButton=speedButton=null;
        minimizedButton=circleButton=expandButton=null;timeline=null;
        advancedContent=pillContent=null;mainHeader=pillHeader=null;
        miniArt=albumArt=null;pillTitle=pillPlay=null;
        pillSeek=null;pillPreviousButton=pillNextButton=null;
    }
    public void Dispose()
    {
        disposed=true;poller.Stop();CloseBelow();
        manager=null;current=null;VisibilityChanged=null;
    }
}
