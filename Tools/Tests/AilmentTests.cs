using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Checks the ailments' numbers against what the Almanac and the letters promise: everyday ailments never kill and
/// pass within about two weeks untreated; each lethal serious illness kills when left untreated, is survived with good
/// care (industrial medicine, a decent doctor) even with bad luck, and with the best a pre-industrial colony has
/// (herbal medicine, a good doctor, steady tending); the serious ones that can't kill (measles, kidney stones,
/// shingles) pass on their own; rabies is past saving once its symptoms show; tuberculosis comes back when treatment
/// stops early; scurvy takes weeks to become dangerous and clears within two weeks of fresh food. The numbers are read
/// from the defs, through the same race the game runs (AilmentMath.Simulate). And every ailment has a mood with a hint,
/// and the other ways its message and letter are told fit where they go (Texts).
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
        /// <summary>A course with a turning point and no immunity (rabies).</summary>
        public bool staged;
        /// <summary>A steady recovery (kidney stones, shingles), per day.</summary>
        public float recovery;
        /// <summary>A diet illness's course (scurvy): worse per day without fresh food, better per day with it.</summary>
        public float dietGrow, dietRecover;
    }

    /// <summary>Every named HediffDef parent in the folder, so a def can inherit across files (and through more than one parent).</summary>
    private static Dictionary<string, XElement> Parents(string folder)
    {
        var parents = new Dictionary<string, XElement>();
        foreach (var f in Directory.GetFiles(folder, "*.xml"))
            foreach (var d in XDocument.Load(f).Root!.Elements("HediffDef"))
                if (d.Attribute("Name") != null) parents[(string)d.Attribute("Name")] = d;
        return parents;
    }

    public static List<Illness> Load(string file)
    {
        var doc = XDocument.Load(file);
        var parents = Parents(Path.GetDirectoryName(file)!);
        var list = new List<Illness>();
        foreach (var def in doc.Root!.Elements("HediffDef").Where(d => (string)d.Attribute("Abstract") != "True"))
        {
            float Inherit(string n, float fb)
            {
                for (var e = def; e != null; e = parents.TryGetValue((string)e.Attribute("ParentName") ?? "", out var up) ? up : null)
                    if (e.Element(n)?.Value?.Trim() is string v && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var f)) return f;
                return fb;
            }
            var imm = Comp(def, "AilmentImmunizable");
            var med = Comp(def, "AilmentMedicine");
            var staged = Comp(def, "AilmentCourse");
            var ill = new Illness
            {
                name = def.Element("defName")!.Value,
                lethal = Inherit("lethalSeverity", -1f) > 0f,
                immunizable = imm != null,
                staged = staged != null,
                recovery = F(Comp(def, "AilmentRecovery"), "severityPerDay", 0f),
                course = new AilmentMath.Course
                {
                    severityPerDayNotImmune = staged != null ? F(staged, "severityPerDay", 0f) : F(imm, "severityPerDayNotImmune", 0f),
                    fastAbove = F(staged, "fastAbove", 999f),
                    fastPerDay = F(staged, "fastPerDay", 0f),
                    tendWorksBelow = F(med, "worksBelow", 999f),
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

        // The skill that keeps bee stings off, a daily smoker's cough, and gout's fresh food.
        check(AilmentMath.SkillFactor(0) == 1f && Math.Abs(AilmentMath.SkillFactor(10) - 0.5f) < 1e-4f && AilmentMath.SkillFactor(15) == 0.25f && AilmentMath.SkillFactor(20) == 0.25f,
            "skill keeps an ailment off: 1 at level 0, a half at 10, a quarter from 15");
        check(AilmentMath.SmokeFactor(0f) == 1f && AilmentMath.SmokeFactor(1f) == 3f && AilmentMath.SmokeFactor(2f) == 3f, "a daily smoker is three times as likely to get the cough");
        check(AilmentMath.FreshFactor(true, 0.5f) == 0.5f && AilmentMath.FreshFactor(false, 0.5f) == 1f, "fruit or vegetables lately halve gout");

        // Everyday ailments: never lethal, and over within about two weeks with no care at all.
        var everyday = new[] { "Council_Hediffs_Ailments.xml", "Rimfolk_Hediffs_Ailments_New.xml", "Rimfolk_Hediffs_Ailments_Mods_New.xml", "Rimfolk_Hediffs_Ailments_Land.xml" }
            .SelectMany(f => Load(Path.Combine(root, "Defs", "HediffDefs", f))).ToList();
        foreach (var a in everyday)
        {
            n++;
            check(!a.lethal, $"{a.name} is an everyday ailment and must not be lethal");
            check(a.course.maxSeverity <= 1f, $"{a.name} caps its severity at 1");
            // The ones that simply get better do so within three weeks at most, even from a bad start.
            if (a.recovery < 0f) check(AilmentMath.DaysToClear(0.6f, a.recovery) <= 21f, $"{a.name} clears on its own within three weeks ({AilmentMath.DaysToClear(0.6f, a.recovery):0} days)");
            if (!a.immunizable) continue;
            var o = AilmentMath.Simulate(a.course, 0f, 1f, 0f);
            check(!o.died && o.days <= 14f, $"{a.name} passes untreated within two weeks, took {o.days:0.0} days");
        }

        // Serious illnesses.
        var serious = new[] { "Council_Hediffs_Illnesses.xml", "Rimfolk_Hediffs_Illnesses_Land.xml" }
            .SelectMany(f => Load(Path.Combine(root, "Defs", "HediffDefs", f))).ToList();
        // The serious ones that can't kill pass on their own, within a month at most.
        foreach (var s in serious.Where(s => !s.lethal))
        {
            n++;
            if (s.immunizable || s.staged)
            {
                var o = AilmentMath.Simulate(s.course, 0f, 1f, 0f);
                check(!o.died && o.days <= 30f, $"{s.name} can't kill and passes untreated within a month, took {o.days:0.0} days");
            }
            else check(s.recovery < 0f, $"{s.name} can't kill and gets better on its own");
        }
        foreach (var s in serious.Where(s => s.lethal && (s.immunizable || s.staged)))
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
        // Rabies: treatment from the bite saves them; once the symptoms show, nothing does.
        var rabies = serious.FirstOrDefault(s => s.name == "Council_Ail_Rabies");
        check(rabies != null && rabies.staged, "rabies is defined with a staged course");
        if (rabies != null)
        {
            var late = rabies.course;
            var symptoms = new AilmentMath.Course
            {
                severityPerDayNotImmune = late.severityPerDayNotImmune,
                fastAbove = late.fastAbove,
                fastPerDay = late.fastPerDay,
                tendWorksBelow = late.tendWorksBelow,
                severityPerDayTended = late.severityPerDayTended,
                startSeverity = late.fastAbove + 0.01f,
                lethalSeverity = late.lethalSeverity,
                maxSeverity = late.maxSeverity
            };
            var tooLate = AilmentMath.Simulate(symptoms, 1f, rabies.ultra, 1f);
            check(tooLate.died && tooLate.days <= 7f, $"rabies is fatal within a week once its symptoms show, even with the best care ({tooLate.days:0.0} days)");
            var untreated = AilmentMath.Simulate(rabies.course, 0f, 1f, 0f);
            check(untreated.days >= 7f, $"rabies gives a week or more to treat it before the symptoms show (died after {untreated.days:0.0} days)");
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
        Texts(root, check);
        return n;
    }

    /// <summary>
    /// Every ailment has its mood and a hint for that mood. The other ways its message and its letter are told (gotTexts,
    /// letters) name nobody but who has it ({PAWN_...}): a message always names them, a letter tagged [one] names its one
    /// person (so it needs no list), and any other letter ends with a colon, for the list of who caught it that follows.
    /// </summary>
    private static void Texts(string root, Action<bool, string> check)
    {
        string defs = Path.Combine(root, "Defs");
        var ailments = new List<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(defs, "HediffDefs"), "*.xml"))
            foreach (var d in XDocument.Load(file).Root!.Elements("HediffDef"))
                if (d.Descendants("li").Any(li => ((string)li.Attribute("Class") ?? "").EndsWith("HediffCompProperties_Ailment")))
                    ailments.Add(d.Element("defName")?.Value ?? "");
        var moods = new Dictionary<string, string>();
        foreach (var file in Directory.GetFiles(Path.Combine(defs, "ThoughtDefs"), "*.xml"))
            foreach (var t in XDocument.Load(file).Root!.Elements("ThoughtDef"))
                if (t.Element("hediff") != null) moods[t.Element("hediff")!.Value] = t.Element("defName")?.Value ?? "";
        var hinted = new HashSet<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(defs, "MoodHintDefs"), "*.xml"))
            foreach (var li in XDocument.Load(file).Root!.Descendants("thoughts").Elements("li"))
                hinted.Add(li.Value.Trim());
        check(ailments.Count >= 60, $"found the ailments ({ailments.Count})");
        foreach (var a in ailments)
        {
            check(moods.TryGetValue(a, out var mood), $"{a} has a mood");
            if (mood != null) check(hinted.Contains(mood), $"{a}'s mood {mood} has a hint");
        }
        var placeholder = new Regex(@"\{([^{}]+)\}");
        int told = 0;
        foreach (var file in Directory.GetFiles(defs, "*.xml", SearchOption.AllDirectories))
        {
            var doc = XDocument.Load(file);
            foreach (var li in doc.Descendants("gotTexts").Elements("li").Concat(doc.Descendants("letters").Elements("li")))
            {
                told++;
                string line = li.Value.Trim();
                string tags = line.StartsWith("[") && line.IndexOf(']') > 0 ? line.Substring(1, line.IndexOf(']') - 1) : "";
                string body = tags.Length > 0 ? line.Substring(line.IndexOf(']') + 1).Trim() : line;
                string where = $"{Path.GetFileName(file)}: '{body}'";
                foreach (Match m in placeholder.Matches(body))
                    check(m.Groups[1].Value.StartsWith("PAWN_"), $"{where} names only who has it ({m.Value})");
                bool letter = li.Parent!.Name.LocalName == "letters";
                bool one = tags.Split(',').Any(t => t.Trim() == "one");
                if (!letter || one) check(body.Contains("{PAWN_"), $"{where} names who has it");
                else check(body.Contains("{PAWN_") || body.EndsWith(":"), $"{where} ends with a colon, for the list that follows");
            }
        }
        check(told >= 40, $"found the ailments' other messages and letters ({told})");
    }
}
