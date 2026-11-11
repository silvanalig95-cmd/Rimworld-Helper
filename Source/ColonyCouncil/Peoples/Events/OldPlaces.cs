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
    // ===================== A reading that says exactly what the map holds (round 1's shared shape: the scholars at an old place, item 19; later news for sale, item 63) =====================

    /// <summary>An old place on the colony's map: an ancient danger's sealed room, or the land's landmark.</summary>
    public class OldPlace
    {
        /// <summary>"map:x,z" of the room's corner, or "map:land" for the landmark, so none is read twice.</summary>
        public string key;
        public IntVec3 cell;
        public CellRect rect;
        public Room room;
        public string name;
        public bool landmark;
    }

    /// <summary>What a reading found in an old place: the game's own contents, counted.</summary>
    public class Reading
    {
        public Dictionary<string, int> mechs = new Dictionary<string, int>();
        public int beasts, hives, insects, caskets, sleepers, crates;
        public List<Thing> waking = new List<Thing>();
        public List<Building_Casket> closed = new List<Building_Casket>();

        public int Mechs => mechs.Values.Sum();

        public bool Dangerous => PeopleEventsMath.Dangerous(Mechs, beasts, hives, sleepers);

        /// <summary>The reading in words, part by part (PeopleEventsMath.ReadingParts), exactly as counted.</summary>
        public string Words()
        {
            var parts = new List<string>();
            foreach (string p in PeopleEventsMath.ReadingParts(Mechs, beasts, hives, caskets, crates))
            {
                switch (p)
                {
                    case "mechs":
                        var kinds = mechs.Select(kv => Find.ActiveLanguageWorker.Pluralize(kv.Key, kv.Value) is string pl && kv.Value > 1 ? kv.Value + " " + pl : Find.ActiveLanguageWorker.WithIndefiniteArticle(kv.Key))
                            .ToList();
                        parts.Add("CC_PEv_Read_mechs".Translate(Mechs.Named("COUNT"), TextUtil.JoinAnd(kinds).Named("KINDS")).Resolve());
                        break;
                    case "fleshbeasts": parts.Add("CC_PEv_Read_fleshbeasts".Translate(beasts.Named("COUNT")).Resolve()); break;
                    case "hives": parts.Add("CC_PEv_Read_hives".Translate(hives.Named("COUNT"), insects.Named("INSECTS")).Resolve()); break;
                    case "caskets": parts.Add("CC_PEv_Read_caskets".Translate(caskets.Named("COUNT"), sleepers.Named("SLEEPERS")).Resolve()); break;
                    case "crates": parts.Add("CC_PEv_Read_crates".Translate(crates.Named("COUNT")).Resolve()); break;
                    default: parts.Add("CC_PEv_Read_nothing".Translate().Resolve()); break;
                }
            }
            return TextUtil.JoinAnd(parts);
        }
    }

    public static class OldPlaces
    {
        private static bool Sleeping(Thing t)
        {
            var d = t.TryGetComp<CompCanBeDormant>();
            return d != null && !d.Awake;
        }

        /// <summary>A thing of an ancient danger: a casket, a sealed crate, a hive or anything sleeping that is the colony's enemy.</summary>
        private static bool Ancient(Thing t, Map map) =>
            t is Building_AncientCryptosleepCasket || t.def == ThingDefOf.AncientHermeticCrate || t is Hive
            || (t is Pawn p && !p.Dead && Sleeping(p) && p.HostileTo(Faction.OfPlayer));

        /// <summary>
        /// The old places on the map: every sealed room still in the fog that holds an ancient danger's things (the game's
        /// ancient shrines and temples), and the land's own landmark (Odyssey's, Vanilla Landmarks Expanded's) or landform
        /// (Geological Landforms).
        /// </summary>
        public static List<OldPlace> Find(Map map)
        {
            var list = new List<OldPlace>();
            if (map == null) return list;
            try
            {
                var seen = new HashSet<Room>();
                var things = map.listerThings.AllThings.Where(t => t != null && t.Spawned && t.Position.Fogged(map) && Ancient(t, map)).ToList();
                foreach (var t in things)
                {
                    var room = t.Position.GetRoom(map);
                    if (room == null || room.UsesOutdoorTemperature || room.CellCount > 2000 || !seen.Add(room)) continue;
                    var cells = room.Cells.ToList();
                    if (cells.Count == 0) continue;
                    var rect = CellRect.FromLimits(cells.Min(c => c.x), cells.Min(c => c.z), cells.Max(c => c.x), cells.Max(c => c.z));
                    list.Add(new OldPlace { room = room, rect = rect, cell = rect.CenterCell, key = map.uniqueID + ":" + rect.minX + "," + rect.minZ });
                }
                string land = Landmark(map);
                if (!land.NullOrEmpty())
                {
                    var centre = CampSite.ColonyCenter(map);
                    var far = CellFinder.RandomClosewalkCellNear(map.Center, map, 30);
                    if (centre.IsValid && far.InHorDistOf(centre, 25f)) far = CellFinder.RandomEdgeCell(map);
                    list.Add(new OldPlace { landmark = true, name = land, cell = far, rect = CellRect.SingleCell(far), key = map.uniqueID + ":land" });
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events: the old places", e);
            }
            return list;
        }

        /// <summary>The old places no scholar has read yet.</summary>
        public static List<OldPlace> Unread(Map map) => Find(map).Where(p => !PeopleEventsBook.Read(p.key)).ToList();

        /// <summary>The land's landmark by name: the game's (Odyssey and Vanilla Landmarks Expanded add them), else Geological Landforms' landform by name; null for none.</summary>
        public static string Landmark(Map map)
        {
            if (map == null || !map.Tile.Valid) return null;
            try
            {
                string name = Verse.Find.WorldGrid[map.Tile]?.Landmark?.name;
                if (!name.NullOrEmpty()) return name;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events: the land's landmark", e);
            }
            return Landform(map);
        }

        private static bool landformLooked;
        private static System.Reflection.MethodInfo landformGet;

        /// <summary>Geological Landforms' landform of the colony's tile, by name in code (its WorldTileInfo.Get and its Landforms); null without it.</summary>
        private static string Landform(Map map)
        {
            try
            {
                if (!landformLooked)
                {
                    landformLooked = true;
                    var type = GenTypes.GetTypeInAnyAssembly("GeologicalLandforms.WorldTileInfo");
                    landformGet = type?.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                        .FirstOrDefault(m => m.Name == "Get" && m.GetParameters().Length >= 1 && (m.GetParameters()[0].ParameterType == typeof(PlanetTile) || m.GetParameters()[0].ParameterType == typeof(int)));
                }
                if (landformGet == null) return null;
                var p0 = landformGet.GetParameters();
                var args = new object[p0.Length];
                args[0] = p0[0].ParameterType == typeof(int) ? (object)map.Tile.tileId : map.Tile;
                for (int i = 1; i < args.Length; i++) args[i] = p0[i].HasDefaultValue ? p0[i].DefaultValue : null;
                var info = landformGet.Invoke(null, args);
                var forms = info == null ? null : AccessTools.Property(info.GetType(), "Landforms")?.GetValue(info) as System.Collections.IEnumerable;
                if (forms == null) return null;
                foreach (var f in forms)
                {
                    if (f == null) continue;
                    string label = (f as Def)?.LabelCap ?? AccessTools.Property(f.GetType(), "TranslatedNameForSelection")?.GetValue(f) as string
                                   ?? AccessTools.Property(f.GetType(), "Id")?.GetValue(f) as string;
                    if (!label.NullOrEmpty()) return label;
                }
            }
            catch (Exception e)
            {
                landformGet = null;
                CouncilLog.ErrorOnce("peoples' events: Geological Landforms' landform", e);
            }
            return null;
        }

        /// <summary>What the place holds now, counted exactly: sleeping mechanoids by kind, fleshbeasts, hives and insects, caskets and those in them, sealed crates.</summary>
        public static Reading Read(OldPlace p, Map map)
        {
            var r = new Reading();
            if (p == null || map == null || p.landmark || p.room == null) return r;
            try
            {
                foreach (var t in p.room.ContainedAndAdjacentThings.Distinct())
                {
                    if (t is Pawn pawn && !pawn.Dead && p.rect.Contains(pawn.Position))
                    {
                        if (pawn.RaceProps.IsMechanoid && pawn.HostileTo(Faction.OfPlayer))
                        {
                            string k = pawn.kindDef?.label ?? pawn.def.label;
                            r.mechs[k] = (r.mechs.TryGetValue(k, out int n) ? n : 0) + 1;
                            if (Sleeping(pawn)) r.waking.Add(pawn);
                        }
                        else if (pawn.RaceProps.IsAnomalyEntity && pawn.HostileTo(Faction.OfPlayer))
                        {
                            r.beasts++;
                            if (Sleeping(pawn)) r.waking.Add(pawn);
                        }
                        else if (pawn.RaceProps.Insect)
                        {
                            r.insects++;
                            if (Sleeping(pawn)) r.waking.Add(pawn);
                        }
                    }
                    else if (t is Hive hive && p.rect.Contains(hive.Position))
                    {
                        r.hives++;
                        if (Sleeping(hive)) r.waking.Add(hive);
                    }
                    else if (t is Building_AncientCryptosleepCasket casket)
                    {
                        r.caskets++;
                        if (casket.HasAnyContents)
                        {
                            r.sleepers++;
                            r.closed.Add(casket);
                        }
                    }
                    else if (t.def == ThingDefOf.AncientHermeticCrate) r.crates++;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events: an old place read", e);
            }
            return r;
        }

        /// <summary>
        /// The place opened by the scholars: its fog lifted, and whatever sleeps in it woken, the game's own (the mechs and
        /// hives woken as the game wakes them, the caskets opened as when someone opens one). True when something woke.
        /// </summary>
        public static bool Open(OldPlace p, Map map)
        {
            if (p == null || map == null || p.landmark) return false;
            var r = Read(p, map);
            try
            {
                FloodFillerFog.FloodUnfog(p.cell, map);
                foreach (var t in r.waking) t.TryGetComp<CompCanBeDormant>()?.WakeUp();
                foreach (var c in r.closed) c.EjectContents();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events: an old place opened", e);
            }
            return r.Dangerous;
        }

        /// <summary>Where a place is from the colony, in words ("the old walls to the north").</summary>
        public static string Name(OldPlace p, Map map)
        {
            if (p == null) return "?";
            if (p.landmark) return p.name ?? "?";
            var centre = CampSite.ColonyCenter(map);
            string dir = centre.IsValid ? CampSite.Direction(p.cell, centre) : null;
            return "CC_PEv_OldWalls".Translate((dir ?? "").Named("WHERE")).Resolve();
        }
    }

    // ===================== 19. What lies in the old place (the learned society) =====================

    public class MeasureState : CampWorkState
    {
        public string key, name, reading;
        public IntVec3 cell = IntVec3.Invalid;
        public bool open, told, opened, landmark;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref key, "key");
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref reading, "reading");
            Scribe_Values.Look(ref cell, "cell", IntVec3.Invalid);
            Scribe_Values.Look(ref open, "open");
            Scribe_Values.Look(ref told, "told");
            Scribe_Values.Look(ref opened, "opened");
            Scribe_Values.Look(ref landmark, "landmark");
        }
    }

    /// <summary>
    /// Two scholars camped four days beside an old place, measuring it by day; at the end they say exactly what is inside,
    /// the game's own contents (OldPlaces.Read), and leave the colony research toward its project. Let open, they open it
    /// as they go, and whatever sleeps there wakes: the game's own danger.
    /// </summary>
    public class CampWorker_Measure : CampWorker
    {
        private static int Now => Find.TickManager.TicksGame;

        public static MeasureState StateOf(Camp c) => c?.workState as MeasureState;

        public static OldPlace PlaceOf(MeasureState st, Map map) => st?.key == null ? null : OldPlaces.Find(map).FirstOrDefault(p => p.key == st.key);

        public override IntVec3 SiteFor(Map map, IntVec3 usual, bool shade)
        {
            var p = OldPlaces.Unread(map).FirstOrDefault();
            if (p == null) return usual;
            var c = CampLand.TryFindNear(map, p.cell, 6f, 18f, shade);
            return c.IsValid ? c : usual;
        }

        public override void Started(Camp c)
        {
            if (c.workState is MeasureState) return;
            // A camp brought by the dev tools: the old place nearest it.
            var p = OldPlaces.Unread(c.map).OrderBy(x => x.cell.DistanceToSquared(c.center)).FirstOrDefault();
            if (p != null) c.workState = new MeasureState { key = p.key, name = OldPlaces.Name(p, c.map), cell = p.cell, landmark = p.landmark };
        }

        public override void Hourly(Camp c, int hour)
        {
            var st = StateOf(c);
            if (st == null || st.told) return;
            // The last hours of their stay: the reading told, the research left, the place opened if they were let.
            if (Now >= c.untilTick - 2 * GenDate.TicksPerHour || c.DaysHere >= PeopleEventsMath.MeasureDays) Tell(c, st);
        }

        /// <summary>The reading: exactly what lies there now, in a letter; research toward the colony's project; opened if they were let.</summary>
        public static void Tell(Camp c, MeasureState st)
        {
            st.told = true;
            var map = c.map;
            var place = PlaceOf(st, map);
            var reading = place != null ? OldPlaces.Read(place, map) : new Reading();
            st.reading = st.landmark ? "CC_PEv_Read_land".Translate(st.name.Named("NAME")).Resolve() : reading.Words();
            if (st.key != null) PeopleEventsBook.NoteRead(st.key);
            var ctx = new HappeningContext { map = map, faction = c.faction, stranger = c.leader, a = IncidentWorker_Happening.Colonists(map).FirstOrDefault() };
            var research = new DilemmaEffect_Research { share = PeopleEventsMath.ResearchShare, max = PeopleEventsMath.ResearchMax };
            bool learned = research.Unavailable(ctx) == null;
            if (learned) research.Apply(ctx);
            var speaker = c.Here.FirstOrDefault();
            if (speaker != null) c.SayMoment(speaker, "read");
            string text = c.Text(st.landmark ? "CC_PEv_ReadingLand" : "CC_PEv_Reading", speaker, null, 0, st.reading);
            if (learned) text += "\n\n" + "CC_PEv_ReadingResearch".Translate(Find.ResearchManager.GetProject()?.label ?? "?").Resolve();
            if (st.open && place != null && !st.landmark)
            {
                st.opened = OldPlaces.Open(place, map);
                text += "\n\n" + c.Text(st.opened ? "CC_PEv_ReadingOpenedDanger" : "CC_PEv_ReadingOpened");
            }
            c.Tell("CC_PEv_ReadingTitle".Translate().Resolve(), text, st.opened ? LetterDefOf.ThreatSmall : LetterDefOf.PositiveEvent, true, speaker);
            c.untilTick = Mathf.Min(c.untilTick, Now + (st.opened ? GenDate.TicksPerHour : 3 * GenDate.TicksPerHour));
        }

        public override Job DayJob(Camp c, Pawn p)
        {
            var st = StateOf(c);
            if (st == null || st.told || !st.cell.IsValid || !Rand.Chance(0.55f)) return null;
            int hour = GenLocalDate.HourOfDay(c.map);
            if (hour < 7 || hour > 18) return null;
            var map = c.map;
            var place = PlaceOf(st, map);
            var around = place != null && !place.landmark ? place.rect.ExpandedBy(2).EdgeCells : GenRadial.RadialCellsAround(st.cell, 8f, false);
            var cells = around.Where(x => x.InBounds(map) && x.Standable(map) && !x.Fogged(map) && x.GetFirstPawn(map) == null && p.CanReach(x, PathEndMode.OnCell, Danger.Some)).ToList();
            var job = DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv_CampMeasure");
            if (cells.Count == 0 || job == null) return null;
            c.SayMoment(p, "measure", 0.25f);
            return JobMaker.MakeJob(job, cells.RandomElement(), st.cell);
        }

        public override void Leaving(Camp c)
        {
            // Driven off before the end, nothing is read: they say only what they measured if most of the four days were done.
            var st = StateOf(c);
            if (st != null && !st.told && c.DaysHere >= PeopleEventsMath.MeasureDays - 0.5f) Tell(c, st);
        }

        public override void AddMarks(Camp c, HashSet<string> marks)
        {
            var st = StateOf(c);
            if (st == null) return;
            if (st.opened) marks.Add("opened");
            else if (st.told) marks.Add("measured");
        }

        public override string Fill(Camp c, string text)
        {
            var st = StateOf(c);
            if (text == null) return null;
            if (text.Contains("{READING}")) text = text.Replace("{READING}", st?.reading ?? "");
            if (text.Contains("{OLDPLACE}")) text = text.Replace("{OLDPLACE}", st?.name ?? "");
            return text;
        }
    }

    /// <summary>
    /// Two scholars of the learned society come to the gate: there is an old place on the colony's land ({TALE}: an ancient
    /// danger's sealed room, or the land's landmark), and they would camp beside it four days, measuring, then say exactly
    /// what is inside and leave the colony research toward its project. Or measure it and open it themselves at the end,
    /// whatever sleeps there waking (the game's own danger).
    /// </summary>
    public class HappeningWorker_ScholarsReading : HappeningWorker_Visitors
    {
        public static CampKindDef Kind => DefDatabase<CampKindDef>.GetNamedSilentFail("Rimfolk_Camp_ScholarsMeasure");

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            Kind != null && CampTracker.Enabled && CampTracker.RoomFor(map) && PeoplesD.Friendly(PeoplesMath.Scholars) != null && OldPlaces.Unread(map).Count > 0
            && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Scholars);
            var place = OldPlaces.Unread(ctx.map).OrderBy(p => p.landmark ? 1 : 0).FirstOrDefault();
            if (ctx.faction == null || place == null) return false;
            ctx.note = place.key;
            ctx.tale = OldPlaces.Name(place, ctx.map);
            ctx.spot = place.cell;
            ctx.variant = place.landmark ? 1 : 0;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Scholars_Fellow"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.group.Add(Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Scholars_Fellow"), ctx.faction, ext.ages));
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Intellectual) ?? PeoplesD.Able(ctx.map).FirstOrDefault();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            tags.Add(ctx.variant == 1 ? "landmark" : "walls");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option > 1) return null;
            var place = OldPlaces.Find(ctx.map).FirstOrDefault(p => p.key == ctx.note);
            if (place == null || PeopleEventsBook.Read(ctx.note)) return "CC_PEv_PlaceGone".Translate().Resolve();
            if (option == 1 && place.landmark) return "CC_PEv_NothingToOpen".Translate().Resolve();
            if (!CampTracker.RoomFor(ctx.map)) return "CC_PEv_NoRoomForCamp".Translate().Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "measure" : "thanks");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The scholars at the gate pitch their camp beside the old place (the camp kind Rimfolk_Camp_ScholarsMeasure, they its people). <see cref="open"/>: let them open it at the end.</summary>
    public class DilemmaEffect_ScholarsMeasure : DilemmaEffect
    {
        public bool open;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return;
            sheet.Play("CC_PEv_Impact_Measure".Translate(hc.tale ?? "?").Resolve(), "CC_PEv_Impact_MeasureBrief".Translate().Resolve());
            new DilemmaEffect_Research { share = PeopleEventsMath.ResearchShare, max = PeopleEventsMath.ResearchMax }.Describe(ctx, sheet);
            if (open) sheet.Add(ImpactKind.Health, -1, "CC_PEv_Impact_Open".Translate(hc.tale ?? "?").Resolve(), "CC_PEv_Impact_OpenBrief".Translate().Resolve(), "pevopen");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            var map = hc?.map;
            var tracker = CampTracker.Instance;
            var kind = HappeningWorker_ScholarsReading.Kind;
            var place = OldPlaces.Find(map).FirstOrDefault(p => p.key == hc?.note);
            var pawns = hc == null ? new List<Pawn>() : PeoplesG.Party(hc).Where(p => PeoplesG.Up(p, map)).ToList();
            if (map == null || tracker == null || kind == null || place == null || pawns.Count == 0) return;
            try
            {
                var usual = CampWeather.StayRange(map, GenDate.TicksPerDay * 5);
                bool hot = CampWeatherMath.PitchForHeat(usual.high);
                var center = CampLand.TryFindNear(map, place.cell, 6f, 18f, hot);
                if (!center.IsValid && !CampSite.TryFind(map, out center, hot)) return;
                var setup = tracker.Make(map, center, CampSite.ColonyCenter(map), kind, hc.faction, hot, null, null, pawns);
                if (setup == null) return;
                setup.stayTicks = Mathf.RoundToInt(PeopleEventsMath.MeasureDays * GenDate.TicksPerDay);
                var c = tracker.Start(setup);
                if (c == null) return;
                c.workState = new MeasureState { key = place.key, name = hc.tale, cell = place.cell, open = open, landmark = place.landmark };
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events: the scholars' camp", e);
            }
        }
    }
}
