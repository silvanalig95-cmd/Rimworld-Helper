using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>Where the stranger's stay stands.</summary>
    public enum StrangerPhase
    {
        /// <summary>At the gate, waiting for the colony's answer.</summary>
        Waiting,
        /// <summary>Let in, or let sleep by the gate: the stay itself.</summary>
        Staying,
        /// <summary>On their own way off the map (Felt, or turned away): the end comes once they are gone.</summary>
        Leaving,
        /// <summary>Stopped and shown for who they are (Walking): standing a moment before they walk off.</summary>
        Farewell,
        /// <summary>Walking off the map as a god.</summary>
        WalkingOff,
        /// <summary>Struck by the colony: the god's anger comes at the next step.</summary>
        Struck,
        /// <summary>Shown in its anger (Walking): a breath, then gone in a flash.</summary>
        Vanishing
    }

    /// <summary>
    /// The stranger at the gate while they stay (Gods/OnMap/GodStranger.cs): who they are and which god, the colony's
    /// answer, the stay's clock, and what the colony did for them (the tally the end is judged by, and each colonist's
    /// kindness).
    /// </summary>
    public class StrangerStay : IExposable
    {
        public Pawn x;
        public int god = -1;
        public Map map;
        public Faction faction;
        public StrangerWelcome welcome = StrangerWelcome.Gate;
        public bool answered;
        public StrangerPhase phase = StrangerPhase.Waiting;
        public int since, leaveAt, phaseDue = -1;
        /// <summary>The one who went out to them at the gate (the letter's A).</summary>
        public Pawn letIn;
        /// <summary>Where they waited at the gate, and where they slept.</summary>
        public IntVec3 gate = IntVec3.Invalid, place = IntVec3.Invalid;
        // The tally.
        public float contentSum;
        public int contentN, ate, slept = -2, kindnesses, slaughterBase = -1, slaughtered, itemsWhenDown = -1;
        public bool animalsNeglected, harmed, robbed, seenStaying;
        public Pawn harmer;
        /// <summary>Each colonist's kindness to them, side by side; and who talked with them.</summary>
        public List<Pawn> kind = new List<Pawn>();
        public List<int> points = new List<int>();
        public List<Pawn> talkers = new List<Pawn>();
        /// <summary>The play log looked at up to here, and when the stranger last said something.</summary>
        public int logSeen = -1, lastSaid = -1;
        public bool thanked;

        public God God => Gods.ById(god);

        public void Credit(Pawn p, int by)
        {
            if (p == null || by == 0 || !p.IsColonist) return;
            int i = kind.IndexOf(p);
            if (i < 0)
            {
                kind.Add(p);
                points.Add(by);
            }
            else points[i] += by;
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref x, "x");
            Scribe_Values.Look(ref god, "god", -1);
            Scribe_References.Look(ref map, "map");
            Scribe_References.Look(ref faction, "faction");
            Scribe_Values.Look(ref welcome, "welcome", StrangerWelcome.Gate);
            Scribe_Values.Look(ref answered, "answered");
            Scribe_Values.Look(ref phase, "phase", StrangerPhase.Waiting);
            Scribe_Values.Look(ref since, "since");
            Scribe_Values.Look(ref leaveAt, "leaveAt");
            Scribe_Values.Look(ref phaseDue, "phaseDue", -1);
            Scribe_References.Look(ref letIn, "letIn");
            Scribe_Values.Look(ref gate, "gate", IntVec3.Invalid);
            Scribe_Values.Look(ref place, "place", IntVec3.Invalid);
            Scribe_Values.Look(ref contentSum, "contentSum");
            Scribe_Values.Look(ref contentN, "contentN");
            Scribe_Values.Look(ref ate, "ate");
            Scribe_Values.Look(ref slept, "slept", -2);
            Scribe_Values.Look(ref kindnesses, "kindnesses");
            Scribe_Values.Look(ref slaughterBase, "slaughterBase", -1);
            Scribe_Values.Look(ref slaughtered, "slaughtered");
            Scribe_Values.Look(ref itemsWhenDown, "itemsWhenDown", -1);
            Scribe_Values.Look(ref animalsNeglected, "animalsNeglected");
            Scribe_Values.Look(ref harmed, "harmed");
            Scribe_Values.Look(ref robbed, "robbed");
            Scribe_Values.Look(ref seenStaying, "seenStaying");
            Scribe_References.Look(ref harmer, "harmer");
            Scribe_Collections.Look(ref kind, "kind", LookMode.Reference);
            Scribe_Collections.Look(ref points, "points", LookMode.Value);
            Scribe_Collections.Look(ref talkers, "talkers", LookMode.Reference);
            Scribe_Values.Look(ref logSeen, "logSeen", -1);
            Scribe_Values.Look(ref lastSaid, "lastSaid", -1);
            Scribe_Values.Look(ref thanked, "thanked");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                kind ??= new List<Pawn>();
                points ??= new List<int>();
                talkers ??= new List<Pawn>();
                if (kind.Count != points.Count)
                {
                    kind.Clear();
                    points.Clear();
                }
                for (int i = kind.Count - 1; i >= 0; i--)
                    if (kind[i] == null)
                    {
                        kind.RemoveAt(i);
                        points.RemoveAt(i);
                    }
                talkers.RemoveAll(p => p == null);
            }
        }
    }

    /// <summary>
    /// The stranger at the gate (brainstorm 7, theoxenia; docs/design/gods.md, "On the map"): a lone traveller of the
    /// colony's age (a wanderer, a pilgrim, a drifter, a lost spacer) asks for shelter, and is a god in disguise
    /// (GodLookSeam, the presence part's tells). The colony answers (Rimfolk_Evt_GodStranger: a bed, the gate, or away);
    /// let in, they are Hospitality's guest when it is loaded and Rimfolk's own visitor otherwise. Over the stay they
    /// notice who is kind (the bed they got, the food, who talked with them and did them a kindness, how the animals were
    /// kept, whether anyone struck or robbed them), and say things a traveller couldn't know. When they leave, or if they
    /// are struck, they show who they were: in Walking plainly (they stop, grow into themselves and speak, then walk off as a
    /// god), in Felt only "some say" once they are gone; in Believed they never come. The colony is blessed or cursed by how
    /// the stranger was treated (GodGifts.cs), and the kindest colonist gets a lasting gift.
    /// </summary>
    public static class GodStranger
    {
        public const string IncidentName = "Rimfolk_Evt_GodStranger";
        public const string HintPool = "Rimfolk_Pool_GodStrangerHint";
        public const string ThanksPool = "Rimfolk_Pool_GodStrangerThanks";

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>The god the next stranger is, and where (set while their event is being fired).</summary>
        internal static God pendingGod;
        internal static Map pendingMap;

        // ================= Who comes =================

        /// <summary>A stranger could come to this home: a people at peace to come from, and someone to answer the gate.</summary>
        public static bool CanCome(Map home) =>
            home != null && DefDatabase<IncidentDef>.GetNamedSilentFail(IncidentName) != null && Arrivals.Friendly() != null
            && IncidentWorker_Happening.Colonists(home).Any(p => !p.DevelopmentalStage.Juvenile()) && Gods.All.Any(g => g != null && GodShapes.Stranger(g));

        /// <summary>
        /// The god who comes as the stranger: only one whose shape can (GodShapes.Stranger: who comes, and as what, is the
        /// god's own); the road's and the hearth's most, the colony's own and its patron more (GodsOnMapMath.StrangerWeight).
        /// </summary>
        public static God PickGod(Map home)
        {
            var mine = Gods.ColonyGods(home);
            var patron = Gods.Patron(home);
            return Gods.All.Where(g => g != null && GodShapes.Stranger(g)).RandomElementByWeightWithFallback(g =>
                GodsOnMapMath.StrangerWeight(g.domain, g.second, g.kind, mine.Contains(g), g == patron, g.known));
        }

        /// <summary>The traveller's word by the colony's age: a wanderer, a pilgrim, a drifter, a lost spacer.</summary>
        public static string Traveller(int age) => ("CC_GodM_Traveller_" + Mathf.Clamp(age, 0, 3)).Translate().Resolve();

        /// <summary>The colony's age (0 tribal to 3 spacer).</summary>
        public static int Age => Avail.EffectiveTech <= TechLevel.Neolithic ? 0 : Avail.EffectiveTech <= TechLevel.Medieval ? 1 : Avail.EffectiveTech <= TechLevel.Industrial ? 2 : 3;

        /// <summary>The planned stranger's hour: their question comes when the letters' gap allows it. True once they are at the gate.</summary>
        internal static bool Come(GodsOnMapBook b, GodComing c, God god) => Fire(b, c.map, god);

        /// <summary>The stranger's event fired now at <paramref name="map"/> as <paramref name="god"/> (the dev action skips the letters' gap).</summary>
        public static bool Fire(GodsOnMapBook b, Map map, God god, bool anyway = false)
        {
            var def = DefDatabase<IncidentDef>.GetNamedSilentFail(IncidentName);
            if (b == null || map == null || god == null || def == null || b.stranger != null) return false;
            if (!anyway && !Tell.CanAsk) return false;
            if (!(def.Worker is IncidentWorker_Happening w)) return false;
            pendingGod = god;
            pendingMap = map;
            try
            {
                // The gods' own event: it comes whatever the world events' switch says, as a scenario's events do.
                return w.ScenarioCanFire(map) && w.ScenarioFire(map) && b.stranger != null;
            }
            finally
            {
                pendingGod = null;
                pendingMap = null;
            }
        }

        // ================= At the gate =================

        /// <summary>The stranger made (by their event's worker): a person of a people at peace, the god in disguise.</summary>
        internal static Pawn Make(God god, Map map, out Faction f)
        {
            // Of the god's own peoples where one is at peace with the colony, else of anyone's.
            f = Arrivals.Friendly(null, x => god.factions.Contains(x) ? 4f : 1f);
            if (f == null) return null;
            var kind = Arrivals.PersonKind(f);
            if (kind == null) return null;
            var x = Arrivals.Make(kind, f, new FloatRange(25f, 60f));
            if (x == null) return null;
            if (!GodLookSeam.Make(x, god, true))
            {
                Arrivals.Discard(x);
                return null;
            }
            return x;
        }

        /// <summary>At the gate now: the stay begins, waiting for the colony's answer.</summary>
        internal static void Arrived(HappeningContext ctx, God god)
        {
            var b = GodsOnMapBook.Instance;
            if (b == null || ctx?.stranger == null) return;
            b.stranger = new StrangerStay
            {
                x = ctx.stranger, god = god.id, map = ctx.map, faction = ctx.stranger.Faction, since = Now, letIn = ctx.a, phase = StrangerPhase.Waiting,
                leaveAt = Now + Mathf.RoundToInt(GenDate.TicksPerDay * GodsOnMapMath.AnswerDays) + GenDate.TicksPerHour * 2
            };
        }

        /// <summary>The colony answered (or the letter's time ran out): a bed (0), away (1), the gate (2).</summary>
        internal static void Answered(HappeningContext ctx, int option)
        {
            var b = GodsOnMapBook.Instance;
            var s = b?.stranger;
            if (s == null || s.x != ctx?.stranger) return;
            var x = s.x;
            s.answered = true;
            s.welcome = option == 0 ? StrangerWelcome.Bed : option == 1 ? StrangerWelcome.TurnedAway : StrangerWelcome.Gate;
            int now = Now;
            if (x == null || x.Dead || !x.Spawned)
            {
                s.phase = StrangerPhase.Leaving;
                return;
            }
            s.gate = x.Position;
            if (s.welcome == StrangerWelcome.TurnedAway)
            {
                s.phase = StrangerPhase.Leaving;
                // Walking: they stop on their way and show who they were, there and then.
                if (GodsOnMapMath.Show(Gods.Realness) == GodShow.Plain) End(b, s, now);
                return;
            }
            if (s.welcome == StrangerWelcome.Bed) s.Credit(ctx.a, GodsOnMapMath.LetIn);
            float hours = Rand.Range(GodsOnMapMath.StayHoursMin, GodsOnMapMath.StayHoursMax);
            s.leaveAt = now + Mathf.RoundToInt(hours * GenDate.TicksPerHour);
            s.since = now;
            s.phase = StrangerPhase.Staying;
            s.slaughterBase = Slaughtered(s.map);
            s.logSeen = LastLogId();
            var spot = s.gate;
            if (s.welcome == StrangerWelcome.Bed)
            {
                // Hospitality's guest when it is loaded: its guest beds and table, its tab; else a warm corner indoors for the night.
                Lord hosted = null;
                try
                {
                    var f = x.Faction;
                    x.GetLord()?.RemovePawn(x);
                    hosted = InvitedGuests.HandOver(ArrivalWhy.NotableGuest, new List<Pawn> { x }, s.map, f, spot, hours + 3f);
                    if (hosted == null) LordMaker.MakeNewLord(f, new LordJob_VisitOnErrand(f, spot, Mathf.RoundToInt((hours + 3f) * GenDate.TicksPerHour)), s.map, new List<Pawn> { x });
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods: the stranger let in", e);
                }
                if (hosted == null) VisitorServices.Lodge(s.map, x, false);
            }
            else
            {
                // By the gate: their own visit's group keeps them at the spot outside, longer.
                try
                {
                    var f = x.Faction;
                    x.GetLord()?.RemovePawn(x);
                    LordMaker.MakeNewLord(f, new LordJob_VisitOnErrand(f, spot, Mathf.RoundToInt((hours + 3f) * GenDate.TicksPerHour)), s.map, new List<Pawn> { x });
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods: the stranger at the gate", e);
                }
            }
        }

        // ================= Over the stay =================

        private static int Slaughtered(Map map)
        {
            int n = 0;
            foreach (var p in Homes.People(map))
                if (p?.records != null)
                    n += p.records.GetAsInt(RecordDefOf.AnimalsSlaughtered);
            return n;
        }

        private static int LastLogId()
        {
            var entries = Find.PlayLog?.AllEntries;
            int max = -1;
            if (entries == null) return max;
            foreach (var e in entries)
                if (e != null && e.LogID > max)
                    max = e.LogID;
            return max;
        }

        internal static void Hourly(GodsOnMapBook b, int now)
        {
            var s = b.stranger;
            if (s == null) return;
            var x = s.x;
            if (x == null || x.Discarded)
            {
                b.stranger = null;
                return;
            }
            // Gone from the map on their own way (Felt, or turned away): the end, where they stayed.
            if (!x.Spawned && !x.Dead && s.phase != StrangerPhase.WalkingOff)
            {
                if (s.phase == StrangerPhase.Waiting && !s.answered) s.welcome = StrangerWelcome.TurnedAway;
                Gone(b, s, now);
                return;
            }
            if (s.phase == StrangerPhase.WalkingOff && !x.Spawned)
            {
                Vanish(x);
                b.stranger = null;
                return;
            }
            // Killed by something not the colony's (a raid, a beast): the god simply goes, its body with it.
            if (x.Dead && !s.harmed)
            {
                var corpse = x.Corpse;
                if (corpse != null && corpse.Spawned) s.place = corpse.Position;
                if (corpse != null && !corpse.Destroyed) corpse.Destroy();
                Gone(b, s, now);
                return;
            }
            if (x.Dead || s.phase != StrangerPhase.Staying) return;
            Observe(s, x, now);
            Speak(s, x, now);
            // The stay's end: their own group leaving, or the time come in daylight.
            int hour = GenLocalDate.HourOfDay(s.map);
            if (Leaving(x) || now >= s.leaveAt && hour >= 7 && hour <= 19 || now >= s.leaveAt + GenDate.TicksPerDay / 2) End(b, s, now);
        }

        /// <summary>Their own group has begun to leave (the visit's time is up, Hospitality's stay over), or they have none.</summary>
        private static bool Leaving(Pawn x)
        {
            var lord = x.GetLord();
            if (lord == null) return true;
            if (lord.CurLordToil is LordToil_ExitMap) return true;
            return InvitedGuests.Hosted(lord) && !InvitedGuests.Staying(lord) && Find.TickManager.TicksGame - (GodsOnMapBook.Instance?.stranger?.since ?? 0) > GenDate.TicksPerHour * 3;
        }

        private static readonly AccessTools.FieldRef<PlayLogEntry_Interaction, InteractionDef> IntDef = AccessTools.FieldRefAccess<PlayLogEntry_Interaction, InteractionDef>("intDef");
        private static readonly AccessTools.FieldRef<PlayLogEntry_Interaction, Pawn> Initiator = AccessTools.FieldRefAccess<PlayLogEntry_Interaction, Pawn>("initiator");
        private static readonly AccessTools.FieldRef<PlayLogEntry_Interaction, Pawn> Recipient = AccessTools.FieldRefAccess<PlayLogEntry_Interaction, Pawn>("recipient");

        /// <summary>An hour of the stay looked at: how the visit goes for them, what they ate, where they slept, who talked with them, the animals.</summary>
        private static void Observe(StrangerStay s, Pawn x, int now)
        {
            var map = s.map;
            // How the visit goes: Hospitality's own score for its guest, else their mood.
            float content = Guests.VisitScore(x);
            if (content < 0f) content = x.needs?.mood?.CurLevelPercentage ?? 0.5f;
            s.contentSum += content;
            s.contentN++;
            // What they ate (the game's own memories of a meal) and whether they go hungry.
            var memories = x.needs?.mood?.thoughts?.memories?.Memories;
            if (memories != null)
                foreach (var m in memories)
                {
                    string n = m?.def?.defName;
                    if (n == null || m.age > GenDate.TicksPerHour * 2) continue;
                    if (n == "AteLavishMeal") s.ate = Math.Max(s.ate, 4);
                    else if (n == "AteFineMeal") s.ate = Math.Max(s.ate, 3);
                    else if (n.StartsWith("AteRaw") || n == "AteKibble" || n == "AteCorpse") s.ate = Math.Max(s.ate, 1);
                }
            var food = x.needs?.food;
            if (food != null && food.CurLevelPercentage > 0.45f && s.ate < 2 && now - s.since > GenDate.TicksPerHour * 6) s.ate = Math.Max(s.ate, s.welcome == StrangerWelcome.Bed ? 2 : 1);
            // Where they sleep: a bed, a good room, a corner indoors, the open.
            if (!x.Awake())
            {
                var bed = x.CurrentBed();
                int here = bed != null ? (bed.GetRoom()?.GetStat(RoomStatDefOf.Impressiveness) ?? 0f) >= 40f ? 2 : 1 : x.Position.Roofed(map) ? 0 : -1;
                if (here > s.slept) s.slept = here;
                s.place = x.Position;
            }
            if (!s.place.IsValid && s.gate.IsValid) s.place = s.gate;
            // Who talked with them, and how (the play log since the last look).
            var entries = Find.PlayLog?.AllEntries;
            if (entries != null)
            {
                int max = s.logSeen;
                foreach (var e in entries)
                {
                    if (e == null || e.LogID <= s.logSeen) continue;
                    if (e.LogID > max) max = e.LogID;
                    if (!(e is PlayLogEntry_Interaction pi) || !pi.Concerns(x)) continue;
                    Pawn a, r;
                    InteractionDef def;
                    try
                    {
                        a = Initiator(pi);
                        r = Recipient(pi);
                        def = IntDef(pi);
                    }
                    catch
                    {
                        continue;
                    }
                    var colonist = a == x ? r : a;
                    if (colonist == null || !colonist.IsColonist) continue;
                    if (!s.talkers.Contains(colonist)) s.talkers.Add(colonist);
                    string n = def?.defName ?? "";
                    int by = n == "Insult" || n == "Slight" ? GodsOnMapMath.Harsh : n == "KindWords" || n == "DeepTalk" ? GodsOnMapMath.Talk : GodsOnMapMath.Word;
                    s.Credit(colonist, by);
                }
                s.logSeen = max;
            }
            // A meal or a drink shared, a gift given (Outsiders/): each a kindness, once each since they came.
            var pairs = OutsiderBook.Instance?.pairs;
            if (pairs != null)
                foreach (var pr in pairs)
                {
                    if (pr?.outsider != x || pr.colonist == null) continue;
                    foreach (var act in new[] { OutsiderAct.Meal, OutsiderAct.Drink, OutsiderAct.Gift, OutsiderAct.Befriend, OutsiderAct.Provoke })
                    {
                        int t = pr.Last(act);
                        if (t < s.since || t < now - GenDate.TicksPerHour) continue;
                        if (act == OutsiderAct.Provoke) s.Credit(pr.colonist, GodsOnMapMath.Harsh);
                        else
                        {
                            s.Credit(pr.colonist, act == OutsiderAct.Befriend ? GodsOnMapMath.Talk : act == OutsiderAct.Gift ? GodsOnMapMath.Given : GodsOnMapMath.Shared);
                            s.kindnesses++;
                        }
                        if (!s.talkers.Contains(pr.colonist)) s.talkers.Add(pr.colonist);
                    }
                }
            // The colony's animals: starving, or in danger from the cold or heat, while they watch; and slaughtered.
            foreach (var a in map.mapPawns.SpawnedColonyAnimals)
            {
                if (a?.needs?.food != null && a.needs.food.Starving) s.animalsNeglected = true;
                var hs = a?.health?.hediffSet;
                if (hs == null) continue;
                var cold = hs.GetFirstHediffOfDef(HediffDefOf.Hypothermia);
                var heat = hs.GetFirstHediffOfDef(HediffDefOf.Heatstroke);
                if (cold != null && cold.Severity > 0.35f || heat != null && heat.Severity > 0.35f) s.animalsNeglected = true;
            }
            if (s.slaughterBase >= 0) s.slaughtered = Math.Max(0, Slaughtered(map) - s.slaughterBase);
            // Their pack taken while they lay hurt.
            if (x.Downed)
            {
                int items = x.inventory?.innerContainer?.Count ?? 0;
                if (s.itemsWhenDown < 0) s.itemsWhenDown = items;
                else if (items < s.itemsWhenDown) s.robbed = true;
            }
            else s.itemsWhenDown = -1;
            s.seenStaying = true;
        }

        /// <summary>
        /// Now and then the stranger says something a traveller couldn't know (by its god's domain, more pointed in Walking),
        /// to whoever is beside them; once, after a good meal, they thank the cook by name.
        /// </summary>
        private static void Speak(StrangerStay s, Pawn x, int now)
        {
            if (!x.Awake() || x.Downed || x.InMentalState || now - s.lastSaid < GenDate.TicksPerHour * 3 || !Rand.Chance(0.4f)) return;
            var god = s.God;
            if (god == null) return;
            var with = Together.Company(x);
            if (with == null) return;
            if (!s.thanked && s.ate >= 2)
            {
                var cook = Homes.People(s.map).Where(p => p.skills?.GetSkill(SkillDefOf.Cooking) is SkillRecord r && !r.TotallyDisabled)
                    .OrderByDescending(p => p.skills.GetSkill(SkillDefOf.Cooking).Level).FirstOrDefault();
                string thanks = cook == null ? null : Flavor.FromPool(ThanksPool, x, null, null, Tags(god).ToArray());
                if (!thanks.NullOrEmpty() && Speech.Say(x, thanks.Formatted(cook.Named("OTHER")).Resolve(), with))
                {
                    s.thanked = true;
                    s.lastSaid = now;
                    return;
                }
            }
            var tags = Tags(god);
            if (Gods.Realness == GodRealness.Walking) tags.Add("walking");
            if (s.welcome == StrangerWelcome.Gate) tags.Add("gate");
            if (s.animalsNeglected) tags.Add("animals");
            string line = Flavor.FromPool(HintPool, x, null, null, tags.ToArray());
            if (line.NullOrEmpty()) return;
            if (Speech.Say(x, line.Formatted(with.Named("OTHER")).Resolve(), with)) s.lastSaid = now;
        }

        /// <summary>The tags a god gives a stranger's lines: its domains ("storm", "harvest") and the colony's age.</summary>
        internal static List<string> Tags(God god)
        {
            var tags = new List<string>();
            if (god == null) return tags;
            tags.Add(god.domain.ToString().ToLowerInvariant());
            if (god.second != GodDomain.None) tags.Add(god.second.ToString().ToLowerInvariant());
            tags.Add("age" + Age);
            // A god written by hand can have words of its own (GodShapeDef.lineTag).
            string own = GodShapes.LineTag(god);
            if (!own.NullOrEmpty()) tags.Add(own);
            return tags;
        }

        // ================= Struck, tended =================

        /// <summary>The colony's own struck the stranger (GodsOnMapPatches): the god's anger comes at once, at the next step.</summary>
        internal static void Struck(Pawn victim, Pawn by)
        {
            var b = GodsOnMapBook.Instance;
            var s = b?.stranger;
            if (s == null || s.x != victim || s.harmed) return;
            s.harmed = true;
            s.harmer = by;
            if (by != null) s.Credit(by, GodsOnMapMath.Blow);
            s.phase = StrangerPhase.Struck;
            s.phaseDue = Now + 1;
            b.Due(s.phaseDue);
        }

        /// <summary>One of the colony tended the stranger's wounds: a kindness.</summary>
        internal static void Tended(Pawn doctor, Pawn patient)
        {
            var s = GodsOnMapBook.Instance?.stranger;
            if (s == null || s.x != patient || doctor == null || !doctor.IsColonist) return;
            s.Credit(doctor, GodsOnMapMath.Tended);
            s.kindnesses++;
        }

        // ================= The end =================

        /// <summary>The steps between the hours: the struck god's anger, a farewell's moment over, the walk off.</summary>
        internal static void Step(GodsOnMapBook b, int now)
        {
            var s = b.stranger;
            if (s == null || s.phaseDue < 0) return;
            if (now < s.phaseDue)
            {
                b.Due(s.phaseDue);
                return;
            }
            s.phaseDue = -1;
            var x = s.x;
            if (s.phase == StrangerPhase.Struck)
            {
                Anger(b, s, now);
                return;
            }
            if (s.phase == StrangerPhase.Vanishing)
            {
                b.stranger = null;
                var god = s.God;
                if (god != null && x != null && x.Spawned) GodPresenceSeam.Call(new PresenceSpec { god = god, map = x.Map, cell = x.Position, kind = PresenceKind.Omen });
                Vanish(x);
                return;
            }
            // Walking off too long (shut in, the way blocked): gone in a breath.
            if (s.phase == StrangerPhase.WalkingOff)
            {
                b.stranger = null;
                Vanish(x);
                return;
            }
            if (s.phase == StrangerPhase.Farewell && x != null && x.Spawned && !x.Dead)
            {
                s.phase = StrangerPhase.WalkingOff;
                s.phaseDue = now + GenDate.TicksPerHour * 6;
                b.Due(s.phaseDue);
                try
                {
                    x.GetLord()?.RemovePawn(x);
                    LordMaker.MakeNewLord(x.Faction, new LordJob_ExitMapBest(LocomotionUrgency.Walk, false, false), x.Map, new List<Pawn> { x });
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods: the stranger walking off", e);
                    Vanish(x);
                    b.stranger = null;
                }
            }
        }

        /// <summary>The stay is over: judged, and the god shows itself (Walking) or goes its way unrevealed (Felt).</summary>
        internal static void End(GodsOnMapBook b, StrangerStay s, int now)
        {
            var x = s.x;
            var god = s.God;
            if (x == null || god == null)
            {
                b.stranger = null;
                return;
            }
            if (GodsOnMapMath.Show(Gods.Realness) != GodShow.Plain || !x.Spawned || x.Dead)
            {
                // Felt: they go their own way, unrevealed; the end comes once they are gone.
                s.phase = StrangerPhase.Leaving;
                if (x.Spawned && !x.Dead) SendOff(x);
                return;
            }
            var t = Judge(s, out _);
            // Walking: they stop, turn, and show who they were.
            try
            {
                x.GetLord()?.RemovePawn(x);
                x.jobs?.StopAll();
                var wait = JobMaker.MakeJob(JobDefOf.Wait, x);
                wait.expiryInterval = GodsOnMapMath.FarewellTicks;
                x.jobs?.StartJob(wait, JobCondition.InterruptForced);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the stranger stopping", e);
            }
            GodAvatar.Reveal(x, false);
            var kindest = Kindest(s);
            var cell = x.Position;
            var outcome = GodGifts.Outcome(god, s.map, cell, t, kindest, s.harmer, s.welcome);
            Moment(god, s.map, cell, x, GodsOnMapMath.EndKind(t), Title(god, t, true), Text(s, god, t, kindest, outcome, true));
            Remember(s, god, cell, t, kindest);
            s.phase = StrangerPhase.Farewell;
            s.phaseDue = now + GodsOnMapMath.FarewellTicks;
            b.Due(s.phaseDue);
        }

        /// <summary>They have left the map on their own (Felt, or turned away and not shown): the end, veiled, where they stayed.</summary>
        private static void Gone(GodsOnMapBook b, StrangerStay s, int now)
        {
            var god = s.God;
            var map = s.map;
            b.stranger = null;
            if (god == null || map == null || !Homes.IsHome(map))
            {
                Vanish(s.x);
                return;
            }
            var t = Judge(s, out _);
            var kindest = Kindest(s);
            var cell = s.place.IsValid ? s.place : s.gate.IsValid ? s.gate : map.Center;
            bool plain = GodsOnMapMath.Show(Gods.Realness) == GodShow.Plain;
            var outcome = GodGifts.Outcome(god, map, cell, t, kindest, s.harmer, s.welcome);
            Moment(god, map, cell, null, GodsOnMapMath.EndKind(t), Title(god, t, plain), Text(s, god, t, kindest, outcome, plain));
            Remember(s, god, cell, t, kindest);
            Vanish(s.x);
        }

        /// <summary>Struck: the god shows its anger where the stranger stands (Walking: it shows itself first, then is gone in a breath) and is gone.</summary>
        private static void Anger(GodsOnMapBook b, StrangerStay s, int now)
        {
            var x = s.x;
            var god = s.God;
            var map = s.map;
            if (god == null || map == null)
            {
                b.stranger = null;
                Vanish(x);
                return;
            }
            var corpse = x?.Corpse;
            var cell = x != null && x.Spawned ? x.Position : corpse != null && corpse.Spawned ? corpse.Position : s.gate.IsValid ? s.gate : map.Center;
            bool plain = GodsOnMapMath.Show(Gods.Realness) == GodShow.Plain && x != null && x.Spawned && !x.Dead;
            if (corpse != null && !corpse.Destroyed) corpse.Destroy();
            // apart: a god shows itself to everyone on the map; nobody confides anything to anyone.
            if (plain)
            {
                try
                {
                    x.GetLord()?.RemovePawn(x);
                    x.jobs?.StopAll();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods: the struck stranger", e);
                }
                GodAvatar.Reveal(x, false);
            }
            // Felt: one moment there, the next only scorched ground where they stood.
            else Vanish(x);
            var kindest = Kindest(s);
            var outcome = GodGifts.Outcome(god, map, cell, Treatment.Harmed, kindest, s.harmer, s.welcome);
            Moment(god, map, cell, plain ? x : null, PresenceKind.Wrath, Title(god, Treatment.Harmed, plain), Text(s, god, Treatment.Harmed, kindest, outcome, plain));
            Remember(s, god, cell, Treatment.Harmed, kindest);
            if (!plain)
            {
                b.stranger = null;
                return;
            }
            s.phase = StrangerPhase.Vanishing;
            s.phaseDue = now + GodsOnMapMath.FarewellTicks / 2;
            b.Due(s.phaseDue);
        }

        private static Treatment Judge(StrangerStay s, out float score)
        {
            var t = new StayTally
            {
                welcome = s.welcome, harmed = s.harmed, robbed = s.robbed, content = s.contentN > 0 ? s.contentSum / s.contentN : 0.5f, ate = s.ate,
                slept = s.slept < -1 ? s.welcome == StrangerWelcome.Bed ? 0 : -1 : s.slept, talkers = s.talkers.Count, kindnesses = s.kindnesses,
                animalsNeglected = s.animalsNeglected, slaughtered = s.slaughtered
            };
            return GodsOnMapMath.Judge(t, out score);
        }

        /// <summary>The kindest colonist still with the colony (GodsOnMapMath.Kindest), or null.</summary>
        private static Pawn Kindest(StrangerStay s)
        {
            var who = new List<Pawn>();
            var pts = new List<int>();
            for (int i = 0; i < s.kind.Count && i < s.points.Count; i++)
            {
                var p = s.kind[i];
                if (p == null || p.Dead || p.Discarded || !p.IsColonist || p == s.harmer) continue;
                who.Add(p);
                pts.Add(s.points[i]);
            }
            int at = GodsOnMapMath.Kindest(pts);
            return at >= 0 ? who[at] : null;
        }

        /// <summary>Sent on their way off the map now: Hospitality's guest as its own stay ends, Rimfolk's visitor on foot.</summary>
        private static void SendOff(Pawn x)
        {
            try
            {
                var lord = x.GetLord();
                if (lord != null && InvitedGuests.Hosted(lord) && InvitedGuests.Leave(lord)) return;
                if (lord?.CurLordToil is LordToil_ExitMap) return;
                lord?.RemovePawn(x);
                LordMaker.MakeNewLord(x.Faction, new LordJob_ExitMapBest(Pace.Errand, false, true), x.Map, new List<Pawn> { x });
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the stranger on their way", e);
            }
        }

        /// <summary>The stranger is gone for good: off the map, out of the world (a god is no one's to meet again as a traveller).</summary>
        internal static void Vanish(Pawn x)
        {
            if (x == null || x.Discarded) return;
            try
            {
                if (x.Spawned)
                {
                    FleckMaker.ThrowLightningGlow(x.DrawPos, x.Map, 2.5f);
                    x.GetLord()?.RemovePawn(x);
                    x.DeSpawn();
                }
                if (!x.Dead) GodAvatar.Unmake(x);
                Find.WorldPawns.ForcefullyKeptPawns.Remove(x);
                if (Find.WorldPawns.Contains(x)) Find.WorldPawns.RemoveAndDiscardPawnViaGC(x);
                else if (!x.Dead) Find.WorldPawns.PassToWorld(x, PawnDiscardDecideMode.Discard);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the stranger gone", e);
            }
        }

        // ================= The words =================

        private static void Moment(God god, Map map, IntVec3 cell, Pawn avatar, PresenceKind kind, string title, string text)
        {
            var spec = PresenceSpec.For(god, map, kind, cell, avatar);
            spec.title = title;
            spec.text = text;
            if (!GodPresenceSeam.Call(spec)) Tell.Letter(NewsKind.Land, Importance.Important, map, title, text, LetterDefOf.NeutralEvent, new LookTargets(cell, map));
        }

        private static string Title(God god, Treatment t, bool plain) =>
            plain ? ("CC_GodM_Stranger_Title_" + t).Translate(Gods.Args(god)).Resolve().CapitalizeFirst() : "CC_GodM_Stranger_TitleVeiled".Translate().Resolve().CapitalizeFirst();

        private static string Text(StrangerStay s, God god, Treatment t, Pawn kindest, string outcome, bool plain)
        {
            var args = new List<NamedArgument>(GodsOnMapWords.Args(god, plain))
            {
                (s.x?.LabelShort ?? "?").Named("STRANGER"),
                Traveller(Age).Named("TRAVELLER"),
                (kindest?.LabelShort ?? "").Named("KINDEST"),
                (s.harmer?.LabelShort ?? "CC_GodM_Someone".Translate().Resolve()).Named("HARMER")
            };
            string key = "CC_GodM_Stranger_" + (plain ? "Plain_" : "Veiled_") + t;
            string text = key.Translate(args.ToArray()).Resolve();
            if (kindest != null && t >= Treatment.Kind) text += " " + "CC_GodM_Stranger_Kindest".Translate(args.ToArray()).Resolve();
            if (!outcome.NullOrEmpty()) text += "\n\n" + outcome;
            return text;
        }

        /// <summary>The visit remembered: in the god's acts, the colony's chronicle, the mark on the land, among those who saw it, and the deeds talked of.</summary>
        private static void Remember(StrangerStay s, God god, IntVec3 cell, Treatment t, Pawn kindest)
        {
            var map = s.map;
            string what = ("CC_GodM_Stranger_Act_" + t).Translate(Traveller(Age).Named("TRAVELLER")).Resolve();
            bool plain = GodsOnMapMath.Show(Gods.Realness) == GodShow.Plain;
            Gods.Remember(new GodAct
            {
                god = god.id, home = Gods.HomeKey(map), tick = Now, kind = plain ? GodActKind.Reveal : GodActKind.Depart, cell = cell,
                text = (plain ? what : "CC_GodM_SomeSay".Translate(what.Named("WHAT")).Resolve()).CapitalizeFirst()
            });
            if (plain) Gods.Learn(god, "visitors");
            GodMarks.Note(god, map, cell, what, GodsOnMapMath.EndKind(t));
            GodMarks.Witnessed(map, cell, "CC_GodM_Memory_Stranger".Translate(Traveller(Age).Named("TRAVELLER")).Resolve(), s.talkers.Concat(s.kind));
            string line = (plain ? "CC_GodM_Chronicle_Stranger" : "CC_GodM_Chronicle_StrangerVeiled").Translate(GodsOnMapWords.Args(god, plain).Concat(new[]
            {
                Traveller(Age).Named("TRAVELLER"), what.Named("WHAT")
            }).ToArray()).Resolve();
            CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.World, line.CapitalizeFirst(), kindest));
            if (kindest != null && t >= Treatment.Kind) Deeds.Record("Rimfolk_Deed_GodSheltered", kindest, null, -1f, null, Traveller(Age));
            if (s.harmer != null && s.harmer.IsColonist && t == Treatment.Harmed) Deeds.Record("Rimfolk_Deed_GodStruck", s.harmer, null, -1f, null, Traveller(Age));
        }

        // ================= This week =================

        /// <summary>The stranger staying, in words for This week (never a time left).</summary>
        public static string StayLine(StrangerStay s)
        {
            if (s?.x == null) return null;
            string key = s.phase == StrangerPhase.Waiting ? "CC_GodM_Week_StrangerAtGate" : s.welcome == StrangerWelcome.Bed ? "CC_GodM_Week_StrangerGuest" : "CC_GodM_Week_StrangerByGate";
            return key.Translate(s.x.LabelShort.Named("STRANGER"), Traveller(Age).Named("TRAVELLER")).Resolve();
        }
    }

    /// <summary>
    /// The stranger at the gate as an event (Defs/GodDefs/Rimfolk_GodsOnMap.xml, Rimfolk_Evt_GodStranger): only fired by the
    /// gods' own coming (GodComings), never drawn by the storyteller. The stranger is made of a people at peace (the god's
    /// own peoples first) as the god in disguise, walks to the gate and waits for the answer; the answer sets the stay going.
    /// </summary>
    public class HappeningWorker_GodStranger : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            GodStranger.pendingGod != null && GodStranger.pendingMap == map && GodsOnMapBook.Instance?.stranger == null && GodsMath.AvatarsCome(Gods.Realness)
            && base.CanHappen(map, ext);

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var god = GodStranger.pendingGod;
            if (god == null) return false;
            var x = GodStranger.Make(god, ctx.map, out var f);
            if (x == null) return false;
            ctx.faction = f;
            ctx.stranger = x;
            ctx.note = god.id.ToString();
            ctx.tale = GodStranger.Traveller(GodStranger.Age);
            // The one who goes out to the gate: someone grown, the most sociable first.
            ctx.a = Colonists(ctx.map).Where(p => !p.DevelopmentalStage.Juvenile())
                .OrderByDescending(p => (p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0) + Rand.Range(0, 6)).FirstOrDefault();
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var god = GodStranger.pendingGod;
            if (god == null) return false;
            float hours = GodsOnMapMath.AnswerDays * 24f + GodsOnMapMath.StayHoursMax + 4f;
            if (Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, null, hours) == null) return false;
            GodStranger.Arrived(ctx, god);
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            var god = int.TryParse(ctx.note, out int id) ? Gods.ById(id) : GodStranger.pendingGod;
            if (god == null) return;
            foreach (var t in GodStranger.Tags(god)) tags.Add(t);
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            GodStranger.Answered(ctx, option);
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }
}
