$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='DeskSpace_Windows_R1/DeskSpace/DeskSpaceDeckOffsetV228.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$before,[string]$after,[string]$label)
{
 $before=$before.Replace($crlf,$lf);$after=$after.Replace($crlf,$lf)
 $count=([regex]::Matches($script:s,[regex]::Escape($before))).Count
 if($count -ne 1){throw ("R235 "+$label+": expected one anchor, got "+$count)}
 $script:s=$script:s.Replace($before,$after)
}
Swap @'
using System.IO;
using System.Text.Json;
'@ @'
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
'@ 'add cross-process synchronization imports'
Swap @'
    private static string FilePath=>Path.Combine(WorkspaceStore.Root,
        "deck-per-monitor-v228.json");
'@ @'
    private static string FilePath=>Path.Combine(WorkspaceStore.Root,
        "deck-per-monitor-v228.json");
    // Both monitor widgets can be hosted by distinct DeskSpace processes.
    // The old lock(Sync) covered only threads in ONE process: competing
    // read-modify-write saves could overwrite the other display's offset.
    // Name the OS mutex by canonical local store, not by widget/device.
    private static string MutexName=>
        @"Local\DeskSpace-DeckOffsetMap-V235-"+
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            Path.GetFullPath(FilePath).ToUpperInvariant())))[..24];
'@ 'per-storage scoped Windows mutex'
Swap @'
        lock(Sync)
        {
            Directory.CreateDirectory(WorkspaceStore.Root);
'@ @'
        lock(Sync)
        {
            using var crossProcess=new Mutex(false,MutexName);
            bool acquired;
            try{acquired=crossProcess.WaitOne(TimeSpan.FromSeconds(2));}
            catch(AbandonedMutexException){acquired=true;}
            if(!acquired)
                throw new IOException("DeskSpace Deck offset file is busy; no position was overwritten.");
            try
            {
                Directory.CreateDirectory(WorkspaceStore.Root);
'@ 'serialize save across monitor processes'
Swap @'
            finally{if(File.Exists(tmp))File.Delete(tmp);}
        }
    }
}
'@ @'
                finally{if(File.Exists(tmp))File.Delete(tmp);}
            }
            finally{crossProcess.ReleaseMutex();}
        }
    }
}
'@ 'release interprocess lock on all save paths'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R235: independent Deck saves now synchronize across both DeskSpace processes by storage path.'
