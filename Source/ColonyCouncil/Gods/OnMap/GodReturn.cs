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
    /// The moon returning by night what was done by day (Selene, the Mirror: brainstorm 10; a god whose shape
    /// <c>returns</c>). By the moon itself (the player, 12 November 2026: variety first): only around the full moon, a cycle
    /// apart at least, and only for a reason, a deed of the day that stands out (GodsOnMapMath.ReturnTrigger: a kindness
    /// or a cruelty out of the ordinary, never the tending the colony does every day) or the colony's dealings with the
    /// peoples that day (GodHealth.Deed); even then its chance is small at first and grows over the cycles after
    /// (GodsOnMapMath.ReturnTonight), so nobody can tell which full moon it will be. A kindness comes back to whoever did it
    /// as something found by their bed, a good dream or a wound healed by morning; a cruelty as a nightmare, a hard frost
    /// about where they sleep, or an animal that comes and stands at their door; the colony's day with the peoples as a
    /// wounded colonist's deep rest, something found at the door, or bad dreams (GodHealth.Mirror). Each time it leans to a
    /// form it didn't take lately; when it comes back to the same person (the same cruelty done again), it is the next step
    /// of their story, and it is spoken of as happening again. Whoever is awake sees it, it goes in the colony's book in
    /// their words, and it is spoken of at breakfast. Nothing says why.
    /// </summary>
    public static class GodReturn
    {
        public const string DreamPool = "Rimfolk_Pool_GodReturnDream";
        public const string TalkPool = "Rimfolk_Pool_GodReturnTalk";

        /// <summary>The moon's return in the gods' short memory (GodPace).</summary>
        public const string What = "return";

        /// <summary>The colony's dealings with the peoples, as whom the moon returns them to in the short memory.</summary>
        public const string ThePeoples = "peoples";

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>The god that returns by night (Selene), while it lives, or null.</summary>
        public static God Moon => Gods.All.FirstOrDefault(g => g != null && g.kind != GodKind.Dead && GodShapes.Of(g).returns);

        internal static void Hourly(GodsOnMapBook b, int now)
        {
            var moon = Moon;
            foreach (var map in Homes.All.ToList())
            {
                if (map == null || GenLocalDate.HourOfDay(map) != 3) continue;
                // The colony's good turns and wrongs to the peoples by day are the seat's to answer for; the day's account starts again each night.
                var world = map == Homes.Seat ? GodWorld.Book : null;
                int kind = world?.kindness ?? 0, cruel = world?.cruelty ?? 0;
                if (world != null) world.kindness = world.cruelty = 0;
                if (moon != null && Night(b, moon, map, now, world != null, kind, cruel)) return;
            }
        }

        /// <summary>The moon's night at a home: whether a reason stands out, whether she returns tonight, and what. True when something came back.</summary>
        private static bool Night(GodsOnMapBook b, God moon, Map map, int now, bool seat, int kind, int cruel)
        {
            var day = DayDeed(map, now);
            var byWorld = seat && GodWorld.On ? GodsOnMapMath.WorldTrigger(kind, cruel) : GodPaceMath.Trigger.None;
            var t = day.trigger >= byWorld ? day.trigger : byWorld;
            if (!GodsOnMapMath.Returns(GodOmens.MoonTonight, GodPace.DaysSince(b.lastReturn, now), t, Rand.Value)) return false;
            if (day.trigger != GodPaceMath.Trigger.None && day.trigger >= byWorld && Return(b, moon, map, now, day, GodsOnMapMath.NightReturn.None)) return true;
            if (byWorld == GodPaceMath.Trigger.None || !GodHealth.Mirror(map, kind, cruel)) return false;
            b.lastReturn = now;
            return true;
        }

        /// <summary>The day's deed that most calls the moon, of the colony's people on a map: who did it, the deed, its kindness weight and how strongly it calls.</summary>
        private struct DayDeedNote
        {
            public Pawn doer;
            public Deed deed;
            public float weight;
            public GodPaceMath.Trigger trigger;
        }

        /// <summary>
        /// The day's deed of the colony's people on <paramref name="map"/> that most calls the moon (done since the morning
        /// before): one out of the ordinary first (GodsOnMapMath.ReturnTrigger: not one the colony does all the time, or a
        /// cruelty done again by one the moon came back to for one), then the weightiest.
        /// </summary>
        private static DayDeedNote DayDeed(Map map, int now)
        {
            var best = new DayDeedNote();
            var tr = Reputations.Tracker;
            if (tr?.deeds == null) return best;
            var people = map.mapPawns.FreeColonistsSpawned.ToDictionary(p => p.thingIDNumber);
            int lately = GodsOnMapMath.RoutineDays * GenDate.TicksPerDay;
            foreach (var d in tr.deeds)
            {
                if (d?.def?.facets == null || now - d.lastTick > GenDate.TicksPerHour * 20 || !people.TryGetValue(d.doer, out var p)) continue;
                float w = d.def.facets.kindness * Mathf.Max(0.5f, d.size);
                if (Math.Abs(w) < GodsOnMapMath.ReturnDeedFrom) continue;
                int same = 0;
                foreach (var x in tr.deeds)
                    if (x != d && x?.def == d.def && now - x.lastTick < lately)
                        same++;
                bool again = w < 0f && TheirLast(p, false) != GodsOnMapMath.NightReturn.None;
                var t = GodsOnMapMath.ReturnTrigger(w, true, same, d.AgeDays(now), again);
                if (t < best.trigger || t == best.trigger && Math.Abs(w) <= Math.Abs(best.weight)) continue;
                best = new DayDeedNote { doer = p, deed = d, weight = w, trigger = t };
            }
            return best;
        }

        /// <summary>What the moon brought back to this person last time for a deed of this kind (a kindness or a cruelty), from the short memory; None when nothing.</summary>
        private static GodsOnMapMath.NightReturn TheirLast(Pawn p, bool kind)
        {
            var steps = kind ? GodsOnMapMath.KindSteps : GodsOnMapMath.CruelSteps;
            foreach (var f in GodPace.Forms(What, p.thingIDNumber.ToString()))
                if (Enum.TryParse(f, out GodsOnMapMath.NightReturn k) && Array.IndexOf(steps, k) >= 0)
                    return k;
            return GodsOnMapMath.NightReturn.None;
        }

        /// <summary>One deed comes back tonight (the dev action: <paramref name="force"/> a form). False when none was worth returning, or it couldn't show.</summary>
        public static bool Return(GodsOnMapBook b, God moon, Map map, int now, GodsOnMapMath.NightReturn force = GodsOnMapMath.NightReturn.None) =>
            Return(b, moon, map, now, DayDeed(map, now), force);

        private static bool Return(GodsOnMapBook b, God moon, Map map, int now, DayDeedNote day, GodsOnMapMath.NightReturn force)
        {
            var p = day.doer;
            var deed = day.deed;
            float w = day.weight;
            if (force != GodsOnMapMath.NightReturn.None && p == null)
            {
                p = map.mapPawns.FreeColonistsSpawned.Where(x => People.HasInnerLife(x)).RandomElementWithFallback();
                w = force <= GodsOnMapMath.NightReturn.Healed ? 5f : -5f;
            }
            if (p == null) return false;
            bool hurt = p.health?.hediffSet?.hediffs.Any(h => h is Hediff_Injury i && !i.IsPermanent()) == true;
            bool toAnimal = deed != null && deed.victim < 0 && (deed.def.defName.IndexOf("Animal", StringComparison.OrdinalIgnoreCase) >= 0
                                                                 || deed.def.defName.IndexOf("Sacred", StringComparison.OrdinalIgnoreCase) >= 0);
            bool cold = map.mapTemperature.OutdoorTemp < 8f;
            // Back to the same person for the same kind of deed: the next step of their story, and it is spoken of as happening again.
            var theirLast = TheirLast(p, w > 0f);
            bool again = theirLast != GodsOnMapMath.NightReturn.None;
            var kind = force != GodsOnMapMath.NightReturn.None ? force : GodsOnMapMath.ReturnFor(w, hurt, toAnimal, cold, Rand.Value, GodPace.Forms(What), theirLast);
            if (kind == GodsOnMapMath.NightReturn.None) return false;
            var at = p.CurrentBed()?.Position ?? p.Position;
            bool shown;
            try
            {
                shown = Show(moon, map, p, at, kind);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the moon's return", e);
                shown = false;
            }
            if (!shown) return false;
            b.lastReturn = now;
            GodPace.Note(What, kind.ToString(), p.thingIDNumber.ToString());
            // Hidden: the moon's regard follows what came back.
            Gods.AddFavour(moon, map, w > 0f ? 0.5f : -0.5f, null);
            var awake = map.mapPawns.FreeColonistsSpawned.Where(x => x != p && x.Awake() && !x.Downed && People.HasInnerLife(x)).OrderBy(x => x.Position.DistanceToSquared(at))
                .FirstOrDefault();
            string tag = kind.ToString().ToLowerInvariant();
            GodsOnMapWords.Saw(moon, map, at, w > 0f ? GodActKind.Blessing : GodActKind.Wrath, awake ?? p,
                "CC_GodM_Saw_Return_" + kind + (again ? "Again" : awake != null ? "Seen" : ""), p.LabelShort.Named("DOER"));
            // Spoken of at breakfast, to whoever they sit with.
            Together.WhenInCompany("godReturn|" + p.thingIDNumber + "|" + now / GenDate.TicksPerDay, p, other =>
            {
                string line = Flavor.FromPool(TalkPool, p, null, null, tag, GodShapes.LineTag(moon), FaceTag(), again ? "again" : null);
                if (!line.NullOrEmpty()) Speech.Say(p, line.Formatted(other.Named("OTHER")).Resolve(), other);
            }, 12f);
            return true;
        }

        /// <summary>The moon's face as a tag for what people say of it ("fullmoon", "halfmoon", "thinmoon").</summary>
        internal static string FaceTag() => GodOmens.MoonTonight switch
        {
            GodsOnMapMath.MoonFace.Full => "fullmoon",
            GodsOnMapMath.MoonFace.Half => "halfmoon",
            GodsOnMapMath.MoonFace.Thin => "thinmoon",
            _ => "darkmoon"
        };

        private static bool Show(God moon, Map map, Pawn p, IntVec3 at, GodsOnMapMath.NightReturn kind)
        {
            switch (kind)
            {
                case GodsOnMapMath.NightReturn.Found:
                    return GodGifts.LeaveFind(moon, map, at, 1) != null;
                case GodsOnMapMath.NightReturn.GoodDream:
                case GodsOnMapMath.NightReturn.Nightmare:
                {
                    var comp = CouncilGameComponent.Instance;
                    string raw = Flavor.FromPool(DreamPool, p, null, null, kind == GodsOnMapMath.NightReturn.GoodDream ? "good" : "bad", FaceTag());
                    if (comp?.dreams == null || raw.NullOrEmpty()) return false;
                    GodOmens.Dreamt(comp, p, raw.Formatted(p.Named("PAWN")).Resolve());
                    if (kind == GodsOnMapMath.NightReturn.Nightmare)
                    {
                        var def = DefDatabase<ThoughtDef>.GetNamedSilentFail(GodWeather.NightmareThought);
                        if (def != null) p.needs?.mood?.thoughts?.memories?.TryGainMemory(def);
                    }
                    return true;
                }
                case GodsOnMapMath.NightReturn.Healed:
                {
                    var wound = p.health?.hediffSet?.hediffs.OfType<Hediff_Injury>().Where(h => !h.IsPermanent()).OrderByDescending(h => h.Severity).FirstOrDefault();
                    if (wound == null) return false;
                    wound.Heal(wound.Severity * 0.6f);
                    return true;
                }
                case GodsOnMapMath.NightReturn.Frost:
                {
                    // A hard frost about where they sleep: rime on the ground outside, and the cold come in.
                    int n = 0;
                    foreach (var c in GenRadial.RadialCellsAround(at, 7f, true).Where(c => c.InBounds(map) && !c.Roofed(map)))
                    {
                        map.snowGrid.AddDepth(c, Rand.Range(0.15f, 0.35f));
                        n++;
                    }
                    GenTemperature.PushHeat(at, map, -30f);
                    return n >= 0;
                }
                case GodsOnMapMath.NightReturn.AnimalBack:
                {
                    // An animal comes and stands at their door a while, looking in.
                    var a = map.mapPawns.AllPawnsSpawned.Where(x => x.RaceProps.Animal && x.Faction == null && !x.Downed && !x.InMentalState && x.Awake()
                                                                    && x.CanReach(at, PathEndMode.Touch, Danger.Some))
                        .OrderBy(x => x.Position.DistanceToSquared(at)).FirstOrDefault();
                    if (a == null) return false;
                    var near = CellFinder.RandomClosewalkCellNear(at, map, 2, c => c.Standable(map) && c.GetFirstPawn(map) == null);
                    var go = JobMaker.MakeJob(JobDefOf.Goto, near);
                    go.locomotionUrgency = LocomotionUrgency.Walk;
                    go.expiryInterval = GenDate.TicksPerHour * 2;
                    a.jobs.StartJob(go, JobCondition.InterruptForced);
                    var stay = JobMaker.MakeJob(JobDefOf.Wait, near);
                    stay.expiryInterval = GenDate.TicksPerHour * 2;
                    a.jobs.jobQueue.EnqueueLast(stay);
                    return true;
                }
            }
            return false;
        }
    }
}
