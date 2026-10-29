$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r193/DeskSpaceSearchActionsV193.cs' "$root/DeskSpaceSearchActionsV193.cs" -Force
$p="$root/DeskSpaceSearchV11.cs"
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Once([string]$from,[string]$to,[string]$reason)
{
 $from=$from.Replace($crlf,$lf);$to=$to.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($from))).Count
 if($n -ne 1){throw ("R193 "+$reason+": expected one source anchor, got "+$n)}
 $script:s=$script:s.Replace($from,$to)
}
Once @'
        var list =new StackPanel();
'@ @'
        var list =new StackPanel();
        int selectedIndex=0;
        void HighlightSearchResult()
        {
            int count=list.Children.Count;
            if(count<=0)return;
            selectedIndex=Math.Clamp(selectedIndex,0,count-1);
            for(int i=0;i<count;i++)
                if(list.Children[i] is Border row)
                    row.Background=i==selectedIndex?
                        new SolidColorBrush(Color.FromRgb(234,226,252)):Brushes.White;
            if(list.Children[selectedIndex] is FrameworkElement chosen)
                chosen.BringIntoView();
        }
'@ 'accessible selected row highlight'
Once @'
        void Do(SearchHit hit,string operation)
'@ @'
        async void Do(SearchHit hit,string operation)
'@ 'nonblocking explicit zip command'
Once @'
                    case "folder":
'@ @'
                    case "zip":
                        if(!DeskSpaceSearchActionsV193.CanZip(hit.OpenTarget))break;
                        var downloads=Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                            "Downloads");
                        var picker=new Microsoft.Win32.SaveFileDialog{
                            Title="Create ZIP of selected file",
                            FileName=Path.GetFileName(hit.OpenTarget)+".zip",
                            Filter="ZIP archive (*.zip)|*.zip",DefaultExt=".zip",
                            AddExtension=true,OverwritePrompt=false,
                            InitialDirectory=Directory.Exists(downloads)?downloads:
                                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)};
                        if(picker.ShowDialog()==true)
                        {
                            status.Text="Creating ZIP of "+hit.Name+"…";
                            await Task.Run(()=>DeskSpaceSearchActionsV193.ZipSingleFile(
                                hit.OpenTarget,picker.FileName));
                            if(!disposed)status.Text="Created ZIP: "+Path.GetFileName(picker.FileName);
                        }
                        break;
                    case "folder":
'@ 'ZIP action is explicit and asynchronous'
Once @'
                if(File.Exists(hit.OpenTarget))
                    chips.Children.Add(Chip("▤","Copy file",hit,"file"));
'@ @'
                if(File.Exists(hit.OpenTarget))
                    chips.Children.Add(Chip("▤","Copy file",hit,"file"));
                if(hit.Category=="Files" &&
                    DeskSpaceSearchActionsV193.CanZip(hit.OpenTarget))
                    chips.Children.Add(Chip("ZIP","Save a ZIP of this file",hit,"zip"));
'@ 'file-only ZIP smart action'
Once @'
                layout.Children.Add(text);item.Child=layout;
'@ @'
                layout.Children.Add(text);item.Child=layout;
                 System.Windows.Automation.AutomationProperties.SetName(
                     item,hit.Category+": "+hit.Name);
                 System.Windows.Automation.AutomationProperties.SetHelpText(
                     item,"Use Search Up/Down arrows and Enter to open; F8 ZIP for ordinary files.");
'@ 'accessible result row semantics'
Once @'
            below.Visibility=Visibility.Visible;SetSize();
        }
        field.KeyDown+=(_,e)=>{
'@ @'
            below.Visibility=Visibility.Visible;SetSize();
             selectedIndex=0;
             HighlightSearchResult();
        }
        field.KeyDown+=(_,e)=>{
             if(hits.Count>0 &&
                (e.Key==Key.Down||e.Key==Key.Up||
                 e.Key==Key.PageDown||e.Key==Key.PageUp))
             {
                 int step=e.Key==Key.Down?1:e.Key==Key.Up?-1:
                     e.Key==Key.PageDown?7:-7;
                 selectedIndex=DeskSpaceSearchActionsV193.NextIndex(
                     list.Children.Count,selectedIndex,step);
                 HighlightSearchResult();
                 e.Handled=true;
                 return;
             }
             if(e.Key==Key.F8 && hits.Count>0 &&
                DeskSpaceSearchActionsV193.CanZip(
                    hits[Math.Clamp(selectedIndex,0,hits.Count-1)].OpenTarget))
             {
                 Do(hits[Math.Clamp(selectedIndex,0,hits.Count-1)],"zip");
                 e.Handled=true;
                 return;
             }
'@ 'arrow and page navigation'
Once @'
            else if(e.Key==Key.Enter && hits.Count>0){Do(hits[0],"open");e.Handled=true;}
'@ @'
             else if(e.Key==Key.Enter && hits.Count>0){
                 Do(hits[Math.Clamp(selectedIndex,0,hits.Count-1)],"open");
                 e.Handled=true;
             }
'@ 'Enter launches keyboard-selected result'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R193: keyboard navigable search results, accessible labels and explicit asynchronous ZIP action without mutating original file.'
