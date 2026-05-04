using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace PensionCompass.Core.History;

/// <summary>
/// Reads and writes <see cref="RebalanceSession"/> JSON files in a folder. Each session is
/// one self-contained file named <c>YYYY-MM-DD_HHmmss_Provider.json</c> so listings sort
/// chronologically by filename alone and the user can tell at a glance which AI produced
/// which recommendation. Listing scans BOTH the configured sync folder (if any) AND the
/// LocalState fallback folder so a user changing the sync setting later doesn't appear
/// to lose old history.
/// </summary>
public static class RebalanceHistoryStore
{
    public const string HistoryFolderName = "History";

    /// <summary>Characters that are never safe in Windows filenames; replaced with "_" when encoded.</summary>
    private static readonly Regex UnsafeFileNameChars = new(@"[<>:""/\\|?*\x00-\x1F]+", RegexOptions.Compiled);

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Writes <paramref name="session"/> to <paramref name="historyFolderPath"/>, creating the folder
    /// if needed. Returns the full path of the written file. The caller picks <paramref name="historyFolderPath"/>:
    /// typically <c>&lt;syncFolder&gt;\History\</c> when sync is configured, else <c>&lt;LocalState&gt;\History\</c>.
    /// </summary>
    public static string Save(string historyFolderPath, RebalanceSession session)
    {
        Directory.CreateDirectory(historyFolderPath);
        var fileName = BuildFileName(session.Meta);
        var fullPath = Path.Combine(historyFolderPath, fileName);
        using var stream = File.Create(fullPath);
        JsonSerializer.Serialize(stream, session, JsonOptions);
        return fullPath;
    }

    /// <summary>
    /// Returns metadata-only listings (no markdown, no account body) for every readable session
    /// file in the candidate folders, deduplicated by absolute path and sorted newest-first.
    /// Each candidate folder is the *parent* folder; this method appends the History subfolder.
    /// Folders that don't exist or contain no JSON files are silently skipped.
    /// </summary>
    public static List<RebalanceSessionEntry> List(IEnumerable<string> candidateRoots)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entries = new List<RebalanceSessionEntry>();

        foreach (var root in candidateRoots.Where(r => !string.IsNullOrWhiteSpace(r)))
        {
            var folder = Path.Combine(root, HistoryFolderName);
            if (!Directory.Exists(folder)) continue;

            foreach (var path in Directory.EnumerateFiles(folder, "*.json"))
            {
                var canonical = Path.GetFullPath(path);
                if (!seen.Add(canonical)) continue;
                if (TryReadMeta(canonical) is { } meta)
                    entries.Add(new RebalanceSessionEntry(canonical, meta));
            }
        }

        entries.Sort((a, b) => b.Meta.Timestamp.CompareTo(a.Meta.Timestamp));
        return entries;
    }

    /// <summary>Loads the full session document at <paramref name="path"/>. Returns null on parse failure.</summary>
    public static RebalanceSession? Load(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<RebalanceSession>(stream, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public static bool Delete(string path)
    {
        try
        {
            if (!File.Exists(path)) return false;
            File.Delete(path);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static RebalanceSessionMeta? TryReadMeta(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            // Materializing only the meta would require a custom converter; the file is small
            // (~10-100 KB) so a full deserialize is fine and keeps the code simpler.
            var session = JsonSerializer.Deserialize<RebalanceSession>(stream, JsonOptions);
            return session?.Meta;
        }
        catch
        {
            return null;
        }
    }

    private static string BuildFileName(RebalanceSessionMeta meta)
    {
        var ts = meta.Timestamp.ToLocalTime().ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture);
        var safeProvider = UnsafeFileNameChars.Replace(meta.ProviderName, "_");
        return $"{ts}_{safeProvider}.json";
    }
}

/// <summary>Pair of file path + metadata, used to drive the history list UI.</summary>
public sealed record RebalanceSessionEntry(string FilePath, RebalanceSessionMeta Meta)
{
    /// <summary>Human-readable one-line label for combo/list rendering.</summary>
    public string DisplayLabel
        => $"{Meta.Timestamp.ToLocalTime():yyyy-MM-dd HH:mm}  ·  {Meta.ProviderName} ({Meta.ModelId})";
}
