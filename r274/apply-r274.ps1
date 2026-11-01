$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
$lf=[string][char]10;$crlf=[string][char]13+$lf
function Swap([string]$path,[string]$before,[string]$after,[string]$reason)
{
 $before=$before.Replace($crlf,$lf);$after=$after.Replace($crlf,$lf)
 $content=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
 $count=([regex]::Matches($content,[regex]::Escape($before))).Count
 if($count -ne 1){throw ("R274 "+$reason+": expected one anchor; found "+$count)}
 [IO.File]::WriteAllText($path,$content.Replace($before,$after),
    [Text.UTF8Encoding]::new($false))
}
Swap "$root/DeskSpaceTextConversionsV198.cs" @'
            "Format JSON"=>FormatJson(input),
            "CSV to Markdown table"=>CsvToMarkdown(input),
            "Unique lines"=>UniqueLines(input),
'@ @'
            "Format JSON"=>FormatJson(input),
            "Minify JSON"=>MinifyJson(input),
            "CSV to Markdown table"=>CsvToMarkdown(input),
            "Unique lines"=>UniqueLines(input),
            "Sort lines"=>SortLines(input),
            "Trim lines"=>TrimLines(input),
'@ 'clipboard conversion'
Swap "$root/DeskSpaceTextConversionsV198.cs" @'
    private static string UniqueLines(string input)
    {
'@ @'
    private static string MinifyJson(string input)
    {
        using var doc=JsonDocument.Parse(input,new JsonDocumentOptions{
            AllowTrailingCommas=false,CommentHandling=JsonCommentHandling.Disallow,
            MaxDepth=32
        });
        return JsonSerializer.Serialize(doc.RootElement);
    }

    private static string Lines(string input)=>
        input.Replace("\r\n","\n",StringComparison.Ordinal)
             .Replace('\r','\n');

    private static string SortLines(string input)=>
        string.Join(Environment.NewLine,
            Lines(input).Split('\n')
                .OrderBy(line=>line,StringComparer.OrdinalIgnoreCase)
                .ThenBy(line=>line,StringComparer.Ordinal));

    private static string TrimLines(string input)=>
        string.Join(Environment.NewLine,
            Lines(input).Split('\n').Select(line=>line.Trim()));

    private static string UniqueLines(string input)
    {
'@ 'clipboard conversion'
Swap "$root/DeskSpaceClipboardTransformsV177.cs" @'
        "Format JSON","CSV to Markdown table","Unique lines"
'@ @'
        "Format JSON","Minify JSON","CSV to Markdown table",
        "Unique lines","Sort lines","Trim lines"
'@ 'clipboard conversion'
Swap "$root/DeskSpaceClipboardTransformsV177.cs" @'
            "Format JSON" or "CSV to Markdown table" or "Unique lines"=>
'@ @'
            "Format JSON" or "Minify JSON" or
            "CSV to Markdown table" or "Unique lines" or
            "Sort lines" or "Trim lines"=>
'@ 'clipboard conversion'
Swap "$root/DeskSpaceClipboardTransformsV177.cs" @'
        "JSON string" or "JSON object" or "Format JSON"=>".json",
'@ @'
        "JSON string" or "JSON object" or "Format JSON" or "Minify JSON"=>".json",
'@ 'clipboard conversion'
Swap "$root/DeskSpaceClipboardTransformsV177.cs" @'
            catch(ArgumentException ex){status.Text=ex.Message;}
'@ @'
            catch(Exception ex) when(ex is ArgumentException or
                FormatException or JsonException)
            {
                // Malformed user-selected text is an ordinary validation
                // outcome, not a reason for the entire UI to close.
                status.Text="Cannot convert: "+ex.Message;
            }
'@ 'clipboard conversion'
Swap "$root/DeskSpaceClipboardTransformsV177.cs" @'
                File.WriteAllText(dialog.FileName,output,new UTF8Encoding(false));
                status.Text="Saved by request.";
'@ @'
                // An incomplete write must not truncate a previous export.
                // Stage on the destination volume, then replace only after
                // the user explicitly approved the SaveFileDialog.
                var destination=dialog.FileName;
                var directory=Path.GetDirectoryName(destination)!;
                var temporary=Path.Combine(directory,
                    ".deskspace-"+Guid.NewGuid().ToString("N")+".partial");
                try
                {
                    File.WriteAllText(temporary,output,new UTF8Encoding(false));
                    File.Move(temporary,destination,true);
                }
                finally
                {
                    try{if(File.Exists(temporary))File.Delete(temporary);}
                    catch(IOException){}
                    catch(UnauthorizedAccessException){}
                }
                status.Text="Saved by request.";
'@ 'clipboard conversion'
Write-Host 'R274: invalid JSON/CSV stay inside editor; added minify/sort/trim; atomic Save As preserves old file on incomplete write.'