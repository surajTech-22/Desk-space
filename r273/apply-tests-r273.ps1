$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$crlf=[string][char]13+[char]10;$lf=[string][char]10
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function One([string]$before,[string]$after,[string]$purpose)
{
 $before=$before.Replace($crlf,$lf);$after=$after.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($before))).Count
 if($n -ne 1){throw ('R273 '+$purpose+' anchor='+$n)}
 $script:s=$script:s.Replace($before,$after)
}
One '                TestMediaDeckRestoreV272();' @'
                TestMediaDeckRestoreV272();
                TestExplicitPreviewChooserV273();
'@ 'native invocation'
$method=@'
    private static void TestExplicitPreviewChooserV273()
    {
        var type=Product.GetType("DeskSpace.DeskSpacePreviewChooserV273",true)!;
        var can=type.GetMethod("CanPreview",BindingFlags.Static|
            BindingFlags.NonPublic)!;
        var create=type.GetMethod("Create",BindingFlags.Static|
            BindingFlags.NonPublic)!;
        bool Check(string path)
        {
            var args=new object[]{path,""};
            return (bool)can.Invoke(null,args)!;
        }
        var root=Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.UserProfile),
            "Downloads","DeskSpace-R273-Preview-Test-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var file=Path.Combine(root,"preview.txt");
            File.WriteAllText(file,"User-chosen sample file, no execution.");
            Assert(Check(file)&&Check(root)&&!Check(Path.Combine(root,"absent.docx")),
                "R273 local ordinary file/folder accepted; missing files fail closed");
            var big=Path.Combine(root,"oversized.bin");
            using(var stream=new FileStream(big,FileMode.CreateNew))
                stream.SetLength(257L*1024*1024);
            Assert(!Check(big),"R273 >256MB inline preview must be blocked");
            var window=(Window)create.Invoke(null,new object[]{new Window()})!;
            try
            {
                // WPF VisualTreeHelper cannot traverse a ScrollViewer's
                // templated content before the test window is shown. Inspect
                // its constructed logical controls directly, without popups.
                var previewLayout=(StackPanel)((ScrollViewer)window.Content).Content;
                var commands=previewLayout.Children.OfType<WrapPanel>().Single();
                var buttons=commands.Children.OfType<Button>()
                    .Select(x=>x.Content?.ToString()??"").ToArray();
                Assert(buttons.Contains("Choose file")&&
                    buttons.Contains("Choose folder")&&
                    buttons.Contains("Clear preview")&&
                    buttons.Contains("Close"),
                    "R273 actual WPF Preview chooser must support safe user actions");
            }
            finally{window.Close();}
        }
        finally
        {
            try{Directory.Delete(root,true);}
            catch(IOException){}
            catch(UnauthorizedAccessException){}
        }
        Console.WriteLine("R273 PASS: chooser accepts ordinary file/folder, "+
            "rejects missing/oversized targets and exposes explicit preview controls.");
    }

'@
One '    private static void TestMediaDeckRestoreV272()' ($method+
'    private static void TestMediaDeckRestoreV272()') 'native test method'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R273 native WPF chooser, file/folder, missing/oversized guard registered.'
