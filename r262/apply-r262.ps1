$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r262/DeskSpacePinnedDocumentChoiceV262.cs' -Destination "$root/DeskSpacePinnedDocumentChoiceV262.cs" -Force
Copy-Item -LiteralPath 'r262/DeskSpacePinnedRecentPickerV262.cs' -Destination "$root/DeskSpacePinnedRecentPickerV262.cs" -Force
$lf=[string][char]10;$crlf=[string][char]13+$lf
function Swap([string]$path,[string]$old,[string]$new,[string]$why)
{
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $text=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
 $n=([regex]::Matches($text,[regex]::Escape($old))).Count
 if($n -ne 1){throw ("R262 "+$why+": expected one anchor, got "+$n)}
 [IO.File]::WriteAllText($path,$text.Replace($old,$new),
     [Text.UTF8Encoding]::new($false))
}
Swap "$root/DeskSpacePinnedRecentV122.cs" @'
    private static List<Record> ReadCache()
'@ @'
    // Explicit picker queries only this pinned app on demand. It does not
    // update or read the persistent single-latest-document cache.
    internal static IReadOnlyList<DeskSpacePinnedDocumentChoiceV262.Candidate>
        CandidatesV262(string pinned)
    {
        if(!File.Exists(pinned))return Array.Empty<DeskSpacePinnedDocumentChoiceV262.Candidate>();
        string executable=PinnedExe(pinned);
        if(!File.Exists(executable) ||
            !Path.GetExtension(executable).Equals(".exe",
                StringComparison.OrdinalIgnoreCase))
            return Array.Empty<DeskSpacePinnedDocumentChoiceV262.Candidate>();
        try
        {
            var folder=Environment.GetFolderPath(
                Environment.SpecialFolder.Recent);
            if(!Directory.Exists(folder))
                return Array.Empty<DeskSpacePinnedDocumentChoiceV262.Candidate>();
            var entries=new List<DeskSpacePinnedDocumentChoiceV262.Candidate>();
            foreach(var shortcut in Directory.EnumerateFiles(folder,"*.lnk")
                .OrderByDescending(File.GetLastWriteTimeUtc).Take(70))
            {
                string target=ShortcutTarget(shortcut);
                if(!DeskSpacePinnedDocumentChoiceV262.SafeDocument(target))
                    continue;
                entries.Add(new(target,File.GetLastWriteTimeUtc(shortcut),
                    AssociatedExe(target)));
            }
            return DeskSpacePinnedDocumentChoiceV262.Filter(executable,entries);
        }
        catch(Exception ex)when(ex is IOException or UnauthorizedAccessException
            or ArgumentException)
        {return Array.Empty<DeskSpacePinnedDocumentChoiceV262.Candidate>();}
    }

    internal static bool CanLaunchRecentV262(string pinned,string document)
    {
        if(!File.Exists(pinned) ||
            !DeskSpacePinnedDocumentChoiceV262.SafeDocument(document))
            return false;
        string executable=PinnedExe(pinned);
        return File.Exists(executable) &&
            DeskSpacePinnedRecentV122.SameExecutableV122(
                AssociatedExe(document),executable);
    }

    private static List<Record> ReadCache()
'@ 'bounded on-demand Windows Recent inspection and revalidation'
Swap "$root/DeskSpaceApplicationsV55.cs" @'
        canvas.PreviewMouseLeftButtonDown+=Down;
'@ @'
        canvas.PreviewMouseLeftButtonDown+=Down;
        // Left click retains the original open-latest pinned action. A
        // RIGHT click on that same pinned arc explicitly opens the recent
        // document choice list; no background discovery is scheduled.
        canvas.PreviewMouseRightButtonDown+=RecentRightClickV262;
'@ 'discoverable pinned app right-click action'
Swap "$root/DeskSpaceApplicationsV55.cs" @'
    private void Down(object sender,MouseButtonEventArgs e)
'@ @'
    private void RecentRightClickV262(object sender,MouseButtonEventArgs e)
    {
        if(disposed || !arc.IsOpen)return;
        int index=Slot(e.GetPosition(canvas));
        if(index<0 || hitZones[index].Kind!="launch")return;
        string pinnedPath=hitZones[index].Path;
        if(!IsLaunchable(pinnedPath))return;
        HideArc();
        owner.Dispatcher.BeginInvoke(new Action(()=>
            DeskSpacePinnedRecentPickerV262.Show(owner,pinnedPath)));
        e.Handled=true;
    }
    private void Down(object sender,MouseButtonEventArgs e)
'@ 'right click safely opens per-app recent list without changing left-click'
Write-Host 'R262: explicit pinned-app right-click opens validated Windows Recent chooser; original left-click remains.'
