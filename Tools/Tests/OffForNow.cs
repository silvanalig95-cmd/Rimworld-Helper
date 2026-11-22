using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// The player's mods turned off for now (docs/player-mods/off-for-now.txt): Rimfolk keeps their content, soft, so the
/// tests that check names against the active rows count these mods' defs as the player's too.
/// </summary>
public static class OffForNow
{
    private static HashSet<string> mods;

    public static HashSet<string> Mods(string root)
    {
        if (mods != null) return mods;
        var path = Path.Combine(root, "docs", "player-mods", "off-for-now.txt");
        mods = File.Exists(path)
            ? File.ReadAllLines(path).Select(l => l.Trim().ToLowerInvariant()).Where(l => l.Length > 0 && !l.StartsWith("#")).ToHashSet()
            : new HashSet<string>();
        return mods;
    }

    /// <summary>A catalogue row's active column, or its mod is one of these.</summary>
    public static bool Counts(string root, string active, string mod) => active == "yes" || Mods(root).Contains((mod ?? "").ToLowerInvariant());
}
