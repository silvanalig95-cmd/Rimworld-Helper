using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Hurts from everyday life (Harm/HarmMath.cs; docs/harm-review-proposals.md, "Rules every proposal keeps"): the setting
/// and the storyteller's lean, the shared cap, a wound that never takes a part off, and the three hurts of work at the
/// review's rates (drills that bruise, tired hands, working through it); and the mod's side: every hurt through the one
/// service, the decrees that name what they can do, the setting, the clock's slot, the words.
/// </summary>
public static class HarmTests
{
    private const int D = HarmMath.DayTicks;

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "hurts from everyday life: " + what);
        }

        bool Near(float a, float b, float eps = 0.0001f) => Math.Abs(a - b) < eps;

        // ---------------- The setting and the storyteller ----------------

        C(Near(HarmMath.Lean(1f, 1f), 1f), "the usual storyteller leans nothing");
        C(HarmMath.Lean(0f, 2f) < 0.45f && HarmMath.Lean(0f, 2f) > 0.35f, $"no threats and diseases half as often, about two fifths ({HarmMath.Lean(0f, 2f):0.00})");
        C(HarmMath.Lean(0.1f, 1.5f) < 0.75f, "a gentle storyteller gentler");
        C(HarmMath.Lean(2.2f, 0.8f) <= 1.25f + 0.0001f && HarmMath.Lean(2.2f, 0.8f) > 1.1f, "the hardest about a quarter more, never past it");
        C(Near(HarmMath.Lean(1f, HarmMath.NoDiseases), HarmMath.Lean(1f, 2f)), "diseases off count as half as many");
        for (float t = 0f; t <= 3f; t += 0.25f)
        for (float dz = 0.5f; dz <= 120f; dz *= 2f)
        {
            float l = HarmMath.Lean(t, dz);
            C(l >= 0.25f && l <= 1.25f + 0.0001f, $"the lean stays between a quarter and a quarter more (threat {t}, disease {dz})");
        }
        C(HarmMath.Lean(0.5f, 1f) < HarmMath.Lean(1f, 1f) && HarmMath.Lean(1f, 2f) < HarmMath.Lean(1f, 1f), "fewer threats or rarer diseases, gentler");
        C(Near(HarmMath.Scale(0f, 1f, 1f), 0f) && Near(HarmMath.Scale(2f, 1f, 1f), 2f) && Near(HarmMath.Scale(5f, 1f, 1f), 2f) && Near(HarmMath.Scale(-1f, 1f, 1f), 0f),
            "the setting runs from nothing to twice, no further");
        C(Near(HarmMath.Scale(1f, 0f, 2f), HarmMath.Lean(0f, 2f)), "the scale is the setting times the lean");
        C(Near(HarmMath.Chance(0.04f, 1f), 0.04f) && Near(HarmMath.Chance(0.04f, 2f), 0.08f) && Near(HarmMath.Chance(0.9f, 2f), 1f) && Near(HarmMath.Chance(0.5f, -1f), 0f),
            "every rate multiplied by the scale, never past certainty");
        C(!HarmMath.Roll(0f, 0.04f, 0f) && HarmMath.Roll(0.039f, 0.04f, 1f) && !HarmMath.Roll(0.041f, 0.04f, 1f), "a roll comes up under the scaled chance");

        // ---------------- The shared cap ----------------

        const int now = 100 * D;
        HurtBar B(bool baby = false, bool child = false, bool downed = false, bool sick = false, bool preg = false, bool fight = false, int last = HarmMath.Never,
            HurtClass c = HurtClass.Injury, float scale = 1f) =>
            HarmMath.Bar(scale, baby, child, downed, sick, preg, fight, last, now, c);
        C(B() == HurtBar.None, "a grown-up up and about may be hurt");
        C(B(scale: 0f) == HurtBar.Off, "at nothing, nobody");
        C(B(baby: true, c: HurtClass.Mild) == HurtBar.Baby, "babies never, not even the mildest");
        C(B(child: true, c: HurtClass.Mild) == HurtBar.None, "children the mildest");
        foreach (var c in new[] { HurtClass.Injury, HurtClass.Bout, HurtClass.Fight })
            C(B(child: true, c: c) == HurtBar.Child, $"children never a {c}");
        C(B(downed: true) == HurtBar.Downed && B(sick: true) == HurtBar.Sickbed && B(preg: true) == HurtBar.Pregnant && B(fight: true) == HurtBar.Fighting,
            "none while downed, in a sickbed, pregnant or already fighting");
        C(B(last: now - HarmMath.GapTicks + 1) == HurtBar.Lately && B(last: now - HarmMath.GapTicks) == HurtBar.None, "one in three days, and again after");
        C(B(last: now + 5) == HurtBar.None, "a hurt in the future (a save played on) counts as long ago");
        C(Near(HarmMath.GapDays, 3f) && HarmMath.GapTicks == 3 * D && HarmMath.FreshHarmTicks == HarmMath.HourTicks, "three days between; hurt within the hour is fighting");
        C(!HarmMath.Lately(HarmMath.Never, now), "never hurt is never lately");
        // Over a long run, a colonist rolled every hour at certainty is hurt at most once in three days.
        int last = HarmMath.Never, hurts = 0;
        for (int t = 0; t < 60 * D; t += HarmMath.HourTicks)
            if (HarmMath.Bar(1f, false, false, false, false, false, false, last, t, HarmMath.ClassFits(false, HurtClass.Injury) ? HurtClass.Injury : HurtClass.Mild) == HurtBar.None)
            {
                hurts++;
                last = t;
            }
        C(hurts == 20, $"sixty days at certainty: twenty hurts at most ({hurts})");

        // ---------------- The wound ----------------

        C(Near(HarmMath.Severity(6f, 9f, 0f, 1f), 6f) && Near(HarmMath.Severity(6f, 9f, 1f, 1f), 9f) && Near(HarmMath.Severity(6f, 9f, 0.5f, 0.5f), 3.75f),
            "a wound's size between its ends, the tough taking half");
        C(Near(HarmMath.Severity(6f, 9f, 2f, 1f), 9f) && Near(HarmMath.Severity(6f, 9f, -1f, 1f), 6f), "the roll held in its range");
        C(Near(HarmMath.SafeSeverity(9f, 30f), 9f) && Near(HarmMath.SafeSeverity(9f, 5f), 4f) && Near(HarmMath.SafeSeverity(9f, 0.5f), 0f),
            "never more than a part has left but one point: nothing taken off, nothing killed");
        C(HarmMath.SmallestWound > 0f && HarmMath.SmallestWound < 1f, "too small to bother with, nothing given");

        // ---------------- Drills that bruise (1.2.1) ----------------

        C(Near(HarmMath.DrillBlowChance, 1f / 25f) && Near(HarmMath.DrillBlow(true), 1f / 50f), "one bout in 25, a marshal halves it");
        C(Near(HarmMath.DrillBlowChance, OutsiderMath.SparHardBlowChance(0, 0)) && Near(HarmMath.DrillBlowChance, FeastMath.HardBlowChance), "the same rate as the outsiders' sparring and the feasts' wrestling");
        C(Near(HarmMath.DrillLow, 6f) && Near(HarmMath.DrillHigh, 9f) && Near(OutsiderMath.SparDamage(0f, true), HarmMath.DrillLow) && Near(OutsiderMath.SparDamage(1f, true), HarmMath.DrillHigh),
            "a bruise or a cut of 6 to 9, the sparring's real blow");
        C(HarmMath.DrillPairs(10, false) == 5 && HarmMath.DrillPairs(11, false) == 5 && HarmMath.DrillPairs(1, false) == 0 && HarmMath.DrillPairs(10, true) == 1 && HarmMath.DrillPairs(1, true) == 0,
            "every two who can fight at the drills; one pair at the muster");
        C(!HarmMath.DrillHour(16) && HarmMath.DrillHour(17) && HarmMath.DrillHour(19) && !HarmMath.DrillHour(20), "the drill is in the evening");
        int musters = Enumerable.Range(0, 40).Count(HarmMath.MusterEvening);
        C(musters == 30 && HarmMath.MusterEvening(-1) == HarmMath.MusterEvening(3), $"the militia musters three evenings in four ({musters} of 40)");
        float drills = HarmMath.DrillBlowsPerDecree(10, 5f, false, false);
        C(drills > 0.9f && drills < 1.1f, $"about one real injury per five-day drills decree in a colony of ten ({drills:0.00})");
        float muster = HarmMath.DrillBlowsPerDecree(10, 12f, true, false);
        C(muster > 0.3f && muster < 0.6f && muster < drills, $"the gentler militia about half that ({muster:0.00})");
        C(HarmMath.DrillBlowsPerDecree(10, 5f, false, true) < drills * 0.51f, "a marshal halves it");
        C(HarmMath.DrillSwings == 6 && HarmMath.DrillSwingTicks > 60, "six pulled swings each");

        // ---------------- Tired hands (1.2.2) ----------------

        C(Near(HarmMath.TiredHandsChance, 1f / 150f) && Near(HarmMath.LongDayHours, 6f) && Near(HarmMath.LowRest, 0.3f), "one in 150 long days of six hours");
        C(Near(HarmMath.TiredHands(1, false, false, false), 1f / 150f), "one work decree, rested: one in 150");
        C(Near(HarmMath.TiredHands(1, true, false, false), 2f / 150f), "twice below 30% rest");
        C(Near(HarmMath.TiredHands(2, false, false, false), 2f / 150f) && Near(HarmMath.TiredHands(3, false, false, false), 2f / 150f), "two work decrees at once, twice; no more");
        C(Near(HarmMath.TiredHands(1, false, true, false), 0.5f / 150f), "the tough half as often");
        C(Near(HarmMath.TiredHands(1, false, false, true), 0.5f / 150f) && HarmMath.WorkHighFor(true) > HarmMath.WorkHighFor(false), "a spacer colony rarer and worse");
        C(Near(HarmMath.TiredHands(0, true, false, false), 0f), "no work decree, no tired hands");
        C(Near(HarmMath.WorkLow, 3f) && Near(HarmMath.WorkHigh, 6f), "a real wound of 3 to 6");
        float decree = HarmMath.TiredHandsPerDecree(10, 5f, 0f);
        C(decree > 0.3f && decree < 0.4f, $"a five-day decree in a colony of ten, all rested: a third of a slip ({decree:0.00})");
        C(HarmMath.TiredHandsPerDecree(10, 5f, 1f) > decree * 1.9f, "about twice that when nobody sleeps enough");
        C(HarmMath.WorkOf("Cooking", false, false) == WorkHurtKind.Hot && HarmMath.WorkOf("Smithing", false, false) == WorkHurtKind.Hot
          && HarmMath.WorkOf("Crafting", false, false) == WorkHurtKind.Bench && HarmMath.WorkOf("Construction", false, false) == WorkHurtKind.Heavy
          && HarmMath.WorkOf(null, true, false) == WorkHurtKind.Heavy && HarmMath.WorkOf("Hauling", false, false) == WorkHurtKind.Load
          && HarmMath.WorkOf(null, false, true) == WorkHurtKind.Load && HarmMath.WorkOf("Growing", false, false) == WorkHurtKind.Field
          && HarmMath.WorkOf("Research", false, false) == WorkHurtKind.None && HarmMath.WorkOf("Doctor", false, false) == WorkHurtKind.None,
            "the work a job is: the stove and the forge, the bench, building and mining, hauling, the field; never research or tending");
        C(HarmMath.Drives(new WorkHurtKind[0], WorkHurtKind.Bench) && HarmMath.Drives(null, WorkHurtKind.Field) && !HarmMath.Drives(null, WorkHurtKind.None)
          && HarmMath.Drives(new[] { WorkHurtKind.Field }, WorkHurtKind.Field) && !HarmMath.Drives(new[] { WorkHurtKind.Field }, WorkHurtKind.Bench),
            "long hours drive all work, a harvest push the field, a building season the building");
        foreach (var age in AgesMath.All)
        foreach (WorkHurtKind w in Enum.GetValues(typeof(WorkHurtKind)))
        {
            if (w == WorkHurtKind.None) continue;
            HarmMath.WorkSlip(w, age, out var kind, out var where);
            C(kind != HurtKind.Crack && where != HurtWhere.Torso, $"a slip at {w} in the {age} age is a wound of the hands or feet, never a crack");
        }
        HarmMath.WorkSlip(WorkHurtKind.Bench, WorldAge.Tribal, out var k1, out _);
        HarmMath.WorkSlip(WorkHurtKind.Bench, WorldAge.Industrial, out var k2, out _);
        HarmMath.WorkSlip(WorkHurtKind.Bench, WorldAge.Spacer, out var k3, out _);
        HarmMath.WorkSlip(WorkHurtKind.Hot, WorldAge.Medieval, out var k4, out _);
        HarmMath.WorkSlip(WorkHurtKind.Load, WorldAge.Medieval, out var k5, out var w5);
        C(k1 == HurtKind.Cut && k2 == HurtKind.Crush && k3 == HurtKind.Burn && k4 == HurtKind.Burn && k5 == HurtKind.Crush && w5 == HurtWhere.Foot,
            "by the colony's means: a cut at the bench, a crushed hand at a machine, a fabricator's burn, the forge's burn, a load on the foot");

        // ---------------- Working through it (3.2.1) ----------------

        C(Near(HarmMath.WorkedThroughChance, 0.1f) && Near(HarmMath.WorkedThroughHours, 4f), "one in ten worked hard through four hours");
        C(HarmMath.WorkedThrough(4f, false, false) && !HarmMath.WorkedThrough(3f, false, false) && !HarmMath.WorkedThrough(8f, true, false) && !HarmMath.WorkedThrough(8f, false, true),
            "a tending or bed rest prevents it");
        C(Near(HarmMath.CrackShare, 1f / 3f) && HarmMath.FallCrackHigh <= 4f && HarmMath.FallBruiseHigh <= 5f, "the fall: a bruise, or a small crack one time in three");

        // ---------------- The mod's side ----------------

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));
        string harm = Code("Harm/Harm.cs"), work = Code("Harm/WorkHurts.cs"), drill = Code("Harm/DrillBouts.cs");
        C(harm.Contains("public static Hediff_Injury Injure(") && harm.Contains("public static Hediff Ail(") && harm.Contains("public static bool Fight(")
          && harm.Contains("public static bool May(") && harm.Contains("public static void Note(") && harm.Contains("public static float Scale"),
            "the service: Scale, May, Injure, Ail, Fight, Note");
        C(harm.Contains("HarmMath.SafeSeverity(") && harm.Contains("IncomingDamageFactor") && harm.Contains("Safe(x)"), "a wound by the body's own damage factor, on a part it can't kill");
        C(Regex.Matches(work + drill, @"Harm\.(Injure|Ail|Roll)\(").Count >= 5, "the work hurts and the drills go through the service");
        C(!Regex.IsMatch(work + drill, @"TakeDamage\(|AddHediff\("), "nothing in them hurts anyone but through the service");
        C(Code("Diplomacy/Feasts.cs").Contains("Harm.ScaleFor(HarmKinds.FeastSour)") && Code("Diplomacy/Feasts.cs").Contains("Harm.May(p, HurtClass.Bout)"),
            "the feasts' real blow and sour night under the same scale and cap");
        C(Code("Outsiders/OutsiderOutcomes.cs").Contains("Harm.Note(defender"), "a practice bout's hard blow counted toward the cap");
        C(drill.Contains("PlayOuts.On") && drill.Contains("!p.DevelopmentalStage.Juvenile()") && drill.Contains("HurtClass.Bout"), "the bouts on the map only, never children, under the cap as bouts");
        C(work.Contains("sickBay") && work.Contains("IsTended()") && work.Contains("AilmentTracker.On"), "working through it: the rest-when-ill decree and a tending prevent it, the ailments' setting holds it");

        // The decrees that name what they can do.
        var decrees = Directory.GetFiles(Path.Combine(root, "Defs", "DecreeDefs"), "*.xml").SelectMany(f => XDocument.Load(f).Root!.Elements("ColonyCouncil.DecreeDef")).ToList();
        string HurtsOf(string name) => decrees.FirstOrDefault(d => d.Element("defName")?.Value == name)?.Element("hurts")?.Value;
        C(HurtsOf("Council_Decree_Drills") == "Drills" && HurtsOf("Council_Decree_Militia") == "Muster", "the drills and the militia spar");
        C(HurtsOf("Council_Decree_LongHours") == "TiredHands" && HurtsOf("Council_Decree_Harvest") == "TiredHands" && HurtsOf("Council_Decree_Building") == "TiredHands",
            "long hours, a harvest push and a building season tire hands");
        C(HurtsOf("Council_Decree_BankedFires") == "ColdNights", "banked fires chill the frail in a hard frost (harm, part 4)");
        C(decrees.Count(d => d.Element("hurts") != null) == 6, "and no other decree");
        var militia = decrees.First(d => d.Element("defName")?.Value == "Council_Decree_Militia");
        C(militia.Element("eras") == null && militia.ToString().Contains("[spacer]"), "the militia in every age, with a spacer form");
        C(Code("UI/DecreePanel.cs").Contains("Harm.DecreeLine(def)") && Code("CouncilDynamics.cs").Contains("Harm.DecreeLine(def)"), "named on the decree's card and in its letter");

        // The setting and the clock.
        string settings = Code("CouncilSettings.cs"), catalog = Code("UI/SettingsCatalog.cs");
        C(settings.Contains("public float everydayHurts = 1f;") && settings.Contains("\"everydayHurts\", 1f"), "one setting, 100% by default, saved");
        C(catalog.Contains("Slider(\"everydayHurts\"") && catalog.Contains("0f, 2f"), "on the settings page, 0 to 200%");
        C(Sched.RareOffsets.Contains(Sched.RareHarm) && PassTable.All.Any(p => p.pass == Pass.RareHarm && p.offset == Sched.RareHarm), "a 250-tick slot of its own, timed");

        // The words: every told message in two wordings or more, every placeholder named.
        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Harm.xml")).Root!.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        foreach (var key in new[] { "CC_Harm_DrillBlow", "CC_Harm_MusterBlow", "CC_Harm_Slip_Bench", "CC_Harm_Slip_Hot", "CC_Harm_Slip_Heavy", "CC_Harm_Slip_Load", "CC_Harm_Slip_Field",
                     "CC_Harm_TornBack", "CC_Harm_AnkleFall", "CC_Harm_AnkleCrack" })
        {
            C(keyed.ContainsKey(key) && keyed.ContainsKey(key + "_2"), $"{key} has two wordings or more");
            C(keyed.Where(kv => kv.Key == key || kv.Key.StartsWith(key + "_")).All(kv => kv.Value.Contains("{PAWN_")), $"{key} names who was hurt");
        }
        foreach (WorkHurtKind w in Enum.GetValues(typeof(WorkHurtKind)))
            if (w != WorkHurtKind.None) C(keyed.ContainsKey("CC_Harm_Slip_" + w), $"a slip at {w} has its words");
        foreach (DecreeHurt h in Enum.GetValues(typeof(DecreeHurt)))
            if (h != DecreeHurt.None) C(keyed.ContainsKey("CC_Harm_Decree_" + h), $"{h} is named before the choice");
        C(keyed.Values.All(v => !v.Contains("—") && !v.Contains("!")), "no em dashes or exclamation marks");
        C(keyed.Where(kv => kv.Key.StartsWith("CC_Harm_Decree_")).All(kv => !Regex.IsMatch(kv.Value, @"\d")), "the decree's line is a hint, no numbers");
        C(File.Exists(Path.Combine(root, "Defs", "HediffDefs", "Rimfolk_Hediffs_Harm.xml")) && File.ReadAllText(Path.Combine(root, "Defs", "HediffDefs", "Rimfolk_Hediffs_Harm.xml")).Contains("Council_Ail_TornBack"),
            "the torn back is one of Rimfolk's ailments");
        C(File.ReadAllText(Path.Combine(root, "DESIGN.md")).Contains("## Harm from everyday life"), "DESIGN.md has the shared rules and the service");
        return n;
    }
}
