using System;
using System.IO;

namespace DeskSpace;

// Hiding the below-circle deck never clears its held files or workspace data.
internal static class DeskSpaceDeckVisibilityV89
{
    private static readonly object Gate=new();
    private static bool? cached;
    internal static event Action<bool>? Changed;
    internal static string Filename=>Path.Combine(WorkspaceStore.Root,
        "below-circle-deck-visibility.txt");

    internal static bool Visible
    {
        get
        {
            lock(Gate)
            {
                if(cached.HasValue)return cached.Value;
                try
                {
                    cached=!File.Exists(Filename) ||
                        !string.Equals(File.ReadAllText(Filename).Trim(),
                            "hidden",StringComparison.OrdinalIgnoreCase);
                }
                catch(IOException){cached=true;}
                catch(UnauthorizedAccessException){cached=true;}
                return cached.Value;
            }
        }
    }

    internal static bool TrySet(bool visible,out string error)
    {
        error="";
        lock(Gate)
        {
            if(Visible==visible)return true;
            string temp=Filename+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                Directory.CreateDirectory(WorkspaceStore.Root);
                File.WriteAllText(temp,visible?"visible":"hidden");
                File.Move(temp,Filename,true);
            }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException)
            {
                error=ex.Message;
                return false;
            }
            finally
            {
                try{if(File.Exists(temp))File.Delete(temp);}
                catch(IOException){}
                catch(UnauthorizedAccessException){}
            }
            cached=visible;
        }
        Changed?.Invoke(visible);
        return true;
    }
}
