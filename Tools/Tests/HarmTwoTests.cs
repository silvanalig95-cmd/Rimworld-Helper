using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Harm, parts 2 and 3 (Harm/HarmTwoMath.cs; docs/harm-review-proposals.md 2.4.1, 3.1.1, 2.2.1, 2.1.1, 3.5.1, 1.4.1,
/// 3.3.1): the rates of a camp's brawl, sickness brought home, the champion's bout, hunts that can turn, a rival's
/// sabotage, the hustings' blows and frayed tempers, each set against the review's anchors; and the mod's side: every
/// hurt through the one service, named before the choice, told in two wordings, said in one sentence, nothing new on the
/// clock or the settings, and the saved parts modelled.
/// </summary>
public static class HarmTwoTests
{
    private const int D = HarmTwoMath.DayTicks;

    public static int Run(Action<bool, string> check)
    {
        string root = Root();
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "harm two: " + what);
        }

        bool Near(float a, float b, float eps = 0.0001f) => Math.Abs(a - b) < eps;

        // ================= A camp's quarrel that becomes a brawl (2.4.1) =================

        C(Near(HarmTwoMath.BrawlTension, 70f), "a quarrel can come to blows at the council's second warning (70)");
        C(HarmTwoMath.Quarrelling(70f, -1, 10 * D) && !HarmTwoMath.Quarrelling(69f, -1, 10 * D), "tension 70 or more, not below");
        C(HarmTwoMath.Quarrelling(10f, 10 * D, 12 * D) && !HarmTwoMath.Quarrelling(10f, 10 * D, 13 * D + 1) && !HarmTwoMath.Quarrelling(10f, 10 * D, 9 * D),
            "the old quarrel answered badly: its next three evenings, no more (and not before it)");
        C(HarmTwoMath.Evening(18) && HarmTwoMath.Evening(21) && !HarmTwoMath.Evening(17) && !HarmTwoMath.Evening(22), "at their fire in the evening, six to ten");
        C(Near(HarmTwoMath.CampBrawlChance(false, 1f), 0.25f) && Near(HarmTwoMath.CampBrawlChance(true, 1f), 0.125f), "a quarter an evening, half with the peacemaker at home");
        C(Near(HarmTwoMath.CampBrawlChance(false, 9f), 0.25f * 1.5f) && Near(HarmTwoMath.CampBrawlChance(false, 0.2f), 0.25f), "a frayed temper half again at most, never less");
        // Once a stay: the share of stays with three to five high evenings that come to blows.
        float Stay(int evenings) => 1f - (float)Math.Pow(1f - HarmTwoMath.CampBrawlChance(false, 1f), evenings);
        C(Stay(3) > 0.5f && Stay(3) < 0.65f && Stay(5) < 0.8f, $"about once in a camp's stay once it runs high ({Stay(3):P0} over three evenings, {Stay(5):P0} over five)");
        C(HarmTwoMath.JoinPairs(5, 5) == 2 && HarmTwoMath.JoinPairs(1, 4) == 1 && HarmTwoMath.JoinPairs(0, 4) == 0 && HarmTwoMath.JoinPairs(-1, 3) == 0,
            "a few on each side join: two more pairs at most, as many as both sides have free");
        C(HarmTwoMath.CampBrawlGoodwill < 0 && HarmTwoMath.CampBrawlGoodwill >= -8, "their people remember it as a fistfight: a little goodwill, once");

        // ================= Sickness brought home (3.1.1) =================

        C(Near(HarmTwoMath.SickHome(SickTown.Plague, 8f, false, false, false), 1f / 6f), "one journey in six to a plague town, for a night's stay");
        C(Near(HarmTwoMath.SickHome(SickTown.Plague, 24f, false, false, false), 1f / 6f), "no more for a longer one");
        C(HarmTwoMath.SickHome(SickTown.Plague, 1.9f, false, false, false) == 0f && HarmTwoMath.SickHome(SickTown.Plague, 2f, false, false, false) > 0f,
            "a quick trade at the gate (under two hours) brings nothing home");
        C(Near(HarmTwoMath.SickHome(SickTown.Plague, 4f, false, false, false), 1f / 12f), "half a night, half the chance");
        C(Near(HarmTwoMath.SickHome(SickTown.Plague, 8f, true, false, false), 1f / 12f) && Near(HarmTwoMath.SickHome(SickTown.Plague, 8f, false, true, false), 1f / 12f)
          && Near(HarmTwoMath.SickHome(SickTown.Plague, 8f, false, false, true), 1f / 12f), "medicine packed, modern medicine or the quarantine decree: half each");
        C(Near(HarmTwoMath.SickHome(SickTown.Plague, 8f, true, true, true), 1f / 48f), "all three: an eighth");
        C(Near(HarmTwoMath.SickHome(SickTown.Famine, 8f, false, false, false), 1f / 12f) && HarmTwoMath.SickHome(SickTown.None, 20f, false, false, false) == 0f,
            "a famine's sickness half a plague's, and a well town none");
        C(HarmTwoMath.KindOf("sickness", false) == SickTown.Plague && HarmTwoMath.KindOf("sickness", true) == SickTown.None
          && HarmTwoMath.KindOf("hunger", false) == SickTown.Famine && HarmTwoMath.KindOf("drought", false) == SickTown.Famine && HarmTwoMath.KindOf("war", false) == SickTown.None,
            "the region's world events read as the town's own hardships do: a plague, hunger or drought, never the herds' sickness");
        C(HarmTwoMath.KindOf("sickness", false) == (TownLifeMath.HardshipKind("sickness") == TownLifeMath.Plague ? SickTown.Plague : SickTown.None)
          && Near(HarmTwoMath.ReachTiles, TownLifeMath.HardshipTiles), "the same reach as the town's own hardships (its people's, or twenty tiles)");
        C(HarmTwoMath.Reaches(true, -1f) && HarmTwoMath.Reaches(false, 20f) && !HarmTwoMath.Reaches(false, 21f) && !HarmTwoMath.Reaches(false, -1f), "its people's own, or within twenty tiles");
        C(HarmTwoMath.Worse(SickTown.Famine, SickTown.Plague) == SickTown.Plague && HarmTwoMath.Worse(SickTown.None, SickTown.Famine) == SickTown.Famine, "a plague over a famine");
        C(HarmTwoMath.OnsetTicks(0f) == 2 * D && HarmTwoMath.OnsetTicks(1f) == 4 * D && HarmTwoMath.OnsetTicks(0.5f) == 3 * D, "it shows two to four days after the homecoming");
        C(!HarmTwoMath.OnsetLapsed(10 * D, 13 * D) && HarmTwoMath.OnsetLapsed(10 * D, 13 * D + 1), "held back by the cap, it waits three days, then passes");
        C(HarmTwoMath.StayKept(0, 29 * D) && !HarmTwoMath.StayKept(0, 30 * D), "a stay remembered for the road home (thirty days)");
        C(Near(HarmTwoMath.PlagueShare, 0.25f), "a plague's: the game's plague one time in four, its flu otherwise");

        // ================= The champion's bout (2.2.1) =================

        C(Near(HarmTwoMath.ChampionBlowChance, 1f / 25f) && Near(HarmTwoMath.ChampionBlowChance, HarmMath.DrillBlowChance), "the sparring rule: one bout in 25");
        var forms = Enum.GetValues(typeof(WorldAge)).Cast<WorldAge>().Select(HarmTwoMath.BoutForm).ToList();
        C(forms.Distinct().Count() == 4, "a form for each of the four ages");

        // ================= Hunts that can bite back (2.1.1) =================

        C(HarmTwoMath.MayTurn(false, false, false, 1f, 0.3f, true), "a lone boar can be taken");
        C(!HarmTwoMath.MayTurn(true, false, false, 1f, 0.3f, true), "never a predator");
        C(!HarmTwoMath.MayTurn(false, true, false, 1f, 0.3f, true) && !HarmTwoMath.MayTurn(false, false, true, 1f, 0.3f, true), "never an insect or a dryad");
        C(!HarmTwoMath.MayTurn(false, false, false, 3.5f, 0.3f, true) && HarmTwoMath.MayTurn(false, false, false, 2.5f, 0.3f, true), "nothing bigger than a muffalo or an elk");
        C(!HarmTwoMath.MayTurn(false, false, false, 1f, 0.3f, false), "never one with its herd about it");
        C(!HarmTwoMath.MayTurn(false, false, false, 1f, 0.1f, true) && Near(HarmTwoMath.SafeRevenge, 0.1f), "game at the safe game's revenge chance is just safe game");
        C(Near(HarmTwoMath.MaxGameSize, 2.5f) && Near(HarmTwoMath.PackRadius, 24f) && HarmTwoMath.MaxTurningPerHunt == 1, "the hunts' own size, the game's own pack reach, one head at most");
        C(Near(HarmTwoMath.TurnChance(0.3f, 1f, true), 0.3f), "the game's own chance at the usual scale");
        C(Near(HarmTwoMath.TurnChance(0.3f, 0.5f, true), 0.15f) && Near(HarmTwoMath.TurnChance(0.6f, 2f, true), 1f) && HarmTwoMath.TurnChance(0.3f, 0f, true) == 0f,
            "times the harm scale, never past certainty, nothing at nothing");
        C(HarmTwoMath.TurnChance(0.9f, 1f, false) == 0f, "never on a hunter the cap keeps from a hurt");
        C(HarmTwoMath.TurningKept(0, 5 * D) && !HarmTwoMath.TurningKept(0, 6 * D) && !HarmTwoMath.TurningKept(5 * D, 4 * D), "a marked head remembered six days");

        // ================= A rival's sabotage, found out (3.5.1) =================

        C(HarmTwoMath.SabotageChance(49f, true, true, false) == 0f && HarmTwoMath.SabotageChance(90f, false, true, false) == 0f,
            "only a bitter rivalry, and only a petty or abrasive nature");
        C(Near(HarmTwoMath.SabotageChance(50f, true, true, false), 1f / 30f), "about twice a 60-day year with their work shared");
        C(Near(HarmTwoMath.SabotageChance(50f, true, false, false), 1f / 60f), "once with it kept apart");
        C(Near(HarmTwoMath.SabotageChance(50f, true, true, true), 1f / 60f) && Near(HarmTwoMath.SabotageChance(50f, true, false, true), 1f / 120f), "half again with the peacemaker in office");
        float year = HarmTwoMath.SabotageChance(60f, true, true, false) * 60f, fights = year * HarmTwoMath.FoundOutChance;
        C(year >= 1f && year <= 2f && Near(HarmTwoMath.FoundOutChance, 0.5f), $"once or twice a year a bitter rivalry, found out half the time ({year:0.0} a year, {fights:0.0} come to blows)");
        C(HarmTwoMath.SabotageHour(8) && HarmTwoMath.SabotageHour(19) && !HarmTwoMath.SabotageHour(7) && !HarmTwoMath.SabotageHour(20), "by day");
        C(HarmTwoMath.BenchHitPoints(100, 100) == 75 && HarmTwoMath.BenchHitPoints(40, 100) == 34 && HarmTwoMath.BenchHitPoints(20, 100) == 20 && HarmTwoMath.BenchHitPoints(5, 0) == 5,
            "a bench takes a quarter of its hit points, never below a third, never more than it had");
        C(HarmTwoMath.SabotageForm(WorldAge.Tribal) == "Tribal" && HarmTwoMath.SabotageForm(WorldAge.Spacer) == "Spacer", "by the colony's means");

        // ================= Hustings that come to blows (1.4.1) =================

        C(Near(HarmTwoMath.HustingsBrawl(false, false, false, 1f), 1f / 8f), "one hustings in eight with rivals in the crowd");
        C(Near(HarmTwoMath.HustingsBrawl(true, false, false, 1f), 1f / 16f) && Near(HarmTwoMath.HustingsBrawl(false, true, false, 1f), 1f / 16f)
          && Near(HarmTwoMath.HustingsBrawl(false, false, true, 1f), 1f / 16f), "the peacemaker there, the best-behavior decree, calm candidates: half each");
        C(Near(HarmTwoMath.HustingsBrawl(false, false, false, 1.5f), 3f / 16f) && Near(HarmTwoMath.HustingsBrawl(false, false, false, 3f), 3f / 16f), "a frayed temper half again at most");

        // ================= Frayed tempers (3.3.1) =================

        C(Near(HarmTwoMath.MaxFrayed, DrinkBeforeWorkMath.MaxFightFactor), "at most a beer's factor (the drink before work's)");
        C(HarmTwoMath.FrayedFactor(StrainBand.Steady, 1f) == 1f && HarmTwoMath.FrayedFactor(StrainBand.Strained, 1f) == 1f, "nothing below worn down");
        C(Near(HarmTwoMath.FrayedFactor(StrainBand.Worn, 1f), 1.25f) && Near(HarmTwoMath.FrayedFactor(StrainBand.Breaking, 1f), 1.5f), "worn down a quarter more, near breaking half again");
        C(Near(HarmTwoMath.FrayedFactor(StrainBand.Breaking, 2f), 1.5f) && Near(HarmTwoMath.FrayedFactor(StrainBand.Breaking, 0.5f), 1.25f) && HarmTwoMath.FrayedFactor(StrainBand.Breaking, 0f) == 1f,
            "the harm scale leans it, never past a beer's, nothing at nothing");
        foreach (StrainBand b in Enum.GetValues(typeof(StrainBand)))
            for (float s = 0f; s <= 2f; s += 0.25f)
                C(HarmTwoMath.FrayedFactor(b, s) >= 1f && HarmTwoMath.FrayedFactor(b, s) <= HarmTwoMath.MaxFrayed + 0.0001f, $"the factor stays between 1 and 1.5 ({b}, {s})");
        C(Near(HarmTwoMath.Frayed(1.25f, 1.5f), 1.5f) && Near(HarmTwoMath.Frayed(1f, 1f), 1f) && Near(HarmTwoMath.Frayed(3f, 1f), 1.5f), "the more frayed of the two, not the two together");
        C(HarmTwoMath.FrayedRoll(0.2f, 0.25f, 1.5f, out bool f1) && !f1, "under the old chance: a fight as before, not the strain's");
        C(HarmTwoMath.FrayedRoll(0.3f, 0.25f, 1.5f, out bool f2) && f2, "between the old chance and its half again: the strain's fight");
        C(!HarmTwoMath.FrayedRoll(0.4f, 0.25f, 1.5f, out bool f3) && !f3, "past it: no fight");
        C(HarmTwoMath.FrayedRoll(0.99f, 0.8f, 1.5f, out _) && !HarmTwoMath.FrayedRoll(0.3f, 0.25f, 1f, out _), "never past certainty, and no factor no more");
        var rng = new Random(11);
        int plain = 0, frayed = 0;
        for (int i = 0; i < 20000; i++)
        {
            float r = (float)rng.NextDouble();
            if (r < 0.25f) plain++;
            if (HarmTwoMath.FrayedRoll(r, 0.25f, 1.5f, out _)) frayed++;
        }
        C(Math.Abs(frayed / (float)plain - 1.5f) < 0.05f, $"over many sharp words, half again as many come to blows ({frayed / (float)plain:0.00})");

        // ================= The mod's side =================

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));
        string camp = Code("Harm/CampBrawls.cs"), sick = Code("Harm/SickHome.cs"), champ = Code("Harm/ChampionBouts.cs"), hunt = Code("Harm/HuntsBite.cs"),
            sab = Code("Harm/Sabotage.cs"), hust = Code("Harm/HustingsBrawls.cs"), fray = Code("Harm/FrayedTempers.cs");
        var mine = new Dictionary<string, string>
        {
            ["CampBrawls"] = camp, ["SickHome"] = sick, ["ChampionBouts"] = champ, ["HuntsBite"] = hunt, ["Sabotage"] = sab, ["HustingsBrawls"] = hust, ["FrayedTempers"] = fray
        };
        foreach (var kv in mine)
        {
            C(!Regex.IsMatch(kv.Value, @"TakeDamage\(|AddHediff\(|StartSocialFight\("), $"{kv.Key}: nobody is hurt but through the harm service");
            C(!Regex.IsMatch(kv.Value, @"Sched\.(Rare|Hourly|Every|Slice)"), $"{kv.Key}: no clock of its own (no new Sched slot)");
            C(!Regex.IsMatch(kv.Value, @"Settings\?\.\w+") || kv.Value.Contains("Settings?.socialWeb"), $"{kv.Key}: no setting of its own");
            C(!kv.Value.Contains("—"), $"{kv.Key}: no em dashes");
        }
        C(camp.Contains("Harm.Fight(a, x") && camp.Contains("Harm.Roll(HarmTwoMath.CampBrawlChance") && camp.Contains("Outsiders.QuietBrawl") && camp.Contains("LoseGoodwill(")
          && camp.Contains("Leave(\"brawl\")"), "the camp's brawl: the game's fight under the cap, a brawl their people don't take for an attack, goodwill once, the camp packs up");
        C(camp.Contains("RimfolkPatches.On") && camp.Contains("MapFacts.ThreatOn(map)") && camp.Contains("!p.DevelopmentalStage.Juvenile()"), "the colony's side only with the deeper hooks; never in danger; never a child");
        C(Code("Camps/CampLife.cs").Contains("BrawlHour();") && Code("Camps/Camp.cs").Contains("ExposeBrawl();"), "watched in the camp's hour, saved with the camp");
        C(Code("Diplomacy/FeastBouts.cs").Contains("quiet = true") && Code("Outsiders/OutsiderOutcomes.cs").Contains("if (!b.quiet) BrawlOver(b);"), "a quiet brawl's end is the camp's to tell");
        C(sick.Contains("Harm.Ail(p, def") && sick.Contains("Harm.Roll(HarmTwoMath.SickHome(") && sick.Contains("Harm.May(x.pawn, HurtClass.Injury)") && sick.Contains("Council_Decree_Quarantine")
          && sick.Contains("IsMedicine") && sick.Contains("Council_WS_Murrain"), "sickness brought home: the scale, the cap, the quarantine, medicine packed, never the herds' sickness");
        C(sick.Contains("WorldSituations.Instance") && sick.Contains("HarmTwoMath.Reaches("), "only a town the region's world events really make sick");
        C(champ.Contains("Harm.Roll(HarmTwoMath.ChampionBlowChance)") && champ.Contains("Harm.May(a, HurtClass.Bout) && Harm.May(x, HurtClass.Bout)") && champ.Contains("Outsiders.SparOn"),
            "the champion's bout: one in 25 under the scale and the cap for both, with the outsiders' sparring allowed");
        C(Code("Outsiders/OutsiderOutcomes.cs").Contains("Harm.Note(defender, \"a practice bout's hard blow\")"), "its hard blow counted for the cap, as every spar's");
        C(Code("Happenings/Workers_Challenges.cs").Contains("ChampionBouts.Begin(v, comp)") && Code("Happenings/Workers_Challenges.cs").Contains("CC_Impact_ChampionBout"),
            "a fighting challenge is a bout on the map, named before the choice");
        C(hunt.Contains("[HarmonyPatch(typeof(PawnUtility), nameof(PawnUtility.GetManhunterOnDamageChance)") && hunt.Contains("HarmTwoMath.TurnChance(__result, Harm.Scale, may)")
          && hunt.Contains("Harm.Note(hunter"), "hunts: the game's own revenge chance through the scale and the cap, a turn counted for the cap");
        C(hunt.Contains("RimfolkPatches.On") && hunt.Contains("PlayOuts.On"), "only with answers on the map and the deeper hooks");
        string effects = Code("PlayOut/PlayOutEffects.cs"), groups = Code("PlayOut/PlayOutGroupEffects.cs"), carry = Code("PlayOut/PlayCarry.cs");
        C(effects.Contains("HuntsBite.Describe(sheet") && groups.Contains("HuntsBite.Describe(sheet") && Code("Calendar/CalendarKeeping.cs").Contains("HuntsBite.CalendarLine(map)"),
            "named before the choice: the answer's sheet, the hunting party's, the calendar's letter");
        C(effects.Contains("HuntsBite.Marked(turning)") && groups.Contains("HuntsBite.Marked(turning)"), "a head that can turn is remembered once marked");
        C(carry.Contains("!a.RaceProps.predator") && carry.Contains("public static bool Huntable("), "safe game as before; a hunt under way keeps its head that can turn");
        C(Code("Decrees/DecreeWork.cs").Contains("using (HuntsBite.SafeOnly())") && hunt.Contains("safeOnly == 0"), "the hunting season's morning game keeps to safe game");
        C(sab.Contains("Harm.Fight(other, rival") && sab.Contains("Harm.Roll(chance)") && sab.Contains("HarmTwoMath.FoundOutChance") && sab.Contains("Together.When(") && sab.Contains("Together.WhenInCompany("),
            "sabotage: the scale, found out half the time by a witness or by word going round, had out face to face under the cap");
        C(sab.Contains("DoBreakdown()") && sab.Contains("listerBuildingsRepairable.Notify_BuildingTookDamage"), "a machine jammed by the game's own breakdown, a bench the game's builders mend");
        C(hust.Contains("Harm.Fight(rival, heckler") && hust.Contains("Harm.Roll(chance)") && hust.Contains("Council_Decree_Manners") && hust.Contains("Peacemaker"),
            "the hustings: the game's fight under the cap, the peacemaker and the best-behavior decree ease it");
        C(Code("Offices/OfficesElection.cs").Contains("if (BrawlPause(e, here)) return;") && Code("Offices/OfficesElection.cs").Contains("HeckleTurns(e, race, rec.pawn, who, fans, here)"),
            "the speeches wait round it, and only a heckle can turn");
        C(fray.Contains("Harm.May(a, HurtClass.Fight)") && fray.Contains("Harm.Note(a") && !fray.Contains("MentalBreak") && !fray.Contains("Thought"),
            "frayed tempers: the strain's part under the cap, never the break picker, no mood");
        C(Code("Dilemmas/Dilemmas.cs").Contains("FrayedTempers.Roll(ctx.a, b, chance, out frayed)") && Code("Outsiders/OutsiderOutcomes.cs").Contains("FrayedTempers.Roll(c, x, chance, out frayed)")
          && Code("PlayOut/PlayOuts.cs").Contains("if (frayed && (!Harm.May(a, HurtClass.Fight) || !Harm.May(b, HurtClass.Fight))) return;"),
            "in Rimfolk's own moments: a near fight, hard words, a held risk keeps the cap when it fires");
        C(Code("Heart/HeartView.cs").Contains("FrayedTempers.BandNote(b)"), "the heart's page says so in the band's words");

        // The quarrel answered badly: the shouting at the table, and keeping out of it.
        var camps = XDocument.Load(Path.Combine(root, "Defs", "DilemmaDefs", "Rimfolk_Dilemmas_Camps.xml")).Root!;
        var feud = camps.Elements("ColonyCouncil.DilemmaDef").First(d => d.Element("defName")?.Value == "Rimfolk_DlmC_Feud");
        var opts = feud.Element("options")!.Elements("li").ToList();
        bool Has(XElement list) => list != null && list.Elements("li").Any(li => (string)li.Attribute("Class") == "ColonyCouncil.DilemmaEffect_CampQuarrel");
        C(Has(opts[1].Element("riskEffects")) && Has(opts[3].Element("effects")) && !Has(opts[0].Element("effects")) && !Has(opts[2].Element("effects")) && !Has(opts[1].Element("effects")),
            "the old quarrel can come to blows after the shouting at the table or keeping out of it, never after standing with them, sending them on or making peace");

        // The words: every told message in two wordings, every placeholder named, no em dashes or exclamation marks.
        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_HarmTwo.xml")).Root!.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        foreach (var key in new[] { "CC_HarmTwo_Frayed", "CC_HarmTwo_CampBrawl", "CC_HarmTwo_CampBrawlTheirs", "CC_HarmTwo_ChampionBlow", "CC_HarmTwo_HuntTurned", "CC_HarmTwo_SickHome",
                     "CC_HarmTwo_SabotageUnknown", "CC_HarmTwo_SabotageFight", "CC_HarmTwo_SabotageWords", "CC_HarmTwo_HustingsBrawl" })
            C(keyed.ContainsKey(key) && keyed.ContainsKey(key + "_2"), $"{key} has two wordings or more");
        C(keyed.Values.All(v => !v.Contains("—") && !v.Contains("!")), "no em dashes or exclamation marks");
        C(keyed.Where(kv => kv.Key.StartsWith("CC_Impact_")).All(kv => !Regex.IsMatch(Regex.Replace(kv.Value, @"\{[^}]*\}", ""), @"\d")), "the sheet's lines are words, no numbers");
        foreach (WorldAge age in Enum.GetValues(typeof(WorldAge)))
        {
            C(keyed.ContainsKey("CC_HarmTwo_BoutForm_" + HarmTwoMath.BoutForm(age)), $"the champion's bout has its {age} form");
            C(keyed.ContainsKey("CC_HarmTwo_Sabotage_What_" + HarmTwoMath.SabotageForm(age)), $"a sabotage has its {age} words");
        }
        C(keyed.ContainsKey("CC_Camp_End_brawl"), "the camp's ending after a brawl has its words");
        // Every key the new code asks for is there.
        var allKeys = new HashSet<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml"))
            foreach (var el in XDocument.Load(file).Root!.Elements())
                allKeys.Add(el.Name.LocalName);
        foreach (var kv in mine)
            foreach (Match m in Regex.Matches(kv.Value, "\"(CC_[A-Za-z_]+)\""))
            {
                string k = m.Groups[1].Value;
                if (k.EndsWith("_")) continue;
                C(allKeys.Contains(k), $"{kv.Key}: {k} exists");
            }

        // Said over a head: one short sentence each, and every pool asked for there with lines.
        var pools = new Dictionary<string, List<string>>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Defs", "FlavorPoolDefs"), "*.xml"))
            foreach (var d in XDocument.Load(file).Root!.Elements("ColonyCouncil.FlavorPoolDef"))
                pools[d.Element("defName")!.Value] = d.Descendants("li").Select(li => li.Value).ToList();
        var asked = mine.Values.SelectMany(v => Regex.Matches(v, "\"(Rimfolk_Pool_[A-Za-z]+)\"").Select(m => m.Groups[1].Value)).Distinct().ToList();
        C(asked.Count >= 14, $"the new pools are asked for ({asked.Count})");
        foreach (var pool in asked)
        {
            C(pools.TryGetValue(pool, out var lines) && lines.Count >= 6, $"{pool} exists with six lines or more");
            if (lines == null) continue;
            foreach (var line in lines)
            {
                string body = Regex.Replace(line, @"^\s*\[[^\]]*\]\s*", "").Trim();
                int cut = -1;
                for (int i = 12; i < body.Length; i++)
                    if (body[i] == '.' || body[i] == '!' || body[i] == '?')
                    {
                        cut = i;
                        break;
                    }
                C(cut < 0 || cut == body.Length - 1, $"{pool}: '{body}' is one sentence");
                C(body.Length <= 90 && !body.Contains("!") && !body.Contains("—"), $"{pool}: '{body}' is short, with no exclamation mark or em dash");
            }
        }

        // Saved: the hunts' turning game and the sick towns' stays, each with a save model; the job's def.
        string harm = Code("Harm/Harm.cs");
        C(harm.Contains("Scribe_Collections.Look(ref turning, \"turning\"") && harm.Contains("Scribe_Collections.Look(ref sick, \"sick\", LookMode.Deep)"), "saved in the harm book");
        string model = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        C(model.Contains("\"Harm/Harm.cs:turning\"") && model.Contains("\"Harm/Harm.cs:sick\""), "each with a save model");
        C(File.ReadAllText(Path.Combine(root, "Defs", "Misc", "Rimfolk_HarmTwo.xml")).Contains("ColonyCouncil.JobDriver_Sabotage"), "the rival's job at the bench");
        C(File.ReadAllText(Path.Combine(root, "DESIGN.md")).Contains("### A camp's quarrel that becomes a brawl (2.4.1"), "DESIGN.md has the seven");
        string review = File.ReadAllText(Path.Combine(root, "docs", "harm-review-proposals.md"));
        C(Regex.Matches(review, @"\*Built \(harm, parts 2 and 3").Count == 7, "the review marks the seven built");
        return n;
    }

    private static string Root()
    {
        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        return root;
    }
}
