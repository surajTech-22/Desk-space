using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace DeskSpace;

// Opt-in widget-local history: no clipboard persistence, no original-file deletion,
// bounded memory footprint, and no pretence of knowing whether another app accepted paste.
public static class DeskSpaceClipboardV10
{
    public sealed record ClipboardEntry(string Text, DateTime Copied)
    { public override string ToString() => Copied.ToString("HH:mm")+"  "+(Text.Length>120?Text[..120]+"…":Text.Replace("\r"," ").Replace("\n"," ")); }
    private static readonly List<ClipboardEntry> Entries = new();
    private static readonly List<string> HeldFiles = new();
    private static string lastContent="";
    private static DateTime lastPoll=DateTime.MinValue;
    public static IReadOnlyList<ClipboardEntry> History=>Entries;
    public static IReadOnlyList<string> Shelf=>
        DeskSpaceDropShelfV15.IsActive ? DeskSpaceDropShelfV15.ActivePaths : HeldFiles;
    public static void Poll()
    {
        if((DateTime.UtcNow-lastPoll).TotalSeconds<2)return;
        lastPoll=DateTime.UtcNow;
        try
        {
            if(!Clipboard.ContainsText())return;
            string value=Clipboard.GetText();
            if(string.IsNullOrWhiteSpace(value)||value==lastContent)return;
            lastContent=value;
            if(value.Length>4096)value=value[..4096];
            Entries.Insert(0,new ClipboardEntry(value,DateTime.UtcNow));
            while(Entries.Count>30)Entries.RemoveAt(Entries.Count-1);
            Entries.RemoveAll(x=>DateTime.UtcNow-x.Copied>TimeSpan.FromMinutes(30));
        }
        catch(System.Runtime.InteropServices.COMException){}
        catch(InvalidOperationException){}
    }
    public static void HoldCurrentFiles()
    {
        if(DeskSpaceDropShelfV15.IsActive){
            DeskSpaceDropShelfV15.ImportClipboardFiles();
            return;
        }
        try
        {
            if(!Clipboard.ContainsFileDropList())return;
            HeldFiles.Clear();
            foreach(string file in Clipboard.GetFileDropList())
            {
                if(HeldFiles.Count>=25)break;
                if(File.Exists(file))HeldFiles.Add(file);
            }
        }
        catch(System.Runtime.InteropServices.COMException){}
    }
    // Keep secrets copied from DeskSpace Vault out of visible clipboard history.
    // Windows clipboard itself is still shared with destination applications.
    public static void CopySensitive(string secret)
    {
        try
        {
            lastContent=secret;
            Clipboard.SetText(secret);
            var timeout=new System.Windows.Threading.DispatcherTimer{
                Interval=TimeSpan.FromSeconds(30)
            };
            timeout.Tick+=(_,_)=>{
                timeout.Stop();
                try{if(Clipboard.ContainsText() && Clipboard.GetText()==secret)Clipboard.Clear();}
                catch(System.Runtime.InteropServices.COMException){}
            };
            timeout.Start();
        }
        catch(System.Runtime.InteropServices.COMException){}
    }
    public static void ClearShelf(){
        DeskSpaceDropShelfV15.ClearActive();
        HeldFiles.Clear();
    }
    public static void ClearHistory(){Entries.Clear();lastContent="";}
}
