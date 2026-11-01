using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace DeskSpace;

// R198: in-memory, explicit-only conversions. No clipboard listener,
// persistence, cloud call, script execution or automatic paste.
internal static class DeskSpaceTextConversionsV198
{
    internal const int MaxInput=262144;
    internal static string Convert(string input,string operation)
    {
        ArgumentNullException.ThrowIfNull(input);
        if(input.Length>MaxInput)
            throw new ArgumentException("Input exceeds 256 KiB conversion limit.");
        string result=operation switch
        {
            "Format JSON"=>FormatJson(input),
            "Minify JSON"=>MinifyJson(input),
            "CSV to Markdown table"=>CsvToMarkdown(input),
            "Unique lines"=>UniqueLines(input),
            "Sort lines"=>SortLines(input),
            "Trim lines"=>TrimLines(input),
            _=>throw new ArgumentOutOfRangeException(nameof(operation))
        };
        if(result.Length>MaxInput*2)
            throw new ArgumentException("Conversion output exceeds safe preview limit.");
        return result;
    }
    private static string FormatJson(string input)
    {
        using var doc=JsonDocument.Parse(input,new JsonDocumentOptions
        {
            AllowTrailingCommas=false,CommentHandling=JsonCommentHandling.Disallow,
            MaxDepth=32
        });
        return JsonSerializer.Serialize(doc.RootElement,new JsonSerializerOptions
        {WriteIndented=true});
    }
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
        var set=new HashSet<string>(StringComparer.Ordinal);
        return string.Join(Environment.NewLine,
            input.Replace("\r\n","\n",StringComparison.Ordinal).Replace('\r','\n')
                .Split('\n').Where(set.Add));
    }
    private static string CsvToMarkdown(string text)
    {
        var rows=new List<List<string>>();
        var row=new List<string>();
        var cell=new StringBuilder();
        bool quoted=false,endedQuote=false,insideField=false;
        void EndCell()
        {
            if(row.Count>=24)throw new FormatException("CSV supports at most 24 columns.");
            if(cell.Length>4096)throw new FormatException("CSV cell is too long.");
            row.Add(cell.ToString());cell.Clear();
            endedQuote=false;insideField=false;
        }
        void EndRow()
        {
            EndCell();
            if(rows.Count>=200)throw new FormatException("CSV supports at most 200 rows.");
            rows.Add(row);
            row=new List<string>();
        }
        for(int i=0;i<text.Length;i++)
        {
            char c=text[i];
            if(quoted)
            {
                if(c=='"')
                {
                    if(i+1<text.Length && text[i+1]=='"')
                    {cell.Append('"');i++;}
                    else{quoted=false;endedQuote=true;}
                }
                else {cell.Append(c);if(cell.Length>4096)
                    throw new FormatException("CSV cell is too long.");}
                continue;
            }
            if(c==','){EndCell();continue;}
            if(c=='\r'||c=='\n')
            {
                if(c=='\r'&&i+1<text.Length&&text[i+1]=='\n')i++;
                EndRow();continue;
            }
            if(c=='"' && !insideField && cell.Length==0 && !endedQuote)
            {quoted=true;insideField=true;continue;}
            if(c=='"'||endedQuote)
                throw new FormatException("Invalid RFC-style CSV quoting.");
            cell.Append(c);insideField=true;
            if(cell.Length>4096)throw new FormatException("CSV cell is too long.");
        }
        if(quoted)throw new FormatException("Unterminated CSV quotation.");
        if(row.Count>0 || cell.Length>0 || endedQuote || insideField)
            EndRow();
        if(rows.Count==0||rows[0].Count==0||rows[0].All(string.IsNullOrWhiteSpace))
            throw new FormatException("CSV header is missing.");
        int width=rows[0].Count;
        if(rows.Any(r=>r.Count!=width))
            throw new FormatException("CSV rows have different column counts.");
        string Escape(string value)=>value.Replace("\\","\\\\",StringComparison.Ordinal)
            .Replace("|","\\|",StringComparison.Ordinal)
            .Replace("\r\n"," ↵ ",StringComparison.Ordinal).Replace('\n',' ')
            .Replace('\r',' ').Replace('\t',' ');
        var output=new StringBuilder();
        void Line(IEnumerable<string> values)
        {
            output.Append("| ");
            output.Append(string.Join(" | ",values.Select(Escape)));
            output.AppendLine(" |");
        }
        Line(rows[0]);
        Line(Enumerable.Repeat("---",width));
        foreach(var value in rows.Skip(1))Line(value);
        return output.ToString();
    }
}
