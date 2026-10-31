using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace DeskSpace;

public abstract class Notifier : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
public sealed class NoteItem : Notifier
{
    public Guid Id { get; set; } = Guid.NewGuid();
    private string title = "New note", content = "";
    public string Title { get => title; set { title = value; Changed(); } }
    public string Content { get => content; set { content = value; Changed(); } }
    public DateTime Updated { get; set; } = DateTime.Now;
    public override string ToString() => Title;
}
public sealed class ReminderItem : Notifier
{
    public Guid Id { get; set; } = Guid.NewGuid();
    private string title = "";
    private DateTime due = DateTime.Now.AddHours(1);
    private bool done;
    public string Title { get => title; set { title = value; Changed(); } }
    public DateTime Due { get => due; set { due = value; Changed(); } }
    public bool Done { get => done; set { done = value; Changed(); } }
    public DateTime? LastAlert { get; set; }
    // Optional properties; legacy workspace.json files remain compatible.
    public string RepeatPattern { get; set; } = "None";
    public string RepeatDays { get; set; } = "";
    public int RepeatEveryDays { get; set; } = 1;
    public DateTime? SnoozedUntil { get; set; }
    public bool SnoozeOnly { get; set; }
    public List<DeskSpaceActionStep> AutomationSteps { get; set; } = new();
    public string TriggerCondition { get; set; } = "None";
    public string TriggerTarget { get; set; } = "";
    // Backwards compatible: existing workspace.json reminders default to
    // the original Windows sound and do not launch anything.
    public string AlertSound { get; set; } = "Exclamation";
    public string AlertSoundFile { get; set; } = "";
    public string AlertAction { get; set; } = "None";
    public string AlertTarget { get; set; } = "";
    public override string ToString() => $"{(Done ? "✓ " : "")}{Due:g}  {Title}";
}
public sealed class EventItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public DateTime Date { get; set; } = DateTime.Today;
    public string Recurrence { get; set; } = "None";
    public override string ToString() => $"{Date:d} — {Title}";
}
public sealed class SnippetItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New snippet";
    public string Text { get; set; } = "";
    public override string ToString() => Name;
}
public sealed class DocumentItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string StoredFilename { get; set; } = "";
    public DateTime Added { get; set; } = DateTime.Now;
    public override string ToString() => Name;
}
public sealed class DataTab
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New tab";
    public List<List<string>> Cells { get; set; } = new();
    public string? SourceImage { get; set; }
    public string? ExcelSnapshot { get; set; }
    public int ExcelSourceRow { get; set; } = 1;
    public int ExcelSourceCol { get; set; } = 1;
    public int ExcelRows { get; set; }
    public int ExcelColumns { get; set; }
    public bool ExcelSnapshotDirty { get; set; }
    public bool OcrNeedsReview { get; set; }
    public override string ToString() => Name + (OcrNeedsReview ? " ⚠ OCR review" : "");
}
public sealed class Workspace
{
    public List<NoteItem> Notes { get; set; } = new();
    public List<ReminderItem> Reminders { get; set; } = new();
    public List<EventItem> Events { get; set; } = new();
    public List<SnippetItem> Snippets { get; set; } = new();
    public List<DocumentItem> Documents { get; set; } = new();
    public List<DataTab> DataTabs { get; set; } = new();
}

public sealed class WorkspaceStore
{
    public static string Root { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeskSpace");
    public static string Attachments => Path.Combine(Root, "attachments");
    public static string DataFile => Path.Combine(Root, "workspace.json");
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public WorkspaceStore() { Directory.CreateDirectory(Root); Directory.CreateDirectory(Attachments); }
    public Workspace Load()
    {
        if (!File.Exists(DataFile)) return new Workspace();
        try { return JsonSerializer.Deserialize<Workspace>(File.ReadAllText(DataFile), Options) ?? new Workspace(); }
        catch (Exception ex)
        {
            throw new IOException($"Workspace could not be loaded. Original file is untouched: {DataFile}", ex);
        }
    }
    public void Save(Workspace model)
    {
        // Named mutex serializes writes from the two DeskSpace processes.
        // A unique temporary file prevents a failed writer from corrupting
        // the prior workspace.json. Preserve a last-known-good backup.
        using var gate=new System.Threading.Mutex(false,
            @"Local\DeskSpace.Workspace.JsonWrite");
        bool acquired=false;
        try
        {
            try{acquired=gate.WaitOne(TimeSpan.FromSeconds(10));}
            catch(System.Threading.AbandonedMutexException){acquired=true;}
            if(!acquired)throw new IOException("DeskSpace workspace is busy");
            var temp=DataFile+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                var serialized=JsonSerializer.Serialize(model,Options);
                File.WriteAllText(temp,serialized);
                if(File.Exists(DataFile))
                    File.Copy(DataFile,DataFile+".bak",true);
                File.Move(temp,DataFile,true);
            }
            finally
            {
                if(File.Exists(temp))File.Delete(temp);
            }
        }
        finally{if(acquired)gate.ReleaseMutex();}
    }
    public string ImportAttachment(string path)
    {
        var stored = Guid.NewGuid().ToString("N") + Path.GetExtension(path);
        File.Copy(path, Path.Combine(Attachments, stored));
        return stored;
    }
    public string ImportAttachmentPathForNew(string extension) => Guid.NewGuid().ToString("N") + extension;
    public string AttachmentPath(string relative) => Path.Combine(Attachments, Path.GetFileName(relative));
}
