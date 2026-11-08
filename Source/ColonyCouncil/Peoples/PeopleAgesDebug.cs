using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: the peoples' ages (Debug actions, "Rimfolk", "Peoples: ..."). Every people's age, its way toward the next
    /// age or back and what the rule reads for it (in the log); a people up or down an age now, with its letter; and every
    /// people's season looked at now, whatever its day.
    /// </summary>
    public static class PeopleAgesDebug
    {
        private static void Say(string text) => Messages.Message("Peoples: " + text, MessageTypeDefOf.SilentInput, false);

        private static IEnumerable<Faction> Ours => Find.FactionManager.AllFactionsListForReading.Where(f => f != null && !f.defeated && !f.IsPlayer && Peoples.IsOurs(f));

        [DebugAction("Rimfolk", "Peoples: every people's age and progress (log)", allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugList()
        {
            int now = Find.TickManager.TicksGame;
            var sb = new StringBuilder("[Rimfolk] Peoples' ages" + (PeopleAges.On ? "" : " (the setting is off: every people lives in the age it began in)") + ":\n");
            foreach (var f in Ours)
            {
                var r = PeopleAges.Ensure(f);
                if (r == null) continue;
                var w = PeopleAges.Ways(f, r);
                var d = PeopleAges.Deeds(f, r, now);
                var why = new List<(string, float)>();
                float boost = PeopleAgesMath.Boost(d, why);
                float cut = PeopleAgesMath.CutOff(d, null);
                var next = PeopleAgesMath.Season(r.age, r.progress, w, PeopleAgesMath.Fate(f.loadID), d);
                sb.AppendLine($"  {f.Name} ({f.def.defName}): {PeopleAgeWords.AgeWord(r.age)} (began {PeopleAgeWords.AgeWord(r.start)}, floor {PeopleAgeWords.AgeWord(w.floor)}, "
                              + $"limit {PeopleAgeWords.AgeWord(w.limit)}), progress {r.progress:0.00}, heading {r.heading} ({r.why}), pace {w.pace:0.#} years x{PeopleAgesMath.Fate(f.loadID):0.00}"
                              + (w.declines ? $", declines unless supplied ({w.declineYears:0.#} years an age)" : "") + "; ");
                sb.AppendLine($"    the year: trade {d.trade:0} silver, gifts {d.gifts:0}, learners {d.learners}, lessons {d.lessons}, bond {d.bond:0.#}, alone {d.yearsAlone:0.0} years, "
                              + $"burned {d.burned} of {d.towns + d.burned} towns, at war with {d.war:P0}, parts {d.supply:0}; boost x{boost:0.00}, cut off {cut:0.00}; "
                              + $"next season {next.progress:0.00}{(next.change != 0 ? (next.change > 0 ? ", rises" : ", falls back") : "")}"
                              + $"{(r.looked >= 0 ? $", looked {(now - r.looked) / (float)GenDate.TicksPerDay:0.#} days ago" : "")}.");
            }
            Log.Message(sb.ToString().TrimEnd());
            Say("listed in the log.");
        }

        private static void Pick(string label, int dir)
        {
            var options = new List<FloatMenuOption>();
            foreach (var f in Ours)
            {
                var people = f;
                int age = Peoples.AgeOf(people);
                if (age + dir < 0 || age + dir > 3) continue;
                options.Add(new FloatMenuOption(people.Name + " (" + PeopleAgeWords.AgeWord(age) + " to " + PeopleAgeWords.AgeWord(age + dir) + ")", () =>
                {
                    if (!PeopleAges.On) Say("the setting \"Peoples' ages change\" is off; the age is kept, and shown once it is on.");
                    Say(PeopleAges.Push(people, dir) ? people.Name + " " + label + "." : "couldn't.");
                }));
            }
            if (options.Count == 0) Say("no people of Rimfolk's can go that way.");
            else Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Peoples: a people up an age now (testing)...", allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugUp() => Pick("rose an age", 1);

        [DebugAction("Rimfolk", "Peoples: a people down an age now (testing)...", allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugDown() => Pick("fell back an age", -1);

        [DebugAction("Rimfolk", "Peoples: every people's season now (testing)", allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugSeason()
        {
            int now = Find.TickManager.TicksGame, changed = 0, n = 0;
            foreach (var f in Ours.ToList())
            {
                var r = PeopleAges.Ensure(f);
                if (r == null) continue;
                n++;
                if (PeopleAges.Look(f, r, now).change != 0) changed++;
            }
            Say(n + " peoples looked at, " + changed + " changed age.");
        }
    }
}
