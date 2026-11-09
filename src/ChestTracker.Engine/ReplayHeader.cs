namespace ChestTracker;

/// <summary>
/// Reads just enough of a .replay header to know whether Fortnite is still recording it.
/// Adapted from Fortnite-ChestCounter (MIT).
/// </summary>
public static class ReplayHeader
{
    private const uint FileMagic = 0x1CA2E27F;

    /// <summary>
    /// True when the replay is encrypted and its key is not written yet. Fortnite writes the real key only when the
    /// match ends (all zeros before), so a match in progress cannot be read.
    /// </summary>
    public static bool KeyMissing(string path)
    {
        try
        {
            using var br = Open(path, out var fs);
            if (!SkipToFlags(br, fs)) return true;
            br.ReadUInt32(); // bIsLive
            br.ReadInt64(); // timestamp
            br.ReadUInt32(); // compressed
            if (br.ReadUInt32() == 0) return false; // not encrypted
            var keyLen = br.ReadInt32();
            if (keyLen <= 0 || keyLen > 256) return true;
            return br.ReadBytes(keyLen).All(b => b == 0);
        }
        catch
        {
            return true;
        }
    }

    /// <summary>True while Fortnite is recording the match, false once finished, null when the header is unreadable.</summary>
    public static bool? IsLive(string path)
    {
        try
        {
            using var br = Open(path, out var fs);
            return SkipToFlags(br, fs) ? br.ReadUInt32() != 0 : null;
        }
        catch
        {
            return null;
        }
    }

    private static BinaryReader Open(string path, out FileStream fs)
    {
        fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return new BinaryReader(fs);
    }

    /// <summary>Moves past magic, version, (custom versions), length, network version, changelist and friendly name.</summary>
    private static bool SkipToFlags(BinaryReader br, FileStream fs)
    {
        if (br.ReadUInt32() != FileMagic) return false;
        var fileVersion = br.ReadUInt32();
        if (fileVersion >= 7)
        {
            var count = br.ReadInt32();
            if (count < 0 || count > 64) return false;
            fs.Seek(count * 20L, SeekOrigin.Current);
        }
        fs.Seek(12, SeekOrigin.Current); // LengthInMS, NetworkVersion, Changelist
        var len = br.ReadInt32(); // FriendlyName (FString, negative = UTF-16)
        fs.Seek(len < 0 ? -2L * len : len, SeekOrigin.Current);
        return true;
    }
}
