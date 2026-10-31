$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$crlf=[string][char]13+[char]10;$lf=[string][char]10
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$old,[string]$next,[string]$why){
 $old=$old.Replace($crlf,$lf);$next=$next.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($n -ne 1){throw ('R275 '+$why+' anchor='+$n)}
 $script:s=$script:s.Replace($old,$next)
}
Swap '                TestClipboardConversionRecoveryV274();' @'
                TestClipboardConversionRecoveryV274();
                TestSafeBatchRenameApplyUndoV275();
'@ 'registered native test'
$method=@'
    private static void TestSafeBatchRenameApplyUndoV275()
    {
        var preview=Product.GetType(
            "DeskSpace.DeskSpaceBatchRenamePreviewV254",true)!;
        var engine=Product.GetType(
            "DeskSpace.DeskSpaceBatchRenameTransactionV275",true)!;
        const BindingFlags flags=BindingFlags.Static|BindingFlags.NonPublic;
        var inspect=preview.GetMethod("InspectFolder",flags)!;
        var apply=engine.GetMethod("Apply",flags)!;
        var undo=engine.GetMethod("Undo",flags)!;
        object Plan(string path,string prefix="P-",string suffix="")=>
            inspect.Invoke(null,new object[]{path,prefix,suffix})!;
        object Apply(string path,object expected,string prefix="P-",
            System.Threading.CancellationToken cancel=default)=>
            apply.Invoke(null,new object[]{path,expected,prefix,"",cancel})!;
        bool Success(object result)=>(bool)result.GetType()
            .GetProperty("Success")!.GetValue(result)!;
        object? Receipt(object result)=>result.GetType()
            .GetProperty("Undo")!.GetValue(result);
        object Undo(object receipt)=>
            undo.Invoke(null,new object[]{receipt,
                System.Threading.CancellationToken.None})!;
        var root=Path.Combine(outdir,"r275-batch-apply-undo-"+
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string First(string file)=>Path.Combine(root,file);
            File.WriteAllText(First("a.txt"),"ALPHA");
            File.WriteAllText(First("b.txt"),"BETA");
            var plan=Plan(root);
            Assert(Success(Apply(root,plan)) &&
                File.Exists(First("P-a.txt"))&&
                File.Exists(First("P-b.txt"))&&
                !File.Exists(First("a.txt")) &&
                File.ReadAllText(First("P-a.txt"))=="ALPHA",
                "R275 approved batch must rename all eligible files without modifying content");
            // First result has already moved files; it must not be
            // replayed to perform a second rename.
            var retry=Apply(root,plan);
            Assert(!Success(retry) && File.Exists(First("P-a.txt")),
                "R275 consumed preview cannot be applied twice");
            var receiptType=engine.GetNestedType("Receipt",BindingFlags.NonPublic)!;
            var receipt=Activator.CreateInstance(receiptType,new object[]{
                root,
                (System.Collections.IReadOnlyList<object>)Array.Empty<object>()
            });
            // Inspect real Apply result and retrieve its exact immutable receipt.
            // Original source plan cannot match post-rename state.
            File.Move(First("P-a.txt"),First("a.txt"));
            File.Move(First("P-b.txt"),First("b.txt"));
            var result=Apply(root,Plan(root));
            Assert(Success(result) && Receipt(result)!=null,
                "R275 successful batch must return undo receipt");
            Assert(Success(Undo(Receipt(result)!)) &&
                File.ReadAllText(First("a.txt"))=="ALPHA" &&
                File.ReadAllText(First("b.txt"))=="BETA" &&
                !File.Exists(First("P-a.txt")),
                "R275 explicit Undo must restore exactly two originals and contents");
            var renamed=Apply(root,Plan(root));
            Assert(Success(renamed),"R275 test setup rename");
            File.WriteAllText(First("P-a.txt"),"CHANGED AFTER APPLY");
            Assert(!Success(Undo(Receipt(renamed)!)) &&
                File.Exists(First("P-a.txt"))&&
                !File.Exists(First("a.txt")),
                "R275 undo must fail closed on post-rename modified content");
            File.Move(First("P-a.txt"),First("a.txt"));
            File.Move(First("P-b.txt"),First("b.txt"));
            File.WriteAllText(First("P-a.txt"),"OCCUPIED");
            var occupied=Apply(root,Plan(root));
            Assert(!Success(occupied) &&
                File.ReadAllText(First("a.txt"))=="CHANGED AFTER APPLY" &&
                File.ReadAllText(First("P-a.txt"))=="OCCUPIED",
                "R275 hidden/new occupied destinations must never be overwritten");
            File.Delete(First("P-a.txt"));
            var planBefore=Plan(root);
            File.WriteAllText(First("new-file.txt"),"NEW");
            Assert(!Success(Apply(root,planBefore)) &&
                File.Exists(First("a.txt")) &&
                !File.Exists(First("P-a.txt")),
                "R275 folder change after preview must invalidate Apply");
            File.Delete(First("new-file.txt"));
            var cancelled=new System.Threading.CancellationToken(true);
            Assert(!Success(Apply(root,Plan(root),"P-",cancelled)) &&
                File.Exists(First("a.txt")),
                "R275 cancellation must leave originals untouched");
            // Failure after first rename (a) must roll back a when b is
            // blocked by a live exclusive Windows file handle.
            var lockedPlan=Plan(root);
            using(var hold=new FileStream(First("b.txt"),FileMode.Open,
                FileAccess.ReadWrite,FileShare.None))
            {
                var blocked=Apply(root,lockedPlan);
                Assert(!Success(blocked) &&
                    File.Exists(First("a.txt")) &&
                    !File.Exists(First("P-a.txt")),
                    "R275 one locked source must roll back earlier successful renames");
            }
            Console.WriteLine("R275 PASS: 2-file Apply/Undo, no overwrite, "+
                "updated-file Undo refusal, drift rejection, cancellation "+
                "and rollback after actual locked-file failure.");
        }
        finally
        {
            try{Directory.Delete(root,true);}catch(IOException){}
            catch(UnauthorizedAccessException){}
        }
    }

'@
Swap '    private static void TestClipboardConversionRecoveryV274()' ($method+
    '    private static void TestClipboardConversionRecoveryV274()') 'native method'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R275 native real Windows filesystem Apply, collision, Undo, cancel and mid-batch rollback fixture registered.'
