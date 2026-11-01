$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$old,[string]$new,[string]$why)
{
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $count=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($count -ne 1){throw "R272 $why anchor count=$count"}
 $script:s=$script:s.Replace($old,$new)
}
Swap '                TestRecurringSnoozeRecoveryV271();' @'
                TestRecurringSnoozeRecoveryV271();
                TestMediaDeckRestoreV272();
'@ 'register media test'
$method=@'
    private static void TestMediaDeckRestoreV272()
    {
        var type=Product.GetType("DeskSpace.DeskSpaceMediaV18",true)!;
        const BindingFlags f=BindingFlags.Instance|BindingFlags.NonPublic;
        var visible=type.GetField("visible",f)!;
        var dismiss=type.GetMethod("DismissPanelV123",f)!;
        var restore=type.GetMethod("RestoreDeckV272",f)!;
        var command=type.GetMethod("DeckCommandV123",f)!;
        var shown=type.GetProperty("DeckVisibleV123",f)!;
        var closed=type.GetProperty("DeckDismissedV272",f)!;
        var player=(IDisposable)Activator.CreateInstance(type)!;
        try
        {
            Assert(!(bool)restore.Invoke(player,null)! &&
                !(bool)closed.GetValue(player)!,
                "R272 unavailable media cannot restore or create a phantom Deck");
            visible.SetValue(player,true);
            Assert((bool)shown.GetValue(player)!,
                "R272 an active media session should have Deck controls");
            dismiss.Invoke(player,null);
            Assert((bool)closed.GetValue(player)! &&
                !(bool)shown.GetValue(player)!,
                "R272 closing Media Player must hide transport without altering playback");
            Assert((bool)restore.Invoke(player,null)! &&
                (bool)shown.GetValue(player)! &&
                !(bool)closed.GetValue(player)! &&
                !(bool)restore.Invoke(player,null)!,
                "R272 a deliberate restore reopens same-track controls exactly once");
            dismiss.Invoke(player,null);
        }
        finally{player.Dispose();}
        Assert(!(bool)restore.Invoke(player,null)! &&
            !(bool)shown.GetValue(player)!,
            "R272 disposed media must never restore controls");
        // This should return before issuing any system media/volume key.
        command.Invoke(player,new object[]{"mute"});
        command.Invoke(player,new object[]{"next"});
        Console.WriteLine("R272 PASS: media close/reopen same track, no phantom "+
            "player, double restore or post-disposal audio controls.");
    }

'@
Swap '    private static void TestRecurringSnoozeRecoveryV271()' ($method+
 '    private static void TestRecurringSnoozeRecoveryV271()') 'native method insertion'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R272 native WPF tests cover dismissed Media Deck, same-track reopen and disposed command safety.'
