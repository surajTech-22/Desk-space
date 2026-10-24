$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
$p="$r/DeskSpaceDropShelfV15.cs"
$s=[IO.File]::ReadAllText($p).Replace([string][char]13+[char]10,[string][char]10)
function Swap([string]$old,[string]$new,[string]$label)
{
 $old=$old.Replace([string][char]13+[char]10,[string][char]10)
 $new=$new.Replace([string][char]13+[char]10,[string][char]10)
 $count=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($count -ne 1){throw "R91 $label: expected one anchor, found $count"}
 $script:s=$script:s.Replace($old,$new)
}
Swap '    private bool appsExpanded=false,heldExpanded=true,recentExpanded=true;' @'
    private bool appsExpanded=false,heldExpanded=true,recentExpanded=true;
    private bool deckMinimized;
'@ 'minimise independent state'
# Header buttons use a true transparent WPF template, not themed white button chrome.
Swap '    private void Render()' @'
    private static Button DeckHeaderButton(string symbol,string tooltip,Action click)
    {
        var button=new Button{
            Content=symbol,ToolTip=tooltip,
            Width=22,Height=22,MinWidth=22,MinHeight=22,
            FontSize=13,FontWeight=FontWeights.Medium,
            Foreground=Brushes.LightSteelBlue,
            BorderThickness=new Thickness(0),
            Background=Brushes.Transparent,
            Padding=new Thickness(0),Margin=new Thickness(0,0,1,0),
            VerticalAlignment=VerticalAlignment.Center,
            HorizontalContentAlignment=HorizontalAlignment.Center,
            VerticalContentAlignment=VerticalAlignment.Center,
            Cursor=Cursors.Hand
        };
        var chrome=new FrameworkElementFactory(typeof(Border));
        chrome.Name="Chrome";
        chrome.SetValue(Border.CornerRadiusProperty,new CornerRadius(5));
        chrome.SetValue(Border.BackgroundProperty,
            new System.Windows.TemplateBindingExtension(
                System.Windows.Controls.Control.BackgroundProperty));
        var presenter=new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty,
            HorizontalAlignment.Center);
        presenter.SetValue(ContentPresenter.VerticalAlignmentProperty,
            VerticalAlignment.Center);
        chrome.AppendChild(presenter);
        button.Template=new ControlTemplate(typeof(Button)){VisualTree=chrome};
        button.MouseEnter+=(_,_)=>button.Background=new SolidColorBrush(
            Color.FromArgb(115,101,126,167));
        button.MouseLeave+=(_,_)=>button.Background=Brushes.Transparent;
        button.Click+=(_,_)=>click();
        return button;
    }
    private void Render()
'@ 'transparent blended controls'
Swap 'if(held.Count+detected.Count+runningApps.Count==0 && !busy)' @'
if(held.Count+detected.Count+runningApps.Count==0 && !busy && !deckMinimized)
'@ 'keep minimised header when list empties'
$start=$s.IndexOf('        var title=new DockPanel {Margin=new Thickness(5,0,3,4)};',
    [StringComparison]::Ordinal)
$end=$s.IndexOf('        var appsGroup=DeskSpaceShelfGroupsV88.Add(', $start,
    [StringComparison]::Ordinal)
if($start -lt 0 -or $end -le $start){throw 'R91 old deck title not found'}
$new=@'
        // Slim header is part of the existing floating deck. No second panel.
        var title=new DockPanel{
            Margin=new Thickness(1,0,1,3),
            Background=new SolidColorBrush(Color.FromArgb(145,34,48,79)),
            LastChildFill=true,MinHeight=26
        };
        var close=DeckHeaderButton("×",
            "Hide deck (restore via circle > Deck Visibility)",()=>{
            if(!DeskSpaceDeckVisibilityV89.TrySet(false,out var problem))
                MessageBox.Show("Could not hide the deck: "+problem,
                    "DeskSpace deck visibility");
        });
        DockPanel.SetDock(close,Dock.Right);
        title.Children.Add(close);
        var minimise=DeckHeaderButton(deckMinimized?"▢":"−",
            deckMinimized?"Expand deck":"Minimise deck to header",()=>{
                deckMinimized=!deckMinimized;
                Render();
            });
        DockPanel.SetDock(minimise,Dock.Right);
        title.Children.Add(minimise);
        var heading=Text(busy?"Copying…":
            "Deck · "+runningApps.Count+" apps · "+
            held.Count+" held · "+detected.Count+" new",
            11,Brushes.White);
        heading.Margin=new Thickness(6,0,0,0);
        heading.ToolTip="Running applications / held copies / recent files";
        title.Children.Add(heading);
        list.Children.Add(title);
        if(deckMinimized)
        {
            popup.IsOpen=host.IsVisible;
            return;
        }
'@.Replace([string][char]13+[char]10,[string][char]10)
$s=$s.Substring(0,$start)+$new+$s.Substring($end)
Swap '(held.Count+detected.Count+runningApps.Count)>0;}' @'
        (held.Count+detected.Count+runningApps.Count)>0 || deckMinimized;}
'@ 'keep minimised deck when widget reappears'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R91: merged dark on-header minimise/close buttons and no-panel minimised state; true Win32 top-level window enumeration.'
