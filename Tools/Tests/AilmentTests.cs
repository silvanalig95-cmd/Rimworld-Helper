using System.Globalization;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Checks the ailments' numbers against what the Almanac and the letters promise: everyday ailments never kill and
/// pass within about two weeks untreated; each lethal serious illness kills when left untreated, is survived with good
/// care (industrial medicine, a decent doctor) even with bad luck, and with the best a pre-industrial colony has
/// (herbal medicine, a good doctor, steady tending); tuberculosis comes back when treatment stops early; scurvy takes
/// weeks to become dangerous and clears within two weeks of fresh food. The numbers are read from the defs, through
/// the same race the game runs (AilmentMath.Simulate).
/// </summary>
public static class AilmentTests
{
    private static float F(XElement e, string name, float fallback)
    {
        var v = e?.Element(name)?.Value?.Trim();
        return v != null && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : fallback;
    }

    private static XElement Comp(XElement def, string classSuffix) =>
        def.Element("comps")?.Elements("li").FirstOrDefault(li => ((string)li.Attribute("Class") ?? "").EndsWith(classSuffix));

    public class Illness
    {
        public string name;
        public bool lethal;
        public AilmentMath.Course course;
        public float basic = 1f, industrial = 1f, ultra = 1f;
        public bool immunizable;
        /// <summary>A diet illness's course (scurvy): worse per day without fresh food, better per day with it.</summary>
        public float dietGrow, dietRecover;
    }

    public static List<Illness> Load(string file)
    {
        var doc = XDocument.Load(file);
        var parents = doc.Root!.Elements("HediffDef").Where(d => d.Attribute("Name") != null).ToDictionary(d => (string)d.Attribute("Name"));
        var list = new List<Illness>();
        foreach (var def in doc.Root.Elements("HediffDef").Where(d => (string)d.Attribute("Abstract") != "True"))
        {
            parents.TryGetValue((string)def.Attribute("ParentName") ?? "", out var parent);
            float Inherit(string n, float fb) => F(def, n, F(parent, n, fb));
            var imm = Comp(def, "AilmentImmunizable");
            var med = Comp(def, "AilmentMedicine");
            var ill = new Illness
            {
                name = def.Element("defName")!.Value,
                lethal = Inherit("lethalSeverity", -1f) > 0f,
                immunizable = imm != null,
                course = new AilmentMath.Course
                {
                    severityPerDayNotImmune = F(imm, "severityPerDayNotImmune", 0f),
                    immunityPerDaySick = F(imm, "immunityPerDaySick", 0f),
                    severityPerDayImmune = F(imm, "severityPerDayImmune", 0f),
                    treatedImmunityPerDay = F(imm, "treatedImmunityPerDay", 0f),
                    untendedImmunityLossPerDay = F(imm, "untendedImmunityLossPerDay", 0f),
                    severityPerDayTended = F(med, "severityPerDayTended", 0f),
                    startSeverity = Math.Max(Inherit("initialSeverity", 0.05f), 0.02f),
                    lethalSeverity = Inherit("lethalSeverity", -1f),
                    maxSeverity = Inherit("maxSeverity", 1f)
                },
                basic = F(med, "basic", 1f),
                industrial = F(med, "industrial", 1f),
                ultra = F(med, "ultra", 1f),
                dietGrow = F(Comp(def, "AilmentDiet"), "growPerDay", 0f),
                dietRecover = F(Comp(def, "AilmentDiet"), "recoverPerDay", 0f)
            };
            list.Add(ill);
        }
        return list;
    }

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        // The medicine tiers, from the medicine's best possible quality.
        check(AilmentMath.TierOf(0.7f) == 0 && AilmentMath.TierOf(1f) == 1 && AilmentMath.TierOf(1.3f) == 2, "medicine tiers: herbal 0, industrial 1, glitterworld 2");
        check(Math.Abs(AilmentMath.CatchingMtbDays(20f, 60f, 1f, 1f, 1f) - 20f) < 1e-4f, "catching clock at defaults is 20 days");
        check(Math.Abs(AilmentMath.CatchingMtbDays(20f, 60f, 1f, 2f, 1f) - 10f) < 1e-4f && Math.Abs(AilmentMath.CatchingMtbDays(20f, 60f, 1f, 0.25f, 1f) - 80f) < 1e-4f,
            "frequency 200% halves and 25% quadruples the catching clock");
        check(AilmentMath.CatchingMtbDays(20f, 1f, 1f, 1f, 1f) >= 10f && AilmentMath.CatchingMtbDays(20f, 9999f, 1f, 1f, 1f) <= 40f, "the biome's disease rate is clamped to half or double");

        // Everyday ailments: never lethal, and over within about two weeks with no care at all.
        foreach (var a in Load(Path.Combine(root, "Defs", "HediffDefs", "Council_Hediffs_Ailments.xml")))
        {
            n++;
            check(!a.lethal, $"{a.name} is an everyday ailment and must not be lethal");
            check(a.course.maxSeverity <= 1f, $"{a.name} caps its severity at 1");
            if (!a.immunizable) continue;
            var o = AilmentMath.Simulate(a.course, 0f, 1f, 0f);
            check(!o.died && o.days <= 14f, $"{a.name} passes untreated within two weeks, took {o.days:0.0} days");
        }

        // Serious illnesses.
        var serious = Load(Path.Combine(root, "Defs", "HediffDefs", "Council_Hediffs_Illnesses.xml"));
        foreach (var s in serious.Where(s => s.immunizable))
        {
            n++;
            var c = s.course;
            check(s.lethal, $"{s.name} is lethal at its worst");
            var untreated = AilmentMath.Simulate(c, 0f, 1f, 0f);
            check(untreated.died, $"{s.name} kills when left untreated (peak {untreated.peak:0.00})");
            // Good care: industrial medicine, a decent doctor, a gap in one tend of four, and bad luck on both sides.
            var good = AilmentMath.Simulate(c, 0.8f, s.industrial, 0.75f, 0.85f, 1.15f);
            check(!good.died, $"{s.name} is survived with industrial medicine and decent care (peak {good.peak:0.00})");
            var best = AilmentMath.Simulate(c, 0.8f, s.industrial, 1f);
            check(!best.died && best.peak < 0.8f, $"{s.name} with steady good care stays short of life-threatening (peak {best.peak:0.00})");
            // The best of a pre-industrial colony: herbal medicine at its best quality, steady tending, average luck.
            var herbal = AilmentMath.Simulate(c, 0.7f, s.basic, 1f);
            check(!herbal.died, $"{s.name} is survived with herbal medicine and steady tending (peak {herbal.peak:0.00})");
            // Industrial medicine helps at least as much as herbs (more for the ones that need modern drugs).
            check(s.industrial >= s.basic && s.ultra >= s.industrial, $"{s.name}: better medicine never helps less");
        }
        var tb = serious.FirstOrDefault(s => s.name == "Council_Ail_Tuberculosis");
        check(tb != null, "tuberculosis is defined");
        if (tb != null)
        {
            var full = AilmentMath.Simulate(tb.course, 0.8f, tb.industrial, 1f);
            check(!full.died && full.daysToImmune >= 20f, $"tuberculosis needs weeks of treatment ({full.daysToImmune:0} days to immunity)");
            var stopped = AilmentMath.Simulate(tb.course, 0.8f, tb.industrial, 1f, 1f, 1f, 15f);
            check(stopped.died, "tuberculosis comes back if treatment stops after two weeks");
        }
        // Scurvy: weeks to become dangerous, two weeks to clear on fresh food.
        var scurvy = serious.FirstOrDefault(s => s.name == "Council_Ail_Scurvy");
        check(scurvy != null && scurvy.dietGrow > 0f && scurvy.dietRecover < 0f, "scurvy has a diet course");
        if (scurvy != null)
        {
            n++;
            check(AilmentMath.DaysToLethal(scurvy.course.startSeverity, scurvy.dietGrow) >= 45f, "scurvy takes many weeks to become fatal");
            check(AilmentMath.DaysToClear(0.75f, scurvy.dietRecover) <= 14f, "scurvy clears within two weeks of fresh food, even from its worst stage");
        }
        return n;
    }
}
