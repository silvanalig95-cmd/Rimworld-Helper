using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// Saving and loading: a collection loaded through a local variable and only read back once loading is done is lost,
/// because the game calls ExposeData again for that later pass and the local is made afresh, empty (how every colonist's
/// character was lost at each load until 0.104.3, Persona/PersonaTracker.cs). The loaded value must go through a field.
/// </summary>
public static class ScribeTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        var local = new Regex(@"^\s*(?:var|List<[^>]+>|Dictionary<[^>]+>|HashSet<[^>]+>)\s+(\w+)\s*=", RegexOptions.Compiled);
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Source", "ColonyCouncil"), "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)) continue;
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                var m = local.Match(lines[i]);
                if (!m.Success) continue;
                string name = m.Groups[1].Value;
                // Scribed by reference within the next few lines...
                int look = -1;
                for (int j = i + 1; j < Math.Min(lines.Length, i + 6); j++)
                    if (lines[j].Contains("Scribe_Collections.Look(ref " + name + ",") || lines[j].Contains("Scribe_Deep.Look(ref " + name + ","))
                    {
                        look = j;
                        break;
                    }
                if (look < 0) continue;
                // ...and read only in a later pass of loading.
                bool late = false;
                for (int j = look + 1; j < Math.Min(lines.Length, look + 20); j++)
                {
                    if (lines[j].Contains("LoadSaveMode.PostLoadInit") || lines[j].Contains("LoadSaveMode.ResolvingCrossRefs"))
                    {
                        for (int k = j + 1; k < Math.Min(lines.Length, j + 12); k++)
                            if (Regex.IsMatch(lines[k], @"\b" + Regex.Escape(name) + @"\b")) late = true;
                        break;
                    }
                }
                n++;
                check(!late, $"saving: {Path.GetFileName(file)}:{i + 1} loads '{name}' into a local and reads it after loading (it comes back empty)");
            }
        }
        n++;
        check(File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "Persona", "PersonaTracker.cs")).Contains("Scribe_Collections.Look(ref peopleSaved,"), "saving: each colonist's character is loaded through a field");
        return n;
    }
}
