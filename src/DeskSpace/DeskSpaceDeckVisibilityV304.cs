using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using System.Runtime.InteropServices;

namespace DeskSpace;

// R304: The Running Apps Deck is a separate native popup HWND, NOT part of
// the always-on-top WidgetShellWindow. Chrome/YouTube can cover it unless
// the popup itself is kept in the topmost band while it is open.
// No foreground activation, no focus stealing, no periodic polling.
internal static class DeskSpaceDeckVisibilityV304
{
    private static readonly IntPtr Topmost=new(-1);
    private const uint NoMove=0x0002,NoSize=0x0001,NoActivate=0x0010;
    private const int ExtendedStyle=-20;
    private const uint WsExTopmost=0x00000008;

    [DllImport("user32.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,
        int x,int y,int cx,int cy,uint flags);
    [DllImport("user32.dll",SetLastError=true)]
    private static extern int GetWindowLong(IntPtr hwnd,int index);

    internal static bool IsNativeTopmostV304(IntPtr hwnd) =>
        hwnd!=IntPtr.Zero && ((uint)GetWindowLong(hwnd,ExtendedStyle)&WsExTopmost)!=0;

    internal static bool EnsureNativeTopmostV304(IntPtr hwnd)
    {
        if(hwnd==IntPtr.Zero)return false;
        if(IsNativeTopmostV304(hwnd))return true;
        return SetWindowPos(hwnd,Topmost,0,0,0,0,
            NoMove|NoSize|NoActivate) && IsNativeTopmostV304(hwnd);
    }

    // Thin dark scrollbar without the bright Windows up/down buttons.
    // Retain a draggable accessible thumb and mouse/touchpad wheel input.
    internal static Style DeckScrollbarStyleV304()
    {
        const string style=@"
<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
       xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
       TargetType='{x:Type ScrollBar}'>
 <Setter Property='Width' Value='11'/>
 <Setter Property='Background' Value='#203251'/>
 <Setter Property='Template'>
  <Setter.Value>
   <ControlTemplate TargetType='{x:Type ScrollBar}'>
    <Border Background='#203251' Padding='2,3' CornerRadius='5'>
     <Track x:Name='PART_Track' IsDirectionReversed='True'
         Minimum='{TemplateBinding Minimum}'
         Maximum='{TemplateBinding Maximum}'
         Value='{TemplateBinding Value}'
         ViewportSize='{TemplateBinding ViewportSize}'>
      <Track.DecreaseRepeatButton>
       <RepeatButton Command='ScrollBar.PageUpCommand'
         Opacity='0' Focusable='False'/>
      </Track.DecreaseRepeatButton>
      <Track.Thumb>
       <Thumb>
        <Thumb.Template>
         <ControlTemplate TargetType='{x:Type Thumb}'>
          <Border CornerRadius='4' Background='#8B9EBE' MinHeight='20'/>
         </ControlTemplate>
        </Thumb.Template>
       </Thumb>
      </Track.Thumb>
      <Track.IncreaseRepeatButton>
       <RepeatButton Command='ScrollBar.PageDownCommand'
         Opacity='0' Focusable='False'/>
      </Track.IncreaseRepeatButton>
     </Track>
    </Border>
   </ControlTemplate>
  </Setter.Value>
 </Setter>
</Style>";
        return (Style)XamlReader.Parse(style);
    }

    internal static void Attach(Window owner,Popup popup,Border panel)
    {
        // Opacity-animation can leave a stale translucent/squeezed frame
        // over Chrome when the popup is repositioned at a monitor seam.
        popup.PopupAnimation=PopupAnimation.None;
        panel.SnapsToDevicePixels=true;
        // Only clip the content to the rounded Deck; monitor confinement
        // remains the responsibility of the existing native R153/R298 fence.
        panel.ClipToBounds=true;

        if(panel.Child is ScrollViewer scroll)
        {
            scroll.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;
            scroll.HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled;
            scroll.CanContentScroll=false;
            scroll.PanningMode=PanningMode.VerticalOnly;
            scroll.Resources[typeof(ScrollBar)]=DeckScrollbarStyleV304();
        }

        void Lift()
        {
            if(!owner.Dispatcher.CheckAccess() || !popup.IsOpen ||
               !owner.IsVisible || popup.Child==null)return;
            if(PresentationSource.FromVisual(popup.Child) is HwndSource src &&
                !src.IsDisposed)
                EnsureNativeTopmostV304(src.Handle);
        }

        void ScheduleLift()
        {
            if(!popup.IsOpen)return;
            owner.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,
                new Action(Lift));
        }
        popup.Opened+=(_,_)=>ScheduleLift();
        panel.SizeChanged+=(_,_)=>ScheduleLift();
        owner.IsVisibleChanged+=(_,e)=>{
            if(e.NewValue is bool visible && visible)ScheduleLift();
        };
        // Each window independently owns its Deck; no cross-monitor
        // global popup ref or forced foreground window activation.
        if(popup.IsOpen)ScheduleLift();
    }
}
