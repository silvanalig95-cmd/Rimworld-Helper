using System;
using System.Collections.Generic;
using System.Linq;
using ColonyCouncil;

/// <summary>
/// What an answer changes for a while and puts back (Dilemmas/AnswerShiftMath.cs): the short lines it is saved as, the
/// timetable plans (the heat wave's hours, a midday rest, a night at work, a late start), how long each lasts, and
/// which of two colonists' work needs a new limb more.
/// </summary>
public static class AnswerShiftTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "answer shifts: " + what);
        }

        // ---------- Written and read back ----------
        var changes = new List<AnswerShiftMath.Change>
        {
            AnswerShiftMath.Hour(13, "Anything", "Sleep"),
            AnswerShiftMath.Hour(0, "Sleep", "Work"),
            AnswerShiftMath.WorkPlace("Cooking", 1, 0)
        };
        string line = AnswerShiftMath.Write(changes);
        C(line == "h:13=Anything>Sleep;h:0=Sleep>Work;w:Cooking=1>0", "written as short lines (" + line + ")");
        var back = AnswerShiftMath.Read(line);
        C(back.Count == 3, "read back whole");
        C(!back[0].work && back[0].hour == 13 && back[0].was == "Anything" && back[0].set == "Sleep", "an hour read back");
        C(back[2].work && back[2].key == "Cooking" && back[2].was == "1" && back[2].set == "0", "a work place read back");
        C(AnswerShiftMath.Read("").Count == 0 && AnswerShiftMath.Read(null).Count == 0, "nothing reads as nothing");
        C(AnswerShiftMath.Read("h:25=Work>Sleep;x:3=a>b;h:5=>Sleep;w:Cooking=one>0;garbage").Count == 0, "what can't be read is left out");

        // ---------- The plans ----------
        string Day(int h) => h <= 5 || h >= 22 ? "Sleep" : h == 20 || h == 21 ? "Joy" : "Anything";
        var heat = AnswerShiftMath.Plan(AnswerShiftMath.Heat, Day);
        C(heat.Where(c => c.set == "Sleep").Select(c => c.hour).OrderBy(h => h).SequenceEqual(new[] { 11, 12, 13, 14, 15, 16 }), "the heat wave's hours sleep 11 to 16");
        C(heat.Where(c => c.set == "Work").Select(c => c.hour).OrderBy(h => h).SequenceEqual(new[] { 0, 1, 2, 22, 23 }), "and work the night's sleeping hours to 2 (21 was recreation, kept)");
        C(heat.All(c => c.was != "Joy"), "recreation hours are never taken");
        var siesta = AnswerShiftMath.Plan(AnswerShiftMath.Siesta, Day);
        C(siesta.Select(c => c.hour).SequenceEqual(new[] { 12, 13, 14, 15 }) && siesta.All(c => c.set == "Joy"), "a midday rest is 12 to 15, recreation");
        var night = AnswerShiftMath.Plan(AnswerShiftMath.Night, Day);
        C(night.Count(c => c.set == "Work") == 8 && night.Where(c => c.set == "Work").All(c => c.was == "Sleep"), "a night at work turns tonight's eight sleeping hours to work");
        C(night.Where(c => c.set == "Sleep").Select(c => c.hour).SequenceEqual(new[] { 6, 7, 8, 9, 10 }), "and sleeps the morning after, 6 to 10");
        var late = AnswerShiftMath.Plan(AnswerShiftMath.Late, Day);
        C(late.Select(c => c.hour).SequenceEqual(new[] { 6, 7, 8 }) && late.All(c => c.set == "Sleep"), "a late start sleeps 6 to 8");
        C(AnswerShiftMath.Plan(AnswerShiftMath.Late, h => "Sleep").Count == 0, "nothing changes for someone already asleep then");
        C(AnswerShiftMath.Plan("nonsense", Day).Count == 0 && AnswerShiftMath.Plan(AnswerShiftMath.Heat, null).Count == 0, "an unknown plan changes nothing");
        C(AnswerShiftMath.Span(22, 2).SequenceEqual(new[] { 22, 23, 0, 1, 2 }), "hours run round midnight");
        foreach (var p in AnswerShiftMath.Plans)
            C(AnswerShiftMath.Read(AnswerShiftMath.Write(AnswerShiftMath.Plan(p, Day))).Count == AnswerShiftMath.Plan(p, Day).Count, p + ": every change keeps in a save");

        // ---------- How long ----------
        C(Math.Abs(AnswerShiftMath.Hours(AnswerShiftMath.Night, 14f) - 21f) < 0.01f, "asked at 14, a night at work lasts to 11 the next morning");
        C(Math.Abs(AnswerShiftMath.Hours(AnswerShiftMath.Night, 2f) - 9f) < 0.01f, "asked at 2 in the night, to 11 that morning");
        C(Math.Abs(AnswerShiftMath.Hours(AnswerShiftMath.Night, 10f) - 25f) < 0.01f, "asked at 10, to 11 the day after (the night still to come)");
        C(Math.Abs(AnswerShiftMath.Hours(AnswerShiftMath.Late, 21f) - 12f) < 0.01f, "a late start asked at 21 lasts to 9 the next morning");
        C(AnswerShiftMath.Hours(AnswerShiftMath.Heat, 12f) >= 24f * 10f, "the heat wave's hours last until it ends, days at most");
        for (float h = 0f; h < 24f; h += 0.5f)
            foreach (var p in AnswerShiftMath.Plans)
                C(AnswerShiftMath.Hours(p, h) > 0f && AnswerShiftMath.Hours(p, h) <= AnswerShiftMath.MaxDays * 24f, $"{p} at {h}: a while, never for ever");

        // ---------- Who gets the new limb ----------
        C(AnswerShiftMath.PartKind("Leg") == "leg" && AnswerShiftMath.PartKind("Foot") == "leg", "legs and feet");
        C(AnswerShiftMath.PartKind("Arm") == "arm" && AnswerShiftMath.PartKind("Hand") == "arm" && AnswerShiftMath.PartKind("Shoulder") == "arm", "arms, hands, shoulders");
        C(AnswerShiftMath.PartKind("Eye") == "eye" && AnswerShiftMath.PartKind("Jaw") == "any", "eyes, and anything else");
        var crafter = new Dictionary<string, int> { ["Crafting"] = 12, ["Shooting"] = 3 };
        var hunter = new Dictionary<string, int> { ["Crafting"] = 2, ["Shooting"] = 11 };
        C(AnswerShiftMath.FirstNeedsMore(crafter, hunter, "arm") && !AnswerShiftMath.FirstNeedsMore(crafter, hunter, "leg"), "a crafter's hands, a hunter's legs");
        AnswerShiftMath.Need(hunter, "eye", out string skill);
        C(skill == "Shooting", "the skill named is the one the part serves (" + skill + ")");
        C(AnswerShiftMath.Need(new Dictionary<string, int>(), "leg", out string none) < 0 && none == null, "no skills: no need, no skill named");
        C(AnswerShiftMath.FirstNeedsMore(crafter, crafter, "arm"), "a tie goes to the first");
        foreach (var k in new[] { "leg", "arm", "eye", "any" })
            C(AnswerShiftMath.SkillsFor(k).Length >= 3, k + ": a few skills it serves");
        return n;
    }
}
