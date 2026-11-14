using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The gods, each their own (round 2 of docs/gods-next-proposals.md; the player, 14 November 2026: "gods effect should be
/// noticeable"; "each of the gods should feel really unique in what it does and whether or how it interacts with the
/// world"): each god's own presence (its names, its words, one effect of its own, its sizes and lengths, its show on the
/// map and its hooks into the game), each god's own way of taking an offering (when, what is left, its words, the next
/// day's noticing, the inspect line), Bellwether's and Ennu's own signs, and how a god plugs in (Tamiu folded in).
/// </summary>
public static class GodsOwnTests
{
    private static readonly string[] Fourteen = { "Hollin", "Smoulder", "Bellwether", "Morel", "Ennu", "Pip", "Cirrus", "Hush", "Undertow", "Helios", "Selene", "Mog", "Murmur", "Ducat" };

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the gods each their own: " + what);
        }
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(string rel) => File.ReadAllText(Path.Combine(src, rel));
        var defs = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).Select(f => XDocument.Load(f).Root!).ToList();
        string keyedAll = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml").Select(File.ReadAllText));
        var keyed = Regex.Matches(keyedAll, @"^\s*<([A-Za-z0-9_]+)>(.*?)</\1>\s*$", RegexOptions.Multiline).Cast<Match>()
            .GroupBy(m => m.Groups[1].Value).ToDictionary(g => g.Key, g => g.First().Groups[2].Value);
        var pools = defs.SelectMany(d => d.Elements().Where(e => e.Name.LocalName == "ColonyCouncil.FlavorPoolDef"))
            .GroupBy(e => e.Element("defName")!.Value).ToDictionary(g => g.Key, g => g.SelectMany(e => e.Descendants("li")).Select(l => l.Value).ToList());
        string Tags(string l) => Regex.Match(l, @"^\[([^\]]*)\]").Groups[1].Value;
        string Body(string l) => Regex.Replace(l, @"^\[[^\]]*\]\s*", "");
        var sentence = new Regex(@"[.?!][""']?\s+[A-Z{]");
        bool Spoken(string l) => !sentence.IsMatch(Body(l)) && Body(l).Length <= 90 && !Body(l).StartsWith("\"");

        // ================= How strong, and each effect's size =================

        C(GodPresenceOwnMath.Strength(PresenceKind.Blessing) == 1f && GodPresenceOwnMath.Strength(PresenceKind.Appear) > GodPresenceOwnMath.Strength(PresenceKind.Act)
          && GodPresenceOwnMath.Strength(PresenceKind.Act) > GodPresenceOwnMath.Strength(PresenceKind.Depart) && GodPresenceOwnMath.Strength(PresenceKind.Omen) == 0f,
            "a blessing fully, a coming nearly, an act less, a departure a little, an omen not at all");
        C(GodPresenceOwnMath.Harsh(PresenceKind.Wrath) && !GodPresenceOwnMath.Harsh(PresenceKind.Blessing), "a wrath is the hard side");
        C(Math.Abs(GodPresenceOwnMath.Growth(PresenceEffect.Growth, 1f, false, PresencePlant.Wild, false) - GodPresenceOwnMath.GreenGrowth) < 0.001f
          && GodPresenceOwnMath.Growth(PresenceEffect.Growth, 1f, true, PresencePlant.Wild, false) == 1f
          && GodPresenceOwnMath.Growth(PresenceEffect.Growth, 0.4f, false, PresencePlant.Crop, false) is > 1f and < GodPresenceOwnMath.GreenGrowth,
            "Hollin's: every plant about a third faster at full, less for a smaller moment, none in her wrath");
        C(GodPresenceOwnMath.Growth(PresenceEffect.Rot, 1f, false, PresencePlant.Fungus, false) == GodPresenceOwnMath.FungusGrowth
          && GodPresenceOwnMath.Growth(PresenceEffect.Rot, 1f, false, PresencePlant.Crop, false) == 1f, "Morel's: mushrooms only");
        C(GodPresenceOwnMath.Growth(PresenceEffect.LongNoon, 1f, false, PresencePlant.Crop, false) > 1f && GodPresenceOwnMath.Growth(PresenceEffect.LongNoon, 1f, false, PresencePlant.Wild, false) == 1f
          && GodPresenceOwnMath.Growth(PresenceEffect.LongNoon, 1f, true, PresencePlant.Crop, false) == 1f, "Helios's: crops, never in his wrath");
        C(GodPresenceOwnMath.Growth(PresenceEffect.Rain, 1f, false, PresencePlant.Wild, true) > 1f && GodPresenceOwnMath.Growth(PresenceEffect.Rain, 1f, false, PresencePlant.Wild, false) == 1f,
            "Murmur's: what stands on wet ground");
        foreach (var e in Enum.GetValues(typeof(PresenceEffect)).Cast<PresenceEffect>().Where(x => x != PresenceEffect.Growth && x != PresenceEffect.Rot && x != PresenceEffect.LongNoon && x != PresenceEffect.Rain))
            C(GodPresenceOwnMath.Growth(e, 1f, false, PresencePlant.Crop, true) == 1f, $"{e} grows nothing");
        C(GodPresenceOwnMath.Temperature(PresenceEffect.WarmStone, 1f, false) > 0f && GodPresenceOwnMath.Temperature(PresenceEffect.WarmStone, 1f, true) > GodPresenceOwnMath.Temperature(PresenceEffect.WarmStone, 1f, false)
          && GodPresenceOwnMath.Temperature(PresenceEffect.LongNoon, 1f, false) > 0f && GodPresenceOwnMath.Temperature(PresenceEffect.WhiteSilence, 1f, false) < 0f
          && GodPresenceOwnMath.Temperature(PresenceEffect.WhiteSilence, 1f, true) < GodPresenceOwnMath.Temperature(PresenceEffect.WhiteSilence, 1f, false)
          && GodPresenceOwnMath.Temperature(PresenceEffect.Warmth, 1f, false) > 0f && GodPresenceOwnMath.Temperature(PresenceEffect.Warmth, 1f, true) == 0f
          && GodPresenceOwnMath.Temperature(PresenceEffect.Growth, 1f, false) == 0f,
            "warm stone and the long noon warm the air (more in a wrath), the white silence chills it (deeper in a wrath), a warm evening a little");
        C(Math.Abs(GodPresenceOwnMath.Temperature(PresenceEffect.WhiteSilence, 1f, false)) <= 8f && GodPresenceOwnMath.Temperature(PresenceEffect.LongNoon, 1f, true) <= 8f,
            "never more than a cold snap's or a heat wave's few degrees (harm at the game's own rate)");
        C(GodPresenceOwnMath.UnderRockHeat(PresenceEffect.WarmStone, 1f, false) > 0f && GodPresenceOwnMath.SnowMelt(PresenceEffect.WarmStone, 1f) > 0f
          && GodPresenceOwnMath.UnderRockHeat(PresenceEffect.Growth, 1f, false) == 0f, "heat under the rock and snow melting off it, Smoulder's alone");
        C(GodPresenceOwnMath.Rot(PresenceEffect.Rot, 1f, false, true) == GodPresenceOwnMath.SporeRot && GodPresenceOwnMath.Rot(PresenceEffect.Rot, 1f, true, true) == GodPresenceOwnMath.SporeRotWrath
          && GodPresenceOwnMath.SporeRotWrath > GodPresenceOwnMath.SporeRot, "Morel's rot faster, faster still in her wrath");
        C(GodPresenceOwnMath.Rot(PresenceEffect.WhiteSilence, 1f, false, false) == 0f && GodPresenceOwnMath.Rot(PresenceEffect.WhiteSilence, 1f, false, true) == 1f
          && GodPresenceOwnMath.Rot(PresenceEffect.Growth, 1f, false, false) == 1f, "nothing rots out of doors under Hush's, as it would under a roof");
        C(GodPresenceOwnMath.Immunity(PresenceEffect.Rot, 1f, false) > 1f && GodPresenceOwnMath.Immunity(PresenceEffect.Rot, 1f, true) == 1f, "sicknesses pass faster under Morel's, not in her wrath");
        C(GodPresenceOwnMath.Insults(PresenceEffect.Grazing, 1f, false) < 1f && GodPresenceOwnMath.Fights(PresenceEffect.Grazing, 1f, false) < GodPresenceOwnMath.Fights(PresenceEffect.Warmth, 1f, false)
          && GodPresenceOwnMath.Fights(PresenceEffect.Warmth, 1f, false) < 1f && GodPresenceOwnMath.Insults(PresenceEffect.Grazing, 1f, true) == 1f
          && GodPresenceOwnMath.Insults(PresenceEffect.Hum, 1f, false) == 1f, "fewer quarrels under the Old Horn (fewest fights) and the Guest, none spared in a wrath");
        C(GodPresenceOwnMath.CalmsBeasts(PresenceEffect.Grazing, false) && !GodPresenceOwnMath.CalmsBeasts(PresenceEffect.Grazing, true) && !GodPresenceOwnMath.CalmsBeasts(PresenceEffect.Watch, false),
            "no animal turns on anyone under the Old Horn's peace");
        C(!GodPresenceOwnMath.Talks(PresenceEffect.WhiteSilence) && GodPresenceOwnMath.Talks(PresenceEffect.Warmth), "nobody talks under the white silence");
        C(GodPresenceOwnMath.WarmEveningMood(PresenceEffect.Warmth, false, true) > GodPresenceOwnMath.WarmEveningMood(PresenceEffect.Warmth, false, false)
          && GodPresenceOwnMath.WarmEveningMood(PresenceEffect.Warmth, false, false) > 0 && GodPresenceOwnMath.WarmEveningMood(PresenceEffect.Warmth, true, true) == 0,
            "a warm evening: visitors gladder than the colony's own, nobody when the Guest was wronged");
        C(GodPresenceOwnMath.BatteryKept(PresenceEffect.Hum, 1f, false) is > 0f and < 1f && GodPresenceOwnMath.BatteryKept(PresenceEffect.Hum, 1f, true) == 0f
          && GodPresenceOwnMath.BenchSpeed(PresenceEffect.Hum, 1f, false) > 1f && GodPresenceOwnMath.Pranks(PresenceEffect.Hum, false) && !GodPresenceOwnMath.Pranks(PresenceEffect.Hum, true),
            "batteries hold (never for free), powered benches go faster, one prank, none of it while Pip sulks");
        C(GodPresenceOwnMath.PrankEnds(10, 100, true, false) && GodPresenceOwnMath.PrankEnds(10, 100, false, true) && GodPresenceOwnMath.PrankEnds(100, 100, false, false)
          && !GodPresenceOwnMath.PrankEnds(10, 100, false, false) && GodPresenceOwnMath.PrankTicks <= 2500,
            "the prank ends when someone else opens the door, when its victim must pass (danger, need), or within the hour");
        C(GodPresenceOwnMath.MiningYield(PresenceEffect.WarmStone, 1f, false) > 1f && GodPresenceOwnMath.MiningYield(PresenceEffect.WarmStone, 1f, true) == 1f, "mining yields more under warm stone, not in his wrath");
        C(GodPresenceOwnMath.Trade(PresenceEffect.Glint, 1f, false) > 0f && GodPresenceOwnMath.Trade(PresenceEffect.Glint, 1f, true) < 0f && GodPresenceOwnMath.Trade(PresenceEffect.Hum, 1f, false) == 0f,
            "traders deal better under Ducat's glint, worse in its wrath");
        C(GodPresenceOwnMath.CoinTrail(49) == 0 && GodPresenceOwnMath.CoinTrail(100) == 2 && GodPresenceOwnMath.CoinTrail(100000) == 5, "a few coins trail off a big store, none off a small one");
        C(GodPresenceOwnMath.Lamps(PresenceEffect.Dark, 1f, true) < GodPresenceOwnMath.Lamps(PresenceEffect.Dark, 1f, false) && GodPresenceOwnMath.Lamps(PresenceEffect.Dark, 1f, false) < 1f
          && GodPresenceOwnMath.Lamps(PresenceEffect.Dark, 1f, true) >= 0.5f && GodPresenceOwnMath.Psychic(PresenceEffect.Dark, 1f, false) > 0f && GodPresenceOwnMath.Darker(PresenceEffect.Dark, false) > 0f,
            "lamps reach less far under the dark (never out), minds open, the sky darker");
        C(GodPresenceOwnMath.HeldGlow(PresenceEffect.LongNoon, 0.9f) == 0.9f && GodPresenceOwnMath.HeldGlow(PresenceEffect.LongNoon, 0.2f) == 0f && GodPresenceOwnMath.HeldGlow(PresenceEffect.Growth, 0.9f) == 0f
          && GodPresenceOwnMath.Solar(PresenceEffect.LongNoon, 1f) > 1f, "the long noon holds the light of the hour it came by day (the night stays night), and the panels give more");
        C(GodPresenceOwnMath.NightGlow(PresenceEffect.MoonSleep, false, 1f) > GodPresenceOwnMath.NightGlow(PresenceEffect.MoonSleep, false, 0.4f)
          && GodPresenceOwnMath.NightGlow(PresenceEffect.MoonSleep, false, 1f) <= 0.5f && GodPresenceOwnMath.NightGlow(PresenceEffect.MoonSleep, true, 1f) == 0f,
            "the night under Selene's as light as her face, at most dusk, none in her wrath");
        C(GodPresenceOwnMath.SleepHealing(PresenceEffect.MoonSleep, 1f, false, true) > 1f && GodPresenceOwnMath.SleepHealing(PresenceEffect.MoonSleep, 1f, false, false) == 1f,
            "healing faster only for those asleep");
        C(GodPresenceOwnMath.GazeChance(PresenceEffect.HighSky, false) > 1f && GodPresenceOwnMath.GazeJoy(PresenceEffect.HighSky, false) > 1f && GodPresenceOwnMath.FarLightningEvery(PresenceEffect.HighSky, true)
          < GodPresenceOwnMath.FarLightningEvery(PresenceEffect.HighSky, false) && GodPresenceOwnMath.FarLightningEvery(PresenceEffect.Rain, false) == 0, "Cirrus's sky watched back, far lightning more often in its wrath");

        // ================= Weather and length =================

        C(GodPresenceOwnMath.Weather(PresenceEffect.HighSky, false, 1, 20f, 0f, "Rain").SequenceEqual(new[] { "Clear" })
          && GodPresenceOwnMath.Weather(PresenceEffect.HighSky, true, 1, 20f, 0f, "Rain").SequenceEqual(new[] { "Rain" }), "Cirrus's clears the sky, or in its wrath holds the weather as it was");
        C(GodPresenceOwnMath.Weather(PresenceEffect.WhiteSilence, false, 3, -5f, 0f, null).SequenceEqual(new[] { "FogSnow", "SnowGentle" })
          && GodPresenceOwnMath.Weather(PresenceEffect.WhiteSilence, false, 1, 15f, 0f, null).SequenceEqual(new[] { "Fog" }), "Hush's fine snow where it is cold (Seasonal Weather's, else the game's), fog where it isn't");
        C(GodPresenceOwnMath.Weather(PresenceEffect.Rain, false, 1, 15f, 0.5f, null).Count == 0 && GodPresenceOwnMath.Weather(PresenceEffect.Rain, false, 1, 15f, 2f, null).SequenceEqual(new[] { "Rain" })
          && GodPresenceOwnMath.Weather(PresenceEffect.Rain, true, 1, 15f, 2f, null).SequenceEqual(new[] { "TorrentialRain", "Rain" }), "Murmur's rain from its first hour on (Odyssey's torrent in his wrath, the game's rain after)");
        C(GodPresenceOwnMath.Weather(PresenceEffect.Growth, false, 0, 15f, 0f, null).SequenceEqual(new[] { "AB_PetalStorms" }) && GodPresenceOwnMath.Weather(PresenceEffect.Growth, false, 1, 15f, 0f, null).Count == 0,
            "Hollin's petals in spring only (Alpha Biomes)");
        foreach (var e in new[] { PresenceEffect.WarmStone, PresenceEffect.Grazing, PresenceEffect.Rot, PresenceEffect.Warmth, PresenceEffect.Hum, PresenceEffect.Dark, PresenceEffect.LongNoon, PresenceEffect.MoonSleep, PresenceEffect.Glint, PresenceEffect.Watch })
            C(GodPresenceOwnMath.Weather(e, false, 0, 10f, 3f, "Clear").Count == 0 && GodPresenceOwnMath.Weather(e, true, 0, 10f, 3f, "Clear").Count == 0, $"{e} leaves the weather alone");
        foreach (var k in new[] { PresenceKind.Appear, PresenceKind.Act, PresenceKind.Blessing, PresenceKind.Wrath, PresenceKind.Depart })
            C(GodPresenceOwnMath.Ticks(PresenceEffect.Growth, k, 12) == GodPresenceMath.ConditionTicks(k) && GodPresenceOwnMath.Ticks(PresenceEffect.Glint, k, 2) == GodPresenceMath.ConditionTicks(k),
                $"{k}: the moment's own length for a presence that asks for no other");
        C(GodPresenceOwnMath.Ticks(PresenceEffect.MoonSleep, PresenceKind.Act, 22) == 9 * 2500 && GodPresenceOwnMath.Ticks(PresenceEffect.MoonSleep, PresenceKind.Act, 3) == 4 * 2500
          && GodPresenceOwnMath.Ticks(PresenceEffect.MoonSleep, PresenceKind.Act, 12) == GodPresenceMath.ConditionTicks(PresenceKind.Act),
            "Selene's begun at night lasts until seven in the morning, so the sleepers have it all night");
        C(GodPresenceOwnMath.Ticks(PresenceEffect.Rain, PresenceKind.Depart, 12) == 6 * 2500 && GodPresenceOwnMath.Ticks(PresenceEffect.Rain, PresenceKind.Omen, 12) == 0,
            "Murmur's at least six hours, so the rain comes; an omen none");
        C(GodPresenceOwnMath.HeldWhileHere(PresenceEffect.Grazing) && GodPresenceOwnMath.HeldWhileHere(PresenceEffect.Warmth) && GodPresenceOwnMath.HeldWhileHere(PresenceEffect.Watch)
          && !GodPresenceOwnMath.HeldWhileHere(PresenceEffect.Growth), "the Old Horn's, the Guest's and Tamiu's last while they walk the land");
        C(GodPresenceOwnMath.Held(100, true) == GodPresenceOwnMath.HeldTicks && GodPresenceOwnMath.Held(100, false) == 100 && GodPresenceOwnMath.Held(99999, true) == 99999, "held: never less than two hours left while here");
        C(GodPresenceOwnMath.EndsWhenGone(PresenceEffect.Watch, false, 3000) && !GodPresenceOwnMath.EndsWhenGone(PresenceEffect.Watch, true, 3000) && !GodPresenceOwnMath.EndsWhenGone(PresenceEffect.Watch, false, 100)
          && !GodPresenceOwnMath.EndsWhenGone(PresenceEffect.Grazing, false, 3000), "Tamiu's ends once she has gone");

        // ================= Dreams =================

        C(GodPresenceOwnMath.TrueDreamNow(PresenceEffect.MoonSleep, false, true, 6) && !GodPresenceOwnMath.TrueDreamNow(PresenceEffect.MoonSleep, false, false, 6)
          && !GodPresenceOwnMath.TrueDreamNow(PresenceEffect.MoonSleep, true, true, 6) && !GodPresenceOwnMath.TrueDreamNow(PresenceEffect.MoonSleep, false, true, 9),
            "a true dream told at six under Selene's, at the full moon only, never in her wrath");
        C(GodPresenceOwnMath.TrueDream(true, true, true, true, true) == "true:raid" && GodPresenceOwnMath.TrueDream(false, true, true, false, false) == "true:visitors"
          && GodPresenceOwnMath.TrueDream(false, false, true, true, true) == "true:predator" && GodPresenceOwnMath.TrueDream(false, false, false, true, true) == "true:rot"
          && GodPresenceOwnMath.TrueDream(false, false, false, false, true) == "true:sad" && GodPresenceOwnMath.TrueDream(false, false, false, false, false) == "true:moon",
            "what a true dream is of: what is coming first, what is here after, only the moon when nothing is");
        var dream = pools.TryGetValue("Rimfolk_Pool_GodPO_Dream", out var dl) ? dl : new List<string>();
        foreach (var t in new[] { "true:raid", "true:visitors", "true:predator", "true:rot", "true:sad", "true:moon", "warmvoice" })
            C(dream.Count(l => Tags(l) == t) >= 2, $"the dream {t} has its own words");
        C(dream.Count > 0 && dream.All(Spoken), "every dream told is one short sentence");
        C(dream.Where(l => Tags(l) == "true:sad").All(l => l.Contains("{OTHER}")) && dream.Where(l => Tags(l) == "true:predator").All(l => l.Contains("{SITE}")), "the dream names who and where");

        // ================= Each god's own presence, declared =================

        var godDefs = defs.SelectMany(d => d.Elements().Where(e => e.Name.LocalName == "ColonyCouncil.GodDef")).ToDictionary(e => e.Element("key")!.Value);
        var conds = defs.SelectMany(d => d.Elements("GameConditionDef")).Where(e => e.Element("defName") != null).ToDictionary(e => e.Element("defName")!.Value);
        XElement Own(XElement c) => c?.Element("modExtensions")?.Elements("li").FirstOrDefault(l => (string)l.Attribute("Class") == "ColonyCouncil.OwnPresence");
        var effects = new Dictionary<string, string>();
        foreach (var k in Fourteen)
        {
            string pres = godDefs.TryGetValue(k, out var gd) ? gd.Element("presence")?.Value : null;
            C(pres == GodPresenceOwnMath.DefNameFor(k), $"{k} declares its own presence ({pres})");
            var cond = pres != null && conds.TryGetValue(pres, out var cd) ? cd : null;
            var own = Own(cond);
            C(cond?.Attribute("ParentName")?.Value == "Rimfolk_GodPresenceOwnBase" && own?.Element("god")?.Value == k, $"{k}'s presence is a god's presence whose declaration names it");
            string effect = own?.Element("effect")?.Value ?? "";
            C(Enum.TryParse<PresenceEffect>(effect, out var pe) && pe != PresenceEffect.None, $"{k}'s presence does something of its own ({effect})");
            effects[k] = effect;
            string label = cond?.Element("label")?.Value ?? "", desc = cond?.Element("description")?.Value ?? "";
            C(label.Length > 0 && !label.Contains("{") && desc.Length > 40 && !desc.Contains("{"), $"{k}'s presence, unnamed, says only what anyone can see");
            bool never = own?.Element("neverNamed")?.Value == "true";
            C(never ? k == "Undertow" : (own?.Element("named")?.Value ?? "").Contains("{NAME}") && (own?.Element("namedText")?.Value ?? "").Contains("{NAME}"),
                $"{k}'s presence is named only when the colony is sure ({(never ? "never: the dark" : own?.Element("named")?.Value)})");
            C(!desc.Contains("favour") && !desc.Contains("pleased") && !desc.Contains("angry") && !desc.Contains("wants"), $"{k}'s words tell no favour, want or mood of the god");
        }
        C(effects.Values.Distinct().Count() == Fourteen.Length, "every god's presence does a different thing");
        C(Enum.GetValues(typeof(PresenceEffect)).Cast<PresenceEffect>().Where(x => x != PresenceEffect.None).All(x => effects.ContainsValue(x.ToString())), "every effect belongs to a god");
        var labels = Fourteen.Select(k => conds[GodPresenceOwnMath.DefNameFor(k)].Element("label")!.Value).ToList();
        var descs = Fourteen.Select(k => conds[GodPresenceOwnMath.DefNameFor(k)].Element("description")!.Value).ToList();
        C(labels.Distinct().Count() == 14 && descs.Distinct().Count() == 14 && !labels.Contains("a strange stillness"), "fourteen names and fourteen words, none of them the shared one's");
        C(conds.TryGetValue(GodPresenceOwnMath.SharedDef, out var shared) && shared.Element("conditionClass")?.Value == "ColonyCouncil.GameCondition_GodPresence" && Own(shared) == null,
            "the shared presence stays for a god that declares none, and the strange calm of the omens");
        var catalogue = Catalogue(root);
        foreach (var k in Fourteen)
        {
            var own = Own(conds[GodPresenceOwnMath.DefNameFor(k)]);
            foreach (var name in own!.Element("motes")?.Elements("li").Select(l => l.Value) ?? Enumerable.Empty<string>())
                C(catalogue.TryGetValue(name, out var t) && (t.Contains("ThingDef") || t.Contains("FleckDef")), $"{k}'s mote '{name}' is in the player's mods");
            foreach (var name in own.Element("sounds")?.Elements("li").Select(l => l.Value) ?? Enumerable.Empty<string>())
                C(catalogue.TryGetValue(name, out var t) && t.Contains("SoundDef"), $"{k}'s sound '{name}' is in the player's mods");
        }
        foreach (var w in new[] { "AB_PetalStorms", "Clear", "FogSnow", "SnowGentle", "Fog", "TorrentialRain", "Rain" })
            C(catalogue.TryGetValue(w, out var t) && t.Contains("WeatherDef"), $"the weather '{w}' is in the player's mods");
        foreach (var name in new[] { "Bullfrog", "Pawn_Frog_Call", "Pawn_Thrumbo_Call", "Thunder_OffMap", "GeyserSpray", "Power_OnSmall", "Door_CloseManual", "Skygaze", "VSIE_Skygaze", "NightOwl", "UVSensitivity_Mild", "VRE_Photophobia",
                     "Agarilux", "Glowstool", "Bryolux", "SoilRich", "Cholera", "Dysentery", "Diarrhea" })
            C(catalogue.ContainsKey(name), $"the code's '{name}' is in the player's mods");

        // ================= What each one shows, and its hooks =================

        string shows = Read("Gods/Presence/GodPresenceShows.cs");
        string cond2 = Read("Gods/Presence/GodPresenceCondition.cs");
        string patches = Read("Gods/Presence/GodPresenceOwnPatches.cs");
        string ownCs = Read("Gods/Presence/GodPresenceOwn.cs");
        foreach (var e in Enum.GetValues(typeof(PresenceEffect)).Cast<PresenceEffect>().Where(x => x != PresenceEffect.None))
        {
            int cases = Regex.Matches(shows, @"case PresenceEffect\." + e + @"\b").Count;
            C(cases >= 2 || e == PresenceEffect.Watch && cases >= 1, $"{e} shows on the map ({cases} places in GodPresenceShows)");
            C(shows.Contains("\"notice:\" + c.Effect.ToString().ToLowerInvariant()") && (pools["Rimfolk_Pool_GodPO_Say"].Count(l => Tags(l).Split(',').Contains("notice:" + e.ToString().ToLowerInvariant())) >= 3),
                $"someone says what they see of {e}");
        }
        var say = pools["Rimfolk_Pool_GodPO_Say"];
        C(say.All(Spoken), "every presence line is one short sentence");
        C(say.Where(l => l.Contains("{GOD}")).All(l => Tags(l).Split(',').Contains("named")) && say.Where(l => Tags(l).Split(',').Contains("named")).All(l => l.Contains("{GOD}")),
            "a line names the god only when the colony is sure (tagged named), and every such line does");
        C(!Regex.IsMatch(string.Join("\n", say), @"\b(Hollin|Smoulder|Bellwether|Morel|Ennu|Pip|Cirrus|Hush|Undertow|Helios|Selene|Tamiu|Murmur|Ducat)\b"), "no line names a god by its own name, only the colony's");
        foreach (var t in new[] { "axe", "prank", "prankend", "whisper", "croak" }) C(say.Count(l => Tags(l).Split(',').Contains(t)) >= 2, $"the moment {t} has its words");
        C(say.Count(l => Tags(l).Split(',').Contains("scoffer")) >= 13, "a scoffer has a word for most of them");
        foreach (var (hook, what) in new[]
                 {
                     ("typeof(Plant), nameof(Plant.GrowthRate), MethodType.Getter", "plants grow faster"), ("typeof(Plant), nameof(Plant.GrowthRateCalcDesc)", "the plant's breakdown says so"),
                     ("typeof(CompRottable), \"TickInterval\"", "rot faster or not at all"), ("typeof(InteractionWorker_Insult)", "fewer insults"), ("typeof(InteractionWorker_Slight)", "fewer slights"),
                     ("nameof(Pawn_InteractionsTracker.SocialFightChance)", "fewer fights"), ("\"TryInteractRandomly\"", "the white silence"),
                     ("nameof(MentalStateHandler.TryStartMentalState)", "no manhunters"), ("nameof(CompPowerBattery.DrawPower)", "batteries hold"),
                     ("typeof(CompPowerPlantSolar), \"DesiredPowerOutput\"", "solar panels give more"), ("nameof(CompGlower.GlowRadius)", "lamps reach less far"),
                     ("nameof(Building_Door.PawnCanOpen)", "the prank's door"), ("nameof(Building_Door.StartManualOpenBy)", "the prank ended by someone else"),
                     ("nameof(Plant.PlantCollected)", "a tree felled under the green"), ("nameof(Pawn_HealthTracker.AddHediff)", "the wells run clean")
                 })
            C(patches.Contains(hook) && patches.Contains("GameCondition_GodPresence.AnyOwnUntil"), $"the hook for {what}, looking at nothing while no own presence lasts");
        foreach (var stat in new[] { "MiningYield", "WorkTableWorkSpeedFactor", "TradePriceImprovement", "InjuryHealingFactor", "ImmunityGainSpeed", "ComfyTemperatureMin", "PsychicSensitivity" })
            C(patches.Contains("StatDefOf." + stat + ", PresenceStat."), $"the stat {stat} shows the presence under its label");
        C(patches.Contains("c.LabelCap + \": \"") && patches.Contains("ExplanationPart"), "a stat's breakdown names the presence by its label, never the god");
        C(patches.Contains("public static bool Prepare() => ModsConfig.IsActive(GodPresenceOwn.DubsWells)") && patches.Contains("\"Cholera\", \"Dysentery\", \"Diarrhea\""),
            "the wells run clean only with Dubs Bad Hygiene, by its sicknesses' names");
        C(cond2.Contains("public override float TemperatureOffset()") && cond2.Contains("public override WeatherDef ForcedWeather()") && cond2.Contains("SkyGazeChanceFactor")
          && cond2.Contains("GodPresenceOwnMath.HeldGlow(e, heldGlow)") && cond2.Contains("GodPresenceOwnMath.NightGlow("), "the sky, the heat, the weather and the stargazing are the condition's own, as the game's conditions do it");
        C(cond2.Contains("GodPresenceOwn.NumberLines(this)") && ownCs.Contains("CC_GodPO_Num_"), "its numbers under its words");
        foreach (Match m in Regex.Matches(ownCs, @"""(CC_GodPO_Num_\w+)""")) C(keyed.ContainsKey(m.Groups[1].Value), $"the words {m.Groups[1].Value}");
        C(cond2.Contains("Gods.CardName(g, Map)") && cond2.Contains("Own?.neverNamed"), "named as soon as the colony is sure (a long stay learns it), the dark never");
        C(Read("Gods/GodsWiring.cs").Contains("GodPresenceOwn.DefOf(s.god), s.god.Key") && Read("Gods/Presence/GodPresence.cs").Contains("m.presence, m.godKey"),
            "a god's moment leaves its own presence after it");
        C(Read("Gods/OnMap/GodStranger.cs").Contains("GodPresenceOwn.Quiet(Gods.ById(s.god), s.map)") && Read("Gods/OnMap/MogsWays.cs").Contains("GodPresenceOwn.Quiet(Her, a.Map)")
          && ownCs.Contains("if (map.gameConditionManager.GetActiveCondition<GameCondition_GodPresence>() != null) return;"),
            "the Guest's and Tamiu's begin quietly as they come, never in place of another's");
        C(Read("Gods/OnMap/GodsOnMapBook.cs").Contains("GodPresenceOwn.Hourly(now)"), "each presence has its hour");
        C(shows.Contains("Sched.Every(now, Sched.RareTicks, Sched.RareGodPresence)") && shows.Contains("PassClock.Time(Pass.RareGodPresence)"), "its turns on a slot of their own, timed");
        var waiting = new[] { Sched.RareNews, Sched.RarePlayOut, Sched.RareWeekMemory, Sched.RareCouriers, Sched.RareVisitors, Sched.RareTimed, Sched.RareRearrange, Sched.RareNeighbours,
            Sched.RareOutsiders, Sched.RareJourneys, Sched.RareOutsiderTalk, Sched.RareTriage, Sched.RareHauling, Sched.RareGuestHelp };
        C(!Sched.RareOffsets.Contains(Sched.RareGodPresence) && !waiting.Contains(Sched.RareGodPresence) && !Sched.HourlyOnCycle.Contains(Sched.RareGodPresence)
          && Sched.HourlyOffsets.All(h => h % Sched.RareTicks != Sched.RareGodPresence) && Sched.RareGodPresence < Sched.RareTicks,
            "the presences' slot is on no other pass's tick");
        C(keyed.ContainsKey("CC_Clock_Pass_RareGodPresence"), "the clock names it");
        foreach (var f in new[] { "godKey", "kind", "shown", "said", "startWeather", "heldGlow", "prankPawn", "prankDoor", "prankUntil", "prankSaid", "whispered", "dreamDay" })
            C(cond2.Contains("\"" + f + "\""), $"the presence keeps its {f} in the save");
        C(!Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories).Any(f => File.ReadAllText(f).Contains("GameCondition_MogPresence")), "Tamiu's presence is one of the gods' own, nothing apart");
        var mogCond = conds["Rimfolk_GodPresence_Mog"];
        C(Own(mogCond)?.Element("effect")?.Value == "Watch" && mogCond.Element("label")?.Value == "the cats keeping watch", "Tamiu's: the cats keeping watch");

        // ================= Each god's own way of taking =================

        var worship = defs.SelectMany(d => d.Elements().Where(e => e.Name.LocalName == "ColonyCouncil.GodWorshipDef")).ToList();
        var ways = new Dictionary<string, string>();
        foreach (var k in Fourteen)
        {
            var w = worship.FirstOrDefault(e => e.Element("gods")?.Elements("li").Any(l => l.Value == k) == true);
            string way = w?.Element("taking")?.Value;
            C(Enum.TryParse<OfferingWay>(way ?? "", out var ow) && ow != OfferingWay.Vanish, $"{k} takes what is left for it in its own way ({way})");
            ways[k] = way ?? "";
        }
        C(ways.Values.Distinct().Count() == 14, "every god's way of taking is different");
        C(ways["Helios"] == "Never" && ways["Morel"] == "Rot" && ways["Ducat"] == "Given" && ways["Hollin"] == "Sapling" && ways["Mog"] == "Cat", "Helios's never taken, Morel's rotted, Ducat's given on, Hollin's a sapling, Tamiu's a cat's");
        C(OfferingWayMath.All.Where(x => x != OfferingWay.Vanish).All(x => ways.ContainsValue(x.ToString())), "every way belongs to a god");
        foreach (var w in OfferingWayMath.All)
        {
            foreach (var v in OfferingWayMath.VariantsOf(w))
            {
                string key = OfferingWayMath.Key(w) + "_" + v;
                C(keyed.TryGetValue(key, out var text) && text.Contains("{WHAT}") && text.Contains("{SHRINE}") && !text.Contains("Nobody saw it go"), $"{key}: its own words, never the shared line");
                C(!keyed.TryGetValue(key, out var t2) || (t2.Contains("{EXTRA}") == (w == OfferingWay.Sapling && v != "Bare" || w == OfferingWay.Mended && v == "Felt" || w == OfferingWay.Hunger && v == "Felt" || w == OfferingWay.Given)),
                    $"{key} names the tree, the machine, what else went or the visitor exactly where the code gives one");
            }
            if (w != OfferingWay.Vanish)
                C(keyed.ContainsKey("CC_Wsh_Inspect_Fate_Way_" + w) || keyed.ContainsKey("CC_Wsh_Inspect_Fate_Way_" + w + "_Returned"), $"the inspect line tells what became of a gift to {w}");
        }
        C(keyed.ContainsKey("CC_Wsh_Taken_Taken_Felt") && keyed["CC_Wsh_Taken_Taken_Felt"].Contains("Nobody saw it go"), "the shared way stays for a god whose worship names none");
        C(OfferingWayMath.VariantsOf(OfferingWay.Vanish).Length == 0 && OfferingWayMath.VariantsOf(OfferingWay.Never).SequenceEqual(new[] { "Returned" }), "the shared way has no words of its own; Helios's only its dusk");

        // When each goes.
        C(!OfferingWayMath.TakesNow(OfferingWay.Never, true, 2, 50f, 1f, true) && OfferingWayMath.LetGo(OfferingWay.Never, 19, 6f) && !OfferingWayMath.LetGo(OfferingWay.Never, 12, 6f)
          && !OfferingWayMath.LetGo(OfferingWay.Never, 19, 2f), "Helios's is never taken: still there at dusk on the day it lay in the sun");
        C(OfferingWayMath.TakesNow(OfferingWay.Given, false, 12, 2f, 0f, true) && !OfferingWayMath.TakesNow(OfferingWay.Given, false, 12, 2f, 0f, false) && !OfferingWayMath.TakesNow(OfferingWay.Given, false, 2, 2f, 0f, true)
          && OfferingWayMath.LetGo(OfferingWay.Given, 12, OfferingWayMath.GivenWaitHours) && OfferingWayMath.ReturnedShare(OfferingWay.Given) == 0f,
            "Ducat's goes to a visitor by day, never left lying: back to the stores after three days with nobody, counting for nothing");
        C(!OfferingWayMath.TakesNow(OfferingWay.Moon, false, 23, 10f, 0f, false) && OfferingWayMath.TakesNow(OfferingWay.Moon, false, 23, 10f, 0.4f, false) && !OfferingWayMath.TakesNow(OfferingWay.Moon, false, 12, 10f, 1f, false)
          && OfferingWayMath.LetGo(OfferingWay.Moon, 12, OfferingWayMath.MoonWaitHours), "Selene's only on a night her face shows; it waits out the dark nights, then goes back");
        C(OfferingWayMath.TakesNow(OfferingWay.Frost, false, 6, 8f, 0f, false) && !OfferingWayMath.TakesNow(OfferingWay.Frost, false, 2, 8f, 0f, false)
          && OfferingWayMath.FrostsNow(OfferingWay.Frost, 23, 3f) && !OfferingWayMath.FrostsNow(OfferingWay.Frost, 12, 3f) && !OfferingWayMath.FrostsNow(OfferingWay.Sapling, 23, 3f),
            "Hush's frosts over in the night and goes at dawn");
        C(OfferingWayMath.TakesNow(OfferingWay.Cat, false, 6, 5f, 0f, false) && !OfferingWayMath.TakesNow(OfferingWay.Cat, false, 2, 5f, 0f, false), "Tamiu's at dawn, when the cats come");
        C(OfferingWayMath.TakesNow(OfferingWay.TakenUp, false, 23, 4f, 0f, false) && !OfferingWayMath.TakesNow(OfferingWay.TakenUp, false, 12, 4f, 0f, false), "Cirrus's taken up by night");
        foreach (var w in new[] { OfferingWay.Vanish, OfferingWay.Sapling, OfferingWay.Crack, OfferingWay.Grazed, OfferingWay.Rot, OfferingWay.Guest, OfferingWay.Mended, OfferingWay.Hunger, OfferingWay.Well })
            C(OfferingWayMath.TakesNow(w, false, 3, 5f, 0f, false) == WorshipMath.GodTakesNow(false, 3, 5f) && OfferingWayMath.TakesNow(w, true, 14, 2f, 0f, false) == WorshipMath.GodTakesNow(true, 14, 2f)
              && !OfferingWayMath.LetGo(w, 19, 100f), $"{w} goes in the shared hour of the night (or before everyone's eyes), never back to the stores");
        C(OfferingWayMath.NoonLightNow(OfferingWay.Never, 12) && !OfferingWayMath.NoonLightNow(OfferingWay.Never, 18) && !OfferingWayMath.NoonLightNow(OfferingWay.Moon, 12), "the noon light lies on Helios's");
        C(OfferingWayMath.HungerTakes(1) == 0 && OfferingWayMath.HungerTakes(2) == 1 && OfferingWayMath.HungerTakes(75) == 3 && OfferingWayMath.HungerTakes(1000) == 3, "the Hunger takes a little more, never all of a stack, never a single thing");
        C(OfferingWayMath.NoticedByDay(OfferingWay.Sapling) && !OfferingWayMath.NoticedByDay(OfferingWay.Never) && !OfferingWayMath.NoticedByDay(OfferingWay.Given)
          && OfferingWayMath.SeenGoing("Seen") && OfferingWayMath.SeenGoing("Plain") && !OfferingWayMath.SeenGoing("Felt") && !OfferingWayMath.SeenGoing(null),
            "noticed the next day when it went unseen in the night (round 1's sign), not when someone saw it go");

        // The code.
        string offerings = Read("Gods/Worship/Offerings.cs");
        string waysCs = Read("Gods/Worship/OfferingWays.cs");
        C(offerings.Contains("OfferingWayMath.TakesNow(way, plain, hour, hours,") && offerings.Contains("OfferingWayMath.LetGo(way, hour, hours)") && offerings.Contains("OfferingWays.Before(o, map, g, way, hour, hours)"),
            "each offering waits, shows and goes in its god's own way");
        C(offerings.Contains("OfferingWays.NoticedLine(p, g, mine) ?? Worship.Line(\"Rimfolk_Pool_Worship_Noticed\"") && offerings.Contains("!OfferingWayMath.NoticedByDay(way)"),
            "the next day's notice in the way's own words, the shared ones for the shared way");
        C(offerings.Contains("OfferingWays.Key(way, variant)") && offerings.Contains("if (fate == OfferingFate.Taken && !ownWords) Worship.Glow(map, cell, g);"), "its own words in the news, and its own sign in place of the shared light");
        C(offerings.Contains("GodWorshipDef.GiftsLeft(g)") && offerings.Contains("GodWorshipDef.Believed(g, k)") && Read("Gods/Worship/GodWorshipDef.cs").Contains("For(g)?.taking == OfferingWay.Never"),
            "gifts are left for Helios too (he takes none), chosen by what the colony believes he likes");
        C(Read("Gods/Worship/Shrines.cs").Contains("OfferingWays.FateKey(g, r.lastFate)"), "the inspect line in the way's words");
        foreach (var w in OfferingWayMath.All.Where(x => x != OfferingWay.Vanish && x != OfferingWay.Never))
            C(waysCs.Contains("case OfferingWay." + w + ":"), $"{w} shows on the map where the gift lay");
        C(waysCs.Contains("d.CanEverPlantAt(c, map, true)") && waysCs.Contains("\"SoilRich\"") && waysCs.Contains("Notify_Repaired()") && waysCs.Contains("inventory.innerContainer.TryAdd(thing)")
          && waysCs.Contains("JobDefOf.Ingest") && waysCs.Contains("MogsWays.Cats(map, false)") && waysCs.Contains("OfferingWayMath.HungerTakes(other.stackCount)"),
            "a sapling, black soil, a machine mended, a visitor given it, a cat that eats it, what else the Hunger took: each as said");
        C(Read("Gods/Worship/WorshipBook.cs").Contains("Scribe_Values.Look(ref stage, \"stage\")") && Read("Gods/Worship/WorshipBook.cs").Contains("Scribe_References.Look(ref cat, \"cat\")"),
            "how far its way has gone is kept in the save");
        var wayPool = pools.TryGetValue("Rimfolk_Pool_Worship_Way", out var wp) ? wp : new List<string>();
        foreach (var w in OfferingWayMath.All.Where(x => OfferingWayMath.NoticedByDay(x) && x != OfferingWay.Vanish))
        {
            string tag = OfferingWayMath.Tag(w);
            C(wayPool.Count(l => Tags(l).Split(',').Contains(tag) && Tags(l).Contains("!scoffer")) >= 2 && wayPool.Count(l => Tags(l).Split(',').Contains(tag) && Tags(l).Split(',').Contains("scoffer")) >= 1,
                $"{w}: what a believer sees where it lay, and a scoffer's word");
        }
        C(wayPool.Count(l => Tags(l).Split(',').Contains("way:given") && Tags(l).Split(',').Contains("visitor")) >= 2, "the visitor given Ducat's gift has a word for it");
        C(wayPool.Count > 0 && wayPool.All(Spoken), "every way's line is one short sentence");

        // ================= Bellwether's and Ennu's own signs =================

        var shapes = defs.SelectMany(d => d.Elements().Where(e => e.Name.LocalName == "ColonyCouncil.GodShapeDef")).ToDictionary(e => e.Element("defName")!.Value);
        List<string> OmensOf(string k) => shapes["Rimfolk_GodShape_" + k].Element("omens")?.Elements("li").Select(l => l.Value).ToList() ?? new List<string>();
        C(OmensOf("Bellwether").SequenceEqual(new[] { "ThrumbosFace", "GrassBowed", "Hoofprint" }), "Bellwether's own signs: the thrumbos facing one way, the grass bowed in a line, the hoofprint");
        C(OmensOf("Ennu").SequenceEqual(new[] { "Knock", "Footprints", "DogsSilent" }), "Ennu's own signs: the knock, the footprints in and none away, the dogs silent");
        var shared5 = new[] { "Birds", "Calm", "Dream", "Candle", "Restless" };
        C(!OmensOf("Bellwether").Intersect(shared5).Any() && !OmensOf("Ennu").Intersect(shared5).Any(), "none of the five any god may send");
        string own2 = Read("Gods/OnMap/GodOmensOwn.cs");
        var remarks = pools["Rimfolk_Pool_GodOmenRemark"];
        foreach (var k in new[] { "ThrumbosFace", "GrassBowed", "Hoofprint", "Knock", "Footprints", "DogsSilent" })
        {
            C(Enum.TryParse<OmenKind>(k, out _) && own2.Contains("case OmenKind." + k + ":"), $"{k} shows on the map");
            C(keyed.ContainsKey("CC_GodM_Saw_" + k), $"{k} is kept in the colony's book");
            C(remarks.Count(l => Tags(l) == k.ToLowerInvariant()) >= 2 && remarks.Where(l => Tags(l) == k.ToLowerInvariant()).All(Spoken), $"someone says what they saw of {k}");
        }
        C(GodsOnMapMath.OmenHour(OmenKind.Hoofprint, 3) is >= 6 and <= 7 && GodsOnMapMath.OmenHour(OmenKind.Footprints, 1) is >= 6 and <= 7 && GodsOnMapMath.OmenHour(OmenKind.Knock, 2) >= 18,
            "the prints are found at dawn, the knock comes at dusk");
        C(own2.Contains("case OmenKind.ThrumbosFace: return Thrumbos(map).Count > 0;") && own2.Contains("case OmenKind.DogsSilent: return Dogs(map).Count > 0;")
          && own2.Contains("case OmenKind.Knock: return OuterDoor(map) != null;"), "a sign that needs thrumbos, dogs or a door waits for them");

        // ================= How a god plugs in =================

        C(Read("Gods/GodDef.cs").Contains("public GameConditionDef presence;") && Read("Gods/GodDef.cs").Contains("a god's own presence is a condition whose OwnPresence names this god"),
            "a god plugs in its presence by its def (GodDef.presence), checked when the defs load");
        C(Read("Gods/Worship/GodWorshipDef.cs").Contains("public OfferingWay taking = OfferingWay.Vanish;"), "its way of taking by its worship def (GodWorshipDef.taking), the shared way by default");
        C(Read("Gods/Presence/GodPresenceCondition.cs").Contains("public class OwnPresence : DefModExtension"), "its presence's words and effect on the condition's own def (OwnPresence)");
        return n;
    }

    private static Dictionary<string, HashSet<string>> catalogue;

    private static Dictionary<string, HashSet<string>> Catalogue(string root)
    {
        if (catalogue != null) return catalogue;
        catalogue = new Dictionary<string, HashSet<string>>();
        using var gz = new GZipStream(File.OpenRead(Path.Combine(root, "docs", "player-mods", "defs.tsv.gz")), CompressionMode.Decompress);
        using var r = new StreamReader(gz);
        string line;
        while ((line = r.ReadLine()) != null)
        {
            var t = line.Split('\t');
            if (t.Length < 4 || t[0] == "type" || t[3] != "yes") continue;
            if (!catalogue.TryGetValue(t[1], out var set)) catalogue[t[1]] = set = new HashSet<string>();
            set.Add(t[0]);
        }
        return catalogue;
    }
}
