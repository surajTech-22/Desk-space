$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p='DeskSpace_Windows_R1/DeskSpace/DeskSpaceDeckOffsetV228.cs'
$lf=[string][char]10;$crlf=[string][char]13+$lf
$s=[IO.File]::ReadAllText($p).Replace($crlf,$lf)
function Swap([string]$before,[string]$after,[string]$why)
{
 $before=$before.Replace($crlf,$lf);$after=$after.Replace($crlf,$lf)
 $n=([regex]::Matches($script:s,[regex]::Escape($before))).Count
 if($n -ne 1){throw ("R238 "+$why+": expected 1 anchor, found "+$n)}
 $script:s=$script:s.Replace($before,$after)
}
Swap @'
    internal static Offset Clamp(Offset shift,int max=4096)=>
        new(Math.Clamp(shift.X,-max,max),Math.Clamp(shift.Y,-max));
'@ @'
    internal static Offset Clamp(Offset shift,int max=4096)=>
        new(Math.Clamp(shift.X,-max,max),Math.Clamp(shift.Y,-max));

    // Parses the entire existing saved Deck layout before any mutation.
    // Corrupt, oversized, duplicate-cased or invalid data must never be
    // silently overwritten with a new empty map.
    internal static Dictionary<string,Offset> ParseForWrite(string? json,int byteLength)
    {
        if(byteLength<0 || byteLength>16_384)
            throw new InvalidDataException("Saved Deck positions exceed 16 KiB.");
        if(json==null)
            return new Dictionary<string,Offset>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string,Offset>? values;
        try{values=JsonSerializer.Deserialize<Dictionary<string,Offset>>(json);}
        catch(JsonException ex)
        {
            throw new InvalidDataException("Saved Deck positions are not valid JSON.",ex);
        }
        if(values==null || values.Count>128)
            throw new InvalidDataException("Saved Deck position list is invalid or too large.");
        var result=new Dictionary<string,Offset>(StringComparer.OrdinalIgnoreCase);
        foreach(var item in values)
        {
            if(string.IsNullOrWhiteSpace(item.Key) || item.Key.Length>128 ||
               item.Value==null || !result.TryAdd(item.Key,Clamp(item.Value)))
                throw new InvalidDataException("Saved Deck positions contain duplicate or invalid monitor identity.");
        }
        return result;
    }
'@ 'insert strict parsing and case-insensitive map'
Swap @'
                var map=JsonSerializer.Deserialize<Dictionary<string,Offset>>(
                    File.ReadAllText(FilePath));
                return map!=null&&map.TryGetValue(device,out var found)?
                    Clamp(found):new(0,0);
'@ @'
                var map=ParseForWrite(File.ReadAllText(FilePath),
                    checked((int)new FileInfo(FilePath).Length));
                return map.TryGetValue(device,out var found)?
                    Clamp(found):new(0,0);
'@ 'read restored per-monitor offsets case-insensitively'
Swap @'
            catch(Exception ex)when(ex is IOException or JsonException or
                UnauthorizedAccessException){return new(0,0);}
'@ @'
            catch(Exception ex)when(ex is IOException or JsonException or
                UnauthorizedAccessException or InvalidDataException){return new(0,0);}
'@ 'read invalid saved offset map safely'
Swap @'
            Dictionary<string,Offset> map=new(StringComparer.OrdinalIgnoreCase);
            try
            {
                if(File.Exists(FilePath) && new FileInfo(FilePath).Length<=16_384)
                    map=JsonSerializer.Deserialize<Dictionary<string,Offset>>(
                        File.ReadAllText(FilePath))??map;
            }
            catch(Exception ex)when(ex is IOException or JsonException or
                UnauthorizedAccessException){}
            map[device]=Clamp(value);
            var tmp=FilePath+"."+Guid.NewGuid().ToString("N")+".tmp";
'@ @'
            // The cross-process mutex is already held here (R235).
            // Preserve an existing malformed or oversized file for manual
            // recovery; do NOT overwrite independent positions on error.
            string? previous=null;
            int bytes=0;
            if(File.Exists(FilePath))
            {
                var size=new FileInfo(FilePath).Length;
                if(size>16_384)
                    throw new InvalidDataException("Existing Deck map is oversized; its contents were preserved.");
                bytes=checked((int)size);
                previous=File.ReadAllText(FilePath);
            }
            var map=ParseForWrite(previous,bytes);
            map[device]=Clamp(value);
            var serialized=JsonSerializer.Serialize(map);
            if(System.Text.Encoding.UTF8.GetByteCount(serialized)>16_384)
                throw new InvalidDataException("Updated Deck map exceeds 16 KiB; original kept.");
            var tmp=FilePath+"."+Guid.NewGuid().ToString("N")+".tmp";
'@ 'fail closed on a corrupt previous layout instead of losing other monitor state'
Swap '                File.WriteAllText(tmp,JsonSerializer.Serialize(map));' '                File.WriteAllText(tmp,serialized);' 'persist strictly validated map'
[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))
Write-Host 'R238: parse full monitor Deck map case-insensitively, reject malformed/oversized/duplicate files without overwriting prior state.'
