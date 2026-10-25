$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
function Swap([string]$file,[string]$before,[string]$after,[string]$label){
 $lf=[string][char]10;$crlf=[string][char]13+$lf
 $s=[IO.File]::ReadAllText($file).Replace($crlf,$lf)
 $before=$before.Replace($crlf,$lf);$after=$after.Replace($crlf,$lf)
 $count=([regex]::Matches($s,[regex]::Escape($before))).Count
 if($count -ne 1){throw "R122 $($label): expected 1 anchor, got $count"}
 [IO.File]::WriteAllText($file,$s.Replace($before,$after),
     [Text.UTF8Encoding]::new($false))
}
Copy-Item 'r122/DeskSpacePinnedRecentV122.cs' "$r/DeskSpacePinnedRecentV122.cs" -Force
Copy-Item 'r122/DeskSpaceExcelActiveV122.cs' "$r/DeskSpaceExcelActiveV122.cs" -Force
$app="$r/DeskSpaceApplicationsV55.cs"
Swap $app @'
                    try{Process.Start(new ProcessStartInfo(item.Path){UseShellExecute=true});}
'@ @'
                    try{DeskSpacePinnedRecentV122.OpenLatestOrApp(item.Path);}
'@ 'pinned app opens last recently-used matching document, otherwise application'
$shelf="$r/DeskSpaceDropShelfV15.cs"
Swap $shelf @'
    private async void RefreshRunningApps()
    {
'@ @'
    private async void RefreshRunningApps()
    {
        if(!disposed && !excelSuggestTimerConnectedV122)
        {
            excelSuggestTimerConnectedV122=true;
            excelSuggestTimerV122.Tick+=(_,_)=>RefreshExcelSuggestionsV122();
            excelSuggestTimerV122.Start();
            RefreshExcelSuggestionsV122();
        }
'@ 'start suggestions alongside existing running-app refresh'
Swap $shelf @'
    // Scan in the background. Do not block circle hover, dragging, or file drops.
    private async void RefreshRunningApps()
'@ @'
    // Excel suggestion state is independent of Running Apps expansion.
    private bool excelExpandedV122=true,excelRefreshingV122;
    private readonly List<(Guid Id,string Name,int Writable)> excelMatchesV122=new();
    private readonly DispatcherTimer excelSuggestTimerV122=new(){
        Interval=TimeSpan.FromSeconds(18)
    };
    private bool excelSuggestTimerConnectedV122;
    // Read-only previews only. Requires an ACTUAL foreground Excel window,
    // a unique saved reference-label signature and at least one safe value.
    // Never silently change a workbook.
    private async void RefreshExcelSuggestionsV122()
    {
        if(disposed||excelRefreshingV122)return;
        excelRefreshingV122=true;
        try
        {
            var found=new List<(Guid Id,string Name,int Writable)>();
            if(DeskSpaceExcelActiveV122.ForegroundExcel())
            {
                var templates=DeskSpaceExcelStoreV16.Load().Where(x=>
                    x.CoordinatesVerified &&
                    x.Cells.Any(c=>c.Role=="Value" && c.ValueKind!="blank") &&
                    (x.Anchors.Count>0 || x.Cells.Any(c=>c.Role=="Reference")))
                    .Take(8).ToArray();
                foreach(var item in templates)
                {
                    if(disposed||!DeskSpaceExcelActiveV122.ForegroundExcel())break;
                    try {
                        var proposed=await DeskSpaceExcelWorkerV41.PreviewAsync(
                            item,false,false);
                        if(DeskSpaceExcelActiveV122.SafeToSuggest(
                            DeskSpaceExcelActiveV122.ForegroundExcel(),
                            proposed.Verified,proposed.Writable))
                            found.Add((item.Id,item.Name,proposed.Writable));
                    }catch(Exception){} // No match never changes Excel.
                }
            }
            if(disposed)return;
            var prior=excelMatchesV122.ToArray();
            excelMatchesV122.Clear();excelMatchesV122.AddRange(found);
            if(!prior.SequenceEqual(found))Render();
        }
        catch(Exception ex){
            System.Diagnostics.Debug.WriteLine("DeskSpace Excel suggestions: "+ex);
        }
        finally{excelRefreshingV122=false;}
    }
    private async void ReviewExcelSuggestionV122(Guid id)
    {
        try
        {
            var saved=DeskSpaceExcelStoreV16.Load();
            var item=saved.FirstOrDefault(x=>x.Id==id);
            if(item==null)throw new InvalidOperationException(
                "The saved mapping changed. Refresh suggestions.");
            var proposal=await DeskSpaceExcelWorkerV41.PreviewAsync(
                item,false,false);
            if(!proposal.Verified || proposal.Writable<1)
                throw new InvalidOperationException(
                    "The Excel headers or destination changed. No values pasted.");
            if(MessageBox.Show(host,
                "Match: "+item.Name+"\n"+proposal.Reason+
                "\n\nPaste "+proposal.Writable+
                " values into "+proposal.SheetName+"?\n"+
                "Existing values and formulas stay protected. "+
                "A workbook backup is made first; Excel is not auto-saved.",
                "DeskSpace · Review header/value paste",
                MessageBoxButton.YesNo,MessageBoxImage.Question)!=
                MessageBoxResult.Yes)return;
            var backup=await DeskSpaceExcelWorkerV41.ApplyAsync(
                proposal,item,false);
            MessageBox.Show(host,
                "Value mapping applied. Review Excel and save manually.\n"+
                "Safety backup: "+backup,"DeskSpace Excel",
                MessageBoxButton.OK,MessageBoxImage.Information);
            RefreshExcelSuggestionsV122();
        }
        catch(Exception ex)
        {
            MessageBox.Show(host,
                "Excel suggestion blocked; nothing was auto-pasted: "+
                ex.Message,"DeskSpace Excel",
                MessageBoxButton.OK,MessageBoxImage.Warning);
        }
    }
    // Scan in the background. Do not block circle hover, dragging, or file drops.
    private async void RefreshRunningApps()
'@ 'isolated foreground Excel read-only preview and explicit confirmed paste'
Swap $shelf @'
    private string NonAppSignature()=>string.Join("\u001e",
        held.Concat(detected).Select(x=>x.StagedPath));
'@ @'
    private string NonAppSignature()=>string.Join("\u001e",
        held.Concat(detected).Select(x=>x.StagedPath)
            .Concat(excelMatchesV122.Select(x=>x.Id.ToString())));
'@ 'new Excel match causes previously dismissed deck to reappear'
Swap $shelf @'
        if(held.Count+detected.Count+runningApps.Count==0 && !busy)
'@ @'
        if(held.Count+detected.Count+runningApps.Count+
            excelMatchesV122.Count==0 && !busy)
'@ 'show deck for Excel-only matches'
Swap $shelf @'
        (held.Count+detected.Count+runningApps.Count)>0;}
'@ @'
        (held.Count+detected.Count+runningApps.Count+
        excelMatchesV122.Count)>0;}
'@ 'shelf visibility when Excel-only suggestions'
Swap $shelf @'
        DeskSpaceRunningAppsV88.AddHoverGroup(list,runningApps,runningWindows);
        var heldGroup=DeskSpaceShelfGroupsV88.Add(list,"Held Files",
'@ @'
        DeskSpaceRunningAppsV88.AddHoverGroup(list,runningApps,runningWindows);
        var suggestionGroup=DeskSpaceShelfGroupsV88.Add(list,
            "Excel paste suggestions",excelMatchesV122.Count,
            excelExpandedV122,()=>{
                excelExpandedV122=!excelExpandedV122;Render();
            });
        if(excelExpandedV122)
            foreach(var suggestion in excelMatchesV122.ToArray())
            {
                var id=suggestion.Id;
                var action=new Button{
                    Content="Review & paste · "+suggestion.Name+" ("+
                        suggestion.Writable+" values)",
                    ToolTip="Verified Excel header match · preview again and "+
                        "confirm before writing; never automatic",
                    FontSize=11,Padding=new Thickness(6,5,6,5),
                    Margin=new Thickness(1,2,1,2),
                    HorizontalContentAlignment=HorizontalAlignment.Left
                };
                action.Click+=(_,_)=>ReviewExcelSuggestionV122(id);
                suggestionGroup.Children.Add(action);
            }
        var heldGroup=DeskSpaceShelfGroupsV88.Add(list,"Held Files",
'@ 'real click-to-confirm suggestions inside existing deck'
Swap $shelf @'
        runningAppsTimer.Stop();
'@ @'
        runningAppsTimer.Stop();
        excelSuggestTimerV122.Stop();
'@ 'cleanup suggestion timer'
$info="$r/DeskSpaceInformationV36.cs"
Swap $info @'
        shelfBody.Children.Add(rowsHost);
'@ @'
        shelfBody.Children.Add(rowsHost);
        // Placeholder only: future opt-in website/app association for UI/PW.
        // Never read URLs, browser windows or passwords automatically yet.
        shelfBody.Children.Add(new TextBlock{
            Text="Future: assign a website/app to a UI or PW entry; "+
                 "when that page is open, suggest the matching entry. "+
                 "Automatic detection and autofill are not enabled.",
            FontSize=10,Foreground=Brushes.Gray,
            TextWrapping=TextWrapping.Wrap,
            Margin=new Thickness(3,8,3,4),
            ToolTip="Placeholder only — existing security vault remains unchanged"
        });
'@ 'vault autofill association informational placeholder'
$smoke='r36/UiSmoke/Program.cs'
Swap $smoke @'
            TestHeaderValueSignatureV120();
'@ @'
            TestHeaderValueSignatureV120();
            TestContextualFeaturesV122();
'@ 'new R122 pure rules runtime smoke'
$method=@'
    private static void TestContextualFeaturesV122()
    {
        var recent=Product.GetType("DeskSpace.DeskSpacePinnedRecentV122",true)!;
        var same=recent.GetMethod("SameExecutableV122",
            BindingFlags.NonPublic|BindingFlags.Static)!;
        bool Same(string a,string b)=>(bool)same.Invoke(null,new object[]{a,b})!;
        Assert(Same(@"C:\Program Files\Microsoft Office\EXCEL.EXE",
                    @"D:\Programs\Excel.exe"),
            "R122 Excel pinned handler must match same application");
        Assert(!Same(@"C:\Windows\notepad.exe",
                     @"C:\Windows\explorer.exe"),
            "R122 never open unrelated recent document for a pin");
        var matching=Product.GetType("DeskSpace.DeskSpaceExcelActiveV122",true)!;
        var safe=matching.GetMethod("SafeToSuggest",
            BindingFlags.NonPublic|BindingFlags.Static)!;
        bool Suggest(bool foreground,bool verified,int writable)=>
            (bool)safe.Invoke(null,new object[]{foreground,verified,writable})!;
        Assert(Suggest(true,true,1),
            "R122 verified foreground Excel header mapping may suggest");
        Assert(!Suggest(false,true,1) &&
               !Suggest(true,false,1) &&
               !Suggest(true,true,0),
            "R122 never suggest when Excel inactive, label signature ambiguous, or no writable cells");
        Console.WriteLine("R122 PASS: app-linked recent paths, foreground Excel suggestion constraints.");
    }

'@
Swap $smoke @'
    private static void TestExcelTypeAwareSheetV101()
'@ ($method+'    private static void TestExcelTypeAwareSheetV101()') 'compiled recent path and Excel suggestion guards'
Write-Host 'R122: pinned app recent-document open; active Excel verified-match buttons on deck with explicit confirmation; credential placeholder without monitoring.'
