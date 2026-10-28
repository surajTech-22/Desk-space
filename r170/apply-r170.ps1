$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='r36/UiSmoke/Program.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
$old=@'
                    var frame=new System.Windows.Threading.DispatcherFrame();
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                        new Action(()=>frame.Continue=false),
                        System.Windows.Threading.DispatcherPriority.Background);
                    System.Windows.Threading.Dispatcher.PushFrame(frame);
                    System.Threading.Thread.Sleep(10);
'@.Replace($crlf,$lf)
$new=@'
                    // Never wait on a queued Background callback which may
                    // be starved by active drag/monitor input. A 40-ms
                    // high-priority timer ALWAYS closes this nested pump.
                    var frame=new System.Windows.Threading.DispatcherFrame();
                    var stopper=new System.Windows.Threading.DispatcherTimer(
                        System.Windows.Threading.DispatcherPriority.Send,
                        System.Windows.Threading.Dispatcher.CurrentDispatcher){
                        Interval=TimeSpan.FromMilliseconds(40)};
                    stopper.Tick+=(_,_)=>{
                        stopper.Stop();
                        frame.Continue=false;
                    };
                    stopper.Start();
                    try{System.Windows.Threading.Dispatcher.PushFrame(frame);}
                    finally{stopper.Stop();}
                    System.Threading.Thread.Sleep(10);
'@.Replace($crlf,$lf)
$count=([regex]::Matches($s,[regex]::Escape($old))).Count
if($count -ne 1){throw "R170 expected unique R160 nested WPF pump, found $count"}
$s=$s.Replace($old,$new)
$log='            // Simulate drop on right half: Copy without ZIP.'
if(([regex]::Matches($s,[regex]::Escape($log))).Count -ne 1){throw 'R170 R160 copy checkpoint missing'}
$s=$s.Replace($log,'            Console.WriteLine("R170 BEGIN: R160 Copy and ZIP staging acceptance");'+$lf+$log)
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R170: bounded real WPF R160 copy/ZIP test pump, no indefinite Background DispatcherFrame starvation.'
