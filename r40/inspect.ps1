$ErrorActionPreference='Stop'
$root='DeskSpace_Windows_R1/DeskSpace'
foreach($target in @('MainWindow.xaml','MainWindow.xaml.cs')){
    $lines=[IO.File]::ReadAllLines("$root/$target")
    Write-Host "==== R40 FILE $target LENGTH $($lines.Length) ===="
    if($target -like '*.xaml'){
        for($i=0;$i -lt $lines.Length;$i++){
            if(($i -lt 190) -or ($lines[$i] -match 'Excel|MainContent|Module|Panel|Content|Scroll|Home')){
                if($i -lt 290){Write-Host ("{0}: {1}" -f ($i+1),$lines[$i])}
            }
        }
    } else {
        for($i=0;$i -lt $lines.Length;$i++){
            if($lines[$i] -match 'ExcelModule_Click|ModuleNavigate_Click|Navigate|ShowHome|ShowModule|CurrentModule|SafeSave|OnClosing|MainContent|private void .*Click'){
                for($j=[Math]::Max(0,$i-4);$j -le [Math]::Min($lines.Length-1,$i+20);$j++){
                    Write-Host ("{0}: {1}" -f ($j+1),$lines[$j])
                }
            }
        }
    }
}
