$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$r='DeskSpace_Windows_R1/DeskSpace'
function Swap([string]$path,[string]$before,[string]$after,[string]$label) {
  $lf=[string][char]10;$crlf=[string][char]13+$lf
  $s=[IO.File]::ReadAllText($path).Replace($crlf,$lf)
  $a=$before.Replace($crlf,$lf);$b=$after.Replace($crlf,$lf)
  $hits=([regex]::Matches($s,[regex]::Escape($a))).Count
  if($hits -ne 1){throw "R132 $label expected exactly one anchor; found $hits"}
  [IO.File]::WriteAllText($path,$s.Replace($a,$b),[Text.UTF8Encoding]::new($false))
}
$format="$r/DeskSpaceExcelFormatApplyV109.cs"
Swap $format @'
    private static Checked Inspect(DeskSpaceExcelTemplateV16 template)
'@ @'
    // ListObject.TableStyle is a Variant: Excel can return a COM TableStyle
    // object instead of a string. Never compare the COM wrapper's ToString().
    private static string TableStyleNameV132(object? raw)
    {
        if(raw is string name)return name;
        if(raw==null)return "";
        if(Marshal.IsComObject(raw))
        {
            dynamic style=raw;
            object? name=style.Name;
            if(name is string resolved)return resolved;
        }
        return "";
    }
    private static Checked Inspect(DeskSpaceExcelTemplateV16 template)
'@ 'normalize COM TableStyle variants in readback'
Swap $format @'
            var current=Text((object?)table.TableStyle);
'@ @'
            var current=TableStyleNameV132((object?)table.TableStyle);
'@ 'actual current table style rather than System._ComObject'
Swap $format @'
            var observed=Text((object?)table.TableStyle);
'@ @'
            var observed=TableStyleNameV132((object?)table.TableStyle);
'@ 'actual table style readback verification'
Swap $format @'
            created.Name=table.Name;
            SetTableStyleV131(created,table.TableStyle);
'@ @'
            // Excel ListObject names are workbook-global. The source may
            // still have Table1 in this SAME workbook: preserve Excel's unique
            // generated destination name, never steal/duplicate source ID.
            SetTableStyleV131(created,table.TableStyle);
'@ 'avoid source table name collision in same workbook'

$editor="$r/DeskSpaceExcelEditorV42.cs"
Swap $editor @'
        editorHead.Children.Add(mappingNameV42);
'@ @'
        editorHead.Children.Add(mappingNameV42);
        // Original collection and mapping inputs, side-by-side.
        // Reparent the EXISTING text boxes so their save/change handlers stay.
        var collectionLabelV132=editorHead.Children.OfType<TextBlock>()
            .Single(x=>x.Text=="Collection name (groups related data together)");
        var dataLabelV132=editorHead.Children.OfType<TextBlock>()
            .Single(x=>x.Text=="Data name (saved Excel mapping)");
        editorHead.Children.Remove(collectionLabelV132);
        editorHead.Children.Remove(mappingGroupV42);
        editorHead.Children.Remove(dataLabelV132);
        editorHead.Children.Remove(mappingNameV42);
        var pairedDataNamesV132=new Grid{Margin=new Thickness(0,1,0,5)};
        pairedDataNamesV132.ColumnDefinitions.Add(new ColumnDefinition{
            Width=new GridLength(1,GridUnitType.Star)});
        pairedDataNamesV132.ColumnDefinitions.Add(new ColumnDefinition{
            Width=new GridLength(1,GridUnitType.Star)});
        var leftDataNameV132=new StackPanel{Margin=new Thickness(0,0,8,0)};
        leftDataNameV132.Children.Add(collectionLabelV132);
        leftDataNameV132.Children.Add(mappingGroupV42);
        var rightDataNameV132=new StackPanel{Margin=new Thickness(8,0,0,0)};
        rightDataNameV132.Children.Add(dataLabelV132);
        rightDataNameV132.Children.Add(mappingNameV42);
        Grid.SetColumn(rightDataNameV132,1);
        pairedDataNamesV132.Children.Add(leftDataNameV132);
        pairedDataNamesV132.Children.Add(rightDataNameV132);
        editorHead.Children.Insert(1,pairedDataNamesV132);
'@ 'data collection and mapping names in one row'
Swap $editor @'
        formatHeadV126.Children.Add(formatGroupV130);
'@ @'
        formatHeadV126.Children.Add(formatGroupV130);
        // Same two-column editor for format/formula capture. Saved grouping,
        // UI bindings and the single shared Saved Mappings list are untouched.
        var formatLabelV132=formatHeadV126.Children.OfType<TextBlock>()
            .Single(x=>x.Text=="Format mapping name");
        var formatCollectionLabelV132=formatHeadV126.Children.OfType<TextBlock>()
            .Single(x=>x.Text=="Collection name (group related format mappings)");
        formatHeadV126.Children.Remove(formatLabelV132);
        formatHeadV126.Children.Remove(formatNameV124);
        formatHeadV126.Children.Remove(formatCollectionLabelV132);
        formatHeadV126.Children.Remove(formatGroupV130);
        var pairedFormatNamesV132=new Grid{Margin=new Thickness(0,1,0,5)};
        pairedFormatNamesV132.ColumnDefinitions.Add(new ColumnDefinition{
            Width=new GridLength(1,GridUnitType.Star)});
        pairedFormatNamesV132.ColumnDefinitions.Add(new ColumnDefinition{
            Width=new GridLength(1,GridUnitType.Star)});
        var leftFormatNameV132=new StackPanel{Margin=new Thickness(0,0,8,0)};
        leftFormatNameV132.Children.Add(formatCollectionLabelV132);
        leftFormatNameV132.Children.Add(formatGroupV130);
        var rightFormatNameV132=new StackPanel{Margin=new Thickness(8,0,0,0)};
        rightFormatNameV132.Children.Add(formatLabelV132);
        rightFormatNameV132.Children.Add(formatNameV124);
        Grid.SetColumn(rightFormatNameV132,1);
        pairedFormatNamesV132.Children.Add(leftFormatNameV132);
        pairedFormatNamesV132.Children.Add(rightFormatNameV132);
        formatHeadV126.Children.Insert(0,pairedFormatNamesV132);
'@ 'format collection and mapping names in one row'
Write-Host 'R132: normalizes Excel COM TableStyle on write/readback, avoids workbook table-name collision, aligns both naming panels without touching saved grouping.'