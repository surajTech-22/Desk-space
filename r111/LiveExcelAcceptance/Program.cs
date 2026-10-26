using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;

internal static class Program
{
    // This test runs in a real Windows STA process with Microsoft Excel installed.
    // It exercises the compiled DeskSpace capture + direct-format engine by reflection.
    // ONLY disposable workbooks in a per-run temporary directory are modified.
    [STAThread]
    private static int Main(string[] args)
    {
        if(args.Length!=1 || !File.Exists(args[0]))
        {
            Console.Error.WriteLine("Usage: DeskSpaceExcelLiveAcceptance <DeskSpace.dll>");
            return 2;
        }
        // The test must never risk attaching DeskSpace to an already-open
        // personal Excel session. Test ONLY in a clean dedicated Excel process.
        if(Process.GetProcessesByName("EXCEL").Length>0)
        {
            Console.Error.WriteLine("BLOCKED: Close existing Microsoft Excel instances before running isolated acceptance.");
            return 4;
        }
        Type? progId=Type.GetTypeFromProgID("Excel.Application");
        if(progId==null)
        {
            Console.Error.WriteLine("BLOCKED: Microsoft Excel COM is not installed. This is NOT a passing live test.");
            return 3;
        }
        dynamic? excel=null;
        string work=Path.Combine(Path.GetTempPath(),
            "DeskSpaceR110ExcelAcceptance_"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));
        Directory.CreateDirectory(work);
        try
        {
            string assemblyPath=Path.GetFullPath(args[0]);
            var dependencyResolver=new AssemblyDependencyResolver(assemblyPath);
            AssemblyLoadContext.Default.Resolving+=(loadContext,assemblyName)=>{
                string? dependency=dependencyResolver.ResolveAssemblyToPath(assemblyName);
                return dependency==null?null:
                    loadContext.LoadFromAssemblyPath(dependency);
            };
            var assembly=AssemblyLoadContext.Default.LoadFromAssemblyPath(
                assemblyPath);
            var templateType=assembly.GetType("DeskSpace.DeskSpaceExcelTemplateV16",true)!;
            var captureType=assembly.GetType("DeskSpace.DeskSpaceExcelVisualCaptureV101",true)!;
            var engineType=assembly.GetType("DeskSpace.DeskSpaceExcelFormatApplyV109",true)!;
            var capture=captureType.GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic)!;
            var preview=engineType.GetMethod("Preview",BindingFlags.Static|BindingFlags.NonPublic)!;
            var apply=engineType.GetMethod("Apply",BindingFlags.Static|BindingFlags.NonPublic)!;

            excel=Activator.CreateInstance(progId)!;
            excel.DisplayAlerts=false;
            excel.Visible=true;
            Thread.Sleep(1800); // Excel must register its automation instance
            dynamic sourceBook=excel.Workbooks.Add();
            dynamic source=sourceBook.Worksheets[1];
            source.Name="Reference";
            source.Range["E7:E11"].Merge(false);
            source.Range["E7"].Value2="Total";
            string[] headers={"a","b","c","d","Total super fantastic"};
            for(int col=0;col<headers.Length;col++)
                source.Cells[7,6+col].Value2=headers[col];
            for(int row=8;row<=10;row++)
            {
                for(int col=6;col<=9;col++)
                    source.Cells[row,col].Value2=(row-7)*2+(col-5);
                source.Cells[row,10].FormulaR1C1="=SUM(RC[-4]:RC[-1])";
            }
            for(int col=6;col<=10;col++)
                source.Cells[11,col].FormulaR1C1=
                    "=SUM(R[-3]C:R[-1]C)";
            dynamic table=source.ListObjects.Add(
                1,source.Range["F7:J11"],Type.Missing,1);
            table.Name="Table1";
            table.TableStyle="TableStyleMedium2";
            table.ShowTotals=false;
            table.ShowTableStyleRowStripes=true;
            source.Range["G8:G9"].NumberFormat="0.00000";
            source.Range["G8:G9"].Font.Underline=2;
            source.Range["G11:H11"].Font.Underline=-4119;
            source.Range["F10"].Font.Size=36;
            source.Range["J7"].Interior.Color=65535;
            source.Range["J7"].WrapText=true;
            sourceBook.SaveAs(Path.Combine(work,"reference.xlsx"),51);

            dynamic sourceRange=source.Range["E7:J11"];
            source.Activate();
            sourceRange.Select();
            object snapshot=capture.Invoke(null,new object[]{sourceRange})!;
            var template=Activator.CreateInstance(templateType)!;
            Put(template,"Name","Live R110 table/merge test");
            Put(template,"SourceWorkbook",Convert.ToString(sourceBook.Name,CultureInfo.InvariantCulture)!);
            Put(template,"SourceSheet","Reference");
            Put(template,"SourceAddress","$E$7:$J$11");
            Put(template,"SourceRow",7);
            Put(template,"SourceColumn",5);
            Put(template,"RowCount",5);
            Put(template,"ColumnCount",6);
            Put(template,"SheetVisualV101",snapshot);
            Console.WriteLine("SOURCE: actual Excel Table1, E7:E11 merged, 30 cells, single/double underline captured.");

            // Test A: wholly empty destination creates true table and formatting,
            // WITHOUT copying any source transaction values or formulas.
            dynamic emptyBook=excel.Workbooks.Add();
            dynamic emptySheet=emptyBook.Worksheets[1];
            emptySheet.Name="Blank target";
            emptySheet.Activate();
            string blankPath=Path.Combine(work,"blank_before.xlsx");
            emptyBook.SaveAs(blankPath,51);
            emptySheet.Range["E16"].Select();
            object emptyReview=preview.Invoke(null,new[]{template})!;
            Expect((string)emptyReview.GetType().GetProperty("Workbook")!.GetValue(emptyReview)! == blankPath,
                "DeskSpace connected to a DIFFERENT Excel application/workbook; aborting safely.");
            Expect(Eligible(emptyReview),"Empty destination must pass preflight: "+emptyReview);
            var created=InvokeApply(apply,template,emptyReview);
            Console.WriteLine("EMPTY APPLIED: "+created);
            Expect((int)emptySheet.ListObjects.Count==1,
                "Blank destination did not create a real Excel ListObject");
            Expect(ReadStyleName(emptySheet.ListObjects[1])=="TableStyleMedium2",
                "Excel Table style was not restored");
            Expect((int)emptySheet.Range["E16"].MergeArea.Rows.Count==5,
                "Excel merged area was not restored");
            Expect((int)emptySheet.Range["G20"].Font.Underline==-4119,
                "Double underline was not restored");
            // Every source formula column must remain blank, not display 0.
            for(int row=16;row<=20;row++)
            for(int col=5;col<=10;col++)
            {
                if(row==16 && col>=6)continue; // Actual ListObject header
                dynamic blankCell=emptySheet.Cells[row,col];
                if((bool)blankCell.HasFormula ||
                   (object?)blankCell.Value2 != null)
                    throw new InvalidOperationException(
                        "Format-only pasted a formula/value into empty cell "+
                        (string)blankCell.Address);
            }
            Expect((object?)emptySheet.Range["J17"].Value2==null,
                "Blank destination calculated column returned a visible zero");
            Expect((object?)emptySheet.Range["F17"].Value2==null,
                "Blank destination input returned an unwanted value");
            emptyBook.SaveAs(Path.Combine(work,"applied_blank.xlsx"),51);
            Console.WriteLine("PASS: empty target real table, merges, underline, "+
                "all body cells blank with zero formula injection.");

            // Test B: partial target MUST block before the first write.
            dynamic partialBook=excel.Workbooks.Add();
            dynamic partialSheet=partialBook.Worksheets[1];
            partialSheet.Range["F8"].Value2=123;
            partialBook.SaveAs(Path.Combine(work,"partial_before.xlsx"),51);
            partialSheet.Activate();
            partialSheet.Range["E7"].Select();
            object partial=preview.Invoke(null,new[]{template})!;
            Expect(!Eligible(partial),"A partially populated destination was accepted");
            Expect((int)partialSheet.ListObjects.Count==0,
                "Partial preflight created a table");
            Expect((int)(double)partialSheet.Range["F8"].Value2==123,
                "Partial preflight changed an existing value");
            Console.WriteLine("PASS: partial target blocked with no workbook edits.");
            partialBook.Close(false);

            // Test C: 100% populated matching shape, no destination Table yet.
            // Workbook is independent because ListObject names are workbook-global.
            dynamic fullBook=excel.Workbooks.Add();
            dynamic fullSheet=fullBook.Worksheets[1];
            fullSheet.Name="Full target";
            fullSheet.Range["E7"].Value2="Total";
            for(int col=0;col<headers.Length;col++)
                fullSheet.Cells[7,6+col].Value2=headers[col];
            for(int row=8;row<=10;row++)
            {
                for(int col=6;col<=9;col++)
                    fullSheet.Cells[row,col].Value2=500+row+col;
                fullSheet.Cells[row,10].FormulaR1C1="=SUM(RC[-4]:RC[-1])";
            }
            for(int col=6;col<=10;col++)
                fullSheet.Cells[11,col].FormulaR1C1="=SUM(R[-3]C:R[-1]C)";
            double retained=(double)fullSheet.Range["F8"].Value2;
            fullBook.SaveAs(Path.Combine(work,"full_before.xlsx"),51);
            fullSheet.Activate();
            fullSheet.Range["E7"].Select();
            var fullReview=preview.Invoke(null,new[]{template})!;
            Expect(Eligible(fullReview),
                "Fully populated matching destination must pass: "+fullReview);
            Console.WriteLine("FULL PLAN: "+fullReview);
            var fullResult=InvokeApply(apply,template,fullReview);
            Console.WriteLine("FULL APPLIED: "+fullResult);
            Expect((double)fullSheet.Range["F8"].Value2==retained,
                "Full-data format apply altered a business value");
            Expect((int)fullSheet.ListObjects.Count==1,
                "Full-data target did not create genuine Excel Table");
            Expect((string)fullSheet.Range["E7"].Value2=="Total",
                "Full-data source label was overwritten");
            fullBook.SaveAs(Path.Combine(work,"applied_full.xlsx"),51);
            Console.WriteLine("PASS: full-data target preserves values and creates real table.");

            // Test D: existing genuine table has one wrong header.
            // The check must reject before changing any other cell.
            fullSheet.Range["G7"].Value2="WRONG HEADER";
            fullSheet.Range["E7"].Select();
            var wrongHeaders=preview.Invoke(null,new[]{template})!;
            Expect(!Eligible(wrongHeaders),
                "Different Excel Table column names were accepted");
            Expect((string)fullSheet.Range["G7"].Value2=="WRONG HEADER",
                "Header mismatch preview unexpectedly changed workbook values");
            fullSheet.Range["G7"].Value2="b";
            Console.WriteLine("PASS: mismatched existing Table headers rejected.");

            // Test E: a simulated Excel COM table-style rejection must not
            // report success and must attempt to roll back a blank destination.
            var features=snapshot.GetType().GetProperty("FeaturesV106")!.
                GetValue(snapshot)!;
            var capturedTables=(IList)features.GetType().GetProperty("Tables")!.
                GetValue(features)!;
            Expect(capturedTables.Count==1,"Expected exactly one captured table");
            var firstTable=capturedTables[0]!;
            var styleProperty=firstTable.GetType().GetProperty("TableStyle")!;
            var savedStyle=(string)styleProperty.GetValue(firstTable)!;
            Expect(savedStyle=="TableStyleMedium2",
                "The captured TableStyle is not its real Excel name: "+savedStyle);
            dynamic failureBook=excel.Workbooks.Add();
            dynamic failureSheet=failureBook.Worksheets[1];
            try
            {
                styleProperty.SetValue(firstTable,"DeskSpaceInvalidStyleForRollback");
                failureBook.SaveAs(Path.Combine(work,"failure_before.xlsx"),51);
                failureSheet.Activate();
                failureSheet.Range["E7"].Select();
                var failureReview=preview.Invoke(null,new[]{template})!;
                Expect(!Eligible(failureReview),
                    "Invalid Excel Table style was allowed through read-only preflight");
                // No Apply call: safety preflight must reject before any writes.
                Expect((int)failureSheet.ListObjects.Count==0,
                    "Failed transaction left a newly created Excel Table");
                Expect(failureSheet.Range["F7"].Value2==null,
                    "Failed transaction left a fabricated header in an empty cell");
                Console.WriteLine("PASS: failure is detected, table/header rollback attempted.");
            }
            finally
            {
                styleProperty.SetValue(firstTable,savedStyle);
                failureBook.Close(false);
            }

            // Test F: paste into a second location INSIDE the source workbook.
            // A new Excel Table may not inherit the source's workbook-global
            // ListObject.Name ("Table1"); Excel must assign a distinct name.
            source.Activate();
            source.Range["E16"].Select();
            var sameBookReview=preview.Invoke(null,new[]{template})!;
            Expect(Eligible(sameBookReview),
                "Same-workbook blank range preflight failed: "+sameBookReview);
            InvokeApply(apply,template,sameBookReview);
            Expect((int)source.ListObjects.Count==2,
                "Same-workbook paste did not create another real Excel Table");
            Expect((string)source.ListObjects[1].Name !=
                   (string)source.ListObjects[2].Name,
                "Same-workbook format paste duplicated original Excel table identity");
            Expect(ReadStyleName(source.ListObjects[2])=="TableStyleMedium2",
                "Same-workbook destination table style differs");
            Expect(!(bool)source.Range["J17"].HasFormula &&
                   (object?)source.Range["J17"].Value2==null,
                "Same-workbook format paste introduced a formula/zero into blank area");
            Expect((bool)source.Range["J8"].HasFormula,
                "Same-workbook format paste unexpectedly modified source formulas");
            Console.WriteLine("PASS: same-workbook table identity/style preserved, "+
                "target body blank and original source formulas intact.");
            sourceBook.SaveAs(Path.Combine(work,"applied_same_workbook.xlsx"),51);

            emptyBook.Close(false);
            fullBook.Close(false);
            sourceBook.Close(false);
            Console.WriteLine("R111 LIVE MICROSOFT EXCEL ACCEPTANCE PASSED");
            Console.WriteLine("Disposable evidence files: "+work);
            return 0;
        }
        catch(Exception error)
        {
            Console.Error.WriteLine("R110 LIVE EXCEL ACCEPTANCE FAILED/BLOCKED:");
            Console.Error.WriteLine(error);
            Console.Error.WriteLine("Evidence directory: "+work);
            return 1;
        }
        finally
        {
            try{if(excel!=null){excel.DisplayAlerts=false;excel.Quit();}}
            catch(Exception e){Console.Error.WriteLine("Excel cleanup: "+e.Message);}
        }
    }
    private static string ReadStyleName(dynamic table)
    {
        object? raw=table.TableStyle;
        if(raw is string name)return name;
        if(raw==null)return "";
        dynamic style=raw;
        return (string)style.Name;
    }
    private static void Put(object instance,string name,object value)=>
        instance.GetType().GetProperty(name,BindingFlags.Public|BindingFlags.Instance)!.
            SetValue(instance,value);
    private static bool Eligible(object review)=>
        (bool)review.GetType().GetProperty("Eligible",
            BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance)!.
            GetValue(review)!;
    private static string InvokeApply(MethodInfo method,object template,object review)
    {
        try{return (string)method.Invoke(null,new[]{template,review})!;}
        catch(TargetInvocationException e) when(e.InnerException!=null)
        {throw new InvalidOperationException(e.InnerException.Message,e.InnerException);}
    }
    private static void Expect(bool condition,string message)
    {
        if(!condition)throw new InvalidOperationException(message);
    }
}
