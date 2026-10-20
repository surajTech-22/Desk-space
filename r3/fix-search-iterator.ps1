$ErrorActionPreference='Stop'
$source='DeskSpace_Windows_R1/DeskSpace/UniversalSearch.cs'
$s=[IO.File]::ReadAllText($source)
$start=$s.IndexOf('    private static IEnumerable<string> SafeFiles(')
if($start -lt 0){throw 'R3 search file enumeration method missing'}
$replacement=@'
    private static IEnumerable<string> SafeFiles(string root,int maxCount,CancellationToken token)
    {
        var pending=new Stack<string>();pending.Push(root);int checkedCount=0;
        while(pending.Count>0&&checkedCount<maxCount)
        {
            token.ThrowIfCancellationRequested();var dir=pending.Pop();
            string[] dirs, files;
            try { dirs=Directory.GetDirectories(dir);files=Directory.GetFiles(dir); }
            catch(UnauthorizedAccessException){continue;}
            catch(IOException){continue;}
            catch(System.Security.SecurityException){continue;}
            foreach(var sub in dirs)
            {
                token.ThrowIfCancellationRequested();
                try { if((File.GetAttributes(sub)&FileAttributes.ReparsePoint)==0)pending.Push(sub); }
                catch(UnauthorizedAccessException){}catch(IOException){}
            }
            foreach(var file in files)
            {
                token.ThrowIfCancellationRequested();
                if(++checkedCount>maxCount)yield break;
                yield return file;
            }
        }
    }
}
'@
[IO.File]::WriteAllText($source,$s.Substring(0,$start)+$replacement,[Text.UTF8Encoding]::new($false))
Write-Host 'R3 safe recursive search iterator compiled with no iterator yield in catch scope.'

$main='DeskSpace_Windows_R1/DeskSpace/MainWindow.xaml.cs'
$source=[IO.File]::ReadAllText($main)
$old='if (WidgetPreferences.Load().ShowAtLaunch)'
if (!$source.Contains($old)) { throw 'R3 main startup preference anchor missing' }
$source=$source.Replace($old,'if (WidgetSettingsV3.Load().ShowAtLaunch)')
[IO.File]::WriteAllText($main,$source,[Text.UTF8Encoding]::new($false))
Write-Host 'R3 startup now respects its own widget preferences.'
