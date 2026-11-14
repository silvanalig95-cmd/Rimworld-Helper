using System;
using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>Murmur's happenings under way (OwnHappeningsBook.murmur): a song at the well and the rain it may bring, what the water gave back.</summary>
    public class MurmurState : IExposable
    {
        /// <summary>His festival kept today, and the song at the well: where, until when, who sang ("id|id"), whether at the festival.</summary>
        public Map festivalMap;
        public int festivalDay = -1;
        public Map songMap;
        public IntVec3 songAt = IntVec3.Invalid;
        public int songUntil = -1;
        public string singers, sent;
        public bool songFestival;

        /// <summary>The rain the song is answered with: where, when it comes, and the rainbow after it.</summary>
        public Map rainMap;
        public int rainAt = -1, rainbowUntil = -1;

        /// <summary>What the water gave back: where it lies, what, who goes to it, until when.</summary>
        public Map giveMap;
        public IntVec3 giveAt = IntVec3.Invalid;
        public string giveWhat;
        public Pawn giveFinder;
        public int giveUntil = -1;

        public void ExposeData()
        {
            Scribe_References.Look(ref festivalMap, "festivalMap");
            Scribe_Values.Look(ref festivalDay, "festivalDay", -1);
            Scribe_References.Look(ref songMap, "songMap");
            Scribe_Values.Look(ref songAt, "songAt", IntVec3.Invalid);
            Scribe_Values.Look(ref songUntil, "songUntil", -1);
            Scribe_Values.Look(ref singers, "singers");
            Scribe_Values.Look(ref sent, "sent");
            Scribe_Values.Look(ref songFestival, "songFestival");
            Scribe_References.Look(ref rainMap, "rainMap");
            Scribe_Values.Look(ref rainAt, "rainAt", -1);
            Scribe_Values.Look(ref rainbowUntil, "rainbowUntil", -1);
            Scribe_References.Look(ref giveMap, "giveMap");
            Scribe_Values.Look(ref giveAt, "giveAt", IntVec3.Invalid);
            Scribe_Values.Look(ref giveWhat, "giveWhat");
            Scribe_References.Look(ref giveFinder, "giveFinder");
            Scribe_Values.Look(ref giveUntil, "giveUntil", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (songMap == null) songUntil = -1;
                if (rainMap == null) rainAt = -1;
                if (giveMap == null) giveUntil = -1;
            }
        }
    }

    /// <summary>
    /// Murmur, Old Croak (docs/gods-next-proposals.md, C 13): a huge old toad in the wells and marshes; when it sings it rains,
    /// and when it falls silent the land goes dry. Rain is its alone. Its own happenings:
    /// <list type="bullet">
    /// <item>Sung back to: at its festival, the Singing at the Wells, or in a dry spell when two or more of the colony hold
    /// to it, at dusk the colony's people walk to the well (Dubs Bad Hygiene's, any well of the colony's, else the still
    /// water or the marsh) and stand round it singing; often, within the day, the rain comes in the night with frogs croaking
    /// under it, a drought ends, and after it the sky shows a double rainbow (Seasonal Weather's, where it is loaded).</item>
    /// <item>The water gives back: the morning after a long wet run, when the rain stops and the water goes down, things lie on
    /// the mud at the water's edge (a little silver, and now and then an old worn thing of the land's age, from the whole of
    /// the player's mods); the first one free walks down and finds them.</item>
    /// <item>The frogs come in: the night a long dry spell breaks, frogs come up out of the wet ground all over the colony in
    /// their dozens (Odyssey's bullfrogs; else only their croaking), croaking all night, and are gone at first light.</item>
    /// </list>
    /// </summary>
    public static class MurmurWays
    {
        public const string Key = "Murmur";
        public const string Pool = "Rimfolk_Pool_GodMurmur_Say";
        public const string SangThought = "Rimfolk_Thought_Murmur_Sang";
        public const string FrogKind = "Bullfrog";

        public static God God => OwnHappenings.God(Key);

        private static int Now => Find.TickManager.TicksGame;

        private static readonly string[] Croaks = { "Pawn_Frog_Call", "AG_Pawn_Frog_Call" };

        internal static void Hourly(OwnHappeningsBook b, Map map, int now)
        {
            var g = God;
            if (g == null) return;
            var s = b.murmur;
            int hour = GenLocalDate.HourOfDay(map);
            int today = GenDate.DaysPassed;
            if (s.rainMap == map && now >= s.rainAt && s.rainAt >= 0) Rain(b, g, map);
            if (s.rainbowUntil >= 0 && s.rainMap == null && now < s.rainbowUntil && !OwnHappenings.Raining(map) && map.skyManager.CurSkyGlow > 0.5f) Rainbow(s, map);
            if (MapFacts.ThreatOn(map)) return;
            if (s.songUntil < 0 && hour >= OwnHappeningsMath.SongFrom && hour < OwnHappeningsMath.SongTo)
            {
                if (s.festivalMap == map && s.festivalDay == today) BeginSong(b, map, true);
                else
                {
                    var t = OwnHappeningsMath.SongTrigger(OwnHappenings.DryRun(map), OwnHappenings.Condition(map, "Drought") != null, Believers(map, g).Count);
                    if (OwnHappenings.Roll(b, OwnHappeningsMath.MurmurSong, t, g, map, now)) BeginSong(b, map, false);
                }
            }
            if (s.giveUntil < 0 && hour >= 7 && hour < 12 && !OwnHappenings.Raining(map))
            {
                var t = OwnHappeningsMath.GiveBackTrigger(OwnHappenings.WetRun(map), false);
                if (OwnHappenings.Roll(b, OwnHappeningsMath.MurmurGiveBack, t, g, map, now)) GiveBack(b, map);
            }
            if (OwnHappenings.Raining(map) && (hour >= 18 || hour < 3))
            {
                var t = OwnHappeningsMath.FrogsTrigger(OwnHappenings.DryRun(map), false);
                if (OwnHappenings.Roll(b, OwnHappeningsMath.MurmurFrogs, t, g, map, now)) Frogs(b, map);
            }
        }

        internal static void Step(OwnHappeningsBook b, int now)
        {
            var s = b.murmur;
            if (s.songUntil >= 0) OwnHappenings.Safe("the song at the well", () => SongStep(b, now));
            if (s.giveUntil >= 0) OwnHappenings.Safe("what the water gave back", () => GiveStep(b, now));
        }

        /// <summary>The colony's people who hold to him (at least leaning toward him).</summary>
        public static List<Pawn> Believers(Map map, God g) =>
            map.mapPawns.FreeColonistsSpawned.Where(p => OwnHappenings.Ours(p) && !Worship.Scoffer(p) && PawnGods.HoldOf(p, g) >= PawnGodsMath.LeansFrom && !PawnGods.Dreads(p, g)).ToList();

        // ================= Sung back to =================

        /// <summary>His festival kept at a home (OwnHappenings.FestivalKept): the singing at the well at dusk.</summary>
        internal static void Festival(OwnHappeningsBook b, Map map)
        {
            b.murmur.festivalMap = map;
            b.murmur.festivalDay = GenDate.DaysPassed;
        }

        /// <summary>The colony's well (Dubs Bad Hygiene's, or any of the colony's called a well), else still water or the marsh near its people.</summary>
        public static IntVec3 Water(Map map, out string what)
        {
            var well = map.listerBuildings.allBuildingsColonist.FirstOrDefault(x => x.Spawned && x.def.defName.IndexOf("Well", StringComparison.OrdinalIgnoreCase) >= 0);
            if (well != null)
            {
                what = well.LabelNoCount;
                return well.Position;
            }
            var c = SeleneWays.StillWater(map);
            if (!c.IsValid) c = GodOmens.Wet(map);
            what = c.IsValid ? c.GetTerrain(map)?.label ?? "" : "";
            return c;
        }

        public static bool BeginSong(OwnHappeningsBook b, Map map, bool festival)
        {
            var s = b.murmur;
            var g = God;
            var at = Water(map, out _);
            if (!at.IsValid) return false;
            var who = new List<Pawn>();
            who.AddRange(Believers(map, g).Where(OwnHappenings.Free));
            foreach (var p in map.mapPawns.FreeColonistsSpawned.Where(OwnHappenings.Free).InRandomOrder())
                if (!who.Contains(p) && (festival || who.Count < Believers(map, g).Count + 2))
                    who.Add(p);
            who = who.Take(OwnHappeningsMath.SongMost).ToList();
            if (who.Count == 0) return false;
            s.songMap = map;
            s.songAt = at;
            s.songUntil = Now + GenDate.TicksPerHour * 2;
            s.singers = null;
            s.sent = null;
            s.songFestival = festival;
            if (festival) s.festivalMap = null;
            foreach (var p in who)
            {
                var to = CellFinder.RandomClosewalkCellNear(at, map, 4, c => c.Standable(map) && c != at && c.GetEdifice(map) == null);
                if (to.IsValid && OwnHappenings.Walk(p, to, GenDate.TicksPerHour * 2)) s.sent = HeliosState.With(s.sent, p);
            }
            if (s.sent.NullOrEmpty())
            {
                s.songUntil = -1;
                s.songMap = null;
                return false;
            }
            OwnHappenings.Came(b, OwnHappeningsMath.MurmurSong, festival ? "festival" : "dry");
            b.Due(Now + 250);
            return true;
        }

        private static void SongStep(OwnHappeningsBook b, int now)
        {
            var s = b.murmur;
            var map = s.songMap;
            if (map == null || !map.IsPlayerHome)
            {
                s.songUntil = -1;
                s.songMap = null;
                return;
            }
            var g = God;
            var music = DefDatabase<FleckDef>.GetNamedSilentFail("Music");
            foreach (var p in map.mapPawns.FreeColonistsSpawned.Where(p => OwnHappenings.Ours(p) && HeliosState.Has(s.sent, p) && p.Position.InHorDistOf(s.songAt, 5.5f)).ToList())
            {
                if (p.CurJobDef == JobDefOf.Goto) continue;
                if (!HeliosState.Has(s.singers, p))
                {
                    bool first = s.singers.NullOrEmpty();
                    s.singers = HeliosState.With(s.singers, p);
                    OwnHappenings.Stand(p, s.songAt, 1200);
                    if (first) OwnHappenings.Say(p, Pool, g, "song");
                }
                if (music != null) FleckMaker.Static(p.DrawPos + new Vector3(0f, 0f, 0.7f), map, music, 0.8f);
            }
            if (now > s.songUntil)
            {
                SongDone(b, map, g);
                return;
            }
            b.Due(now + 300);
        }

        /// <summary>The song is over: those who sang remember it, the book has it, and it may be answered with rain within the day.</summary>
        private static void SongDone(OwnHappeningsBook b, Map map, God g)
        {
            var s = b.murmur;
            int n = HeliosState.Count(s.singers);
            var at = s.songAt;
            bool festival = s.songFestival;
            var sang = map.mapPawns.FreeColonistsSpawned.Where(p => HeliosState.Has(s.singers, p)).ToList();
            s.songUntil = -1;
            s.songMap = null;
            s.singers = s.sent = null;
            if (n == 0) return;
            Water(map, out string what);
            foreach (var p in sang) OwnHappenings.Memory(p, SangThought);
            if (sang.Count > 0 && g != null) PawnGods.RiteLed(sang[0], g);
            OwnHappenings.Line(map, "CC_GodMurmur_Song_Text".Translate(n.Named("COUNT"), what.Named("WATER"), Homes.Name(map).Named("HOME")).Resolve(), new LookTargets(new TargetInfo(at, map)));
            if (g != null) Worship.Booked(g, map, at, "CC_GodMurmur_Book_Song".Translate(n.Named("COUNT"), what.Named("WATER")).Resolve(), sang.FirstOrDefault());
            if (!Rand.Chance(OwnHappeningsMath.SongAnswered(festival, g == null ? 0f : GodNear.Of(g, map)))) return;
            s.rainMap = map;
            s.rainAt = Now + Mathf.RoundToInt(Rand.Range(OwnHappeningsMath.RainAfterMin, OwnHappeningsMath.RainAfterMax) * GenDate.TicksPerHour);
        }

        /// <summary>The rain comes (snow in a frost), a drought on the land ends, a croak from the water; and after it, the rainbow.</summary>
        public static bool Rain(OwnHappeningsBook b, God g, Map map)
        {
            var s = b.murmur;
            s.rainMap = null;
            s.rainAt = -1;
            var w = DefDatabase<WeatherDef>.GetNamedSilentFail(map.mapTemperature.OutdoorTemp > 0f ? "Rain" : "SnowGentle");
            if (w == null) return false;
            try
            {
                map.weatherManager.TransitionTo(w);
                OwnHappenings.Condition(map, "Drought")?.End();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the rain after the singing", e);
                return false;
            }
            var at = Water(map, out _);
            if (at.IsValid) OwnHappenings.Sound(map, at, 1f, Croaks);
            s.rainbowUntil = Now + GenDate.TicksPerDay * 2;
            string text = "CC_GodMurmur_Rain_Text".Translate(Homes.Name(map).Named("HOME")).Resolve();
            OwnHappenings.CardBy(g, map, "Council_Evt_LongRains", "CC_GodMurmur_Rain_Title", "CC_GodMurmur_Rain_TitleNamed", text,
                at.IsValid ? new LookTargets(new TargetInfo(at, map)) : null, LetterDefOf.PositiveEvent);
            var seer = at.IsValid ? OwnHappenings.Seer(map, at, 40f) : null;
            if (seer != null) OwnHappenings.Say(seer, Pool, g, "rain");
            if (g != null) OwnHappenings.Saw(g, map, at, GodActKind.Blessing, seer, "CC_GodMurmur_Saw_Rain");
            return true;
        }

        private static void Rainbow(MurmurState s, Map map)
        {
            s.rainbowUntil = -1;
            var bow = DefDatabase<WeatherDef>.GetNamedSilentFail("DoubleRainbow");
            if (bow == null) return;
            try
            {
                map.weatherManager.TransitionTo(bow);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the rainbow after the rain", e);
            }
        }

        // ================= The water gives back =================

        /// <summary>Open mud at the water's edge near the colony (a river, a lake, a marsh), or none.</summary>
        private static IntVec3 Edge(Map map)
        {
            var mid = OwnHappenings.Middle(map);
            foreach (var c in GenRadial.RadialCellsAround(mid, 50f, true))
            {
                if (!c.InBounds(map) || c.Fogged(map) || !c.Standable(map) || c.Roofed(map) || c.GetFirstItem(map) != null) continue;
                var t = c.GetTerrain(map);
                if (t == null || t.IsWater) continue;
                if (GenAdj.CellsAdjacent8Way(new TargetInfo(c, map)).Any(n => n.InBounds(map) && n.GetTerrain(map) is TerrainDef nt && (nt.IsWater || nt.defName.IndexOf("Marsh", StringComparison.OrdinalIgnoreCase) >= 0)))
                    return c;
            }
            return IntVec3.Invalid;
        }

        /// <summary>An old thing of the land's age from the player's whole pool (a weapon, a tool, a piece of clothing), worn, of little worth; or null.</summary>
        public static Thing OldThing()
        {
            var tech = Avail.EffectiveTech;
            var pool = DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.category == ThingCategory.Item && (d.IsWeapon || d.IsApparel) && !d.destroyOnDrop && d.tradeability != Tradeability.None
                                                                            && d.techLevel <= tech && d.techLevel != TechLevel.Undefined && d.BaseMarketValue >= 15f
                                                                            && d.BaseMarketValue <= OwnHappeningsMath.OldThingWorth && d.graphicData != null && !d.IsCorpse
                                                                            && (d.apparel == null || d.apparel.layers.NullOrEmpty() == false)).ToList();
            var def = pool.RandomElementWithFallback();
            if (def == null) return null;
            try
            {
                var t = ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.RandomStuffFor(def) : null);
                t.HitPoints = Mathf.Max(1, Mathf.RoundToInt(t.MaxHitPoints * OwnHappeningsMath.OldThingWorn));
                t.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Poor, ArtGenerationContext.Outsider);
                return t;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: an old thing the water gave back", e);
                return null;
            }
        }

        public static bool GiveBack(OwnHappeningsBook b, Map map)
        {
            var s = b.murmur;
            var at = Edge(map);
            if (!at.IsValid) return false;
            var things = new List<Thing>();
            var silver = ThingMaker.MakeThing(ThingDefOf.Silver);
            silver.stackCount = Rand.RangeInclusive(OwnHappeningsMath.SilverBackMin, OwnHappeningsMath.SilverBackMax);
            things.Add(silver);
            if (Rand.Chance(OwnHappeningsMath.OldThingChance))
            {
                var old = OldThing();
                if (old != null) things.Add(old);
            }
            var words = new List<string>();
            foreach (var t in things)
            {
                words.Add(t.LabelNoParenthesisCap.UncapitalizeFirst());
                GenPlace.TryPlaceThing(t, at, map, ThingPlaceMode.Near);
            }
            var mud = ThingDefOf.Filth_Dirt;
            foreach (var c in GenRadial.RadialCellsAround(at, 1.9f, true).Where(c => c.InBounds(map) && c.Standable(map))) FilthMaker.TryMakeFilth(c, map, mud);
            s.giveMap = map;
            s.giveAt = at;
            s.giveWhat = words.ToCommaList(true);
            s.giveUntil = Now + GenDate.TicksPerHour * 5;
            s.giveFinder = map.mapPawns.FreeColonistsSpawned.Where(p => OwnHappenings.Free(p) && p.CanReach(at, PathEndMode.Touch, Danger.Some))
                .OrderBy(p => p.Position.DistanceToSquared(at)).FirstOrDefault();
            if (s.giveFinder != null) OwnHappenings.Walk(s.giveFinder, OwnHappenings.Beside(map, at, s.giveFinder), GenDate.TicksPerHour * 2);
            OwnHappenings.Came(b, OwnHappeningsMath.MurmurGiveBack);
            b.Due(Now + 250);
            return true;
        }

        private static void GiveStep(OwnHappeningsBook b, int now)
        {
            var s = b.murmur;
            var map = s.giveMap;
            var p = s.giveFinder;
            if (map == null || now > s.giveUntil)
            {
                s.giveUntil = -1;
                s.giveMap = null;
                return;
            }
            if (p == null || p.Dead || !p.Spawned || p.Map != map)
            {
                p = s.giveFinder = map.mapPawns.FreeColonistsSpawned.Where(x => OwnHappenings.Free(x) && x.CanReach(s.giveAt, PathEndMode.Touch, Danger.Some))
                    .OrderBy(x => x.Position.DistanceToSquared(s.giveAt)).FirstOrDefault();
                if (p != null) OwnHappenings.Walk(p, OwnHappenings.Beside(map, s.giveAt, p), GenDate.TicksPerHour * 2);
            }
            else if (p.Position.InHorDistOf(s.giveAt, 2.5f))
            {
                Found(b, map, p);
                return;
            }
            else if (p.CurJobDef != JobDefOf.Goto && OwnHappenings.Free(p)) OwnHappenings.Walk(p, OwnHappenings.Beside(map, s.giveAt, p), GenDate.TicksPerHour * 2);
            b.Due(now + 300);
        }

        private static void Found(OwnHappeningsBook b, Map map, Pawn p)
        {
            var s = b.murmur;
            var g = God;
            var at = s.giveAt;
            string what = s.giveWhat ?? "";
            s.giveUntil = -1;
            s.giveMap = null;
            s.giveFinder = null;
            OwnHappenings.Stand(p, at, 360);
            OwnHappenings.SayWith(p, Pool, g, new[] { what.Named("WHAT") }, "giveback");
            if (g != null) PawnGods.Gift(p, g, what);
            Water(map, out string water);
            string text = "CC_GodMurmur_GiveBack_Text".Translate(p.Named("PAWN"), what.Named("WHAT")).Resolve();
            OwnHappenings.CardBy(g, map, "SeasonalFlooding", "CC_GodMurmur_GiveBack_Title", "CC_GodMurmur_GiveBack_TitleNamed", text, new LookTargets(new TargetInfo(at, map)),
                LetterDefOf.PositiveEvent);
            if (g != null) OwnHappenings.Saw(g, map, at, GodActKind.Blessing, p, "CC_GodMurmur_Saw_GiveBack", what.Named("WHAT"));
        }

        // ================= The frogs come in =================

        public static bool Frogs(OwnHappeningsBook b, Map map)
        {
            var g = God;
            var kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(FrogKind);
            var mid = OwnHappenings.Middle(map);
            int n = OwnHappeningsMath.Frogs(map.cellIndices.NumGridCells);
            int came = 0;
            int hour = GenLocalDate.HourOfDay(map);
            int untilDawn = ((hour >= 6 ? 30 : 6) - hour) * GenDate.TicksPerHour;
            if (kind != null)
                for (int i = 0; i < n; i++)
                {
                    if (!CellFinder.TryFindRandomCellNear(mid, map, 40, c => c.Standable(map) && !c.Roofed(map) && !c.Fogged(map) && c.GetFirstPawn(map) == null, out var c0)) continue;
                    try
                    {
                        var f = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, null, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true));
                        var spawned = (Pawn)GenSpawn.Spawn(f, c0, map);
                        OwnHappenings.SendOff(spawned, untilDawn + Rand.Range(0, GenDate.TicksPerHour));
                        FleckMaker.ThrowDustPuffThick(c0.ToVector3Shifted(), map, 0.8f, new Color(0.45f, 0.4f, 0.3f, 0.5f));
                        came++;
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("gods: the frogs coming in", e);
                        break;
                    }
                }
            bool heard = Croaks.Any(x => GodSounds.Sound(x) != null);
            if (came == 0 && !heard) return false;
            for (int i = 0; i < 3; i++) OwnHappenings.Sound(map, CellFinder.RandomClosewalkCellNear(mid, map, 20), 0.9f, Croaks);
            var seer = map.mapPawns.FreeColonistsSpawned.Where(p => GodOmens.Awake(p) && OwnHappenings.Ours(p)).RandomElementWithFallback();
            if (seer != null) OwnHappenings.Say(seer, Pool, g, came > 0 ? "frogs" : "frogsheard");
            OwnHappenings.Line(map, (came > 0 ? "CC_GodMurmur_Frogs_Text" : "CC_GodMurmur_Frogs_TextHeard").Translate(Homes.Name(map).Named("HOME")).Resolve(),
                seer != null ? new LookTargets(seer) : null);
            if (g != null) OwnHappenings.Saw(g, map, IntVec3.Invalid, GodActKind.Blessing, seer, came > 0 ? "CC_GodMurmur_Saw_Frogs" : "CC_GodMurmur_Saw_FrogsHeard");
            OwnHappenings.Came(b, OwnHappeningsMath.MurmurFrogs, came > 0 ? "seen" : "heard");
            return true;
        }
    }

    /// <summary>Dev mode: Murmur's happenings now (Debug actions, "Rimfolk", "Gods: Murmur's ... now").</summary>
    public static class MurmurDebug
    {
        private static void Say(string text) => Messages.Message("Gods: " + text, MessageTypeDefOf.SilentInput, false);

        [DebugAction("Rimfolk", "Gods: Murmur sung to at the well now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSong()
        {
            var b = OwnHappenings.Book;
            var map = Find.CurrentMap;
            if (b == null || map == null) return;
            b.murmur.songUntil = -1;
            Say(MurmurWays.BeginSong(b, map, true) ? "they walk to the water and sing; answered, the rain comes within the day (\"Gods: Murmur's rain now\")." : "no water, or nobody free.");
        }

        [DebugAction("Rimfolk", "Gods: Murmur's rain now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugRain()
        {
            var b = OwnHappenings.Book;
            var map = Find.CurrentMap;
            if (b == null || map == null) return;
            Say(MurmurWays.Rain(b, MurmurWays.God, map) ? "the rain comes; when it stops, the double rainbow (with Seasonal Weather)." : "it couldn't.");
        }

        [DebugAction("Rimfolk", "Gods: Murmur's water gives back now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugGiveBack()
        {
            var b = OwnHappenings.Book;
            var map = Find.CurrentMap;
            if (b == null || map == null) return;
            b.murmur.giveUntil = -1;
            Say(MurmurWays.GiveBack(b, map) ? "things lie on the mud at the water's edge; someone walks down to them." : "no water's edge near the colony.");
        }

        [DebugAction("Rimfolk", "Gods: Murmur's frogs come in now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugFrogs()
        {
            var b = OwnHappenings.Book;
            var map = Find.CurrentMap;
            if (b == null || map == null) return;
            Say(MurmurWays.Frogs(b, map) ? "the frogs come in (Odyssey's bullfrogs; without it only their croaking), gone at first light." : "no frogs and no frog sound in this game.");
        }
    }
}
