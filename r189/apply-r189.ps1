$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root='DeskSpace_Windows_R1/DeskSpace'
Copy-Item -LiteralPath 'r189/DeskSpaceDocumentMetadataV189.cs' -Destination "$root/DeskSpaceDocumentMetadataV189.cs" -Force
$p="$root/DeskSpaceFilePreviewV85.cs"
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function One([string]$old,[string]$new,[string]$what){
 $old=$old.Replace($crlf,$lf);$new=$new.Replace($crlf,$lf)
 $hits=([regex]::Matches($script:s,[regex]::Escape($old))).Count
 if($hits -ne 1){throw ("R189 "+$what+" expected one source anchor, got "+$hits)}
 $script:s=$script:s.Replace($old,$new)
}
One @'
            if(!File.Exists(path))throw new FileNotFoundException();
'@ @'
            if(Directory.Exists(path))
            {
                // Direct children only; no recursion or linked-folder traversal.
                panel.Children.Add(DeskSpaceDocumentMetadataV189.Create(path));
            }
            else
            {
            if(!File.Exists(path))throw new FileNotFoundException();
'@ 'folder preview before FileInfo'
One @'
            if(Images.Contains(extension) && info.Length<=40*1024*1024)
'@ @'
            if(DeskSpaceDocumentMetadataV189.Supports(extension))
            {
                panel.Children.Add(DeskSpaceDocumentMetadataV189.Create(path));
            }
            else if(Images.Contains(extension) && info.Length<=40*1024*1024)
'@ 'Office package and WAV preview in existing Deck'
One @'
                Foreground=Brushes.LightSteelBlue});
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException
'@ @'
                Foreground=Brushes.LightSteelBlue});
            }
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException
'@ 'close folder/file source branches'
One @'
        var open=new Button{Content="Open file",Padding=new Thickness(8,5,8,5),
'@ @'
        var open=new Button{Content="Open file or folder",Padding=new Thickness(8,5,8,5),
'@ 'explicit shell-opening label'
One @'
            if(!File.Exists(path))return;
'@ @'
            if(!File.Exists(path) && !Directory.Exists(path))return;
'@ 'allow explicit open folder, never auto-open'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R189: Office parts, WAV header and one-level folder previews in existing Deck; explicit open button only.'
