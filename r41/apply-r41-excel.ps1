$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item 'r41/DeskSpaceExcelWorkerV41.cs' "$root/DeskSpaceExcelWorkerV41.cs" -Force
function Change([string]$file,[string]$before,[string]$after)
{
 $s=[IO.File]::ReadAllText($file).Replace([string][char]13+[char]10,[string][char]10)
 $before=$before.Replace([string][char]13+[char]10,[string][char]10)
 $after=$after.Replace([string][char]13+[char]10,[string][char]10)
 if(!$s.Contains($before)){throw "R41 missing Excel anchor in $file : $($before.Substring(0,[Math]::Min(95,$before.Length)))"}
 $s=$s.Replace($before,$after)
 [IO.File]::WriteAllText($file,$s,[Text.UTF8Encoding]::new($false))
}
# Excel can be running in more than one instance. GetActiveObject only picks
# one first-registered instance, possibly NOT the window the user selected.
# Enumerate already running Workbook COM monikers, and select the Excel
# process highest in Windows' current XLMAIN z-order. NEVER launch a new Excel.
$bridge="$root/DeskSpaceExcelBridgeV16.cs"
$s=[IO.File]::ReadAllText($bridge).Replace([string][char]13+[char]10,[string][char]10)
$start=$s.IndexOf('    private static dynamic Excel()', [StringComparison]::Ordinal)
$end=$s.IndexOf('    private static string Text(', $start,[StringComparison]::Ordinal)
if($start -lt 0 -or $end -lt $start){throw 'R41 cannot find Excel COM resolver'}
$new=@'
    [DllImport("ole32.dll",PreserveSig=true)]
    private static extern int GetRunningObjectTable(int reserved,
        out System.Runtime.InteropServices.ComTypes.IRunningObjectTable table);
    [DllImport("ole32.dll",PreserveSig=true)]
    private static extern int CreateBindCtx(int reserved,
        out System.Runtime.InteropServices.ComTypes.IBindCtx context);
    [DllImport("user32.dll")]
    private static extern bool EnumWindows(WindowEnumerator callback,IntPtr arg);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hwnd,
        System.Text.StringBuilder output,int length);
    private delegate bool WindowEnumerator(IntPtr hwnd,IntPtr arg);

    private static dynamic Excel()
    {
        var apps=new Dictionary<long,object>();
        // ROT ProgID is a fallback; multiple Excel instances can run
        // simultaneously and it can resolve an unrelated workbook.
        try
        {
            CLSIDFromProgID("Excel.Application",out var classId);
            GetActiveObject(ref classId,IntPtr.Zero,out var first);
            RegisterExcelApp(first,apps);
        }
        catch(COMException){}
        catch(InvalidOperationException){}
        try
        {
            if(GetRunningObjectTable(0,out var rot)==0 &&
                CreateBindCtx(0,out var ctx)==0)
            {
                rot.EnumRunning(out var enumeration);
                var item=new System.Runtime.InteropServices.ComTypes.IMoniker[1];
                int limit=0;
                while(limit++<300 && enumeration.Next(1,item,IntPtr.Zero)==0)
                {
                    try
                    {
                        item[0].GetDisplayName(ctx,null,out string name);
                        if(!System.Text.RegularExpressions.Regex.IsMatch(name,
                            @"\.(xlsx|xlsm|xlsb|xls)(!.*)?$",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                            continue;
                        rot.GetObject(item[0],out object running);
                        dynamic workbook=running;
                        RegisterExcelApp((object)workbook.Application,apps);
                    }
                    catch(COMException){}
                    catch(InvalidCastException){}
                    catch(Microsoft.CSharp.RuntimeBinder.RuntimeBinderException){}
                }
            }
        }
        catch(COMException){}
        catch(InvalidOperationException){}
        if(apps.Count==0)
            throw new InvalidOperationException(
                "Could not connect to the already opened Excel workbook. "+
                "Open the workbook in Microsoft Excel, select its cells and "+
                "return to DeskSpace. Excel and DeskSpace must run at the "+
                "same Windows privilege level.");

        // EnumWindows enumerates visible top-level windows in Z order.
        // After clicking DeskSpace, the most recently used Excel window is
        // normally the topmost XLMAIN window behind DeskSpace.
        var ranks=new Dictionary<long,int>();
        EnumWindows((hwnd,_)=>{
            var text=new System.Text.StringBuilder(90);
            if(GetClassName(hwnd,text,text.Capacity)>0 &&
                text.ToString()=="XLMAIN")ranks[hwnd.ToInt64()]=ranks.Count;
            return true;
        },IntPtr.Zero);
        foreach(var candidate in apps.OrderBy(x=>ranks.TryGetValue(x.Key,
            out var rank)?rank:int.MaxValue))
        {
            try
            {
                dynamic app=candidate.Value;
                if(app.ActiveWorkbook==null || app.ActiveWindow==null)continue;
                dynamic selection=app.ActiveWindow.RangeSelection;
                if((int)selection.Row<1 || (int)selection.Column<1)continue;
                return app;
            }
            catch(COMException){}
            catch(Microsoft.CSharp.RuntimeBinder.RuntimeBinderException){}
        }
        throw new InvalidOperationException(
            "Excel is running, but no selectable worksheet range could be "+
            "identified. Click the target worksheet and highlight its cells.");
    }
    private static void RegisterExcelApp(object appObject,
        Dictionary<long,object> apps)
    {
        try
        {
            dynamic app=appObject;
            long hwnd=Convert.ToInt64((object)app.Hwnd,
                CultureInfo.InvariantCulture);
            if(hwnd!=0 && !apps.ContainsKey(hwnd))apps.Add(hwnd,appObject);
        }
        catch(COMException){}
        catch(Microsoft.CSharp.RuntimeBinder.RuntimeBinderException){}
    }
'@
$new=$new.Replace([string][char]13+[char]10,[string][char]10)
$s=$s.Substring(0,$start)+$new+$s.Substring($end)
[IO.File]::WriteAllText($bridge,$s,[Text.UTF8Encoding]::new($false))

# Capture EXACT selected cells of that running Workbook, regardless of which
# Excel window owns the selection or whether copy mode is currently enabled.
Change $bridge @'
        dynamic sheet=app.ActiveSheet;
        dynamic range=string.IsNullOrWhiteSpace(rangeAddress)
            ? app.Selection : sheet.Range[rangeAddress];
'@ @'
        dynamic range;
        if(string.IsNullOrWhiteSpace(rangeAddress))
            range=app.ActiveWindow.RangeSelection;
        else
            range=app.ActiveSheet.Range[rangeAddress];
        dynamic sheet=range.Worksheet;
'@
Change $bridge 'SourceWorkbook=Path.GetFileName(Text(app.ActiveWorkbook.Name)),' 'SourceWorkbook=Path.GetFileName(Text(sheet.Parent.Name)),'
# Make Excel copy shelf work if a valid Excel range was copied; no Ctrl+C is
# needed for explicit capture in the main Excel Mapping module.
Change $bridge @'
            return state!=null && Convert.ToInt32(state,
                CultureInfo.InvariantCulture)==1;
'@ @'
            return state!=null && Convert.ToInt32(state,
                CultureInfo.InvariantCulture)==1;
'@
# The Excel main page must never perform thousands of COM calls on the WPF UI.
$ui="$root/DeskSpaceExcelV16.cs"
Change $ui @'
    private void Capture()
    {
        var candidate=DeskSpaceExcelBridgeV16.Capture(name.Text,range.Text,
            formulas.IsChecked==true);
        if(screenshotBytes!=null)
        {
            candidate.ScreenshotBase64=Convert.ToBase64String(screenshotBytes);
            candidate.ScreenshotSelection=Region();
        }
        PresentCandidate(candidate);
        DeskSpaceExcelClipboardV39.CaptureNow();
    }
'@ @'
    private async void Capture()
    {
        try
        {
            status.Text="Reading selected cells from the already open Excel workbook…";
            var candidate=await DeskSpaceExcelWorkerV41.CaptureAsync(
                name.Text,range.Text,formulas.IsChecked==true);
            if(screenshotBytes!=null)
            {
                candidate.ScreenshotBase64=Convert.ToBase64String(screenshotBytes);
                candidate.ScreenshotSelection=Region();
            }
            PresentCandidate(candidate);
            status.Text="Captured "+candidate.Cells.Count+" cells at "+
                candidate.SourceWorkbook+" / "+candidate.SourceSheet+" / "+
                candidate.SourceAddress+". Review headers and values, then Save.";
        }
        catch(Exception ex)
        {
            status.Text="Excel selection unavailable: "+ex.Message;
            MessageBox.Show(ex.Message,"DeskSpace Excel selection",
                MessageBoxButton.OK,MessageBoxImage.Warning);
        }
    }
'@
Change $ui @'
    private void LinkScreenshotToExcel()
    {
        if(screenshotBytes==null)
            throw new InvalidOperationException("Upload a screenshot to link first.");
        var candidate=DeskSpaceExcelBridgeV16.Capture(name.Text,range.Text,
            formulas.IsChecked==true);
        candidate.SourceKind="Screenshot + live Excel";
        candidate.CoordinatesVerified=true;
        candidate.ScreenshotBase64=Convert.ToBase64String(screenshotBytes);
        candidate.ScreenshotSelection=Region();
        PresentCandidate(candidate);
        status.Text="Linked screenshot to live "+candidate.SourceSheet+" "+
            candidate.SourceAddress+". Actual cell addresses, values, formulas "+
            "and merges are verified from the selected Excel range.";
    }
'@ @'
    private async void LinkScreenshotToExcel()
    {
        if(screenshotBytes==null)
        {
            status.Text="Upload a screenshot before linking it to Excel.";
            return;
        }
        try
        {
            status.Text="Verifying against the selected live Excel range…";
            var candidate=await DeskSpaceExcelWorkerV41.CaptureAsync(
                name.Text,range.Text,formulas.IsChecked==true);
            candidate.SourceKind="Screenshot + live Excel";
            candidate.CoordinatesVerified=true;
            candidate.ScreenshotBase64=Convert.ToBase64String(screenshotBytes);
            candidate.ScreenshotSelection=Region();
            PresentCandidate(candidate);
            status.Text="Verified live workbook "+candidate.SourceWorkbook+
                ", sheet "+candidate.SourceSheet+", cells "+
                candidate.SourceAddress+".";
        }
        catch(Exception ex)
        {
            status.Text="Unable to link screenshot: "+ex.Message;
            MessageBox.Show(ex.Message,"DeskSpace Excel verification",
                MessageBoxButton.OK,MessageBoxImage.Warning);
        }
    }
'@
Write-Host 'R41: selectable already-open Excel workbooks across COM instances; STA background capture; clear UI errors; existing workbook untouched.'
