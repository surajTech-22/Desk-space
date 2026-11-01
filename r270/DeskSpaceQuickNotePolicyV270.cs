using System;

namespace DeskSpace;

// Explicitly edited user notes only. This validator has no IO, clipboard,
// Windows hooks, scheduling, network, external app launch or persistence.
internal static class DeskSpaceQuickNotePolicyV270
{
    internal const int MaxTitleChars=120;
    internal const int MaxBodyChars=16384;
    internal static bool TryPrepare(
        string? proposedTitle,string? proposedBody,
        out string title,out string content,out string reason)
    {
        title=(proposedTitle??"").Trim();
        content=(proposedBody??"").Trim();
        reason="";
        if(title.Length==0 || title.Length>MaxTitleChars)
        {
            reason="Enter a title of 1–120 characters.";
            return false;
        }
        if(content.Length==0 || content.Length>MaxBodyChars)
        {
            reason="Enter note text of 1–16,384 characters.";
            return false;
        }
        foreach(var character in title+content)
        {
            if(character=='\0' ||
               char.IsControl(character) &&
               character is not ('\t' or '\r' or '\n'))
            {
                reason="Note contains unsupported control characters.";
                return false;
            }
        }
        return true;
    }
}
