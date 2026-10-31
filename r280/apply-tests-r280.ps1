$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$crlf=[string][char]13+[char]10;$lf=[string][char]10
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$old,[string]$new,[string]$why){
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($n -ne 1){throw ('R280 '+$why+' anchor='+$n)}
 $script:s=$script:s.Replace($old,$new)
}
Swap '                TestRunningWindowControlsV279();' @'
                TestRunningWindowControlsV279();
                TestSmartContextDeckOptInV280();
'@ 'native register'
$method=@'
    private static void TestSmartContextDeckOptInV280()
    {
        var context=Product.GetType(
            "DeskSpace.DeskSpaceSmartContextDeckV280",true)!;
        var classifier=Product.GetType(
            "DeskSpace.DeskSpaceContextSuggestionsV237",true)!;
        const BindingFlags f=BindingFlags.Static|BindingFlags.NonPublic;
        var actions=context.GetMethod("Actions",f)!;
        var state=context.GetProperty("State",f)!;
        var create=context.GetMethod("CreatePanel",f)!;
        var start=context.GetMethod("Start",f)!;
        var stop=context.GetMethod("Stop",f)!;
        var audit=context.GetMethod("Audit",f)!;
        var open=context.GetMethod("Open",f)!;
        var classify=classifier.GetMethod("Classify",f)!;
        string[] Names(string category)=>
            ((System.Collections.IEnumerable)actions.Invoke(
                null,new object?[]{category})!).Cast<object>().Select(x=>
                (string)x.GetType().GetProperty("Id")!.GetValue(x)!)
                .ToArray();
        bool Listening()=>(bool)state.GetValue(null)!.GetType()
            .GetProperty("Listening")!.GetValue(state.GetValue(null))!;
        stop.Invoke(null,null);
        Assert(!Listening() && Names("None").Length==0 &&
               Names("Excel").SequenceEqual(new[]{"excel","clipboard"}) &&
               Names("Files").SequenceEqual(new[]{"search","preview"}) &&
               Names("Browser").SequenceEqual(new[]{"notes","palette"}) &&
               Names("Media").SequenceEqual(new[]{"palette"}),
            "R280 context is off by default and suggestions are fixed category actions");
        Assert((string)classify.Invoke(null,new object[]{"EXCEL"})!=="Excel" &&
               (string)classify.Invoke(null,new object[]{"explorer"})!=="Files",
            "R280 reuses existing process-only category recognition");
        var owner=new Window{Title="R280 context smoke"};
        int refreshed=0;
        try
        {
            var off=(Border)create.Invoke(null,
                new object[]{owner,(Action)(()=>refreshed++)})!;
            var body=(StackPanel)off.Child;
            var header=(DockPanel)body.Children[0];
            var toggle=header.Children.OfType<Button>().Single();
            Assert(toggle.Content?.ToString()=="Start" &&
                   System.Windows.Automation.AutomationProperties.GetName(toggle)
                       =="Start Smart Context Deck monitoring" &&
                   body.Children.Count==2,
                "R280 actual Deck control must be opt-in and show no hidden actions");
            var summary=(string)audit.Invoke(null,new[]{state.GetValue(null)})!;
            Assert(summary.Contains("Off.") &&
                   summary.Contains("no session history retained"),
                "R280 off session must have no retained activity");
            bool refused=false;
            try{open.Invoke(null,new object[]{owner,"untrusted-user-command"});}
            catch(TargetInvocationException ex) when(
                ex.InnerException is ArgumentException){refused=true;}
            Assert(refused,
                "R280 arbitrary/unsafe context command must be refused");
            // A real user click starts only the Windows foreground-category
            // listener; a locked-down CI runner may refuse WinEvent hooks.
            toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if(Listening())
            {
                var on=(Border)create.Invoke(null,
                    new object[]{owner,(Action)(()=>refreshed++)})!;
                var controls=(StackPanel)on.Child;
                var halt=((DockPanel)controls.Children[0])
                    .Children.OfType<Button>().Single();
                Assert(halt.Content?.ToString()=="Stop" &&
                    System.Windows.Automation.AutomationProperties.GetName(halt)
                        =="Stop Smart Context Deck monitoring" &&
                    controls.Children.OfType<TextBlock>()
                        .Any(t=>t.Text.Contains("Privacy audit: Listening")),
                    "R280 started Deck must provide visible Stop and session privacy audit");
                halt.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert(!Listening(),"R280 Stop clears session and unhooks monitoring");
            }
            else
            {
                var after=(Border)create.Invoke(null,
                    new object[]{owner,(Action)(()=>refreshed++)})!;
                Assert(((StackPanel)after.Child).Children.OfType<TextBlock>()
                    .Any(t=>t.Text.Contains("Opt in")),
                    "R280 Windows hook denied must fail closed without recommendations");
            }
            Assert(Names("None").Length==0 && !Listening(),
                "R280 no recommendations remain after session ends");
            Console.WriteLine("R280 PASS: real Deck opt-in/stop WPF controls, "+
                "shared process-only classifier, audit, fixed action whitelist "+
                "and zero monitoring after Stop.");
        }
        finally{stop.Invoke(null,null);owner.Close();}
    }

'@
Swap '    private static void TestRunningWindowControlsV279()' ($method+
 '    private static void TestRunningWindowControlsV279()') 'native method'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R280: native context Deck opt-in, fixed actions, local audit and Stop checks registered.'
