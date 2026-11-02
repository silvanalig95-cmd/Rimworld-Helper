using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What the newer wishes and life moments see of the colony's days, once an hour (CouncilGameComponent.HourlyPeople):
    /// who is reading which book (a reader's year, a life goal that counts different books), who worked a bench in the
    /// dark (a lamp at the bench), and who came in soaked to the table or their bed (a roof over the way). And, as it
    /// happens, two colonists drinking coffee or tea in the same room within the hour (the morning cup). Only the books
    /// read are kept with the save; the rest is what has been seen lately, and starts afresh after a load.
    /// </summary>
    public class LifeNewTracker : GameComponent
    {
        /// <summary>"ThingID|title": books read, once each (LifeNewMath.AddRead), the newest <see cref="LifeNewMath.MaxRead"/>.</summary>
        private List<string> read = new List<string>();

        private static LifeNewTracker cached;
        private static Game cachedGame;

        public static LifeNewTracker Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<LifeNewTracker>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        public LifeNewTracker(Game game)
        {
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref read, "read", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && read == null) read = new List<string>();
        }

        /// <summary>Different books this colonist has read (since the counting began).</summary>
        public int BooksRead(Pawn p) => p == null ? 0 : LifeNewMath.ReadCount(read, p.ThingID);

        /// <summary>Just got clean of a drug (the memory of the story's end): nothing stronger than tea at a toast.</summary>
        public static bool JustClean(Pawn p)
        {
            var def = DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_Arc_Clean");
            return def != null && p?.needs?.mood?.thoughts?.memories?.GetFirstMemoryOfDef(def) != null;
        }

        // ---------------- What has been seen lately (not saved) ----------------

        /// <summary>A bench worked in the dark: which, and when it was last seen.</summary>
        public struct DarkNote
        {
            public Building bench;
            public int tick;
        }

        private static readonly Dictionary<Pawn, DarkNote> dark = new Dictionary<Pawn, DarkNote>();
        private static readonly Dictionary<Pawn, int> soaked = new Dictionary<Pawn, int>();

        /// <summary>The bench this colonist was last seen working in the dark, within <paramref name="days"/>; null if none.</summary>
        public static Building DarkBench(Pawn p, float days)
        {
            if (p == null || !dark.TryGetValue(p, out var n) || n.bench == null || n.bench.Destroyed) return null;
            return Find.TickManager.TicksGame - n.tick <= days * GenDate.TicksPerDay ? n.bench : null;
        }

        /// <summary>This colonist came in soaked to the table or their bed within <paramref name="days"/>.</summary>
        public static bool CameInSoaked(Pawn p, float days) =>
            p != null && soaked.TryGetValue(p, out int t) && Find.TickManager.TicksGame - t <= days * GenDate.TicksPerDay;

        /// <summary>Once an hour: each colonist at home, as they are right now.</summary>
        public static void Hourly(CouncilGameComponent comp)
        {
            var tracker = Instance;
            int now = Find.TickManager.TicksGame;
            foreach (var map in Find.Maps)
            {
                if (!map.IsPlayerHome) continue;
                foreach (var p in map.mapPawns.FreeColonistsSpawned)
                {
                    if (p.Dead || p.Downed || !p.Awake() || !People.HasInnerLife(p)) continue;
                    try
                    {
                        Look(tracker, comp, p, map, now);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("life moments hour", e);
                    }
                }
            }
            Prune(dark, x => x.Value.bench == null || x.Value.bench.Destroyed || now - x.Value.tick > GenDate.TicksPerDay * 5);
            Prune(soaked, x => now - x.Value > GenDate.TicksPerDay * 5);
            cups.RemoveAll(c => c.pawn == null || c.pawn.Dead || now - c.tick > LifeNewMath.HourTicks * 2);
        }

        private static void Prune<T>(Dictionary<Pawn, T> notes, Func<KeyValuePair<Pawn, T>, bool> old)
        {
            foreach (var k in notes.Where(x => x.Key == null || x.Key.Dead || x.Key.Discarded || old(x)).Select(x => x.Key).ToList()) notes.Remove(k);
        }

        private static void Look(LifeNewTracker tracker, CouncilGameComponent comp, Pawn p, Map map, int now)
        {
            // A book in hand, being read (the game's own reading): counted once per book.
            if (tracker != null && p.jobs?.curDriver is JobDriver_Reading reading && reading.IsReading && reading.Book != null)
            {
                var book = reading.Book;
                string title = !book.Title.NullOrEmpty() ? book.Title : book.def.defName + "#" + book.thingIDNumber;
                if (LifeNewMath.AddRead(tracker.read, p.ThingID, title)) comp?.aspirations?.Notify(p, "booksread");
            }
            var job = p.CurJob;
            // At a bench (or the research bench) on its work, in the dark.
            if (job?.workGiverDef != null && job.targetA.Thing is Building b && (b is Building_WorkTable || b is Building_ResearchBench)
                && b.def.hasInteractionCell && p.Position == b.InteractionCell && map.glowGrid.PsychGlowAt(p.Position) == PsychGlow.Dark)
                dark[p] = new DarkNote { bench = b, tick = now };
            // Just in out of the rain, at the table or in bed (or their own bedroom, or a dining room).
            var wet = p.needs?.mood?.thoughts?.memories?.GetFirstMemoryOfDef(ThoughtDefOf.SoakingWet);
            if (wet != null && wet.age < LifeNewMath.HourTicks && p.Position.Roofed(map))
            {
                var room = p.GetRoom();
                bool there = job != null && (job.def == JobDefOf.Ingest || job.def == JobDefOf.LayDown)
                             || (room != null && (room == RoomUtil.BedRoom(p) || room.Role == RoomUtil.DiningRoom));
                if (there) soaked[p] = now;
            }
        }

        // ---------------- The morning cup ----------------

        private struct Cup
        {
            public Pawn pawn;
            public int tick;
        }

        private static readonly List<Cup> cups = new List<Cup>();
        private static readonly HashSet<string> sharedToday = new HashSet<string>();
        private static int sharedDay = -1;

        /// <summary>Coffee or tea (a mod's): a drink a teetotaler may have, called coffee or tea.</summary>
        public static bool IsCup(ThingDef d) =>
            d != null && Cuisine.IsSoftDrink(d) && (LifeNewMath.IsCupWord(d.label) || LifeNewMath.IsCupWord(d.defName));

        /// <summary>
        /// A colonist has just had coffee or tea: if another had one within the hour and is with them now, in the same
        /// room, the two share a word over their cups (the social log, and the bubbles) and grow a little closer. Once
        /// a day for any two.
        /// </summary>
        public static void Notify_Cup(Pawn a, Thing drink)
        {
            if (a?.Map == null || !a.Map.IsPlayerHome || drink == null || !IsCup(drink.def) || !People.HasInnerLife(a)) return;
            int now = Find.TickManager.TicksGame;
            int day = now / GenDate.TicksPerDay;
            if (day != sharedDay)
            {
                sharedDay = day;
                sharedToday.Clear();
            }
            cups.RemoveAll(c => c.pawn == null || c.pawn == a || !LifeNewMath.SameHour(c.tick, now));
            foreach (var c in cups)
            {
                var b = c.pawn;
                if (!Together.Can(a, b) || !b.IsColonist || !People.HasInnerLife(b) || a.GetRoom() != b.GetRoom()) continue;
                string key = Math.Min(a.thingIDNumber, b.thingIDNumber) + "|" + Math.Max(a.thingIDNumber, b.thingIDNumber);
                if (sharedToday.Contains(key)) continue;
                sharedToday.Add(key);
                Share(a, b);
                break;
            }
            cups.Add(new Cup { pawn = a, tick = now });
            if (cups.Count > 30) cups.RemoveAt(0);
        }

        private static void Share(Pawn a, Pawn b)
        {
            // together: Notify_Cup found the two in the same room and within talking range (Together.Can).
            if (!Together.Can(a, b)) return;
            string line = Flavor.FromPool("Rimfolk_Log_MorningCup", a);
            if (line.NullOrEmpty()) return;
            Speech.Logged(a, b, line.Formatted(a.Named("PAWN"), b.Named("OTHER")).Resolve());
            CouncilGameComponent.Instance?.social?.Bump(a, b, TieKind.Friends, 2f, "CC_TieNote_Cup".Translate());
        }
    }

    /// <summary>
    /// A colonist drank something: coffee or tea is noted for the morning cup (LifeNewTracker.Notify_Cup). Only reads
    /// what was drunk. Without this hook (deeper game hooks off) the cups simply aren't shared.
    /// </summary>
    [HarmonyPatch(typeof(Thing), nameof(Thing.Ingested))]
    public static class Patch_MorningCup
    {
        public static void Postfix(Thing __instance, Pawn ingester)
        {
            if (!RimfolkPatches.On || ingester == null || __instance?.def == null || ingester.Faction != Faction.OfPlayer || !ingester.RaceProps.Humanlike) return;
            try
            {
                if (__instance.def.IsDrug) LifeNewTracker.Notify_Cup(ingester, __instance);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("morning cup", e);
            }
        }
    }
}
