using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The content update's variety, the gods first (part C of docs/content-update-proposals.md; the player's go of 20 November
/// 2026): four more vows (C5), the gods' rarest gifts from the player's mods (C10), the rest of the player's xenotypes drawn
/// to a god (C11), more council questions of faith (C13) and telling the bees (C23). Their rules, defs, words, seams and the
/// player's mods by name, each in the active rows of docs/player-mods.
/// </summary>
public static class GodsVarietyTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the gods' variety: " + what);
        }
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(string rel) => File.ReadAllText(Path.Combine(src, rel));
        string keyedAll = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml").Select(File.ReadAllText));
        var keyed = Regex.Matches(keyedAll, @"^\s*<([A-Za-z0-9_]+)>(.*?)</\1>\s*$", RegexOptions.Multiline).Cast<Match>()
            .GroupBy(m => m.Groups[1].Value).ToDictionary(g => g.Key, g => g.First().Groups[2].Value);
        var defs = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).Select(f => XDocument.Load(f).Root!).ToList();
        List<string> Pool(string name) => defs.SelectMany(d => d.Elements().Where(e => e.Name.LocalName == "ColonyCouncil.FlavorPoolDef" && e.Element("defName")?.Value == name))
            .SelectMany(e => e.Descendants("li")).Select(l => l.Value).ToList();
        XElement Def(string type, string name) => defs.SelectMany(d => d.Elements()).FirstOrDefault(e => e.Name.LocalName == type && e.Element("defName")?.Value == name);
        string mods = Path.Combine(root, "docs", "player-mods");
        var active = new HashSet<string>(File.ReadAllLines(Path.Combine(mods, "xenotypes.tsv")).Skip(1).Select(l => l.Split('\t')).Where(c => c.Length > 2 && c[2] == "yes").Select(c => c[0]));
        var weapons = new HashSet<string>(File.ReadAllLines(Path.Combine(mods, "weapons.tsv")).Skip(1).Select(l => l.Split('\t')).Where(c => c.Length > 2 && c[2] == "yes").Select(c => c[0]));
        var all = new HashSet<string>();
        using (var gz = new GZipStream(File.OpenRead(Path.Combine(mods, "defs.tsv.gz")), CompressionMode.Decompress))
        using (var sr = new StreamReader(gz))
        {
            string? line;
            while ((line = sr.ReadLine()) != null)
            {
                var c = line.Split('\t');
                if (c.Length > 3 && c[3] == "yes") all.Add(c[0] + "|" + c[1] + "|" + c[2]);
            }
        }

        // ================= C11: who is drawn to which god =================

        var rows = new (string x, string g)[]
        {
            ("AG_Animusen", "Hush"), ("AG_Malachai", "Undertow"), ("AG_Wretch", "Undertow"), ("AG_Lapis", "Smoulder"), ("Uredd", "Cirrus"), ("Askbarn", "Cirrus"),
            ("AG_Taukai", "Pip"), ("AG_Helixien", "Pip"), ("VRE_Archon", "Pip"), ("AG_Hiveling", "Morel"), ("VRE_Insector", "Morel"), ("VRE_Ocularkin", "Hollin"),
            ("VRESaurids_Saurid", "Helios"), ("Highmate", "Revel"), ("Waster", "Revel"), ("AG_Fleetkind", "Ennu"), ("DV_Avaloi", "Selene"),
            ("PoI_Umbreon", "Selene"), ("PoI_Espeon", "Helios"), ("PoI_Glaceon", "Hush"), ("PoI_Flareon", "Smoulder"), ("PoI_Leafeon", "Hollin"), ("PoI_Vaporeon", "Murmur"),
            ("Bonobo", "Bellwether"), ("Chimp", "Bellwether"), ("Gorilla", "Bellwether"), ("Orangutan", "Bellwether"), ("RimworldMonkey", "Bellwether")
        };
        foreach (var (x, g) in rows)
            C(PawnGodsMath.XenotypeDraws.Any(d => d.xeno == x && d.god == g && d.w > 0f) && active.Contains(x), $"C11: {x} draws to {g}, an active xenotype of the player's");
        C(PawnGodsMath.XenotypeDraws.Where(d => d.xeno.StartsWith("PoI_") || d.xeno.StartsWith("AG_")).All(d => active.Contains(d.xeno)), "C11: every mod xenotype named is in the player's active rows");
        C(PawnGodsMath.XenotypeDraws.All(d => OwnHappeningsMath.Gods.Contains(d.god)), "C11: each draws to one of the fifteen (as belief, never favour)");

        // ================= C5: four more vows =================

        var season = new[] { VowKind.Lamp, VowKind.Trees, VowKind.PlainBread, VowKind.Visitors };
        C(season.All(k => (int)k > (int)VowKind.BeastSpared) && season.All(WorshipMath.SeasonVow) && !WorshipMath.SeasonVow(VowKind.Temple),
            "C5: four vows of a season, added after the older four so a save keeps its kinds");
        C(season.All(k => WorshipMath.VowDays(k) == WorshipMath.SeasonVowDays) && WorshipMath.SeasonVowDays == 15, "C5: each for a season, fifteen days");
        C(WorshipMath.VowFits(VowKind.Lamp, "Pip") && WorshipMath.VowFits(VowKind.Lamp, "Selene") && WorshipMath.VowFits(VowKind.Lamp, "Helios") && WorshipMath.VowFits(VowKind.Lamp, "Undertow")
          && !WorshipMath.VowFits(VowKind.Lamp, "Hollin") && WorshipMath.VowFits(VowKind.Trees, "Hollin") && !WorshipMath.VowFits(VowKind.Trees, "Pip")
          && WorshipMath.VowFits(VowKind.PlainBread, "Hush") && WorshipMath.VowFits(VowKind.PlainBread, "Morel") && !WorshipMath.VowFits(VowKind.PlainBread, "Revel")
          && WorshipMath.VowFits(VowKind.Visitors, "Ennu") && WorshipMath.VowFits(VowKind.Visitors, "Ducat") && !WorshipMath.VowFits(VowKind.Visitors, "Smoulder")
          && WorshipMath.VowFits(VowKind.Temple, "Pip"), "C5: each only to the gods its words are for; the older vows to any");
        C(!WorshipMath.WeekMissed(6, 0) && WorshipMath.WeekMissed(7, 0) && !WorshipMath.WeekMissed(7, 1) && WorshipMath.WeekMissed(14, 1) && !WorshipMath.WeekMissed(14, 2)
          && WorshipMath.TreeWeek(0) == 0 && WorshipMath.TreeWeek(13) == 1, "C5: a tree a week, broken when a week goes by with none planted");
        C(WorshipMath.TooFine(9, 9) && WorshipMath.TooFine(10, 9) && !WorshipMath.TooFine(8, 9), "C5: plain bread broken by a fine or a lavish meal, kept by a simple one");
        string seasonCode = Read("Gods/Worship/VowsSeason.cs"), vows = Read("Gods/Worship/Vows.cs");
        foreach (var k in season)
        {
            C(new[] { "CC_Wsh_Vow_What_", "CC_Wsh_Vowed_", "CC_Wsh_VowKept_", "CC_Wsh_VowBroken_", "CC_Wsh_Goal_Season_", "CC_Wsh_Goal_SeasonDone_" }.All(p => keyed.ContainsKey(p + k)), k + ": its words");
            C(Pool("Rimfolk_Pool_Worship_Vow").Any(l => l.StartsWith("[vow:" + k.ToString().ToLowerInvariant() + "]")) && Pool("Rimfolk_Pool_Worship_VowKeep").Any(l => l.StartsWith("[" + k.ToString().ToLowerInvariant() + "]")),
                k + ": said when vowed and as it is kept");
            var asp = defs.SelectMany(d => d.Elements()).FirstOrDefault(e => e.Name.LocalName == "ColonyCouncil.AspirationDef" && e.Element("workerClass")?.Value == "ColonyCouncil.AspirationWorker_Vow" + k);
            C(asp != null && asp.Element("chosen") != null && asp.Element("achieved") != null && Regex.IsMatch(seasonCode, @"class AspirationWorker_Vow" + k + @" : AspirationWorker_SeasonVow"),
                k + ": a life goal in the life goals' flavour style, its vow made when it is set");
        }
        C(vows.Contains("case VowKind.Lamp:") && vows.Contains("VowsSeason.Hourly(r, map, now);") && vows.Contains("VowsSeason.Ate(ingester, __instance);")
          && Read("Gods/OnMap/OwnHappenings.cs").Contains("VowsSeason.Felled(by);") && vows.Contains("AddMark(p, \"vowkept:\" + r.kind, g.id)"),
            "C5: in the vows' hour, a meal eaten, a tree felled, and the life goal done when the vow is kept");
        C(seasonCode.Contains("RefuelWorkGiverUtility.RefuelJob(p, r.thing, true)") && seasonCode.Contains("PeoplesE.Sapling(map, target.Cell, def)") && seasonCode.Contains("JobDefOf.TakeInventory")
          && seasonCode.Contains("TryDrop(meal, guest.Position"), "C5: kept on the map by the vower's own walk: fuel carried, a sapling set, a meal set down by a caller");
        C(seasonCode.Contains("CC_Wsh_How_LampOut") && seasonCode.Contains("CC_Wsh_How_NoTree") && seasonCode.Contains("CC_Wsh_How_FineMeal") && seasonCode.Contains("CC_Wsh_How_Unfed") && seasonCode.Contains("CC_Wsh_How_Felled"),
            "C5: broken by what anyone can see: the light gone out, a week with no tree, a tree felled, a fine meal eaten, callers left unfed");
        C(Def("JobDef", "Rimfolk_VowKeep") != null && Read("Gods/Worship/WorshipBook.cs").Contains("Scribe_References.Look(ref thing, \"thing\");"), "C5: its job, and the lamp it keeps saved with the vow");

        // ================= C10: the gods' rarest gifts =================

        foreach (var kv in GodRareGiftsMath.Weapons)
            foreach (var w in kv.Value)
                C(weapons.Contains(w) && OwnHappeningsMath.Gods.Contains(kv.Key), $"C10: {kv.Key}'s {w}, Possessed Weapons' own in the player's active rows");
        C(GodRareGiftsMath.Weapons.Count == 8 && GodRareGiftsMath.Of("Smoulder").Length == 2 && GodRareGiftsMath.Of("Bellwether").Length == 0, "C10: eight gods' weapons, the maul and the craghammer for the Coil, none for the others");
        C(all.Contains("ThingDef|" + GodRareGiftsMath.DrakeEgg + "|onyxae.dragonsdescent"), "C10: Dragons Descent's warm drake's egg, by name");
        C(GodRareGiftsMath.RareDays == 120f && !GodRareGiftsMath.Due(119f, 0f, 1f) && GodRareGiftsMath.Due(120f, 0.1f, 0.5f) && !GodRareGiftsMath.Due(200f, 0.6f, 0.5f) && !GodRareGiftsMath.Due(float.NaN, 0f, 1f),
            "C10: once in two colony-years at most, at its chance");
        string rare = Read("Gods/Touch/GodRareGifts.cs");
        C(Read("Gods/Touch/GodTouch.cs").Contains("GodRareGifts.AfterGreat(b, ctx);") && Read("Gods/OnMap/GodWaking.cs").Contains("GodRareGifts.AfterWaking(god, map, at);"),
            "C10: after a god's great effect, and after the Coil's waking");
        C(rare.Contains("if (c == null || c.map == null || c.dark) return false;") && rare.Contains("hatcheeFaction = Faction.OfPlayer") && rare.Contains("Filth_Ash"),
            "C10: never after an anger; the egg hatches as the colony's; a ring of scorched ground");
        C(new[] { "CC_GodRare_Weapon", "CC_GodRare_Egg" }.All(k => keyed.ContainsKey(k) && keyed.ContainsKey(k + "_2") && keyed.ContainsKey(k + "_3") && !keyed[k].Contains("{GOD}")),
            "C10: the words say what lay there, never whose");
        C(!Regex.IsMatch(rare, @"Harm\.|TakeDamage|DamageInfo"), "C10: Rimfolk adds no harm (the weapons' bonds and traits are the game's and the mod's)");

        // ================= C13: more council questions of faith =================

        string more = Read("Gods/Worship/WorshipCouncilMore.cs");
        var playEffects = new[] { "DilemmaEffect_ShrineWords", "DilemmaEffect_ScofferCarries", "DilemmaEffect_ShrineGiven", "DilemmaEffect_ShrineInTheWay" };
        foreach (var name in new[] { "Rimfolk_Dlm_ShrineInTheWay", "Rimfolk_Dlm_FeastHardSeason", "Rimfolk_Dlm_ScofferAtRite", "Rimfolk_Dlm_ShrineUnkept" })
        {
            var d = Def("ColonyCouncil.DilemmaDef", name);
            var worker = d?.Element("workerClass")?.Value?.Replace("ColonyCouncil.", "") ?? "";
            C(d != null && more.Contains("class " + worker + " : DilemmaWorker_Gods") && d.Element("texts")!.Elements("li").Count() >= 3, name + ": a question of its own, three openings");
            var opts = d?.Element("options")?.Elements("li").ToList() ?? new List<XElement>();
            C(opts.Count == 3, name + ": three answers");
            foreach (var o in opts.Where(o => !o.Element("label")!.Value.StartsWith("Let it be")))
                C(o.Element("effects")!.Elements("li").Any(e => playEffects.Contains(e.Attribute("Class")?.Value?.Replace("ColonyCouncil.", "") ?? "")),
                    name + ": \"" + o.Element("label")!.Value + "\" plays out on the map");
        }
        C(Read("Calendar/CalendarKeeping.cs").Contains("string eve = WorshipCouncilMore.EveAnswer(GodOfDay(d), map);") && Read("Gods/Worship/Festivals.cs").Contains("bool lean = WorshipCouncilMore.EveAnswer(g, map) == \"lean\";"),
            "C13: the festival's eve answer kept on its day: with less (no offering, no guests), the rite only (the day not kept), or let pass");
        C(Read("Gods/Worship/FaithHeart.cs").Contains("Worship.Book?.AddMark(scoffer, \"mocked\", g.id);") && Read("Gods/OnMap/OwnFeasts.cs").Contains("!WorshipCouncilMore.Barred(p)")
          && Read("Gods/Worship/Offerings.cs").Contains("if (o.asked != null) who.Add(o.asked);"),
            "C13: the scoffer's laugh noted; one kept from the rites left out of the festival's gathering; one set to carry the offering carries it");
        C(more.Contains("DesignationDefOf.Uninstall") && more.Contains("t.Destroy(DestroyMode.Cancel)") && more.Contains("DesignationDefOf.Deconstruct") && more.Contains("Shrines.Ask(place, c.God2)")
          && more.Contains("Shrines.Undedicate(place)"), "C13: the place taken up, built round, taken down, given to another god, or let fall");

        // ================= C23: telling the bees =================

        C(TellingBeesMath.Hives.All(h => all.Contains("ThingDef|" + h + "|sarg.rimbees")) && all.Contains("WorkTypeDef|" + TellingBeesMath.BeeWork + "|sarg.rimbees"),
            "C23: Alpha Bees' beehouses and its beekeeping work, by name, in the player's active rows");
        C(TellingBeesMath.Keeper(new[] { 0, 3, 1 }, new[] { 20, 5, 2 }) == 2 && TellingBeesMath.Keeper(new[] { 2, 2 }, new[] { 4, 9 }) == 1 && TellingBeesMath.Keeper(new[] { 0, 0 }, new[] { 4, 9 }) == -1,
            "C23: the one who tends the hives most (first priority, then skill with animals); nobody with the work, nobody by it");
        C(TellingBeesMath.Hour(10) && !TellingBeesMath.Hour(22) && TellingBeesMath.StillWaiting(2f) && !TellingBeesMath.StillWaiting(4f) && TellingBeesMath.MostHives <= 4,
            "C23: told in the day's light hours, the news let go after three days (no penalty if nobody goes), four hives at most");
        string bees = Read("Immersion/TellingBees.cs");
        C(bees.Contains("[HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]") && Read("CouncilGameComponent.cs").Contains("Safe(\"telling the bees\", TellingBees.Hourly);")
          && bees.Contains("TimeAsColonistOrColonyAnimal"), "C23: a death of the colony's, and someone new, both told");
        C(bees.Contains("TellingBeesDefOf.Rimfolk_Bees_Told") && bees.Contains("news.death &&") && bees.Contains("CC_Bees_Journal_Dead") && Def("ThoughtDef", "Rimfolk_Bees_Told") != null,
            "C23: after a death the teller's grief eases a little, and the journal keeps a line");
        C(Pool("Rimfolk_Pool_TellingBees").Count(l => l.StartsWith("[dead]")) >= 3 && Pool("Rimfolk_Pool_TellingBees").Count(l => l.StartsWith("[new]")) >= 3 && Def("JobDef", "Rimfolk_TellBees") != null,
            "C23: the words to the bees, one sentence each, and the round of the hives");
        C(bees.Contains("public static bool On => SmallHabits.On && HiveDefs.Count > 0"), "C23: never without Alpha Bees, under the small habits setting");
        string save = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        C(save.Contains("\"Immersion/TellingBees.cs:waiting\"") && save.Contains("\"Immersion/TellingBees.cs:told\""), "C23: saved small and modelled");

        return n;
    }
}
