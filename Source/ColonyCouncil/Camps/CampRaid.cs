using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    [DefOf]
    public static class CampCrowdDefOf
    {
        /// <summary>The question when a band is seen coming against a camp: a camp's letter in a threat's colour.</summary>
        public static LetterDef Rimfolk_CampLetterDanger;

        static CampCrowdDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(CampCrowdDefOf));
        }
    }

    /// <summary>
    /// A band of a people at war with a camp's people, come against that camp (the player's request, 8 November 2026:
    /// "perhaps camps can also get attacked by enemy factions"): who they are, where they gathered, what the colony
    /// answered, whether word reached the camp, who of the colony fought beside the campers, and how it ended. One per
    /// camp at a time, kept by the home (CampHome.raids) while the band is on the map.
    /// </summary>
    public class CampRaid : IExposable
    {
        public int campId;
        public Faction people, campPeople;
        public Map map;
        public Lord lord;
        public List<Pawn> pawns = new List<Pawn>();
        /// <summary>Where they came in and gathered, and the camp's fire they came for.</summary>
        public IntVec3 entry = IntVec3.Invalid, at = IntVec3.Invalid;
        public int startTick, moveTick = -1, endTick = -1;
        public int came, campStood, down;
        public CampRaidAnswer answer;
        /// <summary>Word reached the camp (the runner got there, or the radio); they chose to stand, or went in time.</summary>
        public bool warned, stood, fledInTime;
        /// <summary>At war with the colony too; come to claim the camp's old site (it held it before); arrived for the same site the same day.</summary>
        public bool hostile, claim, together;
        /// <summary>The colonist carrying the word to the camp, while on the way.</summary>
        public Pawn runner;
        /// <summary>The colony's people who fought beside the campers (drafted at the camp while the band was on it).</summary>
        public List<Pawn> helpers = new List<Pawn>();
        /// <summary>How it ended (CampRaidEnd), null while it goes on.</summary>
        public string end;
        /// <summary>The old site the camp held (CampGround.cs), -1 for none; the band held it after (the history is told once it leaves).</summary>
        public int groundId = -1;
        public bool heldTold;

        private static int Now => Find.TickManager.TicksGame;

        public List<Pawn> Here => pawns.Where(p => p != null && !p.Dead && p.Spawned && p.Map == map).ToList();

        public bool LordAlive => lord != null && map != null && Find.Maps.Contains(map) && map.lordManager.lords.Contains(lord);

        /// <summary>They have set off from the edge against the camp.</summary>
        public bool Moving => moveTick >= 0;

        /// <summary>Ended and off the map (or a day past its end): nothing left to watch.</summary>
        public bool Over => end != null && (!LordAlive || Here.Count == 0 || Now - endTick > GenDate.TicksPerDay);

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                pawns.RemoveAll(p => p == null || p.Discarded);
                helpers.RemoveAll(p => p == null || p.Discarded);
                if (runner != null && runner.Discarded) runner = null;
                if (lord != null && !LordAlive) lord = null;
                if (map != null && !Find.Maps.Contains(map)) map = null;
            }
            Scribe_Values.Look(ref campId, "campId");
            Scribe_References.Look(ref people, "people");
            Scribe_References.Look(ref campPeople, "campPeople");
            Scribe_References.Look(ref map, "map");
            Scribe_References.Look(ref lord, "lord");
            Scribe_Collections.Look(ref pawns, "pawns", LookMode.Reference);
            Scribe_Values.Look(ref entry, "entry", IntVec3.Invalid);
            Scribe_Values.Look(ref at, "at", IntVec3.Invalid);
            Scribe_Values.Look(ref startTick, "startTick");
            Scribe_Values.Look(ref moveTick, "moveTick", -1);
            Scribe_Values.Look(ref endTick, "endTick", -1);
            Scribe_Values.Look(ref came, "came");
            Scribe_Values.Look(ref campStood, "campStood");
            Scribe_Values.Look(ref down, "down");
            Scribe_Values.Look(ref answer, "answer");
            Scribe_Values.Look(ref warned, "warned");
            Scribe_Values.Look(ref stood, "stood");
            Scribe_Values.Look(ref fledInTime, "fledInTime");
            Scribe_Values.Look(ref hostile, "hostile");
            Scribe_Values.Look(ref claim, "claim");
            Scribe_Values.Look(ref together, "together");
            Scribe_References.Look(ref runner, "runner");
            Scribe_Collections.Look(ref helpers, "helpers", LookMode.Reference);
            Scribe_Values.Look(ref end, "end");
            Scribe_Values.Look(ref groundId, "groundId", -1);
            Scribe_Values.Look(ref heldTold, "heldTold");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                pawns ??= new List<Pawn>();
                helpers ??= new List<Pawn>();
                pawns.RemoveAll(p => p == null);
                helpers.RemoveAll(p => p == null);
            }
        }
    }

    // The camp's side of a band come against it: when one is fated to come, a second party for its old site, standing
    // guard by the fire, at ease again, and the aftermath of a fight it won.
    public partial class Camp
    {
        /// <summary>The old camp's site they hold (CampGround.cs), -1 for a fresh spot.</summary>
        public int groundId = -1;
        /// <summary>When a band comes against them (CampRaidMath.AttackTick), -1 for none; rolled once as they settle; how many hours it has waited on a passing reason.</summary>
        public int raidAt = -1, raidWaits;
        public bool raidRolled;
        /// <summary>They stand guard by their fire against a band (LordToil_CampGuard).</summary>
        public bool guarding;
        /// <summary>The people whose band drove them off, for their old site's history.</summary>
        public string drivenBy;
        /// <summary>A second party for their old site, and when it comes (CampGrounds.Moving): it gives way, or comes to blows.</summary>
        public Faction rivalPeople;
        public int rivalAt = -1;
        /// <summary>They found the old site they came for taken and camped elsewhere: they say so as they arrive.</summary>
        public string gaveWayAt;

        private void ExposeCrowd()
        {
            Scribe_Values.Look(ref groundId, "groundId", -1);
            Scribe_Values.Look(ref raidAt, "raidAt", -1);
            Scribe_Values.Look(ref raidWaits, "raidWaits");
            Scribe_Values.Look(ref raidRolled, "raidRolled");
            Scribe_Values.Look(ref guarding, "guarding");
            Scribe_Values.Look(ref drivenBy, "drivenBy");
            Scribe_References.Look(ref rivalPeople, "rivalPeople");
            Scribe_Values.Look(ref rivalAt, "rivalAt", -1);
            Scribe_Values.Look(ref gaveWayAt, "gaveWayAt");
        }

        /// <summary>Each hour (CampLife.Hourly): a second party for their old site, and the band fated to come against them.</summary>
        private void CrowdHour()
        {
            if (rivalPeople != null && rivalAt >= 0 && Now >= rivalAt && Settled) RivalHour();
            if (phase != CampPhase.Living || holding) return;
            if (!raidRolled)
            {
                raidRolled = true;
                RollRaid();
            }
            if (raidAt >= 0 && Now >= raidAt) CampRaids.TryCome(this);
        }

        /// <summary>Once, as they settle: whether a band will come against them in their stay, and when (CampRaidMath).</summary>
        private void RollRaid()
        {
            var g = CampGrounds.ById(groundId);
            bool contested = g != null && (CampGroundMath.Contested(CampGrounds.Holds(g)) || CampRaids.Attackers(this, g).Any(x => x.facts.heldSiteBefore));
            if (!Rand.Chance(CampRaidMath.FateChance(CampCrowds.Level, stayTicks / (float)GenDate.TicksPerDay, contested))) return;
            raidAt = CampRaidMath.AttackTick(arrivedTick, untilTick, Rand.Value);
        }

        /// <summary>The second party for their old site is here: at war with them, it comes to blows; else it camps elsewhere.</summary>
        private void RivalHour()
        {
            var people = rivalPeople;
            rivalPeople = null;
            rivalAt = -1;
            if (Hostile || people == null || people.defeated || faction == null) return;
            if (people.HostileTo(faction)) CampRaids.Send(this, people, true);
            else CampRaids.GiveWay(this, people);
        }

        /// <summary>They take up their arms by their fire against the band (LordToil_CampGuard), their packing let go.</summary>
        public void Guard()
        {
            if (guarding || !LordAlive || !Settled) return;
            guarding = true;
            packingFor = null;
            lord.ReceiveMemo(LordJob_Camp.MemoGuard);
        }

        /// <summary>The band is gone: back to their day.</summary>
        public void AtEase()
        {
            if (!guarding) return;
            guarding = false;
            if (LordAlive) lord.ReceiveMemo(LordJob_Camp.MemoAtEase);
        }

        /// <summary>
        /// After a fight they won: their healer (whoever can) tends their hurt, the game's own tending, without medicine; the
        /// others take up the weapons the band dropped by their camp (beside one of the band, fallen or down, and forbidden to
        /// the colony as the game leaves what an outsider drops: never a colonist's), into their packs.
        /// </summary>
        public void Aftermath(CampRaid band)
        {
            if (map == null || !Settled) return;
            var up = Here.Where(p => !p.Downed && !p.DevelopmentalStage.Juvenile() && !p.InMentalState).ToList();
            var hurt = Here.Where(p => p.health?.hediffSet != null && p.health.hediffSet.HasTendableHediff()).ToList();
            foreach (var patient in hurt.Take(4))
            {
                var healer = up.Where(p => p != patient && !p.WorkTagIsDisabled(WorkTags.Caring) && p.CanReserveAndReach(patient, PathEndMode.Touch, Danger.Some))
                    .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0).FirstOrDefault();
                if (healer == null) continue;
                try
                {
                    healer.jobs.StartJob(JobMaker.MakeJob(JobDefOf.TendPatient, patient), JobCondition.InterruptForced);
                    up.Remove(healer);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("a camp tends its own", e);
                }
            }
            var fallen = (band?.pawns ?? new List<Pawn>()).Select(p => p == null ? IntVec3.Invalid : p.Dead ? p.Corpse?.PositionHeld ?? IntVec3.Invalid : p.Spawned ? p.Position : IntVec3.Invalid)
                .Where(x => x.IsValid).ToList();
            var weapons = map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon)
                .Where(t => t.Spawned && t.def.IsWeapon && !Owns(t) && t.IsForbidden(Faction.OfPlayer) && t.Position.InHorDistOf(FireCell, CampSite.CoreRadius + 12)
                            && ClearOfColony(t.Position) && fallen.Any(x => x.InHorDistOf(t.Position, 3f))).ToList();
            foreach (var w in weapons)
            {
                var looter = up.Where(p => !p.WorkTagIsDisabled(WorkTags.Hauling) && p.CanReserveAndReach(w, PathEndMode.Touch, Danger.Some))
                    .OrderBy(p => p.Position.DistanceToSquared(w.Position)).FirstOrDefault();
                if (looter == null) break;
                try
                {
                    var job = JobMaker.MakeJob(JobDefOf.TakeInventory, w);
                    job.count = 1;
                    looter.jobs.StartJob(job, JobCondition.InterruptForced);
                    up.Remove(looter);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("a camp takes up the band's weapons", e);
                }
            }
        }

        /// <summary>Their thanks carried over to the colony's gate by one of them, set down there (CampCarry.cs). False with nobody to send.</summary>
        public bool BringThanks(ThingDef def, int count)
        {
            if (def == null || count <= 0 || map == null || !Settled) return false;
            var porter = Free.Where(p => !p.WorkTagIsDisabled(WorkTags.Hauling)).OrderBy(p => p == trader ? 1 : 0).FirstOrDefault();
            if (porter?.inventory == null) return false;
            var gate = ErrandCell("gate", null, porter);
            if (!gate.IsValid) return false;
            var gift = ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
            gift.stackCount = Mathf.Clamp(count, 1, def.stackLimit);
            porter.inventory.TryAddItemNotForSale(gift);
            carries.Add(new CampCarry { by = porter, help = Thanks, def = def, count = gift.stackCount, to = gate, from = Now, until = Now + GenDate.TicksPerHour * 12, carrying = true });
            return true;
        }

        /// <summary>The help word of a carry of thanks for fighting beside them (CampCarry.Brought).</summary>
        public const string Thanks = "thanks";

        /// <summary>The World page's line for the old camp's site they hold, or null.</summary>
        public string GroundPanelLine()
        {
            var g = groundId >= 0 ? CampGrounds.ById(groundId) : null;
            return g == null ? null : CampGrounds.Says(this, "CC_CampGround_CardLine", g.name);
        }

        /// <summary>The World page's line for a band come against them, or null.</summary>
        public string BandPanelLine()
        {
            var b = CampRaids.Against(this);
            if (b == null) return null;
            string key = b.Moving ? "CC_CampRaid_CardMoving" : "CC_CampRaid_CardGathering";
            string line = CampRaids.Words(this, b, key.Translate().RawText);
            if (guarding) line += " " + CampRaids.Words(this, b, "CC_CampRaid_CardGuard".Translate().RawText);
            return line;
        }
    }

    /// <summary>
    /// Bands come against camps (CampRaidMath): one in some nine camps at the usual level is fated, as it settles, to have a
    /// band of a people at war with its people come against it in its stay, never on its first day. The band walks in at
    /// the map's edge nearest the camp, not the colony's, and gathers there a few hours; the colony is told as it is seen
    /// (a camp's question in the words of the age) and answers: send our fighters to stand with them (word goes that help
    /// comes, and the player drafts whoever they send), send word only (they stand or go in time), or stay out. Then the
    /// game's own fight: the band goes for the camp; the campers stand by their fire or run; one at war with the colony
    /// too is the colony's enemy on the map, by the game's own rules. Campers who win take up the band's weapons and tend
    /// their own; campers who lose flee with what they carry (their dead lie where they fell, their camp stands as it was
    /// left). Their people remember who stood with them, and send thanks.
    /// </summary>
    public static class CampRaids
    {
        private static int Now => Find.TickManager.TicksGame;

        /// <summary>Hours the band gathers at the edge before it moves on the camp.</summary>
        public static readonly IntRange StageHours = new IntRange(2, 3);

        /// <summary>A drafted colonist this near the camp's fire while the band is on it fights beside them.</summary>
        public const float HelpRadius = 30f;

        /// <summary>The band this near the fire: the campers know it is on them, warned or not.</summary>
        public const float SeenRadius = 22f;

        /// <summary>The band against this camp now, or null.</summary>
        public static CampRaid Against(Camp c)
        {
            if (c?.map == null) return null;
            var h = CampTracker.Instance?.HomeWith(c);
            return h?.raids?.FirstOrDefault(r => r != null && r.campId == c.id && r.end == null);
        }

        /// <summary>Every band on this map still going.</summary>
        public static IEnumerable<CampRaid> On(Map map)
        {
            var t = CampTracker.Instance;
            if (t == null || map == null || !Homes.IsHome(map)) return Enumerable.Empty<CampRaid>();
            return t.HomeOf(map).raids.Where(r => r != null && r.map == map && r.end == null);
        }

        /// <summary>Every band on this map, going or ended and not yet off it.</summary>
        private static IEnumerable<CampRaid> Still(Map map)
        {
            var t = CampTracker.Instance;
            if (t == null || map == null || !Homes.IsHome(map)) return Enumerable.Empty<CampRaid>();
            return t.HomeOf(map).raids.Where(r => r != null && r.map == map && !r.Over);
        }

        /// <summary>The band against this camp, going or ended and not yet off the map, or null.</summary>
        public static CampRaid Of(Camp c) => c?.map == null ? null : Still(c.map).FirstOrDefault(r => r.campId == c.id);

        /// <summary>Every danger to the colony on the map is a band come against a camp (going, or beaten and leaving): the camps keep their wits about them.</summary>
        public static bool OnlyBandsOn(Map map)
        {
            var bands = Still(map).Where(r => r.hostile).ToList();
            if (bands.Count == 0) return false;
            var theirs = new HashSet<Pawn>(bands.SelectMany(r => r.pawns));
            // A beast or an insect about is no danger to count here (CampHarmMath): the camps fight those off where they are.
            foreach (var t in map.attackTargetsCache.TargetsHostileToColony)
                if (t.Thing is Pawn p && !p.Dead && !p.Downed && !theirs.Contains(p) && GenHostility.IsActiveThreatToPlayer(t) && CampHarmMath.RealAttack(CampHarm.ByOf(p, null))) return false;
            return true;
        }

        // ---------------- Who may come ----------------

        /// <summary>The peoples that may send a band against this camp now (CampRaidMath.MayAttack), with what the rules know of each.</summary>
        public static List<(Faction f, AttackerFacts facts)> Attackers(Camp c, CampGround g = null, bool force = false)
        {
            var list = new List<(Faction, AttackerFacts)>();
            if (c?.faction == null || c.map == null) return list;
            var tech = Avail.EffectiveTech;
            bool fights = force || IncidentWorker_Happening.ScenarioFightsPossible && Now - c.map.storyState.LastThreatBigTick > GenDate.TicksPerDay * 2;
            foreach (var f in Find.FactionManager.AllFactionsListForReading)
            {
                if (f == null || f.def == null) continue;
                var facts = new AttackerFacts
                {
                    hostileToCamp = f != c.faction && f.HostileTo(c.faction),
                    isPlayer = f.IsPlayer,
                    allyOfColony = Allies.IsAlly(f),
                    humanlike = f.def.humanlikeFaction,
                    hasFighters = f.def.pawnGroupMakers != null && f.def.pawnGroupMakers.Any(m => m.kindDef == PawnGroupKindDefOf.Combat),
                    gone = f.defeated || f.Hidden || f.temporary,
                    hostileToColony = !f.IsPlayer && f.HostileTo(Faction.OfPlayer),
                    fightsAllowed = fights,
                    truce = Truces.Bars(f, c.map),
                    heldSiteBefore = g != null && g.held.Any(h => h.faction == f),
                    techWeight = f.def.humanlikeFaction ? Arrivals.TechWeight(f, tech) : 0f
                };
                if (CampRaidMath.MayAttack(facts)) list.Add((f, facts));
            }
            return list;
        }

        /// <summary>The fated hour: the band comes when nothing puts it off (danger on the map, the camp packing or gone), for up to half a day.</summary>
        public static void TryCome(Camp c)
        {
            bool wait = !CampTracker.Enabled || c.packingFor != null || !c.Settled || c.Hostile || Against(c) != null || MapFacts.ThreatOn(c.map) || CampWeather.Hazard(c.map) != null;
            if (!wait && Send(c, null) != null)
            {
                c.raidAt = -1;
                return;
            }
            if (++c.raidWaits > CampRaidMath.PutOffHours) c.raidAt = -1;
        }

        // ---------------- The band comes ----------------

        /// <summary>How strong a group is in a fight: its grown, armed people on their feet, by their kinds' own combat power.</summary>
        public static float Strength(IEnumerable<Pawn> pawns) =>
            pawns.Where(p => p != null && !p.Dead && !p.Downed && !p.DevelopmentalStage.Juvenile() && p.equipment?.Primary != null && !p.WorkTagIsDisabled(WorkTags.Violent))
                .Sum(p => Mathf.Max(20f, p.kindDef?.combatPower ?? 40f));

        /// <summary>
        /// A band of <paramref name="people"/> (else one of those that may, by CampRaidMath.Weight) against the camp: its
        /// fighters made the game's own way for their people (their kinds, their age's gear: Rimfolk's peoples' kits come
        /// with their kinds), about as strong as the camp's armed (CampRaidMath.Points); in at the edge nearest the camp,
        /// gathering there; the colony asked what to do. <paramref name="together"/>: they came for the camp's old site the
        /// same day it did. Null when none can come.
        /// </summary>
        public static CampRaid Send(Camp c, Faction people, bool together = false, bool force = false)
        {
            var map = c?.map;
            var tracker = CampTracker.Instance;
            if (map == null || tracker == null || c.faction == null || Against(c) != null) return null;
            var g = CampGrounds.ById(c.groundId);
            var may = Attackers(c, g, force);
            if (people == null)
            {
                if (may.Count == 0) return null;
                people = may.RandomElementByWeight(x => CampRaidMath.Weight(x.facts)).f;
            }
            else if (!may.Any(x => x.f == people) && !(force && people.HostileTo(c.faction))) return null;
            bool hostile = people.HostileTo(Faction.OfPlayer);
            float points = CampRaidMath.Points(Strength(c.Here), StorytellerUtility.DefaultThreatPointsNow(map), hostile, Rand.Value);
            var made = MakeBand(people, map, points);
            if (made.Count == 0) return null;
            if (!Edge(map, c.FireCell, out var entry))
            {
                foreach (var p in made) Arrivals.Discard(p);
                return null;
            }
            foreach (var p in made) GenSpawn.Spawn(p, CellFinder.RandomClosewalkCellNear(entry, map, 4), map);
            var band = new CampRaid
            {
                campId = c.id, people = people, campPeople = c.faction, map = map, pawns = made.ToList(), entry = entry, at = c.FireCell, startTick = Now, came = made.Count,
                hostile = hostile, claim = g != null && g.held.Any(h => h.faction == people), together = together, groundId = c.groundId
            };
            int stage = StageHours.RandomInRange * GenDate.TicksPerHour;
            try
            {
                band.lord = LordMaker.MakeNewLord(people, new LordJob_CampRaid(c.FireCell, entry, stage, band.claim || together ? GenDate.TicksPerDay / 2 : GenDate.TicksPerHour), map, made);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a band against a camp: its group", e);
                Arrivals.SendAway(made);
                return null;
            }
            tracker.HomeWith(c).raids.Add(band);
            // At war with the colony too: the storyteller knows a fight is on the map, so it doesn't send its own on top of it.
            if (hostile)
            {
                try
                {
                    var parms = new IncidentParms { target = map, faction = people, points = points };
                    map.storyState.Notify_IncidentFired(new FiringIncident(IncidentDefOf.RaidEnemy, null, parms));
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("a band against a camp: the storyteller told", e);
                }
            }
            Ask(c, band);
            // apart: one of the band calls out to their own as they gather at the edge, to nobody of the colony or the camp.
            var shout = band.Here.FirstOrDefault();
            if (shout != null) Speech.Say(shout, Words(c, band, Line(band)));
            return band;
        }

        /// <summary>Their fighters, the game's own combat group of their people at these points; eight at most.</summary>
        private static List<Pawn> MakeBand(Faction f, Map map, float points)
        {
            var list = new List<Pawn>();
            float min = 0f;
            try
            {
                min = f.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Combat, null);
            }
            catch
            {
            }
            var parms = new PawnGroupMakerParms { groupKind = PawnGroupKindDefOf.Combat, faction = f, points = Mathf.Max(points, min * 1.05f), tile = map.Tile };
            try
            {
                foreach (var p in PawnGroupMakerUtility.GeneratePawns(parms, false))
                {
                    if (list.Count >= 8 || p == null || p.RaceProps?.Humanlike != true)
                    {
                        if (p != null) Arrivals.Discard(p);
                        continue;
                    }
                    list.Add(p);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a band against a camp: its fighters", e);
            }
            return list;
        }

        /// <summary>
        /// The edge they come in at: open, with a way to the camp, nearest the camp and away from the colony (the best of a
        /// few tried), so they come from the camp's side of the land.
        /// </summary>
        private static bool Edge(Map map, IntVec3 camp, out IntVec3 entry)
        {
            entry = IntVec3.Invalid;
            var colony = CampSite.ColonyCenter(map);
            var tp = TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false);
            float best = float.MaxValue;
            for (int i = 0; i < 30; i++)
            {
                if (!CellFinder.TryFindRandomEdgeCellWith(x => x.Standable(map) && !x.Fogged(map) && map.reachability.CanReach(x, camp, PathEndMode.OnCell, tp), map,
                        CellFinder.EdgeRoadChance_Hostile, out var cell)) continue;
                float score = Mathf.Sqrt(cell.DistanceToSquared(camp)) - 0.5f * Mathf.Sqrt(cell.DistanceToSquared(colony));
                if (score < best)
                {
                    best = score;
                    entry = cell;
                }
            }
            return entry.IsValid;
        }

        /// <summary>A side of the map in words, from where they came in: "north", "east", "south", "west".</summary>
        private static string SideOf(Map map, IntVec3 c)
        {
            int n = map.Size.z - 1 - c.z, e = map.Size.x - 1 - c.x, s = c.z, w = c.x;
            int least = Mathf.Min(Mathf.Min(n, e), Mathf.Min(s, w));
            int side = least == n ? 0 : least == e ? 1 : least == s ? 2 : 3;
            return ("CC_Letter_Dir_" + "NESW"[side]).Translate().Resolve();
        }

        /// <summary>
        /// A camp's text about the band, with {PEOPLE} (the band's), {BAND} (what they are, by their age), {SIDE}, {GROUND}
        /// (the old site), {HOW} (how word goes, by the colony's age), {CAMPERS}, {ARMED} and {DOWN}, besides the camp's own.
        /// </summary>
        public static string Words(Camp c, CampRaid b, string raw, Pawn x = null, Pawn a = null, int count = 0)
        {
            if (raw.NullOrEmpty() || c == null) return "";
            var g = CampGrounds.ById(c.groundId);
            var here = c.Here;
            string s = raw.Replace("{PEOPLE}", b?.people?.Name ?? "?")
                .Replace("{BAND}", ("CC_CampRaid_Band_" + Ages.Tag(Ages.Of(b?.people))).Translate().Resolve())
                .Replace("{SIDE}", b?.map != null ? SideOf(b.map, b.entry) : "")
                .Replace("{GROUND}", g?.name ?? "")
                .Replace("{HOW}", ("CC_CampRaid_How_" + (Radio(c) ? "radio" : "runner")).Translate().Resolve())
                .Replace("{CAMPERS}", here.Count(p => !p.DevelopmentalStage.Juvenile()).ToString())
                .Replace("{ARMED}", here.Count(p => !p.DevelopmentalStage.Juvenile() && p.equipment?.Primary != null).ToString())
                .Replace("{DOWN}", (b?.down ?? 0).ToString());
            return c.Format(s, x, a, count);
        }

        private static string Words(Camp c, CampRaid b, string key, bool keyed, Pawn x = null, Pawn a = null, int count = 0) => Words(c, b, key.Translate().RawText, x, a, count);

        /// <summary>A line of theirs as they gather, one sentence, by why they came.</summary>
        private static string Line(CampRaid b) => TextVariants.Vary(b.claim ? "CC_CampRaid_SayClaim" : "CC_CampRaid_Say").Translate().RawText;

        /// <summary>The colony's age and the camp's both have the radio, and the colony a comms console: word goes over the air, at once.</summary>
        private static bool Radio(Camp c) =>
            c?.map != null && Ages.Ours >= WorldAge.Industrial && Ages.Of(c.faction) >= WorldAge.Industrial
            && c.map.listerBuildings.allBuildingsColonist.Any(b => b is Building_CommsConsole && (b.TryGetComp<CompPowerTrader>()?.PowerOn ?? true));

        /// <summary>The colony's question, as the band is seen gathering: a camp's letter in a threat's colour, its answers in CampAnswers ("raided").</summary>
        private static void Ask(Camp c, CampRaid b)
        {
            var seer = IncidentWorker_Happening.Colonists(c.map).Where(p => !p.Downed && !p.DevelopmentalStage.Juvenile())
                .OrderBy(p => p.Position.DistanceToSquared(b.entry)).FirstOrDefault();
            var parts = new List<string> { Words(c, b, "CC_CampRaid_Text", true, null, seer, b.came) };
            if (b.together) parts.Add(Words(c, b, "CC_CampRaid_TextTogether", true));
            else if (b.claim) parts.Add(Words(c, b, "CC_CampRaid_TextClaim", true));
            parts.Add(Words(c, b, b.hostile ? "CC_CampRaid_TextOurFoes" : "CC_CampRaid_TextNotOurs", true));
            c.Ask("raided", Words(c, b, "CC_CampRaid_Title", true), string.Join(" ", parts), null, 0.12f, null, CampCrowdDefOf.Rimfolk_CampLetterDanger);
        }

        // ---------------- The colony's answer ----------------

        /// <summary>
        /// The colony's answer: help (word goes that help comes; the player drafts whoever goes), warn (word only), or stay out.
        /// Returns what is told (or null).
        /// </summary>
        public static string Answer(Camp c, CampRaidAnswer a)
        {
            var b = Against(c);
            if (b == null) return null;
            b.answer = a;
            if (!CampRaidMath.SendsWord(a)) return Words(c, b, "CC_CampRaid_StayOut", true);
            string word = SendWord(c, b);
            return a == CampRaidAnswer.Help ? word + " " + Words(c, b, "CC_CampRaid_HelpDraft", true) : word;
        }

        /// <summary>
        /// Word to the camp, the colony's way by its age: over the radio at once, where both have it; else a runner, the
        /// quickest free grown colonist who can reach the camp, sprinting there (the camp acts when they get there). Told
        /// what was done.
        /// </summary>
        private static string SendWord(Camp c, CampRaid b)
        {
            if (Radio(c))
            {
                Reached(c, b, null);
                return Words(c, b, "CC_CampRaid_WordRadio", true);
            }
            var fire = c.FireCell;
            var runner = IncidentWorker_Happening.Colonists(c.map)
                .Where(p => !p.Downed && !p.Drafted && !p.InMentalState && !p.DevelopmentalStage.Juvenile() && p.health.capacities.CapableOf(PawnCapacityDefOf.Moving)
                            && p.CanReach(fire, PathEndMode.Touch, Danger.Some))
                .OrderByDescending(p => p.GetStatValue(StatDefOf.MoveSpeed) / Mathf.Max(1f, Mathf.Sqrt(p.Position.DistanceToSquared(fire)) / 20f)).FirstOrDefault();
            if (runner == null) return Words(c, b, "CC_CampRaid_NoRunner", true);
            if (!CellFinder.TryFindRandomReachableNearbyCell(fire, c.map, 4f, TraverseParms.For(runner), x => x.Standable(c.map), null, out var to)) to = fire;
            var job = JobMaker.MakeJob(JobDefOf.Goto, to);
            job.locomotionUrgency = LocomotionUrgency.Sprint;
            job.playerForced = true;
            runner.jobs.StartJob(job, JobCondition.InterruptForced);
            b.runner = runner;
            return Words(c, b, "CC_CampRaid_WordRunner", true, null, runner);
        }

        /// <summary>
        /// Word reached them: with help coming, or strong enough against the band, they stand by their fire; else they pack
        /// what they can and go, before the band comes (CampRaidMath.Stand).
        /// </summary>
        private static void Reached(Camp c, CampRaid b, Pawn runner)
        {
            b.warned = true;
            b.runner = null;
            if (!c.Settled || c.Hostile) return;
            b.stood = CampRaidMath.Stand(CampRaids.Strength(c.Here), CampRaids.Strength(b.Here), b.answer == CampRaidAnswer.Help, c.kind.fights || c.guards);
            // together: the runner speaks once got to the camp's fire (within seven cells of it, Track), where the campers are.
            if (runner != null) Speech.Say(runner, Words(c, b, "CC_CampRaid_RunnerSays", true, null, runner));
            if (b.stood)
            {
                c.Guard();
                c.Note(Words(c, b, "CC_CampRaid_WordStand", true, null, runner), MessageTypeDefOf.NeutralEvent, Importance.Notable, null, true);
            }
            else
            {
                b.fledInTime = true;
                c.drivenBy = b.people?.Name;
                c.Note(Words(c, b, "CC_CampRaid_WordGo", true, null, runner), MessageTypeDefOf.NeutralEvent, Importance.Notable, null, true);
                c.Leave("warned");
            }
        }

        // ---------------- The fight, watched ----------------

        /// <summary>The camps' quarter hour: each band at this home watched (CampTracker), and let go once it is gone.</summary>
        public static void Watch(CampHome h, int now)
        {
            foreach (var b in h.raids.ToList())
            {
                if (b == null)
                {
                    h.raids.Remove(b);
                    continue;
                }
                try
                {
                    if (b.end != null)
                    {
                        if (!b.Over) continue;
                        Gone(b);
                        h.raids.Remove(b);
                        continue;
                    }
                    Track(b, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("a band against a camp, watched", e);
                    h.raids.Remove(b);
                }
            }
        }

        private static void Track(CampRaid b, int now)
        {
            var c = CampTracker.Instance?.ById(b.campId);
            var here = b.Here;
            if (b.map == null || !Find.Maps.Contains(b.map) || !b.LordAlive || here.Count == 0)
            {
                Settle(b, c, here.Count == 0 && b.came > 0 && b.pawns.All(p => p == null || p.Dead || p.Downed || !p.Spawned) ? CampRaidEnd.CampersWon : CampRaidEnd.Held);
                return;
            }
            var fire = b.at;
            // The runner on the way: there, the camp hears it; gone off it (down, called away), the word never comes.
            if (b.runner != null)
            {
                if (b.runner.Dead || b.runner.Downed || !b.runner.Spawned) b.runner = null;
                else if (b.runner.Position.InHorDistOf(fire, 7f) && c != null) Reached(c, b, b.runner);
                else if (b.runner.CurJobDef != JobDefOf.Goto) b.runner = null;
            }
            // They set off from the edge.
            var toil = b.lord.CurLordToil;
            if (!b.Moving && !(toil is LordToil_Stage))
            {
                b.moveTick = now;
                b.campStood = c?.Here.Count(p => !p.Downed && !p.DevelopmentalStage.Juvenile()) ?? 0;
                if (c != null)
                    Tell.Message(NewsKind.World, Importance.Important, b.map, Words(c, b, "CC_CampRaid_Moving", true), new LookTargets(here.Cast<Thing>()), MessageTypeDefOf.ThreatSmall);
            }
            // The band on them: those not warned find out now, and stand or run.
            bool campGone = c == null || !c.Settled;
            if (!campGone && !c.guarding && here.Any(p => !p.Downed && p.Position.InHorDistOf(fire, SeenRadius)))
            {
                b.stood = CampRaidMath.Stand(Strength(c.Here), Strength(here), b.answer == CampRaidAnswer.Help && b.warned, c.kind.fights || c.guards);
                if (b.stood)
                {
                    c.Guard();
                    if (!b.warned) c.Note(Words(c, b, "CC_CampRaid_SurprisedStand", true), MessageTypeDefOf.NeutralEvent, Importance.Notable, here.FirstOrDefault(), true);
                }
                else
                {
                    c.Note(Words(c, b, "CC_CampRaid_SurprisedRun", true), MessageTypeDefOf.NegativeEvent, Importance.Notable, here.FirstOrDefault(), true);
                    c.drivenBy = b.people?.Name;
                    c.Leave("raided", true);
                    campGone = true;
                }
            }
            // The colony's fighters at the camp while the band is on it, or sent against the band wherever it is: they fought beside them.
            foreach (var p in b.map.mapPawns.FreeColonistsSpawned)
                if (p.Drafted && !b.helpers.Contains(p)
                    && (b.Moving && p.Position.InHorDistOf(fire, HelpRadius) || b.answer == CampRaidAnswer.Help && here.Any(x => !x.Downed && x.Position.InHorDistOf(p.Position, 20f))))
                    b.helpers.Add(p);
            int bandUp = here.Count(p => !p.Downed);
            b.down = Mathf.Max(b.down, b.came - bandUp);
            // Half of them down, they run (the band's own group, Trigger_FractionPawnsLost); hours of it, they give up and walk off.
            bool bandFled = toil is LordToil_ExitMapAndDefendSelf;
            if (toil is LordToil_ExitMap)
            {
                Settle(b, c, CampRaidEnd.Held);
                return;
            }
            int campUp = c == null ? 0 : c.Here.Count(p => !p.Downed && !p.DevelopmentalStage.Juvenile());
            // Half of those who stood down, and the band still the stronger: the campers break and run.
            if (!campGone && c.guarding && b.Moving && CampRaidMath.CampersBreak(campUp, b.campStood, bandUp))
            {
                c.drivenBy = b.people?.Name;
                c.Leave("raided", true);
                campGone = true;
            }
            float hours = b.Moving ? (now - b.moveTick) / (float)GenDate.TicksPerHour : 0f;
            var end = CampRaidMath.Outcome(bandUp, b.came, bandFled, campUp, b.Moving ? b.campStood : 1, campGone, hours);
            // Before they move, a camp that went in time is all the outcome there is: the band takes the empty camp.
            if (end == CampRaidEnd.CampersLost && !b.Moving && !b.fledInTime) end = CampRaidEnd.Ongoing;
            if (end != CampRaidEnd.Ongoing) Settle(b, c, end);
        }

        /// <summary>How it ended: the band told what to do, the camp at ease or gone, the colony told, thanks for the help.</summary>
        private static void Settle(CampRaid b, Camp c, CampRaidEnd end)
        {
            b.end = end.ToString();
            b.endTick = Now;
            b.runner = null;
            if (b.LordAlive)
                b.lord.ReceiveMemo(end == CampRaidEnd.CampersLost ? LordJob_CampRaid.MemoWon : end == CampRaidEnd.Held ? LordJob_CampRaid.MemoLeave : LordJob_CampRaid.MemoFlee);
            bool there = c != null && c.Settled && !c.Hostile;
            if (there)
            {
                c.AtEase();
                if (end != CampRaidEnd.CampersLost) c.Aftermath(b);
                else
                {
                    // Beaten where they stood: those who can, go; the hurt once they can walk (CampLife.SendOff).
                    c.drivenBy = b.people?.Name;
                    c.Leave("raided", true);
                }
            }
            var look = c != null && c.Look.IsValid ? c.Look : new LookTargets(b.Here.Cast<Thing>());
            if (c != null)
            {
                int dead = c.members.Count(p => p != null && p.Dead);
                string key = end == CampRaidEnd.CampersWon ? "CC_CampRaid_Won" : end == CampRaidEnd.Held ? "CC_CampRaid_Held" : b.fledInTime ? "CC_CampRaid_Gone"
                    : dead > 0 ? "CC_CampRaid_Lost" : "CC_CampRaid_LostNoDead";
                string text = Words(c, b, key, true, null, null, dead);
                var def = end == CampRaidEnd.CampersLost && !b.fledInTime ? LetterDefOf.NegativeEvent : LetterDefOf.NeutralEvent;
                Tell.Letter(NewsKind.World, Importance.Notable, b.map, Words(c, b, "CC_CampRaid_EndTitle", true), text, def, look, c.faction);
                CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.World, text, c.leader));
                Thank(b, c, end);
            }
        }

        /// <summary>
        /// What their people make of the colony's part (CampRaidMath.Goodwill, GiftWorth): fought beside them, they remember
        /// it ("sidedWith") and the band's people remember it against the colony ("tookSides"), and the campers, holding
        /// their camp, carry a gift of their people's goods to the colony's gate; word sent in time, a little.
        /// </summary>
        private static void Thank(CampRaid b, Camp c, CampRaidEnd end)
        {
            if (c?.faction == null || c.faction.IsPlayer) return;
            bool fought = b.answer == CampRaidAnswer.Help && b.helpers.Count > 0;
            bool warned = b.warned && CampRaidMath.SendsWord(b.answer);
            int goodwill = CampRaidMath.Goodwill(fought, warned, end);
            try
            {
                if (goodwill > 0 && !c.faction.HostileTo(Faction.OfPlayer)) c.faction.TryAffectGoodwillWith(Faction.OfPlayer, goodwill, true, false);
                if (fought)
                {
                    FactionMemory.Note(c.faction, "sidedWith", b.people?.Name, 14);
                    if (b.people != null) FactionMemory.Note(b.people, "tookSides", c.faction.Name);
                }
                else if (warned) FactionMemory.Note(c.faction, "warned", b.people?.Name);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a band against a camp: remembered", e);
            }
            if (!fought) return;
            int worth = CampRaidMath.GiftWorth(true, b.down, end);
            string names = TextUtil.JoinAnd(b.helpers.Where(p => p != null && !p.Dead).Select(p => p.LabelShort).Take(5).ToList());
            string text = Words(c, b, "CC_CampRaid_Thanks".Translate().RawText.Replace("{HELPERS}", names));
            if (worth > 0 && c.Settled)
            {
                var gift = ThingRoles.From(ThingRole.Gift, c.faction, c.map, ThingRoles.Seed("camp thanks", c.id, b.people)) ?? ThingDefOf.Silver;
                int n = ThingRoles.CountFor(worth, gift, 1);
                if (c.BringThanks(gift, n)) text += " " + Words(c, b, "CC_CampRaid_ThanksGift".Translate().RawText.Replace("{GIFT}", GenLabel.ThingLabel(gift, null, n)), null, null, n);
            }
            Tell.Letter(NewsKind.World, Importance.Notable, c.map, Words(c, b, "CC_CampRaid_ThanksTitle", true), text, LetterDefOf.PositiveEvent, c.Look, c.faction);
        }

        /// <summary>The band is off the map: a band that took the camp's old site and held it goes into the site's history.</summary>
        private static void Gone(CampRaid b)
        {
            if (b.heldTold || b.end != CampRaidEnd.CampersLost.ToString() || b.groundId < 0) return;
            b.heldTold = true;
            if (b.claim || b.together) CampGrounds.HeldBy(CampGrounds.ById(b.groundId), b.people, b.campPeople?.Name);
        }

        // ---------------- Two for one old site ----------------

        /// <summary>
        /// A second party came for the camp's old site, of a people at peace with them: it gives way and camps elsewhere on
        /// the land, when there is room for it; its leader says so as they arrive.
        /// </summary>
        public static void GiveWay(Camp c, Faction people)
        {
            var map = c?.map;
            var tracker = CampTracker.Instance;
            if (map == null || tracker == null || !CampTracker.RoomFor(map) || !CampCrowds.AtPeaceHere(map, people)) return;
            var g = CampGrounds.ById(c.groundId);
            var kinds = DefDatabase<CampKindDef>.AllDefs.Where(k => k.FitsEra() && k.Possible && !k.fights && k.FitsMap(map) && k.FitsSeason(map) && k.FitsFaction(people) && Peoples.CanSend(k)
                                                                    && k.needs.NullOrEmpty() && !k.seekOnly).ToList();
            if (kinds.Count == 0) return;
            var kind = kinds.RandomElementByWeight(k => k.WeightNow(map));
            var usual = CampWeather.StayRange(map, GenDate.TicksPerDay * 7);
            bool hot = CampWeatherMath.PitchForHeat(usual.high);
            if (!CampSite.TryFind(map, out var center, hot)) return;
            var setup = tracker.Make(map, center, CampSite.ColonyCenter(map), kind, people, hot, null, null);
            if (setup == null) return;
            setup.land = TravelLand.Read(map);
            var other = tracker.Start(setup);
            if (other == null)
            {
                CampTracker.Discard(setup);
                return;
            }
            other.gaveWayAt = g?.name;
            CampTracker.AnnounceCamp(other);
            string raw = "CC_CampGround_GaveWay".Translate().RawText.Replace("{GROUND}", g?.name ?? "").Replace("{OTHER}", people.Name).Replace("{OPLACE}", other.place ?? "");
            c.Note(c.Format(raw, null, null, 0, other.Label), MessageTypeDefOf.NeutralEvent, Importance.Notable, other.leader, true);
        }
    }

    /// <summary>
    /// A band come against a camp: they gather at the edge a few hours (or until harmed), then go for the camp and fight
    /// whoever of the camp's people stands there (the game's own defend duty at the camp's fire); the camp taken, they hold
    /// it a while (an hour, half a day for a claim) and go; half of them down, or told the fight is lost, they run.
    /// </summary>
    public class LordJob_CampRaid : LordJob
    {
        public IntVec3 camp, stage;
        public int stageTicks, holdTicks;

        public const string MemoGo = "CampRaidGo";
        public const string MemoWon = "CampRaidWon";
        public const string MemoFlee = "CampRaidFlee";
        public const string MemoLeave = "CampRaidLeave";

        public LordJob_CampRaid()
        {
        }

        public LordJob_CampRaid(IntVec3 camp, IntVec3 stage, int stageTicks, int holdTicks)
        {
            this.camp = camp;
            this.stage = stage;
            this.stageTicks = stageTicks;
            this.holdTicks = holdTicks;
        }

        public override bool AddFleeToil => false;

        public override bool ShouldExistWithoutPawns => false;

        public override StateGraph CreateGraph()
        {
            var graph = new StateGraph();
            var gather = new LordToil_Stage(stage);
            graph.StartingToil = gather;
            var attack = new LordToil_DefendPoint(camp, 20f, 8f);
            graph.AddToil(attack);
            var hold = new LordToil_DefendPoint(camp, 10f, 6f);
            graph.AddToil(hold);
            var leave = new LordToil_ExitMap(LocomotionUrgency.Jog, false, true);
            graph.AddToil(leave);
            var flee = new LordToil_ExitMapAndDefendSelf();
            graph.AddToil(flee);

            var go = new Transition(gather, attack);
            go.AddTrigger(new Trigger_TicksPassed(stageTicks));
            go.AddTrigger(new Trigger_Memo(MemoGo));
            go.AddTrigger(new Trigger_PawnHarmed(1f, false, null, null, null));
            go.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(go);

            var won = new Transition(attack, hold);
            won.AddSource(gather);
            won.AddTrigger(new Trigger_Memo(MemoWon));
            graph.AddTransition(won);

            var done = new Transition(hold, leave);
            done.AddTrigger(new Trigger_TicksPassed(holdTicks));
            graph.AddTransition(done);

            var giveUp = new Transition(attack, leave);
            giveUp.AddSource(gather);
            giveUp.AddTrigger(new Trigger_Memo(MemoLeave));
            giveUp.AddTrigger(new Trigger_TicksPassed(Mathf.RoundToInt(CampRaidMath.GiveUpHours * GenDate.TicksPerHour) + stageTicks));
            graph.AddTransition(giveUp);

            var run = new Transition(attack, flee);
            run.AddSources(gather, hold);
            run.AddTrigger(new Trigger_FractionPawnsLost(0.5f));
            run.AddTrigger(new Trigger_Memo(MemoFlee));
            run.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(run);
            return graph;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref camp, "camp");
            Scribe_Values.Look(ref stage, "stage");
            Scribe_Values.Look(ref stageTicks, "stageTicks");
            Scribe_Values.Look(ref holdTicks, "holdTicks");
        }
    }

    /// <summary>
    /// A camp stands guard by its fire against a band (Camp.Guard): its grown people on the game's own defend duty there
    /// (those with weapons fight whoever comes at the camp), its animals about the camp as always.
    /// </summary>
    public class LordToil_CampGuard : LordToil
    {
        private readonly IntVec3 center;

        public const float Radius = 16f;

        public LordToil_CampGuard(IntVec3 center)
        {
            this.center = center;
        }

        public override IntVec3 FlagLoc => center;

        public override bool AllowSatisfyLongNeeds => false;

        public override void UpdateAllDuties()
        {
            foreach (var p in lord.ownedPawns)
                p.mindState.duty = p.RaceProps.Humanlike
                    ? new PawnDuty(DutyDefOf.Defend, center, Radius)
                    : new PawnDuty(CampDefOf.Council_CampBeasts, center, CampSite.CoreRadius + 6f);
        }
    }
}
