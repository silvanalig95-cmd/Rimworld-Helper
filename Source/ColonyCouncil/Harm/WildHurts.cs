using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Harm, part 6: hurts from animals that aren't the colony's own (Harm/HarmSix.cs is the hub). The animals are found by
    /// what they are, never by a list. The pets' own hunting (Animals/PetHunts.cs) and the wild's alphas and great herds
    /// (Beasts/) are left as they are: nothing here makes a pet hunt, a pack gather or a herd run.
    /// <list type="bullet">
    /// <item>The drovers' bull (2.4.3): an hour among a camp's herd (within three cells of one of their beasts of a body of
    /// 0.5 or more), one charge in 30, once a stay: a bruise, a crack one time in six, a child's small bruise; a drover's
    /// word. Told as the game tells an animal turning on its handler. With the camps.</item>
    /// <item>The wild one too close (2.10.2): an encounter in which one of ours steps close to a wild animal (the great
    /// beast close, the wild thing fed, a child meeting a wild animal), by the game's own taming-revenge chance for the kind,
    /// about one in 20: a kick from a big one, a bite from a small one (named for the animal, so it can carry what bites
    /// carry by the ailments' own roll), a child nipped. The nervous never step closer. With the small moments with animals.</item>
    /// <item>Walks the fields at dawn (3.11.4): one with the quirk walks out to a field's edge at dawn (or is already at the
    /// growing there); alone, beyond the walls, with a hungry wild predator in reach that would take them, one dawn in eight
    /// it comes, by the game's own hunt. Walls round the fields keep it off. With the quirks.</item>
    /// <item>The fed wolf grows bold (4.5.2): a resident predator the colony leaves food out for (its "fed" answer), three
    /// days on, shows itself at the stores' door at dusk; a night with someone of ours alone near the stores, one in 40, it
    /// comes for them (the game's own hunt, stopping once they're down). Keeping close, driving it off or taming it stop it;
    /// never a grazer. Named under the answer. With the beast that stays.</item>
    /// <item>A catch that fights back (4.6.1, PokéWorld): the trainer gone out to catch the wild Pokémon ("let them try"),
    /// once it's in reach, one try in four it fights, PokéWorld's own way (the game's attack, which its moves are); a partner
    /// Pokémon of the trainer's at their side takes it instead. Named under the answer. With PokéWorld and the beast that stays.</item>
    /// <item>The visitor at the stores turns (4.6.2, PokéWorld): the Pokémon the colony feeds comes to the stores at dusk one
    /// day in three; whoever of ours is free goes to shoo it, and one shooing in ten it swipes at them (a scratch) before it
    /// runs. Keeping the stores shut or catching it stops it. Named under the answer.</item>
    /// </list>
    /// </summary>
    public static class WildHurts
    {
        private static int Now => Find.TickManager.TicksGame;

        public static bool DebugBull, DebugClose, DebugDawn, DebugBold, DebugCatch, DebugSwipe;

        /// <summary>Camps whose herd charged someone this stay (by the camp), session memory.</summary>
        private static readonly HashSet<Camp> charged = new HashSet<Camp>();
        /// <summary>A colonist's dawn, a map's dusk and night at the stores, a Pokémon's try and visit: the day each was looked at.</summary>
        private static readonly Dictionary<int, int> dawnDay = new Dictionary<int, int>(), boldDusk = new Dictionary<int, int>(), boldNight = new Dictionary<int, int>(),
            catchDay = new Dictionary<int, int>(), visitDay = new Dictionary<int, int>();
        /// <summary>The one gone to shoo the visitor at the stores: the visitor.</summary>
        private static readonly Dictionary<int, Pawn> shooing = new Dictionary<int, Pawn>();

        private static QuirkDef walksFields;
        private static TraitDef nerves;
        private static bool looked;

        public static void Reset()
        {
            charged.Clear();
            dawnDay.Clear();
            boldDusk.Clear();
            boldNight.Clear();
            catchDay.Clear();
            visitDay.Clear();
            shooing.Clear();
            looked = false;
        }

        private static void Look()
        {
            if (looked) return;
            looked = true;
            walksFields = DefDatabase<QuirkDef>.GetNamedSilentFail("Council_Quirk_WalksFields");
            nerves = DefDatabase<TraitDef>.GetNamedSilentFail("Nerves");
        }

        private static int Day(Map map) => GenLocalDate.Year(map) * 60 + GenLocalDate.DayOfYear(map);

        /// <summary>Each colonist's hour: among a camp's herd, a dawn at the fields.</summary>
        public static void Hour(Pawn p, int now)
        {
            if (!Harm.On || !HarmFour.Ours(p)) return;
            Look();
            Bull(p);
            Dawn(p);
        }

        /// <summary>The harm book's hour: the beast that stays at each home, at the stores, or being caught.</summary>
        public static void Hourly(int now)
        {
            if (!Harm.On) return;
            var comp = CouncilGameComponent.Instance;
            if (comp == null) return;
            var maps = Find.Maps;
            for (int i = 0; i < maps.Count; i++)
            {
                var map = maps[i];
                if (!map.IsPlayerHome) continue;
                var rb = comp.BeastOf(map);
                if (rb == null || !rb.Here || rb.animal.Map != map) continue;
                Bold(rb, map, now);
                Catch(rb, map);
                Visitor(rb, map);
            }
            charged.RemoveWhere(c => c == null || !c.Settled);
        }

        private static void Charge(Pawn beast, Pawn p)
        {
            beast.rotationTracker?.FaceTarget(p);
            beast.Drawer?.Notify_MeleeAttackOn(p);
            if (beast.Map != null) FleckMaker.ThrowDustPuff(beast.Position, beast.Map, 1.2f);
        }

        // ================= The drovers' bull (2.4.3) =================

        private static void Bull(Pawn p)
        {
            if (!Together.Able(p) || !Harm.SystemOn(HarmKinds.DroversBull.system)) return;
            var t = CampTracker.Instance;
            if (t == null) return;
            foreach (var c in t.All)
            {
                if (c == null || c.map != p.Map || !c.Settled || c.Hostile || c.herd.NullOrEmpty() || charged.Contains(c)) continue;
                Pawn beast = null;
                foreach (var a in c.herd)
                    if (a != null && a.Spawned && !a.Dead && !a.Downed && a.Map == p.Map && !a.InMentalState && a.BodySize >= HarmSixMath.HerdBeastSize
                        && a.Position.InHorDistOf(p.Position, HarmSixMath.HerdRange))
                    {
                        beast = a;
                        break;
                    }
                if (beast == null) continue;
                var cls = HarmFour.Child(p) ? HurtClass.Mild : HurtClass.Injury;
                bool debug = DebugBull;
                if (debug ? !Harm.May(p, cls) : !Harm.Roll(p, HarmKinds.DroversBull, HarmSixMath.DroversChance, cls)) return;
                DebugBull = false;
                charged.Add(c);
                Charge(beast, p);
                bool crack = cls == HurtClass.Injury && Rand.Chance(HarmSixMath.BullCrackShare);
                Hediff_Injury h;
                if (cls == HurtClass.Mild) h = Harm.Injure(p, HurtKind.Bruise, HurtWhere.Leg, HarmSixMath.BullChildLow, HarmSixMath.BullChildHigh, HarmKinds.DroversBull, cls);
                else
                {
                    h = crack ? Harm.Injure(p, HurtKind.Crack, HurtWhere.Leg, HarmSixMath.BullCrackLow, HarmSixMath.BullCrackHigh, HarmKinds.DroversBull) : null;
                    if (h == null)
                    {
                        crack = false;
                        h = Harm.Injure(p, HurtKind.Bruise, Rand.Bool ? HurtWhere.Torso : HurtWhere.Leg, HarmSixMath.BullLow, HarmSixMath.BullHigh, HarmKinds.DroversBull);
                    }
                }
                if (h == null) return;
                HarmFour.SitDown(p, 300);
                HarmFour.Say(p, "Rimfolk_Pool_DroverCharge", null, crack ? "crack" : "bruise");
                var drover = c.Here.FirstOrDefault(o => Together.Can(o, p));
                // together: Together.Can, one of the camp beside them at the herd.
                if (drover != null) HarmFour.Say(drover, "Rimfolk_Pool_DroverWord", p);
                HarmFour.TellHurt(p, "CC_HarmSix_DroversBull", p.Named("PAWN"), beast.Named("ANIMAL"), (h.Part?.Label ?? "").Named("PART"));
                return;
            }
        }

        // ================= The wild one too close (2.10.2) =================

        /// <summary>An encounter's colonist stepped close to a wild animal (DilemmaEffect_TooClose): its one roll.</summary>
        public static void TooClose(Pawn a, Pawn x)
        {
            if (!Harm.On || !HarmFour.Ours(a) || x?.RaceProps == null || !x.RaceProps.Animal || x.Faction != null || x.Dead || x.Downed || !x.Spawned || x.Map != a.Map) return;
            if (!a.Position.InHorDistOf(x.Position, HarmSixMath.TooCloseRange) || !Harm.SystemOn(HarmKinds.TooClose.system)) return;
            Look();
            bool cautious = nerves != null && (a.story?.traits?.DegreeOfTrait(nerves) ?? 0) < 0;
            var cls = HarmFour.Child(a) ? HurtClass.Mild : HurtClass.Injury;
            bool debug = DebugClose;
            DebugClose = false;
            float chance = HarmSixMath.TooClose(x.RaceProps.manhunterOnTameFailChance, cautious);
            if (debug ? !Harm.May(a, cls) : chance <= 0f || !Harm.Roll(a, HarmKinds.TooClose, chance, cls)) return;
            bool big = x.BodySize >= HarmSixMath.KickSize;
            Charge(x, a);
            Hediff_Injury h;
            if (cls == HurtClass.Mild) h = Harm.Injure(a, HurtKind.Bite, HurtWhere.Hand, HarmSixMath.NipLow, HarmSixMath.NipHigh, HarmKinds.TooClose, cls, x);
            else if (big) h = Harm.Injure(a, HurtKind.Bruise, Rand.Bool ? HurtWhere.Leg : HurtWhere.Torso, HarmSixMath.KickLow, HarmSixMath.KickHigh, HarmKinds.TooClose, cls, x);
            else h = Harm.Injure(a, HurtKind.Bite, Rand.Bool ? HurtWhere.Hand : HurtWhere.Arm, HarmSixMath.BiteLow, HarmSixMath.BiteHigh, HarmKinds.TooClose, cls, x);
            if (h == null) return;
            Flee(x, a);
            HarmFour.Say(a, "Rimfolk_Pool_TooClose", null, cls == HurtClass.Mild ? "nip" : big ? "kick" : "bite");
            HarmFour.Mote(a, big && cls != HurtClass.Mild ? "CC_HarmSix_Mote_Kicked" : "CC_HarmSix_Mote_Bitten", x.Named("ANIMAL"));
        }

        /// <summary>An animal off and away from someone, by the game's own fleeing.</summary>
        private static void Flee(Pawn x, Pawn from)
        {
            try
            {
                if (x?.jobs == null || !x.Spawned) return;
                var dest = CellFinderLoose.GetFleeDest(x, new List<Thing> { from }, 14f);
                if (!dest.IsValid) return;
                var job = JobMaker.MakeJob(JobDefOf.Flee, dest, from);
                x.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm six: an animal fleeing", e);
            }
        }

        // ================= Walks the fields at dawn (3.11.4) =================

        private static void Dawn(Pawn p)
        {
            if (walksFields == null || !MoreHabits.HasQuirk(p, walksFields) || HarmFour.Child(p) && !DebugDawn) return;
            bool debug = DebugDawn;
            if (!debug && !HarmSixMath.Dawn(GenLocalDate.HourOfDay(p.Map))) return;
            int day = Day(p.Map);
            if (!debug && dawnDay.TryGetValue(p.thingIDNumber, out int d) && d == day) return;
            if (dawnDay.Count > 200) dawnDay.Clear();
            dawnDay[p.thingIDNumber] = day;
            if (!Harm.SystemOn(HarmKinds.DawnWalk.system) || p.Map.dangerWatcher.DangerRating != StoryDanger.None) return;
            // Already out at the growing: this is the dawn walk.
            if (p.CurJob?.workGiverDef?.workType?.defName == "Growing" && ColdHurts.Outdoors(p))
            {
                DawnLook(p);
                return;
            }
            if (!Together.Able(p) || !Together.FreeTime(p) && !debug) return;
            var cell = FieldEdge(p);
            if (!cell.IsValid) return;
            var job = JobMaker.MakeJob(HarmSixDefOf.Rimfolk_DawnWalk, cell);
            job.count = HarmSixMath.DawnWalkTicks;
            job.locomotionUrgency = LocomotionUrgency.Amble;
            p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
        }

        /// <summary>A cell at the edge of one of the colony's fields under the open sky, the nearest field first.</summary>
        private static IntVec3 FieldEdge(Pawn p)
        {
            var map = p.Map;
            foreach (var z in map.zoneManager.AllZones.OfType<Zone_Growing>().OrderBy(z => z.Position.DistanceToSquared(p.Position)))
            {
                foreach (var c in z.Cells.InRandomOrder().Take(40))
                {
                    if (c.Roofed(map) || !c.Standable(map) || c.IsForbidden(p)) continue;
                    bool edge = false;
                    foreach (var n in GenAdj.CellsAdjacent8Way(new TargetInfo(c, map)))
                        if (n.InBounds(map) && n.GetZone(map) != z)
                        {
                            edge = true;
                            break;
                        }
                    if (edge && p.CanReach(c, PathEndMode.OnCell, Danger.Some)) return c;
                }
            }
            return IntVec3.Invalid;
        }

        /// <summary>At the field's edge (JobDriver_HarmWalk's arrival, or the growing at dawn): alone, beyond the walls, a hungry predator may come.</summary>
        public static void DawnArrived(Pawn p) => DawnLook(p);

        private static void DawnLook(Pawn p)
        {
            if (p?.Map == null) return;
            bool alone = !p.Map.mapPawns.FreeColonistsSpawned.Any(o => o != p && o.Position.InHorDistOf(p.Position, HarmSixMath.AloneRange));
            bool outside = NightWalks.OutsideWalls(p.Map, p.Position);
            var predator = alone && outside ? NightWalks.PredatorFor(p) : null;
            float chance = HarmSixMath.DawnPredator(alone, outside, predator != null);
            bool debug = DebugDawn && predator != null;
            DebugDawn = false;
            if (debug ? Harm.May(p, HurtClass.Injury) : chance > 0f && Harm.Roll(p, HarmKinds.DawnWalk, chance, HurtClass.Injury))
            {
                if (!NightWalks.Hunt(predator, p, HarmKinds.DawnWalk)) return;
                HarmFour.Say(p, "Rimfolk_Pool_DawnPredator");
                if (p.CurJobDef == HarmSixDefOf.Rimfolk_DawnWalk) p.jobs.EndCurrentJob(JobCondition.InterruptForced);
                return;
            }
            // apart: a word to nobody over the rows at first light, now and then.
            if (p.CurJobDef == HarmSixDefOf.Rimfolk_DawnWalk && Rand.Chance(0.4f)) HarmFour.Say(p, "Rimfolk_Pool_DawnWalk");
        }

        // ================= The fed wolf grows bold (4.5.2) =================

        /// <summary>The middle of the colony's stores: the biggest stockpile's middle cell, or invalid with none.</summary>
        private static IntVec3 Stores(Map map)
        {
            var z = map.zoneManager.AllZones.OfType<Zone_Stockpile>().OrderByDescending(s => s.CellCount).FirstOrDefault();
            return z?.Position ?? IntVec3.Invalid;
        }

        private static void Bold(ResidentBeast rb, Map map, int now)
        {
            if (rb.pokemon || !rb.Predator || !Harm.SystemOn(HarmKinds.BoldWolf.system)) return;
            float daysFed = rb.lastAskTick < 0 ? 0f : (now - rb.lastAskTick) / (float)HarmMath.DayTicks;
            bool debug = DebugBold;
            if (!debug && !HarmSixMath.Bold(rb.Predator, rb.stance == "fed" && !rb.feared, daysFed)) return;
            var stores = Stores(map);
            if (!stores.IsValid || map.dangerWatcher.DangerRating != StoryDanger.None) return;
            int hour = GenLocalDate.HourOfDay(map), day = Day(map);
            var wolf = rb.animal;
            if (HarmSixMath.Dusk(hour) && !(boldDusk.TryGetValue(map.uniqueID, out int dd) && dd == day))
            {
                boldDusk[map.uniqueID] = day;
                AtTheDoor(wolf, stores, map);
                return;
            }
            // A night is counted from the evening it begins.
            int night = (now + 6 * HarmMath.HourTicks) / HarmMath.DayTicks;
            if (!HarmSixMath.Night(hour) && !debug || boldNight.TryGetValue(map.uniqueID, out int dn) && dn == night && !debug) return;
            var victim = map.mapPawns.FreeColonistsSpawned.Where(o => HarmFour.Ours(o) && Together.Able(o) && o.Position.InHorDistOf(stores, HarmSixMath.StoresNear)
                                                                     && !map.mapPawns.FreeColonistsSpawned.Any(x => x != o && x.Position.InHorDistOf(o.Position, HarmSixMath.AloneRange))
                                                                     && wolf.CanReach(o, PathEndMode.Touch, Danger.Deadly)).FirstOrDefault();
            if (victim == null) return;
            boldNight[map.uniqueID] = night;
            DebugBold = false;
            if (debug ? !Harm.May(victim, HurtClass.Injury) : !Harm.Roll(victim, HarmKinds.BoldWolf, HarmSixMath.BoldNightChance, HurtClass.Injury)) return;
            if (NightWalks.Hunt(wolf, victim, HarmKinds.BoldWolf)) HarmFour.Say(victim, "Rimfolk_Pool_BoldWolf");
        }

        /// <summary>At dusk the bold one comes to the stores' door, and whoever sees it says so.</summary>
        private static void AtTheDoor(Pawn wolf, IntVec3 stores, Map map)
        {
            if (wolf?.jobs == null) return;
            if (!CellFinder.TryFindRandomCellNear(stores, map, 6, c => c.Standable(map) && wolf.CanReach(c, PathEndMode.OnCell, Danger.Some), out var cell)) return;
            wolf.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Goto, cell), JobCondition.InterruptForced, null, false, true);
            var seer = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(o => Together.Able(o) && o.Position.InHorDistOf(cell, 16f) && GenSight.LineOfSight(o.Position, cell, map));
            // apart: a remark on seeing it come to the stores' door, to nobody in particular.
            if (seer != null) HarmFour.Say(seer, "Rimfolk_Pool_BoldWolfSeen", null, PokeWorldLink.IsPokemon(wolf) ? "pokemon" : null);
        }

        // ================= A catch that fights back (4.6.1) =================

        private static void Catch(ResidentBeast rb, Map map)
        {
            if (!rb.pokemon || rb.stance != "catch" || !Harm.SystemOn(HarmKinds.CatchFights.system)) return;
            var wild = rb.animal;
            int day = Day(map);
            if (catchDay.TryGetValue(wild.thingIDNumber, out int d) && d == day && !DebugCatch) return;
            var trainer = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(o => HarmFour.Ours(o) && Together.Able(o) && o.Position.InHorDistOf(wild.Position, HarmSixMath.CatchRange)
                                                                                && PlayOutTracker.Instance?.RunningFor(o)?.kind == PlayKind.Visit);
            if (trainer == null) return;
            catchDay[wild.thingIDNumber] = day;
            var partner = map.mapPawns.SpawnedColonyAnimals.FirstOrDefault(a => PokeWorldLink.IsPokemon(a) && a.playerSettings?.Master == trainer && !a.Downed
                                                                                 && a.Position.InHorDistOf(trainer.Position, HarmSixMath.PartnerRange));
            bool debug = DebugCatch;
            DebugCatch = false;
            if (partner != null)
            {
                // The partner takes it: PokéWorld's own fight between the two, nobody of ours hurt.
                if (!debug && !Rand.Chance(HarmSixMath.CatchFightChance)) return;
                if (Attack(wild, partner)) HarmFour.Say(trainer, "Rimfolk_Pool_CatchPartner", null, null);
                return;
            }
            if (debug ? !Harm.May(trainer, HurtClass.Injury) : !Harm.Roll(trainer, HarmKinds.CatchFights, HarmSixMath.CatchFight(false), HurtClass.Injury)) return;
            if (!Attack(wild, trainer)) return;
            Harm.Note(trainer, HarmKinds.CatchFights);
            HarmFour.Say(trainer, "Rimfolk_Pool_CatchFights");
        }

        /// <summary>The wild one's attack for a while, the game's own (its moves are PokéWorld's), not on the downed.</summary>
        private static bool Attack(Pawn wild, Pawn target)
        {
            try
            {
                var job = JobMaker.MakeJob(JobDefOf.AttackMelee, target);
                job.expiryInterval = HarmSixMath.CatchFightTicks;
                job.killIncappedTarget = false;
                wild.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                return wild.CurJobDef == JobDefOf.AttackMelee;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm six: the catch's fight", e);
                return false;
            }
        }

        /// <summary>The catch's answer: the wild one may fight back, a partner Pokémon at the trainer's side takes it.</summary>
        public static void DescribeCatch(ImpactSheet sheet)
        {
            if (sheet == null || !Harm.On || !Harm.SystemOn(HarmKinds.CatchFights.system)) return;
            sheet.Add(ImpactKind.Health, -1, "CC_Impact_CatchFights".Translate().Resolve(), "CC_Impact_CatchFightsBrief".Translate().Resolve(), "catchfights");
        }

        // ================= The visitor at the stores turns (4.6.2) =================

        private static void Visitor(ResidentBeast rb, Map map)
        {
            if (!rb.pokemon || rb.stance != "fed" || !Harm.SystemOn(HarmKinds.StoresVisitor.system)) return;
            bool debug = DebugSwipe;
            if (!debug && !HarmSixMath.Dusk(GenLocalDate.HourOfDay(map))) return;
            var pk = rb.animal;
            int day = Day(map);
            if (!debug && visitDay.TryGetValue(pk.thingIDNumber, out int d) && d == day) return;
            visitDay[pk.thingIDNumber] = day;
            if (!debug && Rand.ValueSeeded(pk.thingIDNumber * 131 + day * 17) >= HarmSixMath.StoresVisitChance) return;
            var stores = Stores(map);
            if (!stores.IsValid || map.dangerWatcher.DangerRating != StoryDanger.None) return;
            if (!CellFinder.TryFindRandomCellNear(stores, map, 5, c => c.Standable(map) && pk.CanReach(c, PathEndMode.OnCell, Danger.Some), out var cell)) return;
            pk.jobs?.StartJob(JobMaker.MakeJob(JobDefOf.Goto, cell), JobCondition.InterruptForced, null, false, true);
            var shooer = map.mapPawns.FreeColonistsSpawned.Where(o => HarmFour.Ours(o) && Together.Able(o) && Together.FreeTime(o) && o.Position.InHorDistOf(cell, 30f)
                                                                     && o.CanReach(cell, PathEndMode.Touch, Danger.Some))
                .OrderBy(o => o.Position.DistanceToSquared(cell)).FirstOrDefault();
            if (shooer == null) return;
            var job = JobMaker.MakeJob(HarmSixDefOf.Rimfolk_ShooVisitor, cell);
            job.count = 240;
            shooer.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            if (shooer.CurJobDef != HarmSixDefOf.Rimfolk_ShooVisitor) return;
            if (shooing.Count > 50) shooing.Clear();
            shooing[shooer.thingIDNumber] = pk;
        }

        /// <summary>At the stores to shoo it (JobDriver_HarmWalk's arrival): it runs, or one time in ten it swipes first.</summary>
        public static void Shooed(Pawn p)
        {
            if (!shooing.TryGetValue(p.thingIDNumber, out var pk)) return;
            shooing.Remove(p.thingIDNumber);
            if (pk == null || !pk.Spawned || pk.Dead || pk.Downed || pk.Map != p.Map || !pk.Position.InHorDistOf(p.Position, 6f)) return;
            var cls = HarmFour.Child(p) ? HurtClass.Mild : HurtClass.Injury;
            bool debug = DebugSwipe;
            DebugSwipe = false;
            if (debug ? Harm.May(p, cls) : Harm.Roll(p, HarmKinds.StoresVisitor, HarmSixMath.StoresSwipeChance, cls))
            {
                Charge(pk, p);
                var h = cls == HurtClass.Mild
                    ? Harm.Injure(p, HurtKind.Scratch, HurtWhere.Hand, HarmSixMath.SwipeChildLow, HarmSixMath.SwipeChildHigh, HarmKinds.StoresVisitor, cls, pk)
                    : Harm.Injure(p, HurtKind.Scratch, Rand.Bool ? HurtWhere.Hand : HurtWhere.Arm, HarmSixMath.SwipeLow, HarmSixMath.SwipeHigh, HarmKinds.StoresVisitor, cls, pk);
                if (h != null)
                {
                    HarmFour.Say(p, "Rimfolk_Pool_Swiped");
                    HarmFour.Mote(p, "CC_HarmSix_Mote_Swiped", pk.Named("ANIMAL"));
                    Flee(pk, p);
                    return;
                }
            }
            // apart: a word to the visitor, which isn't one of ours to answer.
            HarmFour.Say(p, "Rimfolk_Pool_Shoo");
            Flee(pk, p);
        }

        /// <summary>The beast's answer (DilemmaEffect_BeastStance): what feeding a predator or a Pokémon can bring.</summary>
        public static void DescribeStance(DilemmaContext ctx, ImpactSheet sheet, string stance)
        {
            if (sheet == null || stance != "fed" || !Harm.On || ctx?.map == null) return;
            var rb = CouncilGameComponent.Instance?.BeastOf(ctx.map);
            if (rb?.animal == null) return;
            if (rb.pokemon)
            {
                if (Harm.SystemOn(HarmKinds.StoresVisitor.system))
                    sheet.Add(ImpactKind.Health, -1, "CC_Impact_StoresVisitor".Translate().Resolve(), "CC_Impact_StoresVisitorBrief".Translate().Resolve(), "storesvisitor");
                return;
            }
            if (rb.Predator && Harm.SystemOn(HarmKinds.BoldWolf.system))
                sheet.Add(ImpactKind.Health, -1, "CC_Impact_BoldWolf".Translate().Resolve(), "CC_Impact_BoldWolfBrief".Translate().Resolve(), "boldwolf");
        }
    }

    /// <summary>An encounter in which one of ours steps close to a wild animal: by the game's own taming-revenge chance, it may lash out (Harm/WildHurts.cs).</summary>
    public class DilemmaEffect_TooClose : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
        }

        public override void Apply(DilemmaContext ctx)
        {
            try
            {
                WildHurts.TooClose(ctx?.a, ctx?.stranger);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm six: too close", e);
            }
        }
    }
}
