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
    /// PokéWorld: the colony's own Pokémon do a little at home by their type (the player's pick of 7 November 2026), read
    /// by name like the rest of the integration (types from <see cref="PokeWorldLink.TypesOf"/>). Only the colony's own,
    /// tamed and out of their balls on a home map, up and not in a mental state; each effect small, bounded and shown on
    /// the map with a glow, and now and then a colonist's one-sentence word (once a day at most for each on a map):
    /// <list type="bullet">
    /// <item>Fire: the room it rests in is warmed like by a little heater (6 heat a second times its body size, a quarter of
    /// a heater's), only while the room is under 21 °C and never past it; outdoors nothing. Ice: a room over 23 °C cooled
    /// the same way, never below it. Two of them a room at most.</item>
    /// <item>Grass: the crops within 2.9 cells of where it rests (a growing zone, a hydroponics basin or a pot) grow a
    /// quarter faster, by their own growth rate (nothing in the dark, the cold or at night).</item>
    /// <item>Electric: a battery the colony built that it rests beside gets 100 W, a small trickle.</item>
    /// <item>Psychic: a colonist within 6 cells in its sight, near a mental break, is calmed (+5 for half a day): one
    /// calming in two days for a colonist, one a day for each Psychic type.</item>
    /// <item>Fighting: spars with a colonist at their leisure (a joy job: melee practice, both lunging, nobody's weapon);
    /// one spar in 25 lands a bruise, scaled by "Hurts from everyday life" and under its cap.</item>
    /// <item>Water: a fire within 2.9 cells is doused a little each 250 ticks; an idle one goes over to a fire on the
    /// colony's ground within 10 cells.</item>
    /// </list>
    /// The colony's typed Pokémon are counted once an hour (a new one waits up to an hour); the effects run every 250
    /// ticks (Animals/AnimalPowers.cs). Nothing is saved here but the Psychic types' cooldowns, in the book. Settings: "Pokémon
    /// types at home" and its six parts (PokéWorld's section of the other mods' page).
    /// </summary>
    public static class PokeWorldTypes
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public static bool On => PokeWorldLink.Active && S != null && S.pokemonTypes;

        public static bool PartOn(PokemonPower p)
        {
            if (!On) return false;
            switch (p)
            {
                case PokemonPower.Warm:
                case PokemonPower.Cool: return S.pokemonTypeWarmth;
                case PokemonPower.Growth: return S.pokemonTypeGrowth;
                case PokemonPower.Battery: return S.pokemonTypeBattery;
                case PokemonPower.Calm: return S.pokemonTypeCalm;
                case PokemonPower.Spar: return S.pokemonTypeSpar;
                case PokemonPower.Douse: return S.pokemonTypeDouse;
                default: return false;
            }
        }

        private static JobDef sparJob, stayJob;

        public static JobDef SparJob => sparJob ??= DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PW_Spar");

        public static JobDef StayJob => stayJob ??= DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_StayWith");

        // ===================== The colony's typed Pokémon, counted once an hour =====================

        private sealed class Typed
        {
            public Pawn pawn;
            public int mask;
        }

        private static readonly Dictionary<Map, List<Typed>> typed = new Dictionary<Map, List<Typed>>();
        private static readonly Dictionary<ThingDef, int> masks = new Dictionary<ThingDef, int>();
        private static Game typedGame;

        /// <summary>A Pokémon's powers (by its species' types), or 0 for anything else.</summary>
        public static int MaskOf(Pawn p)
        {
            if (p?.def == null || !PokeWorldLink.IsPokemon(p)) return 0;
            if (!masks.TryGetValue(p.def, out int m))
            {
                m = AnimalPowersMath.Mask(PokeWorldLink.TypesOf(p.def));
                masks[p.def] = m;
            }
            return m;
        }

        public static void Hourly(int now)
        {
            if (typedGame != Current.Game)
            {
                typedGame = Current.Game;
                typed.Clear();
            }
            typed.Clear();
            if (!On) return;
            foreach (var map in Find.Maps)
            {
                if (map == null || !map.IsPlayerHome) continue;
                List<Typed> list = null;
                foreach (var a in map.mapPawns.SpawnedColonyAnimals)
                {
                    int m = MaskOf(a);
                    if (m == 0) continue;
                    (list ??= new List<Typed>()).Add(new Typed { pawn = a, mask = m });
                }
                if (list != null) typed[map] = list;
            }
        }

        /// <summary>The typed Pokémon of the colony on this map as last counted, with their powers.</summary>
        public static IEnumerable<(Pawn pawn, int mask)> Here(Map map)
        {
            if (map == null || !typed.TryGetValue(map, out var list)) yield break;
            foreach (var t in list) yield return (t.pawn, t.mask);
        }

        /// <summary>One of the colony's, out of its ball on this map, up, and itself.</summary>
        public static bool Fit(Pawn p, Map map) =>
            p != null && !p.Dead && p.Spawned && p.Map == map && p.Faction == Faction.OfPlayer && !p.Downed && !p.InMentalState;

        /// <summary>Resting: where it is, not walking somewhere.</summary>
        public static bool Resting(Pawn p) => !(p.pather?.Moving ?? false);

        // ===================== Every 250 ticks =====================

        private static readonly Dictionary<Room, int> warmedRooms = new Dictionary<Room, int>();
        private static readonly HashSet<Plant> grown = new HashSet<Plant>();

        public static void Rare(AnimalPowersBook book, int now)
        {
            if (!On) return;
            if (typedGame != Current.Game) Hourly(now);
            foreach (var kv in typed.ToList())
            {
                var map = kv.Key;
                if (map == null || !map.IsPlayerHome || !Find.Maps.Contains(map)) continue;
                warmedRooms.Clear();
                grown.Clear();
                foreach (var t in kv.Value)
                {
                    var p = t.pawn;
                    if (!Fit(p, map)) continue;
                    try
                    {
                        if (AnimalPowersMath.Has(t.mask, PokemonPower.Warm) && PartOn(PokemonPower.Warm)) Warmth(p, map, true, now);
                        if (AnimalPowersMath.Has(t.mask, PokemonPower.Cool) && PartOn(PokemonPower.Cool)) Warmth(p, map, false, now);
                        if (AnimalPowersMath.Has(t.mask, PokemonPower.Growth) && PartOn(PokemonPower.Growth)) Growth(p, map, now);
                        if (AnimalPowersMath.Has(t.mask, PokemonPower.Battery) && PartOn(PokemonPower.Battery)) Battery(p, map, now);
                        if (AnimalPowersMath.Has(t.mask, PokemonPower.Calm) && PartOn(PokemonPower.Calm) && book != null) Calm(book, p, map, now);
                        if (AnimalPowersMath.Has(t.mask, PokemonPower.Douse) && PartOn(PokemonPower.Douse)) Douse(p, map, now);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("PokéWorld: a Pokémon's type at home", e);
                    }
                }
            }
            warmedRooms.Clear();
            grown.Clear();
        }

        // ---------------- Fire and Ice ----------------

        private static readonly Color CoolColor = new Color(0.62f, 0.85f, 1f, 0.45f);

        private static void Warmth(Pawn p, Map map, bool warm, int now)
        {
            if (!Resting(p)) return;
            var room = p.GetRoom();
            if (room == null || room.UsesOutdoorTemperature || room.IsDoorway) return;
            warmedRooms.TryGetValue(room, out int n);
            if (n >= AnimalPowersMath.MaxPerRoom) return;
            float energy = AnimalPowersMath.HeatPush(room.Temperature, room.CellCount, p.BodySize, warm);
            if (energy == 0f) return;
            warmedRooms[room] = n + 1;
            GenTemperature.PushHeat(p.Position, map, energy);
            if (AnimalPowers.InView(map, p.Position))
            {
                if (warm) FleckMaker.ThrowHeatGlow(p.Position, map, 0.3f);
                else AnimalPowers.Glow(map, p.DrawPos, CoolColor, 1.3f);
            }
            Line(warm ? PokemonPower.Warm : PokemonPower.Cool, p, map, now, c => c.GetRoom() == room);
        }

        // ---------------- Grass ----------------

        private static readonly Color GrowColor = new Color(0.55f, 1f, 0.5f, 0.4f);

        /// <summary>A crop of the colony's: sown in a growing zone, a hydroponics basin or a plant pot.</summary>
        public static bool Crop(Plant plant, Map map)
        {
            if (plant?.def?.plant == null || !plant.def.plant.Sowable) return false;
            if (map.zoneManager.ZoneAt(plant.Position) is Zone_Growing) return true;
            return plant.Position.GetEdifice(map) is Building_PlantGrower;
        }

        private static void Growth(Pawn p, Map map, int now)
        {
            if (!Resting(p) || !AnimalPowersMath.PlantsAwake(GenLocalDate.DayPercent(map))) return;
            int pushed = 0;
            Plant shown = null;
            foreach (var c in GenRadial.RadialCellsAround(p.Position, AnimalPowersMath.GrowthRadius, true))
            {
                if (!c.InBounds(map)) continue;
                var plant = c.GetPlant(map);
                // One push a crop a pass, however many Grass types stand round it.
                if (plant == null || !grown.Add(plant) || !Crop(plant, map) || plant.LifeStage != PlantLifeStage.Growing) continue;
                float add = AnimalPowersMath.GrowthPush(plant.def.plant.growDays, plant.GrowthRate);
                if (add <= 0f) continue;
                plant.Growth += add;
                pushed++;
                if (shown == null || Rand.Chance(0.2f)) shown = plant;
            }
            if (pushed == 0) return;
            if (shown != null && AnimalPowers.InView(map, shown.Position))
            {
                AnimalPowers.Glow(map, shown.DrawPos, GrowColor, 1.1f);
                AnimalPowers.Glow(map, p.DrawPos, GrowColor, 1.2f);
            }
            Line(PokemonPower.Growth, p, map, now, c => c.Position.InHorDistOf(p.Position, 8f));
        }

        // ---------------- Electric ----------------

        private static readonly Color SparkColor = new Color(1f, 0.95f, 0.55f, 0.45f);

        /// <summary>A battery of the colony's (built by it, its own) beside the Pokémon, with room for more; null for none.</summary>
        public static CompPowerBattery BatteryBeside(Pawn p, Map map)
        {
            foreach (var c in GenAdj.CellsAdjacent8WayAndInside(p))
            {
                if (!c.InBounds(map)) continue;
                var things = c.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                {
                    if (!(things[i] is Building b) || b.Faction != Faction.OfPlayer) continue;
                    var bat = b.TryGetComp<CompPowerBattery>();
                    if (bat != null && bat.AmountCanAccept > 0.001f) return bat;
                }
            }
            return null;
        }

        private static void Battery(Pawn p, Map map, int now)
        {
            if (!Resting(p)) return;
            var bat = BatteryBeside(p, map);
            if (bat == null) return;
            bat.AddEnergy(AnimalPowersMath.BatteryPush(AnimalPowersMath.BatteryWatts));
            if (AnimalPowers.InView(map, p.Position))
            {
                FleckMaker.ThrowMicroSparks(bat.parent.DrawPos, map);
                AnimalPowers.Glow(map, p.DrawPos, SparkColor, 1f);
            }
            Line(PokemonPower.Battery, p, map, now, c => c.Position.InHorDistOf(p.Position, 8f));
        }

        // ---------------- Psychic ----------------

        private static readonly Color CalmColor = new Color(0.82f, 0.68f, 1f, 0.45f);
        private static ThoughtDef calmed;

        private static void Calm(AnimalPowersBook book, Pawn p, Map map, int now)
        {
            if (!p.Awake() || !AnimalPowersMath.Ready(book.CalmedAt(p), now, AnimalPowersMath.CalmPokemonCooldown)) return;
            Pawn best = null;
            float lowest = float.MaxValue;
            foreach (var c in map.mapPawns.FreeColonistsSpawned)
            {
                if (!c.Position.InHorDistOf(p.Position, AnimalPowersMath.CalmRadius) || c.InMentalState || c.Downed || !c.Awake() || !People.HasInnerLife(c)) continue;
                var mood = c.needs?.mood;
                var breaker = c.mindState?.mentalBreaker;
                if (mood == null || breaker == null || !AnimalPowersMath.NearBreak(mood.CurLevel, breaker.BreakThresholdMinor)) continue;
                if (!AnimalPowersMath.Ready(book.CalmedAt(c), now, AnimalPowersMath.CalmPersonCooldown)) continue;
                if (!GenSight.LineOfSight(p.Position, c.Position, map)) continue;
                if (mood.CurLevel < lowest)
                {
                    lowest = mood.CurLevel;
                    best = c;
                }
            }
            if (best == null) return;
            book.NoteCalm(best, p, now);
            calmed ??= DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_PW_Calmed");
            using (Causes.Push("CC_PW_Cause_Calmed".Translate(p.Named("PET")).Resolve()))
                CouncilGameComponent.GiveThought(best, calmed);
            if (AnimalPowers.InView(map, best.Position))
            {
                AnimalPowers.Glow(map, p.DrawPos, CalmColor, 1.4f, 0.6f);
                AnimalPowers.Glow(map, best.DrawPos + new Vector3(0f, 0f, 0.5f), CalmColor, 1f, 0.6f);
            }
            if (Speech.On && map == Find.CurrentMap)
            {
                string line = Interplay.Line("Rimfolk_Pool_PW_SayCalm", best, Tags(p), p.Named("PET"));
                // apart: said to the Pokémon, not to another colonist.
                if (!line.NullOrEmpty()) Speech.Say(best, line, null, SpeechColor);
            }
            News.Post("CC_PW_Calmed".Translate(best.Named("PAWN"), p.Named("PET")).Resolve(), new LookTargets(best, p), NewsKind.Land, MessageTypeDefOf.PositiveEvent, Importance.Flavor);
        }

        // ---------------- Water ----------------

        /// <summary>Doing nothing it would mind leaving: waiting or wandering.</summary>
        private static bool Idle(Pawn p)
        {
            var d = p.CurJobDef;
            return d == null || d == JobDefOf.Wait || d == JobDefOf.Wait_Wander || d == JobDefOf.GotoWander || d == JobDefOf.Wait_MaintainPosture;
        }

        private static void Douse(Pawn p, Map map, int now)
        {
            var fires = map.listerThings.ThingsOfDef(ThingDefOf.Fire);
            if (fires.Count == 0 || !p.Awake()) return;
            Fire near = null, far = null;
            float nearD = AnimalPowersMath.DouseRadius * AnimalPowersMath.DouseRadius, farD = AnimalPowersMath.DouseWalk * AnimalPowersMath.DouseWalk;
            for (int i = 0; i < fires.Count; i++)
            {
                if (!(fires[i] is Fire f) || f.Destroyed || !f.Spawned) continue;
                float d = (f.Position - p.Position).LengthHorizontalSquared;
                if (d <= nearD)
                {
                    near = f;
                    nearD = d;
                }
                else if (near == null && d <= farD && map.areaManager.Home[f.Position])
                {
                    far = f;
                    farD = d;
                }
            }
            if (near != null)
            {
                var at = near.Position;
                near.TakeDamage(new DamageInfo(DamageDefOf.Extinguish, AnimalPowersMath.DouseDamage));
                p.rotationTracker?.FaceCell(at);
                if (AnimalPowers.InView(map, at)) FleckMaker.WaterSplash(at.ToVector3Shifted(), map, 1.4f, 3f);
                Line(PokemonPower.Douse, p, map, now, c => c.Position.InHorDistOf(at, 12f));
                return;
            }
            if (far == null || !Idle(p)) return;
            if (!CellFinder.TryFindRandomCellNear(far.Position, map, 2, c => c != far.Position && c.Standable(map) && !c.ContainsStaticFire(map)
                                                                       && c.InHorDistOf(far.Position, 2.5f) && p.CanReach(c, PathEndMode.OnCell, Danger.Some), out var cell))
                return;
            var job = JobMaker.MakeJob(JobDefOf.Goto, cell);
            job.locomotionUrgency = LocomotionUrgency.Jog;
            job.expiryInterval = 1200;
            p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
        }

        // ---------------- Fighting: the spar ----------------

        /// <summary>A Fighting type of the colony's to spar with: the colonist's own first, else the nearest, up and free.</summary>
        public static Pawn SparPartner(Pawn pawn)
        {
            var map = pawn?.Map;
            if (map == null) return null;
            if (typedGame != Current.Game) Hourly(Find.TickManager.TicksGame);
            Pawn best = null;
            float bestScore = float.MaxValue;
            foreach (var (mon, mask) in Here(map))
            {
                if (!AnimalPowersMath.Has(mask, PokemonPower.Spar) || !Fit(mon, map) || !mon.Awake() || !Idle(mon)) continue;
                if (!mon.Position.InHorDistOf(pawn.Position, AnimalPowersMath.SparReach) || mon.IsForbidden(pawn)) continue;
                if (!pawn.CanReserveAndReach(mon, PathEndMode.Touch, Danger.None)) continue;
                float score = mon.Position.DistanceToSquared(pawn.Position);
                if (PetTracker.PersonOf(mon) == pawn) score -= 100000f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = mon;
                }
            }
            return best;
        }

        /// <summary>The colonist has come up to the Pokémon: it squares up to them, and they say a word to it.</summary>
        public static void SparBegins(Pawn pawn, Pawn mon, int ticks)
        {
            if (pawn == null || mon == null || StayJob == null) return;
            try
            {
                var stay = JobMaker.MakeJob(StayJob, pawn);
                stay.count = ticks + 200;
                mon.jobs.StartJob(stay, JobCondition.InterruptForced, null, false, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("PokéWorld: a spar's partner", e);
            }
            if (Speech.On && pawn.Map == Find.CurrentMap && Rand.Chance(0.5f))
            {
                string line = Interplay.Line("Rimfolk_Pool_PW_SaySparBegin", pawn, Tags(mon), mon.Named("PET"));
                // apart: said to the Pokémon, not to another colonist.
                if (!line.NullOrEmpty()) Speech.Say(pawn, line, null, SpeechColor);
            }
        }

        /// <summary>A practice swing each way in turn: the game's lunge, melee practice for the colonist's own, and dust kicked up.</summary>
        public static void SparSwing(Pawn pawn, Pawn mon, int swing)
        {
            if (pawn?.Map == null || mon == null) return;
            if (swing % 2 == 1)
            {
                pawn.Drawer?.Notify_MeleeAttackOn(mon);
                pawn.skills?.Learn(SkillDefOf.Melee, AnimalPowersMath.SparXpSwing);
            }
            else mon.Drawer?.Notify_MeleeAttackOn(pawn);
            if (swing % 3 == 0 && AnimalPowers.InView(pawn.Map, mon.Position)) FleckMaker.ThrowDustPuff(mon.Position, pawn.Map, 0.5f);
        }

        /// <summary>
        /// A spar played out: now and then the Pokémon's blow lands (a bruise, scaled by "Hurts from everyday life" and under
        /// its cap), said by the one it hit and told as harm always is.
        /// </summary>
        public static void SparDone(Pawn pawn, Pawn mon)
        {
            if (pawn == null || mon == null || !pawn.Spawned || pawn.Dead) return;
            if (!Harm.Roll(pawn, HarmKinds.PokemonSpar, AnimalPowersMath.SparBlowChance, HurtClass.Bout)) return;
            var wound = Harm.Injure(pawn, HurtKind.Bruise, HurtWhere.Any, AnimalPowersMath.SparLow, AnimalPowersMath.SparHigh, HarmKinds.PokemonSpar, HurtClass.Bout);
            if (wound == null) return;
            mon.Drawer?.Notify_MeleeAttackOn(pawn);
            if (Speech.On && pawn.Map == Find.CurrentMap)
            {
                string line = Interplay.Line("Rimfolk_Pool_PW_SaySparBlow", pawn, Tags(mon), mon.Named("PET"));
                // apart: said to the Pokémon, not to another colonist.
                if (!line.NullOrEmpty()) Speech.Say(pawn, line, null, SpeechColor);
            }
            Tell.Message(NewsKind.People, Importance.Important, pawn.Map,
                "CC_PW_SparBlow".Translate(pawn.Named("PAWN"), mon.Named("PET"), (wound.Part?.Label ?? "").Named("PART")).Resolve(), new LookTargets(pawn),
                MessageTypeDefOf.NegativeHealthEvent);
        }

        // ---------------- Words ----------------

        private static readonly Color SpeechColor = new Color(0.95f, 0.92f, 0.75f);
        private static readonly Dictionary<(Map, PokemonPower), int> lastLine = new Dictionary<(Map, PokemonPower), int>();

        private static string Pool(PokemonPower power) => "Rimfolk_Pool_PW_Say" + power;

        public static string[] Tags(Pawn mon)
        {
            var t = new List<string> { "pokemon" };
            if (PokeWorldLife.Shiny(mon)) t.Add("shiny");
            if (mon?.Name != null && !mon.Name.Numerical) t.Add("named");
            return t.ToArray();
        }

        /// <summary>Now and then (once a day at most for each power on a map), a colonist who sees it says a word about it.</summary>
        private static void Line(PokemonPower power, Pawn mon, Map map, int now, Func<Pawn, bool> where)
        {
            if (!Speech.On || map != Find.CurrentMap || !Rand.Chance(AnimalPowersMath.LineChance)) return;
            if (lastLine.TryGetValue((map, power), out int last) && last <= now && now - last < AnimalPowersMath.LineGap) return;
            Pawn who = null;
            float best = float.MaxValue;
            foreach (var c in map.mapPawns.FreeColonistsSpawned)
            {
                if (!Together.Able(c) || !People.HasInnerLife(c) || c.DevelopmentalStage.Baby() || !where(c) || !AnimalPowers.InView(map, c.Position)) continue;
                float d = c.Position.DistanceToSquared(mon.Position);
                if (d < best)
                {
                    best = d;
                    who = c;
                }
            }
            if (who == null) return;
            string line = Interplay.Line(Pool(power), who, Tags(mon), mon.Named("PET"));
            if (line.NullOrEmpty()) return;
            lastLine[(map, power)] = now;
            // apart: said about the Pokémon, to whoever is about.
            Speech.Say(who, line, null, SpeechColor);
        }
    }

    // ===================== The spar's jobs =====================

    /// <summary>
    /// Fighting types: a colonist at their leisure who can fight (not a child, not unable to do violence, not hurting or
    /// ailing, no danger on the map) seeks out one of the colony's Fighting types to spar with.
    /// </summary>
    public class JoyGiver_PokemonSpar : JoyGiver
    {
        public override float GetChance(Pawn pawn) => PokeWorldTypes.PartOn(PokemonPower.Spar) ? base.GetChance(pawn) : 0f;

        public override Job TryGiveJob(Pawn pawn)
        {
            if (!PokeWorldTypes.PartOn(PokemonPower.Spar) || PokeWorldTypes.SparJob == null || pawn?.Map == null || !pawn.Map.IsPlayerHome) return null;
            if (!pawn.IsColonist || pawn.DevelopmentalStage.Juvenile() || pawn.WorkTagIsDisabled(WorkTags.Violent)) return null;
            var melee = pawn.skills?.GetSkill(SkillDefOf.Melee);
            if (melee == null || melee.TotallyDisabled) return null;
            if (pawn.health.hediffSet.PainTotal > 0.1f || HealthAIUtility.ShouldSeekMedicalRest(pawn) || pawn.Map.dangerWatcher.DangerRating != StoryDanger.None) return null;
            var mon = PokeWorldTypes.SparPartner(pawn);
            if (mon == null) return null;
            var job = JobMaker.MakeJob(PokeWorldTypes.SparJob, mon);
            job.count = Rand.RangeInclusive(AnimalPowersMath.SparMinTicks, AnimalPowersMath.SparMaxTicks);
            return job;
        }
    }

    /// <summary>The colonist's side of a spar with a Fighting type: up to it, then practice swings each way in turn.</summary>
    public class JobDriver_PokemonSpar : JobDriver
    {
        private int swings;
        private int sinceSwing;

        private Pawn Mon => job.targetA.Thing as Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref swings, "swings");
            Scribe_Values.Look(ref sinceSwing, "sinceSwing");
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => Mon == null || Mon.Dead || Mon.Downed || Mon.InMentalState || Mon.Faction != Faction.OfPlayer || !PokeWorldTypes.PartOn(PokemonPower.Spar)
                              || pawn.Map == null || pawn.Map.dangerWatcher.DangerRating != StoryDanger.None);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            var spar = ToilMaker.MakeToil("PokemonSpar");
            spar.initAction = () => PokeWorldTypes.SparBegins(pawn, Mon, job.count);
            spar.tickIntervalAction = delta =>
            {
                var mon = Mon;
                if (mon == null || !pawn.Position.InHorDistOf(mon.Position, 2.5f))
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                pawn.rotationTracker.FaceTarget(mon);
                sinceSwing += delta;
                if (sinceSwing >= AnimalPowersMath.SparSwingTicks)
                {
                    sinceSwing = 0;
                    swings++;
                    PokeWorldTypes.SparSwing(pawn, mon, swings);
                }
                if (pawn.needs?.joy != null) JoyUtility.JoyTickCheckEnd(pawn, delta, JoyTickFullJoyAction.None);
            };
            spar.defaultCompleteMode = ToilCompleteMode.Delay;
            spar.defaultDuration = Mathf.Max(600, job.count);
            spar.handlingFacing = true;
            spar.socialMode = RandomSocialMode.Off;
            yield return spar;
            var done = ToilMaker.MakeToil("PokemonSparDone");
            done.initAction = () => PokeWorldTypes.SparDone(pawn, Mon);
            done.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return done;
        }
    }

    /// <summary>
    /// An animal stays with someone who came to it (a spar, a familiar's binding): it faces them, and the job ends as soon as
    /// their own job is about something else.
    /// </summary>
    public class JobDriver_StayWith : JobDriver
    {
        private Pawn Person => job.targetA.Thing as Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            var stay = ToilMaker.MakeToil("StayWith");
            stay.tickIntervalAction = delta =>
            {
                var p = Person;
                if (p == null || p.Dead || p.CurJob == null || p.CurJob.targetA.Thing != pawn)
                {
                    EndJobWith(JobCondition.Succeeded);
                    return;
                }
                pawn.rotationTracker.FaceTarget(p);
            };
            stay.defaultCompleteMode = ToilCompleteMode.Delay;
            stay.defaultDuration = Mathf.Max(300, job.count);
            stay.handlingFacing = true;
            yield return stay;
        }
    }
}
