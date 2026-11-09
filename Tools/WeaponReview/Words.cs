using ColonyCouncil;

/// <summary>
/// A weapon in a seasoned player's words: what it is, where it shines and where it doesn't, and what its specials mean in
/// a fight. Made from its rating and its own tags (so the line and the tags never disagree), one sentence, clauses joined
/// by semicolons, no stats.
/// </summary>
public static class Words
{
    public static string Quality(float o) =>
        o >= 8.5f ? "superb" : o >= 7.5f ? "excellent" : o >= 6.5f ? "strong" : o >= 5.5f ? "solid" : o >= 4.5f ? "fair" : o >= 3.5f ? "weak" : "poor";

    public static string Plain(Reviewed w)
    {
        var r = w.Rating;
        var tags = new HashSet<string>(w.Tags.Select(t => t.Id));
        bool T(string id) => tags.Contains(id);
        var parts = new List<string>();
        string label = w.Label.ToLowerInvariant();
        float o = r.Overall;
        if (r.Kind == "ranged")
        {
            var g = w.Ranged;
            bool bow = label.Contains("bow");
            string noun = r.Role switch
            {
                "close" => bow ? "short-range bow" : label.Contains("shotgun") ? "close-quarters shotgun" : label.Contains("pistol") || label.Contains("revolver") ? "close-quarters pistol" : "close-quarters gun",
                "long" => bow ? "long bow for picking targets" : "long gun for picking targets",
                "area" => w.Table == "grenades" ? "throw for groups" : "launcher for groups",
                "one-use" => "one-shot launcher",
                "anti-mechanoid" => "tool against mechanoids",
                "utility" => "tool rather than a weapon",
                _ => bow ? "all-round bow" : "all-rounder"
            };
            if (r.Role == "utility")
            {
                parts.Add("A " + noun);
                parts.Add(label.Contains("smoke") ? "its smoke blinds shooters and covers an advance" : "what it does lies in its own effect");
            }
            else parts.Add("A " + Quality(o) + " " + noun);
            float close = r.Sub["close"], med = r.Sub["medium"], lng = r.Sub["long"], far = r.Sub["far"];
            bool emp = T("emp");
            if (r.Role != "utility" && r.Role != "anti-mechanoid")
            {
                if (g.MinRange > 3f) parts.Add("can't fire at anyone closer than " + g.MinRange.ToString("0") + " cells");
                else if (T("short-range")) parts.Add(Math.Max(close, med) >= 6f ? "deadly inside its " + g.Range.ToString("0") + " cells" : "reaches only " + g.Range.ToString("0") + " cells");
                else if (r.Role == "close" && Math.Max(close, med) >= 6f) parts.Add("deadly within ten cells, fades past twenty");
                else if (r.Role != "close" && (lng >= close + 1.2f || far >= close + 1.2f) && Math.Max(lng, far) >= 4.5f) parts.Add("clumsy up close, comes into its own past twenty cells");
                else if (r.Role != "close" && g.Range >= 25f && Math.Abs(close - lng) < 1f && o >= 4.5f && !g.OneUse) parts.Add("steady at every range it reaches");
                if (T("long-reach")) parts.Add("outranges a raider's rifle");
            }
            if (emp) parts.Add(g.HarmsHealth ? "its EMP stuns mechanoids and breaks shields" : "the answer to mechanoids and shield belts, harmless to flesh");
            else if (T("sets-fire") && T("mechs-shrug")) parts.Add("sets people alight but mechanoids shrug it off");
            else if (T("sets-fire")) parts.Add("sets people alight");
            if (T("shreds-armour") && g.HarmsHealth) parts.Add("keeps its bite against marine armour");
            else if (T("bounces-off") && g.HarmsHealth) parts.Add("shrugged off by heavy armour");
            if (T("mech-killer") && !emp) parts.Add("good against mechanoids");
            if (T("crowd-breaker")) parts.Add(g.ExplosionRadius >= 1f ? "breaks up a bunched group" : g.Beam || g.Line ? "cuts through a line of them" : "sprays a crowd");
            if (T("hits-friends")) parts.Add(T("ruins-hunt") ? "keep it away from your own line and out of hunts" : "keep it away from your own line");
            if (T("one-use")) parts.Add("spent after one volley");
            if (T("toxic")) parts.Add("poisons the unprotected");
            if (T("stuns") && !emp) parts.Add("stuns what it hits");
            if (T("non-lethal")) parts.Add("brings them down alive");
            if (T("wild-spray") && !T("crowd-breaker")) parts.Add("sprays wild");
            if (T("heavy-carry")) parts.Add(w.Passives.Any(p => p.Stat == "MoveSpeed" && p.Offset < 0f) ? "slows whoever carries it" : "heavy to carry");
            if (T("good-shot")) parts.Add("wants your best shooter");
            if (T("slow-aim") && r.Role != "long" && r.Role != "one-use") parts.Add("too slow to stop a charge");
            if (T("self-harm")) parts.Add("can blast its own shooter when it overheats");
            if (T("role-sidearm") && o >= 4f && (r.Role == "close" || w.Mass <= 2f)) parts.Add("a fine sidearm");
        }
        else
        {
            bool blunt = T("breaks-bones") || r.Tags.Contains("blunt");
            bool light = r.Role == "light melee";
            string noun = (light ? "light " : "heavy ") + (blunt ? "blunt weapon" : "blade");
            if (T("quick-strikes")) noun = "quick " + noun;
            parts.Add("A " + Quality(o) + " " + noun);
            bool emp = T("emp");
            if (T("shreds-armour")) parts.Add(blunt ? "smashes through armour" : "cuts through marine armour");
            else if (T("bounces-off")) parts.Add("glances off heavy armour");
            if (emp) parts.Add("its EMP stuns mechanoids");
            else if (T("mech-killer")) parts.Add("good against mechanoids");
            if (T("sets-fire")) parts.Add("sets people alight");
            if (T("toxic")) parts.Add("poisons as it cuts");
            if (T("stuns") && !emp) parts.Add("stuns what it hits");
            if (T("non-lethal")) parts.Add("brings them down alive");
            else if (blunt && o >= 3f) parts.Add("takes prisoners more often than a blade");
            if (T("good-defence")) parts.Add("turns aside blows or shots aimed at its wielder");
            if (T("role-sidearm") && o >= 3.5f) parts.Add("a handy sidearm beside a gun");
            if (T("heavy-carry")) parts.Add(w.Passives.Any(p => p.Stat == "MoveSpeed" && p.Offset < 0f) ? "slows whoever carries it" : "heavy to carry");
        }
        var counted = w.Abilities.FirstOrDefault(a => a.Counted);
        if (counted != null) parts.Add("its " + counted.Label + " adds a punch when it counts");
        if (T("restricted")) parts.Add("not for everyone to carry");
        if (T("hard-to-get")) parts.Add("hard to find in your game");
        if (w.Unsure.Count > 0 && w.Unsure.Any(u => !u.StartsWith("a unique weapon"))) parts.Add("read roughly (its own rules)");
        else if (T("unique")) parts.Add("its rolled traits can lift or sink it");
        parts[0] = Article(parts[0]);
        var picked = new List<string> { parts[0] };
        foreach (var p in parts.Skip(1))
        {
            if (picked.Count >= 5) break;
            picked.Add(p);
        }
        return string.Join("; ", picked) + ".";
    }

    /// <summary>"A" before a consonant, "An" before a vowel.</summary>
    static string Article(string s) =>
        s.StartsWith("A ") && s.Length > 2 && "aeiou".IndexOf(char.ToLowerInvariant(s[2])) >= 0 ? "An " + s.Substring(2) : s;

    /// <summary>The distance sub-ratings as a short line.</summary>
    public static string Subs(WeaponRating r)
    {
        string F(string k) => r.Sub.TryGetValue(k, out var v) ? v.ToString("0.0") : "";
        if (r.Kind == "ranged")
            return $"{F("close")} / {F("medium")} / {F("long")} / {F("far")}";
        return "";
    }

    /// <summary>The sub-ratings against each enemy as a short line.</summary>
    public static string Armour(WeaponRating r)
    {
        string F(string k) => r.Sub.TryGetValue(k, out var v) ? v.ToString("0.0") : "";
        return $"{F("unarmoured")} / {F("armoured")} / {F("heavy")} / {F("mechanoids")}";
    }

    public static string SpecialsLine(WeaponRating r)
    {
        var shown = r.Specials.Where(s => s.Key != "bleeding" && s.Key != "blunt" && s.Key != "biocodable").Select(s => s.Label).ToList();
        return string.Join("; ", shown);
    }

    public static string Tech(string t) => string.IsNullOrEmpty(t) ? "" : t switch
    {
        "Neolithic" => "neolithic", "Medieval" => "medieval", "Industrial" => "industrial", "Spacer" => "spacer",
        "Ultra" => "ultra", "Archotech" => "archotech", "Animal" => "animal", _ => t.ToLowerInvariant()
    };

    /// <summary>Text safe in a Markdown table cell.</summary>
    public static string Cell(string s) => (s ?? "").Replace("|", "/").Replace("\n", " ");
}
