using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Traits that fit a life (21 November 2026, the player, of Guido, a shotgunner given brawler by Vanilla Social
/// Interactions Expanded: "be a bit more delicate, I guess, with giving brawler"; Persona/TraitGainMath.cs, TraitGains.cs,
/// TraitChanges.cs): which gains are looked at (another mod's during play, never a newcomer's, a gene's, a scenario's, the
/// game's own, Rimfolk's own or the player's), what becomes of them by the setting, who gave a trait read from the calls
/// that led to it, and how someone fights as Rimfolk reads it: by the past year's fights, never the weapon in hand, and
/// never a brawler whose Shooting is near their Melee.
/// </summary>
public static class TraitGainTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "traits that fit: " + what);
        }

        // ---------------- Which gains are looked at ----------------

        var fit = OtherTraitGains.WhereTheyFit;
        C(TraitGainMath.Looks(fit, true, true, true, false, false, false, false), "a trait given to one of the colony during play is looked at");
        C(!TraitGainMath.Looks(OtherTraitGains.AsTheyCome, true, true, true, false, false, false, false), "as they come: nothing is looked at");
        C(!TraitGainMath.Looks(fit, false, true, true, false, false, false, false), "the deeper game hooks off: nothing is looked at");
        C(!TraitGainMath.Looks(fit, true, false, true, false, false, false, false), "not in play (a new game being set up)");
        C(!TraitGainMath.Looks(fit, true, true, false, false, false, false, false), "not someone else's people");
        C(!TraitGainMath.Looks(fit, true, true, true, true, false, false, false), "never a newcomer's traits as they are made");
        C(!TraitGainMath.Looks(fit, true, true, true, false, true, false, false), "never a gene's trait");
        C(!TraitGainMath.Looks(fit, true, true, true, false, false, true, false), "never the scenario's trait");
        C(!TraitGainMath.Looks(fit, true, true, true, false, false, false, true), "never a trait the player gives with a click");
        C(TraitGainMath.Looks(OtherTraitGains.None, true, true, true, false, false, false, false), "none: looked at too");

        // ---------------- Who gave it ----------------

        C(TraitGainMath.Giver(new List<string>(), false) == TraitGiver.Game, "nothing but the game's own calls: the game's (a growth moment, a quest)");
        C(TraitGainMath.Giver(null, false) == TraitGiver.Game, "no calls read: the game's");
        C(TraitGainMath.Giver(new[] { "rimfolk" }, false) == TraitGiver.Rimfolk, "Rimfolk's own changes (trait changes, the gods' and the peoples' gifts)");
        C(TraitGainMath.Giver(new[] { "mod" }, false) == TraitGiver.OtherMod, "another mod's call: another mod's gain");
        C(TraitGainMath.Giver(new[] { "mod" }, true) == TraitGiver.Player, "a click of the player's is theirs, whoever's window ran it (an editor)");
        C(TraitGainMath.Giver(new[] { "rimfolk", "mod" }, false) == TraitGiver.Rimfolk, "the nearest caller decides");

        // ---------------- What becomes of it ----------------

        foreach (TraitGiver g in Enum.GetValues(typeof(TraitGiver)))
        {
            if (g == TraitGiver.OtherMod) continue;
            foreach (OtherTraitGains s in Enum.GetValues(typeof(OtherTraitGains)))
                C(TraitGainMath.Refused(s, g, "unfit") == null, $"{g}'s gain is never refused ({s})");
        }
        C(TraitGainMath.Refused(OtherTraitGains.AsTheyCome, TraitGiver.OtherMod, "unfit") == null, "as they come: another mod's unfit gain stands");
        C(TraitGainMath.Refused(OtherTraitGains.WhereTheyFit, TraitGiver.OtherMod, null) == null, "where they fit: a fitting gain stands");
        C(TraitGainMath.Refused(OtherTraitGains.WhereTheyFit, TraitGiver.OtherMod, "unfit") == "unfit", "where they fit: an unfit gain is refused, with why");
        C(TraitGainMath.Refused(OtherTraitGains.None, TraitGiver.OtherMod, null) == "none", "none: every other mod's gain is refused");
        C(default(OtherTraitGains) == OtherTraitGains.AsTheyCome && Enum.GetNames(typeof(OtherTraitGains)).SequenceEqual(new[] { "AsTheyCome", "WhereTheyFit", "None" }),
            "the setting's three choices");

        // ---------------- How they fight ----------------

        // Guido in the player's save: Shooting 10, Melee 6, a shotgun most of his life.
        C(TraitGainMath.Fights(6, false, 10, false, 0, 6) == "shoots", "Guido (Shooting 10, Melee 6): fights at range");
        C(!TraitGainMath.FitsCloseFighter(TraitGainMath.Fights(6, false, 10, false, 0, 6)), "Guido: another mod's brawler is refused");
        C(TraitGainMath.Fights(6, false, 10, false, 9, 0) == "shoots", "Guido, even after nine close fights: Shooting at least Melee is never a brawler");
        C(TraitGainMath.Fights(10, false, 10, false, 9, 0) == "shoots" && TraitGainMath.Fights(11, false, 10, false, 9, 0) == "shoots", "Melee level with Shooting, or one above: not a brawler");
        C(TraitGainMath.Fights(12, false, 10, false, 4, 1) == null, "Melee two above Shooting, four fights up close and one at range: a close fighter");
        C(TraitGainMath.Fights(14, false, 4, false, 2, 0) == "few", "two fights up close in a year: too few for Rimfolk's own brawler");
        C(TraitGainMath.FitsCloseFighter("few"), "a quiet life with few fights is no reason to refuse another mod's brawler");
        C(TraitGainMath.Fights(14, false, 4, false, 4, 3) == "range", "three fights at range to four up close: more at range than half");
        C(TraitGainMath.Fights(14, false, 4, false, 0, 1) == "range", "one fight at range and none up close: fights at range");
        C(!TraitGainMath.FitsCloseFighter("range") && !TraitGainMath.FitsCloseFighter("shoots") && !TraitGainMath.FitsCloseFighter("melee"), "at range, or no melee: not a close fighter's trait");
        C(TraitGainMath.Fights(14, true, 4, false, 9, 0) == "melee", "who can't fight in melee is no brawler");
        C(TraitGainMath.Fights(5, false, 0, true, 3, 0) == null, "who can't shoot fights up close, by their fights");
        C(TraitGainMath.Fights(12, false, 3, false, 6, 3) == null, "six up close to three at range: twice as many, a close fighter");

        // The year's fights, each kind of day counted within sixty days, not the weapon in hand.
        int day = TraitChangeMath.DayTicks;
        int now = 200 * day;
        var close = new List<int> { now - 5 * day, now - 30 * day, now - 59 * day, now - 61 * day, now - 120 * day };
        var range = new List<int> { now - 1 * day, now - 70 * day, now + day };
        C(TraitGainMath.FightDays(close, range, now, day) == (3, 1), $"the past year's fights: three up close, one at range ({TraitGainMath.FightDays(close, range, now, day)})");
        C(TraitGainMath.FightDays(null, null, now, day) == (0, 0), "no fights counted yet");
        C(TraitGainMath.Close(true, false, false) == false, "a kill with a gun or a bow: at range");
        C(TraitGainMath.Close(false, true, false) == true, "a kill with a sword or a club: up close");
        C(TraitGainMath.Close(false, false, true) == true, "a kill with bare hands: up close");
        C(TraitGainMath.Close(false, false, false) == null, "a death by fire or a turret: not counted");
        C(TraitGainMath.CloseKey != TraitGainMath.RangeKey && TraitGainMath.FoughtWithinDays == 60, "two tallies, a year read");

        // ---------------- The rules that read a trait ----------------

        C(TraitGainMath.RuleReads(TraitChangeKind.Gain, 0, false, 0), "a gain of the trait reads the trait");
        C(!TraitGainMath.RuleReads(TraitChangeKind.Gain, 1, false, 2) && TraitGainMath.RuleReads(TraitChangeKind.Gain, 1, true, 2), "a gain at another degree doesn't, unless at any");
        C(TraitGainMath.RuleReads(TraitChangeKind.StepUp, 0, false, 1) && !TraitGainMath.RuleReads(TraitChangeKind.StepUp, 0, false, -1), "a step up reads the upper degrees");
        C(TraitGainMath.RuleReads(TraitChangeKind.StepDown, 0, false, -2) && !TraitGainMath.RuleReads(TraitChangeKind.StepDown, 0, false, 1), "a step down the lower");
        C(!TraitGainMath.RuleReads(TraitChangeKind.Lose, 0, true, 0), "a way of losing it says nothing of gaining it");

        // ---------------- The defs and the code ----------------

        var character = XDocument.Load(Path.Combine(root, "Defs", "PersonaDefs", "Rimfolk_TraitChanges_Character.xml"));
        var brawler = character.Root!.Elements("ColonyCouncil.TraitChangeDef").FirstOrDefault(d => d.Element("defName")?.Value == "Rimfolk_TC_BrawlerGained");
        C(brawler != null && brawler.Element("meleeFighter")?.Value == "true" && brawler.Element("skills")?.Value == "Melee>=10", "Rimfolk's brawler reads how they fight and asks Melee 10");
        var marks = Directory.GetFiles(Path.Combine(root, "Defs", "PersonaDefs"), "*.xml").SelectMany(f => XDocument.Load(f).Root!.Elements()).Select(e => e.Element("defName")?.Value).Where(x => x != null).ToHashSet();
        foreach (var m in TraitGainMath.FightMarks) C(marks.Contains(m), $"a fight's mark is a real mark ({m})");
        string src = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "Persona", "TraitChanges.cs"));
        int at = src.IndexOf("private static bool MeleeFighter(Pawn p)", StringComparison.Ordinal);
        string body = at < 0 ? "" : src.Substring(at, Math.Min(400, src.Length - at));
        C(at >= 0 && !body.Contains("equipment") && body.Contains("Fights(p, Now) == null"), "Rimfolk's brawler no longer reads the weapon in hand");
        string gains = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "Persona", "TraitGains.cs"));
        C(gains.Contains("[HarmonyPatch(typeof(TraitSet), nameof(TraitSet.GainTrait))]") && gains.Contains("PawnGenerator.IsBeingGenerated") && gains.Contains("sourceGene") && gains.Contains("ScenForced"),
            "caught where every mod gives a trait, past newcomers, genes and the scenario");
        string settings = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "CouncilSettings.cs"));
        string catalog = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "UI", "SettingsCatalog.cs"));
        C(settings.Contains("public OtherTraitGains otherTraitGains = OtherTraitGains.WhereTheyFit;") && settings.Contains("Scribe_Values.Look(ref otherTraitGains, \"otherTraitGains\", OtherTraitGains.WhereTheyFit);"),
            "the setting, its default only where they fit, and saved");
        C(catalog.Contains("Choice(\"otherTraitGains\""), "the setting is drawn");
        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_TraitChanges.xml")).Root!.Elements().Select(e => e.Name.LocalName).ToHashSet();
        foreach (var key in System.Text.RegularExpressions.Regex.Matches(gains + src, "\"(CC_TraitGain_[A-Za-z_]+)\"").Select(m => m.Groups[1].Value).Distinct())
            C(keyed.Contains(key), $"its words are there ({key})");
        foreach (var o in new[] { "AsTheyCome", "WhereTheyFit", "None" })
            C(keyed.Contains("CC_OtherTraitGains_" + o) && keyed.Contains("CC_OtherTraitGains_" + o + "_Tip") && keyed.Contains("CC_SetNote_OtherTraitGains_" + o), $"the choice {o} has its words");
        return n;
    }
}
