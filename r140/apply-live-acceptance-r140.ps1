$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$file='r111/LiveExcelAcceptance/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($file).Replace($crlf,$lf)
function Swap([string]$a,[string]$b,[string]$label){
 $a=$a.Replace($crlf,$lf);$b=$b.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($a))).Count
 if($n -ne 1){throw "R140 Excel COM acceptance $($label) anchor $n"}
 $script:s=$script:s.Replace($a,$b)
}
Swap @'
            source.Range["J7"].WrapText=true;
            sourceBook.SaveAs(Path.Combine(work,"reference.xlsx"),51);
'@ @'
            source.Range["J7"].WrapText=true;
            // Full capture/write/readback stress matrix. The entire source
            // content stays in the original; the target copies ONLY formatting.
            source.Range["H9"].Font.ColorIndex=-4105; // automatic
            source.Range["I9"].Font.ThemeColor=5; // explicit theme
            source.Range["I9"].Font.TintAndShade=0.25;
            source.Range["I10"].Interior.Pattern=1;
            source.Range["I10"].Interior.ThemeColor=4;
            source.Range["I10"].Interior.TintAndShade=-0.20;
            source.Range["G10"].HorizontalAlignment=-4108;
            source.Range["G10"].VerticalAlignment=-4108;
            source.Range["G10"].WrapText=true;
            source.Range["H10"].Orientation=30;
            source.Range["J10"].Borders[9].LineStyle=1;
            source.Range["J10"].Borders[9].Color=65535;
            source.Range["J10"].Borders[9].Weight=2;
            source.Range["F9"].Font.Superscript=true;
            source.Range["G9"].Locked=false;
            source.Range["G9"].FormulaHidden=true;
            sourceBook.SaveAs(Path.Combine(work,"reference.xlsx"),51);
'@ 'add actual Excel source colour/theme/border/alignment/protection'
Swap @'
            Expect((int)emptySheet.Range["G20"].Font.Underline==-4119,
                "Double underline was not restored");
'@ @'
            Expect((int)emptySheet.Range["G20"].Font.Underline==-4119,
                "Double underline was not restored");
            Expect((int)emptySheet.Range["H18"].Font.ColorIndex==-4105,
                "Automatic font colour was turned into a direct theme/colour");
            Expect((int)emptySheet.Range["I18"].Font.ThemeColor==5,
                "Explicit font theme slot missing");
            Expect(Math.Abs((double)emptySheet.Range["I18"].Font.TintAndShade-0.25)<0.001,
                "Explicit font tint missing");
            Expect((int)emptySheet.Range["I19"].Interior.ThemeColor==4,
                "Explicit fill theme slot missing");
            Expect(Math.Abs((double)emptySheet.Range["I19"].Interior.TintAndShade+0.20)<0.001,
                "Explicit fill tint missing");
            Expect((int)emptySheet.Range["G19"].HorizontalAlignment==-4108 &&
                   (int)emptySheet.Range["G19"].VerticalAlignment==-4108,
                "Center alignment missing");
            Expect((bool)emptySheet.Range["G19"].WrapText,
                "Wrap Text missing");
            Expect((int)emptySheet.Range["H19"].Orientation==30,
                "Text rotation missing");
            Expect((int)emptySheet.Range["J19"].Borders[9].Color==65535 &&
                   (int)emptySheet.Range["J19"].Borders[9].Weight==2,
                "Border colour or weight missing");
            Expect((bool)emptySheet.Range["F18"].Font.Superscript,
                "Superscript missing");
            Expect(!(bool)emptySheet.Range["G18"].Locked &&
                   (bool)emptySheet.Range["G18"].FormulaHidden,
                "Captured input protection settings not restored");
'@ 'verify actual Excel direct font/fill colour and other formats'
[IO.File]::WriteAllText($file,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R140 real Microsoft Excel acceptance expanded: automatic K14 theme, explicit RGB/theme/tints, border colour/weight, alignment/orientation/wrap, underline, superscript, protection, tables/merges and formulas staying blank.'