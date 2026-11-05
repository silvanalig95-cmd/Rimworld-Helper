using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What groups do together (<see cref="GroupHabitDef"/>). Once an hour (Sched.Groups) each group looks whether two or
    /// more of its people are together right now doing its thing: at leisure by the fire of an evening, at the same table,
    /// at games in the rec room, at the same work, up in the small hours. If they are, they keep the habit (once a day):
    /// each feels they belong (a small good memory that gives company, not mood), their strain eases a little, one of
    /// them says something, and the group grows closer. Every week or two, with "Answers play out on the map", the anchor
    /// of a close group calls them all to it: a fire laid of an evening or a gathering in the rec room, on the map, which
    /// says as it begins who gathers, what the group is and what it does for them.
    /// </summary>
    public static class GroupHabits
    {
        private const int Day = GenDate.TicksPerDay;
        private static int Now => Find.TickManager.TicksGame;
        private static int Today => Now / Day;

        private static readonly Color SpeechColor = new Color(0.95f, 0.9f, 0.78f);

        // ---------------- Kept as it comes ----------------

        public static void Hourly(GroupTracker t)
        {
            int today = Today;
            var fires = new Dictionary<Map, Thing>();
            foreach (var g in t.groups.ToList())
            {
                if (g.habitDay == today || g.playId >= 0) continue;
                var here = g.Pawns.Where(p => Together.Able(p) && p.Map != null && p.Map.IsPlayerHome && !MapFacts.ThreatOn(p.Map)).ToList();
                if (here.Count < 2) continue;
                bool kept = false;
                foreach (var habit in g.Habits)
                {
                    foreach (var byMap in here.GroupBy(p => p.Map))
                    {
                        if (!habit.InHours(GenLocalDate.HourOfDay(byMap.Key))) continue;
                        var doing = byMap.Where(p => Doing(habit, g, p, fires)).ToList();
                        if (doing.Count < 2) continue;
                        var circle = Circle(habit, doing);
                        if (circle.Count < 2) continue;
                        Keep(g, habit, circle);
                        kept = true;
                        break;
                    }
                    if (kept) break;
                }
            }
        }

        /// <summary>At leisure: their free time, and doing nothing in particular or something for fun.</summary>
        private static bool Leisure(Pawn p) => Together.FreeTime(p) && p.CurJob?.def != JobDefOf.LayDown;

        /// <summary>Whether this member is doing the habit's thing right now.</summary>
        private static bool Doing(GroupHabitDef habit, SocialGroup g, Pawn p, Dictionary<Map, Thing> fires)
        {
            var job = p.CurJob;
            switch (habit.where)
            {
                case "fire":
                {
                    if (!Leisure(p)) return false;
                    if (!fires.TryGetValue(p.Map, out var fire))
                    {
                        fire = PlaySpots.ColonyFire(p.Map, p);
                        fires[p.Map] = fire;
                    }
                    return fire != null && fire.Spawned && p.Position.InHorDistOf(fire.Position, 9f);
                }
                case "table":
                {
                    if (job?.def != JobDefOf.Ingest) return false;
                    var room = p.GetRoom();
                    return room != null && !room.PsychologicallyOutdoors;
                }
                case "rec":
                    return Leisure(p) && p.GetRoom()?.Role != null && p.GetRoom().Role == DefRefs.Named<RoomRoleDef>("RecRoom");
                case "work":
                {
                    var wt = job?.workGiverDef?.workType;
                    if (wt == null) return false;
                    var works = habit.Works.Count > 0 ? habit.Works : g.kind?.Works;
                    return works != null && works.Contains(wt);
                }
                case "night":
                    return job?.workGiverDef != null || Leisure(p);
                case "prayer":
                {
                    if (job?.def == null) return false;
                    string n = job.def.defName;
                    return job.def.joyKind?.defName == "Meditative" || n.IndexOf("Pray", StringComparison.OrdinalIgnoreCase) >= 0
                           || n.IndexOf("Meditat", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Worship", StringComparison.OrdinalIgnoreCase) >= 0;
                }
                case "outdoors":
                    return Leisure(p) && (p.GetRoom()?.PsychologicallyOutdoors ?? true);
                default:
                    return Leisure(p);
            }
        }

        /// <summary>
        /// The biggest knot of them who are really together (each within talking range of another: Together.Can; at a
        /// table, in the same room too).
        /// </summary>
        private static List<Pawn> Circle(GroupHabitDef habit, List<Pawn> doing)
        {
            var best = new List<Pawn>();
            var seen = new HashSet<Pawn>();
            foreach (var start in doing)
            {
                if (!seen.Add(start)) continue;
                var knot = new List<Pawn> { start };
                for (int k = 0; k < knot.Count; k++)
                {
                    foreach (var o in doing)
                    {
                        if (seen.Contains(o) || !Together.Can(knot[k], o)) continue;
                        if (habit.where == "table" && knot[k].GetRoom() != o.GetRoom()) continue;
                        seen.Add(o);
                        knot.Add(o);
                    }
                }
                if (knot.Count > best.Count) best = knot;
            }
            return best;
        }

        /// <summary>Those of the group in <paramref name="circle"/> keep its habit together.</summary>
        private static void Keep(SocialGroup g, GroupHabitDef habit, List<Pawn> circle)
        {
            // together: the circle is made only of those who are really together right now (Circle, Together.Can).
            g.habitDay = Today;
            g.habitsKept++;
            g.todayHabits++;
            string cause = "CC_Cause_GroupHabit".Translate(habit.label, g.Name).Resolve();
            foreach (var p in circle)
            {
                CouncilGameComponent.GiveThought(p, GroupDefs.Habit, null, -1f, cause);
                Interplay.Ease(p, habit.ease, StrainRelief.Friends);
            }
            var speaker = Speaker(g, circle);
            var other = circle.Where(x => x != speaker).RandomElementWithFallback();
            if (speaker != null && other != null)
            {
                string line = Flavor.FromPool(habit.pool, speaker);
                if (!line.NullOrEmpty()) Speech.Say(speaker, line, other, SpeechColor);
            }
            int kept = g.habitsKept;
            if (kept == 1) g.Note("CC_GroupNote_HabitFirst".Translate(habit.label.Named("HABIT")).Resolve());
            else if (kept % 10 == 0) g.Note("CC_GroupNote_HabitCount".Translate(habit.label.Named("HABIT"), kept.Named("COUNT")).Resolve());
            if ((kept == 1 || kept % 6 == 0) && speaker != null && other != null && GroupDefs.HabitMemory != null)
                SharedMemories.Add(speaker, other, GroupDefs.HabitMemory, habit.label, 0.6f);
        }

        /// <summary>Who speaks at it: the joker or the anchor more often than the rest; the quiet one hardly ever.</summary>
        private static Pawn Speaker(SocialGroup g, List<Pawn> circle) =>
            circle.RandomElementByWeightWithFallback(p =>
            {
                switch (g.RoleOf(p))
                {
                    case GroupRole.Joker: return 3f;
                    case GroupRole.Anchor: return 2f;
                    case GroupRole.Firebrand: return 1.5f;
                    case GroupRole.Quiet: return 0.3f;
                    case GroupRole.Newcomer: return 0.6f;
                    default: return 1f;
                }
            });

        // ---------------- Called to it ----------------

        /// <summary>
        /// Once a day, with "Answers play out on the map": now and then (every week or two for a close group:
        /// <see cref="GroupMath.MayCall"/>, cohesion "close" on its card or more) the anchor calls the group to its habit on
        /// the map, one group at a time. Those who come get the habit's memory when it's over (the play-out holds it until
        /// then).
        /// </summary>
        public static void Daily(GroupTracker t, GroupFacts f)
        {
            var tracker = PlayOutTracker.Instance;
            foreach (var g in t.groups)
            {
                if (g.playId < 0) continue;
                var play = tracker?.Get(g.playId);
                if (play == null || play.state == PlayState.Done) g.playId = -1;
            }
            if (!PlayOuts.On || tracker == null || t.groups.Any(g => g.playId >= 0)) return;
            if (tracker.plays.Count(p => p.state == PlayState.Pending) >= 3) return;
            int now = Now;
            foreach (var g in t.groups.InRandomOrder().ToList())
            {
                // A close group (the card's "close" or "tight-knit"; a loose one was called too, more often than the words said).
                if (!GroupMath.MayCall(g.cohesion, (now - g.lastArranged) / (float)Day) || !Rand.Chance(GroupMath.CallChance)) continue;
                var habit = g.Habits.FirstOrDefault(h => h.Play != null);
                if (habit == null) continue;
                var anchor = g.Holder(GroupRole.Anchor) ?? g.members.OrderByDescending(m => m.attachment).FirstOrDefault()?.pawn;
                var map = anchor?.MapHeld;
                if (anchor == null || map == null || !map.IsPlayerHome || !PlayOuts.OurPerson(anchor)) continue;
                var invited = g.Pawns.Where(p => PlayOuts.OurPerson(p) && p.MapHeld == map).ToList();
                if (invited.Count < 3) continue;
                try
                {
                    var play = new PlayOut
                    {
                        kind = habit.Play.Value,
                        map = map,
                        created = now,
                        label = "CC_Group_PlayLabel".Translate(g.Name.Named("GROUP"), habit.label.Named("HABIT")).Resolve(),
                        invited = invited,
                        organizer = anchor,
                        spots = habit.Spots.Count > 0 ? habit.Spots : new List<PlaySpot> { PlaySpot.Rec, PlaySpot.Dining, PlaySpot.Gather },
                        durationTicks = PlayOutMath.DurationTicks(2f),
                        pool = habit.pool,
                        hourFrom = habit.hourFrom,
                        hourTo = habit.hourTo
                    };
                    if (play.kind == PlayKind.Bonfire)
                    {
                        play.buildFire = true;
                        play.music = true;
                    }
                    play.deadline = PlayOutMath.Deadline(now, GenLocalDate.HourOfDay(map), play.hourFrom, play.hourTo, PlayOutMath.DefaultWaitHours);
                    string cause = "CC_Cause_GroupHabit".Translate(habit.label, g.Name).Resolve();
                    if (GroupDefs.Habit != null)
                        foreach (var x in invited)
                            play.moods.Add(new HeldMood { pawn = x, thought = GroupDefs.Habit, cause = cause });
                    tracker.Add(play);
                    g.playId = play.id;
                    g.lastArranged = now;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("groups: calling a habit", e);
                }
                return;
            }
        }

        /// <summary>A gathering is over (Interplay.Notify_GatheringOver): if a group's anchor called it, the group was together.</summary>
        public static void Notify_GatheringOver(PlayOut play, List<Pawn> attended, bool early)
        {
            if (play == null || !GroupLife.Active) return;
            try
            {
                var t = GroupTracker.Instance;
                var g = t?.groups.FirstOrDefault(x => x.playId == play.id);
                if (g == null) return;
                g.playId = -1;
                var came = attended?.Where(g.Has).ToList() ?? new List<Pawn>();
                if (early || came.Count < 2) return;
                // together: they were at the gathering itself, called by their anchor.
                g.habitDay = Today;
                g.habitsKept++;
                g.todayHabits += 2;
                foreach (var p in came) Interplay.Ease(p, 3f, StrainRelief.Gathering);
                g.Note("CC_GroupNote_Gathered".Translate((play.label ?? "").Named("NAME"), came.Count.Named("COUNT")).Resolve());
                // The gathering's closing words say who of them came and what it did (PlayOutTracker.GatheringEnded tells them).
                play.outcome = "CC_GroupPlay_End".Translate((play.label ?? g.Name).CapitalizeFirst().Named("LABEL"), came.Count.Named("COUNT")).Resolve();
                var writer = came.RandomElement();
                GroupText.Journal(writer, "Rimfolk_Journal_Group_Gathered", g.Name.Named("GROUP"));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("groups: gathering over", e);
            }
        }

        // ---------------- What it is, in words ----------------

        /// <summary>The group whose anchor called this gathering, while it's on; else null.</summary>
        public static SocialGroup CalledBy(PlayOut play)
        {
            if (play == null || !GroupLife.Active) return null;
            var t = GroupTracker.Instance;
            if (t == null) return null;
            foreach (var g in t.groups)
                if (g.playId == play.id)
                    return g;
            return null;
        }

        /// <summary>
        /// A group's gathering as it begins, on the map (PlayOutTracker): who gathers where ("An evening at the fire for the
        /// fireside circle: the fire is lit, and their anchor, Stace, gathers them by the fire."). Null when no group
        /// called it.
        /// </summary>
        public static string BeginText(PlayOut play, string where)
        {
            var g = CalledBy(play);
            if (g == null) return null;
            var anchor = g.Holder(GroupRole.Anchor);
            // together: the one leading them to the place is the gathering's organizer, on the map; their anchor only when it's them.
            bool leads = anchor != null && play.organizer == anchor;
            bool fire = play.kind == PlayKind.Bonfire;
            string label = (play.label ?? g.Name).CapitalizeFirst();
            string text = leads
                ? (fire ? "CC_GroupPlay_Begin_FireAnchor" : "CC_GroupPlay_Begin_Anchor").Translate(label.Named("LABEL"), anchor.Named("PAWN"), (where ?? "").Named("WHERE")).Resolve()
                : (fire ? "CC_GroupPlay_Begin_Fire" : "CC_GroupPlay_Begin").Translate(label.Named("LABEL"), (where ?? "").Named("WHERE")).Resolve();
            return text;
        }

        /// <summary>
        /// Said after it, as it begins: what the group is and what the gathering does for them ("They are friends, and keep
        /// their evenings at the fire together. Those who come feel they belong, and their strain eases a little."). Null
        /// when no group called it.
        /// </summary>
        public static string Does(PlayOut play)
        {
            var g = CalledBy(play);
            return g == null ? null : GroupText.Sentences("CC_GroupPlay_Does".Translate((g.why ?? "").Named("WHY")).Resolve());
        }

        /// <summary>
        /// A group's gathering in This week's list (UI/PlayOutText.cs): its people, what the group is, and what the gathering
        /// does and how often. Null when no group called it.
        /// </summary>
        public static string Tip(PlayOut play)
        {
            var g = CalledBy(play);
            if (g == null) return null;
            var anchor = g.Holder(GroupRole.Anchor);
            string who = "CC_GroupPlay_Who".Translate(g.Name.CapitalizeFirst().Named("GROUP"), GroupText.Members(g).Named("NAMES"), (g.why ?? "").Named("WHY")).Resolve();
            string often = anchor != null
                ? "CC_GroupPlay_Often".Translate(anchor.Named("PAWN")).Resolve()
                : "CC_GroupPlay_OftenNoAnchor".Translate().Resolve();
            return GroupText.Sentences(who) + "\n" + often;
        }

        /// <summary>
        /// What a group does, told once with its founding (GroupLife.Form): keeping its habits together gives them a sense
        /// of belonging and eases their strain; with "Answers play out on the map", its anchor calls them all to its first
        /// habit that can be called (as <see cref="Daily"/> does) every week or two while it's close.
        /// </summary>
        public static string Formed(SocialGroup g)
        {
            if (g == null) return null;
            var habits = g.Habits;
            if (habits.Count == 0) return null;
            string text = "CC_Group_Formed_Does".Translate().Resolve();
            var called = habits.FirstOrDefault(h => h.Play != null);
            if (called == null || !PlayOuts.On) return text;
            var holder = g.Holder(GroupRole.Anchor);
            return text + " " + (holder != null
                ? "CC_Group_Formed_Calls".Translate(holder.Named("PAWN"), called.label.Named("HABIT")).Resolve()
                : "CC_Group_Formed_CallsNoAnchor".Translate(called.label.Named("HABIT")).Resolve());
        }

        /// <summary>A habit's pill on the group's card: what keeping it does, and whether the anchor calls them to it.</summary>
        public static string HabitTip(SocialGroup g, GroupHabitDef h)
        {
            var lines = new List<string> { h.description, "", "CC_Group_HabitDoes".Translate().Resolve() };
            if (h.Play != null && PlayOuts.On && g.Habits.FirstOrDefault(x => x.Play != null) == h) lines.Add("CC_Group_HabitCalled".Translate().Resolve());
            lines.Add("CC_Group_HabitTip".Translate(g.habitsKept).Resolve());
            return string.Join("\n", lines);
        }
    }
}
