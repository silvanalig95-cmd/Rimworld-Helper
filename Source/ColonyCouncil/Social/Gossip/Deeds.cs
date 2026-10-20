using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Records what colonists do that people talk about (<see cref="DeedDef"/>). Each entry point below is called from
    /// where the game or Rimfolk already notices the event, in one line: the fight's end (WorldReactions.Victory), a
    /// contest (Moments), a secret out (Secrets), an election (Offices), a fine piece made (Patch_ArcMade), a council
    /// ambition reached, an encounter with a stranger, a council meeting someone didn't bother with; and through its own
    /// light hooks (DeedPatches.cs) a memory given (rescued me, harmed me, cheated on me...), a mental break, a history
    /// event (an execution, an organ taken) and a social fight. Whoever saw it knows of it from the start; everyone else
    /// hears of it in gossip (<see cref="Gossip"/>).
    /// </summary>
    public static class Deeds
    {
        private static bool On => Reputations.On;

        private static int Now => Find.TickManager.TicksGame;

        public static DeedDef Named(string defName) => defName.NullOrEmpty() ? null : DefDatabase<DeedDef>.GetNamedSilentFail(defName);

        // ---------------- The defs' own triggers, read once ----------------

        private static bool looked;
        private static readonly Dictionary<ThoughtDef, List<DeedDef>> byMemory = new Dictionary<ThoughtDef, List<DeedDef>>();
        private static readonly Dictionary<HistoryEventDef, List<DeedDef>> byHistory = new Dictionary<HistoryEventDef, List<DeedDef>>();
        private static readonly Dictionary<MentalStateDef, List<DeedDef>> byBreak = new Dictionary<MentalStateDef, List<DeedDef>>();
        private static readonly Dictionary<string, DeedDef> byEncounter = new Dictionary<string, DeedDef>();

        private static void Look()
        {
            if (looked) return;
            looked = true;
            void Index<T>(Dictionary<T, List<DeedDef>> index, List<string> names, DeedDef d) where T : Def
            {
                if (names == null) return;
                foreach (var n in names)
                {
                    var def = DefDatabase<T>.GetNamedSilentFail(n ?? "");
                    if (def == null) continue;
                    if (!index.TryGetValue(def, out var list)) index[def] = list = new List<DeedDef>();
                    list.Add(d);
                }
            }
            foreach (var d in DeedDef.All)
            {
                Index(byMemory, d.memories, d);
                Index(byHistory, d.historyEvents, d);
                Index(byBreak, d.mentalStates, d);
                if (d.encounters != null)
                    foreach (var e in d.encounters)
                        if (!e.NullOrEmpty()) byEncounter[e] = d;
            }
        }

        // ---------------- Recording ----------------

        /// <summary>
        /// <paramref name="doer"/> did it (to <paramref name="victim"/>, if anyone): kept with its size (the def's own when
        /// not given), a name for when it was, and who knows of it from the start: the victim, the
        /// <paramref name="witnesses"/>, and (with <paramref name="seen"/>) whoever of the colony was close enough to see
        /// it. The same deed again soon after grows the one already kept. Null when nothing was kept (the setting off, or
        /// the doer isn't one of the colony's own).
        /// </summary>
        public static Deed Record(DeedDef def, Pawn doer, Pawn victim = null, float size = -1f, string label = null, string detail = null,
            IEnumerable<Pawn> witnesses = null, bool seen = true)
        {
            var tr = Reputations.Tracker;
            if (!On || tr == null || def == null || !Reputations.Public(doer)) return null;
            if (victim == doer) victim = null;
            try
            {
                int now = Now;
                float s = size > 0f ? size : def.size;
                int victimId = victim?.thingIDNumber ?? -1;
                Deed d = null;
                if (def.mergeDays > 0f)
                    foreach (var x in tr.By(doer.thingIDNumber))
                        if (x.def == def && x.victim == victimId && now - x.lastTick >= 0 && now - x.lastTick < def.mergeDays * GenDate.TicksPerDay)
                        {
                            d = x;
                            break;
                        }
                if (d != null)
                {
                    d.size = RepMath.Merged(d.size, s, def.mergeShare);
                    d.lastTick = now;
                    tr.Changed();
                }
                else
                {
                    d = new Deed
                    {
                        def = def,
                        doer = doer.thingIDNumber,
                        doerName = doer.LabelShort,
                        victim = victimId,
                        victimName = victim?.LabelShort,
                        tick = now,
                        lastTick = now,
                        label = label ?? LabelFor(def, doer),
                        detail = detail,
                        size = Mathf.Min(s, RepMath.MaxSize)
                    };
                    tr.Keep(d);
                }
                if (victim != null && Reputations.Public(victim)) tr.Learn(d, victim, null, d.size, 1f, Slant.Plain);
                if (witnesses != null)
                    foreach (var w in witnesses.ToList())
                        if (w != doer && Reputations.Public(w)) tr.Learn(d, w, null, d.size, 1f, Slant.Plain);
                if (seen)
                    foreach (var w in Onlookers(doer))
                        tr.Learn(d, w, null, d.size, 1f, Slant.Plain);
                return d;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("deed " + def.defName, e);
                return null;
            }
        }

        public static Deed Record(string defName, Pawn doer, Pawn victim = null, float size = -1f, string label = null, string detail = null,
            IEnumerable<Pawn> witnesses = null, bool seen = true) =>
            On ? Record(Named(defName), doer, victim, size, label, detail, witnesses, seen) : null;

        /// <summary>The colony's people close enough to <paramref name="doer"/> to see what they did: in the same room, or in sight not far off.</summary>
        public static List<Pawn> Onlookers(Pawn doer, float range = 13f)
        {
            var list = new List<Pawn>();
            if (doer?.Map == null || !doer.Spawned) return list;
            foreach (var w in doer.Map.mapPawns.FreeColonistsSpawned.ToList())
                if (w != doer && Reputations.Public(w) && Sees(w, doer, range))
                    list.Add(w);
            return list;
        }

        /// <summary>Awake, on the same map, within <paramref name="range"/>, and in the same room or in sight (drafted or not).</summary>
        internal static bool Sees(Pawn w, Pawn doer, float range)
        {
            if (w == null || doer == null || !w.Spawned || !doer.Spawned || w.Map != doer.Map || !w.Awake()) return false;
            if (!w.Position.InHorDistOf(doer.Position, range)) return false;
            try
            {
                var room = w.GetRoom();
                if (room != null && room == doer.GetRoom()) return true;
                return GenSight.LineOfSight(w.Position, doer.Position, w.Map, true);
            }
            catch
            {
                return false;
            }
        }

        // ---------------- Names for when it happened ----------------

        private static string DateNow(Map map) => FightNames.DateNow(map);

        /// <summary>A fight on the doer's map (a threat on it, or the fight the colony is in still going).</summary>
        public static bool FightOn(Map map)
        {
            if (map == null) return false;
            if (MapFacts.ThreatOn(map)) return true;
            var social = CouncilGameComponent.Instance?.social;
            return social != null && social.battleActive && social.battleMap == map;
        }

        /// <summary>Food running short: a few days' worth or less at home, or a hunger in the world.</summary>
        public static bool Hungry(Map map)
        {
            if (map != null && map.IsPlayerHome && MapFacts.Of(map).FoodDays < 2.5f) return true;
            return LifeConditions.Situation(HungerKinds);
        }

        private static readonly List<string> HungerKinds = new List<string> { "hunger" };

        /// <summary>
        /// The fight's name, the same for everything done in it and wherever else it is told (marks, shared memories):
        /// "the raid on the 5th of Aprimay", "the mechanoid attack on...". See <see cref="FightNames"/>.
        /// </summary>
        public static string FightLabel(Map map) => FightNames.Of(map);

        /// <summary>A name for when a deed happened: its own pattern ("the tantrum on {DATE}"), or the fight's when it was done in one.</summary>
        public static string LabelFor(DeedDef def, Pawn doer)
        {
            var map = doer?.MapHeld;
            if (def.duringFight) return FightLabel(map);
            string pattern = def.labelText.NullOrEmpty() ? "CC_Rep_Label_Day".Translate().ToString() : def.labelText;
            return pattern.Formatted(DateNow(map).Named("DATE")).Resolve();
        }

        /// <summary>
        /// What they did, in words: the def's text with {SUBJECT_...} the doer, {VICTIM} the other person in it (by name)
        /// and {DETAIL} the hook's detail; without the victim's part when there was none.
        /// </summary>
        public static string TextOf(Deed d, Pawn subject = null)
        {
            if (d?.def == null) return "";
            string text = d.victimName.NullOrEmpty() && !d.def.textAlone.NullOrEmpty() ? d.def.textAlone : d.def.text;
            subject ??= Reputations.Tracker?.PawnOf(d.doer);
            var args = new[]
            {
                subject != null ? subject.Named("SUBJECT") : (d.doerName ?? "?").Named("SUBJECT"),
                (d.victimName ?? "").Named("VICTIM"),
                (d.detail ?? "").Named("DETAIL")
            };
            try
            {
                return text.Formatted(args).Resolve();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("deed text " + d.def.defName, e);
                return d.def.label ?? "";
            }
        }

        // ---------------- Hooks: where Rimfolk already notices the event ----------------

        /// <summary>
        /// A fight at home seen off (WorldReactions.Victory): those who brought down three or more are talked of, and
        /// in a hard fight (one of ours fell, or six or more enemies did) everyone who held their ground (not those who
        /// broke and ran in it).
        /// </summary>
        public static void Victory(List<Pawn> fighters, Faction foe, int kills, List<string> fallen, Dictionary<Pawn, int> killsBy)
        {
            if (!On || fighters.NullOrEmpty()) return;
            try
            {
                if (kills < 3 && !(foe != null && kills >= 1)) return;
                var map = fighters.FirstOrDefault(p => p != null && p.Spawned)?.Map;
                string label = FightLabel(map);
                var many = Named("Rimfolk_Deed_ManyKills");
                if (killsBy != null)
                    foreach (var kv in killsBy)
                        if (kv.Key != null && kv.Value >= 3)
                            Record(many, kv.Key, null, Mathf.Min(RepMath.MaxSize, kv.Value / 3f), label, kv.Value.ToString(), fighters, false);
                bool hard = (fallen?.Count ?? 0) > 0 || kills >= 6;
                var held = Named("Rimfolk_Deed_HeldLine");
                var fled = Named("Rimfolk_Deed_Fled");
                var tr = Reputations.Tracker;
                if (!hard || held == null || tr == null) return;
                foreach (var p in fighters)
                {
                    if (!Reputations.Public(p) || p.Downed) continue;
                    if (tr.By(p.thingIDNumber).Any(d => d.def == fled && d.label == label)) continue;
                    Record(held, p, null, -1f, label, null, fighters, false);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("deeds: victory", e);
            }
        }

        /// <summary>A friendly contest decided (Moments): the winner beat the loser, and whoever watched saw it.</summary>
        public static void Contest(Pawn winner, Pawn loser, SkillDef skill)
        {
            if (!On || winner == null || loser == null) return;
            string label = "CC_Rep_Label_Contest".Translate(DateNow(winner.MapHeld).Named("DATE")).Resolve();
            // What the contest was ("arm-wrestling", "storytelling"), by the skill that decided it.
            string key = "CC_Rep_Contest_" + (skill?.defName ?? "");
            string what = key.CanTranslate() ? key.Translate().ToString() : "CC_Rep_Contest_Other".Translate().ToString();
            Record("Rimfolk_Deed_ContestWon", winner, loser, -1f, label, what);
            Record("Rimfolk_Deed_ContestLost", loser, winner, -1f, label, what);
        }

        /// <summary>
        /// A secret came out (Secrets.Reveal): a confidant who told it betrayed it; everyone else who knew and kept it
        /// kept it (which its owner can now tell). A good secret coming out is neither.
        /// </summary>
        public static void SecretOut(Secret s, Pawn leaker, bool confessed)
        {
            var owner = s?.owner;
            if (!On || owner == null || s.def == null || s.def.good) return;
            try
            {
                float size = s.def.major ? 1.5f : 1f;
                if (!confessed && leaker != null && s.knowers.Contains(leaker)) Record("Rimfolk_Deed_Betrayed", leaker, owner, size);
                foreach (var k in s.knowers.ToList())
                    if (k != leaker && k != owner && Reputations.Public(k))
                        Record("Rimfolk_Deed_KeptSecret", k, owner, size, null, null, null, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("deeds: secret", e);
            }
        }

        /// <summary>Voted into an office (Offices): everyone who voted knows.</summary>
        public static void Elected(Pawn winner, OfficeDef office, List<Pawn> voters)
        {
            if (!On || winner == null || office == null) return;
            Record("Rimfolk_Deed_Elected", winner, null, -1f, null, office.label, voters, false);
        }

        /// <summary>Something made at masterwork quality or better (Patch_ArcMade): a legendary piece counts double.</summary>
        public static void Made(Pawn worker, Thing thing)
        {
            if (!On || worker == null || thing == null || !thing.TryGetQuality(out var q) || q < QualityCategory.Masterwork) return;
            string detail = "CC_Rep_MadeDetail".Translate(q.GetLabel().Named("QUALITY"), (thing.def?.label ?? "").Named("THING")).Resolve();
            Record("Rimfolk_Deed_Masterwork", worker, null, q >= QualityCategory.Legendary ? 2f : 1f, null, detail);
        }

        /// <summary>A council ambition reached (Ambitions): the one who pushed for it gets the credit, and everyone at home knows.</summary>
        public static void GreatWork(Pawn proposer, string ambition, Map map)
        {
            if (!On || proposer == null || proposer.Dead) return;
            Record("Rimfolk_Deed_GreatWork", proposer, null, -1f, null, ambition, map?.mapPawns.FreeColonistsSpawned.ToList(), false);
        }

        /// <summary>A moment with a stranger (Encounters): a kindness or a cruelty, when its def is one a deed lists.</summary>
        public static void Encounter(EncounterDef def, Pawn colonist, Pawn stranger)
        {
            if (!On || def == null || colonist == null) return;
            Look();
            if (!byEncounter.TryGetValue(def.defName, out var deed)) return;
            Record(deed, colonist, stranger);
        }

        /// <summary>The council met, and someone free to come didn't bother (MeetingAttendance): those who were there know.</summary>
        public static void MeetingSkipped(CouncilMeeting m)
        {
            if (!On || m?.invites == null) return;
            try
            {
                var deed = Named("Rimfolk_Deed_SkippedMeeting");
                foreach (var inv in m.invites)
                {
                    var p = inv?.pawn;
                    if (p == null || inv.status != MeetingStatus.Skipped || inv.Arrived || m.Present(p)) continue;
                    Record(deed, p, null, -1f, null, null, m.attendees, false);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("deeds: meeting", e);
            }
        }

        // ---------------- Hooks: the game's own events (DeedPatches.cs) ----------------

        private static bool Fits(DeedDef d, Pawn doer, Pawn victim)
        {
            var map = doer.MapHeld;
            if (d.duringFight && !FightOn(map)) return false;
            if (d.notInFight && FightOn(map)) return false;
            if (d.doerInBreak && !doer.InAggroMentalState) return false;
            if (d.victimOutsider && (victim == null || Reputations.Public(victim))) return false;
            if (d.duringHunger && !Hungry(map)) return false;
            return true;
        }

        /// <summary>A memory given to <paramref name="holder"/> about <paramref name="other"/> (rescued me, harmed me, cheated on me): the other did it.</summary>
        public static void Notify_Memory(Pawn holder, ThoughtDef def, Pawn other)
        {
            if (!On || def == null || other == null || holder == null || holder == other) return;
            Look();
            if (!byMemory.TryGetValue(def, out var list) || !Reputations.Public(other)) return;
            foreach (var d in list)
                if (Fits(d, other, holder))
                    Record(d, other, holder);
        }

        /// <summary>A colonist started a mental break: some breaks are deeds (a tantrum, running from a fight, a binge in a hunger).</summary>
        public static void Notify_Break(Pawn p, MentalStateDef state, Pawn other, bool wasDrafted)
        {
            if (!On || p == null || state == null) return;
            Look();
            if (!byBreak.TryGetValue(state, out var list) || !Reputations.Public(p)) return;
            var victim = other ?? TargetOf(p.MentalState);
            foreach (var d in list)
            {
                if (!Fits(d, p, victim)) continue;
                Record(d, p, victim, wasDrafted && d.draftedSize > 0f ? d.draftedSize : -1f);
            }
        }

        /// <summary>The person a break is aimed at, when there is one (a murderous rage, an insulting spree, a tantrum at someone).</summary>
        private static Pawn TargetOf(MentalState s)
        {
            switch (s)
            {
                case MentalState_MurderousRage rage: return rage.target;
                case MentalState_InsultingSpree spree: return spree.target;
                case MentalState_Tantrum tantrum: return tantrum.target as Pawn;
                default: return null;
            }
        }

        /// <summary>A history event naming a colonist as its doer (an execution, an organ taken from someone held).</summary>
        public static void Notify_History(HistoryEvent e)
        {
            if (!On || e.def == null) return;
            Look();
            if (!byHistory.TryGetValue(e.def, out var list)) return;
            if (!e.args.TryGetArg(HistoryEventArgsNames.Doer, out Pawn doer) || !Reputations.Public(doer)) return;
            e.args.TryGetArg(HistoryEventArgsNames.Victim, out Pawn victim);
            foreach (var d in list)
                if (Fits(d, doer, victim))
                    Record(d, doer, victim);
        }

        /// <summary>A colonist threw the first punch (a social fight): seen by whoever was there.</summary>
        public static void Notify_SocialFight(Pawn starter, Pawn other)
        {
            if (!On || starter == null || other == null) return;
            Record("Rimfolk_Deed_StartedFight", starter, other);
        }

        /// <summary>A new game or a load: the last fight's name is let go.</summary>
        public static void Reset() => FightNames.Reset();
    }
}
