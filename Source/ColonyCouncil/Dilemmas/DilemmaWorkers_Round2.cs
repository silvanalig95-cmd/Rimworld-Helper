using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // The content update's second round, its council questions (docs/content-update-proposals-2.md, D24 to D32;
    // Defs/DilemmaDefs/Rimfolk_Round2_Council.xml), in the flavour style of the council's questions. Five of the gods and the
    // faith (how to lay the dead down, the ordeal at the hearth, a prisoner who asks to pray, the feast after a death, a path
    // with the keeper of the colony's book, the work a believer's god won't have) and four of everyday life (the room nobody
    // sleeps in, one of the camp who died, the new age). Each answer plays out on the map with what exists: words in the
    // dead's room, the game's own burying or cremating, a gathering at the hearth, a watch at a prisoner's side, a toast with
    // a cup set, a feast kept later (one of the storyteller's things on their way, Round2Map.LateFeast), a lesson at the
    // keeper's side, work handed over for a season (AnswerShifts), a camp's dead carried home, a gathering at the new thing.

    /// <summary>
    /// A council question of the second round: up to three gods (GodDilemmaContext), a third of the colony ({THIRD_...}), the
    /// dead it is about ({DEAD}), a thing on the map it is about (the dead's bed, the new age's first building) and a word
    /// the worker keeps (the work a god won't have).
    /// </summary>
    public class Round2Context : GodDilemmaContext
    {
        public Pawn c;
        public Pawn dead;
        public Thing place;
        public string note;

        protected override void AddArgs(List<NamedArgument> args)
        {
            base.AddArgs(args);
            args.Add((c ?? a) is Pawn p ? p.Named("THIRD") : "?".Named("THIRD"));
            args.Add((dead?.LabelShort ?? "").Named("DEAD"));
            args.Add((Round2Council.WorkDef(note)?.gerundLabel ?? Round2Council.WorkDef(note)?.labelShort ?? "").Named("WORK"));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref c, "c");
            Scribe_References.Look(ref dead, "dead");
            Scribe_References.Look(ref place, "place");
            Scribe_Values.Look(ref note, "note");
        }
    }

    /// <summary>Shared lookups of the second round's council questions.</summary>
    public static class Round2Council
    {
        /// <summary>A dead colonist's own room, left (the memo Inheritance leaves at the death): other the dead, def the bed's cell.</summary>
        public const string EmptyRoom = "deadroom";

        private static int Now => Find.TickManager.TicksGame;

        public static WorkTypeDef WorkDef(string name) => name.NullOrEmpty() ? null : DefDatabase<WorkTypeDef>.GetNamedSilentFail(name);

        /// <summary>A colonist died who had a room of their own: kept for the question about it (D24), whatever becomes of it.</summary>
        public static void RoomLeft(Pawn dead, Building_Bed bed)
        {
            // apart: a room left by the dead, for a later question.
            if (dead == null || bed == null || !bed.Spawned || bed.Medical) return;
            CouncilGameComponent.Instance?.happenings?.Remember(EmptyRoom, null, null, bed.Position.x + "," + bed.Position.z, dead.LabelShort, false, dead, bed.Map);
        }

        /// <summary>Someone of the colony died lately: from a memory of the death in the colony (the game's own), the dead and how long ago.</summary>
        public static Pawn DiedLately(Map map, float days)
        {
            foreach (var p in map.mapPawns.FreeColonistsSpawned)
            {
                var mems = p.needs?.mood?.thoughts?.memories?.Memories;
                if (mems == null) continue;
                foreach (var m in mems)
                    if (m?.otherPawn != null && m.otherPawn.Dead && m.otherPawn.Faction == Faction.OfPlayer && (m.def == ThoughtDefOf.KnowColonistDied || m.def == ThoughtDefOf.PawnWithGoodOpinionDied)
                        && m.age <= days * GenDate.TicksPerDay)
                        return m.otherPawn;
            }
            return null;
        }

        /// <summary>The way a god's believers give their dead back (D25): Tamiu's keep them whole, Morel's give them to the soil, the Smoulder's and Helios's to the fire.</summary>
        public static string WayOf(God g)
        {
            switch (g?.Key)
            {
                case "Mog": return "whole";
                case "Morel": return "soil";
                case "Smoulder":
                case "Helios": return "fire";
                default: return null;
            }
        }

        public static Building_Grave EmptyGrave(Map map, bool sarcophagus) =>
            map?.listerBuildings.allBuildingsColonist.OfType<Building_Grave>()
                .FirstOrDefault(g => !g.HasCorpse && (g.def.defName.IndexOf("Sarcophagus", StringComparison.Ordinal) >= 0) == sarcophagus && g.TryGetComp<CompAssignableToPawn>() is CompAssignableToPawn c && !c.AssignedPawnsForReading.Any());

        public static Building_WorkTable Crematorium(Map map, out RecipeDef recipe)
        {
            recipe = null;
            if (map == null) return null;
            foreach (var b in map.listerBuildings.allBuildingsColonist.OfType<Building_WorkTable>())
            {
                var r = b.def.AllRecipes?.FirstOrDefault(x => x.defName.IndexOf("Cremate", StringComparison.Ordinal) >= 0 && x.AvailableNow);
                if (r == null || !b.CurrentlyUsableForBills()) continue;
                recipe = r;
                return b;
            }
            return null;
        }

        /// <summary>The place a way needs is here now (a crematorium, an empty sarcophagus; the soil asks for nothing).</summary>
        public static bool Ready(Map map, string way) =>
            way == null || way == "soil" || way == "fire" && Crematorium(map, out _) != null || way == "whole" && EmptyGrave(map, true) != null;

        /// <summary>The colony's oldest grown-up, an elder by preference.</summary>
        public static Pawn Elder(Map map) =>
            map.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger() && p.RaceProps.Humanlike && !p.DevelopmentalStage.Juvenile() && !p.Downed && !p.InMentalState)
                .OrderByDescending(Flavor.HumanAge).FirstOrDefault();
    }

    // ===================================================================================================
    // D24. The room nobody will sleep in
    // ===================================================================================================

    /// <summary>
    /// A dead colonist's own room has stood empty since the death (nobody was given it: the bed has no owner) for three days
    /// or more. A speaks: the keeper of the colony's book, else the one who thought best of the dead. B is the one closest to
    /// the dead with no room of their own (else the closest), who might move in. The bed is the question's place.
    /// </summary>
    public class DilemmaWorker_EmptyRoom : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            var t = CouncilGameComponent.Instance?.happenings;
            if (t == null || !CanHappenNow(map) || map.mapPawns.FreeColonistsSpawnedCount < def.minColonists) return false;
            int now = Find.TickManager.TicksGame;
            foreach (var m in t.Memos(Round2Council.EmptyRoom, Round2EventsMath.EmptyFromDays, map).ToList())
            {
                if ((now - m.tick) / (float)GenDate.TicksPerDay > Round2EventsMath.EmptyToDays || m.other == null || !m.other.Dead)
                {
                    t.Use(m);
                    continue;
                }
                var dead = m.other;
                var parts = (m.def ?? "").Split(',');
                if (parts.Length < 2 || !int.TryParse(parts[0], out int x) || !int.TryParse(parts[1], out int z)) continue;
                var cell = new IntVec3(x, 0, z);
                var bed = cell.InBounds(map) ? cell.GetFirstThing<Building_Bed>(map) : null;
                if (bed == null || bed.Medical || bed.OwnersForReading.Any() || bed.GetRoom()?.Role != RoomRoleDefOf.Bedroom)
                {
                    t.Use(m);
                    continue;
                }
                var comp = CouncilGameComponent.Instance;
                var adults = map.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger() && !p.DevelopmentalStage.Juvenile() && def.roleA.Allows(p, null)).ToList();
                var friend = adults.Where(p => p.relations != null && p.relations.OpinionOf(dead) >= Round2EventsMath.FriendOpinion).OrderByDescending(p => p.relations.OpinionOf(dead)).FirstOrDefault();
                if (friend == null) continue;
                var keeper = GodLore.Keeper(out _);
                var a = keeper != null && keeper.Spawned && keeper.Map == map && adults.Contains(keeper) ? keeper : friend;
                var b = adults.Where(p => p != a && p.ownership != null && p.ownership.OwnedBed == null).OrderByDescending(p => Inheritance.Closeness(comp, p, dead)).FirstOrDefault()
                        ?? adults.Where(p => p != a).OrderByDescending(p => Inheritance.Closeness(comp, p, dead)).FirstOrDefault();
                if (b == null) continue;
                t.Use(m);
                ctx = new Round2Context { def = def, map = map, a = a, b = b, c = friend, dead = dead, place = bed };
                return true;
            }
            return false;
        }

        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            var r = ctx as Round2Context;
            if (r?.a != null && r.a != r.c) tags.Add("keeper");
            if (r?.b?.ownership?.OwnedBed == null) tags.Add("roomless");
        }
    }

    /// <summary>
    /// The dead's room: <c>movein</c> (B claims the bed, as the game assigns beds), <c>clear</c> (the bed taken up and carried
    /// to the stores, the room the colony's to use), <c>keep</c> (left as it was; nobody is given it now).
    /// </summary>
    public class DilemmaEffect_DeadRoom : DilemmaEffect
    {
        public string mode = "keep";

        public override bool ChanceEach => false;

        private static Building_Bed Bed(DilemmaContext ctx) => (ctx as Round2Context)?.place is Building_Bed b && b.Spawned && b.Map == ctx.map ? b : null;

        public override string Unavailable(DilemmaContext ctx)
        {
            var bed = Bed(ctx);
            if (bed == null) return "CC_R2_Room_Gone".Translate().Resolve();
            if (mode == "movein" && bed.OwnersForReading.Any()) return "CC_R2_Room_Taken".Translate().Resolve();
            return null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var bed = Bed(ctx);
            if (bed == null) return;
            sheet.Play(("CC_R2_Impact_Room_" + mode).Translate((ctx.b ?? ctx.a).LabelShort, (ctx as Round2Context)?.dead?.LabelShort ?? "").Resolve(), ("CC_R2_Impact_Room_" + mode + "Brief").Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var bed = Bed(ctx);
            if (bed == null) return;
            switch (mode)
            {
                case "movein":
                    if (ctx.b?.ownership != null && ctx.b.ownership.ClaimBedIfNonMedical(bed))
                        Tell.Message(NewsKind.Colony, Importance.Notable, ctx.map, "CC_R2_Room_MovedIn".Translate(ctx.b.Named("PAWN"), ((ctx as Round2Context)?.dead?.LabelShort ?? "").Named("DEAD")).Resolve(),
                            new LookTargets(ctx.b, bed), MessageTypeDefOf.NeutralEvent);
                    break;
                case "clear":
                    var dm = ctx.map.designationManager;
                    if (dm.DesignationOn(bed) == null)
                        dm.AddDesignation(new Designation(bed, bed.def.Minifiable ? DesignationDefOf.Uninstall : DesignationDefOf.Deconstruct));
                    Tell.Message(NewsKind.Colony, Importance.Notable, ctx.map, "CC_R2_Room_Cleared".Translate(((ctx as Round2Context)?.dead?.LabelShort ?? "").Named("DEAD")).Resolve(), new LookTargets(bed), MessageTypeDefOf.NeutralEvent);
                    break;
            }
        }
    }

    /// <summary>Words said at the question's place (the dead's bed, the new age's first building): a short gathering there.</summary>
    public class DilemmaEffect_WordsAtPlace : DilemmaEffect_Words
    {
        protected override bool Fill(PlayOut p, DilemmaContext ctx)
        {
            var t = (ctx as Round2Context)?.place;
            if (t == null || !t.Spawned || t.Map != ctx.map || !base.Fill(p, ctx)) return false;
            p.place = t;
            p.spots = new List<PlaySpot> { PlaySpot.Place };
            return true;
        }
    }

    /// <summary>A gathering at the question's place (the new age's first building).</summary>
    public class DilemmaEffect_GatheringAtPlace : DilemmaEffect_Gathering
    {
        protected override bool Fill(PlayOut p, DilemmaContext ctx)
        {
            if (!base.Fill(p, ctx)) return false;
            var t = (ctx as Round2Context)?.place;
            if (t != null && t.Spawned && t.Map == ctx.map)
            {
                p.place = t;
                p.spots = new List<PlaySpot> { PlaySpot.Place, PlaySpot.Gather, PlaySpot.Outdoors };
            }
            return true;
        }
    }

    /// <summary>A quiet hour kept at the question's place (the dead's room) by those it's for.</summary>
    public class DilemmaEffect_VigilAtPlace : DilemmaEffect_Vigil
    {
        public DilemmaEffect_VigilAtPlace()
        {
            to = PlaySpot.Place;
            duration = new FloatRange(1f, 1.5f);
            hours = new IntRange(14, 20);
        }

        protected override bool Fill(PlayOut p, DilemmaContext ctx)
        {
            var t = (ctx as Round2Context)?.place;
            if (t == null || !t.Spawned || t.Map != ctx.map || !base.Fill(p, ctx)) return false;
            p.place = t;
            p.to = PlaySpot.Place;
            return true;
        }
    }

    // ===================================================================================================
    // D25. How to lay them down
    // ===================================================================================================

    /// <summary>
    /// One of the colony died holding a god (GraveGods keeps it), their body not yet laid down, and the one closest to them (A)
    /// holds another whose believers lay the dead down differently: whole in a sarcophagus (Tamiu's), in the soil (Morel's),
    /// in the fire (the Smoulder's, Helios's). god1 the dead's, god2 A's. Asked only while what each way needs is here.
    /// </summary>
    public class DilemmaWorker_LayThemDown : DilemmaWorker_Gods
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            var book = GodsAmongBook.Book;
            var comp = CouncilGameComponent.Instance;
            if (book == null || !Worship.On || !CanHappenNow(map) || map.mapPawns.FreeColonistsSpawnedCount < def.minColonists) return false;
            int now = Find.TickManager.TicksGame;
            foreach (var d in book.deadHeld.Where(d => d?.pawn != null && now - d.tick <= GenDate.TicksPerDay * 2).ToList())
            {
                var dead = d.pawn;
                var corpse = dead.Corpse;
                if (corpse == null || !corpse.Spawned || corpse.Map != map) continue;
                var g1 = Gods.ById(d.god);
                var closest = map.mapPawns.FreeColonistsSpawned.Where(p => p != dead && !p.DevelopmentalStage.Juvenile() && def.roleA.Allows(p, null))
                    .OrderByDescending(p => Inheritance.Closeness(comp, p, dead)).FirstOrDefault();
                var g2 = closest == null ? null : PawnGods.Held(closest);
                if (g1 == null || g2 == null || g1 == g2) continue;
                string w1 = Round2Council.WayOf(g1), w2 = Round2Council.WayOf(g2);
                if (w1 == w2 || !Round2Council.Ready(map, w1) || !Round2Council.Ready(map, w2)) continue;
                ctx = new Round2Context { def = def, map = map, a = closest, dead = dead, god1 = g1.id, god2 = g2.id };
                return true;
            }
            return false;
        }

        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            base.AddTags(ctx, tags);
            var r = ctx as Round2Context;
            string w1 = Round2Council.WayOf(r?.God1), w2 = Round2Council.WayOf(r?.God2);
            if (w1 != null) tags.Add("dead:" + w1);
            if (w2 != null) tags.Add("asks:" + w2);
        }
    }

    /// <summary>
    /// The dead laid down: <c>held</c> as the dead held (their god's way), <c>closest</c> as the one closest asks (theirs),
    /// <c>custom</c> as the colony always does (the game's own burying). The fire: a bill at the crematorium for them; whole: an
    /// empty sarcophagus assigned to them; the soil: an empty grave assigned, else the game's own. The eulogy then gives them to
    /// the god of the way chosen (GraveGods).
    /// </summary>
    public class DilemmaEffect_LayDown : DilemmaEffect
    {
        public string which = "custom";

        public override bool ChanceEach => false;

        private God Chosen(Round2Context r) => which == "held" ? r?.God1 : which == "closest" ? r?.God2 : null;

        private string Way(Round2Context r) => which == "custom" ? null : Round2Council.WayOf(Chosen(r));

        public override string Unavailable(DilemmaContext ctx)
        {
            var r = ctx as Round2Context;
            if (r?.dead?.Corpse == null || !r.dead.Corpse.Spawned) return "CC_R2_Lay_Gone".Translate(r?.dead?.LabelShort ?? "").Resolve();
            return Round2Council.Ready(ctx.map, Way(r)) ? null : "CC_R2_Lay_NoPlace".Translate().Resolve();
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var r = ctx as Round2Context;
            if (r?.dead == null) return;
            sheet.Play(("CC_R2_Impact_Lay_" + (Way(r) ?? "custom")).Translate(r.dead.LabelShort).Resolve(), ("CC_R2_Impact_Lay_" + (Way(r) ?? "custom") + "Brief").Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var r = ctx as Round2Context;
            var dead = r?.dead;
            var corpse = dead?.Corpse;
            var map = ctx.map;
            if (corpse == null || !corpse.Spawned || map == null) return;
            // The eulogy gives them to the god of the way chosen.
            var g = Chosen(r);
            var held = GodsAmongBook.Book?.deadHeld.LastOrDefault(x => x != null && x.pawn == dead);
            if (g != null && held != null) held.god = g.id;
            switch (Way(r))
            {
                case "fire":
                    var at = Round2Council.Crematorium(map, out var recipe);
                    if (at == null || !(recipe.MakeNewBill() is Bill_Production bill)) return;
                    bill.ingredientFilter.SetDisallowAll();
                    bill.ingredientFilter.SetAllow(corpse.def, true);
                    bill.repeatMode = BillRepeatModeDefOf.RepeatCount;
                    bill.repeatCount = 1;
                    at.BillStack.AddBill(bill);
                    Tell.Message(NewsKind.Colony, Importance.Notable, map, "CC_R2_Lay_Fire".Translate(dead.LabelShort.Named("DEAD"), at.LabelShort.Named("PLACE")).Resolve(), new LookTargets(at), MessageTypeDefOf.NeutralEvent);
                    break;
                case "whole":
                case "soil":
                    var grave = Round2Council.EmptyGrave(map, Way(r) == "whole");
                    if (grave?.TryGetComp<CompAssignableToPawn>() is CompAssignableToPawn assign)
                    {
                        assign.TryAssignPawn(dead);
                        Tell.Message(NewsKind.Colony, Importance.Notable, map, "CC_R2_Lay_Grave".Translate(dead.LabelShort.Named("DEAD"), grave.LabelShort.Named("PLACE")).Resolve(), new LookTargets(grave), MessageTypeDefOf.NeutralEvent);
                    }
                    break;
            }
        }
    }

    // ===================================================================================================
    // D26. The ordeal
    // ===================================================================================================

    /// <summary>
    /// Two of the colony are at each other's throats (A and B: each thinks badly of the other), and one devout to the
    /// Smoulder (C) asks that the hearth decide between them. god1 the Smoulder.
    /// </summary>
    public class DilemmaWorker_Ordeal : DilemmaWorker_Gods
    {
        public const float DevoutFrom = 40f;

        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            var g = Gods.On && Worship.On ? Gods.ByKey("Smoulder") : null;
            if (g == null || !CanHappenNow(map) || map.mapPawns.FreeColonistsSpawnedCount < def.minColonists) return false;
            var adults = map.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger() && !p.DevelopmentalStage.Juvenile() && !p.Downed && !p.InMentalState).ToList();
            var devout = adults.Where(p => PawnGods.HoldOf(p, g) >= DevoutFrom && !PawnGods.Dreads(p, g)).OrderByDescending(p => PawnGods.HoldOf(p, g)).ToList();
            foreach (var asker in devout)
                foreach (var a in adults.Where(p => p != asker && def.roleA.Allows(p, null)).InRandomOrder())
                {
                    var b = adults.Where(p => p != asker && p != a && a.relations.OpinionOf(p) <= -15 && p.relations.OpinionOf(a) <= -15).OrderBy(p => a.relations.OpinionOf(p) + p.relations.OpinionOf(a)).FirstOrDefault();
                    if (b == null) continue;
                    ctx = new Round2Context { def = def, map = map, a = a, b = b, c = asker, god1 = g.id };
                    return true;
                }
            return false;
        }

        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            base.AddTags(ctx, tags);
            if (Avail.IsPreIndustrial) tags.Add("lowtech");
        }
    }

    /// <summary>
    /// The ordeal at the hearth, before the colony: a gathering at the fire at dusk, and once it is under way (TimedKind.Ordeal)
    /// each of the two carries a hot stone across it (in the older ages; a burn across the hands for one or both within the
    /// budget, GodBurn) or swears with a hand on the furnace's housing (later ages, no burn). Those who hold to the hearth take
    /// the one less burned for the truthful (the book keeps it as the asker's belief, by the colony's name for the god or
    /// unnamed); after an oath they watch the one who swore.
    /// </summary>
    public class DilemmaEffect_Ordeal : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var r = ctx as Round2Context;
            if (r?.a == null || r.b == null || !PlayOuts.On) return;
            bool old = Avail.IsPreIndustrial;
            sheet.Play((old ? "CC_R2_Impact_OrdealStone" : "CC_R2_Impact_OrdealOath").Translate(r.a.LabelShort, r.b.LabelShort).Resolve(), "CC_R2_Impact_OrdealBrief".Translate().Resolve());
            if (old && Harm.On && Harm.SystemOn(HarmKinds.GodBurn.system))
                sheet.Add(ImpactKind.Health, -1, "CC_R2_Impact_OrdealBurn".Translate(r.a.LabelShort, r.b.LabelShort).Resolve(), "CC_R2_Impact_OrdealBurnBrief".Translate().Resolve(), "r2burn");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var r = ctx as Round2Context;
            if (r?.a == null || r.b == null || ctx.map == null || OnMapTimed.Instance == null) return;
            var gathering = new DilemmaEffect_Gathering
            {
                label = "CC_R2_OrdealLabel".Translate().Resolve(),
                spots = new List<PlaySpot> { PlaySpot.Fire, PlaySpot.Gather, PlaySpot.Outdoors },
                hours = new IntRange(17, 22),
                duration = new FloatRange(1f, 1.5f),
                pool = "Council_Pool_PlayWords"
            };
            var play = gathering.Begin(ctx);
            var k = new TimedThing { kind = TimedKind.Ordeal, map = ctx.map, count = play?.id ?? -1, until = Find.TickManager.TicksGame + GenDate.TicksPerDay * 2, on = Avail.IsPreIndustrial };
            k.pawns.Add(r.a);
            k.pawns.Add(r.b);
            k.pawns.Add(r.c ?? r.a);
            k.nights = r.god1;
            OnMapTimed.Instance.Add(k);
        }
    }

    /// <summary>The ordeal's moment (TimedKind.Ordeal): when the gathering at the hearth is under way, the stones (or the oath).</summary>
    public static class OrdealFire
    {
        public static bool Step(TimedThing k, int now)
        {
            if (k.pawns.Count < 3 || k.pawns.Take(2).Any(p => p == null || p.Dead)) return true;
            if (k.count >= 0)
            {
                var play = PlayOutTracker.Instance?.plays.FirstOrDefault(p => p.id == k.count);
                if (play != null && play.state == PlayState.Pending && now <= k.until) return false;
                if (play != null && play.state == PlayState.Running && !k.pawns.Take(2).All(p => p.Spawned && p.Map == k.map && play.invited.Contains(p))) return now > k.until;
            }
            Resolve(k);
            return true;
        }

        private static void Resolve(TimedThing k)
        {
            var b = k.pawns[0];
            var c = k.pawns[1];
            var asker = k.pawns[2];
            var g = Gods.ById(k.nights);
            var map = k.map;
            float burnB = 0f, burnC = 0f;
            if (k.on)
            {
                // The older ages: each carries the hot stone across the hearth.
                if (b.Spawned && Harm.Roll(b, HarmKinds.GodBurn, Round2EventsMath.OrdealBurnChance))
                    burnB = Harm.Injure(b, HurtKind.Burn, HurtWhere.Hand, Round2EventsMath.OrdealBurnLow, Round2EventsMath.OrdealBurnHigh, HarmKinds.GodBurn)?.Severity ?? 0f;
                if (c.Spawned && Harm.Roll(c, HarmKinds.GodBurn, Round2EventsMath.OrdealBurnChance))
                    burnC = Harm.Injure(c, HurtKind.Burn, HurtWhere.Hand, Round2EventsMath.OrdealBurnLow, Round2EventsMath.OrdealBurnHigh, HarmKinds.GodBurn)?.Severity ?? 0f;
            }
            int truthful = Round2EventsMath.Truthful(burnB, burnC, Rand.Bool);
            var right = truthful == 0 ? b : c;
            var wrong = truthful == 0 ? c : b;
            string key = !k.on ? "CC_R2_Ordeal_Oath" : burnB > 0f || burnC > 0f ? "CC_R2_Ordeal_Burned" : "CC_R2_Ordeal_Clean";
            string text = key.Translate(right.Named("TRUE"), wrong.Named("OTHER"), b.Named("B"), c.Named("C")).Resolve();
            if (burnB > 0f) text += " " + "CC_R2_Ordeal_Burn".Translate(b.Named("PAWN")).Resolve();
            if (burnC > 0f) text += " " + "CC_R2_Ordeal_Burn".Translate(c.Named("PAWN")).Resolve();
            Tell.Message(NewsKind.Colony, Importance.Notable, map, text, new LookTargets(new[] { b, c }), burnB > 0f || burnC > 0f ? MessageTypeDefOf.NegativeHealthEvent : MessageTypeDefOf.NeutralEvent);
            // The book keeps what the asker believes of it, by the colony's name for the hearth's god, or unnamed.
            if (asker != null && !asker.Dead && g != null)
            {
                bool named = GodLore.Instance?.PageOf(g) != null;
                string words = (k.on ? "CC_R2_Ordeal_Belief" : "CC_R2_Ordeal_BeliefOath").Translate(right.LabelShort.Named("TRUE"), wrong.LabelShort.Named("OTHER")).Resolve();
                GodLore.Believes(asker, named ? g : null, words);
            }
            Round2Map.Give(right, k.on ? "Rimfolk_R2_OrdealCleared" : "Rimfolk_R2_OathSworn");
            Round2Map.Give(wrong, k.on ? "Rimfolk_R2_OrdealFailed" : "Rimfolk_R2_Watched");
        }
    }

    // ===================================================================================================
    // D27. A prisoner asks to pray
    // ===================================================================================================

    /// <summary>
    /// A prisoner (X) of a people that holds gods, held three days or more, asks to pray. A is the colony's warden (the best
    /// at talk of those who do the warden's work); B, when there is one, one of the colony who holds a god of the prisoner's
    /// people, who may speak up for them. god1 the god they pray to; {FACTION} their people.
    /// </summary>
    public class DilemmaWorker_PrisonerPray : DilemmaWorker_Gods
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!Gods.On || !CanHappenNow(map) || map.mapPawns.FreeColonistsSpawnedCount < def.minColonists) return false;
            var warden = map.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger() && !p.DevelopmentalStage.Juvenile() && !p.WorkTypeIsDisabled(WorkTypeDefOf.Warden) && def.roleA.Allows(p, null))
                .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0).FirstOrDefault();
            if (warden == null) return false;
            foreach (var x in map.mapPawns.PrisonersOfColonySpawned.InRandomOrder())
            {
                if (x.Faction == null || x.Faction.IsPlayer || x.Downed || !(x.RaceProps?.Humanlike ?? false)) continue;
                if ((x.records?.GetValue(RecordDefOf.TimeAsPrisoner) ?? 0f) < 3f * GenDate.TicksPerDay) continue;
                var gods = Gods.Of(x.Faction).Where(g => g.kind != GodKind.Dead).ToList();
                if (gods.Count == 0) continue;
                var g0 = Gods.SwearBy(x) is God sw && gods.Contains(sw) ? sw : gods[0];
                var b = map.mapPawns.FreeColonistsSpawned.Where(p => p != warden && !p.DevelopmentalStage.Juvenile() && gods.Any(g => PawnGods.HoldOf(p, g) >= PawnGodsMath.LeansFrom)).RandomElementWithFallback();
                ctx = new Round2Context { def = def, map = map, a = warden, b = b, stranger = x, faction = x.Faction, god1 = g0.id };
                return true;
            }
            return false;
        }

        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            base.AddTags(ctx, tags);
            if (ctx?.b != null) tags.Add("believer");
        }
    }

    // ===================================================================================================
    // D28. The feast after a death
    // ===================================================================================================

    /// <summary>A god's festival falls tomorrow, within three days of a colonist's death; the priest (or the most devout, A) asks. god1 the festival's god.</summary>
    public class DilemmaWorker_FeastAfterDeath : DilemmaWorker_Gods
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!Worship.On || !CanHappenNow(map) || map.mapPawns.FreeColonistsSpawnedCount < def.minColonists) return false;
            var day = WorshipCouncilMore.FestivalTomorrow(map);
            var g = ColonyCalendar.GodOfDay(day);
            if (g == null || WorshipCouncilMore.EveAnswer(g, map) != null) return false;
            var dead = Round2Council.DiedLately(map, Round2EventsMath.DeathWithinDays);
            if (dead == null) return false;
            var a = GodSpeaker.Of(map, def.roleA, g) ?? GodSpeaker.Of(map, def.roleA);
            if (a == null) return false;
            ctx = new Round2Context { def = def, map = map, a = a, dead = dead, god1 = g.id };
            return true;
        }
    }

    /// <summary>
    /// The feast kept later, once the mourning days are past (one of the storyteller's things on their way, Round2Map.LateFeast):
    /// then the festival's feast is held at the table, the believers' festival kept (PawnGods.Festival).
    /// </summary>
    public class DilemmaEffect_LateFeast : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var g = (ctx as GodDilemmaContext)?.God1;
            if (g == null || !Worship.On) return;
            sheet.Play("CC_R2_Impact_LateFeast".Translate(Festivals.Name(g).Named("FESTIVAL")).Resolve(), "CC_R2_Impact_LateFeastBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            // apart: the council's word for the colony's festival, kept a few days on.
            var r = ctx as Round2Context;
            var g = r?.God1;
            if (g == null || ctx.map == null) return;
            StorytellerEvents.Schedule(Round2Map.LateFeast, null, null, g.id.ToString(), r.dead?.LabelShort ?? "", ctx.a, Round2EventsMath.LateFeastDays, ctx.map);
        }

        /// <summary>Its day: the feast at the table, the festival kept by those of the colony at home.</summary>
        public static void Held(Memo m)
        {
            var map = HappeningTracker.MapFor(m);
            if (map == null || !int.TryParse(m.def, out int id) || !(Gods.ById(id) is God g) || !Worship.On) return;
            var a = GodSpeaker.Of(map, null, g) ?? GodSpeaker.Of(map, null);
            var ctx = new Round2Context { map = map, a = a, god1 = g.id };
            string label = "CC_R2_LateFeastLabel".Translate(Festivals.Name(g).Named("FESTIVAL")).Resolve();
            var feast = new DilemmaEffect_Feast { label = label, hours = new IntRange(17, 22), duration = new FloatRange(2f, 3f) };
            feast.Begin(ctx);
            PawnGods.Festival(map.mapPawns.FreeColonistsSpawned.Where(Worship.Ours).ToList(), g, label);
            Tell.Message(NewsKind.Colony, Importance.Notable, map, "CC_R2_LateFeast".Translate(Festivals.Name(g).Named("FESTIVAL"), (m.text ?? "").Named("DEAD")).Resolve(), LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
        }
    }

    // ===================================================================================================
    // D29. A path: the shrine
    // ===================================================================================================

    /// <summary>
    /// One just come of age (A) who holds a god of their own asks to follow the keeper of the colony's book (B, GodLore.Keeper).
    /// god1 the god A holds most.
    /// </summary>
    public class DilemmaWorker_PathShrine : DilemmaWorker_Gods
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            var comp = CouncilGameComponent.Instance;
            if (comp == null || !Gods.On || !ColonyCouncilMod.Settings.comingOfAge || !CanHappenNow(map)) return false;
            var keeper = GodLore.Keeper(out _);
            if (keeper == null || !keeper.Spawned || keeper.Map != map) return false;
            foreach (var a in comp.legacy.cameOfAge.Where(p => p != null && p.Spawned && p.Map == map && p != keeper).InRandomOrder())
            {
                if (!def.roleA.Allows(a, null)) continue;
                var g = PawnGods.Held(a);
                if (g == null) continue;
                comp.legacy.cameOfAge.Remove(a);
                ctx = new Round2Context { def = def, map = map, a = a, b = keeper, god1 = g.id };
                return true;
            }
            return false;
        }
    }

    /// <summary>The keeper tells the one who follows them a piece of the gods' tales (GodTales), and their belief in their own god grows a little.</summary>
    public class DilemmaEffect_KeeperTales : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.a == null || ctx.b == null || !Gods.On) return;
            sheet.Add(ImpactKind.Other, 1, "CC_R2_Impact_Tales".Translate(ctx.b.LabelShort, ctx.a.LabelShort).Resolve(), "CC_R2_Impact_TalesBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var g = (ctx as GodDilemmaContext)?.God1;
            if (ctx?.a == null || ctx.b == null || g == null) return;
            // together: the lesson at the keeper's side; the tale is told when they are together (GodTales checks it).
            GodTales.Tell(ctx.b, ctx.a, new List<string> { g.Key }, "Walk");
            PawnGods.Told(ctx.a, ctx.b, g);
        }
    }

    // ===================================================================================================
    // D30. The work my god won't have
    // ===================================================================================================

    /// <summary>
    /// A devoted believer (A) is at work their god won't have by its own ways: Hollin's fell no grown tree, Bellwether's hunt no
    /// wild grazer, Tamiu's butcher no cat, Morel's burn no dead. B is the colony's best other hand at that work. god1 A's god;
    /// note the work's kind.
    /// </summary>
    public class DilemmaWorker_GodsWork : DilemmaWorker_Gods
    {
        /// <summary>The work A is at that their god won't have, or null.</summary>
        public static WorkTypeDef Refused(Pawn p, God g)
        {
            var job = p?.CurJob;
            if (job == null || g == null) return null;
            var work = job.workGiverDef?.workType;
            switch (g.Key)
            {
                case "Hollin":
                    return (job.def == JobDefOf.CutPlant || job.def == JobDefOf.HarvestDesignated || job.def == JobDefOf.Harvest) && job.targetA.Thing is Plant tree && tree.def.plant.IsTree && tree.Growth >= 0.9f
                        ? work ?? WorkTypeDefOf.PlantCutting : null;
                case "Bellwether":
                    return job.def == JobDefOf.Hunt && job.targetA.Thing is Pawn prey && prey.Faction == null && prey.RaceProps.Animal && !prey.RaceProps.predator && prey.RaceProps.Eats(FoodTypeFlags.Plant)
                        ? work ?? WorkTypeDefOf.Hunting : null;
                case "Mog":
                    return job.def == JobDefOf.DoBill && job.bill?.recipe != null && job.bill.recipe.defName.IndexOf("Butcher", StringComparison.Ordinal) >= 0
                           && job.GetTargetQueue(TargetIndex.B).Any(t => t.Thing is Corpse c && PawnGods.IsCat(c.InnerPawn))
                        ? work : null;
                case "Morel":
                    return job.def == JobDefOf.DoBill && job.bill?.recipe != null && job.bill.recipe.defName.IndexOf("Cremate", StringComparison.Ordinal) >= 0 ? work : null;
                default:
                    return null;
            }
        }

        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!Gods.On || !Worship.On || !CanHappenNow(map) || map.mapPawns.FreeColonistsSpawnedCount < def.minColonists) return false;
            var adults = map.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger() && !p.DevelopmentalStage.Juvenile() && !p.Downed && !p.InMentalState).ToList();
            foreach (var a in adults)
            {
                if (!def.roleA.Allows(a, null)) continue;
                var g = PawnGods.Held(a);
                if (g == null || PawnGods.HoldOf(a, g) < Round2EventsMath.DevotedFrom) continue;
                var work = Refused(a, g);
                if (work == null || a.workSettings == null || a.workSettings.GetPriority(work) <= 0) continue;
                var b = adults.Where(p => p != a && !p.WorkTypeIsDisabled(work)).OrderByDescending(p => work.relevantSkills.NullOrEmpty() ? 0 : work.relevantSkills.Max(s => p.skills?.GetSkill(s)?.Level ?? 0)).FirstOrDefault();
                if (b == null) continue;
                ctx = new Round2Context { def = def, map = map, a = a, b = b, god1 = g.id, note = work.defName };
                return true;
            }
            return false;
        }

        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            base.AddTags(ctx, tags);
            var g = (ctx as GodDilemmaContext)?.God1;
            if (g != null) tags.Add("godkey:" + g.Key.ToLowerInvariant());
        }
    }

    /// <summary>A excused from the work their god won't have for a season: it goes to B meanwhile, and both are put back after (AnswerShifts).</summary>
    public class DilemmaEffect_Excused : DilemmaEffect
    {
        public override bool ChanceEach => false;

        private static WorkTypeDef Work(DilemmaContext ctx) => Round2Council.WorkDef((ctx as Round2Context)?.note);

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var w = Work(ctx);
            if (w == null || ctx.a == null || ctx.b == null) return;
            sheet.Play("CC_Impact_HandOver".Translate(ctx.a.LabelShort, w.labelShort ?? w.label, ctx.b.LabelShort, ImpactMath.Number(Round2EventsMath.ExcusedDays)).Resolve(),
                "CC_Impact_HandOverBrief".Translate(ctx.b.LabelShort, w.labelShort ?? w.label).Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var w = Work(ctx);
            if (w == null || ctx.a == null || ctx.b == null || AnswerShifts.Instance == null) return;
            if (AnswerShifts.Instance.HandOver(ctx.map, ctx.a, ctx.b, w, Round2EventsMath.ExcusedDays, "CC_R2_ExcusedLabel".Translate(ctx.a.LabelShort.Named("PAWN")).Resolve()))
                Tell.Message(NewsKind.Colony, Importance.Notable, ctx.map, "CC_DlmV_HandedOver".Translate(ctx.a.LabelShort, w.labelShort ?? w.label, ctx.b.LabelShort).Resolve(),
                    new LookTargets(new[] { ctx.a, ctx.b }), MessageTypeDefOf.NeutralEvent);
        }
    }

    /// <summary>A's belief in the question's god moved by the answer: <see cref="how"/> "rite" (their own small rite kept), "shaken" (made to do what it won't have).</summary>
    public class DilemmaEffect_Belief : DilemmaEffect
    {
        public string how = "rite";

        public override bool ChanceEach => false;

        public override void Apply(DilemmaContext ctx)
        {
            // apart: what A believes, moved by the council's answer.
            var g = (ctx as GodDilemmaContext)?.God1;
            if (ctx?.a == null || g == null) return;
            if (how == "rite") PawnGods.Move(ctx.a, g, BeliefEvent.OwnRite, "CC_R2_Why_Pardon".Translate().Resolve());
            else PawnGods.Move(ctx.a, g, BeliefEvent.Unanswered, "CC_R2_Why_Made".Translate().Resolve());
        }
    }

    // ===================================================================================================
    // D31. One of the camp died in the night
    // ===================================================================================================

    /// <summary>Someone of a camp on the colony's land has died there lately (their body there still); the one who speaks for them (X) asks. {DEAD} the dead.</summary>
    public class DilemmaWorker_CampDead : DilemmaWorker_Camp
    {
        public static Pawn Dead(Camp c)
        {
            if (c?.map == null) return null;
            int now = Find.TickManager.TicksGame;
            return c.members.FirstOrDefault(p => p != null && p.Dead && p.RaceProps.Humanlike && p.Corpse is Corpse k && k.Spawned && k.Map == c.map
                                                 && now - k.timeOfDeath <= Round2EventsMath.CampDeathDays * GenDate.TicksPerDay);
        }

        protected override bool Also(Camp c) => Dead(c) != null;

        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!base.TryCast(map, out var c0) || c0 == null) return false;
            var camp = CampOnMap.For(c0);
            var dead = Dead(camp);
            if (dead == null) return false;
            ctx = new Round2Context { def = def, map = map, a = c0.a, b = c0.b, stranger = c0.stranger, faction = c0.faction, count = c0.count, dead = dead };
            return true;
        }
    }

    /// <summary>
    /// The camp's dead: <c>grave</c> (an empty grave of the colony's given to them, the game's own burying, else a grave marked
    /// out by the colony's), <c>home</c> (one of theirs carries the body and the camp leaves early with them), <c>own</c> (the
    /// colony's ground is for its own: one of theirs carries the body off the land, and the camp takes it hard).
    /// </summary>
    public class DilemmaEffect_CampDead : DilemmaEffect
    {
        public string mode = "grave";

        public override bool ChanceEach => false;

        private static Pawn Dead(DilemmaContext ctx) => (ctx as Round2Context)?.dead;

        private static Corpse Body(DilemmaContext ctx) => Dead(ctx)?.Corpse is Corpse c && c.Spawned && c.Map == ctx.map ? c : null;

        public override string Unavailable(DilemmaContext ctx)
        {
            if (Body(ctx) == null) return "CC_R2_Lay_Gone".Translate(Dead(ctx)?.LabelShort ?? "").Resolve();
            if (mode == "grave" && Round2Council.EmptyGrave(ctx.map, false) == null && !GraveSpot(ctx.map).IsValid) return "CC_R2_Camp_NoGrave".Translate().Resolve();
            return null;
        }

        private static IntVec3 GraveSpot(Map map)
        {
            var near = map.listerBuildings.allBuildingsColonist.OfType<Building_Grave>().Select(g => g.Position).DefaultIfEmpty(StorytellerEvents.Center(map)).First();
            foreach (var c in GenRadial.RadialCellsAround(near, 12f, true))
                if (c.InBounds(map) && GenConstruct.CanPlaceBlueprintAt(ThingDefOf.Grave, c, Rot4.North, map).Accepted)
                    return c;
            return IntVec3.Invalid;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var dead = Dead(ctx);
            if (dead == null || Body(ctx) == null) return;
            sheet.Play(("CC_R2_Impact_Camp_" + mode).Translate(dead.LabelShort).Resolve(), ("CC_R2_Impact_Camp_" + mode + "Brief").Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var body = Body(ctx);
            var map = ctx.map;
            if (body == null) return;
            var camp = CampOnMap.For(ctx);
            if (mode == "grave")
            {
                var grave = Round2Council.EmptyGrave(map, false);
                if (grave?.TryGetComp<CompAssignableToPawn>() is CompAssignableToPawn assign)
                {
                    assign.TryAssignPawn(body.InnerPawn);
                    Tell.Message(NewsKind.Colony, Importance.Notable, map, "CC_R2_Camp_Grave".Translate(body.InnerPawn.LabelShort.Named("DEAD"), grave.LabelShort.Named("PLACE")).Resolve(), new LookTargets(grave), MessageTypeDefOf.NeutralEvent);
                    return;
                }
                var spot = GraveSpot(map);
                if (!spot.IsValid) return;
                GenConstruct.PlaceBlueprintForBuild(ThingDefOf.Grave, spot, map, Rot4.North, Faction.OfPlayer, null);
                Tell.Message(NewsKind.Colony, Importance.Notable, map, "CC_R2_Camp_GraveDug".Translate(body.InnerPawn.LabelShort.Named("DEAD")).Resolve(), new LookTargets(spot, map), MessageTypeDefOf.NeutralEvent);
                return;
            }
            // One of theirs carries the body.
            var bearer = camp?.Here.Where(p => !p.Downed && !p.DevelopmentalStage.Juvenile() && p.carryTracker != null && p.carryTracker.CarriedThing == null)
                .OrderBy(p => p.Position.DistanceToSquared(body.Position)).FirstOrDefault();
            if (bearer == null || !bearer.CanReach(body, PathEndMode.ClosestTouch, Danger.Some)) return;
            // They walk to the body, take it up and carry it off the land (the game's own way of carrying a thing away).
            var job = JobMaker.MakeJob(JobDefOf.Steal, body);
            job.count = 1;
            bearer.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            if (mode == "home") camp.Leave("moved");
            Tell.Message(NewsKind.Colony, Importance.Notable, map, ("CC_R2_Camp_Carried_" + mode).Translate(bearer.Named("PAWN"), body.InnerPawn.LabelShort.Named("DEAD")).Resolve(), new LookTargets(bearer), MessageTypeDefOf.NeutralEvent);
        }
    }

    // ===================================================================================================
    // D32. The new age
    // ===================================================================================================

    /// <summary>
    /// The colony has finished its first research of a later age than it began in (the def's own age, by its name's end:
    /// Medieval, Industrial, Spacer). An elder (A) asks. The place is the newest thing the colony has built of that age, if any.
    /// Once for each age (the def's long cooldown).
    /// </summary>
    public class DilemmaWorker_NewAge : DilemmaWorker
    {
        private TechLevel Age
        {
            get
            {
                string n = def?.defName ?? "";
                if (n.EndsWith("Spacer", StringComparison.Ordinal)) return TechLevel.Spacer;
                if (n.EndsWith("Industrial", StringComparison.Ordinal)) return TechLevel.Industrial;
                if (n.EndsWith("Medieval", StringComparison.Ordinal)) return TechLevel.Medieval;
                return TechLevel.Undefined;
            }
        }

        public override bool CanHappenNow(Map map) => Age != TechLevel.Undefined && Avail.ResearchedTech == Age && Avail.PlayerTech < Age && base.CanHappenNow(map);

        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map) || map.mapPawns.FreeColonistsSpawnedCount < def.minColonists) return false;
            var a = Round2Council.Elder(map);
            if (a == null) return false;
            var age = Age;
            var place = map.listerBuildings.allBuildingsColonist.Where(b => b.def.techLevel == age && !(b is Frame) && b.def.building != null && !b.def.building.isNaturalRock && b.def.size.Area >= 1)
                .OrderByDescending(b => b.thingIDNumber).FirstOrDefault();
            ctx = new Round2Context { def = def, map = map, a = a, place = place, thing = place?.def, note = age.ToString() };
            return true;
        }

        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            if ((ctx as Round2Context)?.place != null) tags.Add("built");
        }
    }

    /// <summary>
    /// The new age's day put on the colony's calendar, kept each year from now on as a named day is (ColonyCalendar.AddNamed:
    /// a feast on its date), on today's date.
    /// </summary>
    public class DilemmaEffect_NewAgeDay : DilemmaEffect
    {
        [MustTranslate] public string label;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (!ColonyCalendar.On || ColonyCalendar.For(ctx?.map) == null || label.NullOrEmpty()) return;
            sheet.Add(ImpactKind.Other, 1, "CC_R2_Impact_AgeDay".Translate(label).Resolve(), "CC_R2_Impact_AgeDayBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            // apart: the council's word for the colony's calendar.
            var cal = ColonyCalendar.On ? ColonyCalendar.For(ctx?.map) : null;
            if (cal == null || label.NullOrEmpty()) return;
            cal.AddNamed(label, DayWay.Feast, GenLocalDate.DayOfYear(ctx.map));
        }
    }

    /// <summary>A thought for those of an age (the elders, the young), as the answer says.</summary>
    public class DilemmaEffect_ByAge : DilemmaEffect
    {
        public string thought;
        public float minAge;
        public float maxAge = 9999f;

        public override bool ChanceEach => false;

        private List<Pawn> Who(DilemmaContext ctx) =>
            ctx?.map == null ? new List<Pawn>() : ctx.Colonists.Where(p => p.RaceProps.Humanlike && Flavor.HumanAge(p) >= minAge && Flavor.HumanAge(p) <= maxAge).ToList();

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var def = DefDatabase<ThoughtDef>.GetNamedSilentFail(thought ?? "");
            var who = Who(ctx);
            if (def == null || who.Count == 0) return;
            float mood = def.stages?.FirstOrDefault()?.baseMoodEffect ?? 0f;
            sheet.Add(ImpactKind.Mood, mood >= 0f ? 1 : -1, ("CC_R2_Impact_ByAge_" + (minAge > 0f ? "old" : "young")).Translate(def.stages[0].label, who.Count).Resolve(), def.stages[0].label);
        }

        public override void Apply(DilemmaContext ctx)
        {
            // apart: each of them takes the answer as their own age does.
            foreach (var p in Who(ctx)) Round2Map.Give(p, thought);
        }
    }
}
