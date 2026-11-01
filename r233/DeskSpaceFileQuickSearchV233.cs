using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace DeskSpace;

// Fast, bounded filename search while the process-wide all-drive index warms up.
// This does not inspect file contents, follow junctions or hide index failures.
internal static class DeskSpaceFileQuickSearchV233
{
    private const int MaximumDirectories = 1200;
    private const int MaximumMatches = 75;

    private static IEnumerable<string> DefaultRoots()
    {
        yield return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        foreach (var drive in DriveInfo.GetDrives())
        {
            string? readyRoot = null;
            try { if (drive.IsReady) readyRoot = drive.RootDirectory.FullName; }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            if (readyRoot != null) yield return readyRoot;
        }
    }

    // An unqualified term searches only filenames, NOT all children of a
    // directory whose parent path happens to contain that term.
    internal static bool MatchesName(string name, string term) =>
        !string.IsNullOrWhiteSpace(name) &&
        name.Contains(term, StringComparison.OrdinalIgnoreCase);

    internal static bool MatchesIndexedEntry(string name, string path, string term)
    {
        if (MatchesName(name, term)) return true;
        // Preserve explicit path-fragment searches, never broad implicit
        // directory-name matching for ordinary filename queries.
        return (term.Contains('\\') || term.Contains('/')) &&
               path.Contains(term, StringComparison.OrdinalIgnoreCase);
    }

    internal static List<SearchHit> Find(
        string term, CancellationToken token, string? driveFilter = null,
        IEnumerable<string>? searchRoots = null)
    {
        var hits = new List<SearchHit>();
        if (string.IsNullOrWhiteSpace(term) || term.Length < 2 ||
            term.StartsWith("content:", StringComparison.OrdinalIgnoreCase))
            return hits;

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();
        foreach (var root in searchRoots ?? DefaultRoots())
        {
            token.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(root)) continue;
            try
            {
                if (!Directory.Exists(root)) continue;
                var full = Path.GetFullPath(root);
                if (driveFilter != null &&
                    !full.StartsWith(driveFilter, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (visited.Add(full)) queue.Enqueue(full);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                or ArgumentException or NotSupportedException or System.Security.SecurityException) { }
        }

        int scanned = 0;
        while (queue.Count > 0 && scanned < MaximumDirectories &&
               hits.Count < MaximumMatches)
        {
            token.ThrowIfCancellationRequested();
            var directory = queue.Dequeue();
            scanned++;
            string[] files, children;
            try
            {
                files = Directory.GetFiles(directory);
                children = Directory.GetDirectories(directory);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                or ArgumentException or System.Security.SecurityException) { continue; }

            foreach (var file in files)
            {
                token.ThrowIfCancellationRequested();
                if (!MatchesName(Path.GetFileName(file), term) || !found.Add(file)) continue;
                hits.Add(new SearchHit {
                    Category = "Files", Name = Path.GetFileName(file),
                    OpenTarget = file, Location = file
                });
                if (hits.Count >= MaximumMatches) break;
            }
            foreach (var child in children)
            {
                if (visited.Contains(child)) continue;
                try
                {
                    var attributes = File.GetAttributes(child);
                    if ((attributes & (FileAttributes.ReparsePoint |
                        FileAttributes.Hidden | FileAttributes.System)) != 0)
                        continue;
                    if (visited.Add(child)) queue.Enqueue(child);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                    or ArgumentException or System.Security.SecurityException) { }
            }
        }
        return hits;
    }
}
