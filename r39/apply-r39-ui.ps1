$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$path='DeskSpace_Windows_R1/DeskSpace/DeskSpaceExcelV16.cs'
$s=[IO.File]::ReadAllText($path).Replace([string][char]13+[char]10,[string][char]10)
function Replace([string]$old,[string]$new)
{
 $old=$old.Replace([string][char]13+[char]10,[string][char]10)
 $new=$new.Replace([string][char]13+[char]10,[string][char]10)
 if(!$script:s.Contains($old)){throw "R39 Excel UI anchor not found: $($old.Substring(0,[Math]::Min($old.Length,105)))"}
 $script:s=$script:s.Replace($old,$new)
}
Replace @'
        body.Children.Add(Buttons(
            ActionButton("Open workbook…",()=>Run(OpenWorkbook)),
            ActionButton("Add screenshot…",()=>Run(OpenScreenshot))
        ));
'@ @'
        body.Children.Add(Buttons(
            ActionButton("Open workbook…",()=>Run(OpenWorkbook)),
            ActionButton("Upload screenshot + OCR",()=>OpenScreenshot()),
            ActionButton("Analyze marked image region",AnalyzeScreenshot),
            ActionButton("Verify screenshot against selected Excel range",
                ()=>Run(LinkScreenshotToExcel))
        ));
        body.Children.Add(Caption(
            "The screenshot and live Excel inputs use the same classification and "+
            "save screen. Only Excel itself can verify exact cell addresses and formulas.",12));
'@
Replace '        body.Children.Add(Caption("Choose exactly which captured values to save",12,true));' '        body.Children.Add(Caption("Review Reference Cell / Value Cell / formulas and select what to save",12,true));'
Replace @'
            ActionButton("Capture selected Excel cells",()=>Run(Capture))
        ));
'@ @'
            ActionButton("Copy and analyze selected Excel cells",()=>Run(Capture)),
            ActionButton("Copy range to Excel-only shelf",
                DeskSpaceExcelClipboardV39.CaptureNow)
        ));
'@
$start=$s.IndexOf('    private void OpenScreenshot()', [StringComparison]::Ordinal)
$end=$s.IndexOf('    private void ShowScreenshot(', $start, [StringComparison]::Ordinal)
if($start -lt 0 -or $end -lt $start){throw 'R39 original screenshot loading method missing'}
$openMethod=@'
    private async void OpenScreenshot()
    {
        try
        {
            var dialog=new OpenFileDialog{
                Filter="Screenshots|*.png;*.jpg;*.jpeg;*.bmp",
                CheckFileExists=true,Title="Choose an Excel table screenshot"
            };
            if(dialog.ShowDialog()!=true)return;
            var bytes=File.ReadAllBytes(dialog.FileName);
            if(bytes.Length>12000000)
                throw new InvalidOperationException(
                    "Use a screenshot no larger than 12 MB.");
            ShowScreenshot(bytes,"");
            status.Text="Screenshot loaded. Detecting the visible table and text…";
            await AnalyzeScreenshotAsync();
        }
        catch(Exception ex){status.Text="Screenshot: "+ex.Message;}
    }

'@
$s=$s.Substring(0,$start)+$openMethod+$s.Substring($end)
$begin=$s.IndexOf('    private void Capture()', [StringComparison]::Ordinal)
$finish=$s.IndexOf('    private void Delete()', [Math]::Max(0,$begin),[StringComparison]::Ordinal)
if($begin -lt 0 -or $finish -lt $begin){throw 'R39 cannot locate R17 saved-cell method boundary'}
$methods=[IO.File]::ReadAllText('r39/DeskSpaceExcelMethodsV39.txt')
$s=$s.Substring(0,$begin)+$methods+[Environment]::NewLine+$s.Substring($finish)
[IO.File]::WriteAllText($path,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R39: unified live selection and screenshot OCR, Reference/Value/Formula review, verified cell destination binding and Excel clipboard.'
