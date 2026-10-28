$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$old=@'
    private static int Main(string[] args)
    {
        outdir=args.Length>0?args[0]:Path.Combine(Path.GetTempPath(),"deskspace-r36-ui");
'@.Replace($crlf,$lf)
$new=@'
    private static int Main(string[] args)
    {
        // The product's widget-host spawn path can inherit the current test
        // runner EXE when WPF is hosted by reflection. The smoke executable
        // is NOT DeskSpace.exe; never recursively run its entire UI tests
        // when a product test requests a secondary --widget-host process.
        // This guard is compiled into the TEST RUNNER only, not the product.
        if(args.Length>0 && string.Equals(args[0],"--widget-host",
            StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("R169 PASS: smoke child widget-host launch ignored.");
            return 0;
        }
        outdir=args.Length>0?args[0]:Path.Combine(Path.GetTempPath(),"deskspace-r36-ui");
'@.Replace($crlf,$lf)
$count=([regex]::Matches($s,[regex]::Escape($old))).Count
if($count -ne 1){throw "R169 test runner Main anchor mismatch: $count"}
[IO.File]::WriteAllText($p,$s.Replace($old,$new),[Text.UTF8Encoding]::new($false))
Write-Host 'R169: recursive --widget-host test child no longer reruns entire WPF smoke or locks screenshot evidence.'
