using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // The content round's camps (docs/content-update-proposals-2.md, section 4, D33 to D38; Defs/CampDefs/
    // Rimfolk_CampKinds_Round2.xml): diggers at the old place, a herd kept apart, the stone-raisers, smugglers by night,
    // novices on the road and a lord's hunt. Each is a camp kind with ways of its own (Camps/CampWorker.cs), its state kept
    // with the camp (CampWorkState); the pure rules are World/Variety/Round2Math.cs.

    /// <summary>What the round's camps share: the colony's believers of a god by its key, and the camps' state on the map.</summary>
    public static class CampRound2
    {
        public const string Diggers = "Rimfolk_Camp_Diggers", HerdApart = "Rimfolk_Camp_HerdApart", StoneRaisers = "Rimfolk_Camp_StoneRaisers",
            Smugglers = "Rimfolk_Camp_Smugglers", Novices = "Rimfolk_Camp_Novices", LordsHunt = "Rimfolk_Camp_LordsHunt";

        public static readonly string[] Kinds = { Diggers, HerdApart, StoneRaisers, Smugglers, Novices, LordsHunt };

        public static int Now => Find.TickManager.TicksGame;

        /// <summary>The colony's people at this home who hold the god (not those who only dread it), strongest first.</summary>
        public static List<Pawn> Believers(Map map, God g)
        {
            if (g == null || map == null || !Gods.On) return new List<Pawn>();
            try
            {
                return PawnGods.HoldersOf(g).Where(x => !PawnGodsMath.Fears(x.word) && x.pawn.Spawned && x.pawn.Map == map).Select(x => x.pawn).ToList();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 2 camps: believers", e);
                return new List<Pawn>();
            }
        }

        /// <summary>A mood for each of a god's believers at this home (the god is not named in it).</summary>
        public static int Feel(Map map, God g, string thought)
        {
            var def = DefDatabase<ThoughtDef>.GetNamedSilentFail(thought);
            if (def == null) return 0;
            var list = Believers(map, g);
            foreach (var p in list) CouncilGameComponent.GiveThought(p, def);
            return list.Count;
        }

        /// <summary>A job of the camp's own (a CampJobExtension's: a spot to stand at and a target to face), or null when there's no such def.</summary>
        public static Job Work(string def, IntVec3 at, LocalTargetInfo face)
        {
            var d = DefDatabase<JobDef>.GetNamedSilentFail(def);
            return d == null || !at.IsValid ? null : JobMaker.MakeJob(d, at, face.IsValid ? face : new LocalTargetInfo(at));
        }

        /// <summary>An open cell near <paramref name="target"/> this one of them can reach and stand on, clear of the colony.</summary>
        public static IntVec3 Near(Camp c, Pawn p, IntVec3 target, int radius)
        {
            var map = c?.map;
            if (map == null || !target.IsValid || p == null) return IntVec3.Invalid;
            return CellFinder.TryFindRandomCellNear(target, map, radius, x => x.Standable(map) && x.GetEdifice(map) == null && x.GetFirstPawn(map) == null && !x.Fogged(map)
                                                                                && c.ClearOfColony(x) && p.CanReach(x, PathEndMode.OnCell, Danger.Some), out var cell)
                ? cell
                : IntVec3.Invalid;
        }

        /// <summary>A far corner of the land, clear of the colony: where a herd kept apart and the smugglers pitch.</summary>
        public static IntVec3 FarCorner(Map map, IntVec3 usual, bool shade)
        {
            if (map == null) return usual;
            var mid = CampSite.ColonyCenter(map);
            if (!mid.IsValid) mid = map.Center;
            var (x, z) = Round2Math.FarCorner(map.Size.x, map.Size.z, mid.x, mid.z, CampSite.CoreRadius + 12);
            var c = CampLand.TryFindNear(map, new IntVec3(x, 0, z), 0f, 26f, shade);
            return c.IsValid ? c : usual;
        }

        /// <summary>The world event that brought a camp of this kind (its situationKinds), or null.</summary>
        public static WorldSituation Behind(Camp c) => CampWords.Situation(c?.kind);
    }

    public partial class Camp
    {
        /// <summary>
        /// Something of theirs carried to the colony's gate and set down there by one of them (the diggers' share): the thing
        /// itself goes into a porter's pack for the walk (CampCarry.cs, its set-down told as goods brought). False with nobody
        /// free to carry it or no way to the gate.
        /// </summary>
        public bool BringFound(Thing t)
        {
            if (t == null || t.Destroyed || map == null || !Settled) return false;
            var porter = Free.Where(p => !p.WorkTagIsDisabled(WorkTags.Hauling) && p.inventory != null && CarryOf(p) == null).OrderBy(p => p == trader ? 1 : 0).FirstOrDefault();
            if (porter == null) return false;
            var gate = ErrandCell("gate", null, porter);
            if (!gate.IsValid) return false;
            porter.inventory.TryAddItemNotForSale(t);
            carries.Add(new CampCarry { by = porter, help = "bought", def = t.def, count = t.stackCount, to = gate, from = Now, until = Now + GenDate.TicksPerHour * 12, carrying = true, taken = true });
            return true;
        }
    }

    // ===================== D33. Diggers at the old place =====================

    public class DigState : CampWorkState
    {
        /// <summary>Asked about the old graves; let dig them; told to keep to the ruins; the night rolled; at the graves now; seen there; found by morning; the share carried.</summary>
        public bool asked, graves, ruinsOnly, nightRolled, atGraves, caught, found, shared;
        public Pawn nightDigger;
        public IntVec3 grave = IntVec3.Invalid;
        public int nightUntil = -1;
        public string shareWords;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref asked, "asked");
            Scribe_Values.Look(ref graves, "graves");
            Scribe_Values.Look(ref ruinsOnly, "ruinsOnly");
            Scribe_Values.Look(ref nightRolled, "nightRolled");
            Scribe_Values.Look(ref atGraves, "atGraves");
            Scribe_Values.Look(ref caught, "caught");
            Scribe_Values.Look(ref found, "found");
            Scribe_Values.Look(ref shared, "shared");
            Scribe_References.Look(ref nightDigger, "nightDigger");
            Scribe_Values.Look(ref grave, "grave", IntVec3.Invalid);
            Scribe_Values.Look(ref nightUntil, "nightUntil", -1);
            Scribe_Values.Look(ref shareWords, "shareWords");
        }
    }

    /// <summary>
    /// Diggers at the old place (D33): three to five with picks and sieves camp by the ruins on the colony's land (the kind's
    /// need, CampLand.RuinTargets) and dig there by day, sifting the earth at their fire. Where the land has old graves that
    /// aren't the colony's (a grave nobody of the colony built), they ask on their first morning to dig those too: let, they
    /// do (and the colony's believers of the keeper of the dead are uneasy, Tamiu's); told no, they keep to the ruins, though
    /// one night in a stay one of them may go to the graves with a spade, caught if someone of the colony is awake near enough
    /// (a quarrel: their unease at the brawl's mark, CampBrawls), else found by the turned earth in the morning. On their last
    /// day one of them carries the colony's share to its gate: an inert relic where Vanilla Ideology Expanded - Relics and
    /// Artifacts is loaded, else an old worn thing, now and then something precious sealed long ago (as the old powers' buried
    /// finds give); two things if they dug the graves.
    /// </summary>
    public class CampWorker_Diggers : CampWorker
    {
        public const string GravesAsk = "graves";
        private static readonly string[] Relics = { "VIE_RelicInertBone", "VIE_RelicInertScroll", "VIE_RelicInertMedallion" };

        public static DigState StateOf(Camp c) => c?.workState as DigState;

        private static int Now => CampRound2.Now;

        /// <summary>Old graves on the land that aren't the colony's (built by nobody of it), spawned and seen.</summary>
        public static List<Building_Grave> OldGraves(Map map) =>
            map == null
                ? new List<Building_Grave>()
                : map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial).OfType<Building_Grave>().Where(g => g.Spawned && g.Faction != Faction.OfPlayer && !g.Position.Fogged(map)).ToList();

        public override IntVec3 SiteFor(Map map, IntVec3 usual, bool shade)
        {
            var t = CampLand.RuinTargets(map).FirstOrDefault();
            if (!t.IsValid) return usual;
            var c = CampLand.TryFindNear(map, t, 9f, 20f, shade);
            return c.IsValid ? c : usual;
        }

        public override void Started(Camp c)
        {
            if (!(c.workState is DigState)) c.workState = new DigState();
        }

        public override void Hourly(Camp c, int hour)
        {
            if (!(c.workState is DigState st))
            {
                Started(c);
                st = StateOf(c);
            }
            var map = c.map;
            // The graves asked about on their first morning, when the land has any.
            if (!st.asked && c.DaysHere >= 0.3f && hour >= 7 && hour <= 11 && ColonyCouncil.Tell.CanAsk && OldGraves(map).Count > 0)
            {
                var ask = c.AskByKey(GravesAsk);
                if (ask != null && c.AskBlocked(ask) == null && c.Request(ask)) st.asked = true;
            }
            // Told no (or the letter let go): the ruins only.
            if (!st.graves && !st.ruinsOnly && c.asksMade.Contains(GravesAsk)) st.ruinsOnly = true;
            Night(c, st, hour);
            // The colony's share, carried to its gate in their last hours.
            if (!st.shared && c.untilTick != int.MaxValue && Now >= c.untilTick - 4 * GenDate.TicksPerHour && hour >= 7 && hour <= 18) Share(c, st);
        }

        /// <summary>One night, told to keep to the ruins: one of them goes to the old graves with a spade; caught or found in the morning.</summary>
        private static void Night(Camp c, DigState st, int hour)
        {
            var map = c.map;
            if (st.ruinsOnly && !st.nightRolled && hour == 23 && c.DaysHere >= 1f)
            {
                st.nightRolled = true;
                var grave = OldGraves(map).RandomElementWithFallback();
                var digger = c.Here.Where(p => p != c.leader && !p.Downed && !p.DevelopmentalStage.Juvenile()).RandomElementWithFallback();
                if (grave == null || digger == null || !Rand.Chance(Round2Math.NightAtGravesChance)) return;
                st.nightDigger = digger;
                st.grave = grave.Position;
                st.nightUntil = Now + 4 * GenDate.TicksPerHour;
                return;
            }
            if (st.nightDigger == null || st.caught || st.found) return;
            var d = st.nightDigger;
            if (d.Dead || !d.Spawned || d.Map != map || Now > st.nightUntil + 8 * GenDate.TicksPerHour)
            {
                st.nightDigger = null;
                return;
            }
            bool there = d.Position.InHorDistOf(st.grave, 3f) && d.CurJobDef?.defName == "Rimfolk_R2_CampDigNight";
            if (there && !st.atGraves)
            {
                st.atGraves = true;
                // Someone of the colony awake and near enough sees it.
                var seen = IncidentWorker_Happening.Colonists(map)
                    .Where(p => p.Spawned && !p.Downed && !p.InMentalState && Round2Math.Caught(p.Position.DistanceTo(st.grave), p.Awake()))
                    .OrderBy(p => p.Position.DistanceToSquared(st.grave)).FirstOrDefault();
                if (seen == null) return;
                st.caught = true;
                c.tension = Round2Math.CaughtTension(c.tension);
                c.SayMoment(d, "caught");
                Speech.Say(seen, "CC_R2Dig_CaughtShout".Translate().Resolve());
                d.jobs?.EndCurrentJob(JobCondition.InterruptForced);
                c.Note(c.Text("CC_R2Dig_Caught", d, seen), MessageTypeDefOf.NegativeEvent, Importance.Notable, d, true);
                return;
            }
            // Gone back to the camp before anyone saw: the turned earth is found in the morning.
            if (st.atGraves && !there && hour >= 6 && hour <= 10)
            {
                st.found = true;
                c.AddTension(Round2Math.FoundTension);
                foreach (var x in GenRadial.RadialCellsAround(st.grave, 1.5f, true))
                    if (x.InBounds(map) && x.Standable(map) && !x.Roofed(map)) FilthMaker.TryMakeFilth(x, map, ThingDefOf.Filth_Dirt, 1);
                c.Note(c.Text("CC_R2Dig_Found"), MessageTypeDefOf.NegativeEvent, Importance.Notable, null, true);
            }
        }

        /// <summary>The share: what came up, carried to the colony's gate by one of them, and told.</summary>
        public static void Share(Camp c, DigState st)
        {
            st.shared = true;
            var map = c.map;
            var things = new List<Thing>();
            bool relics = Relics.Any(n => DefDatabase<ThingDef>.GetNamedSilentFail(n) != null);
            for (int i = 0; i < Round2Math.ShareCount(st.graves); i++)
            {
                var t = Find(Round2Math.Share(relics, Rand.Value), map);
                if (t != null) things.Add(t);
            }
            if (things.Count == 0) return;
            st.shareWords = TextUtil.JoinAnd(things.Select(t => t.LabelNoCount).ToList());
            var bearer = c.Here.FirstOrDefault(p => !p.Downed);
            var left = new List<Thing>();
            foreach (var t in things)
                if (!c.BringFound(t)) left.Add(t);
            // Nobody free to carry it over: set down at the camp's edge for the colony.
            foreach (var t in left)
                if (c.share.IsValid && GenPlace.TryPlaceThing(t, c.share, map, ThingPlaceMode.Near, out var put)) put.SetForbidden(false, false);
            if (bearer != null) c.SayMoment(bearer, "share", 0.8f);
            c.Tell(c.Text("CC_R2Dig_ShareTitle"), c.Text(left.Count == things.Count ? "CC_R2Dig_ShareLeft" : "CC_R2Dig_Share", bearer, null, things.Count, st.shareWords),
                LetterDefOf.PositiveEvent, true, bearer);
        }

        /// <summary>One find of the share, made: a relic of the mod's (of a fitting material), an old worn thing, or something precious of the colony's age.</summary>
        private static Thing Find(Round2Math.DigFind kind, Map map)
        {
            try
            {
                switch (kind)
                {
                    case Round2Math.DigFind.Relic:
                    {
                        var def = Relics.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).Where(d => d != null).RandomElementWithFallback();
                        if (def == null) goto default;
                        return ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.RandomStuffFor(def) : null);
                    }
                    case Round2Math.DigFind.Sealed:
                    {
                        var def = ThingRoles.Ours(ThingRole.Precious, map, ThingRoles.Seed("round2-diggers", map?.uniqueID ?? 0, Now / GenDate.TicksPerDay));
                        if (def == null) goto default;
                        var t = ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
                        t.stackCount = Mathf.Clamp(ThingRoles.CountFor(150f, def), 1, def.stackLimit);
                        return t;
                    }
                    default:
                        return OwnMurmur.OldThing();
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 2 camps: the diggers' find", e);
                return null;
            }
        }

        public override Job DayJob(Camp c, Pawn p)
        {
            var st = StateOf(c);
            int hour = GenLocalDate.HourOfDay(c.map);
            if (st == null || hour < 7 || hour > 17 || !Rand.Chance(0.6f)) return null;
            IntVec3 target;
            if (st.graves && Rand.Chance(0.35f) && OldGraves(c.map).RandomElementWithFallback() is Building_Grave g) target = g.Position;
            else
            {
                var ruins = CampLand.RuinTargets(c.map);
                if (ruins.Count == 0) return null;
                target = ruins.RandomElement();
            }
            var at = CampRound2.Near(c, p, target, 5);
            if (!at.IsValid) return null;
            c.SayMoment(p, "dig", 0.15f);
            return CampRound2.Work("Rimfolk_R2_CampDig", at, new LocalTargetInfo(target));
        }

        public override Job NightJob(Camp c, Pawn p, int hour)
        {
            var st = StateOf(c);
            if (st == null || p != st.nightDigger || st.caught || st.found || st.atGraves || Now > st.nightUntil || !st.grave.IsValid) return null;
            var at = CampRound2.Near(c, p, st.grave, 2);
            if (!at.IsValid)
            {
                st.nightDigger = null;
                return null;
            }
            return CampRound2.Work("Rimfolk_R2_CampDigNight", at, new LocalTargetInfo(st.grave));
        }

        public override void Leaving(Camp c)
        {
            // Sent off before their last day: what they found so far is still the colony's share, if they dug two days or more.
            var st = StateOf(c);
            if (st != null && !st.shared && !c.Hostile && c.DaysHere >= 2f) Share(c, st);
        }

        public override void AddMarks(Camp c, HashSet<string> marks)
        {
            var st = StateOf(c);
            if (st == null) return;
            if (st.graves) marks.Add("graves");
            if (st.caught) marks.Add("caught");
            if (st.shared) marks.Add("shared");
        }

        public override string Fill(Camp c, string text) => text?.Replace("{GOODS}", StateOf(c)?.shareWords ?? "");

        public override string AskBlocked(Camp c, CampAsk a) =>
            a?.key == GravesAsk && OldGraves(c?.map).Count == 0 ? "CC_R2Dig_NoGraves".Translate().Resolve() : null;
    }

    /// <summary>The diggers let dig the old graves too (their ask): they do, and the colony's believers of the keeper of the dead are uneasy.</summary>
    public class DilemmaEffect_DiggersGraves : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (CampOnMap.For(ctx)?.kind?.Worker is CampWorker_Diggers)
                sheet.Add(ImpactKind.Other, -1, "CC_R2Dig_GravesImpact".Translate().Resolve(), "CC_R2Dig_GravesImpactBrief".Translate().Resolve(), "r2graves");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampOnMap.For(ctx);
            var st = CampWorker_Diggers.StateOf(c);
            if (st == null) return;
            st.graves = true;
            st.ruinsOnly = false;
            CampRound2.Feel(c.map, Gods.ByKey("Mog"), "Rimfolk_R2_GravesDug");
        }
    }

    // ===================== D34. A herd kept apart =====================

    public class HerdApartState : CampWorkState
    {
        public bool met, metRolled, tended, tendRolled, flu;
        public int tendedTick = -1;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref met, "met");
            Scribe_Values.Look(ref metRolled, "metRolled");
            Scribe_Values.Look(ref tended, "tended");
            Scribe_Values.Look(ref tendRolled, "tendRolled");
            Scribe_Values.Look(ref flu, "flu");
            Scribe_Values.Look(ref tendedTick, "tendedTick", -1);
        }
    }

    /// <summary>
    /// A herd kept apart (D34): only while a sickness in the herds runs in the region (Council_WS_Murrain), herders whose beasts
    /// are coughing camp at the land's farthest corner from the colony, their herd grazing at their camp. If their beasts and
    /// the colony's meet (within a few cells), the game's own animal flu comes to the colony's herd at its chance, once; the
    /// colony's best with animals may tend their beasts (their ask), which brings it a closer brush with the sickness. People
    /// are never touched by it.
    /// </summary>
    public class CampWorker_HerdApart : CampWorker
    {
        public const string Murrain = "Council_WS_Murrain", TendAsk = "tend";

        public static HerdApartState StateOf(Camp c) => c?.workState as HerdApartState;

        public static bool MurrainRuns() => WorldSituations.Instance?.Running?.Any(s => s?.def?.defName == Murrain) ?? false;

        public override bool FitsMap(Map map) => MurrainRuns();

        public override IntVec3 SiteFor(Map map, IntVec3 usual, bool shade) => CampRound2.FarCorner(map, usual, shade);

        public override void Started(Camp c)
        {
            if (!(c.workState is HerdApartState)) c.workState = new HerdApartState();
        }

        public override void Hourly(Camp c, int hour)
        {
            if (!(c.workState is HerdApartState st))
            {
                Started(c);
                st = StateOf(c);
            }
            var map = c.map;
            if (!st.met)
            {
                Pawn ours = null, theirs = null;
                foreach (var b in c.herd.Where(h => h != null && h.Spawned && !h.Dead && h.Map == map))
                {
                    ours = map.mapPawns.SpawnedColonyAnimals.FirstOrDefault(a => !a.Dead && a.Position.InHorDistOf(b.Position, Round2Math.MetRange));
                    if (ours == null) continue;
                    theirs = b;
                    break;
                }
                if (ours != null && theirs != null)
                {
                    st.met = true;
                    c.Note(c.Text("CC_R2Herd_Met", null, null, 0, ours.LabelShort), MessageTypeDefOf.NegativeEvent, Importance.Notable, ours, true);
                    if (!st.metRolled)
                    {
                        st.metRolled = true;
                        if (Harm.On && Harm.SystemOn(HarmKinds.CampFever.system) && Harm.Roll(HarmKinds.CampFever, Round2Math.FluChanceMet)) Flu(c, st, "CC_R2Herd_FluMet");
                    }
                }
            }
            // The one who tended their beasts back among the colony's: once, a while after.
            if (st.tended && !st.tendRolled && st.tendedTick >= 0 && CampRound2.Now >= st.tendedTick + 3 * GenDate.TicksPerHour)
            {
                st.tendRolled = true;
                if (!st.flu && Harm.On && Harm.SystemOn(HarmKinds.CampFever.system) && Harm.Roll(HarmKinds.CampFever, Round2Math.FluChanceTended)) Flu(c, st, "CC_R2Herd_FluTended");
            }
        }

        /// <summary>The game's own animal flu, on the colony's beasts (its own incident and letter), and how it came, told; only with the hurts of everyday life on, through the shared harm budget (the fever at their fire).</summary>
        private static void Flu(Camp c, HerdApartState st, string how)
        {
            if (st.flu) return;
            var map = c.map;
            var def = DefDatabase<IncidentDef>.GetNamedSilentFail("Disease_AnimalFlu");
            if (def == null || map == null || !map.mapPawns.SpawnedColonyAnimals.Any()) return;
            try
            {
                var parms = StorytellerUtility.DefaultParmsNow(def.category, map);
                parms.forced = true;
                if (!def.Worker.TryExecute(parms)) return;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 2 camps: the herd's cough comes to the colony's beasts", e);
                return;
            }
            st.flu = true;
            c.Note(c.Text(how), MessageTypeDefOf.NegativeEvent, Importance.Notable, null, true);
        }

        public override void AddMarks(Camp c, HashSet<string> marks)
        {
            var st = StateOf(c);
            if (st == null) return;
            if (st.tended) marks.Add("tended");
            if (st.flu) marks.Add("flu");
            else if (st.met) marks.Add("met");
        }

        public override string AskBlocked(Camp c, CampAsk a)
        {
            if (a?.key != TendAsk) return null;
            if (StateOf(c)?.tended ?? false) return "CC_CampAsk_Done".Translate().Resolve();
            return c.herd.Any(h => h != null && h.Spawned && !h.Dead) ? null : "CC_CampErrand_NoHerd".Translate().Resolve();
        }
    }

    /// <summary>The colony's best with animals tends the herders' coughing beasts (their ask): a couple of hours beside one, learning Animals.</summary>
    public class DilemmaEffect_TendTheirHerd : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override string Unavailable(DilemmaContext ctx)
        {
            var c = CampOnMap.For(ctx);
            if (c == null || !c.herd.Any(h => h != null && h.Spawned && !h.Dead)) return "CC_CampErrand_NoHerd".Translate().Resolve();
            return ctx?.a == null || ctx.a.WorkTypeIsDisabled(WorkTypeDefOf.Handling) ? "CC_CampVariety_NoHandler".Translate().Resolve() : null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.a == null || CampOnMap.For(ctx) == null) return;
            sheet.Play("CC_R2Herd_TendImpact".Translate(ctx.a.LabelShort).Resolve(), "CC_R2Herd_TendImpactBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampOnMap.For(ctx);
            var a = ctx?.a;
            var st = CampWorker_HerdApart.StateOf(c);
            var beast = c?.herd.Where(h => h != null && h.Spawned && !h.Dead).OrderBy(h => a != null ? h.Position.DistanceToSquared(a.Position) : 0).FirstOrDefault();
            var job = DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_R2_TendTheirHerd");
            if (st == null || beast == null || a == null || job == null || !a.Spawned || a.Downed) return;
            try
            {
                if (!a.jobs.TryTakeOrderedJob(JobMaker.MakeJob(job, beast), JobTag.Misc)) return;
                st.tended = true;
                st.tendedTick = CampRound2.Now;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 2 camps: tending the herders' beasts", e);
            }
        }
    }

    // ===================== D35. The stone-raisers =====================

    public class StoneState : CampWorkState
    {
        public int god = -1;
        public string def, stuff, godName, place;
        public IntVec3 site = IntVec3.Invalid;
        public bool asked, edge, laid, stood, rite, carve;
        public int worked, lastDay = -1, days = 3;
        public Thing made;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref god, "god", -1);
            Scribe_Values.Look(ref def, "def");
            Scribe_Values.Look(ref stuff, "stuff");
            Scribe_Values.Look(ref godName, "godName");
            Scribe_Values.Look(ref place, "place");
            Scribe_Values.Look(ref site, "site", IntVec3.Invalid);
            Scribe_Values.Look(ref asked, "asked");
            Scribe_Values.Look(ref edge, "edge");
            Scribe_Values.Look(ref laid, "laid");
            Scribe_Values.Look(ref stood, "stood");
            Scribe_Values.Look(ref rite, "rite");
            Scribe_Values.Look(ref carve, "carve");
            Scribe_Values.Look(ref worked, "worked");
            Scribe_Values.Look(ref lastDay, "lastDay", -1);
            Scribe_Values.Look(ref days, "days", 3);
            Scribe_References.Look(ref made, "made");
        }

        public ThingDef Def => def.NullOrEmpty() ? null : DefDatabase<ThingDef>.GetNamedSilentFail(def);
        public ThingDef Stuff => stuff.NullOrEmpty() ? null : DefDatabase<ThingDef>.GetNamedSilentFail(stuff);
    }

    /// <summary>
    /// The stone-raisers (D35): a people at peace that holds a god camp near a place on the colony's land that fits a god's
    /// stone (the oldest tree, the water's edge, else open ground) and ask on their first morning to raise it there; told no,
    /// they raise it at the edge of the land instead. The stone is of their own age (Round2Math.StoneFor: a totemic slab, a
    /// megalith, an obelisk, a futuristic obelisk, the game's large sculpture else), of a material they brought. One the game
    /// lets be built goes up as a real frame: their blueprint, the stone they brought carried to it, and their own building
    /// work (CampWork.BuildStep); one only carved (the obelisk of Vanilla Furniture Expanded - Art, the large sculpture) is
    /// carved where it will stand over two to four days and set up when done. When it stands they keep a short rite at it and
    /// leave it to the colony: art the colony may dedicate to a god (Gods/Worship/Shrines.cs). The colony's book has their
    /// name for the god from them (GodLore.Heard); the believers of the colony's most held god grumble when it is that god's
    /// rival.
    /// </summary>
    public class CampWorker_StoneRaisers : CampWorker
    {
        public const string PlaceAsk = "place";

        public static StoneState StateOf(Camp c) => c?.workState as StoneState;

        private static int Now => CampRound2.Now;

        public override bool FitsMap(Map map) => Gods.On && Find.FactionManager.AllFactionsListForReading.Any(f => Arrivals.Usable(f) && Gods.Of(f).Count > 0);

        /// <summary>The place that fits a god's stone: the oldest tree, else the water's edge, else none (open ground).</summary>
        private static IntVec3 Fitting(Map map, out string kind)
        {
            foreach (var k in new[] { "tree", "water" })
            {
                var cell = GodCamps.FindSite(map, k, out _);
                if (!cell.IsValid) continue;
                kind = k;
                return cell;
            }
            kind = "open";
            return IntVec3.Invalid;
        }

        public override IntVec3 SiteFor(Map map, IntVec3 usual, bool shade)
        {
            var at = Fitting(map, out _);
            if (!at.IsValid) return usual;
            var c = CampLand.TryFindNear(map, at, CampSite.CoreRadius + 6, CampSite.CoreRadius + 14, shade);
            return c.IsValid ? c : usual;
        }

        public override void Started(Camp c)
        {
            if (c.workState is StoneState) return;
            var g = Gods.Of(c.faction).RandomElementWithFallback();
            var st = new StoneState { god = g?.id ?? -1, godName = g != null ? Gods.NameBy(g, c.leader) : null, days = Round2Math.RaiseDays(Rand.Value) };
            var age = Ages.Tag(Ages.Of(c.faction));
            var def = Round2Math.StoneFor(age).Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).FirstOrDefault(d => d != null && (d.IsArt || d.ritualFocus != null));
            if (def != null)
            {
                st.def = def.defName;
                st.stuff = StuffFor(def, c.map)?.defName;
                st.carve = def.designationCategory == null || def.blueprintDef == null;
            }
            var place = Fitting(c.map, out st.place);
            st.site = Spot(c, def, place.IsValid ? place : c.center, place.IsValid);
            c.workState = st;
        }

        /// <summary>A stone of the land's own rock (its blocks) where the stone takes stone, else what it is made of by the game's choice.</summary>
        private static ThingDef StuffFor(ThingDef def, Map map)
        {
            if (def == null || !def.MadeFromStuff) return null;
            var allowed = GenStuff.AllowedStuffsFor(def).ToList();
            try
            {
                if (map != null)
                    foreach (var rock in Find.World.NaturalRockTypesIn(map.Tile))
                    {
                        var blocks = DefDatabase<ThingDef>.GetNamedSilentFail("Blocks" + rock.defName);
                        if (blocks != null && allowed.Contains(blocks)) return blocks;
                    }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 2 camps: the land's rock", e);
            }
            return GenStuff.DefaultStuffFor(def) ?? allowed.FirstOrDefault();
        }

        /// <summary>Open ground for the stone near <paramref name="near"/>, clear of the colony and away from their own camp's ground.</summary>
        private static IntVec3 Spot(Camp c, ThingDef def, IntVec3 near, bool atPlace)
        {
            var map = c.map;
            if (map == null || def == null) return IntVec3.Invalid;
            float min = atPlace ? 1f : CampSite.CoreRadius + 6;
            float max = atPlace ? 5f : CampSite.CoreRadius + 14;
            foreach (var cell in GenRadial.RadialCellsAround(near, max, true).Where(x => x.DistanceTo(near) >= min).InRandomOrder().Take(200))
            {
                var rect = GenAdj.OccupiedRect(cell, Rot4.North, def.size);
                if (c.center.IsValid && rect.Cells.Any(x => x.InHorDistOf(c.center, CampSite.CoreRadius + 5))) continue;
                if (rect.Cells.All(x => x.InBounds(map) && x.Standable(map) && !x.Fogged(map) && x.GetEdifice(map) == null && !(x.GetTerrain(map)?.IsWater ?? false)
                                        && x.GetFirstItem(map) == null && !(x.GetPlant(map)?.def.plant?.IsTree ?? false) && c.ClearOfColony(x)))
                    return cell;
            }
            return IntVec3.Invalid;
        }

        public override void Hourly(Camp c, int hour)
        {
            if (!(c.workState is StoneState st))
            {
                Started(c);
                st = StateOf(c);
                if (st == null) return;
            }
            var def = st.Def;
            if (def == null || st.stood) return;
            // Where it may stand, asked on their first morning (told no: the edge of the land).
            if (!st.asked && c.DaysHere >= 0.2f && hour >= 7 && hour <= 12 && ColonyCouncil.Tell.CanAsk)
            {
                var ask = c.AskByKey(PlaceAsk);
                st.asked = true;
                if (ask != null && c.AskBlocked(ask) == null) c.Request(ask);
            }
            if (!st.edge && !st.laid && c.asksMade.Contains(PlaceAsk) && !Granted(c))
            {
                st.edge = true;
                st.site = Spot(c, def, CampRound2.FarCorner(c.map, c.center, false), false);
            }
            // The stone set down once the place is settled (answered, or a day gone by).
            if (!st.laid && (c.asksMade.Contains(PlaceAsk) || c.DaysHere >= 1f) && hour >= 7 && hour <= 16) Lay(c, st, def);
            if (!st.laid) return;
            var built = Built(c, st, def);
            if (built != null) Stand(c, st, built);
            else if (st.carve && st.worked >= st.days && hour >= 14) Stand(c, st, null);
        }

        /// <summary>The colony said yes to the place they asked for (their ask done by its effect, which marks it).</summary>
        private static bool Granted(Camp c) => c.workState is StoneState st && st.place != null && st.place.StartsWith("+", StringComparison.Ordinal);

        /// <summary>The ask's yes: the place as they asked.</summary>
        public static void Grant(Camp c)
        {
            var st = StateOf(c);
            if (st != null && !(st.place ?? "").StartsWith("+", StringComparison.Ordinal)) st.place = "+" + st.place;
        }

        /// <summary>The place in words for their lines: by the old tree, by the water, on open ground, at the edge of the land.</summary>
        private static string PlaceWords(StoneState st) =>
            ("CC_R2Stone_Place_" + (st.edge ? "edge" : (st.place ?? "open").TrimStart('+'))).Translate().Resolve();

        /// <summary>Their stone set down at the site: a blueprint of theirs with the stone they brought by it, or a pile to carve.</summary>
        private static void Lay(Camp c, StoneState st, ThingDef def)
        {
            var map = c.map;
            if (!st.site.IsValid) st.site = Spot(c, def, c.center, false);
            if (!st.site.IsValid) return;
            st.laid = true;
            var stuff = st.Stuff;
            try
            {
                if (!st.carve && GenConstruct.CanPlaceBlueprintAt(def, st.site, Rot4.North, map, false, null, null, stuff).Accepted)
                {
                    var bp = GenConstruct.PlaceBlueprintForBuild(def, st.site, map, Rot4.North, c.faction, stuff);
                    var cost = (bp as Blueprint_Build)?.TotalMaterialCost();
                    foreach (var need in cost ?? new List<ThingDefCountClass>()) Supply(c, need.thingDef, need.count);
                }
                else
                {
                    st.carve = true;
                    if (stuff != null) Supply(c, stuff, Mathf.Min(Round2Math.StoneBlocks, stuff.stackLimit), st.site);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 2 camps: the stone laid out", e);
                st.carve = true;
            }
            c.Note(c.Text("CC_R2Stone_Laid", c.leader, null, 0, PlaceWords(st)), MessageTypeDefOf.NeutralEvent, Importance.Notable, null, true);
        }

        /// <summary>What they brought to raise it with, set down at their stash (or at the site, to carve), theirs.</summary>
        private static void Supply(Camp c, ThingDef def, int count, IntVec3? at = null)
        {
            if (def == null || count <= 0) return;
            var where = at ?? (c.stash.IsValid ? c.stash : c.center);
            while (count > 0)
            {
                var t = ThingMaker.MakeThing(def);
                t.stackCount = Mathf.Min(count, def.stackLimit);
                count -= t.stackCount;
                if (!GenPlace.TryPlaceThing(t, where, c.map, ThingPlaceMode.Near, out var put))
                {
                    t.Destroy();
                    return;
                }
                c.Own(put);
            }
        }

        /// <summary>Their stone standing finished at the site (built by them, still theirs).</summary>
        private static Building Built(Camp c, StoneState st, ThingDef def)
        {
            if (!st.site.IsValid || c.map == null) return null;
            return st.site.GetThingList(c.map).OfType<Building>().FirstOrDefault(b => b.def == def && b.Faction == c.faction);
        }

        /// <summary>The blueprint or frame of theirs at the site, if any.</summary>
        private static Thing Unfinished(Camp c, StoneState st)
        {
            if (!st.site.IsValid || c.map == null) return null;
            return st.site.GetThingList(c.map).FirstOrDefault(t => (t is Blueprint_Build || t is Frame) && t.Faction == c.faction);
        }

        /// <summary>It stands: left to the colony, the short rite kept at it, the book told their name for the god, the rival's believers grumbling.</summary>
        public static void Stand(Camp c, StoneState st, Building built)
        {
            if (st.stood) return;
            var map = c.map;
            var maker = c.Here.Where(p => p.RaceProps.Humanlike).OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Artistic)?.Level ?? 0).FirstOrDefault() ?? c.leader;
            try
            {
                if (built == null)
                {
                    // Carved where it stands: set up now, of the stone they brought, the carver's work.
                    var def = st.Def;
                    if (def == null) return;
                    // The ground clear first (nothing built, nobody standing on it): else it waits, and the stone they brought stays.
                    if (!GenAdj.OccupiedRect(st.site, Rot4.North, def.size).Cells.All(x => x.InBounds(map) && x.Standable(map) && x.GetEdifice(map) == null && x.GetFirstPawn(map) == null)) return;
                    foreach (var t in GenRadial.RadialDistinctThingsAround(st.site, map, 3f, true).Where(t => t.def == st.Stuff && c.Owns(t)).ToList()) t.Destroy();
                    var thing = ThingMaker.MakeThing(def, def.MadeFromStuff ? st.Stuff ?? GenStuff.DefaultStuffFor(def) : null);
                    thing.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Good, ArtGenerationContext.Outsider);
                    var art = thing.TryGetComp<CompArt>();
                    if (art != null && maker != null) art.JustCreatedBy(maker);
                    built = (Building)GenSpawn.Spawn(thing, st.site, map, Rot4.North);
                }
                // Left to the colony: theirs no more, so never packed; the colony's art, to dedicate to a god if it will.
                built.SetFaction(Faction.OfPlayer);
                FleckMaker.ThrowDustPuff(st.site, map, 1.5f);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 2 camps: the stone stands", e);
                return;
            }
            st.stood = true;
            st.made = built;
            var g = Gods.ById(st.god);
            if (maker != null && maker.Spawned) c.SayMoment(maker, "rite");
            Rite(c, st);
            if (g != null)
            {
                Gods.Learn(g, "visitors");
                GodLore.Heard(c.leader, g, "CC_R2Stone_Told".Translate((st.godName ?? g.name).Named("GOD"), (c.faction?.Name ?? "?").Named("FACTION")).Resolve(), "Gate", null, -1, st.godName);
                var patron = Gods.Patron(map);
                if (patron != null && patron != g && Gods.Rivals(g).Contains(patron)) CampRound2.Feel(map, patron, "Rimfolk_R2_RivalStone");
            }
            Tell.Message(NewsKind.Land, Importance.Important, map, c.Text("CC_R2Stone_Stands", maker, null, 0, built.LabelNoCount), new LookTargets(built), MessageTypeDefOf.PositiveEvent);
            if (c.faction != null) FactionMemory.Note(c.faction, "campHosted", built.LabelNoCount);
            // Their stay is done once the rite is kept.
            c.untilTick = Mathf.Min(c.untilTick, Now + 4 * GenDate.TicksPerHour);
        }

        /// <summary>Their short rite at the stone: each of them kneels at it a while (their prayer, said in the old way).</summary>
        private static void Rite(Camp c, StoneState st)
        {
            st.rite = true;
            var pray = DefDatabase<JobDef>.GetNamedSilentFail("Council_CampPray");
            if (pray == null || st.made == null) return;
            foreach (var p in c.Here.Where(x => x.RaceProps.Humanlike && !x.Downed && x.Awake()).Take(5))
            {
                var at = CampRound2.Near(c, p, st.site, 3);
                if (!at.IsValid) continue;
                try
                {
                    p.jobs.StartJob(JobMaker.MakeJob(pray, at, st.made), JobCondition.InterruptOptional, null, true, true);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("round 2 camps: the stone's rite", e);
                    return;
                }
            }
        }

        public override Job DayJob(Camp c, Pawn p)
        {
            var st = StateOf(c);
            if (st == null || !st.laid || st.stood || !st.site.IsValid) return null;
            if (!st.carve)
            {
                var t = Unfinished(c, st);
                if (t == null) return null;
                var job = CampWork.BuildStep(c, p, t, c.Goods(), out _);
                if (job != null && Rand.Chance(0.2f)) c.SayMoment(p, "haul");
                return job;
            }
            // Carved where it will stand: a day counted once a day's carving is done.
            int hour = GenLocalDate.HourOfDay(c.map);
            if (hour < 7 || hour > 17 || !Rand.Chance(0.6f)) return null;
            var at = CampRound2.Near(c, p, st.site, 2);
            // Beside the stone, never on the ground it will stand on.
            if (!at.IsValid || st.Def != null && GenAdj.OccupiedRect(st.site, Rot4.North, st.Def.size).Contains(at)) return null;
            if (st.lastDay != GenDate.DaysPassed && hour >= 12)
            {
                st.lastDay = GenDate.DaysPassed;
                st.worked++;
            }
            if (Rand.Chance(0.2f)) c.SayMoment(p, "carve");
            return CampRound2.Work("Rimfolk_R2_CampCarve", at, new LocalTargetInfo(st.site));
        }

        public override void Leaving(Camp c)
        {
            // Gone before it stood: what they set down for it is left lying, nobody's; their blueprint or frame goes with them.
            var st = StateOf(c);
            if (st == null || st.stood) return;
            var t = Unfinished(c, st);
            try
            {
                if (t != null && !t.Destroyed) t.Destroy(DestroyMode.Cancel);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 2 camps: an unfinished stone", e);
            }
        }

        public override void AddMarks(Camp c, HashSet<string> marks)
        {
            var st = StateOf(c);
            if (st == null) return;
            if (st.stood) marks.Add("stood");
            if (st.edge) marks.Add("edge");
        }

        public override string Fill(Camp c, string text)
        {
            if (text.NullOrEmpty()) return text;
            var st = StateOf(c);
            var g = Gods.ById(st?.god ?? -1);
            string stone = st?.Def != null ? st.Def.label : "CC_R2Stone_AStone".Translate().Resolve();
            return text.Replace("{GOD}", st?.godName ?? g?.name ?? "CC_R2Stone_TheirGod".Translate().Resolve()).Replace("{ITEM}", stone)
                .Replace("{SITE}", st != null ? PlaceWords(st) : "");
        }

        public override string AskBlocked(Camp c, CampAsk a)
        {
            var st = StateOf(c);
            if (a?.key == PlaceAsk && (st == null || st.laid)) return "CC_CampAsk_Done".Translate().Resolve();
            return null;
        }
    }

    /// <summary>The stone-raisers' ask granted: the stone at the place they asked for.</summary>
    public class DilemmaEffect_StonePlace : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (CampOnMap.For(ctx)?.kind?.Worker is CampWorker_StoneRaisers)
                sheet.Add(ImpactKind.Other, 1, "CC_R2Stone_PlaceImpact".Translate().Resolve(), "CC_R2Stone_PlaceImpactBrief".Translate().Resolve(), "r2stone");
        }

        public override void Apply(DilemmaContext ctx) => CampWorker_StoneRaisers.Grant(CampOnMap.For(ctx));
    }

    // ===================== D36. Smugglers by night =====================

    public class SmuggleState : CampWorkState
    {
        public bool told;
        public int walkDay = -1;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref told, "told");
            Scribe_Values.Look(ref walkDay, "walkDay", -1);
        }
    }

    /// <summary>
    /// Smugglers by night (D36): only while a ban or a shortage runs in the region (a ban on drink, markets closed, iron,
    /// medicine, cloth or timber gone scarce: the kind's situationKinds), a small camp sits at the land's far corner, keeps
    /// out of sight by day and sells by one lamp at night (their trader trades only at night, the game's own trading). Said
    /// nothing of them, one of the colony with a craving, or who holds the god of the cup, may walk out after dark to buy a
    /// bottle (their silver carried out, the bottle brought to the gate). Bought from openly (their ask), a colonist carries
    /// the silver out after dark and they bring the goods the region lacks. Told on (their other ask), the people whose law
    /// it is think better of the colony, and the smugglers go in the night and don't come back.
    /// </summary>
    public class CampWorker_Smugglers : CampWorker
    {
        public const string BuyAsk = "buy", TellAsk = "tellon";

        public static SmuggleState StateOf(Camp c) => c?.workState as SmuggleState;

        public override bool FitsMap(Map map) => CampWords.Situation(def) != null;

        public override IntVec3 SiteFor(Map map, IntVec3 usual, bool shade) => CampRound2.FarCorner(map, usual, shade);

        public override void Started(Camp c)
        {
            if (!(c.workState is SmuggleState)) c.workState = new SmuggleState();
        }

        public override void Hourly(Camp c, int hour)
        {
            if (!(c.workState is SmuggleState st))
            {
                Started(c);
                st = StateOf(c);
            }
            // Their trader deals only by the lamp, at night.
            if (c.trader?.mindState != null && !c.trader.Dead) c.trader.mindState.wantsToTradeWithColony = Round2Math.NightHour(hour);
            // Said nothing of them: one night in a while, someone of the colony walks out on their own to buy.
            if (!st.told && hour == 22 && st.walkDay != GenDate.DaysPassed && PlayOuts.On)
            {
                st.walkDay = GenDate.DaysPassed;
                if (Rand.Chance(Round2Math.WalkOutChance)) WalkOut(c);
            }
        }

        /// <summary>One with a craving, else a believer of the cup's god, walks out after dark: their silver carried out, a bottle brought to the gate.</summary>
        private static void WalkOut(Camp c)
        {
            var map = c.map;
            var cup = Gods.ByKey("Revel");
            var cupBelievers = CampRound2.Believers(map, cup);
            var who = IncidentWorker_Happening.Colonists(map).Where(p => PlayOuts.CanTakePart(p, map) && !p.DevelopmentalStage.Juvenile())
                .Select(p => (p, craving: RevelGoods.Craving(p).craving, cup: cupBelievers.Contains(p))).Where(x => x.craving || x.cup)
                .OrderByDescending(x => x.craving ? 1 : 0).Select(x => x.p).FirstOrDefault();
            var drink = RevelGoods.Pick(map, true) ?? RevelGoods.Pick(map, false);
            if (who == null || drink == null) return;
            int silver = Round2Math.WalkOutSilver(drink.BaseMarketValue);
            if (CampAnswers.Silver(map) < silver) return;
            var ctx = new DilemmaContext { map = map, faction = c.faction, a = who, stranger = c.trader != null && c.Here.Contains(c.trader) ? c.trader : c.Here.FirstOrDefault() };
            // The silver carried out to their lamp by the one who goes (the play-out); the bottle brought back to the gate by one of theirs.
            new DilemmaEffect_CarryOut { what = "silver", count = silver, to = PlaySpot.Camp, who = PlayWho.A, steward = false, label = "CC_R2Smug_WalkLabel".Translate().Resolve(), hours = new IntRange(21, 23) }
                .Apply(ctx);
            c.StartBring(drink, 1);
            c.Note(c.Text("CC_R2Smug_WalkOut", ctx.stranger, who, silver, drink.label), MessageTypeDefOf.NeutralEvent, Importance.Notable, who, false);
        }

        public override Job DayJob(Camp c, Pawn p)
        {
            // By day they keep out of sight round their fire.
            if (p == c.trader || !Rand.Chance(0.7f)) return null;
            var at = CampRound2.Near(c, p, c.FireCell, 3);
            return at.IsValid ? CampRound2.Work("Rimfolk_R2_CampLieLow", at, new LocalTargetInfo(c.FireCell)) : null;
        }

        public override Job NightJob(Camp c, Pawn p, int hour)
        {
            // Their trader keeps the lamp from dusk until the small hours.
            if (p != c.trader || !Round2Math.NightHour(hour) || (p.needs?.rest?.CurLevelPercentage ?? 1f) < 0.15f) return null;
            var lamp = c.plans.FirstOrDefault(x => x.role == "light")?.Built(c.map, c.faction);
            var near = lamp?.Position ?? c.FireCell;
            var at = CampRound2.Near(c, p, near, 2);
            if (!at.IsValid) return null;
            if (Rand.Chance(0.1f)) c.SayMoment(p, "lamp");
            return CampRound2.Work("Rimfolk_R2_CampKeepLamp", at, lamp != null ? new LocalTargetInfo(lamp) : new LocalTargetInfo(near));
        }

        public override void AddMarks(Camp c, HashSet<string> marks)
        {
            if (StateOf(c)?.told ?? false) marks.Add("told");
        }

        public override string Fill(Camp c, string text) =>
            text?.Replace("{OTHERPEOPLE}", CampRound2.Behind(c)?.ctx?.faction?.Name ?? "CC_R2Smug_TheLaw".Translate().Resolve());

        public override string AskBlocked(Camp c, CampAsk a)
        {
            if (a?.key == BuyAsk && !Round2Math.NightHour(GenLocalDate.HourOfDay(c.map))) return "CC_R2Smug_NightOnly".Translate().Resolve();
            if (a?.key == TellAsk && CampRound2.Behind(c)?.ctx?.faction == null) return "CC_R2Smug_NoLaw".Translate().Resolve();
            return null;
        }
    }

    /// <summary>The smugglers' goods bought openly: what the region is short of, by the world event behind them, brought to the gate by one of them.</summary>
    public class DilemmaEffect_SmugglersGoods : DilemmaEffect
    {
        public override bool ChanceEach => false;

        /// <summary>What they carry for the shortage that brought them, and how much: (def, count).</summary>
        public static (ThingDef def, int count) Goods(Camp c)
        {
            var s = CampRound2.Behind(c);
            ThingDef D(string n) => DefDatabase<ThingDef>.GetNamedSilentFail(n);
            switch (s?.def?.defName)
            {
                case "Council_WS_IronScarce": return (ThingDefOf.Steel, 45);
                case "Rimfolk_WS_MedicineScarce": return (Avail.IsPreIndustrial ? ThingDefOf.MedicineHerbal : ThingDefOf.MedicineIndustrial, 5);
                case "Rimfolk_WS_ClothScarce": return (ThingDefOf.Cloth, 45);
                case "Rimfolk_WS_TimberScarce": return (ThingDefOf.WoodLog, 80);
                case "Rimfolk_WS_DryLaw": return (RevelGoods.Pick(c?.map, true) ?? D("Beer"), 8);
                default: return (D("Beer") ?? ThingDefOf.Cloth, 8);
            }
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var c = CampOnMap.For(ctx);
            var (d, n) = Goods(c);
            if (c != null && d != null) sheet.Goods(1, d, n);
        }

        public override string Unavailable(DilemmaContext ctx)
        {
            var c = CampOnMap.For(ctx);
            if (c == null || Goods(c).def == null) return "CC_CampAsk_NotNow".Translate().ToString();
            return c.CanBring ? null : "CC_CampErrand_No_gate".Translate().ToString();
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampOnMap.For(ctx);
            var (d, n) = Goods(c);
            if (c == null || d == null) return;
            try
            {
                // apart: an answer to the camp; one of them carries the goods over to the gate themselves.
                if (!c.StartBring(d, n)) c.GiveGoods(d.defName, n, d.defName);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 2 camps: the smugglers' goods", e);
            }
        }
    }

    /// <summary>The smugglers told on (their ask): the people whose law it is think better of the colony; the smugglers go that night and aren't remembered to come back.</summary>
    public class DilemmaEffect_TellOnSmugglers : DilemmaEffect
    {
        public override bool ChanceEach => false;
        public int goodwill = 6;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var f = CampRound2.Behind(CampOnMap.For(ctx))?.ctx?.faction;
            if (f != null) sheet.Add(ImpactKind.People, 1, "CC_R2Smug_TellImpact".Translate(f.Name.Named("FACTION")).Resolve(), "CC_R2Smug_TellImpactBrief".Translate().Resolve(), "r2tell");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampOnMap.For(ctx);
            var st = CampWorker_Smugglers.StateOf(c);
            if (c == null || st == null || st.told) return;
            st.told = true;
            var f = CampRound2.Behind(c)?.ctx?.faction;
            if (f != null) WorldSituationWorker.Goodwill(f, goodwill, GoodwillWhy.Helped);
            // A stay that ends like this is not one they come back to (CampStoryMath.Remembered); they go when night falls.
            c.warned = Math.Max(c.warned, 70);
            c.tension = Mathf.Max(c.tension, 60f);
            int hour = GenLocalDate.HourOfDay(c.map);
            int wait = Round2Math.NightHour(hour) ? 1 : (22 - hour + 24) % 24;
            c.untilTick = Mathf.Min(c.untilTick, CampRound2.Now + Math.Max(1, wait) * GenDate.TicksPerHour);
        }
    }

    // ===================== D37. Novices on the road =====================

    public class NoviceState : CampWorkState
    {
        public List<int> gods = new List<int>();
        public List<Pawn> heard = new List<Pawn>();
        public Pawn teller;
        public int tellerUntil = -1;
        public bool knelt;

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref gods, "gods", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.Saving) heard?.RemoveAll(p => p == null || p.Discarded);
            Scribe_Collections.Look(ref heard, "heard", LookMode.Reference);
            Scribe_References.Look(ref teller, "teller");
            Scribe_Values.Look(ref tellerUntil, "tellerUntil", -1);
            Scribe_Values.Look(ref knelt, "knelt");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                gods ??= new List<int>();
                heard ??= new List<Pawn>();
                heard.RemoveAll(p => p == null);
            }
        }
    }

    /// <summary>
    /// Novices on the road (D37): three or four young people of a faith, walking between holy places, stop two nights. The
    /// colony may send someone over to tell them a tale of its gods (their ask, a walk to their fire): at the fire the colonist
    /// tells a piece of one of the colony's gods' stories (GodTales.Tell) and one of them tells one back by their own people's
    /// names, which the colony's book hears. The colony's young who sit at their fire hear a tale from them too. Shown the
    /// colony's god's place (their other ask), they walk to it and kneel there. Their god's name comes only from what they say.
    /// </summary>
    public class CampWorker_Novices : CampWorker
    {
        public const string KneelAsk = "kneel";

        public static NoviceState StateOf(Camp c) => c?.workState as NoviceState;

        public override bool FitsMap(Map map) => Gods.On;

        public override void Started(Camp c)
        {
            if (c.workState is NoviceState) return;
            var gods = Gods.Of(c.faction);
            if (gods.Count == 0) gods = Gods.All.InRandomOrder().Take(1).ToList();
            c.workState = new NoviceState { gods = gods.Select(g => g.id).ToList() };
        }

        protected virtual List<string> TheirKeys(NoviceState st) => st.gods.Select(Gods.ById).Where(g => g != null).Select(g => g.Key).ToList();

        public override void Hourly(Camp c, int hour)
        {
            if (!(c.workState is NoviceState st))
            {
                Started(c);
                st = StateOf(c);
            }
            if (hour < 8 || hour > 22) return;
            var map = c.map;
            var them = c.Here.Where(p => p.RaceProps.Humanlike && !p.Downed && p.Awake()).ToList();
            if (them.Count == 0) return;
            // The one the colony sent: at their fire now, the tales are told both ways.
            if (st.teller != null)
            {
                if (st.teller.Dead || CampRound2.Now > st.tellerUntil) st.teller = null;
                else
                {
                    var novice = them.FirstOrDefault(n => Together.Can(n, st.teller));
                    if (novice != null)
                    {
                        Exchange(c, st, st.teller, novice);
                        st.teller = null;
                    }
                }
            }
            // The colony's young at their fire hear a tale from them, each once.
            foreach (var young in IncidentWorker_Happening.Colonists(map).Where(p => p.DevelopmentalStage.Juvenile() && !st.heard.Contains(p)))
            {
                var novice = them.FirstOrDefault(n => Together.Can(n, young));
                if (novice == null) continue;
                st.heard.Add(young);
                // together: the child sits within talking range of the novice (Together.Can), and the tale is said to them.
                if (GodTales.Tell(novice, young, TheirKeys(st), "Fire", TheirName(st, novice)) || GodTales.WhatOf(novice, young, TheirKeys(st), false, "Fire"))
                    c.SayMoment(novice, "tale");
                break;
            }
        }

        /// <summary>Their own name for the god they tell of (their people's), or null for the teller's own.</summary>
        protected virtual string TheirName(NoviceState st, Pawn novice) => null;

        /// <summary>The colonist tells a piece of one of the colony's gods' stories, and one of them tells one back by their people's names.</summary>
        protected void Exchange(Camp c, NoviceState st, Pawn colonist, Pawn novice)
        {
            var ours = Gods.ColonyGods(c.map).Select(g => g.Key).ToList();
            var held = PawnGods.Held(colonist);
            if (held != null && !ours.Contains(held.Key)) ours.Insert(0, held.Key);
            // together: the colonist walked to their fire (the ask's visit) and stands within talking range of the novice (Together.Can).
            bool told = ours.Count > 0 && (GodTales.Tell(colonist, novice, ours, "Fire") || GodTales.WhatOf(colonist, novice, ours, false, "Fire"));
            bool back = GodTales.Tell(novice, colonist, TheirKeys(st), "Fire", TheirName(st, novice)) || GodTales.WhatOf(novice, colonist, TheirKeys(st), false, "Fire");
            if (!told && !back) return;
            c.SayMoment(novice, "tale");
            c.Warm(colonist, 2);
            c.Note(c.Text("CC_R2Nov_Exchanged", novice, colonist), MessageTypeDefOf.PositiveEvent, Importance.Notable, novice, false);
        }

        public override void AddMarks(Camp c, HashSet<string> marks)
        {
            var st = StateOf(c);
            if (st?.knelt ?? false) marks.Add("knelt");
            if (st != null && st.heard.Count > 0) marks.Add("young");
        }

        public override string AskBlocked(Camp c, CampAsk a)
        {
            if (a?.key == KneelAsk && GodPlace(c) == null) return "CC_R2Nov_NoPlace".Translate().Resolve();
            return null;
        }

        /// <summary>The colony's god's place on this map (a dedicated one of any god of the colony's), nearest their camp.</summary>
        public static Thing GodPlace(Camp c)
        {
            if (c?.map == null) return null;
            try
            {
                return Shrines.Of(c.map, null).Select(s => s.thing).Where(t => t != null && t.Spawned).OrderBy(t => t.Position.DistanceToSquared(c.center)).FirstOrDefault();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 2 camps: the colony's god's place", e);
                return null;
            }
        }

        /// <summary>The colonist the colony sends to their fire to tell a tale (their ask, with its walk over).</summary>
        public static void Sent(Camp c, Pawn a)
        {
            var st = StateOf(c);
            if (st == null || a == null) return;
            st.teller = a;
            st.tellerUntil = CampRound2.Now + GenDate.TicksPerDay;
        }

        /// <summary>Shown the colony's god's place: each of them walks there and kneels a while.</summary>
        public static bool Kneel(Camp c)
        {
            var st = StateOf(c);
            var place = GodPlace(c);
            var pray = DefDatabase<JobDef>.GetNamedSilentFail("Council_CampPray");
            if (st == null || place == null || pray == null) return false;
            int sent = 0;
            foreach (var p in c.Here.Where(x => x.RaceProps.Humanlike && !x.Downed && x.Awake()))
            {
                var at = CampRound2.Near(c, p, place.Position, 3);
                if (!at.IsValid)
                {
                    var map = c.map;
                    if (!CellFinder.TryFindRandomCellNear(place.Position, map, 3, x => x.Standable(map) && x.GetFirstPawn(map) == null && p.CanReach(x, PathEndMode.OnCell, Danger.Some), out at)) continue;
                }
                try
                {
                    p.jobs.StartJob(JobMaker.MakeJob(pray, at, place), JobCondition.InterruptOptional, null, true, true);
                    sent++;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("round 2 camps: the novices kneel", e);
                    break;
                }
            }
            if (sent > 0) st.knelt = true;
            return sent > 0;
        }
    }

    /// <summary>A colonist sent to the novices' fire to tell a tale (the ask's walk over is its visit); the tales are told once they're together.</summary>
    public class DilemmaEffect_NovicesTale : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.a != null && CampOnMap.For(ctx)?.kind?.Worker is CampWorker_Novices)
                sheet.Add(ImpactKind.Other, 1, "CC_R2Nov_TaleImpact".Translate(ctx.a.LabelShort).Resolve(), "CC_R2Nov_TaleImpactBrief".Translate().Resolve(), "r2tale");
        }

        public override void Apply(DilemmaContext ctx) => CampWorker_Novices.Sent(CampOnMap.For(ctx), ctx?.a);
    }

    /// <summary>The novices shown the colony's god's place: they walk there and kneel.</summary>
    public class DilemmaEffect_NovicesKneel : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override string Unavailable(DilemmaContext ctx) =>
            CampWorker_Novices.GodPlace(CampOnMap.For(ctx)) == null ? "CC_R2Nov_NoPlace".Translate().Resolve() : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (CampWorker_Novices.GodPlace(CampOnMap.For(ctx)) != null)
                sheet.Play("CC_R2Nov_KneelImpact".Translate().Resolve(), "CC_R2Nov_KneelImpactBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx) => CampWorker_Novices.Kneel(CampOnMap.For(ctx));
    }

    // ===================== D38. A lord's hunt =====================

    public class LordHuntState : CampWorkState
    {
        public Pawn beast;
        public string beastLabel;
        public bool asked, take, spare, strayRolled, strayed, killed;

        public override void ExposeData()
        {
            Scribe_References.Look(ref beast, "beast");
            Scribe_Values.Look(ref beastLabel, "beastLabel");
            Scribe_Values.Look(ref asked, "asked");
            Scribe_Values.Look(ref take, "take");
            Scribe_Values.Look(ref spare, "spare");
            Scribe_Values.Look(ref strayRolled, "strayRolled");
            Scribe_Values.Look(ref strayed, "strayed");
            Scribe_Values.Look(ref killed, "killed");
        }
    }

    /// <summary>
    /// A lord's hunt (D38): a noble of a people at peace with two or three hunters and their dogs hunt the land some days (the
    /// kind's huntsPerDay, armed), roasting at night. They name one beast (Round2Math.Named): a god's own beast or one of a
    /// god's sacred kind on the land first (Gods/SacredKinds.cs), else the land's rarest (a thrumbo, Vanilla Animals Expanded -
    /// Royal Animals' royal tiger), and ask on their first morning to take it: let, they go after it with the game's own
    /// hunting and leave a gift of silver at the gate; told no, they keep to small game, though while the camp's unease runs
    /// high one of them may go after it anyway, once. A god's beast killed is the worst thing, as it always is (SacredKinds.Killed).
    /// The Empire keeps its own titles and honour: nothing here gives any.
    /// </summary>
    public class CampWorker_LordsHunt : CampWorker
    {
        public const string BeastAsk = "beast";

        public static LordHuntState StateOf(Camp c) => c?.workState as LordHuntState;

        /// <summary>The beast a lord would name on this land, or null.</summary>
        public static Pawn Named(Map map)
        {
            if (map == null) return null;
            var wild = map.mapPawns.AllPawnsSpawned.Where(a => a.Faction == null && a.RaceProps?.Animal == true && !a.Dead && !a.Downed && !a.Position.Fogged(map)).ToList();
            var list = new List<(float value, bool sacred, bool godBeast)>();
            var pool = new List<Pawn>();
            foreach (var a in wild)
            {
                bool sacred = Gods.On && SacredKinds.AnySacred(a.def);
                bool godBeast = Gods.On && SacredKinds.BeastOf(a.def) != null && a.Name != null && !a.Name.Numerical;
                float value = a.MarketValue;
                if (!sacred && !godBeast && value < Round2Math.NamedWorth) continue;
                list.Add((value, sacred, godBeast));
                pool.Add(a);
            }
            int i = Round2Math.Named(list);
            return i < 0 ? null : pool[i];
        }

        public override bool FitsMap(Map map) => Named(map) != null;

        public override void Started(Camp c)
        {
            if (c.workState is LordHuntState) return;
            var b = Named(c.map);
            c.workState = new LordHuntState { beast = b, beastLabel = b?.LabelShort };
        }

        public override void Hourly(Camp c, int hour)
        {
            if (!(c.workState is LordHuntState st))
            {
                Started(c);
                st = StateOf(c);
            }
            var b = st.beast;
            if (b != null && b.Dead && !st.killed)
            {
                st.killed = true;
                var hunter = c.Here.FirstOrDefault(p => p.RaceProps.Humanlike);
                if (hunter != null) c.SayMoment(hunter, "named");
                c.Note(c.Text(st.spare ? "CC_R2Hunt_KilledAnyway" : "CC_R2Hunt_Killed", hunter, null, 0, st.beastLabel), st.spare ? MessageTypeDefOf.NegativeEvent : MessageTypeDefOf.NeutralEvent,
                    Importance.Notable, null, true);
                if (st.spare) c.AddTension(15f);
                return;
            }
            if (b == null || b.Dead || !b.Spawned) return;
            if (!st.asked && c.DaysHere >= 0.3f && hour >= 7 && hour <= 11 && ColonyCouncil.Tell.CanAsk)
            {
                var ask = c.AskByKey(BeastAsk);
                st.asked = true;
                if (ask != null && c.AskBlocked(ask) == null) c.Request(ask);
            }
            if (!st.take && !st.spare && c.asksMade.Contains(BeastAsk)) st.spare = true;
            // Told to spare it, one of them may go after it anyway while the camp's unease runs high: once.
            if (st.spare && !st.strayRolled && c.tension >= Round2Math.StrayTension && hour == 9)
            {
                st.strayRolled = true;
                if (Round2Math.MayStray(c.tension, Rand.Value)) st.strayed = true;
            }
        }

        public override Job DayJob(Camp c, Pawn p)
        {
            var st = StateOf(c);
            var b = st?.beast;
            if (st == null || !(st.take || st.strayed) || b == null || b.Dead || !b.Spawned || c.prey.Contains(b) || !GamePrey.CanHunt(p)) return null;
            if (!p.CanReserveAndReach(b, PathEndMode.Touch, Danger.Some) || c.ColonyInLineOfFire(p.Position, b.Position) || !c.ClearOfColony(b.Position)) return null;
            c.prey.Add(b);
            c.SayMoment(p, "named", 0.6f);
            var job = JobMaker.MakeJob(JobDefOf.Hunt, b);
            job.ignoreDesignations = true;
            job.killIncappedTarget = true;
            return job;
        }

        public override void AddMarks(Camp c, HashSet<string> marks)
        {
            var st = StateOf(c);
            if (st == null) return;
            if (st.killed) marks.Add("killed");
            else if (st.spare) marks.Add("spared");
        }

        public override string Fill(Camp c, string text) => text?.Replace("{BEAST}", StateOf(c)?.beastLabel ?? "CC_R2Hunt_TheBeast".Translate().Resolve());

        public override string AskBlocked(Camp c, CampAsk a)
        {
            var b = StateOf(c)?.beast;
            return a?.key == BeastAsk && (b == null || b.Dead || !b.Spawned) ? "CC_R2Hunt_NoBeast".Translate().Resolve() : null;
        }
    }

    /// <summary>The lord let take the beast (their ask): they go after it, and the lord's gift of silver is carried to the gate.</summary>
    public class DilemmaEffect_LordsHuntLeave : DilemmaEffect
    {
        public override bool ChanceEach => false;

        private static int Gift(Camp c) => Round2Math.HuntGift(CampWorker_LordsHunt.StateOf(c)?.beast?.MarketValue ?? 0f);

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var c = CampOnMap.For(ctx);
            if (CampWorker_LordsHunt.StateOf(c) != null) sheet.Goods(1, ThingDefOf.Silver, Gift(c));
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampOnMap.For(ctx);
            var st = CampWorker_LordsHunt.StateOf(c);
            if (st == null) return;
            st.take = true;
            st.spare = false;
            try
            {
                // apart: an answer to the camp; one of them carries the lord's gift over to the gate.
                if (!c.StartBring(ThingDefOf.Silver, Gift(c))) c.GiveGoods(ThingDefOf.Silver.defName, Gift(c), ThingDefOf.Silver.defName);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 2 camps: the lord's gift", e);
            }
        }
    }

    /// <summary>A colonist's dance on the spot (the dancing sickness's visitors, and those of the colony who join them): turning where they stand, a note now and then.</summary>
    public class JobDriver_R2Dance : JobDriver
    {
        private int stayed;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            var dance = ToilMaker.MakeToil("R2Dance");
            dance.defaultCompleteMode = ToilCompleteMode.Delay;
            dance.defaultDuration = job.count > 0 ? job.count : 1500;
            dance.handlingFacing = true;
            dance.tickIntervalAction = delta =>
            {
                stayed += delta;
                if (stayed / 30 != (stayed - delta) / 30) pawn.Rotation = pawn.Rotation.Rotated(RotationDirection.Clockwise);
                if (stayed / 120 != (stayed - delta) / 120) VisualTouches.Notes(pawn, 1);
            };
            yield return dance;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref stayed, "stayed");
        }
    }
}
