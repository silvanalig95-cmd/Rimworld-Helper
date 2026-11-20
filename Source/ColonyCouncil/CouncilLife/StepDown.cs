using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // A seat holder asks to step down (the content update's A3, the player's go of 20 November 2026): when a holder has
    // stood worn down or near breaking five days running (the heart's bands), or has been laid up in a sickbed ten days with
    // the same illness or wound, never while an election is under way, the council is asked. Answers: let them step down
    // (the seat empties and the by-election comes as it does for any empty seat; their strain eases; a deed and a line in
    // the chronicle); give them a deputy for ten days (the next best of the colony, holding no seat, does the seat's daily
    // work: Interplay's acts ask for the one acting, OfficeTracker.Acting; and the holder has the heart's day off at once);
    // or ask them to stay (the devoted trust the council more, their strain stays). The rules are StepDownMath.

    /// <summary>A deputy doing a seat's daily work for its holder for a while: the seat, its holder, the deputy, until when.</summary>
    public class SeatDeputy : IExposable
    {
        public string office;
        public int seat;
        public Pawn holder;
        public Pawn deputy;
        public int until;

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                if (holder != null && holder.Discarded) holder = null;
                if (deputy != null && deputy.Discarded) deputy = null;
            }
            Scribe_Values.Look(ref office, "office");
            Scribe_Values.Look(ref seat, "seat");
            Scribe_References.Look(ref holder, "holder");
            Scribe_References.Look(ref deputy, "deputy");
            Scribe_Values.Look(ref until, "until");
        }
    }

    public partial class OfficeTracker
    {
        /// <summary>
        /// When each seat's holder took it: "thingID:tick" by the seat's key, so a record no longer about the holder reads as
        /// unknown. One per seat at most.
        /// </summary>
        private Dictionary<string, string> seatFrom = new Dictionary<string, string>();

        /// <summary>Deputies doing a seat's work for its holder (CouncilLife/StepDown.cs), one per seat at most.</summary>
        public List<SeatDeputy> deputies = new List<SeatDeputy>();

        /// <summary>Someone takes a seat: noted with the tick, for how long they held it.</summary>
        private void NoteSeated(string key, Pawn p)
        {
            if (key == null || p == null) return;
            seatFrom[key] = p.thingIDNumber + ":" + Find.TickManager.TicksGame;
        }

        /// <summary>How many days <paramref name="p"/> has held the seat of this key, or -1 when the record doesn't say (an older save).</summary>
        private float HeldDays(string key, Pawn p)
        {
            if (key == null || p == null || !seatFrom.TryGetValue(key, out var v) || v == null) return -1f;
            int colon = v.IndexOf(':');
            if (colon < 0 || !int.TryParse(v.Substring(0, colon), out int id) || id != p.thingIDNumber || !int.TryParse(v.Substring(colon + 1), out int tick)) return -1f;
            return StepDownMath.DaysSince(tick, Find.TickManager.TicksGame, GenDate.TicksPerDay);
        }

        /// <summary>The deputy now in for this seat, while their days run and the holder still holds it; null otherwise.</summary>
        public SeatDeputy DeputyOf(string defName, int seat = 0)
        {
            int now = Find.TickManager.TicksGame;
            foreach (var d in deputies)
            {
                if (d == null || d.office != defName || d.seat != seat || !StepDownMath.DeputyOn(d.until, now)) continue;
                var office = DefDatabase<OfficeDef>.GetNamedSilentFail(defName);
                if (office == null || HolderOf(office, seat) != d.holder || d.deputy == null || d.deputy.Dead || !Valid(d.deputy)) continue;
                return d;
            }
            return null;
        }

        /// <summary>
        /// Who does the seat's daily work now: its deputy for their days, else its holder (Interplay.Officer, OfficerOf, Holds,
        /// Officers: the seats' acts and words).
        /// </summary>
        public Pawn Acting(string defName) => DeputyOf(defName)?.deputy ?? HolderOf(defName);

        /// <summary>Whether this holder has a deputy in for any seat now.</summary>
        public bool HasDeputy(Pawn holder) => holder != null && deputies.Any(d => d?.holder == holder && DeputyOf(d.office, d.seat) == d);

        /// <summary>A deputy for a holder's seat, for <see cref="StepDownMath.DeputyDays"/>.</summary>
        public SeatDeputy AddDeputy(OfficeDef office, int seat, Pawn holder, Pawn deputy)
        {
            if (office == null || holder == null || deputy == null) return null;
            deputies.RemoveAll(d => d == null || (d.office == office.defName && d.seat == seat));
            var add = new SeatDeputy
            {
                office = office.defName, seat = seat, holder = holder, deputy = deputy,
                until = Find.TickManager.TicksGame + Mathf.RoundToInt(StepDownMath.DeputyDays * GenDate.TicksPerDay)
            };
            deputies.Add(add);
            return add;
        }

        /// <summary>The council's day: a deputy whose days are over hands the seat's work back (told), one whose seat changed hands goes quietly.</summary>
        private void DeputiesDaily()
        {
            int now = Find.TickManager.TicksGame;
            foreach (var d in deputies.ToList())
            {
                var office = d == null ? null : DefDatabase<OfficeDef>.GetNamedSilentFail(d.office);
                if (office == null || d.deputy == null || d.holder == null || HolderOf(office, d.seat) != d.holder)
                {
                    deputies.Remove(d);
                    continue;
                }
                if (StepDownMath.DeputyOn(d.until, now)) continue;
                deputies.Remove(d);
                if (S.offices && d.holder.Spawned)
                    Tell.Minor(NewsKind.Colony, null, HomeMap, "CC_Life_DeputyOver".Translate(d.deputy.Named("DEPUTY"), d.holder.Named("PAWN"), office.Name.Named("OFFICE")).Resolve(),
                        new LookTargets(d.holder), MessageTypeDefOf.NeutralEvent);
            }
        }

        /// <summary>
        /// The holder of this seat steps down, by the council's leave: the seat is empty (the by-election comes as for any empty
        /// seat, OfficeTracker.Daily), with no mood of a seat lost and no trust lost. True when they held it.
        /// </summary>
        public bool StepDown(OfficeDef office, int seat, Pawn holder)
        {
            if (office == null || holder == null || HolderOf(office, seat) != holder) return false;
            string key = Key(office, seat);
            holders.Remove(key);
            elected.Remove(key);
            seatFrom.Remove(key);
            deputies.RemoveAll(d => d == null || (d.office == office.defName && d.seat == seat));
            SyncHediffs();
            return true;
        }

        /// <summary>The seat a holder holds, and which of its seats (-1 none).</summary>
        public int SeatOf(Pawn p, OfficeDef office)
        {
            if (p == null || office == null) return -1;
            for (int s = 0; s < office.Seats; s++)
                if (HolderOf(office, s) == p) return s;
            return -1;
        }
    }

    /// <summary>
    /// The people and things of colony life's council questions: the seat and which of its seats (A3), the two wishes and the
    /// stuff they share (A4), two names an animal could have (a name for the newborn), and a place (a room). Saved with the
    /// letter. {OFFICE} is the seat, {WISHA} and {WISHB} the wishes, {NAMEA} and {NAMEB} the names.
    /// </summary>
    public class DilemmaContextLife : DilemmaContext
    {
        public OfficeDef office;
        public int seat;
        public int wishA = -1, wishB = -1;
        public string labelA, labelB;
        public string nameA, nameB;
        public string tag;

        public override string SeatName => office?.Name ?? base.SeatName;

        protected override void AddArgs(List<NamedArgument> args)
        {
            base.AddArgs(args);
            args.Add((labelA ?? "").Named("WISHA"));
            args.Add((labelB ?? "").Named("WISHB"));
            args.Add((nameA ?? "").Named("NAMEA"));
            args.Add((nameB ?? "").Named("NAMEB"));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref office, "office");
            Scribe_Values.Look(ref seat, "seat");
            Scribe_Values.Look(ref wishA, "wishA", -1);
            Scribe_Values.Look(ref wishB, "wishB", -1);
            Scribe_Values.Look(ref labelA, "labelA");
            Scribe_Values.Look(ref labelB, "labelB");
            Scribe_Values.Look(ref nameA, "nameA");
            Scribe_Values.Look(ref nameB, "nameB");
            Scribe_Values.Look(ref tag, "tag");
        }
    }

    /// <summary>
    /// A holder at home worn down or near breaking five days running, or laid up ten days in a sickbed (A), with no deputy in
    /// already, while no election is under way; B the one who would stand in (the most able at the seat's work at home who
    /// holds no seat). Tags: worn, breaking or laidup.
    /// </summary>
    public class DilemmaWorker_StepDown : DilemmaWorker
    {
        /// <summary>Dev mode (CouncilLife/CouncilLifeDebug.cs): the next look takes any holder at home, however they are.</summary>
        internal static bool DebugAny;

        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!SeatCalls.On || !CanHappenNow(map)) return false;
            var council = OfficeTracker.For(map);
            if (council == null) return false;
            int now = Find.TickManager.TicksGame;
            var home = map.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger()).ToList();
            if (home.Count < def.minColonists) return false;
            foreach (var office in OfficeTracker.Offices.InRandomOrder())
                for (int seat = 0; seat < office.Seats; seat++)
                {
                    var h = council.HolderOf(office, seat);
                    if (h == null || !home.Contains(h) || h.InMentalState || council.HasDeputy(h)) continue;
                    // Not while a friend's request for a day off for them waits (Wishes/FriendDayOff.cs): one answer at a time.
                    if (FriendDayOff.AskedFor(h)) continue;
                    var heart = Heart.Of(h);
                    float worn = heart == null ? 0f : StepDownMath.DaysSince(heart.wornSince, now, GenDate.TicksPerDay);
                    float laid = Life.LaidUpDays(h);
                    if (!StepDownMath.Asks(worn, laid, council.election != null) && !(DebugAny && council.election == null)) continue;
                    // The one who would stand in: holding no seat, able for its work, up and about.
                    var b = home.Where(p => p != h && OfficeTracker.HeldBy(p) == null && office.CanHold(p) && !p.Downed && !p.InMentalState && !p.DevelopmentalStage.Juvenile())
                        .OrderByDescending(office.Competence).FirstOrDefault();
                    if (b == null) continue;
                    string tag = laid >= StepDownMath.LaidUpDays ? "laidup" : heart != null && heart.breakingSince >= 0 ? "breaking" : "worn";
                    ctx = new DilemmaContextLife { def = def, map = map, a = h, b = b, office = office, seat = seat, tag = tag };
                    return true;
                }
            return false;
        }

        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            if (ctx is DilemmaContextLife l && !l.tag.NullOrEmpty()) tags.Add(l.tag);
        }
    }

    /// <summary>A steps down from the seat (A3's first answer): the seat empties, strain eases, the deed and the chronicle.</summary>
    public class DilemmaEffect_StepDown : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (!(ctx is DilemmaContextLife l) || l.office == null || ctx.a == null) return;
            sheet.Play("CC_Impact_StepDown".Translate(ctx.a.LabelShort, l.office.Name).Resolve(), "CC_Impact_StepDownBrief".Translate(l.office.Name).Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            if (!(ctx is DilemmaContextLife l) || l.office == null || ctx.a == null || ctx.map == null) return;
            var council = OfficeTracker.For(ctx.map);
            if (council == null || !council.StepDown(l.office, l.seat, ctx.a)) return;
            var comp = CouncilGameComponent.Instance;
            var a = ctx.a;
            if (Heart.On) HeartLogic.Relieve(a, StepDownMath.Relief, StrainRelief.Council);
            comp?.journals.Write(a, "Rimfolk_Journal_SteppedDown", null, l.office.Name.Named("OFFICE"));
            comp?.AddChronicle(new ChronicleEntry(ChronicleKind.Council, "CC_Life_SteppedDownChronicle".Translate(a.Named("PAWN"), l.office.Name.Named("OFFICE")).Resolve(), a));
            History.Add(LifeArcs.KindStepped, new List<HistoryActor> { new HistoryActor(Refs.Of(a), HistoryRole.About) }, Refs.Colony(ctx.map), HistoryMath.Everyday, null, null,
                new List<string> { l.office.defName });
            Deeds.Record("Rimfolk_Deed_SteppedDown", a, null, -1f, l.office.Name);
        }
    }

    /// <summary>B does the seat's daily work for ten days in A's place (A3's second answer): the deputy noted on the council.</summary>
    public class DilemmaEffect_Deputy : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (!(ctx is DilemmaContextLife l) || l.office == null || ctx.a == null || ctx.b == null) return;
            sheet.Play("CC_Impact_Deputy".Translate(ctx.b.LabelShort, l.office.Name, ImpactMath.Number(StepDownMath.DeputyDays)).Resolve(),
                "CC_Impact_DeputyBrief".Translate(ctx.b.LabelShort).Resolve());
        }

        public override string Unavailable(DilemmaContext ctx) =>
            ctx?.b == null || ctx.b.Dead || !ctx.b.IsColonist ? "CC_Life_NoDeputy".Translate().Resolve() : null;

        public override void Apply(DilemmaContext ctx)
        {
            if (!(ctx is DilemmaContextLife l) || l.office == null || ctx.a == null || ctx.b == null || ctx.map == null) return;
            var council = OfficeTracker.For(ctx.map);
            if (council == null || council.HolderOf(l.office, l.seat) != ctx.a) return;
            if (council.AddDeputy(l.office, l.seat, ctx.a, ctx.b) == null) return;
            Tell.Message(NewsKind.Colony, Importance.Notable, ctx.map,
                "CC_Life_DeputyIn".Translate(ctx.b.Named("DEPUTY"), ctx.a.Named("PAWN"), l.office.Name.Named("OFFICE"), ((int)StepDownMath.DeputyDays).Named("DAYS")).Resolve(),
                new LookTargets(ctx.b), MessageTypeDefOf.NeutralEvent);
        }
    }

    /// <summary>
    /// The heart's own day off for the target (A unless given), at once, as the player's button gives it (Heart/HeartTimeOff.cs);
    /// when the heart can't give one now (off, or one too lately), the calendar's rest timetable for the rest of today instead.
    /// </summary>
    public class DilemmaEffect_HeartDayOff : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.map == null) return;
            var who = Reached(ctx).Where(p => PlayOuts.OurPerson(p) && p.Spawned && p.Map == ctx.map).ToList();
            if (who.Count == 0) return;
            string w = WhoOf(ctx, who);
            sheet.Play("CC_Impact_HeartDayOff".Translate(w).Resolve(), "CC_Impact_RestBrief".Translate(w).Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            if (ctx?.map == null) return;
            foreach (var p in Targets(ctx).Where(p => PlayOuts.OurPerson(p) && p.Spawned && p.Map == ctx.map).ToList())
            {
                try
                {
                    if (HeartTimeOff.DayOffOn && HeartTimeOff.GiveDayOff(p)) continue;
                    var cal = ColonyCalendar.For(ctx.map);
                    var given = cal?.RestFor(ctx.map, new List<Pawn> { p }, false);
                    if (given != null && given.Count > 0)
                        Tell.Message(NewsKind.People, Importance.Notable, null, "CC_Play_Rest".Translate(p.LabelShort, 1).Resolve(), new LookTargets(p), MessageTypeDefOf.NeutralEvent);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("heart day off from an answer", e);
                }
            }
        }
    }

    /// <summary>Those of the colony who trust the council most (devoted) trust it a little more.</summary>
    public class DilemmaEffect_TrustDevoted : DilemmaEffect
    {
        public float amount = StepDownMath.DevotedTrust;

        private static List<Pawn> Devoted(DilemmaContext ctx)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null) return new List<Pawn>();
            return ctx.Colonists.Where(p => p != ctx.a && comp.standings.For(p, false)?.Level == TrustLevel.Devoted).ToList();
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.map == null) return;
            var who = Devoted(ctx);
            if (who.Count > 0) sheet.Trust("CC_Impact_TheDevoted".Translate(who.Count).Resolve(), KeyOf(who), amount, who.Count > 1);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null || ctx?.map == null) return;
            string why = ctx.def != null ? "CC_Log_Dilemma".Translate(ctx.def.label).ToString() : ctx.why;
            foreach (var p in Devoted(ctx)) comp.standings.Change(p, amount, why);
        }
    }

    /// <summary>Small look-ups colony life's questions share.</summary>
    public static class Life
    {
        /// <summary>
        /// How long someone has been laid up: in a bed now, resting by the doctor's word, with the illness or wound that has
        /// kept them there longest (the game's own age of it), in days; 0 when they aren't laid up.
        /// </summary>
        public static float LaidUpDays(Pawn p)
        {
            if (p?.health?.hediffSet == null || !p.InBed() || !HealthAIUtility.ShouldSeekMedicalRest(p)) return 0f;
            int oldest = 0;
            foreach (var h in p.health.hediffSet.hediffs)
            {
                if (h == null || !h.Visible || h.IsPermanent()) continue;
                bool bad = h is Hediff_Injury || h.def.makesSickThought || (h.def.tendable && h.def.isBad);
                if (bad && h.ageTicks > oldest) oldest = h.ageTicks;
            }
            return oldest / (float)GenDate.TicksPerDay;
        }

        /// <summary>Days someone has been one of the colony (the game's record of it).</summary>
        public static float DaysHere(Pawn p) => ArcUtil.DaysHere(p);
    }
}
