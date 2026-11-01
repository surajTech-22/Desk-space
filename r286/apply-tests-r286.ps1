$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$crlf=[string][char]13+[char]10;$lf=[string][char]10
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$old,[string]$next,[string]$why){
 $old=$old.Replace($crlf,$lf);$next=$next.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($n -ne 1){throw ('R286 '+$why+' anchor='+$n)}
 $script:s=$script:s.Replace($old,$next)
}
Swap '                TestPerMonitorDpiManifestPolicyV285();' @'
                TestPerMonitorDpiManifestPolicyV285();
                TestTrayExitUntilExplicitShowV286();
'@ 'register R286 WPF lifecycle'
$method=@'
    private static void TestTrayExitUntilExplicitShowV286()
    {
        var lifecycle=Product.GetType(
            "DeskSpace.DeskSpaceWidgetExitV286",true)!;
        const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Static;
        var allowed=lifecycle.GetMethod("MayOpen",flags)!;
        var mark=lifecycle.GetMethod("MarkAt",flags)!;
        var clear=lifecycle.GetMethod("ClearAt",flags)!;
        bool Can(bool manual,bool exit,bool atLaunch)=>
            (bool)allowed.Invoke(null,new object[]{manual,exit,atLaunch})!;
        Assert(Can(false,false,true) && !Can(false,false,false) &&
            !Can(false,true,true) && !Can(false,true,false) &&
            Can(true,true,true) && Can(true,true,false),
            "R286 automatic startup must never clear tray Exit; "+
            "manual Show must win regardless of ShowAtLaunch setting");
        var dir=Path.GetFullPath(Path.Combine(outdir,
            "r286-exit-"+Guid.NewGuid().ToString("N")));
        var sentinel=Path.Combine(dir,"widget-user-exited-v286.flag");
        try
        {
            Assert(!File.Exists(sentinel),
                "R286 test starts without any widget user-exit marker");
            mark.Invoke(null,new object[]{sentinel});
            Assert(File.Exists(sentinel) &&
                !Can(false,File.Exists(sentinel),true),
                "R286 tray Exit must persist across app restarts even "+
                "when automatic startup setting remains enabled");
            mark.Invoke(null,new object[]{sentinel});
            Assert(Directory.GetFiles(dir).Length==1,
                "R286 repeating Exit must not leave stale partial files");
            clear.Invoke(null,new object[]{sentinel});
            Assert(!File.Exists(sentinel) &&
                Can(false,File.Exists(sentinel),true),
                "R286 explicit Show removes only its own marker and "+
                "restores the normal startup behavior");
            clear.Invoke(null,new object[]{sentinel});
            Assert(!File.Exists(sentinel),
                "R286 repeated Show must be safe");
            Console.WriteLine("R286 PASS: persistent intentional tray Exit "+
                "beats startup/relaunch; explicit Show alone re-enables widget.");
        }
        finally
        {
            try{Directory.Delete(dir,true);}
            catch(IOException){}
            catch(UnauthorizedAccessException){}
        }
    }

'@
Swap '    private static void TestPerMonitorDpiManifestPolicyV285()' ($method+
 '    private static void TestPerMonitorDpiManifestPolicyV285()') 'R286 native lifecycle method'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R286 native persistent Exit/Show and startup gate tests registered.'
