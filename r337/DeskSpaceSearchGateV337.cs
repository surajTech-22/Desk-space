namespace DeskSpace;

// A single query must finish or be cancelled before another full-disk
// search starts. Indexing fires frequently, but is not user input and must
// never cancel an in-flight query every refresh tick.
internal sealed class DeskSpaceSearchGateV337
{
    private long lastRevision=long.MinValue;
    private bool queued;
    internal bool Active{get;private set;}
    internal bool Begin()
    {
        if(Active){queued=true;return false;}
        Active=true;
        return true;
    }
    internal void QueueUserChange(){if(Active)queued=true;}
    internal bool RefreshTick(long revision)
    {
        if(revision==lastRevision)return false;
        lastRevision=revision;
        if(Active){queued=true;return false;}
        return true;
    }
    internal bool Finish()
    {
        Active=false;
        bool refresh=queued;
        queued=false;
        return refresh;
    }
}
