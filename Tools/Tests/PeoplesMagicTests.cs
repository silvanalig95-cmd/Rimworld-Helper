using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Magic among the peoples, seen (Source/ColonyCouncil/Peoples/Magic/PeoplesMagic*.cs, PeoplesMagicMath.cs; DESIGN.md "Magic
/// among the peoples, seen"): the rules (the peoples built on magic and the shares' caps, a caster's piece by the age, the
/// helpful spells in peace and who may cast them, the rain's dry spell, what a people is known for, the spells kept out);
/// every name of A RimWorld of Magic it uses (pieces, spells, classes) in the player's own mods, active
/// (docs/player-mods/defs.tsv.gz); every kind's class shares within the caps, sparing but among the peoples built on magic;
/// the words (each class in Keyed, one and many, the card, what a spell did; the pools said over heads, each need and
/// class covered; the letters' asides); and the seams (the gift dresses the caster, the letters' tags, the guard's hooks,
/// the pass's tick, the save's model).
/// </summary>
public static class PeoplesMagicTests
{
    public static int Run(Action<bool, string> check)
    {
        string root = Root();
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "peoples' magic: " + what);
        }

        // ================= The rules =================

        C(PeoplesMagicMath.BuiltOnMagic("seers") && PeoplesMagicMath.BuiltOnMagic("grovekeepers") && PeoplesMagicMath.BuiltOnMagic("listeners")
          && PeoplesMagicMath.BuiltOnMagic("sextons") && !PeoplesMagicMath.BuiltOnMagic("healers") && !PeoplesMagicMath.BuiltOnMagic("traders")
          && !PeoplesMagicMath.BuiltOnMagic(null), "the Conclave, the Mossborn, the Choir and the Bell are built on magic; the order and the league aren't");
        C(PeoplesMagicMath.ShareCap(false, false) < PeoplesMagicMath.ShareCap(true, false) && PeoplesMagicMath.ShareCap(false, true) < PeoplesMagicMath.ShareCap(true, true)
          && PeoplesMagicMath.ShareCap(false, false) <= 0.06f && PeoplesMagicMath.ShareCap(false, true) <= 0.15f && PeoplesMagicMath.ShareCap(true, true) <= PeoplesGearMath.MaxShare,
            "sparing everywhere, more among the peoples built on magic, never past the gifts' own most");
        C(PeoplesMagicMath.ShareFits(0.06f, false, false) && !PeoplesMagicMath.ShareFits(0.07f, false, false) && PeoplesMagicMath.ShareFits(0.15f, true, false)
          && !PeoplesMagicMath.ShareFits(float.NaN, true, true) && !PeoplesMagicMath.ShareFits(-0.1f, true, true), "a kind's shares fit its cap");

        C(PeoplesMagicMath.Garb("Priest", 0) == "TM_DiscipleHood" && PeoplesMagicMath.Garb("Priest", 3) == "TM_DiscipleHood", "a priest's disciple hood in every age");
        C(PeoplesMagicMath.Garb("Paladin", 1) == "RoyalHelm" && PeoplesMagicMath.Garb("Paladin", 2) == "TM_MantleOfProtection",
            "a paladin's Helm of the Valiant over plate, the Mantle of Protection over a later age's armour");
        C(PeoplesMagicMath.Garb("StormBorn", 0) == "TM_LightningWand" && PeoplesMagicMath.Garb("InnerFire", 1) == "TM_FireWand" && PeoplesMagicMath.Garb("HeartOfFrost", 1) == "TM_IceWand"
          && PeoplesMagicMath.Garb("StormBorn", 2) == "ArcaneHood" && PeoplesMagicMath.Garb("HeartOfFrost", 3) == "ArcaneHood",
            "the elemental mages' wands in the old ages, their hood when the age's guns take the wand's place");
        C(PeoplesMagicMath.Garb("TM_Wanderer", 2) == "TM_MaestersCloak" && PeoplesMagicMath.Garb("Druid", 0) == "TM_HoodOfProtection", "the wanderer's cloak, the druid's hood");
        C(PeoplesMagicMath.Garb("Ranger", 0) == null && PeoplesMagicMath.Garb("Technomancer", 3) == null && PeoplesMagicMath.Garb("TM_Commander", 1) == null && PeoplesMagicMath.Garb(null, 0) == null,
            "nothing for a class the mod has no piece of its own for");
        C(PeoplesMagicMath.Wand("TM_FireWand") && !PeoplesMagicMath.Wand("ArcaneHood") && !PeoplesMagicMath.Wand(null), "a wand told from a piece worn");
        C(PeoplesMagicMath.Garbed.All(PeoplesGearMath.MagicClasses.ContainsKey), "every class with a piece is one Rimfolk's peoples know");

        var priest = PeoplesMagicMath.PeaceSpells("Priest");
        C(priest.Count == 2 && priest[0].need == PeoplesMagicMath.Need.Heal && priest[0].spell == "TM_AdvancedHeal" && priest[1].spell == "TM_Purify",
            "a priest heals the hurt, then eases an old scar");
        var druid = PeoplesMagicMath.PeaceSpells("Druid");
        C(druid.Count == 2 && druid[0].need == PeoplesMagicMath.Need.Cure && druid[0].spell == "TM_CureDisease" && druid[1].spell == "TM_Regenerate",
            "a druid cures an infection or the flu first, else heals");
        C(PeoplesMagicMath.PeaceSpells("Paladin").Single().spell == "TM_Heal" && PeoplesMagicMath.PeaceSpells("HeartOfFrost").Single().need == PeoplesMagicMath.Need.Rain,
            "a paladin heals, an ice mage calls the rain");
        C(PeoplesMagicMath.PeaceSpells("Ranger").Count == 0 && PeoplesMagicMath.PeaceSpells("Enchanter").Count == 0 && PeoplesMagicMath.PeaceSpells("InnerFire").Count == 0
          && PeoplesMagicMath.PeaceSpells(null).Count == 0 && !PeoplesMagicMath.Helps("StormBorn") && PeoplesMagicMath.Helps("Druid"),
            "no spell in peace for the classes whose workings harm or last only while their maker keeps them");
        C(PeoplesMagicMath.AllPeaceSpells.All(s => !PeoplesMagicMath.IsKeptOut(s)), "no spell for peace is kept out");
        C(PeoplesMagicMath.BaseSpell("TM_Heal_II") == "TM_Heal" && PeoplesMagicMath.BaseSpell("TM_AdvancedHeal_III") == "TM_AdvancedHeal" && PeoplesMagicMath.BaseSpell("TM_Heal_I") == "TM_Heal"
          && PeoplesMagicMath.BaseSpell("TM_Heal") == "TM_Heal" && PeoplesMagicMath.BaseSpell(null) == null, "an ability's levels read as the ability");

        C(PeoplesMagicMath.MayCastInPeace(false, false, 0.6f, false, false, false, false), "a content visitor may cast");
        C(PeoplesMagicMath.MayCastInPeace(false, true, 0.1f, false, false, false, false) && PeoplesMagicMath.MayCastInPeace(false, true, float.NaN, false, false, false, false),
            "an ally's visitor may, whatever their mood");
        C(!PeoplesMagicMath.MayCastInPeace(false, false, 0.3f, false, false, false, false) && !PeoplesMagicMath.MayCastInPeace(false, false, float.NaN, false, false, false, false),
            "an unhappy visitor of a people not allied doesn't");
        C(!PeoplesMagicMath.MayCastInPeace(true, true, 1f, false, false, false, false) && !PeoplesMagicMath.MayCastInPeace(false, true, 1f, true, false, false, false)
          && !PeoplesMagicMath.MayCastInPeace(false, true, 1f, false, true, false, false) && !PeoplesMagicMath.MayCastInPeace(false, true, 1f, false, false, true, false)
          && !PeoplesMagicMath.MayCastInPeace(false, true, 1f, false, false, false, true),
            "never a hostile, never twice a visit, never with danger on the map, never one busy or unable, never one leaving");
        C(PeoplesMagicMath.PeaceChance(true) > PeoplesMagicMath.PeaceChance(false) && PeoplesMagicMath.PeaceChance(false) > 0f && PeoplesMagicMath.PeaceChance(true) <= 0.1f,
            "rare: a few in a hundred an hour, a little more among the peoples built on magic");
        C(PeoplesMagicMath.HurtEnough(4f, false) && PeoplesMagicMath.HurtEnough(0f, true) && !PeoplesMagicMath.HurtEnough(2f, false) && !PeoplesMagicMath.HurtEnough(float.NaN, false),
            "hurt enough for a healing: bleeding, or wounds adding up");
        C(PeoplesMagicMath.CuredAtFirst("WoundInfection") && PeoplesMagicMath.CuredAtFirst("Flu") && PeoplesMagicMath.CuredAtFirst("Animal_Flu") && PeoplesMagicMath.CuredAtFirst("LungInfection")
          && !PeoplesMagicMath.CuredAtFirst("Plague") && !PeoplesMagicMath.CuredAtFirst("Malaria") && !PeoplesMagicMath.CuredAtFirst(null),
            "the druid's cure at the mod's first level: infections and the flus, not the plague or malaria");
        C(PeoplesMagicMath.RainWanted(false, 20f, false, false, true) && PeoplesMagicMath.RainWanted(false, 35f, true, false, false) && PeoplesMagicMath.RainWanted(false, 10f, false, true, false),
            "rain wanted in a dry spell, a heat wave or a fire");
        C(!PeoplesMagicMath.RainWanted(true, 20f, true, true, true) && !PeoplesMagicMath.RainWanted(false, 0f, true, true, true) && !PeoplesMagicMath.RainWanted(false, 20f, false, false, false)
          && !PeoplesMagicMath.RainWanted(false, float.NaN, true, true, true), "not while it rains, not below freezing (it would snow), not when the land isn't dry");
        C(PeoplesMagicMath.DrySpell == "dry:7", "a dry spell is a week with nothing fallen, as the weather record reads it");

        var among = PeoplesMagicMath.Among(new List<(string, float)> { ("Ranger", 0.06f), ("Druid", 0.04f), ("Druid", 0.05f), ("Gladiator", 0.03f), ("Priest", float.NaN), (null, 0.5f), ("Monk", -1f) });
        C(among.SequenceEqual(new[] { "Druid", "Ranger", "Gladiator" }), "a people is known for its commonest crafts, summed over its kinds: " + string.Join(",", among));
        C(PeoplesMagicMath.Among(new List<(string, float)> { ("B", 0.05f), ("A", 0.05f), ("C", 0.01f), ("D", 0.02f) }, 2).SequenceEqual(new[] { "A", "B" }),
            "ties by name, and no more than asked");
        C(PeoplesMagicMath.Among(null).Count == 0, "nothing from nothing");

        C(PeoplesMagicMath.IsKeptOut("TM_Firestorm") && PeoplesMagicMath.IsKeptOut("TM_Resurrection") && PeoplesMagicMath.IsKeptOut("TM_Headshot") && PeoplesMagicMath.IsKeptOut("TM_Meteor")
          && !PeoplesMagicMath.IsKeptOut("TM_Firebolt") && !PeoplesMagicMath.IsKeptOut("TM_Heal") && !PeoplesMagicMath.IsKeptOut(null),
            "the masters' spells and the outright kills kept out, the rest left to the mod's AI");
        C(PeoplesMagicMath.KeptOut.Count >= 20, $"the list kept out ({PeoplesMagicMath.KeptOut.Count})");
        foreach (var kv in PeoplesMagicMath.KeptOut)
            C(kv.Value.Length > 10 && !kv.Value.Contains('—') && !kv.Value.Contains('!'), $"{kv.Key}: why it is kept out, in plain words");

        // ================= Every name in the player's own mods =================

        var defs = Catalogue(root);
        bool Rom(string type, string name) => defs.Any(r => r.type == type && r.def == name && r.mod == "torann.arimworldofmagic" && r.active);
        foreach (string piece in PeoplesMagicMath.AllGarb)
            C(Rom("ThingDef", piece), $"{piece} is A RimWorld of Magic's own, in the player's active mods");
        var apparel = File.ReadAllLines(Path.Combine(root, "docs", "player-mods", "apparel.tsv")).Select(l => l.Split('\t')[0]).ToHashSet();
        var weapons = File.ReadAllLines(Path.Combine(root, "docs", "player-mods", "weapons.tsv")).Select(l => l.Split('\t')[0]).ToHashSet();
        foreach (string piece in PeoplesMagicMath.AllGarb)
            C(PeoplesMagicMath.Wand(piece) ? weapons.Contains(piece) : apparel.Contains(piece), $"{piece} is {(PeoplesMagicMath.Wand(piece) ? "a weapon" : "apparel")} in the catalogue");
        foreach (string spell in PeoplesMagicMath.AllPeaceSpells.Concat(PeoplesMagicMath.KeptOut.Keys))
            C(Rom("TMAbilityDef", spell), $"{spell} is one of A RimWorld of Magic's abilities, in the player's active mods");
        foreach (string trait in PeoplesGearMath.MagicClasses.Keys)
            C(Rom("TraitDef", trait), $"the class {trait} is A RimWorld of Magic's, in the player's active mods");

        // ================= The shares, against each people's lore =================

        var peopleDefs = Directory.GetFiles(Path.Combine(root, "Defs", "PeopleDefs"), "Rimfolk_People_*.xml").Select(f => XDocument.Load(f)).ToList();
        var callings = new HashSet<string>();
        int kinds = 0;
        foreach (var doc in peopleDefs)
        {
            string calling = doc.Descendants("calling").FirstOrDefault()?.Value.Trim();
            if (calling != null) callings.Add(calling);
            bool magic = PeoplesMagicMath.BuiltOnMagic(calling);
            float most = 0f;
            foreach (var kind in doc.Root!.Elements("PawnKindDef"))
            {
                string name = kind.Element("defName")?.Value;
                var gifts = kind.Descendants("li").Where(li => li.Attribute("Class")?.Value == "ColonyCouncil.PeopleKindExtension").SelectMany(e => e.Element("gifts")?.Elements("li") ?? Enumerable.Empty<XElement>()).ToList();
                if (name == null || gifts.Count == 0) continue;
                bool leader = kind.Element("factionLeader")?.Value == "true";
                float sum = gifts.Where(g => g.Element("trait") != null).Sum(g => float.Parse(g.Element("chance")?.Value ?? "0", CultureInfo.InvariantCulture));
                if (sum <= 0f) continue;
                kinds++;
                if (!leader) most = Math.Max(most, sum);
                C(PeoplesMagicMath.ShareFits(sum, magic, leader), $"{name} ({calling}{(leader ? ", leader" : "")}): its class shares {sum:0.00} within {PeoplesMagicMath.ShareCap(magic, leader):0.00}");
            }
            if (magic) C(most > PeoplesMagicMath.SpareShare, $"the {calling}, built on magic, have more casters than the sparing peoples ({most:0.00})");
        }
        C(kinds >= 60, $"the kinds with a class read ({kinds})");
        foreach (string c in PeoplesMagicMath.MagicCallings) C(callings.Contains(c), $"{c} is one of the peoples' callings");

        // ================= The words =================

        var keyed = new Dictionary<string, string>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml"))
            foreach (var el in XDocument.Load(file).Root!.Elements())
                keyed[el.Name.LocalName] = el.Value;
        foreach (string trait in PeoplesGearMath.MagicClasses.Keys)
        {
            C(keyed.TryGetValue("CC_PeoplesMagic_Craft_" + trait, out var one) && one.Length > 0 && one == one.ToLowerInvariant(), $"{trait} in a word, lower case");
            C(keyed.TryGetValue("CC_PeoplesMagic_Crafts_" + trait, out var many) && many.EndsWith("s"), $"{trait}: many of them");
        }
        C(keyed.TryGetValue("CC_PeoplesMagic_Card_Few", out var few) && few.Contains("{CRAFTS}") && keyed.TryGetValue("CC_PeoplesMagic_Card_Many", out var lots) && lots.Contains("{CRAFTS}"),
            "the card says what crafts are among them");
        foreach (var k in new[] { "Heal", "Cure", "CureTried", "Scar", "Rain" })
        {
            C(keyed.TryGetValue("CC_PeoplesMagic_Did_" + k, out var did) && did.Contains("{CASTER_nameDef}") && did.Contains("{ACRAFT}") && did.Contains("{PEOPLE}")
              && !did.Contains('—') && !did.Contains('!'), $"what a spell in peace did ({k}) names who cast it, their craft and their people");
            if (k != "Rain") C(did != null && did.Contains("{TARGET_nameDef}"), $"{k}: and whom it was for");
            if (k.StartsWith("Cure")) C(did != null && did.Contains("{ILLNESS}"), $"{k}: and the illness");
        }
        C(keyed.ContainsKey("CC_Clock_Pass_RarePeoplesMagic"), "the clock names the pass");

        var pools = new Dictionary<string, List<string>>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories))
        {
            XDocument doc;
            try
            {
                doc = XDocument.Load(file);
            }
            catch
            {
                continue;
            }
            foreach (var p in doc.Root!.Elements("ColonyCouncil.FlavorPoolDef"))
            {
                string dn = p.Element("defName")?.Value ?? "";
                pools[dn] = p.Descendants("li").Select(li => li.Value).ToList();
            }
        }
        var tag = new Regex(@"^\[([^\]]*)\]\s*");
        List<string> Tags(string line)
        {
            var m = tag.Match(line);
            return m.Success ? m.Groups[1].Value.Split(',').SelectMany(t => t.Split('|')).Select(t => t.Trim()).ToList() : new List<string>();
        }
        foreach (string pool in new[] { "Rimfolk_Pool_MagicName", "Rimfolk_Pool_MagicSeen", "Rimfolk_Pool_MagicFoe", "Rimfolk_Pool_MagicCast", "Rimfolk_Pool_MagicThanks" })
        {
            C(pools.TryGetValue(pool, out var lines) && lines.Count >= 5, $"{pool} has lines");
            if (lines == null) continue;
            C(lines.Any(l => Tags(l).Count == 0), $"{pool}: a line for anyone, so every class has one");
            foreach (var l in lines)
                foreach (var t in Tags(l))
                {
                    if (t.StartsWith("cl:")) C(PeoplesGearMath.MagicClasses.ContainsKey(t.Substring(3)), $"{pool}: {t} names a class");
                    else C(t.StartsWith("need:") && new[] { "heal", "cure", "scar", "rain" }.Contains(t.Substring(5)), $"{pool}: {t} is a class or a need");
                }
        }
        foreach (string need in new[] { "heal", "cure", "scar", "rain" })
        {
            C(pools.TryGetValue("Rimfolk_Pool_MagicCast", out var cast) && cast.Any(l => Tags(l).Contains("need:" + need)), $"the caster has a word after a spell for {need}");
            C(pools.TryGetValue("Rimfolk_Pool_MagicThanks", out var thanks) && thanks.Any(l => Tags(l).Contains("need:" + need)), $"and the one it was for answers ({need})");
        }
        foreach (string trait in PeoplesMagicMath.Garbed)
            C(pools.TryGetValue("Rimfolk_Pool_MagicName", out var name) && name.Any(l => Tags(l).Contains("cl:" + trait)), $"a {trait} names their craft in words of their own");
        C(pools.TryGetValue("Rimfolk_Pool_LetterAside", out var aside) && aside.Count(l => Tags(l).Contains("among:magic") && l.Contains("{AMONG}")) >= 4
          && aside.Any(l => Tags(l).Contains("among:magic") && Tags(l).Contains("olde")), "their letters mention the crafts among them now and then, the old speech too");

        // ================= The seams =================

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string gifts2 = File.ReadAllText(Path.Combine(src, "Peoples", "PeopleGifts.cs"));
        C(gifts2.Contains("PeoplesMagic.Dress(p, age)"), "a new pawn of the peoples with a class is dressed as one");
        C(File.ReadAllText(Path.Combine(src, "Letters", "LetterForm.cs")).Contains("PeoplesMagic.LetterTags(f, s.tags, s.values)"), "their letters know the crafts among them");
        string patches = File.ReadAllText(Path.Combine(src, "Peoples", "Magic", "PeoplesMagicPatches.cs"));
        C(Regex.Matches(patches, @"\[HarmonyPatch\]").Count == 2 && Regex.Matches(patches, @"RimfolkPatches\.On").Count == 2 && patches.Contains("\"HasResourcesForAbility\"")
          && patches.Contains("\"CanCastPowerCheck\""), "the guard sits on the mod's own checks before a cast, off with the deeper hooks");
        string watch = File.ReadAllText(Path.Combine(src, "Peoples", "Magic", "PeoplesMagicWatch.cs"));
        C(watch.Contains("Sched.RarePeoplesMagic") && Sched.RareOffsets.Contains(Sched.RarePeoplesMagic)
          && PassTable.All.Any(p => p.pass == Pass.RarePeoplesMagic && p.offset == Sched.RarePeoplesMagic && p.period == Sched.RareTicks), "a 250-tick slot of its own, timed");
        C(!Regex.IsMatch(watch, @"MoteMaker\.ThrowText|Log\.Message"), "said only through Speech and the outsiders' scenes");
        C(watch.Contains("TryTakeOrderedJob(job, JobTag.Misc)") && File.ReadAllText(Path.Combine(src, "Peoples", "Magic", "PeoplesMagic.cs")).Contains("\"GetJob\""),
            "cast by the mod's own job, as the mod casts for those not of the colony");
        string save = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        C(save.Contains("Peoples/Magic/PeoplesMagicWatch.cs:cast"), "the saved list has a model in the save estimate");

        return n;
    }

    private static List<(string type, string def, string mod, bool active)> catalogue;

    private static List<(string type, string def, string mod, bool active)> Catalogue(string root)
    {
        if (catalogue != null) return catalogue;
        catalogue = new List<(string, string, string, bool)>();
        using var fs = File.OpenRead(Path.Combine(root, "docs", "player-mods", "defs.tsv.gz"));
        using var gz = new System.IO.Compression.GZipStream(fs, System.IO.Compression.CompressionMode.Decompress);
        using var rd = new StreamReader(gz);
        rd.ReadLine();
        string line;
        while ((line = rd.ReadLine()) != null)
        {
            var c = line.Split('\t');
            if (c.Length >= 4) catalogue.Add((c[0], c[1], c[2], c[3] == "yes"));
        }
        return catalogue;
    }

    private static string Root()
    {
        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        return root;
    }
}
