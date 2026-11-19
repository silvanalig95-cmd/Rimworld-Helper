using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // ===================== 60. The master's mark (the Kiln Guild) =====================

    /// <summary>
    /// The master's mark (item 60): when one of the colony makes a masterwork or a legend (the fine-piece hook, Arcs/ArcHooks.cs
    /// Patch_ArcMade), word reaches the Kiln Guild and, a day or more later, a master of the guild comes to the gate to judge it. Let
    /// in, the master walks to the piece where it lies (or to its maker, who holds or wears it) and goes over it for an hour with the
    /// maker standing by; with the second answer the maker learns from it. A true piece gets the guild's mark: a good memory for its
    /// maker, the guild's word on them (a deed their people remember by name, which the colony's renown reads), and with Vanilla
    /// Skills Expanded the quality expertise for a piece made at a bench. A fault found in it ("bad work is a lie told in stone"),
    /// one masterwork in five and one legend in twelve, stings its maker. One of the events' visits (kind "pev:mark"): x the master, a
    /// the maker, place the piece, stake 1 with the lesson.
    /// </summary>
    public static class MastersMark
    {
        public const string Kind = "pev:mark";
        public const string EventDef = "Rimfolk_Evt_MastersMark";

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>The record the event is being fired for (set only while it is).</summary>
        private static PieceRecord firing;

        /// <summary>Something with a quality made by one of the colony (Patch_ArcMade): a masterwork or a legend at a home is noted.</summary>
        public static void Made(Pawn worker, Thing thing)
        {
            if (worker == null || thing == null || worker.Faction == null || !worker.Faction.IsPlayer || !thing.TryGetQuality(out var q)) return;
            if (!PeopleEvents7Math.Judged((int)q)) return;
            var map = worker.MapHeld;
            if (map == null || !Homes.IsHome(map)) return;
            PeopleEvents7Book.Instance?.AddMark(new PieceRecord { thing = thing, maker = worker, map = map, tick = Now, quality = (int)q });
        }

        /// <summary>The piece is still the colony's on its home: lying there, or held, carried or worn by someone there.</summary>
        public static bool Here(PieceRecord r) =>
            r?.thing != null && !r.thing.Destroyed && r.map != null && r.thing.MapHeld == r.map && (r.thing.Spawned || r.thing.ParentHolder is Pawn_EquipmentTracker
                                                                                                     || r.thing.ParentHolder is Pawn_ApparelTracker || r.thing.ParentHolder is Pawn_InventoryTracker
                                                                                                     || r.thing.ParentHolder is Pawn_CarryTracker);

        private static bool MakerHere(PieceRecord r) => r?.maker != null && !r.maker.Dead && r.maker.Spawned && r.maker.Map == r.map && r.maker.Faction == Faction.OfPlayer;

        /// <summary>What the master walks to: the piece where it lies, or whoever holds it.</summary>
        public static LocalTargetInfo At(Thing piece)
        {
            if (piece == null || piece.Destroyed) return LocalTargetInfo.Invalid;
            if (piece.Spawned) return piece;
            var holder = ThingOwnerUtility.GetFirstSpawnedParentThing(piece);
            return holder != null && holder.Spawned ? new LocalTargetInfo(holder) : LocalTargetInfo.Invalid;
        }

        /// <summary>The skill the piece was made by: a work of art is the sculptor's, anything else the crafter's.</summary>
        public static SkillDef SkillOf(Thing piece) => piece?.TryGetComp<CompArt>() != null ? SkillDefOf.Artistic : SkillDefOf.Crafting;

        /// <summary>A record a master could come for at this home now.</summary>
        public static PieceRecord Open(Map map) =>
            PeopleEvents7Book.Instance?.marks.Where(r => r != null && r.map == map && Here(r) && MakerHere(r) && Now - r.tick <= PeopleEvents7Math.MarkKeepDays * GenDate.TicksPerDay)
                .OrderByDescending(r => r.quality).ThenByDescending(r => r.tick).FirstOrDefault();

        /// <summary>The record the event is for: the one the watch fires it for, else the best open one at this home.</summary>
        public static PieceRecord For(Map map) => firing != null && firing.map == map && Here(firing) && MakerHere(firing) ? firing : Open(map);

        /// <summary>
        /// The events' hour: a piece made a day ago or more, while it is fresh, at the colony and its maker at home, is rolled for once:
        /// at a chance (the storytellers' lean on visitors), a guild at peace, not soon after the last master, a master comes.
        /// </summary>
        public static void Watch(int now)
        {
            var b = PeopleEvents7Book.Instance;
            if (b == null) return;
            foreach (var r in b.marks.Where(r => r != null && !r.rolled).ToList())
            {
                if (now - r.tick > PeopleEvents7Math.MarkKeepDays * GenDate.TicksPerDay || !Here(r) || r.maker == null || r.maker.Dead)
                {
                    r.rolled = true;
                    continue;
                }
                if (now - r.tick < GenDate.TicksPerDay || !MakerHere(r) || !Tell.CanAsk || MapFacts.ThreatOn(r.map)) continue;
                r.rolled = true;
                var f = PeoplesD.Friendly(PeoplesMath.Artisans);
                if (f == null || b.lastMark >= 0 && now - b.lastMark < PeopleEvents7Math.MarkGapDays * GenDate.TicksPerDay) continue;
                if (!Rand.Chance(PeopleEvents7Math.MarkChance * StoryDirection.Factor(StoryWaysMath.Visitors))) continue;
                if (Fire(r, f)) b.lastMark = now;
            }
        }

        private static bool Fire(PieceRecord r, Faction f)
        {
            firing = r;
            try
            {
                return PeopleEvents2.Fire(EventDef, r.map, f);
            }
            finally
            {
                firing = null;
            }
        }

        /// <summary>The judging under way for a master, or null.</summary>
        private static Visit Of(Pawn master) =>
            CouncilGameComponent.Instance?.happenings?.visits?.FirstOrDefault(v => v != null && v.kind == Kind && !v.done && v.x == master);

        /// <summary>The answer taken: the master to the piece, the maker beside it; with the play-outs off (or nobody can get there), judged at once.</summary>
        public static void Begin(HappeningContext ctx, bool lesson)
        {
            var map = ctx?.map;
            var t = CouncilGameComponent.Instance?.happenings;
            var x = ctx?.stranger;
            var piece = ctx?.place;
            if (map == null || t == null || x == null || piece == null || piece.Destroyed) return;
            var v = t.AddVisit(Kind, ctx.incident, map, x, ctx.a, null, Now + 8 * GenDate.TicksPerHour);
            v.place = piece;
            v.stake = lesson ? 1 : 0;
            v.skill = ctx.skill;
            var at = At(piece);
            if (PlayOuts.On && at.IsValid && PeoplesE.Up(x, map) && x.CanReach(at, PathEndMode.Touch, Danger.Some))
            {
                Lords.Leave(x);
                Verse.AI.Group.LordMaker.MakeNewLord(x.Faction, new LordJob_VisitOnErrand(x.Faction, at.Cell, 6 * GenDate.TicksPerHour), map, new List<Pawn> { x });
                if (PeopleEvents7.Send(x, PeopleEvents7.JudgeJob, at, PeopleEvents7Math.JudgeTicks))
                {
                    var a = ctx.a;
                    if (a != null && a.Spawned && a.Map == map && !a.Downed && !a.Drafted && !a.InMentalState && at.Thing != a)
                        PeopleEvents7.Send(a, PeopleEvents7.StandByJob, at, PeopleEvents7Math.JudgeTicks);
                    else if (a != null && at.Thing == a && lesson) a.skills?.Learn(ctx.skill ?? SkillDefOf.Crafting, PeopleEvents7Math.JudgeXp, true);
                    return;
                }
            }
            if (lesson && ctx.a != null) ctx.a.skills?.Learn(ctx.skill ?? SkillDefOf.Crafting, PeopleEvents7Math.JudgeXp, true);
            Verdict(v);
        }

        /// <summary>The visit's hour: the master gone or the hours run out before the hour at the piece is done, the verdict is given there and then.</summary>
        public static bool Hour(Visit v, int now)
        {
            if (v.done) return false;
            var x = v.x;
            bool working = x != null && x.Spawned && x.CurJobDef == PeopleEvents7.JudgeJob;
            if (working && now <= v.until) return true;
            Verdict(v);
            return false;
        }

        /// <summary>The master's hour at the piece done: the verdict.</summary>
        public static void Judged(Pawn master)
        {
            var v = Of(master);
            if (v != null) Verdict(v);
        }

        /// <summary>The maker's hour beside the master done: with the second answer, what they learned from it.</summary>
        public static void StoodBy(Pawn maker)
        {
            var v = CouncilGameComponent.Instance?.happenings?.visits?.LastOrDefault(x => x != null && x.kind == Kind && x.a == maker && x.stake == 1);
            if (v != null && Now - (v.until - 8 * GenDate.TicksPerHour) < 10 * GenDate.TicksPerHour) maker.skills?.Learn(v.skill ?? SkillDefOf.Crafting, PeopleEvents7Math.JudgeXp, true);
        }

        /// <summary>
        /// The verdict, once: a fault found at its chance (by the piece's quality), else the guild's mark. Told, said by the master and
        /// answered by the maker where they stand, and the master goes home.
        /// </summary>
        private static void Verdict(Visit v)
        {
            if (v.done) return;
            v.done = true;
            var x = v.x;
            var maker = v.a;
            var piece = v.place;
            var map = v.map;
            var f = x?.Faction ?? PeopleEvents5.PeopleOf(v);
            if (maker == null || maker.Dead || piece == null)
            {
                if (x != null) Arrivals.SendAway(new List<Pawn> { x });
                return;
            }
            int quality = piece.TryGetQuality(out var q) ? (int)q : PeopleEvents7Math.Masterwork;
            bool fault = PeopleEvents7Math.Fault(quality, Rand.Value);
            string label = piece.LabelNoCount;
            var look = maker.Spawned ? new LookTargets(maker) : piece.Spawned ? new LookTargets(piece) : LookTargets.Invalid;
            if (fault)
            {
                PeopleEvents7.Thought(maker, "Rimfolk_Thought_MarkFault");
                PeoplesG.SayLine(x, v.incident, "fault");
                if (maker.Spawned) Speech.Say(maker, TextVariants.Vary("CC_PEv7_MarkFaultBack").Translate().Resolve());
                Tell.Message(NewsKind.People, Importance.Notable, map, "CC_PEv7_MarkFault".Translate(maker.Named("PAWN"), label.Named("THING"), (f?.Name ?? "?").Named("FACTION")).Resolve(),
                    look, MessageTypeDefOf.NegativeEvent);
            }
            else
            {
                PeopleEvents7.Thought(maker, "Rimfolk_Thought_MarkTrue");
                if (f != null) KinMemory.Note(f, "markTrue", maker, label);
                bool expert = (v.skill ?? SkillOf(piece)) == SkillDefOf.Crafting && Expertise.CanGive(maker, PeopleEvents7Math.QualityExpertise)
                              && Expertise.Give(maker, PeopleEvents7Math.QualityExpertise);
                PeoplesG.SayLine(x, v.incident, "true");
                if (maker.Spawned) Speech.Say(maker, TextVariants.Vary("CC_PEv7_MarkTrueBack").Translate().Resolve());
                Tell.Message(NewsKind.People, Importance.Notable, map,
                    (expert ? "CC_PEv7_MarkTrueExpert" : "CC_PEv7_MarkTrue").Translate(maker.Named("PAWN"), label.Named("THING"), (f?.Name ?? "?").Named("FACTION")).Resolve(), look,
                    MessageTypeDefOf.PositiveEvent);
            }
            // Let in to judge, the guild thinks the better of the colony whatever the verdict.
            PeopleEvents7.Goodwill(f, PeopleEvents7Math.MarkGoodwill, GoodwillWhy.Judged);
            if (x != null) Arrivals.SendAway(new List<Pawn> { x });
        }

        /// <summary>Dev mode: the newest fine piece at this home rolled for now (one made the best there is, if none).</summary>
        public static string Now_(Map map)
        {
            var f = PeoplesD.Friendly(PeoplesMath.Artisans);
            var b = PeopleEvents7Book.Instance;
            if (map == null || f == null || b == null) return "no Kiln Guild at peace.";
            var r = Open(map);
            if (r == null)
            {
                var maker = PeoplesD.Best(map, SkillDefOf.Crafting);
                var piece = map.listerThings.AllThings.Where(t => t.Spawned && t.def.HasComp(typeof(CompQuality)) && t.TryGetQuality(out _) && !t.IsForbidden(Faction.OfPlayer)
                                                               && t.def.category == ThingCategory.Item).OrderByDescending(t => t.TryGetQuality(out var qq) ? (int)qq : 0).FirstOrDefault();
                if (maker == null || piece == null) return "no maker and no piece with a quality here.";
                if (!piece.TryGetQuality(out var q) || (int)q < PeopleEvents7Math.Masterwork) piece.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Masterwork, ArtGenerationContext.Colony);
                r = new PieceRecord { thing = piece, maker = maker, map = map, tick = Now - GenDate.TicksPerDay, quality = (int)QualityCategory.Masterwork };
                b.AddMark(r);
            }
            r.rolled = true;
            b.lastMark = Now;
            return Fire(r, f) ? "a master of " + f.Name + " comes to judge " + r.thing.LabelNoCount + "." : "the master could not come now (a question waits, or a threat is on).";
        }
    }

    /// <summary>
    /// A master of {FACTION} ({X}) at the gate: they have heard of the {TALE} {A} made, and would judge it where it is; a true piece
    /// gets the guild's mark, and a fault found in it is said plainly.
    /// </summary>
    public class HappeningWorker_MastersMark : HappeningWorker_Visitors
    {
        private static PieceRecord Pick(Map map) => MastersMark.For(map);

        protected override bool CanHappen(Map map, HappeningExtension ext) => PeoplesD.Friendly(PeoplesMath.Artisans) != null && Pick(map) != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Artisans);
            var r = Pick(ctx.map);
            if (ctx.faction == null || r == null) return false;
            ctx.a = r.maker;
            ctx.place = r.thing;
            ctx.thing = r.thing.def;
            ctx.tale = r.thing.LabelNoCount;
            ctx.variant = r.quality;
            ctx.skill = MastersMark.SkillOf(r.thing);
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_KilnGuild_MasterTrader"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            StorytellerEvents.Skilled(ctx.stranger, ctx.skill, new IntRange(14, 18));
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            tags.Add(ctx.skill == SkillDefOf.Artistic ? "art" : "craft");
            if (ctx.variant >= PeopleEvents7Math.Legendary) tags.Add("legend");
            if (ctx.place != null && !ctx.place.Spawned) tags.Add("held");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option <= 1 && (ctx.place == null || ctx.place.Destroyed || ctx.place.MapHeld != ctx.map || !MastersMark.At(ctx.place).IsValid))
                return "CC_PEv7_PieceGone".Translate(ctx.tale ?? "?").Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "judge" : "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The piece judged where it is (MastersMark.Begin); with <see cref="lesson"/>, the maker learns from the master as they stand by.</summary>
    public class DilemmaEffect_MastersMark : DilemmaEffect
    {
        public bool lesson;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.a == null || hc.place == null) return;
            sheet.Play("CC_PEv7_Impact_Judge".Translate(hc.a.LabelShort, hc.tale ?? "?").Resolve(), "CC_PEv7_Impact_JudgeBrief".Translate().Resolve());
            bool expert = hc.skill == SkillDefOf.Crafting && Expertise.CanGive(hc.a, PeopleEvents7Math.QualityExpertise);
            sheet.Add(ImpactKind.Mood, 1, (expert ? "CC_PEv7_Impact_MarkTrueExpert" : "CC_PEv7_Impact_MarkTrue").Translate(hc.a.LabelShort).Resolve(),
                "CC_PEv7_Impact_MarkTrueBrief".Translate().Resolve(), "pev7marktrue");
            sheet.Add(ImpactKind.Mood, -1, (hc.variant >= PeopleEvents7Math.Legendary ? "CC_PEv7_Impact_MarkFaultLegend" : "CC_PEv7_Impact_MarkFault").Translate(hc.a.LabelShort).Resolve(),
                "CC_PEv7_Impact_MarkFaultBrief".Translate().Resolve(), "pev7markfault");
            if (lesson)
                sheet.Add(ImpactKind.Skill, 1, "CC_PEv7_Impact_JudgeLesson".Translate(hc.a.LabelShort, (hc.skill ?? SkillDefOf.Crafting).label).Resolve(),
                    "CC_PEv7_Impact_JudgeLessonBrief".Translate().Resolve(), "pev7judgelesson");
            if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents7Math.MarkGoodwill);
        }

        public override void Apply(DilemmaContext ctx) => MastersMark.Begin(ctx as HappeningContext, lesson);
    }
}
