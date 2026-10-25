$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
function Swap([string]$path,[string]$before,[string]$after,[string]$label){
 $lf=[string][char]10;$crlf=[string][char]13+$lf
 $source=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
 $before=$before.Replace($crlf,$lf);$after=$after.Replace($crlf,$lf)
 $n=([regex]::Matches($source,[regex]::Escape($before))).Count
 if($n -ne 1){throw "R123 $($label) expected one anchor, got $n"}
 [IO.File]::WriteAllText($path,$source.Replace($before,$after),[Text.UTF8Encoding]::new($false))
}
$fmt="$root/DeskSpaceExcelFormatApplyV109.cs"
Swap $fmt @'
    private static int ToInt(object? value)=>
        Convert.ToInt32(value,CultureInfo.InvariantCulture);
'@ @'
    private static int ToInt(object? value)=>
        Convert.ToInt32(value,CultureInfo.InvariantCulture);
    // Excel requires internal ListObject column names. They must not be
    // copied from the saved source, and the destination's own names
    // must never be compared against the captured workbook's headers.
    internal static string StructuralHeaderV123(int column)=>
        "Column"+(column+1).ToString(CultureInfo.InvariantCulture);
    internal static bool HeadersIndependentV123(
        IEnumerable<string> destination,IEnumerable<string> source)=>
        destination.All(x=>!string.IsNullOrWhiteSpace(x)) &&
        destination.Distinct(StringComparer.OrdinalIgnoreCase).Count()==
            destination.Count();
'@ 'independent generated Excel Table headers'
Swap $fmt @'
            if(!t.HasHeaders || t.ColumnNames.Count!=t.ColumnCount ||
               t.ColumnNames.Any(x=>string.IsNullOrWhiteSpace(x)) ||
               t.ColumnNames.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=t.ColumnCount)
                problems.Add("A source Excel Table has missing/duplicate header metadata.");
'@ @'
            // The recorded table footprint is structural. Header TEXT is
            // value data and is irrelevant for format/formula application.
            if(!t.HasHeaders || t.ColumnCount<1)
                problems.Add("A source Excel Table lacks a valid header row structure.");
'@ 'ignore captured header labels for table layout'
Swap $fmt @'
                    if(sourceTable.ColumnNames.Count!=tw)
                        problems.Add("Source Excel Table column names were not captured.");
                    else
                        for(int headerNo=0;headerNo<tw;headerNo++)
                            if(!string.Equals(Text(table.ListColumns[headerNo+1].Name),
                                sourceTable.ColumnNames[headerNo],StringComparison.Ordinal))
                                problems.Add("Destination Excel Table header layout does not match its source.");
'@ @'
                    // Existing tables retain their OWN column names.
                    // Only geometric/header visibility compatibility matters.
                    var currentNames=new string[tw];
                    for(int headerIndex=0;headerIndex<tw;headerIndex++)
                        currentNames[headerIndex]=Text(
                            (object?)table.ListColumns[headerIndex+1].Name);
                    if(!HeadersIndependentV123(currentNames,sourceTable.ColumnNames))
                        problems.Add("Destination Excel Table has blank/duplicate column names.");
'@ 'stop source-to-destination label comparison'
Swap $fmt @'
                    if(!string.Equals(Text((object?)header.Value2),
                        sourceTable.ColumnNames[c],StringComparison.Ordinal))
                        problems.Add("Cannot create an Excel Table over populated data: "+
                            "existing column headers differ from captured structural headers.");
'@ @'
                    var label=Text((object?)header.Value2);
                    if(string.IsNullOrWhiteSpace(label))
                        problems.Add("Cannot create an Excel Table over populated data with blank destination column names.");
'@ 'retain populated destination labels not captured source'
Swap $fmt @'
                for(int c=0;c<sourceTable.ColumnCount;c++)
                {
                    dynamic header=target.Cells[sourceTable.Row+1,
                        sourceTable.Column+c+1];
                    var label=Text((object?)header.Value2);
                    if(string.IsNullOrWhiteSpace(label))
                        problems.Add("Cannot create an Excel Table over populated data with blank destination column names.");
                }
'@ @'
                var destinationLabels=new List<string>();
                for(int c=0;c<sourceTable.ColumnCount;c++)
                {
                    dynamic header=target.Cells[sourceTable.Row+1,
                        sourceTable.Column+c+1];
                    destinationLabels.Add(Text((object?)header.Value2));
                }
                if(!HeadersIndependentV123(destinationLabels,sourceTable.ColumnNames))
                    problems.Add("Populated destination Excel Table needs unique, nonempty OWN column names, not source headers.");
'@ 'protect user data when creating tables over populated ranges'
Swap $fmt @'
                      " structural table headers and "+
'@ @'
                      " Excel-generated neutral table column names and "+
'@ 'correct blank table preview'
Swap $fmt @'
                          "Create "+features.Tables.Count+" real table(s) using already matching headers; ":"")+
'@ @'
                          "Create "+features.Tables.Count+" real table(s) using destination's existing columns; ":"")+
'@ 'no cross-sheet header prompt'
Swap $fmt @'
            if(empty)
                for(int i=0;i<table.ColumnCount;i++)
                    region.Cells[1,i+1].Value2=table.ColumnNames[i];
'@ @'
            if(empty)
                for(int i=0;i<table.ColumnCount;i++)
                    region.Cells[1,i+1].Value2=StructuralHeaderV123(i);
'@ 'never write source labels into new target'
Swap $fmt @'
            for(int i=0;i<table.ColumnCount;i++)
                if(!string.Equals(Text(created.ListColumns[i+1].Name),
                    table.ColumnNames[i],StringComparison.Ordinal))
                    throw new InvalidDataException(
                        "Excel changed a required table header during creation.");
'@ @'
            // Excel may normalise its own generated names. Ensure unique
            // valid structural names without demanding SOURCE header data.
            var createdNames=new string[table.ColumnCount];
            for(int i=0;i<table.ColumnCount;i++)
                createdNames[i]=Text((object?)created.ListColumns[i+1].Name);
            if(!HeadersIndependentV123(createdNames,table.ColumnNames))
                throw new InvalidDataException(
                    "Excel Table creation produced invalid structural names.");
'@ 'verify actual Excel Table names independent of captured values'
Swap $fmt @'
                            header=t.ColumnNames[s.Column-t.Column];
'@ @'
                            header=StructuralHeaderV123(s.Column-t.Column);
'@ 'verify generated neutral table headers on blank target'
Swap $fmt @'
                    x.ShowAutoFilter==expectedTable.ShowAutoFilter &&
                    x.ColumnNames.SequenceEqual(expectedTable.ColumnNames)))
'@ @'
                    x.ShowAutoFilter==expectedTable.ShowAutoFilter &&
                    HeadersIndependentV123(x.ColumnNames,expectedTable.ColumnNames)))
'@ 'read back real table geometry and own names, not captured headers'
$media="$root/DeskSpaceMediaV18.cs"
Swap $media @'
    public bool Visible=>visible;
'@ @'
    internal static DeskSpaceMediaV18? ActiveV123 {get;private set;}
    internal static bool DeckIntegrationV123 {get;set;}
    private bool dismissedV123;
    internal bool DeckVisibleV123=>visible&&!dismissedV123;
    internal string DeckTitleV123=>title;
    internal string DeckArtistV123=>artist;
    internal bool DeckPlayingV123=>playing;
    internal string DeckStampV123=>visible+"|"+dismissedV123+"|"+
        playing+"|"+title+"|"+artist;
    internal void DeckCommandV123(string command)
    {
        if(command=="mute")Volume(0xAD);
        else _=Control(command);
    }
    internal void DismissPanelV123()
    {
        dismissedV123=true;
        CloseBelow();
        VisibilityChanged?.Invoke(false);
    }
    public bool Visible=>visible;
'@ 'share playback state and commands with deck'
Swap $media @'
    public DeskSpaceMediaV18()
    {
        poller=new DispatcherTimer(DispatcherPriority.Background)
'@ @'
    public DeskSpaceMediaV18()
    {
        ActiveV123=this;
        poller=new DispatcherTimer(DispatcherPriority.Background)
'@ 'publish active media session for integrated deck'
Swap $media @'
            string newTitle="";
'@ @'
            string newTitle="";
            string priorIdentity=source+"|"+title;
'@ 'record changed track to reset dismissed card'
Swap $media @'
            title=string.IsNullOrWhiteSpace(newTitle)?
                (string.IsNullOrWhiteSpace(source)?"Media playing":source):newTitle;
'@ @'
            title=string.IsNullOrWhiteSpace(newTitle)?
                (string.IsNullOrWhiteSpace(source)?"Media playing":source):newTitle;
            if(priorIdentity!=source+"|"+title)dismissedV123=false;
'@ 'reveal player for a genuinely new track'
Swap $media @'
    public void ShowBelow(Window window,Func<string> previousModuleData)
    {
        if(disposed||!visible)return;
'@ @'
    public void ShowBelow(Window window,Func<string> previousModuleData)
    {
        if(disposed||!visible||dismissedV123)return;
        // This deck owns the media player. Do not create another floating
        // overlay that competes with the deck's running-apps and Excel groups.
        if(DeckIntegrationV123)
        {
            if(popup!=null)CloseBelow();
            return;
        }
'@ 'eliminate independent media overlay while deck is integrated'
Swap $media @'
            minimizedButton=Make("−","Minimize to compact pill",TogglePanel,26);
'@ @'
            var closeMedia=Make("×","Close media player (playback continues)",
                DismissPanelV123,26);
            DockPanel.SetDock(closeMedia,Dock.Right);header.Children.Add(closeMedia);
            minimizedButton=Make("−","Minimize to compact pill",TogglePanel,26);
'@ 'close option on expanded fallback media panel'
Swap $media @'
            var restore=Make("▢","Restore full media player",TogglePanel,26);
'@ @'
            var pillClose=Make("×","Close media player (playback continues)",
                DismissPanelV123,26);
            DockPanel.SetDock(pillClose,Dock.Right);pillRow.Children.Add(pillClose);
            var restore=Make("▢","Restore full media player",TogglePanel,26);
'@ 'close option on minimized fallback media player'
Swap $media @'
        disposed=true;poller.Stop();CloseBelow();
'@ @'
        disposed=true;poller.Stop();CloseBelow();
        if(ReferenceEquals(ActiveV123,this))ActiveV123=null;
'@ 'clean up global media reference on dispose'
$shelf="$root/DeskSpaceDropShelfV15.cs"
Swap $shelf @'
    private void Render()
'@ @'
    // Only refresh the deck when the media title/playback state actually
    // changes. The transport controls never auto-start or interrupt playback.
    private readonly DispatcherTimer mediaDeckTimerV123=new(){
        Interval=TimeSpan.FromSeconds(1.8)
    };
    private string mediaDeckStampV123="";
    private bool mediaDeckTimerConnectedV123;
    private void RefreshMediaDeckV123()
    {
        if(disposed)return;
        var player=DeskSpaceMediaV18.ActiveV123;
        var stamp=player?.DeckStampV123??"";
        if(stamp==mediaDeckStampV123)return;
        mediaDeckStampV123=stamp;
        Render();
    }
    private void Render()
'@ 'add media-aware deck without altering other groups'
Swap $shelf @'
    private void Render()
    {
        if(disposed)return;
'@ @'
    private void Render()
    {
        if(disposed)return;
        DeskSpaceMediaV18.DeckIntegrationV123=true;
        if(!mediaDeckTimerConnectedV123)
        {
            mediaDeckTimerConnectedV123=true;
            mediaDeckTimerV123.Tick+=(_,_)=>RefreshMediaDeckV123();
            mediaDeckTimerV123.Start();
        }
'@ 'enable deck-owned player on the current Render lifecycle'
Swap $shelf @'
            excelMatchesV122.Count==0 && !busy)
'@ @'
            excelMatchesV122.Count==0 &&
            DeskSpaceMediaV18.ActiveV123?.DeckVisibleV123!=true && !busy)
'@ 'show deck when media is the only active item'
Swap $shelf @'
        (held.Count+detected.Count+runningApps.Count+
        excelMatchesV122.Count)>0;}
'@ @'
        (held.Count+detected.Count+runningApps.Count+
        excelMatchesV122.Count)>0 ||
        DeskSpaceMediaV18.ActiveV123?.DeckVisibleV123==true;}
'@ 'show deck when media is active'
Swap $shelf @'
        var suggestionGroup=DeskSpaceShelfGroupsV88.Add(list,
'@ @'
        var activeMedia=DeskSpaceMediaV18.ActiveV123;
        if(activeMedia?.DeckVisibleV123==true)
        {
            var mediaPanel=new StackPanel{Margin=new Thickness(3,3,3,5)};
            var header=new DockPanel{LastChildFill=true};
            var mediaCloseButtonV123=new Button{Content="×",FontSize=13,
                MinWidth=23,Padding=new Thickness(3,1,3,1),
                ToolTip="Close media controls without stopping playback"};
            mediaCloseButtonV123.Click+=(_,_)=>{activeMedia.DismissPanelV123();Render();};
            DockPanel.SetDock(mediaCloseButtonV123,Dock.Right);
            header.Children.Add(mediaCloseButtonV123);
            header.Children.Add(Text("♫ Media Player",12,Brushes.White));
            mediaPanel.Children.Add(header);
            var track=Text(activeMedia.DeckTitleV123,11,Brushes.White);
            track.ToolTip=activeMedia.DeckTitleV123;
            mediaPanel.Children.Add(track);
            if(!string.IsNullOrWhiteSpace(activeMedia.DeckArtistV123))
                mediaPanel.Children.Add(Text(activeMedia.DeckArtistV123,10,
                    Brushes.LightSteelBlue));
            var transport=new WrapPanel{
                HorizontalAlignment=HorizontalAlignment.Center,
                Margin=new Thickness(0,4,0,0)};
            void Action(string label,string command,string tooltip)
            {
                var b=new Button{Content=label,MinWidth=40,Height=29,
                    Padding=new Thickness(3),Margin=new Thickness(2),
                    ToolTip=tooltip};
                b.Click+=(_,_)=>activeMedia.DeckCommandV123(command);
                transport.Children.Add(b);
            }
            Action("«","previous","Previous track");
            Action(activeMedia.DeckPlayingV123?"Ⅱ":"▶","toggle","Play / Pause");
            Action("»","next","Next track");
            Action("Mute","mute","Mute or unmute system audio");
            Action("■","stop","Stop playback");
            mediaPanel.Children.Add(transport);
            list.Children.Add(new Border{
                Background=new SolidColorBrush(Color.FromRgb(39,53,83)),
                BorderBrush=new SolidColorBrush(Color.FromRgb(72,88,125)),
                BorderThickness=new Thickness(1),
                CornerRadius=new CornerRadius(7),Child=mediaPanel,
                Margin=new Thickness(2,3,2,4)
            });
        }
        var suggestionGroup=DeskSpaceShelfGroupsV88.Add(list,
'@ 'mount actual playback controls inside the shared deck'
Swap $shelf @'
        excelSuggestTimerV122.Stop();
'@ @'
        excelSuggestTimerV122.Stop();
        mediaDeckTimerV123.Stop();
        DeskSpaceMediaV18.DeckIntegrationV123=false;
'@ 'stop deck media state watcher when widget closes'
$smoke='r36/UiSmoke/Program.cs'
Swap $smoke @'
            TestContextualFeaturesV122();
'@ @'
            TestContextualFeaturesV122();
            TestFormatHeadersAndDeckV123();
'@ 'run actual header-independent format helper regression'
$method=@'
    private static void TestFormatHeadersAndDeckV123()
    {
        var fmt=Product.GetType("DeskSpace.DeskSpaceExcelFormatApplyV109",true)!;
        var check=fmt.GetMethod("HeadersIndependentV123",
            BindingFlags.NonPublic|BindingFlags.Static)!;
        bool CanUse(string[] dest,string[] source)=>
            (bool)check.Invoke(null,new object[]{dest,source})!;
        Assert(CanUse(new[]{"Date","Amount"},
                      new[]{"Invoice no","Tax"}),
            "R123 destination Table columns must NOT have to match source headers");
        Assert(CanUse(new[]{"MyHeader1","MyHeader2"},
                      Array.Empty<string>()),
            "R123 format-only templates require no saved header labels");
        Assert(!CanUse(new[]{"","Amount"},new[]{"A","B"}) &&
               !CanUse(new[]{"Dup","dup"},new[]{"X","Y"}),
            "R123 preserve Excel Table structural safety: no missing/duplicate destination headers");
        var generator=fmt.GetMethod("StructuralHeaderV123",
            BindingFlags.NonPublic|BindingFlags.Static)!;
        Assert((string)generator.Invoke(null,new object[]{0})!=="Column1" &&
               (string)generator.Invoke(null,new object[]{1})!=="Column2",
            "R123 blank Table gets neutral structural names, not source data");
        var media=Product.GetType("DeskSpace.DeskSpaceMediaV18",true)!;
        Assert(media.GetMethod("DeckCommandV123",BindingFlags.NonPublic|
                   BindingFlags.Instance)!=null &&
               media.GetMethod("DismissPanelV123",BindingFlags.NonPublic|
                   BindingFlags.Instance)!=null &&
               media.GetProperty("DeckVisibleV123",BindingFlags.NonPublic|
                   BindingFlags.Instance)!=null,
            "R123 deck exposes actual session playback commands and non-stop close");
        Console.WriteLine("R123 PASS: source-header independent Table metadata and integrated media close/controls.");
    }

'@
Swap $smoke @'
    private static void TestExcelTypeAwareSheetV101()
'@ ($method+'    private static void TestExcelTypeAwareSheetV101()') 'WPF runtime regression for header-neutral formats and player controls'
Write-Host 'R123: Excel formatting ignores source headers, preserving structural safety; media player controls + Close are inside existing deck.'
