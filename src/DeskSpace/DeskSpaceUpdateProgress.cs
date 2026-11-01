using System;
namespace DeskSpace;

// A small in-memory notification channel, no polling loop, network or persistent secrets.
public static class DeskSpaceUpdateProgress
{
    private static readonly object Sync = new();
    public static event Action? Changed;
    private static string label = "";
    private static int? percent;
    public static bool Active { get; private set; }
    public static (bool Active, string Label, int? Percent) Snapshot()
    {
        lock (Sync) return (Active, label, percent);
    }
    public static void Publish(string message, int? percentage = null)
    {
        lock (Sync)
        {
            Active = true;
            label = message;
            percent = percentage.HasValue ? Math.Clamp(percentage.Value, 0, 100) : null;
        }
        Changed?.Invoke();
    }
    public static void Clear()
    {
        lock (Sync) { Active = false; label = ""; percent = null; }
        Changed?.Invoke();
    }
}
