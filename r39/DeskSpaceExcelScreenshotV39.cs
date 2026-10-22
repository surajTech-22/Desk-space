using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace DeskSpace;

// Same review model for both inputs. Live Excel gives authoritative native
// addresses/formulas; screenshot OCR supplies source-visible text and layout
// positions ONLY. Never invent Excel addresses or silently repair OCR text.
internal static class DeskSpaceExcelScreenshotV39
{
    public static async Task<DeskSpaceExcelTemplateV16> AnalyzeAsync(
        byte[] image,string name,string region)
    {
        if(image.Length==0 || image.Length>12000000)
            throw new InvalidDataException("Screenshot must be 1–12 MB.");
        using var stream=new InMemoryRandomAccessStream();
        using(var writer=new DataWriter(stream))
        {
            writer.WriteBytes(image);
            await writer.StoreAsync();
            writer.DetachStream();
        }
        stream.Seek(0);
        var decoder=await BitmapDecoder.CreateAsync(stream);
        using var bitmap=await decoder.GetSoftwareBitmapAsync();
        var engine=OcrEngine.TryCreateFromUserProfileLanguages();
        if(engine==null)
            throw new InvalidOperationException(
                "Windows OCR language is unavailable. Install an OCR-compatible language under Windows Language settings.");
        var ocr=await engine.RecognizeAsync(bitmap);
        int width=(int)decoder.PixelWidth,height=(int)decoder.PixelHeight;
        double x0=0,y0=0,x1=width,y1=height;
        if(!string.IsNullOrWhiteSpace(region))
        {
            var q=region.Split(',');
            if(q.Length==4 &&
               double.TryParse(q[0],System.Globalization.NumberStyles.Float,
                 System.Globalization.CultureInfo.InvariantCulture,out var rx) &&
               double.TryParse(q[1],System.Globalization.NumberStyles.Float,
                 System.Globalization.CultureInfo.InvariantCulture,out var ry) &&
               double.TryParse(q[2],System.Globalization.NumberStyles.Float,
                 System.Globalization.CultureInfo.InvariantCulture,out var rw) &&
               double.TryParse(q[3],System.Globalization.NumberStyles.Float,
                 System.Globalization.CultureInfo.InvariantCulture,out var rh))
            {
                x0=Math.Clamp(rx*width,0,width);
                y0=Math.Clamp(ry*height,0,height);
                x1=Math.Clamp((rx+rw)*width,x0,width);
                y1=Math.Clamp((ry+rh)*height,y0,height);
            }
        }
        var lines=new List<(double X,double Y,double Right,double Bottom,string Text)>();
        foreach(var line in ocr.Lines)
        {
            if(line.Words.Count==0)continue;
            double lx=line.Words.Min(w=>w.BoundingRect.X);
            double ly=line.Words.Min(w=>w.BoundingRect.Y);
            double right=line.Words.Max(w=>w.BoundingRect.X+w.BoundingRect.Width);
            double bottom=line.Words.Max(w=>w.BoundingRect.Y+w.BoundingRect.Height);
            if((lx+right)*.5<x0||(lx+right)*.5>x1||
               (ly+bottom)*.5<y0||(ly+bottom)*.5>y1)continue;
            var raw=line.Text.Trim();
            if(raw.Length==0)continue;
            lines.Add((lx,ly,right,bottom,raw));
        }
        lines=lines.OrderBy(x=>x.Y).ThenBy(x=>x.X).ToList();
        var entry=new DeskSpaceExcelTemplateV16 {
            Name=string.IsNullOrWhiteSpace(name)?"Screenshot selection":name.Trim(),
            SourceWorkbook="",
            SourceSheet="Screenshot (not linked to Excel)",
            SourceAddress="Unverified screenshot coordinates",
            SourceRow=0,SourceColumn=0,
            SourceKind="Screenshot OCR",
            CoordinatesVerified=false,
            ScreenshotBase64=Convert.ToBase64String(image),
            ScreenshotSelection=region
        };
        if(lines.Count==0)return entry;
        // Group text lines into visual rows by source-image vertical spacing.
        var rowY=new List<double>();
        double medianHeight=lines.Select(l=>l.Bottom-l.Y).OrderBy(z=>z)
            .ElementAt(lines.Count/2);
        double tolerance=Math.Max(3,medianHeight*.54);
        foreach(var line in lines)
        {
            int row=rowY.FindIndex(y=>Math.Abs(y-line.Y)<=tolerance);
            if(row<0){row=rowY.Count;rowY.Add(line.Y);}
            // Column is only visual order inside a row, NOT an Excel column.
            var sameRow=entry.Cells.Where(c=>c.RowOffset==row).ToArray();
            int col=sameRow.Length;
            bool looksNumber=double.TryParse(line.Text.TrimEnd('%'),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,out _);
            bool header=!looksNumber && (line.Text.EndsWith(":") ||
                (col==0 && lines.Count>3));
            var cell=new DeskSpaceExcelCellV16{
                RowOffset=row,ColumnOffset=col,
                CellAddress="",AddressVerified=false,
                Role=header?"Reference":"Value",
                FixedOrVariable=header?"Fixed label":"Review",
                RawValue=line.Text,Display=line.Text,
                ValueKind="text",
                ImageX=line.X/width,ImageY=line.Y/height,
                ImageWidth=(line.Right-line.X)/width,
                ImageHeight=(line.Bottom-line.Y)/height
            };
            entry.Cells.Add(cell);
        }
        entry.RowCount=rowY.Count;
        entry.ColumnCount=entry.Cells.GroupBy(x=>x.RowOffset)
            .Select(g=>g.Count()).DefaultIfEmpty(0).Max();
        return entry;
    }
}
