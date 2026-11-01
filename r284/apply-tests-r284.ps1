$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$old,[string]$after,[string]$why){
 $old=$old.Replace($crlf,$lf);$after=$after.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($n -ne 1){throw ('R284 '+$why+' expected 1 anchor, got '+$n)}
 $script:s=$script:s.Replace($old,$after)
}
Swap '                TestModernDesignSystemV283();' @'
                TestModernDesignSystemV283();
                TestModernDialogAccessibilityV284();
'@ 'native register'
$method=@'
    private static void TestModernDialogAccessibilityV284()
    {
        var design=Product.GetType("DeskSpace.DeskSpaceDesignV283",true)!;
        var settings=Product.GetType("DeskSpace.WidgetSettingsV3",true)!;
        var options=Product.GetType("DeskSpace.WidgetOptionsWindow",true)!;
        var pinned=Product.GetType("DeskSpace.AppPinsWindow",true)!;
        const BindingFlags f=BindingFlags.NonPublic|BindingFlags.Static;
        var buttonMethod=design.GetMethod("Button",f)!;
        var b=new Button{Content="Keyboard action"};
        buttonMethod.Invoke(null,new object?[]{b,false,null});
        var triggers=b.Template!.Triggers.OfType<System.Windows.Trigger>()
            .ToArray();
        Assert(triggers.Any(x=>
                x.Property==UIElement.IsKeyboardFocusedProperty) &&
            triggers.Any(x=>x.Property==UIElement.IsMouseOverProperty) &&
            b.IsTabStop,
            "R284 rounded native buttons must visibly mark keyboard focus "+
            "and hover, without replacing original Button click handling");
        var preferences=Activator.CreateInstance(settings)!;
        var window=(Window)Activator.CreateInstance(options,
            new object[]{preferences,(Action)(()=>{})})!;
        try
        {
            var scroll=window.Content as ScrollViewer;
            Assert(scroll!=null &&
                scroll.VerticalScrollBarVisibility==
                    ScrollBarVisibility.Auto,
                "R284 small-height widget options must scroll rather "+
                "than hiding important settings");
            var stack=scroll.Content as StackPanel;
            Assert(stack!=null && stack.Children.OfType<TextBlock>()
                .Any(x=>x.Text.Contains("Live Widget")),
                "R284 actual options dialog must show modern title and "+
                "retain native checkbox controls");
            var checkBoxes=stack.Children.OfType<CheckBox>().ToArray();
            Assert(checkBoxes.Length>=5 &&
                   checkBoxes.All(c=>c.MinHeight>=30),
                "R284 all original widget toggles must remain "+
                "keyboard-focusable and physically accessible");
            var save=stack.Children.OfType<Button>()
                .Single(x=>x.Content?.ToString()=="Save preferences");
            Assert(save.Template!=null &&
                   save.MinHeight>=34,
                "R284 save button remains a native focusable WPF control");
        }
        finally{window.Close();}
        var apps=(Window)Activator.CreateInstance(pinned,
            new object[]{preferences})!;
        try
        {
            var dock=apps.Content as DockPanel;
            Assert(dock!=null &&
                dock.Children.OfType<ListBox>().Count()==1 &&
                dock.Children.OfType<StackPanel>().Count()==1 &&
                dock.Children.OfType<TextBlock>()
                    .Any(x=>x.Text=="Pinned applications"),
                "R284 actual pinned-app dialog retains Add, Launch, Unpin "+
                "and its same single list");
            var row=dock.Children.OfType<StackPanel>().Single();
            Assert(row.Children.OfType<Button>().Count()==3 &&
                   row.Children.OfType<Button>().All(x=>
                        x.Template!=null && x.MinHeight>=34),
                "R284 pinned-app actions must use modern native focusable buttons");
        }
        finally{apps.Close();}
        Console.WriteLine("R284 PASS: native focus/hover triggers, "+
            "small-height scrolling preferences, preserved five toggles, "+
            "pinned app controls and accessible modern dialog buttons.");
    }

'@
Swap '    private static void TestModernDesignSystemV283()' ($method+
 '    private static void TestModernDesignSystemV283()') 'native WPF dialogs method'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R284 Windows native dialog accessibility and focus tests registered.'
