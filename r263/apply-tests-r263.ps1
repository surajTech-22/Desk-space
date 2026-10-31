$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$a='                TestPinnedRecentDocumentChoicesV262();'
if(([regex]::Matches($s,[regex]::Escape($a))).Count -ne 1){
 throw 'R263 native invocation missing'
}
$s=$s.Replace($a,$a+$lf+'                TestExcelSuggestionRecheckV263();')
$anchor='    private static void TestPinnedRecentDocumentChoicesV262()'
$method=@'
    private static void TestExcelSuggestionRecheckV263()
    {
        var compareType=Product.GetType(
            "DeskSpace.DeskSpaceExcelSuggestionReviewV263",true)!;
        var proposalType=Product.GetType(
            "DeskSpace.DeskSpaceExcelProposalV16",true)!;
        var changeType=Product.GetType(
            "DeskSpace.DeskSpaceExcelChangeV16",true)!;
        var method=compareType.GetMethod("SameTarget",
            BindingFlags.Static|BindingFlags.NonPublic)!;
        object Build(string workbook="C:\\Test\\Synthetic.xlsx",
            string sheet="Sheet1",string previous="",
            string next="123",string target="B2",bool verified=true,
            bool signature=true,string decision="Ready")
        {
            var proposal=Activator.CreateInstance(proposalType)!;
            void Set(object obj,string field,object value)=>
                obj.GetType().GetProperty(field)!.SetValue(obj,value);
            Set(proposal,"WorkbookFullName",workbook);
            Set(proposal,"SheetName",sheet);
            Set(proposal,"StartRow",2);
            Set(proposal,"StartColumn",2);
            Set(proposal,"Verified",verified);
            Set(proposal,"UsedReferenceSignatureV120",signature);
            var change=Activator.CreateInstance(changeType)!;
            Set(change,"Row",2);Set(change,"Column",2);
            Set(change,"CellAddress",target);
            Set(change,"PreviousRaw",previous);
            Set(change,"Previous",previous);
            Set(change,"NewValue",next);
            Set(change,"Decision",decision);
            ((System.Collections.IList)proposalType.GetProperty(
                "Changes")!.GetValue(proposal)!).Add(change);
            return proposal;
        }
        bool Same(object oldProposal,object newProposal)=>
            (bool)method.Invoke(null,new object[]{oldProposal,newProposal})!;
        var original=Build();
        Assert(Same(original,Build()),"R263 identical verified target review accepted");
        Assert(!Same(original,Build(workbook="C:\\Test\\Different.xlsx")) &&
               !Same(original,Build(sheet="Other")) &&
               !Same(original,Build(previous="another value")) &&
               !Same(original,Build(next="456")) &&
               !Same(original,Build(target="C2")) &&
               !Same(original,Build(verified=false)) &&
               !Same(original,Build(signature=false)) &&
               !Same(original,Build(decision="Skip")) &&
               !Same(original,Build(workbook="")) &&
               !Same(original,Build(sheet="")),
            "R263 workbook, sheet, old/new content, address, reference signature and "+
            "writable state must remain identical between consent and apply");
        Assert(!Same(original,Build(decision="Skip")) &&
               !Same(original,null!),
            "R263 corrupted or null previews cannot trigger Excel mutation");
        Console.WriteLine("R263 PASS: exact confirmed Excel workbook/sheet/"+
            "cell values, destination address and signature revalidated before paste.");
    }

'@
if(([regex]::Matches($s,[regex]::Escape($anchor))).Count -ne 1){
 throw 'R263 native method anchor missing'
}
$s=$s.Replace($anchor,$method.Replace($crlf,$lf)+$anchor)
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R263: native WPF Excel after-consent stale-target rejection tests registered.'
