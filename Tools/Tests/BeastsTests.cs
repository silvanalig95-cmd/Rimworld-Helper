using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The wild's alphas and bosses (Beasts/BeastsMath.cs; DESIGN.md "Alphas, nemeses, legends and great herds"): the
/// settings' rates and waits; which animals lead a pack, which run as a legend or cross as a great herd, by what they are;
/// the alpha picked; when a hurt beast flees and when it comes back, who swears against it; what spooks a herd and what a
/// stampede can do; and the mod's side: the defs and words each part names, the settings with their tooltips, the downed
/// rule, the land's animals read from the biomes' records, the clock's slot, the dev actions.
/// </summary>
public static class BeastsTests
{
    private const int Day = BeastsMath.DayTicks;

    public static int Run(Action<bool, string> check)
    {
        string root = Root();
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "beasts: " + what);
        }

        // ================= The settings =================

        C(BeastsMath.Rate(1f) == 1f && BeastsMath.Rate(0f) == 0f && BeastsMath.Rate(-1f) == 0f && BeastsMath.Rate(5f) == 2f && BeastsMath.Rate(float.NaN) == 0f,
            "a setting is a rate from 0 to 200%");
        C(Near(BeastsMath.Gap(1f, 30f), 30f) && Near(BeastsMath.Gap(2f, 30f), 15f) && Near(BeastsMath.Gap(0.5f, 30f), 60f) && float.IsPositiveInfinity(BeastsMath.Gap(0f, 30f)),
            "200% halves a wait, 50% doubles it, 0% never");
        C(Near(BeastsMath.Chance(1f, 0.25f), 0.25f) && Near(BeastsMath.Chance(2f, 0.6f), 1f) && BeastsMath.Chance(0f, 0.6f) == 0f, "200% doubles a chance, never past certain");
        C(BeastsMath.Waited(1f, 30f, 100 * Day, -1) && !BeastsMath.Waited(1f, 30f, 100 * Day, 71 * Day) && BeastsMath.Waited(1f, 30f, 100 * Day, 70 * Day)
          && !BeastsMath.Waited(0f, 30f, 100 * Day, -1), "the wait since the last one, by the setting; off never");

        // ================= Pack alphas =================

        C(BeastsMath.PackKind(true, true, false, false, 0.85f), "a wolf's kind runs in packs");
        C(!BeastsMath.PackKind(true, false, false, false, 1.5f) && !BeastsMath.PackKind(true, true, true, false, 1f) && !BeastsMath.PackKind(true, true, false, true, 1f)
          && !BeastsMath.PackKind(true, true, false, false, 0.2f) && !BeastsMath.PackKind(false, true, false, false, 1f), "never a grazer, an insect, a dryad, a tiny one or a person");
        C(BeastsMath.AlphaScore(1.2f, 0.5f, false) > BeastsMath.AlphaScore(0.9f, 0.5f, true), "the biggest leads");
        C(BeastsMath.AlphaScore(1f, 0.5f, false) > BeastsMath.AlphaScore(1f, 0.95f, false) && BeastsMath.AlphaScore(1f, 0.5f, false) > BeastsMath.AlphaScore(1f, 0.05f, false),
            "of the same size, one in its prime before the old and the young");
        C(BeastsMath.PackMin == 3 && BeastsMath.AlphaGapDays >= 30f && BeastsMath.AlphaDayChance <= 0.25f && BeastsMath.ManhunterAlphaChance <= 0.3f,
            "three make a pack; an alpha a month apart at most, a day in four, three manhunter packs in ten");
        float first = BeastsMath.FollowAfterDays(0, 1f), second = BeastsMath.FollowAfterDays(1, 0f);
        C(first <= 1f && second > BeastsMath.FollowAfterDays(0, 0f) && BeastsMath.FollowAfterDays(5, 0f) < 6f && BeastsMath.FollowMax == 6,
            "a tamed alpha's pack comes in one by one over the next days, the first within a day, six at most");

        // ================= Animal nemeses =================

        C(BeastsMath.Flees(0.4f, false, false, false) && !BeastsMath.Flees(0.5f, false, false, false), "a beast nemesis badly hurt (under 45%) turns and runs");
        C(!BeastsMath.Flees(0.3f, true, false, false) && !BeastsMath.Flees(0.3f, false, true, false) && !BeastsMath.Flees(0.3f, false, false, true),
            "never downed, dead or someone's: a downed beast doesn't get away");
        C(Near(BeastsMath.ReturnDays(1f, 0f), 6f) && Near(BeastsMath.ReturnDays(1f, 1f), 16f) && Near(BeastsMath.ReturnDays(2f, 1f), 8f) && float.IsPositiveInfinity(BeastsMath.ReturnDays(0f, 0.5f)),
            "it comes back 6 to 16 days later at 100%, half that at 200%, never at 0");
        C(BeastsMath.MayReturn(1f, 0) && BeastsMath.MayReturn(1f, 1) && !BeastsMath.MayReturn(1f, 2) && !BeastsMath.MayReturn(0f, 0), "twice at most");
        int blood = Enumerable.Range(0, 1000).Count(i => BeastsMath.ForBlood(i / 1000f));
        C(blood == 350, $"one return in three for blood ({blood} in 1000)");
        C(BeastsMath.Grave("killed") && BeastsMath.Grave("maimed") && !BeastsMath.Grave("downed") && !BeastsMath.Grave(null), "scars and an oath for a death or a maiming, not a knock-down");
        C(BeastsMath.Close(true, 0f, -50) && BeastsMath.Close(false, 30f, 0) && BeastsMath.Close(false, 0f, 60) && !BeastsMath.Close(false, 29f, 59),
            "close as a vendetta asks: a partner or kin, a bond of 30, an opinion of 60");
        C(BeastsMath.Closeness(true, 0f, 0) > BeastsMath.Closeness(false, 40f, 50), "a partner or kin swears first");
        C(BeastsMath.OathChance <= 0.6f && BeastsMath.FleeBelow <= 0.45f, "three oaths in five; flight under 45%");

        // ================= Legends =================

        C(BeastsMath.LegendFits(true, false, false, false, 4f, false, false, false), "a thrumbo's size makes a legend");
        C(!BeastsMath.LegendFits(true, false, false, false, 2.4f, false, false, false), "a muffalo is too small");
        C(!BeastsMath.LegendFits(true, true, false, false, 6f, false, false, false) && !BeastsMath.LegendFits(true, false, true, false, 6f, false, false, false)
          && !BeastsMath.LegendFits(true, false, false, true, 6f, false, false, false), "never an insect, a dryad or an entity");
        C(!BeastsMath.LegendFits(true, false, false, false, 9f, true, false, true) && !BeastsMath.LegendFits(true, false, false, false, 1f, true, true, false)
          && BeastsMath.LegendFits(true, false, false, false, 0.5f, true, true, true), "a Pokémon only a legendary one, and only once PokéWorld opens them, whatever its size");
        C(BeastsMath.LegendWeight(6f, false) > BeastsMath.LegendWeight(3f, false) && BeastsMath.LegendPool == 12, "the bigger the likelier, of the dozen biggest");
        C(!BeastsMath.LegendDue(1f, 39 * Day, -1) && BeastsMath.LegendDue(1f, 40 * Day, -1), "none before the world's 40th day");
        C(!BeastsMath.LegendDue(1f, 300 * Day, 151 * Day) && BeastsMath.LegendDue(1f, 300 * Day, 150 * Day) && BeastsMath.LegendDue(2f, 300 * Day, 225 * Day)
          && !BeastsMath.LegendDue(0f, 300 * Day, -1), "150 days between two at 100%, 75 at 200%, none at 0");
        C(BeastsMath.LairMinTiles >= 4 && BeastsMath.LairMaxTiles <= 12 && BeastsMath.MoveMinDays >= 5f && BeastsMath.MoveMaxDays <= 9f, "its ground 4 to 12 tiles off, moving every five to nine days");

        // ================= Great herds =================

        C(BeastsMath.Migrates(0) && BeastsMath.Migrates(2) && !BeastsMath.Migrates(1) && !BeastsMath.Migrates(3), "spring and fall");
        C(Enumerable.Range(0, 100).All(i => BeastsMath.PlannedDay(i / 99f) >= 2 && BeastsMath.PlannedDay(i / 99f) <= 12), "on a day of the season's first twelve");
        C(BeastsMath.HerdKind(true, true, false, false, false, true, 2.4f) && !BeastsMath.HerdKind(true, true, true, false, false, true, 2f) && !BeastsMath.HerdKind(true, true, false, false, false, false, 2f)
          && !BeastsMath.HerdKind(true, false, false, false, false, true, 2f) && !BeastsMath.HerdKind(true, true, false, false, false, true, 0.5f), "herd animals that migrate, never hunters, nothing tiny");
        C(BeastsMath.HerdKindWeight(2f, true) > BeastsMath.HerdKindWeight(2f, false) * 2f, "the land's own likelier than one from beyond");
        C(Enumerable.Range(0, 11).All(i => BeastsMath.GreatCount(1f, i / 10f) >= 6 && BeastsMath.GreatCount(1f, i / 10f) <= 24 && BeastsMath.GreatCount(8f, i / 10f) <= 7),
            "six to twenty-four head, a few of the biggest");
        C(BeastsMath.GreatCount(1f, 1f) > BeastsMath.GreatCount(4f, 1f), "the bigger, the fewer");
        C(Near(BeastsMath.SpookChance(BeastsMath.SpookHurt), 0.6f) && Near(BeastsMath.SpookChance(BeastsMath.SpookShot), 0.25f) && Near(BeastsMath.SpookChance(BeastsMath.SpookFire), 0.5f)
          && BeastsMath.SpookChance("thunder") == 0f, "spooked by one of it hurt, a shot near, a fire close by, nothing else");
        C(BeastsMath.TrampleChance(2.4f, 1f) <= 0.6f && BeastsMath.TrampleChance(1.2f, 3f) >= 0.08f && BeastsMath.TrampleChance(3f, 1f) > BeastsMath.TrampleChance(1.2f, 1f),
            "a runner knocks someone down more often the bigger it is beside them, never more than three times in five");
        C(BeastsMath.TrampleDamage(1f, 0f) >= 6f * 0.75f - 0.01f && BeastsMath.TrampleDamage(20f, 1f) <= 16f * 1.25f + 0.01f, "the blow: 6 to 16 by size, give or take a quarter");
        C(BeastsMath.CanTrample(2.4f, 1f) && !BeastsMath.CanTrample(1f, 0.5f) && !BeastsMath.CanTrample(1.5f, 3f), "only a big runner, and only someone not much bigger than it");
        C(Near(BeastsMath.SegmentDistSq(0, 0, 10, 0, 5, 1), 1f) && Near(BeastsMath.SegmentDistSq(0, 0, 10, 0, 12, 0), 4f) && Near(BeastsMath.SegmentDistSq(3, 3, 3, 3, 4, 3), 1f),
            "how far someone stood from a runner's way");
        C(BeastsMath.MaxTrampled == 3 && BeastsMath.MaxCrops == 12 && BeastsMath.StampedeHours <= 1.5f, "three knocked down and twelve plants at most, in the first hour or so");
        C(BeastsMath.WatchMax == 3 && BeastsMath.MinWatchHerd == 6 && BeastsMath.MinHerd == 4, "up to three stop to watch a herd of six; four head make a herd");

        // ================= The mod's side =================

        if (root == null)
        {
            C(false, "the repository root is found");
            return n;
        }
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));
        var beastCode = string.Join("\n", Directory.GetFiles(Path.Combine(src, "Beasts"), "*.cs").Select(File.ReadAllText));
        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Beasts.xml")).Root!.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);

        // Every word the code names is there.
        foreach (Match m in Regex.Matches(beastCode, "\"(CC_(?:Alpha|BeastNem|Herd|Legend)_[A-Za-z_]+)\""))
        {
            string key = m.Groups[1].Value;
            if (key.EndsWith("_")) continue;
            C(keyed.ContainsKey(key), $"the words {key} are there");
        }
        foreach (var cause in new[] { BeastsMath.SpookHurt, BeastsMath.SpookShot, BeastsMath.SpookFire })
            C(keyed.ContainsKey("CC_Herd_Stampede_" + cause), $"a stampede's words for {cause}");
        C(keyed.Values.All(v => !v.Contains('—')), "no em dashes in the words");

        // The settings: four rates, each with its label, note and tooltip.
        string settings = Code("CouncilSettings.cs"), catalog = Code("UI/SettingsCatalog.cs");
        foreach (var (field, key) in new[] { ("packAlphas", "PackAlphas"), ("beastNemeses", "BeastNemeses"), ("regionLegends", "RegionLegends"), ("greatHerds", "GreatHerds") })
        {
            C(settings.Contains("public float " + field + " = 1f;") && settings.Contains("Scribe_Values.Look(ref " + field + ", \"" + field + "\", 1f);"), $"the setting {field} is saved, 1 by default");
            C(catalog.Contains("Slider(\"" + field + "\", \"CC_Settings_" + key + "\", \"CC_SetNote_" + key + "\", 0f, 2f,"), $"{field} is a slider from 0 to 200%");
            C(keyed.ContainsKey("CC_Settings_" + key) && keyed.ContainsKey("CC_SetNote_" + key) && keyed.ContainsKey("CC_Settings_" + key + "_Tip"), $"{field} has a label, a note and a tooltip");
        }

        // The defs each part names.
        var defs = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).Select(f => XDocument.Load(f).Root!).SelectMany(r => r.Elements()).ToList();
        bool Has(string type, string name) => defs.Any(e => e.Name.LocalName == type && e.Element("defName")?.Value == name);
        foreach (var h in new[] { "Rimfolk_PackAlpha", "Rimfolk_BeastScars", "Rimfolk_BeastOathFury" }) C(Has("HediffDef", h), $"the hediff {h}");
        foreach (var t in new[] { "Rimfolk_Alpha_Slain", "Rimfolk_Alpha_Tamed", "Rimfolk_Beast_OathKept", "Rimfolk_Beast_OathOthers", "Rimfolk_Herd_Watched", "Rimfolk_Legend_Slayer",
                     "Rimfolk_Legend_Fell", "Rimfolk_Legend_Befriended", "Rimfolk_Legend_Awe" })
            C(Has("ThoughtDef", t), $"the thought {t}");
        foreach (var e in new[] { "Rimfolk_Epithet_Packbreaker", "Rimfolk_Epithet_GiantSlayer", "Rimfolk_Epithet_BeastSpeaker" }) C(Has("ColonyCouncil.EpithetDef", e), $"the name {e}");
        C(Has("JobDef", "Rimfolk_WatchHerd") && Has("SitePartDef", "Rimfolk_LegendGround"), "the herd-watching job and the legend's ground");
        C(defs.Any(e => e.Name.LocalName == "GenStepDef" && e.Element("linkWithSite")?.Value == "Rimfolk_LegendGround"), "the legend is put on its ground's map by a step linked to the site");
        var legend = defs.FirstOrDefault(e => e.Name.LocalName == "ColonyCouncil.WorldSituationDef" && e.Element("defName")?.Value == "Rimfolk_WS_Legend");
        C(legend != null && legend.Element("workerClass")?.Value == "ColonyCouncil.LegendWorker", "the legend is a world event with its worker");
        var endings = legend?.Element("endings")?.Elements("li").Select(li => li.Element("key")?.Value).ToList() ?? new List<string>();
        C(new[] { "slain", "befriended", "fell", "gone", "moved" }.All(endings.Contains), "it ends slain, befriended, fallen otherwise, gone, or moved on");
        C(legend?.Descendants("li").Any(li => (string)li.Attribute("Class") == "ColonyCouncil.DilemmaEffect_Bonfire") == true, "a legend's fall is sung around a fire at home");
        C(legend?.Element("beats")?.Elements("li").Any(b => b.Element("key")?.Value == "bounty" && b.Element("requires")?.Value == "town") == true, "a town may put a price on it");
        var pools = defs.Where(e => e.Name.LocalName == "ColonyCouncil.FlavorPoolDef").Select(e => e.Element("defName")?.Value).ToHashSet();
        foreach (var p in new[] { "Rimfolk_Pool_AlphaNames", "Rimfolk_Pool_AlphaSighting", "Rimfolk_Pool_BeastOath", "Rimfolk_Pool_HerdPasses", "Rimfolk_Pool_LegendNames", "Rimfolk_Pool_LegendSong",
                     "Rimfolk_Journal_AlphaSlain", "Rimfolk_Journal_BeastOath", "Rimfolk_Journal_BeastOathEnd", "Rimfolk_Journal_LegendSlain", "Rimfolk_Journal_LegendBefriended" })
            C(pools.Contains(p), $"the pool {p}");

        // The rules the player set.
        string nemeses = Code("Strangers/Nemeses.cs");
        C(nemeses.Contains("if (!x.Downed && !x.InMentalState && !comp.social.BattleOn(x.Map)"), "a beast lying downed is never named as got away");
        C(nemeses.Contains("BeastNemeses.Fled(x)"), "a beast that ran off hurt is told as got away");
        C(Code("Aspirations/Aspirations.cs").Contains("BeastNemeses.Sworn(p)"), "one sworn against a beast may take up settling the score");
        C(!beastCode.Contains(".AllWildAnimals") && !beastCode.Contains("CommonalityOfAnimal") && beastCode.Contains("BiomeAnimals.Of("),
            "the land's animals are read through the safe helper (Animals/BiomeAnimals.cs), never the game's lookup");
        C(beastCode.Contains("Harm.May(p)") && beastCode.Contains("Harm.Chance(HarmKinds.Stampede, ") && beastCode.Contains("Harm.Note(p"), "a stampede's blow is held to the everyday hurts' scale and shared cap");
        C(Regex.Matches(beastCode, "BeastBook\\.Up\\(").Count >= 10, "only a beast up and about is named, flees, comes back or leads");
        C(beastCode.Contains("Speech.Say(") && !beastCode.Contains("MoteMaker.ThrowText("), "words are said aloud through speech, and nothing else floats over a head");
        C(!Regex.IsMatch(beastCode, "\\[HarmonyPatch"), "no Harmony patch of its own");

        // Its clock and its dev actions.
        C(Sched.RareOffsets.Contains(Sched.RareBeasts) && PassTable.All.Any(p => p.pass == Pass.Beasts && p.period == Sched.HourTicks && p.offset == Sched.RareBeasts),
            "its pass every 250 ticks, its hour on the same tick");
        string book = Code("Beasts/BeastBook.cs");
        C(book.Contains("Sched.Every(now, Sched.RareTicks, Sched.RareBeasts)") && book.Contains("PassClock.Time(Pass.RareBeasts)") && book.Contains("PassClock.Time(Pass.Beasts)")
          && book.Contains("Sched.FirstHourOfDay(now)"), "the book runs on its slot, timed, its day in the first hour");
        int debug = Regex.Matches(beastCode, "\\[DebugAction\\(\"Rimfolk\", \"Beasts: ").Count;
        C(debug >= 12, $"a dev action for each part ({debug})");

        // Merged beside the colony's other animal stories: one story an animal.
        string resident = Code("Animals/ResidentBeast.cs");
        C(resident.Contains("if (PackAlphas.InPack(a) || CouncilGameComponent.Instance?.nemeses?.For(a) != null) return false;"),
            "the beast that stays never takes up an alpha, one of its pack or a nemesis");
        return n;
    }

    private static bool Near(float a, float b) => Math.Abs(a - b) < 0.0001f;

    private static string Root()
    {
        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        return root;
    }
}
