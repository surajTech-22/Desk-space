$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$old,[string]$after,[string]$why){
 $old=$old.Replace($crlf,$lf);$after=$after.Replace($crlf,$lf)
 $count=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($count -ne 1){throw ('R283 '+$why+' patch anchor count='+$count)}
 $script:s=$script:s.Replace($old,$after)
}
Swap '                TestRunningAppCountAndDeckClearanceV282();' @'
                TestRunningAppCountAndDeckClearanceV282();
                TestModernDesignSystemV283();
'@ 'register design WPF acceptance'
$method=@'
    private static void TestModernDesignSystemV283()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceDesignV283",true)!;
        var appearance=Product.GetType(
            "DeskSpace.DeskSpaceAppearanceV148",true)!;
        const BindingFlags f=BindingFlags.Static|BindingFlags.NonPublic;
        var resolve=type.GetMethod("Resolve",f)!;
        var windowStyle=type.GetMethod("ApplyWindow",f)!;
        var deckStyle=type.GetMethod("ApplyDeck",f)!;
        var makeButton=type.GetMethod("Button",f)!;
        var makeInput=type.GetMethod("Input",f)!;
        var makeCard=type.GetMethod("Card",f)!;
        var sanitize=appearance.GetMethod("Sanitize",f)!;
        var purple=System.Windows.Media.Color.FromRgb(130,145,255);
        object Palette(bool dark,bool high)=>
            resolve.Invoke(null,new object[]{dark,high,purple})!;
        object Get(object palette,string field)=>
            palette.GetType().GetProperty(field)!.GetValue(palette)!;
        var dark=Palette(true,false);
        var light=Palette(false,false);
        var high=Palette(true,true);
        Assert((bool)Get(dark,"Dark") &&
            !(bool)Get(light,"Dark") &&
            (bool)Get(high,"HighContrast") &&
            (System.Windows.Media.Color)Get(dark,"Text")!=
                (System.Windows.Media.Color)Get(dark,"Surface") &&
            (System.Windows.Media.Color)Get(light,"Canvas")!=
                (System.Windows.Media.Color)Get(dark,"Canvas") &&
            (System.Windows.Media.Color)Get(high,"Text")==
                System.Windows.SystemColors.WindowTextColor,
            "R283 palettes must support dark, light and native Windows "+
            "high-contrast text without inventing another settings store");
        var actual=new Window{
            Title="R283 design smoke",Width=480,Height=340,
            ShowInTaskbar=false
        };
        try
        {
            windowStyle.Invoke(null,new object[]{actual,dark});
            Assert(actual.UseLayoutRounding &&
                actual.SnapsToDevicePixels &&
                actual.FontFamily.Source.Contains("Segoe UI"),
                "R283 modern WPF surfaces must use readable typography "+
                "and pixel-aligned layout at mixed DPI");
            var panel=new Border{Child=new TextBlock{Text="Running Apps"}};
            deckStyle.Invoke(null,new object[]{panel});
            Assert(panel.CornerRadius.TopLeft>=15 &&
                panel.BorderThickness.Left>=1,
                "R283 real Deck panel must have modern rounded chrome "+
                "and explicit readable border");
            var button=new Button{Content="Open notes"};
            makeButton.Invoke(null,new object?[]{button,true,dark});
            Assert(button.Template!=null && button.MinHeight>=34 &&
                   System.Windows.Automation.AutomationProperties.GetName(button)
                       =="Open notes",
                "R283 primary action must remain native, keyboard-focusable "+
                "and named for screen readers");
            var templateRoot=button.Template!.LoadContent() as Border;
            Assert(templateRoot!=null &&
                   templateRoot.CornerRadius.TopLeft>=9,
                "R283 primary action chrome must be rounded without "+
                "changing Button Click behavior");
            var text=new TextBox();
            makeInput.Invoke(null,new object?[]{text,dark});
            Assert(text.MinHeight>=36 && text.Padding.Left>=9,
                "R283 text field must have a usable touch/keyboard target");
            var card=(Border)makeCard.Invoke(null,
                new object?[]{new TextBlock{Text="Local-first"},dark})!;
            Assert(card.CornerRadius.TopLeft>=14 &&
                card.Child is TextBlock,
                "R283 cards must contain actual content, not a static mockup");
            var data=Activator.CreateInstance(appearance,true)!;
            appearance.GetProperty("Style")!.SetValue(data,"Modern");
            appearance.GetProperty("Mode")!.SetValue(data,"Dark");
            var sanitized=sanitize.Invoke(null,new[]{data})!;
            Assert((string)appearance.GetProperty("Style")!.GetValue(sanitized)!
                   =="Modern",
                "R283 modern appearance must be persistable and supported");
            appearance.GetProperty("Style")!.SetValue(data,"Soft");
            Assert((string)appearance.GetProperty("Style")!.GetValue(
                    sanitize.Invoke(null,new[]{data})!)!=="Soft",
                "R283 old Soft appearance presets must remain compatible");
            appearance.GetProperty("Style")!.SetValue(data,"Material");
            Assert((string)appearance.GetProperty("Style")!.GetValue(
                    sanitize.Invoke(null,new[]{data})!)!=="Material",
                "R283 old Material presets must remain compatible");
            Console.WriteLine("R283 PASS: real WPF rounded buttons, "+
                "Deck chrome, themes, high contrast, keyboard targets "+
                "and backward-compatible presets.");
        }
        finally{actual.Close();}
    }

'@
Swap '    private static void TestRunningAppCountAndDeckClearanceV282()' ($method+
 '    private static void TestRunningAppCountAndDeckClearanceV282()') 'native modern WPF method'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R283 native WPF modern design, high contrast and controls registered.'
