using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The review of the gods (docs/review-gods.md, Tools/gods_impact_audit.py): the bugs found, each kept fixed, the answer given
/// the effect its words already said, and the three signs added. A god's happening never moves one drafted, in a break, down
/// or on the player's order; the colony's own words name a god only by the name the colony knows it by; the one who tends the
/// wounded beast learns doctoring by it; and Selene, Murmur and Helios each have one more sign of their own, shown on the map,
/// said aloud, kept in the book and followed by an effect of its own.
/// </summary>
public static class ReviewGodsTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "review of the gods: " + what);
        }

        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        if (root == null)
        {
            C(false, "found the mod's folder");
            return n;
        }

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));
        string Between(string text, string from, string to)
        {
            int a = text.IndexOf(from, StringComparison.Ordinal);
            if (a < 0) return "";
            int b = text.IndexOf(to, a + from.Length, StringComparison.Ordinal);
            return b < 0 ? text.Substring(a) : text.Substring(a, b - a);
        }

        // ================= Bugs =================

        // ---- A god's happening never takes someone out of the player's hands ----
        // The wounded thrumbo's tender, the feast's table and every scene's later steps sent their people on with a forced
        // job, so one drafted since (or in a break, or on the player's own order) was walked off by it.
        var own = Code("Gods/OnMap/OwnHappenings.cs");
        C(own.Contains("public static bool Held(Pawn p) => p != null && (p.Drafted || p.InMentalState || p.Downed || (p.CurJob?.playerForced ?? false));"),
            "one drafted, in a break, down or on the player's order is held from the gods' happenings");
        var job = Between(own, "public static bool Job(Pawn p, Job j)", "public static bool GoTo(");
        C(job.Contains("if (p == null || j == null || !p.Spawned || Held(p)) return false;"),
            "the one door every happening's walk, wait and carry goes through refuses one held");
        C(Between(own, "public static bool GoTo(", "public static bool Stand(").Contains("return Job(p, j);")
          && Between(own, "public static bool Stand(", "public static bool Carry(").Contains("return Job(p, w);")
          && Between(own, "public static bool Carry(", "public static Pawn Witness(").Contains("return Job(p, job);"),
            "walking, standing and carrying all go through it");
        var feasts = Code("Gods/OnMap/OwnFeasts.cs");
        C(Between(feasts, "private static bool TakeThen(", "catch (Exception e)").Contains("OwnHappenings.Held(p)"),
            "the feast's carrying from the stores refuses one held, as the rest");
        var tend = Between(Code("Gods/OnMap/OwnBellwether.cs"), "private static void TendStep(", "private static void Peace(");
        C(tend.Contains("OwnHappenings.GoTo(p,") && tend.Contains("OwnHappenings.Stand(p,"), "the thrumbo's tender is sent through the same door");

        // ---- The colony's own words name a god by the name the colony knows it by ----
        // A council question's {GOD} was the god's own name while {GOD2} and {GOD3} were the colony's: "Honour Hollin this year"
        // to a colony that knows her only as the Green Lady. The festival on the calendar, a colonist's word about a god, the
        // myth at supper and the colony's slighting letter were the same.
        var worship = Code("Gods/Worship/Worship.cs");
        C(worship.Contains("public static NamedArgument[] ArgsCalled(God g, Pawn speaker = null)") && worship.Contains("Called(g, speaker).Named(GodsMath.God)"),
            "the colony's own placeholders for a god: its name as the colony's book keeps it, or the speaker's");
        var ctx = Between(Code("Gods/Worship/WorshipCouncil.cs"), "protected override void AddArgs(", "public override void ExposeData()");
        C(ctx.Contains("Worship.ArgsCalled(g)") && !ctx.Contains("Gods.Args(g)") && ctx.Contains("Worship.Called(God2)") && ctx.Contains("Worship.Called(God3)"),
            "a council question about the gods names all three by the colony's names");
        C(Between(Code("Gods/Worship/Festivals.cs"), "public static string Name(God g)", "/// <summary>").Contains("Formatted(Worship.ArgsCalled(g))"),
            "a festival on the colony's calendar is named by the colony's name for its god");
        var say = Between(Code("Gods/World/GodWorld.cs"), "public static bool Say(Pawn p, string moment, God g", "public const string Pool");
        C(say.Contains("Worship.ArgsCalled(g, p)") && !say.Contains("Gods.Args(g)"), "a colonist's word about a god uses their name for it");
        var myth = Code("Gods/World/GodDrama.cs");
        C(myth.Contains("Worship.ArgsCalled(god, teller)") && myth.Contains("Worship.Called(other, teller)") && !myth.Contains("(other?.name ?? \"\").Named(\"OTHER\")"),
            "the myth told at supper names both gods as the teller knows them");
        var letters = Code("Gods/World/GodLetters.cs");
        C(!letters.Contains("theirs.name.Named(") && !letters.Contains("mine?.name ?? \"\"") && letters.Contains("Worship.Called(theirs, writer)")
          && letters.Contains("\"CC_GW_Slight_Taken\".Translate(f.Name.Named(\"PEOPLE\"), Worship.Called(g).Named(\"GOD\")"),
            "the colony's slighting letter, and their answer to it, name the gods as the colony knows them");

        // ================= Answers given real effects =================

        // The wounded thrumbo: "{A} walks out to the {BEAST} to tend its wounds", the game's own doctoring, which teaches.
        C(OwnBellwetherLesson(root) == 200f && tend.Contains("p.skills?.Learn(SkillDefOf.Medicine, TendLesson);")
          && tend.IndexOf("h.Tended(", StringComparison.Ordinal) < tend.IndexOf("TendLesson", StringComparison.Ordinal),
            "the one who tends the wounded beast learns doctoring by it, once its wounds are tended");

        // ================= The three signs added =================

        var kinds = Enum.GetNames(typeof(OmenKind));
        C(kinds.Length >= 3 && kinds.Skip(kinds.Length - 3).SequenceEqual(new[] { "Moths", "WaterRisen", "SunShaft" }),
            "the three signs go at the end, so a save keeps the others' names and order");
        bool hours = true;
        for (int seed = 0; seed < 40; seed++)
        {
            int m = GodsOnMapMath.OmenHour(OmenKind.Moths, seed), w = GodsOnMapMath.OmenHour(OmenKind.WaterRisen, seed), s = GodsOnMapMath.OmenHour(OmenKind.SunShaft, seed);
            hours &= m >= 21 && m <= 23 && w >= 6 && w <= 7 && s >= 9 && s <= 16;
        }
        C(hours, "the moths come at night, the water is found at dawn, the shaft of sun by day");
        var review = Code("Gods/OnMap/GodOmensReview.cs");
        var ownOmens = Code("Gods/OnMap/GodOmensOwn.cs");
        foreach (var k in new[] { "Moths", "WaterRisen", "SunShaft" }) C(review.Contains("case OmenKind." + k + ":"), $"{k} shows on the map");
        C(ownOmens.Contains("default: return CanGodR6(map, k);") && ownOmens.Contains("return ShowGodR6(map, kind, out at, out speaker);"),
            "the gods' own signs reach them");
        var show = Between(review, "private static bool ShowGodR6(", "\n    }\n}");
        C(Between(show, "case OmenKind.Moths:", "case OmenKind.WaterRisen:").Contains("if (!Night(map)) return false;")
          && Between(show, "case OmenKind.WaterRisen:", "case OmenKind.SunShaft:").Contains("rainRate > 0f) return false;")
          && Between(show, "case OmenKind.SunShaft:", "return true;").Contains("w == WeatherDefOf.Clear) return false;"),
            "each waits for what its words need: the dark, a night with no rain, a gray sky");
        C(Between(review, "private static bool CanGodR6(", "private static bool ShowGodR6(").Contains("case OmenKind.Moths: return Lights(map).Count > 0;")
          && Between(review, "private static bool CanGodR6(", "private static bool ShowGodR6(").Contains("case OmenKind.WaterRisen: return Wet(map).IsValid;"),
            "the moths need a light of the colony's, the water wet ground or a well");

        var shapes = XDocument.Load(Path.Combine(root, "Defs", "GodDefs", "Rimfolk_GodsOnMap_Shapes.xml")).Root!.Elements("ColonyCouncil.GodShapeDef")
            .ToDictionary(e => (string)e.Element("defName"), e => e.Element("omens")?.Elements("li").Select(l => l.Value).ToList() ?? new List<string>());
        C(shapes["Rimfolk_GodShape_Selene"].Contains("Moths") && shapes["Rimfolk_GodShape_Murmur"].Contains("WaterRisen") && shapes["Rimfolk_GodShape_Helios"].Contains("SunShaft"),
            "each is its god's own: Selene's moths, Murmur's water, Helios's sun");
        C(shapes.Where(kv => kv.Key != "Rimfolk_GodShape_Selene" && kv.Key != "Rimfolk_GodShape_Murmur" && kv.Key != "Rimfolk_GodShape_Helios")
                .All(kv => !kv.Value.Intersect(new[] { "Moths", "WaterRisen", "SunShaft" }).Any()),
            "and no other god's");

        var keyed = new Dictionary<string, string>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml"))
            foreach (var e in XDocument.Load(file).Root!.Elements())
                keyed[e.Name.LocalName] = e.Value;
        var godNames = new[] { "Hollin", "Smoulder", "Bellwether", "Morel", "Ennu", "Pip", "Cirrus", "Hush", "Undertow", "Helios", "Selene", "Tamiu", "Mog", "Murmur", "Ducat", "Revel" };
        foreach (var k in new[] { "Moths", "WaterRisen", "SunShaft" })
        {
            C(keyed.TryGetValue("CC_GodM_Saw_" + k, out var w) && w.StartsWith("{WHO} ") && w.Contains("{HOME}") && !w.Contains("{GOD") && !godNames.Any(g => w.Contains(g)),
                $"the book keeps {k} in the words of who saw it, naming no god");
            var impact = GodImpactMath.Of("CC_GodM_Saw_" + k);
            C(GodImpactMath.Has("CC_GodM_Saw_" + k) && impact.effect != TouchEffect.None && !impact.dark, $"{k} leaves an effect of its own, a kind one");
        }
        C(GodImpactMath.Of("CC_GodM_Saw_Moths").effect == TouchEffect.Inspire && GodImpactMath.Of("CC_GodM_Saw_WaterRisen").effect == TouchEffect.Bloom
          && GodImpactMath.Of("CC_GodM_Saw_SunShaft").effect == TouchEffect.LongNoon,
            "the moths' watcher inspired, flowers where the water stood, the crops in the open a day ahead");
        C(GodImpactMath.NeedsWitness(GodImpactMath.Of("CC_GodM_Saw_Moths").effect) && !GodImpactMath.NeedsWitness(TouchEffect.Bloom) && !GodImpactMath.NeedsWitness(TouchEffect.LongNoon),
            "the inspiration goes to who watched the moths; the flowers and the crops are there to be found");

        var ext = XDocument.Load(Path.Combine(root, "Defs", "GodDefs", "Rimfolk_GodR6_Review.xml")).Root!.Elements("ColonyCouncil.FlavorExtensionDef").ToList();
        C(ext.Count == 1 && (string)ext[0].Element("pool") == "Rimfolk_Pool_GodOmenRemark", "what is said of them joins the signs' own pool");
        var lines = ext.SelectMany(e => e.Descendants("li")).Select(l => l.Value).ToList();
        foreach (var k in new[] { "moths", "waterrisen", "sunshaft" })
        {
            var mine = lines.Where(l => l.StartsWith("[" + k + "] ")).Select(l => l.Substring(k.Length + 3)).ToList();
            C(mine.Count >= 2, $"someone says what they saw of {k}");
            C(mine.All(l => !Regex.IsMatch(l.TrimEnd('.', '?', '!'), @"[.!?]\s") && !l.Contains("{GOD") && !godNames.Any(g => l.Contains(g))),
                $"{k}: one sentence each, naming no god");
            C(mine.All(l => !l.Contains('—') && !l.Contains('!') && !Regex.IsMatch(l, @"\b(colour|grey|traveller|armour|honour)\b")),
                $"{k}: no dashes or exclamations, American spelling");
        }

        // ================= The review's own measure =================

        C(File.Exists(Path.Combine(root, "Tools", "gods_impact_audit.py")), "the review's measure can be run again");
        string doc = File.ReadAllText(Path.Combine(root, "docs", "review-gods.md"));
        C(doc.Contains("<!-- gods_impact_audit: begin -->") && doc.Contains("<!-- gods_impact_audit: end -->"), "the review keeps the measure's part between its markers");
        return n;
    }

    /// <summary>OwnBellwether.TendLesson, read from its source (the type needs the game).</summary>
    private static float OwnBellwetherLesson(string root)
    {
        var text = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "Gods", "OnMap", "OwnBellwether.cs"));
        var m = Regex.Match(text, @"public const float TendLesson = ([0-9.]+)f;");
        return m.Success ? float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : -1f;
    }
}
