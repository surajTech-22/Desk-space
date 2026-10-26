$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$file='r133/publish-r133.ps1'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($file).Replace($crlf,$lf)
function Swap([string]$a,[string]$b,[string]$label){
 $count=([regex]::Matches($script:s,[regex]::Escape($a))).Count
 if($count -ne 1){throw "R137 publishing $label anchor count $count"}
 $script:s=$script:s.Replace($a,$b)
}
Swap @'
      Write-Host ("R133 release-event delivered "+$message+
                  " receipt="+$received.id)
      $done=$true
'@ @'
      # GitHub's publisher checks replay-cache visibility. A POST receipt
      # cannot prove that any specific Windows device received the event.
      $confirmed=$false
      for($read=1;$read -le 3;$read++){
        try{
          $readback=Invoke-WebRequest -UseBasicParsing -Uri (
            "https://ntfy.sh/"+$topic+"/json?poll=1&since=latest") `
            -TimeoutSec 15
          $events=@([string]$readback.Content -split "\n" |
            Where-Object { $_.Trim() -match '^\{' })
          foreach($line in $events){
            $event=$line|ConvertFrom-Json
            if($event.id -eq $received.id -and
               $event.topic -eq $topic -and
               $event.message -eq $message){$confirmed=$true;break}
          }
          if($confirmed){break}
        }catch{
          Write-Warning ("R137 relay cache verification retry "+$read+
                         ": "+$_.Exception.Message)
        }
        if($read -lt 3){Start-Sleep -Seconds 2}
      }
      if(-not $confirmed){throw "Event not found in relay replay cache: $message"}
      Write-Host ("R137 event available for reconnecting clients: "+$message+
                  " id="+$received.id)
      $done=$true
'@ 'check relay replay cache'
[IO.File]::WriteAllText($file,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R137 publisher verifies relay replay visibility after publish, not individual desktop delivery.'