$ErrorActionPreference='Stop'
$p='DeskSpace_Windows_R1/DeskSpace/WidgetHost.cs'
$s=[IO.File]::ReadAllText($p).Replace([string][char]13+[char]10,[string][char]10)
foreach($key in @('    private void BuildCompact(','    private void BuildDonut(','    private void OpenInWidget(','    private void OpenCalendarExpanded(','    private void OpenCalendarCompact(','    private void OpenMedia','    private void ShowMedia','    private void BuildWeather','    private void RenderWeather','    private void UpdateWeather','    private void RefreshClock','    private void CollapseRing(','    private void ReturnFromModule('))
{
 $a=$s.IndexOf($key,[StringComparison]::Ordinal)
 if($a -lt 0){Write-Host "INSPECT $key MISSING";continue}
 $b=$s.IndexOf('    private ', $a+$key.Length,[StringComparison]::Ordinal)
 if($b -lt 0){$b=$s.Length}
 Write-Host "INSPECT $key";Write-Host $s.Substring($a,[Math]::Min($b-$a,11000))
}
Write-Host 'INSPECT WEATHER REFERENCES'
$s.Split([string][char]10)|Where-Object{$_ -match 'WeatherSceneV11|new DeskSpaceWeather|TextBlock.*Weather|weatherScene|WeatherReading|DeskSpaceCalculator|new DeskSpaceMedia|prev.*Clock|← Clock|new DeskSpaceCompactCalendarV34'}|Select-Object -First 100|ForEach-Object{Write-Host $_}
Write-Host 'INSPECT project configuration'
Get-ChildItem 'DeskSpace_Windows_R1/DeskSpace' -Filter '*.csproj' | ForEach-Object {
 Write-Host $_.FullName;Get-Content $_.FullName | Select-Object -First 100 | ForEach-Object{Write-Host $_}
}
