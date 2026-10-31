$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$a,[string]$b,[string]$why)
{
 $a=$a.Replace($crlf,$lf);$b=$b.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($a))).Count
 if($n -ne 1){throw ("R241 "+$why+" expected one anchor, got "+$n)}
 $script:s=$script:s.Replace($a,$b)
}
Swap '                TestTextDeckPreviewV240();' @'
                TestTextDeckPreviewV240();
                TestWordDeckPreviewV241();
'@ 'native invocation'
$method=@'
    private static void TestWordDeckPreviewV241()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceDocxPreviewV241",true)!;
        const BindingFlags flags=BindingFlags.Static|BindingFlags.NonPublic;
        var supports=type.GetMethod("Supports",flags)!;
        bool Supported(string name)=>(bool)supports.Invoke(null,
            new object?[]{name})!;
        Assert(Supported(".DOCX") && !Supported(".docm") &&
            !Supported(".dotm") && !Supported(".zip") &&
            !Supported(".xlsx") && !Supported(".exe"),
            "R241 only explicit non-macro .docx extension may be previewed");
        var reader=type.GetMethod("ReadDocument",flags)!;
        (bool Ok,string Text,string Status) Read(string path)
        {
            var data=reader.Invoke(null,new object[]{path})!;
            var t=data.GetType();
            return ((bool)t.GetProperty("Available")!.GetValue(data)!,
                (string)t.GetProperty("Text")!.GetValue(data)!,
                (string)t.GetProperty("Status")!.GetValue(data)!);
        }
        var folder=Path.Combine(outdir,"r241-word-preview");
        Directory.CreateDirectory(folder);
        var normal=Path.Combine(folder,"safe.docx");
        var macro=Path.Combine(folder,"macro.docx");
        var unsafeXml=Path.Combine(folder,"unsafe.docx");
        var huge=Path.Combine(folder,"huge.docx");
        var corrupt=Path.Combine(folder,"invalid.docx");
        const string start=
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>"+
            "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\">"+
            "<w:body><w:p><w:r><w:t>Hello DeskSpace</w:t></w:r></w:p>"+
            "<w:p><w:r><w:t>Second paragraph</w:t></w:r></w:p></w:body></w:document>";
        void Make(string destination,string xml,bool addMacro=false)
        {
            using var zip=System.IO.Compression.ZipFile.Open(destination,
                System.IO.Compression.ZipArchiveMode.Create);
            var entry=zip.CreateEntry("word/document.xml");
            using(var writer=new StreamWriter(entry.Open(),
                new System.Text.UTF8Encoding(false)))
                writer.Write(xml);
            if(addMacro)
            {
                var attached=zip.CreateEntry("word/vbaProject.bin");
                using var output=attached.Open();
                output.WriteByte(1);
            }
        }
        try
        {
            Make(normal,start);
            var before=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                File.ReadAllBytes(normal)));
            var okay=Read(normal);
            var after=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                File.ReadAllBytes(normal)));
            Assert(okay.Ok && okay.Text.Contains("Hello DeskSpace") &&
                okay.Text.Contains("Second paragraph") && before==after,
                "R241 read both Word paragraphs without modifying source");
            Make(macro,start,true);
            Assert(!Read(macro).Ok && Read(macro).Status.Contains("macros"),
                "R241 macro-containing package refused even if renamed .docx");
            Make(unsafeXml,"<!DOCTYPE x [<!ENTITY a SYSTEM \"file:///C:/private\">]>"+
                "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\">"+
                "<w:p><w:t>&a;</w:t></w:p></w:document>");
            Assert(!Read(unsafeXml).Ok,
                "R241 DTD/external XML entities forbidden");
            Make(huge,"<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\">"+
                "<w:p><w:t>"+new string('A',2*1024*1024+1)+
                "</w:t></w:p></w:document>");
            Assert(!Read(huge).Ok,
                "R241 huge decompressed document XML refused");
            File.WriteAllText(corrupt,"not a valid Office zip package");
            Assert(!Read(corrupt).Ok && !Read(normal+".missing").Ok,
                "R241 invalid ZIP and missing file fail closed");
            Assert(type.GetMethod("Create",flags)!.Invoke(null,
                new object[]{normal}) is System.Windows.Controls.StackPanel,
                "R241 Word preview is native WPF Deck child, not an Office launch");
            Console.WriteLine(
                "R241 PASS: two-paragraph read-only .docx text, SHA unchanged, "+
                "macro/DTD/oversize/corrupt/missing refused.");
        }
        finally{Directory.Delete(folder,true);}
    }

'@
Swap '    private static void TestTextDeckPreviewV240()' ($method+
 '    private static void TestTextDeckPreviewV240()') 'native fixture tests'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R241: native Word document preview, DTD/macro/ZIP bound and source SHA tests registered.'
