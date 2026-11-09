namespace ChestTracker;

/// <summary>What one scan did.</summary>
public sealed record ScanResult(List<MatchRecord> Added, int Skipped, List<string> Errors)
{
    public static readonly ScanResult Empty = new(new(), 0, new());
}

/// <summary>
/// Finds finished replays in the Demos folder, analyzes each one once and keeps the history.
/// Timing rules adapted from Fortnite-ChestCounter (MIT).
/// </summary>
public sealed class Tracker
{
    /// <summary>Unknown header state: the file must be quiet for this long before it is read.</summary>
    private static readonly TimeSpan QuietPeriod = TimeSpan.FromSeconds(10);

    /// <summary>Finalized by Fortnite (not live, real key written): a few quiet seconds are enough.</summary>
    private static readonly TimeSpan FinalizedQuietPeriod = TimeSpan.FromSeconds(4);

    /// <summary>A finished replay untouched for this long is complete for sure (left from earlier).</summary>
    private static readonly TimeSpan OldReplayAge = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Fortnite appends to a live replay about every 25 s. Still live but untouched for this long: the recording was
    /// cut (Fortnite crashed or was closed) and will never be finished.
    /// </summary>
    private static readonly TimeSpan AbandonedLiveAge = TimeSpan.FromMinutes(10);

    private static readonly TimeSpan ParseTimeout = TimeSpan.FromMinutes(3);

    private readonly Store _store;
    private readonly Settings _settings;
    private readonly Dictionary<string, long> _lastSeenSize = new();
    private readonly object _lock = new();

    public TrackerData Data { get; }

    public Tracker(Store store, Settings settings, TrackerData data)
    {
        _store = store;
        _settings = settings;
        Data = data;
        RequeueOutdated();
    }

    /// <summary>True while a replay is being recorded (a match is in progress).</summary>
    public bool MatchInProgress { get; private set; }

    /// <summary>True right after a match ended, while its replay waits to be read: scan again within seconds.</summary>
    public bool Finishing { get; private set; }

    /// <summary>Totals, safe to call from the UI thread while a scan runs.</summary>
    public Totals GetTotals(DateTime sessionStartUtc)
    {
        lock (_lock)
        {
            return Totals.From(Data, _settings, sessionStartUtc);
        }
    }

    /// <summary>Copy of the history, safe to read while a scan runs.</summary>
    public List<MatchRecord> MatchesSnapshot()
    {
        lock (_lock)
        {
            return Data.Matches.ToList();
        }
    }

    public static string FileKey(FileInfo f) => $"{f.Name}|{f.Length}|{f.LastWriteTimeUtc.Ticks}";

    public IEnumerable<FileInfo> ReplayFiles()
    {
        var dir = new DirectoryInfo(_settings.DemosFolder);
        return dir.Exists
            ? dir.EnumerateFiles("*.replay", SearchOption.TopDirectoryOnly).OrderBy(f => f.LastWriteTimeUtc)
            : Enumerable.Empty<FileInfo>();
    }

    public List<FileInfo> UnprocessedFiles()
    {
        lock (_lock)
        {
            return ReplayFiles().Where(f => !Data.ProcessedFiles.ContainsKey(FileKey(f))).ToList();
        }
    }

    /// <summary>
    /// When the analyzer improves, matches whose replay is still on disk are read again (their record is replaced,
    /// never duplicated). Matches whose replay is gone keep their old figures.
    /// </summary>
    private void RequeueOutdated()
    {
        var outdated = Data.Matches.Where(m => m.AnalyzerVersion < ReplayAnalyzer.CurrentVersion).Select(m => m.SessionId).ToHashSet();
        foreach (var key in Data.ProcessedFiles.Where(kv => outdated.Contains(kv.Value) || kv.Value.StartsWith("error:")).Select(kv => kv.Key).ToList())
        {
            Data.ProcessedFiles.Remove(key);
        }
    }

    /// <summary>One pass over the folder: reads every replay that is ready.</summary>
    /// <param name="ignoreQuietPeriod">Read finished replays immediately (scan tool, first launch).</param>
    /// <param name="progress">(done, total, file name), called before each replay.</param>
    public ScanResult Scan(bool ignoreQuietPeriod = false, Action<int, int, string>? progress = null)
    {
        Finishing = false;
        var pending = UnprocessedFiles();
        MatchInProgress = pending.Any(f => ReplayHeader.IsLive(f.FullName) == true && DateTime.UtcNow - f.LastWriteTimeUtc < TimeSpan.FromMinutes(3));

        var ready = pending.Where(f => IsReady(f, ignoreQuietPeriod)).ToList();
        if (ready.Count == 0)
        {
            return ScanResult.Empty;
        }

        var result = new ScanResult(new(), 0, new());
        var skipped = 0;
        for (var i = 0; i < ready.Count; i++)
        {
            progress?.Invoke(i, ready.Count, ready[i].Name);
            switch (Process(ready[i]))
            {
                case { } match when match.Error == null && match.Record != null:
                    result.Added.Add(match.Record);
                    break;
                case { Error: { } error }:
                    result.Errors.Add(error);
                    break;
                default:
                    skipped++;
                    break;
            }
        }
        progress?.Invoke(ready.Count, ready.Count, "");
        lock (_lock)
        {
            _store.SaveData(Data);
        }
        return result with { Skipped = skipped };
    }

    private bool IsReady(FileInfo file, bool ignoreQuietPeriod)
    {
        file.Refresh();
        if (!file.Exists)
        {
            return false;
        }
        var live = ReplayHeader.IsLive(file.FullName);
        var quietFor = DateTime.UtcNow - file.LastWriteTimeUtc;
        if (live == true)
        {
            // Still being recorded, unless the recording was cut long ago (Process marks it incomplete).
            return quietFor > AbandonedLiveAge;
        }
        if (live == false && (ignoreQuietPeriod || quietFor > OldReplayAge))
        {
            return true;
        }

        // The size must be the same on two scans in a row and the file quiet for a while.
        var stable = _lastSeenSize.TryGetValue(file.FullName, out var size) && size == file.Length;
        _lastSeenSize[file.FullName] = file.Length;
        if (live == null)
        {
            return stable && quietFor > TimeSpan.FromMinutes(2);
        }
        // Fortnite cleared the live flag and wrote the real key: the match just ended.
        if (!ReplayHeader.KeyMissing(file.FullName))
        {
            Finishing = true;
            return stable && quietFor >= FinalizedQuietPeriod;
        }
        return stable && quietFor > QuietPeriod;
    }

    private sealed record Outcome(MatchRecord? Record, string? Error = null);

    private Outcome? Process(FileInfo file)
    {
        var key = FileKey(file);

        // Recording cut mid-match: Fortnite never wrote the key, nothing can be read.
        if (ReplayHeader.IsLive(file.FullName) == true && ReplayHeader.KeyMissing(file.FullName))
        {
            Mark(key, "incomplete");
            return null;
        }

        MatchRecord record;
        try
        {
            record = Analyze(file.FullName, ParseTimeout);
        }
        catch (Exception ex)
        {
            var message = (ex as AggregateException)?.InnerException?.Message ?? ex.Message;
            _store.Log($"Unreadable replay: {file.FullName} ({file.Length} bytes)", ex);
            Mark(key, "error: " + message);
            return new Outcome(null, $"{file.Name}: {message}");
        }

        record.ReplayFile = file.Name;
        if (!record.IsBattleRoyale)
        {
            Mark(key, "skipped");
            return null;
        }

        lock (_lock)
        {
            // Same match analyzed again (newer analyzer, or the same replay renamed by Fortnite): replace it.
            Data.Matches.RemoveAll(m => m.SessionId == record.SessionId);
            Data.Matches.Add(record);
            Data.Matches.Sort((a, b) => a.When.CompareTo(b.When));
            Data.ProcessedFiles[key] = record.SessionId;
        }
        return new Outcome(record);
    }

    private void Mark(string key, string value)
    {
        lock (_lock)
        {
            Data.ProcessedFiles[key] = value;
        }
    }

    /// <summary>Reads the file (even while Fortnite holds it) and parses it on a low-priority thread with a timeout.</summary>
    public static MatchRecord Analyze(string path, TimeSpan timeout)
    {
        byte[] bytes;
        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        {
            bytes = new byte[fs.Length];
            fs.ReadExactly(bytes);
        }

        MatchRecord? result = null;
        Exception? failure = null;
        var worker = new Thread(() =>
        {
            try
            {
                using var ms = new MemoryStream(bytes, writable: false);
                // Replays saved on a PC are its player's: the replay owner is tracked.
                result = new ReplayAnalyzer().Analyze(ms, Path.GetFileName(path), "");
            }
            catch (Exception e)
            {
                failure = e;
            }
        })
        {
            IsBackground = true,
            Priority = ThreadPriority.BelowNormal,
            Name = "ReplayAnalysis",
        };
        worker.Start();
        if (!worker.Join(timeout))
        {
            throw new TimeoutException("analysis took too long");
        }
        if (failure != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }

        // Analysis allocates a lot: give the memory back, the app idles in the tray.
        System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        return result!;
    }
}
