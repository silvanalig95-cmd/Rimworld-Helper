using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// What the six quests at home share (quests, round 1; the workers in Workers_MissionHome.cs and
    /// Workers_MissionHome2.cs, the pure rules in HomeQuestsMath.cs): who of the colony deals with them, the reward by the
    /// asking people's age (silver, or a tribe's goods), the people a prisoner or an outlaw comes from, places far out on
    /// the colony's land, the letter and the keepsake by the asking people's age, and their words over a head.
    /// </summary>
    public static class HomeQuests
    {
        private static int Now => Find.TickManager.TicksGame;

        /// <summary>One of the quests at home: round 1's six, round 2's seven (HomeQuests2.cs), or round 3's cache in old ruins (Workers_MissionHome5.cs).</summary>
        public static bool IsOurs(Mission m) =>
            m != null && (Array.IndexOf(HomeQuestsMath.Kinds, m.kind) >= 0 || HomeQuests2.IsOurs(m) || Array.IndexOf(HomeQuests3Math.Kinds, m.kind) >= 0);

        /// <summary>The open quest at home of this kind, if any.</summary>
        public static Mission Open(string kind) => Missions.All.FirstOrDefault(m => m.Open && m.kind == kind);

        // ---------------- Who of the colony deals with it ----------------

        /// <summary>The holder of an office, at home and up; else the colony's best at the skill; else any adult.</summary>
        public static Pawn Officer(Map map, string office, SkillDef skill)
        {
            var o = office == null ? null : Interplay.Officer(office, map);
            if (o != null && o.Spawned && o.Map == map && !o.Downed && !o.Dead) return o;
            return (skill != null ? VarietyUtil.Best(map, skill) : null) ?? StorytellerEvents.Adults(map).RandomElementWithFallback();
        }

        // ---------------- The reward ----------------

        public static int AgeOf(Faction f) => (int)Ages.Of(f);

        public static int Reward(HomeQuest q, Faction f, Map map) => HomeQuestsMath.Reward(q, AgeOf(f), Ages.IsEmpire(f), LivingEvents.WealthFactor(map), Rand.Value);

        public static bool InGoods(Mission m) => m?.faction != null && HomeQuestsMath.InGoods(AgeOf(m.faction), Ages.IsEmpire(m.faction));

        /// <summary>What they hand over: silver, or for a tribe things of the colony's era worth as much (Loot).</summary>
        public static List<Thing> PayThings(Mission m, int amount)
        {
            if (amount <= 0) return new List<Thing>();
            if (InGoods(m))
            {
                var goods = Loot.Make(amount);
                if (!goods.NullOrEmpty()) return goods;
            }
            return Couriers.Make(ThingDefOf.Silver, amount);
        }

        /// <summary>The reward carried in by someone of theirs (Couriers/), or by <paramref name="bearer"/> already here; else set down at once.</summary>
        public static void Pay(Mission m, Map map, IntVec3 near, Pawn bearer, int amount)
        {
            if (map == null || amount <= 0) return;
            if (!near.IsValid) near = DropCellFinder.TradeDropSpot(map);
            if (InGoods(m)) Couriers.Loot(PayThings(m, amount), map, near, m.faction, bearer);
            else Couriers.PaySilver(map, near, amount, m.faction, bearer);
        }

        /// <summary>The reward in words: "240 silver", or "goods of their own making worth about 240 silver".</summary>
        public static string RewardWords(Mission m, int amount) =>
            (InGoods(m) ? "CC_HomeQ_PayGoods" : "CC_HomeQ_PaySilver").Translate(amount.Named("COUNT")).Resolve();

        /// <summary>The people will owe the colony a favour for a quest done (the "mission" deed, World/FactionMemory.cs).</summary>
        public static bool FavourOwed
        {
            get
            {
                try
                {
                    return FavoursOwed.On;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// A quest's terms under its offer: {ITEM} the reward in words (or <paramref name="item"/>), {DAYS} the days it
        /// names (none when 0), and a plain line that the people will owe the colony a favour when they will.
        /// </summary>
        public static string Terms(HappeningWorker_Mission w, HappeningContext ctx, Mission m, Func<Mission, int> days, Func<Mission, int> reward, string item = null)
        {
            if (m == null || w == null) return null;
            int d = days(m);
            string line = VarietyUtil.Line(w.More?.terms, ctx, Missions.Tags(m), item ?? RewardWords(m, reward(m)), d > 0 ? TextUtil.DaysString(d * GenDate.TicksPerDay) : null);
            if (line.NullOrEmpty()) return null;
            if (FavourOwed && m.faction != null) line += " " + "CC_HomeQ_Favour".Translate(m.faction.Name.Named("FACTION")).Resolve();
            return line;
        }

        // ---------------- Someone handed back to their people ----------------

        /// <summary>The colony's prisoner collected by their people, who bring the reward (Couriers/). False: nobody comes, and the caller does it at once.</summary>
        public static bool FetchPrisoner(Mission m, Map map, Pawn x, int amount)
        {
            if (map == null || x == null || x.Dead || !x.Spawned || x.Map != map || !x.IsPrisonerOfColony) return false;
            return Couriers.Fetch(map, x, m.faction, PayThings(m, amount), x.Position, true);
        }

        /// <summary>Their people have them: walking off as theirs, or taken by a courier who came for them.</summary>
        public static bool Collected(Mission m, Pawn x)
        {
            if (x == null || x.Dead) return false;
            if (x.Faction == m.faction && !x.IsPrisoner) return true;
            var trips = CourierTracker.Instance?.trips;
            return trips != null && trips.Any(t => t != null && t.fetch == x && t.fetched);
        }

        /// <summary>Taken away at once (no courier came): the prisoner off the map and back in the world.</summary>
        public static void TakeNow(Pawn x)
        {
            if (x == null || x.Dead || !x.IsPrisonerOfColony) return;
            try
            {
                if (x.Spawned) x.DeSpawn();
                x.guest?.SetGuestStatus(null);
                if (!Find.WorldPawns.Contains(x)) Find.WorldPawns.PassToWorld(x, PawnDiscardDecideMode.Decide);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("quests at home: someone taken away", e);
            }
        }

        /// <summary>A thing in the hands or packs of someone of <paramref name="f"/>.</summary>
        public static bool HeldBy(Thing t, Faction f) =>
            t != null && f != null && (t.ParentHolder is Pawn_InventoryTracker inv && inv.pawn?.Faction == f || t.ParentHolder is Pawn_CarryTracker c && c.pawn?.Faction == f);

        // ---------------- A messenger's wounds ----------------

        /// <summary>
        /// The wounds of an attack on the road, the game's own: cuts and stabs from a low-tech band, gunshots and cuts from
        /// a modern one, bites and scratches from wild animals. Two or three on the arms, legs and body, each a share of the
        /// part's hit points, never so many that they can't walk or would bleed to death before they could be treated.
        /// </summary>
        public static void Wound(Pawn x, bool beasts, int age)
        {
            if (x?.health?.hediffSet == null) return;
            try
            {
                string[] names = beasts ? new[] { "Bite", "Scratch" } : age >= HomeQuestsMath.Industrial ? new[] { "Gunshot", "Cut" } : new[] { "Cut", "Stab" };
                var defs = names.Select(n => DefDatabase<HediffDef>.GetNamedSilentFail(n)).Where(d => d != null).ToList();
                if (defs.Count == 0) defs.Add(HediffDefOf.Cut);
                string[] limbs = { "Arm", "Shoulder", "Leg", "Torso", "Hand", "Foot" };
                var parts = x.health.hediffSet.GetNotMissingParts(BodyPartHeight.Undefined, BodyPartDepth.Outside).Where(p => limbs.Contains(p.def.defName)).ToList();
                if (parts.Count == 0)
                    parts = x.health.hediffSet.GetNotMissingParts(BodyPartHeight.Undefined, BodyPartDepth.Outside).Where(p => p.def.hitPoints >= 15 && p.depth == BodyPartDepth.Outside
                                                                                                                              && !p.def.tags.Any(t => t != null && t.vital)).ToList();
                int n = Rand.RangeInclusive(HomeQuestsMath.WoundsMin, HomeQuestsMath.WoundsMax);
                for (int i = 0; i < n && parts.Count > 0; i++)
                {
                    var part = parts.RandomElement();
                    var h = HediffMaker.MakeHediff(defs.RandomElement(), x, part);
                    h.Severity = Mathf.Max(1f, part.def.GetMaxHealth(x) * Rand.Range(HomeQuestsMath.WoundShareMin, HomeQuestsMath.WoundShareMax));
                    x.health.AddHediff(h, part);
                    bool tooMuch = x.health.Downed || x.health.hediffSet.BleedRateTotal > 0f && HealthUtility.TicksUntilDeathDueToBloodLoss(x) < HomeQuestsMath.MinBleedDays * GenDate.TicksPerDay;
                    if (tooMuch)
                    {
                        x.health.RemoveHediff(h);
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("quests at home: the messenger's wounds", e);
            }
        }

        /// <summary>
        /// A quest at home's tags for its lines: [goods] when a tribe pays in goods, [favour] when the people will owe the
        /// colony for it (Diplomacy/FavoursOwed.cs), [scuffle] when the quarrel came to blows. A quest's variant is a tag of
        /// its own already (the outlaw's [hills] or [edge], the messenger's [raiders] or [beasts], the keepsake's kind).
        /// </summary>
        public static void AddTags(Mission m, List<string> tags)
        {
            if (!IsOurs(m) || tags == null) return;
            if (InGoods(m)) tags.Add("goods");
            if (FavourOwed) tags.Add("favour");
            if (m.kind == HomeQuestsMath.DisputeKind && m.misses > 0) tags.Add("scuffle");
        }

        // ---------------- The people a prisoner or an outlaw comes from ----------------

        /// <summary>A people at war with the colony that fights (pirates, a raiding tribe), nearest in age to <paramref name="like"/>.</summary>
        public static Faction Enemy(Faction like)
        {
            int age = like != null ? AgeOf(like) : (int)Ages.Ours;
            var all = Find.FactionManager.AllFactionsListForReading.Where(f => Raids.CanRaid(f) && f != like).ToList();
            if (all.Count == 0) return null;
            // Their own enemies first: a people takes prisoners and puts prices on the heads of those it is at war with.
            return all.RandomElementByWeight(f => Mathf.Pow(0.35f, Mathf.Abs(AgeOf(f) - age)) * (like != null && f.HostileTo(like) ? 3f : 1f));
        }

        /// <summary>
        /// One of a people's fighters at most <paramref name="power"/> strong (the game's combat power): the strongest few
        /// under it; the weakest they have when none is. Null when they have no people to send.
        /// </summary>
        public static PawnKindDef FighterKind(Faction f, float power)
        {
            var kinds = f?.def?.pawnGroupMakers?.Where(g => g?.options != null && g.kindDef == PawnGroupKindDefOf.Combat).SelectMany(g => g.options)
                .Select(o => o.kind).Where(k => k?.RaceProps?.Humanlike == true && k.combatPower > 0f).Distinct().ToList();
            if (kinds.NullOrEmpty()) return Arrivals.PersonKind(f);
            var under = kinds.Where(k => k.combatPower <= power).OrderByDescending(k => k.combatPower).Take(3).ToList();
            return under.Count > 0 ? under.RandomElement() : kinds.OrderBy(k => k.combatPower).First();
        }

        /// <summary>Someone of a people for a quest, kept in the world until they come.</summary>
        public static Pawn Person(Faction f, FloatRange ages, PawnKindDef kind = null)
        {
            kind ??= Arrivals.PersonKind(f);
            if (kind == null) return null;
            Pawn p = null;
            try
            {
                p = Arrivals.Make(kind, f, ages);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("quests at home: someone made", e);
            }
            if (p != null) Arrivals.Hold(p);
            return p;
        }

        /// <summary>Takes away what they carry to fight with: a prisoner comes in with nothing.</summary>
        public static void Disarm(Pawn p)
        {
            try
            {
                p?.equipment?.DestroyAllEquipment();
                p?.inventory?.DestroyAll();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("quests at home: a prisoner's weapons", e);
            }
        }

        // ---------------- Places on the land ----------------

        /// <summary>
        /// A cell well out from the colony: standable, open sky, not the colony's ground, seen, reachable from its middle,
        /// at least <paramref name="min"/> from it (less if there's no room), and as <paramref name="also"/> wants.
        /// </summary>
        public static bool FarCell(Map map, float min, Func<IntVec3, bool> also, out IntVec3 cell)
        {
            cell = IntVec3.Invalid;
            if (map == null) return false;
            var center = CampSite.ColonyCenter(map);
            var home = map.areaManager.Home;
            var tp = TraverseParms.For(TraverseMode.PassDoors, Danger.Some, false);
            for (int tries = 0; tries < 3; tries++)
            {
                float d = min * (tries == 0 ? 1f : tries == 1 ? 0.7f : 0.45f);
                if (CellFinder.TryFindRandomCellNear(center, map, 90, c => c.Standable(map) && !c.Roofed(map) && !c.Fogged(map) && (home == null || !home[c])
                                                                          && !c.InHorDistOf(center, d) && c.GetFirstBuilding(map) == null
                                                                          && !(c.GetTerrain(map)?.IsWater ?? false) && (also == null || also(c))
                                                                          && map.reachability.CanReach(center, c, PathEndMode.OnCell, tp), out cell)) return true;
            }
            cell = IntVec3.Invalid;
            return false;
        }

        /// <summary>Natural rock beside it (the foot of the hills), or near the edge of the map.</summary>
        public static bool Hills(Map map, IntVec3 c)
        {
            foreach (var n in GenAdjFast.AdjacentCells8Way(c))
                if (n.InBounds(map) && n.GetEdifice(map) is Building b && b.def.building != null && b.def.building.isNaturalRock) return true;
            return false;
        }

        public static bool NearEdge(Map map, IntVec3 c, int within = 12) => c.x < within || c.z < within || c.x >= map.Size.x - within || c.z >= map.Size.z - within;

        /// <summary>Which way a cell lies from the middle of the colony, in words ("north", "south-east").</summary>
        public static string Direction(Map map, IntVec3 c)
        {
            var center = CampSite.ColonyCenter(map);
            float dx = c.x - center.x, dz = c.z - center.z;
            float a = Mathf.Atan2(dz, dx) * Mathf.Rad2Deg;
            if (a < 0f) a += 360f;
            string[] keys = { "E", "NE", "N", "NW", "W", "SW", "S", "SE" };
            int i = Mathf.RoundToInt(a / 45f) % 8;
            return ("CC_HomeQ_Dir_" + keys[i]).Translate().Resolve();
        }

        // ---------------- The letter and the keepsake ----------------

        /// <summary>The messenger's letter by their people's age: a message stick, a sealed letter, a dispatch case.</summary>
        public static ThingDef LetterDef(Faction f)
        {
            int age = AgeOf(f);
            string name = Ages.IsEmpire(f) || age >= HomeQuestsMath.Industrial ? "Rimfolk_QuestDispatch" : age == HomeQuestsMath.Medieval ? "Rimfolk_QuestLetter" : "Rimfolk_QuestMessageStick";
            return DefDatabase<ThingDef>.GetNamedSilentFail(name) ?? DefDatabase<ThingDef>.GetNamedSilentFail("Rimfolk_QuestLetter");
        }

        /// <summary>The keepsake's kind as a tag ("hide", "book", "photos"), by its owner's people's age.</summary>
        public static string KeepsakeTag(Faction f)
        {
            int age = AgeOf(f);
            if (Ages.IsEmpire(f) || age == HomeQuestsMath.Medieval) return "book";
            return age >= HomeQuestsMath.Industrial ? "photos" : "hide";
        }

        public static ThingDef KeepsakeDef(string tag)
        {
            string name = tag == "book" ? "Rimfolk_KeepsakeBook" : tag == "photos" ? "Rimfolk_KeepsakePhotos" : "Rimfolk_KeepsakeHide";
            return DefDatabase<ThingDef>.GetNamedSilentFail(name);
        }

        /// <summary>The thing of a quest made: one of it, unforbidden.</summary>
        public static Thing Make(ThingDef def)
        {
            if (def == null) return null;
            var t = ThingMaker.MakeThing(def);
            t.stackCount = 1;
            return t;
        }

        /// <summary>Where a thing of the colony's is: on this map, in someone's hands or packs on it, or in a container here.</summary>
        public static bool OnMap(Thing t, Map map) => t != null && !t.Destroyed && map != null && t.MapHeld == map;

        // ---------------- Words over a head ----------------

        /// <summary>A line from a pool over someone's head ({PAWN}, {OTHER}).</summary>
        public static void Say(Pawn p, string pool, Pawn to = null, string tag = null)
        {
            try
            {
                if (p == null || !p.Spawned || p.Dead) return;
                if (to == p) to = null;
                string line = Flavor.FromPool(pool, p, null, null, tag);
                if (line.NullOrEmpty()) return;
                var args = to != null ? new[] { p.Named("PAWN"), to.Named("OTHER") } : new[] { p.Named("PAWN") };
                // together: the caller has them face to face when it names the other.
                Speech.Say(p, line.Formatted(args).Resolve(), to);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("quests at home: a line from " + pool, e);
            }
        }
    }

    // ===================== The keepsake searched for =====================

    /// <summary>
    /// A look for a lost keepsake (Workers_MissionHome2.cs): the colonist walks to a spot in the marked area and searches
    /// the ground around it for about an hour. Found, it is set down where it lay; not found, they go on to the next spot
    /// not yet searched, until it is found, the area is all searched, or they are called away.
    /// </summary>
    public class JobDriver_SearchKeepsake : JobDriver
    {
        public const string JobName = "Rimfolk_SearchKeepsake";

        private IntVec3 Spot => job.targetA.Cell;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);

        public override string GetReport()
        {
            var m = HomeQuests.Open(HomeQuestsMath.KeepsakeKind);
            return "CC_HomeQ_Keepsake_Job".Translate((m?.name ?? "?").Named("OWNER"), (HomeQuests.KeepsakeDef(m?.variant)?.label ?? "?").Named("THING")).Resolve();
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => !HappeningWorker_MissionKeepsake.Searching(pawn.Map, out _));
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            var look = ToilMaker.MakeToil("SearchKeepsake");
            look.defaultCompleteMode = ToilCompleteMode.Delay;
            look.defaultDuration = HomeQuestsMath.LookTicks;
            look.WithProgressBarToilDelay(TargetIndex.A);
            look.handlingFacing = true;
            look.tickIntervalAction = delta =>
            {
                // Turning to look around the spot, now this way, now that.
                if (pawn.IsHashIntervalTick(250, delta)) pawn.Rotation = Rot4.Random;
            };
            yield return look;
            yield return Toils_General.Do(() => HappeningWorker_MissionKeepsake.Looked(pawn, Spot));
        }
    }

    /// <summary>Right-clicking inside the marked area with a colonist selected: search here for the keepsake.</summary>
    public class FloatMenuOptionProvider_SearchKeepsake : FloatMenuOptionProvider
    {
        protected override bool Drafted => false;

        protected override bool Undrafted => true;

        protected override bool Multiselect => false;

        protected override bool RequiresManipulation => false;

        protected override bool AppliesInt(FloatMenuContext context) => HappeningWorker_MissionKeepsake.Searching(context.map, out _);

        protected override FloatMenuOption GetSingleOption(FloatMenuContext context)
        {
            var c = context.FirstSelectedPawn;
            if (c == null || !c.IsColonistPlayerControlled || c.DevelopmentalStage.Baby() || !HappeningWorker_MissionKeepsake.Searching(context.map, out var m)) return null;
            var cell = context.ClickedCell;
            if (!HappeningWorker_MissionKeepsake.InArea(m, cell)) return null;
            string label = "CC_HomeQ_Keepsake_Menu".Translate((m.name ?? "?").Named("OWNER"), (HomeQuests.KeepsakeDef(m.variant)?.label ?? "?").Named("THING")).Resolve();
            if (!cell.Standable(context.map) || !c.CanReach(cell, PathEndMode.OnCell, Danger.Deadly))
                return new FloatMenuOption(label + " (" + "CC_HomeQ_Keepsake_NoReach".Translate().Resolve() + ")", null);
            return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(label, () =>
            {
                var def = DefDatabase<JobDef>.GetNamedSilentFail(JobDriver_SearchKeepsake.JobName);
                if (def != null) c.jobs.TryTakeOrderedJob(JobMaker.MakeJob(def, cell), JobTag.Misc);
            }), c, cell);
        }
    }

    /// <summary>The area marked for a keepsake's search, drawn on the map while it is searched for.</summary>
    public class HomeQuestOverlay : MapComponent
    {
        private static readonly Color Edge = new Color(0.95f, 0.8f, 0.35f, 0.8f);
        private List<IntVec3> cells;
        private IntVec3 cellsFor = IntVec3.Invalid;

        public HomeQuestOverlay(Map map) : base(map)
        {
        }

        public override void MapComponentUpdate()
        {
            if (map != Find.CurrentMap) return;
            try
            {
                if (!HappeningWorker_MissionKeepsake.Searching(map, out var m)) return;
                if (cells == null || cellsFor != m.area)
                {
                    cells = HappeningWorker_MissionKeepsake.AreaCells(map, m.area).ToList();
                    cellsFor = m.area;
                }
                if (cells.Count > 0) GenDraw.DrawFieldEdges(cells, Edge);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("quests at home: the search area drawn", e);
            }
        }
    }
}
