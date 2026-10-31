$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r269/DeskSpaceUniversalSearchPolicyV269.cs' -Destination "$root/DeskSpaceUniversalSearchPolicyV269.cs" -Force
$path="$root/UniversalSearch.cs"
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
function Swap([string]$before,[string]$after,[string]$label)
{
 $before=$before.Replace($crlf,$lf);$after=$after.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($before))).Count
 if($n -ne 1){throw ("R269 "+$label+": expected one anchor, got "+$n)}
 $script:s=$script:s.Replace($before,$after)
}

Swap @'
    private readonly string[] settingsUris={
        "Display|ms-settings:display", "Sound|ms-settings:sound", "Bluetooth|ms-settings:bluetooth",
        "Network and Internet|ms-settings:network", "Wi-Fi|ms-settings:network-wifi", "Apps|ms-settings:appsfeatures",
        "Default applications|ms-settings:defaultapps", "Storage|ms-settings:storagesense",
        "Windows Update|ms-settings:windowsupdate", "Accounts|ms-settings:yourinfo",
        "Mouse|ms-settings:mousetouchpad", "Keyboard|ms-settings:typing",
        "Notifications|ms-settings:notifications", "Privacy|ms-settings:privacy",
        "Personalization|ms-settings:personalization", "Taskbar|ms-settings:taskbar",
        "Power|ms-settings:powersleep", "Date and time|ms-settings:dateandtime"
    };
'@ @'
    private int requestSequence;
'@ 'remove duplicate Windows settings list'

Swap @'
        category.ItemsSource=new[]{"All","Files","Applications","Settings","Notes","Commands"};category.SelectedIndex=0;
'@ @'
        category.ItemsSource=DeskSpaceUniversalSearchPolicyV269.Categories;category.SelectedIndex=0;
'@ 'use real category list'

Swap @'
        query.TextChanged+=(_,_)=>{debounce.Stop();debounce.Start();};
'@ @'
        query.TextChanged+=(_,_)=>{requestSequence++;searchCancel?.Cancel();debounce.Stop();debounce.Start();};
'@ 'cancel pending on each keystroke'

Swap @'
        category.SelectionChanged+=(_,_)=>{debounce.Stop();debounce.Start();};
'@ @'
        category.SelectionChanged+=(_,_)=>{requestSequence++;searchCancel?.Cancel();debounce.Stop();debounce.Start();};
'@ 'cancel pending on category'

Swap @'
        Add("Open",Launch);Add("Open folder",OpenFolder);Add("Copy path",CopyPath);
        Add("Copy file",CopyFile);Add("Pin application",Pin);
'@ @'
        Add("Open",Launch);Add("Open folder",OpenFolder);Add("Copy path",CopyPath);
        Add("Copy file",CopyFile);Add("Pin application",Pin);
        var actions=new Button{Content="Commands",Padding=new Thickness(10,5,10,5),
            ToolTip="Open the complete DeskSpace Action Palette",Margin=new Thickness(0,0,7,0)};
        actions.Click+=(_,_)=>DeskSpaceActionPaletteV197.Show(this,false);
        buttons.Children.Add(actions);
'@ 'working commands action'

Swap @'
        list.MouseDoubleClick+=(_,_)=>Selected(Launch);Loaded+=(_,_)=>query.Focus();
        Closed+=(_,_)=>{searchCancel?.Cancel();searchCancel?.Dispose();debounce.Stop();};
'@ @'
        list.MouseDoubleClick+=(_,_)=>Selected(Launch);Loaded+=(_,_)=>query.Focus();
        PreviewKeyDown+=(_,e)=>{
            if(e.Key==Key.Escape){Close();e.Handled=true;}
        };
        Closed+=(_,_)=>{requestSequence++;searchCancel?.Cancel();searchCancel?.Dispose();debounce.Stop();};
'@ 'escape and cancellation'

Swap @'
        if(hit.Category=="Notes") {Clipboard.SetText(hit.NoteText??"");MessageBox.Show("Note copied. Open DeskSpace to edit.");return;}
'@ @'
        if(hit.Category is "Notes" or "Reminders" or "Calendar" or "Information")
        {
            if(hit.Category=="Information" &&
               MessageBox.Show(this,"Copy this Information entry to the clipboard? "+
                   "Its contents may be private.","DeskSpace Search",
                   MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)
                return;
            Clipboard.SetText(hit.NoteText??hit.Name);
            MessageBox.Show(this,"Selected DeskSpace entry copied by request. "+
                "No file or app was launched.","DeskSpace Search");
            return;
        }
'@ 'safe DeskSpace entries action'

Swap @'
        if(!File.Exists(h.OpenTarget))return;
        Process.Start(new ProcessStartInfo("explorer.exe",$"/select,\"{h.OpenTarget}\""){UseShellExecute=true});
'@ @'
        if(h.Category!="Files")return;
        if(Directory.Exists(h.OpenTarget))
            Process.Start(new ProcessStartInfo(h.OpenTarget){UseShellExecute=true});
        else if(File.Exists(h.OpenTarget))
            Process.Start(new ProcessStartInfo("explorer.exe",$"/select,\"{h.OpenTarget}\""){UseShellExecute=true});
'@ 'open folder support'

Swap @'
    private void CopyPath(SearchHit h){Clipboard.SetText(string.IsNullOrWhiteSpace(h.Location)?h.OpenTarget:h.Location);}
'@ @'
    private void CopyPath(SearchHit h)
    {
        if(h.Category is "Files" or "Applications" && !string.IsNullOrWhiteSpace(h.OpenTarget))
            Clipboard.SetText(h.OpenTarget);
    }
'@ 'copy true path only'

$start=$s.IndexOf('    private async Task RunSearch()')
if($start -lt 0 -or !$s.Contains('SafeFiles(drive.RootDirectory.FullName,350000,token)')){
 throw 'R269 legacy full-drive per-keystroke scan anchor missing'
}
$new=@'
    private async Task RunSearch()
    {
        searchCancel?.Cancel();
        searchCancel?.Dispose();
        searchCancel=new CancellationTokenSource();
        var token=searchCancel.Token;
        int sequence=requestSequence;
        var term=query.Text.Trim();
        var selected=category.SelectedItem as string??"All";
        if(term.Length<2)
        {
            list.ItemsSource=null;
            status.Text="Enter two or more characters to search.";
            return;
        }
        status.Text="Searching apps, Windows settings and indexed names across ready drives…";
        // Capture Workspace only on the WPF thread; the single shared search
        // index performs file/application discovery off the UI thread.
        Workspace snapshot;
        try{snapshot=workspace();}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException)
        {
            status.Text="Workspace unavailable ("+ex.GetType().Name+").";
            return;
        }
        try
        {
            var indexed=await Task.Run(()=>
                DeskSpaceSearchIndexV13.Search(term,snapshot,token),token);
            if(!DeskSpaceUniversalSearchPolicyV269.Accept(
                sequence,requestSequence,token.IsCancellationRequested,
                term,query.Text.Trim(),selected,category.SelectedItem as string))
                return;
            var filtered=DeskSpaceUniversalSearchPolicyV269.Filter(indexed,selected);
            list.ItemsSource=filtered;
            status.Text=filtered.Count+" matching result(s)"+
                (DeskSpaceSearchIndexV13.IsIndexing
                    ?" · discovering additional drive names":"")+
                ". Select a result to act.";
        }
        catch(OperationCanceledException){}
        catch(Exception ex)
        {
            if(!token.IsCancellationRequested &&
               sequence==requestSequence)
                status.Text="Search unavailable ("+ex.GetType().Name+").";
        }
    }
}
'@.Replace($crlf,$lf)
$s=$s.Substring(0,$start)+$new+$lf
[IO.File]::WriteAllText($path,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R269: Workspace Search now uses progressive shared multi-drive index; no 350k-file rescan for each query, with cancel/stale-result protection.'
