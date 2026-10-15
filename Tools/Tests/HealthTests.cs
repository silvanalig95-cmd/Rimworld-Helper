using System.Globalization;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Checks what the Almanac and the settings promise about convalescence and healers' reputations: a recovery of a few
/// days to two weeks from every cause, setbacks that only lengthen it (and seldom), never more than its longest, and a
/// doctor's standing that a run of losses can take away. The hediff's own numbers are read from the def, so HealthMath
/// and the XML can't drift apart.
/// </summary>
public static class HealthTests
{
    private static float F(XElement e, string name)
    {
        var v = e?.Element(name)?.Value?.Trim();
        return v != null && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : float.NaN;
    }

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "health: " + what);
        }

        // The hediff in the defs is the one HealthMath describes.
        var doc = XDocument.Load(Path.Combine(root, "Defs", "HediffDefs", "Council_Hediffs_Ailments.xml"));
        var conv = doc.Root!.Elements("HediffDef").FirstOrDefault(d => d.Element("defName")?.Value == "Council_Ail_Convalescent");
        C(conv != null, "Council_Ail_Convalescent exists");
        if (conv != null)
        {
            var rec = conv.Element("comps")?.Elements("li").FirstOrDefault(li => ((string)li.Attribute("Class") ?? "").EndsWith("AilmentRecovery"));
            C(Math.Abs(-F(rec, "severityPerDay") - HealthMath.RecoveryPerDay) < 0.0001f, "recovery per day matches the def");
            C(Math.Abs(F(rec, "bedFactor") - HealthMath.BedFactor) < 0.0001f, "bed factor matches the def");
            var stages = conv.Element("stages")?.Elements("li").ToList() ?? new List<XElement>();
            C(stages.Count == 2 && Math.Abs(F(stages[1], "minSeverity") - HealthMath.WeakFrom) < 0.0001f, "the weak stage starts where setbacks can");
        }
        // What the serious illnesses leave behind stays within the longest convalescence.
        var ill = XDocument.Load(Path.Combine(root, "Defs", "HediffDefs", "Council_Hediffs_Illnesses.xml"));
        foreach (var li in ill.Descendants("li").Where(l => l.Element("hediff")?.Value == "Council_Ail_Convalescent"))
        {
            var range = li.Element("severity")?.Value?.Split('~');
            float max = range != null && range.Length == 2 && float.TryParse(range[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var m) ? m : 1f;
            C(max <= HealthMath.MaxSeverity + 0.0001f, "an illness's convalescence stays within the longest one");
        }

        // Every cause: a few days to two weeks.
        float days = HealthMath.DaysUp(HealthMath.MaxSeverity);
        C(days <= 15.01f, $"the longest convalescence is two weeks out of bed ({days:0.0})");
        foreach (var (what, s) in new[]
                 {
                     ("four days in bed", HealthMath.AfterBedRest(4f)), ("three weeks in bed", HealthMath.AfterBedRest(21f)),
                     ("an operation on a limb", HealthMath.AfterSurgery(false, false, 0.12f)), ("an organ", HealthMath.AfterSurgery(false, true, 0.02f)),
                     ("the brain", HealthMath.AfterSurgery(true, true, 0.01f)), ("an arm lost", HealthMath.AfterLimbLost(0.12f)),
                     ("a leg lost", HealthMath.AfterLimbLost(0.14f)), ("an illness at half", HealthMath.AfterIllness(0.5f)),
                     ("a close call", HealthMath.AfterIllness(0.99f))
                 })
        {
            float d = HealthMath.DaysUp(s);
            C(d >= 2f && d <= 15f, $"after {what}: {d:0.0} days out of bed");
            C(HealthMath.DaysInBed(s) < d, $"after {what}: shorter in bed");
        }
        C(HealthMath.AfterBedRest(3.9f) == 0f, "under four days in bed leaves nothing");
        C(HealthMath.AfterIllness(0.49f) == 0f, "an illness that never got serious leaves nothing");
        C(HealthMath.AfterBedRest(10f) > HealthMath.AfterBedRest(5f), "longer in bed, longer to recover");
        C(!HealthMath.MajorPart(false, 0.013f) && HealthMath.MajorPart(false, 0.12f) && HealthMath.MajorPart(true, 0.01f), "a hand is minor, an arm or an organ major");

        // Causes don't add up; setbacks only lengthen, seldom, and never past the longest.
        C(HealthMath.Combine(0.5f, 0.3f) == 0.5f && HealthMath.Combine(0.3f, 0.5f) == 0.5f, "two causes take the larger");
        C(HealthMath.AfterSetback(0.85f) <= HealthMath.MaxSeverity, "a setback stays within the longest");
        C(HealthMath.AfterSetback(0.5f) > 0.5f, "a setback lengthens it");
        float extra = HealthMath.DaysUp(HealthMath.SetbackSeverity * HealthMath.MaxSetbacks);
        C(extra <= 4.01f, $"setbacks add at most four days in all ({extra:0.0})");
        float perDay = HealthMath.SetbackChancePerDay(8f);
        C(perDay > 0.02f && perDay < 0.1f, $"a full working day while weak sets it back seldom ({perDay:P0})");

        // A doctor's standing.
        C(HealthMath.DoctorLevel(0, 0) == 0 && HealthMath.DoctorLevel(2, 0) == 0, "two saved isn't a reputation yet");
        C(HealthMath.DoctorLevel(3, 0) == 1 && HealthMath.DoctorLevel(3, 1) == 1, "three saved: trusted");
        C(HealthMath.DoctorLevel(3, 2) == 0, "a run of losses takes the trust away");
        C(HealthMath.DoctorLevel(8, 2) == 2 && HealthMath.DoctorLevel(8, 3) == 1, "eight saved and few lost: renowned");
        C(HealthMath.MidwifeLevel(1) == 0 && HealthMath.MidwifeLevel(2) == 1 && HealthMath.MidwifeLevel(5) == 2, "midwife levels");
        return n;
    }
}
