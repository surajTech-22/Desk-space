$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function One([string]$old,[string]$new,[string]$why){
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $hits=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($hits -ne 1){throw ("R189 "+$why+" expected one anchor, found "+$hits)}
 $script:s=$script:s.Replace($old,$new)
}
One '            TestSafeUpdateDeferralV188();' @'
            TestSafeUpdateDeferralV188();
            TestDocumentMetadataV189();
'@ 'new native WPF test invocation'
$method=@'
    private static void TestDocumentMetadataV189()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceDocumentMetadataV189",true)!;
        var describe=type.GetMethod("Describe",BindingFlags.NonPublic|BindingFlags.Static)!;
        string Info(string path)=>(string)describe.Invoke(null,new object[]{path})!;
        var preview=Product.GetType("DeskSpace.DeskSpaceFilePreviewV85",true)!;
        var create=preview.GetMethod("Create",BindingFlags.NonPublic|BindingFlags.Static)!;
        var root=Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.UserProfile),"Downloads",
            "DeskSpace_R189_Metadata_"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string CreateOffice(string extension,string main,string part)
            {
                string path=Path.Combine(root,"sample"+extension);
                using(var archive=System.IO.Compression.ZipFile.Open(
                    path,System.IO.Compression.ZipArchiveMode.Create))
                {
                    archive.CreateEntry("[Content_Types].xml");
                    archive.CreateEntry(main);
                    archive.CreateEntry(part);
                    archive.CreateEntry("word/media/photo.png");
                }
                return path;
            }
            var doc=CreateOffice(".docx","word/document.xml","word/header1.xml");
            var xlsx=CreateOffice(".xlsx","xl/workbook.xml",
                "xl/worksheets/sheet1.xml");
            var ppt=CreateOffice(".pptx","ppt/presentation.xml",
                "ppt/slides/slide1.xml");
            Assert(Info(doc).Contains("Word package")&&
                Info(doc).Contains("header XML parts")&&
                Info(xlsx).Contains("Excel package")&&
                Info(xlsx).Contains("1 worksheet XML parts")&&
                Info(ppt).Contains("PowerPoint package")&&
                Info(ppt).Contains("1 slide XML parts"),
                "R189 Office preview must count package structure without opening document data");
            var invalid=Path.Combine(root,"invalid.xlsx");
            File.WriteAllText(invalid,"not a ZIP");
            var fileSurface=(FrameworkElement)create.Invoke(null,new object[]{invalid})!;
            Assert(Walk(fileSurface).OfType<TextBox>().Any(x=>
                x.Text.Contains("unavailable",StringComparison.OrdinalIgnoreCase)),
                "R189 corrupt Office documents must fail inside a bounded read-only preview");
            var folder=Path.Combine(root,"Folder");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder,"note.txt"),"content not to inspect");
            Directory.CreateDirectory(Path.Combine(folder,"Child"));
            string folderSummary=Info(folder);
            Assert(folderSummary.Contains("1 files, 1 subfolders")&&
                folderSummary.Contains("[FILE] note.txt")&&
                !folderSummary.Contains("content not to inspect"),
                "R189 folder overview may enumerate only direct entry names, never file contents");
            var folderSurface=(FrameworkElement)create.Invoke(null,new object[]{folder})!;
            Assert(Walk(folderSurface).OfType<TextBox>().Any(x=>
                x.Text.Contains("1 files, 1 subfolders")),
                "R189 one-level folder summary must render in the real Deck preview");
            var wav=Path.Combine(root,"sample.wav");
            using(var writer=new BinaryWriter(File.Create(wav)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                writer.Write((uint)16036);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                writer.Write((uint)16);
                writer.Write((ushort)1); // PCM
                writer.Write((ushort)1); // mono
                writer.Write((uint)8000);
                writer.Write((uint)16000);
                writer.Write((ushort)2);
                writer.Write((ushort)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                writer.Write((uint)16000);
                writer.Write(new byte[16000]);
            }
            Assert(Info(wav).Contains("WAV audio")&&
                Info(wav).Contains("8000 Hz")&&
                Info(wav).Contains("00:00:01"),
                "R189 WAV must display metadata and approximate duration without playing audio");
            Assert(File.Exists(doc)&&File.Exists(xlsx)&&File.Exists(ppt)&&
                File.Exists(wav)&&File.ReadAllText(Path.Combine(folder,"note.txt"))==
                "content not to inspect",
                "R189 previews must never mutate source documents or files");
            Console.WriteLine("R189 PASS: native WPF folder+Office preview, "+
                "ZIP metadata only, malformed input, PCM WAV duration and intact sources.");
        }
        finally
        {
            if(Directory.Exists(root))Directory.Delete(root,true);
        }
    }

'@
One '    private static void TestIndependentMonitorsV154()' (
 $method.Replace($crlf,$lf)+'    private static void TestIndependentMonitorsV154()'
) 'source-backed UI and no-mutation test insertion'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R189: native WPF preview and disposable Downloads-only file fixtures added.'
