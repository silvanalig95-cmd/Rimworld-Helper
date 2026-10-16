using System.Globalization;
using System.Text.RegularExpressions;

namespace Balance
{
    /// <summary>
    /// Constants read straight from the mod's C# source, so the simulator follows the code when it changes. Each has a
    /// fallback (the value when this was written); the report lists every one and says which were found, so a renamed
    /// or rewritten line shows up instead of silently using an old number.
    /// </summary>
    public sealed class SourceConstants
    {
        private readonly string root;
        private readonly Dictionary<string, string> files = new Dictionary<string, string>();
        public readonly List<(string name, string file, float value, bool found)> read = new List<(string, string, float, bool)>();

        public SourceConstants(string root)
        {
            this.root = root;
        }

        private string Text(string rel)
        {
            if (!files.TryGetValue(rel, out var t))
            {
                string path = Path.Combine(root, "Source", "ColonyCouncil", rel);
                t = File.Exists(path) ? File.ReadAllText(path) : "";
                files[rel] = t;
            }
            return t;
        }

        /// <summary>The first number the pattern's group 1 catches in the file, or the fallback.</summary>
        public float Num(string name, string rel, string pattern, float fallback)
        {
            var m = Regex.Match(Text(rel), pattern);
            bool ok = m.Success && float.TryParse(m.Groups[1].Value.TrimEnd('f', 'F'), NumberStyles.Float, CultureInfo.InvariantCulture, out float v);
            float value = ok ? float.Parse(m.Groups[1].Value.TrimEnd('f', 'F'), NumberStyles.Float, CultureInfo.InvariantCulture) : fallback;
            read.Add((name, rel, value, ok));
            return value;
        }

        /// <summary>A "{ StrangerKind.X, 0.10f }" table entry.</summary>
        public float Table(string name, string rel, string table, string key, float fallback)
        {
            string text = Text(rel);
            int at = text.IndexOf(table, StringComparison.Ordinal);
            if (at < 0)
            {
                read.Add((name, rel, fallback, false));
                return fallback;
            }
            int end = text.IndexOf("};", at, StringComparison.Ordinal);
            string body = end > at ? text.Substring(at, end - at) : text.Substring(at);
            var m = Regex.Match(body, @"\{\s*StrangerKind\." + key + @"\s*,\s*([\d.]+)f?\s*\}");
            float value = m.Success ? float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : fallback;
            read.Add((name, rel, value, m.Success));
            return value;
        }

        /// <summary>
        /// Every simple field default of CouncilSettings ("public float daysBetweenWishes = 2f;"), by name and as text:
        /// the settings as Rimfolk ships them.
        /// </summary>
        public Dictionary<string, string> SettingDefaults()
        {
            var d = new Dictionary<string, string>();
            foreach (Match m in Regex.Matches(Text("CouncilSettings.cs"), @"public\s+(\w+)\s+(\w+)\s*(=\s*([^;]+))?;"))
            {
                string type = m.Groups[1].Value, field = m.Groups[2].Value;
                string value = m.Groups[4].Success ? m.Groups[4].Value.Trim() : type == "bool" ? "false" : type == "float" || type == "int" ? "0" : null;
                if (value == null) continue;
                if (value.Contains(type + ".")) value = value.Substring(value.LastIndexOf('.') + 1);
                if (Regex.IsMatch(value, @"^-?[\d.]+[fF]$")) value = value.Substring(0, value.Length - 1);
                d[field] = value;
            }
            return d;
        }
    }
}
