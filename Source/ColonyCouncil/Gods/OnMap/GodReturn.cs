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
    /// <c>returns</c>). In the small hours, as much as the moon's face shows (GodsOnMapMath.Returns: full, half, thin; nothing
    /// on a night with no moon), one real deed of the colony's from that day comes back to whoever did it (the deeds the
    /// colony talks of, Social/Gossip: a kindness or a cruelty by the deed's own name). A kindness comes back as something
    /// found by their bed, a good dream or a wound healed by morning; a cruelty as a nightmare, a hard frost about where
    /// they sleep, or an animal that comes and stands at their door. Small and rare (one a night, days apart). Whoever is
    /// awake sees it, it goes in the colony's book in their words, and it is spoken of at breakfast. Nothing says why.
    /// </summary>
    public static class GodReturn
    {
        public const string DreamPool = "Rimfolk_Pool_GodReturnDream";
        public const string TalkPool = "Rimfolk_Pool_GodReturnTalk";

        private static int Now => Find.TickManager.TicksGame;

        internal static void Hourly(GodsOnMapBook b, int now)
        {
            if (now - b.lastReturn < GodsOnMapMath.ReturnGapDays * GenDate.TicksPerDay) return;
            var moon = Gods.All.FirstOrDefault(g => g != null && g.kind != GodKind.Dead && GodShapes.Of(g).returns);
            if (moon == null || Gods.Realness == GodRealness.Believed) return;
            foreach (var map in Homes.All.ToList())
            {
                if (map == null || GenLocalDate.HourOfDay(map) != 3) continue;
                if (!GodsOnMapMath.Returns(GodOmens.MoonTonight, Rand.Value)) return;
                if (Return(b, moon, map, now)) return;
            }
        }

        /// <summary>The day's strongest deed of the colony's people on <paramref name="map"/> (since the morning before), and its kindness weight.</summary>
        private static (Pawn doer, Deed deed, float weight) DayDeed(Map map, int now)
        {
            var tr = Reputations.Tracker;
            if (tr?.deeds == null) return (null, null, 0f);
            var people = map.mapPawns.FreeColonistsSpawned.ToDictionary(p => p.thingIDNumber);
            Pawn best = null;
            Deed bestDeed = null;
            float bestW = 0f;
            foreach (var d in tr.deeds)
            {
                if (d?.def?.facets == null || now - d.lastTick > GenDate.TicksPerHour * 20 || !people.TryGetValue(d.doer, out var p)) continue;
                float w = d.def.facets.kindness * Mathf.Max(0.5f, d.size);
                if (Math.Abs(w) > Math.Abs(bestW))
                {
                    best = p;
                    bestDeed = d;
                    bestW = w;
                }
            }
            return (best, bestDeed, bestW);
        }

        /// <summary>One deed comes back tonight. False when none was worth returning, or it couldn't show.</summary>
        public static bool Return(GodsOnMapBook b, God moon, Map map, int now, GodsOnMapMath.NightReturn force = GodsOnMapMath.NightReturn.None)
        {
            var (p, deed, w) = DayDeed(map, now);
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
            var kind = force != GodsOnMapMath.NightReturn.None ? force : GodsOnMapMath.ReturnFor(w, hurt, toAnimal, cold, Rand.Value);
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
            // Hidden: the moon's regard follows what came back.
            Gods.AddFavour(moon, map, w > 0f ? 0.5f : -0.5f, null);
            var awake = map.mapPawns.FreeColonistsSpawned.Where(x => x != p && x.Awake() && !x.Downed && People.HasInnerLife(x)).OrderBy(x => x.Position.DistanceToSquared(at))
                .FirstOrDefault();
            string tag = kind.ToString().ToLowerInvariant();
            GodsOnMapWords.Saw(moon, map, at, w > 0f ? GodActKind.Blessing : GodActKind.Wrath, awake ?? p, "CC_GodM_Saw_Return_" + kind + (awake != null ? "Seen" : ""),
                p.LabelShort.Named("DOER"));
            // Spoken of at breakfast, to whoever they sit with.
            Together.WhenInCompany("godReturn|" + p.thingIDNumber + "|" + now / GenDate.TicksPerDay, p, other =>
            {
                string line = Flavor.FromPool(TalkPool, p, null, null, tag, GodShapes.LineTag(moon), FaceTag());
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
