using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Two councils become one (docs/design/stage3-many-colonies.md, 3b; the player's decision 12A). When a colony's home is
    /// lost (given up or handed over) and its people come to live at another home that has people and a council of its own,
    /// then once they have lived a day there, on the first evening: what they kept merges into the colony's (their traditions
    /// join at half their strength, one both kept keeps the stronger), the council is chosen again within the season when they
    /// are a third or more of the grown-ups, the names they were known by are looked at again in the next naming round (the
    /// better keeps one both colonies had, and no rival comes of it in the first season), their old colony's founding day is
    /// kept as a remembrance while some of them live there, and the colony sits down to a meal together to welcome them, its
    /// speaker's greeting at the table. A milestone, colony:merged, and one letter that says what merged. 7a's rules already
    /// make a moving officer step down and keep the old council thirty days; this builds on them. Nothing per tick: the
    /// council's hour, after the homes (Homes.Look). With one colony nothing is ever noted.
    /// </summary>
    public static class Merges
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>The setting "Two councils become one" (Many colonies).</summary>
        public static bool On => S?.manyMerge ?? true;

        /// <summary>The pool of what is said at the welcome's table, and the speaker's greeting (Defs/FlavorPoolDefs/Rimfolk_ManyColonies.xml).</summary>
        public const string TablePool = "Rimfolk_Pool_MergeWelcome", GreetingPool = "Rimfolk_Pool_MergeGreeting";

        // ================= A home lost =================

        /// <summary>Homes lost (Homes.Changed): each one with another home left, and people of its own, is noted for a merge.</summary>
        public static void HomesLost(IReadOnlyList<int> lost)
        {
            var h = Homes.Instance;
            if (h == null || lost == null || lost.Count == 0 || !On || Homes.All.Count == 0) return;
            int now = Find.TickManager.TicksGame;
            foreach (int key in lost)
            {
                if (key == HomesMath.None || h.merges.Any(m => m != null && m.from == key && !m.Merged)) continue;
                var people = h.PeopleOfLost(key, now);
                if (people.Count == 0) continue;
                // Only with another home of people of its own (a gravship's new home, or a colony that moved, is theirs alone).
                var set = new HashSet<Pawn>(people);
                if (!Homes.All.Any(m => Homes.People(m).Any(p => !set.Contains(p) && p.DevelopmentalStage.Adult()))) continue;
                var rec = WorldLedger.ColonyRecord(key);
                string name = rec?.name;
                if (name.NullOrEmpty()) name = Find.WorldObjects.AllWorldObjects.FirstOrDefault(o => o != null && o.ID == key)?.LabelCap ?? "?";
                int founded = rec?.founded ?? CalendarTracker.Instance?.colonies?.FirstOrDefault(c => c != null && c.home == key)?.FoundedTick ?? -1;
                h.merges.Add(new ColonyMerge { from = key, fromName = name, lostAt = now, founded = founded, people = people });
                while (h.merges.Count > ManyColoniesMath.MaxMerges) h.merges.RemoveAt(0);
            }
        }

        // ================= The council's hour =================

        /// <summary>The council's hour (after the homes): a merge that can happen now does; one whose season is over is let go.</summary>
        public static void Hourly(Homes h, int now)
        {
            if (h?.merges == null || h.merges.Count == 0) return;
            foreach (var m in h.merges.ToList())
            {
                try
                {
                    if (!m.Merged) Look(h, m, now);
                    else Due(m, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("merges: a merge", e);
                    h.merges.Remove(m);
                }
            }
            h.merges.RemoveAll(m => m == null || m.Merged && Over(m, now));
        }

        /// <summary>Done with: its first season over, its names looked at, its election called.</summary>
        private static bool Over(ColonyMerge m, int now) =>
            !ManyColoniesMath.QuietSeason(now, m.mergedAt) && ((m.namesLooked || !S.epithets) && m.electionAt < 0 || now - m.mergedAt > 2 * ManyColoniesMath.SeasonTicks);

        /// <summary>A merge not yet made: where its people live a day, and whether it is one; made at the first evening there.</summary>
        private static void Look(Homes h, ColonyMerge m, int now)
        {
            var comp = CouncilGameComponent.Instance;
            if (!On || comp == null || ManyColoniesMath.Lapsed(now, m.lostAt))
            {
                h.merges.Remove(m);
                return;
            }
            m.people.RemoveAll(p => p == null || p.Dead || !People.StillOurs(p));
            if (m.people.Count == 0)
            {
                h.merges.Remove(m);
                return;
            }
            var homes = new HashSet<int>(Homes.All.Select(Homes.KeyOf));
            int into = ManyColoniesMath.MergedInto(m.from, m.people.Select(h.LastKeyOf).ToList(), homes);
            var map = Homes.MapOf(into);
            if (map == null) return;
            var newcomers = m.people.Where(p => h.LastKeyOf(p) == into).ToList();
            var set = new HashSet<Pawn>(m.people);
            int hosts = Homes.People(map).Count(p => !set.Contains(p) && p.DevelopmentalStage.Adult());
            var mapRec = WorldLedger.ColonyRecord(into);
            bool founding = mapRec != null && ManyColoniesMath.FoundedLately(mapRec.founded, now, mapRec.mother, m.from);
            if (!ManyColoniesMath.IsMerge(hosts, comp.ColonyByKey(m.from) != null, founding))
            {
                // Their colony moved (nobody of the home's own), or its council is gone: 7a's rules, and nothing more.
                h.merges.Remove(m);
                return;
            }
            // Those of them still on the road and a fight on the map wait (three days at most); the welcome's meal waits for
            // the evening by its own hours.
            bool late = now - m.lostAt > ManyColoniesMath.WelcomeWaitDays * ManyColoniesMath.DayTicks;
            if (!late && (MapFacts.ThreatOn(map) || m.people.Any(p => p.GetCaravan() != null))) return;
            Merge(comp, m, map, newcomers, now);
        }

        // ================= The two become one =================

        /// <summary>What merges, the welcome, the milestone and the letter.</summary>
        private static void Merge(CouncilGameComponent comp, ColonyMerge m, Map map, List<Pawn> newcomers, int now)
        {
            m.into = Homes.KeyOf(map);
            m.mergedAt = now;
            m.newcomers = newcomers;
            string place = Homes.Name(map);
            string old = m.fromName ?? "?";
            var lines = new List<string>();
            // Traditions: one only they kept joins at half its strength; one both kept keeps the stronger.
            try
            {
                var theirs = CalendarTracker.Instance?.colonies?.FirstOrDefault(c => c != null && c.home == m.from);
                var joined = ColonyCalendar.For(map)?.MergeFrom(theirs, newcomers, map);
                if (joined != null && joined.Count > 0)
                    lines.Add("CC_Many_MergedTraditions".Translate(TextUtil.JoinAnd(joined).Named("TRADITIONS")).Resolve());
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("merges: the traditions", e);
            }
            // The election: when they are a third or more of the grown-ups, the council is chosen again within the season.
            int theirGrown = newcomers.Count(p => p.DevelopmentalStage.Adult());
            int allGrown = Homes.People(map).Count(p => p.DevelopmentalStage.Adult());
            bool early = S.offices && ManyColoniesMath.EarlyElection(theirGrown, allGrown);
            m.electionAt = ManyColoniesMath.ElectionAt(now, early);
            if (early) lines.Add("CC_Many_MergedElection".Translate().Resolve());
            // The names they were known by: looked at again in the next naming round (EpithetTracker.Merged).
            m.namesLooked = !S.epithets;
            if (S.epithets && newcomers.Any(p => comp.epithets.Of(p).Any())) lines.Add("CC_Many_MergedNames".Translate(old.Named("OLD")).Resolve());
            // Their old colony's founding day: a remembrance here while some of them live here.
            try
            {
                if (ColonyCalendar.On && ColonyCalendar.Notify_Merged(map, old, m.founded, newcomers))
                    lines.Add("CC_Many_MergedDay".Translate(old.Named("OLD")).Resolve());
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("merges: the old founding day", e);
            }
            // The welcome: a meal together this first evening, the speaker's greeting at the table.
            bool meal = Welcome(map, old, newcomers);
            lines.Add((meal ? "CC_Many_MergedMeal" : "CC_Many_MergedGreeting").Translate(old.Named("OLD")).Resolve());
            // The milestone, and the one letter.
            var actors = newcomers.Select(p => new HistoryActor(Refs.Of(p), HistoryRole.Did)).ToList();
            actors.Add(new HistoryActor(Ref.Colony(m.from), HistoryRole.About));
            string history = "CC_Many_MergedHistory".Translate(old.Named("OLD"), place.Named("PLACE")).Resolve();
            History.Add(FoundingMath.KindMerged, actors, Refs.Colony(map), HistoryMath.Milestone, history);
            string text = TextVariants.Vary("CC_Many_MergedText").Translate(old.Named("OLD"), place.Named("PLACE"), Foundings.Names(newcomers).Named("NAMES")).Resolve();
            if (lines.Count > 0) text += "\n\n" + string.Join(" ", lines);
            Tell.Letter(NewsKind.Colony, Importance.Important, map, "CC_Many_MergedLabel".Translate(old.Named("OLD"), place.Named("PLACE")).Resolve(), text,
                LetterDefOf.PositiveEvent, new LookTargets(newcomers.Where(p => p.Spawned && p.Map == map)));
            LensModel.Dirty();
            Cards.Dirty();
        }

        /// <summary>
        /// The welcome: with answers played out, a meal together at the colony's table this evening for everyone at home, the
        /// speaker (else the eldest of the colony's own) its host, whose greeting goes with the cup; else the greeting alone,
        /// in a bubble. True when the meal was set going.
        /// </summary>
        private static bool Welcome(Map map, string old, List<Pawn> newcomers)
        {
            var home = map.mapPawns.FreeColonistsSpawned.Where(p => PlayOuts.OurPerson(p) && !p.Downed && !p.InMentalState).ToList();
            var set = new HashSet<Pawn>(newcomers);
            var speaker = ColonyState.For(map)?.offices?.Speaker;
            if (speaker == null || !home.Contains(speaker))
                speaker = home.Where(p => !set.Contains(p) && p.DevelopmentalStage.Adult()).OrderByDescending(p => p.ageTracker.AgeBiologicalYearsFloat).FirstOrDefault()
                          ?? home.FirstOrDefault(p => p.DevelopmentalStage.Adult());
            if (speaker == null) return false;
            string greeting = Flavor.FromPool(GreetingPool, speaker, null, null, "speaker");
            greeting = greeting.NullOrEmpty() ? null : greeting.Formatted(old.Named("OLD"), Homes.Name(map).Named("PLACE")).Resolve();
            string cause = "CC_Many_WelcomeCause".Translate(old.Named("OLD")).Resolve();
            using (Causes.Push(cause))
            {
                var play = PlayFeast.Stage(new FeastOrder
                {
                    why = FeastWhy.Answer, map = map, label = "CC_Many_WelcomeLabel".Translate(old.Named("OLD")).Resolve(), invited = home, organizer = speaker,
                    hours = new IntRange(ManyColoniesMath.WelcomeFrom, 23), pool = TablePool, toast = true, toastLine = greeting, music = true
                });
                if (play != null) return true;
            }
            // apart: with answers not played out on the map, the welcome is the speaker's word, as any gathering is then.
            if (greeting != null && Speech.On) Speech.Say(speaker, greeting);
            return false;
        }

        /// <summary>A merge made: its early election when it falls due (none already under way).</summary>
        private static void Due(ColonyMerge m, int now)
        {
            if (m.electionAt < 0 || now < m.electionAt) return;
            var map = Homes.MapOf(m.into);
            var offices = map != null ? ColonyState.For(map)?.offices : null;
            var comp = CouncilGameComponent.Instance;
            if (offices == null || comp == null || !S.offices)
            {
                m.electionAt = -1;
                return;
            }
            if (offices.election != null) return;
            // One already held since the merge (the year's own, or a by-election) gave them their say.
            if (ManyColoniesMath.ElectionDue(now, m.electionAt, offices.lastElectionTick)) offices.Call(comp, GenLocalDate.Year(map), false);
            m.electionAt = -1;
        }

        // ================= What reads it =================

        /// <summary>A merge into this colony in its first season: a name taken over there makes no rival (ManyColoniesMath.QuietSeason).</summary>
        public static bool Quiet(int colony, int now)
        {
            var h = Homes.Instance;
            if (h?.merges == null || h.merges.Count == 0) return false;
            foreach (var m in h.merges)
                if (m != null && m.Merged && m.into == colony && ManyColoniesMath.QuietSeason(now, m.mergedAt))
                    return true;
            return false;
        }

        /// <summary>The naming round (EpithetTracker.Daily): the names under a merged colony's key are looked at again, once.</summary>
        public static void NamesRound(EpithetTracker names, CouncilGameComponent comp)
        {
            var h = Homes.Instance;
            if (h?.merges == null || h.merges.Count == 0 || names == null) return;
            foreach (var m in h.merges)
            {
                if (m == null || !m.Merged || m.namesLooked) continue;
                m.namesLooked = true;
                try
                {
                    names.Merged(m.from, m.into, m.newcomers, comp);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("merges: the names", e);
                }
            }
        }

        // ================= Dev tools =================

        /// <summary>Dev mode: a colony's council merged into this home now, its people as the newcomers (the waits skipped).</summary>
        public static string DebugMerge(Map into, int from, string fromName, List<Pawn> people)
        {
            var h = Homes.Instance;
            var comp = CouncilGameComponent.Instance;
            if (h == null || comp == null || into == null || people.NullOrEmpty()) return "no merge (no home or nobody to come)";
            int now = Find.TickManager.TicksGame;
            var m = new ColonyMerge { from = from, fromName = fromName, lostAt = now, people = people };
            h.merges.Add(m);
            Merge(comp, m, into, people, now);
            return "merged " + fromName + " into " + Homes.Name(into) + " (" + people.Count + " newcomers)";
        }
    }
}
