using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // The content round's third part, its camps (docs/content-round-3-camps-world.md, R3-C1 to R3-C6; Defs/CampDefs/
    // Rimfolk_CampKinds_Round3.xml): rain-callers, dyers, witch-finders, rag-and-bone men, gamblers and dog-breeders. Each is a
    // camp kind with ways of its own (Camps/CampWorker.cs), its state kept with the camp (a CampWorkState of a few values and
    // single references, no collection); the pure rules are World/Variety/Round3WorldMath.cs. What they buy, are given or
    // take goes into their store like any camp's, and is shared out among their packs when they leave.

    /// <summary>What the round's camps share: their kinds, their people's age, who of them is up, and someone of the colony at their fire.</summary>
    public static class CampRound3
    {
        public const string RainCallers = "Rimfolk_Camp_RainCallers", Dyers = "Rimfolk_Camp_Dyers", WitchFinders = "Rimfolk_Camp_WitchFinders",
            RagPickers = "Rimfolk_Camp_RagPickers", Gamblers = "Rimfolk_Camp_Gamblers", Kennel = "Rimfolk_Camp_Kennel";

        public static readonly string[] Kinds = { RainCallers, Dyers, WitchFinders, RagPickers, Gamblers, Kennel };

        public static int Now => Find.TickManager.TicksGame;

        /// <summary>Their people's age as lines are tagged (tribal, medieval, industrial, spacer).</summary>
        public static string AgeOf(Camp c) => c?.faction == null ? "medieval" : Ages.Tag(Ages.Of(c.faction));

        /// <summary>Those of them up and about at the camp: awake, not downed or out of their minds.</summary>
        public static List<Pawn> Up(Camp c) =>
            c == null ? new List<Pawn>() : c.Here.Where(p => p.RaceProps.Humanlike && !p.Downed && p.Awake() && !p.InMentalState && !p.DevelopmentalStage.Juvenile()).ToList();

        /// <summary>One of them within talking range of this one of the colony (Together.Can), or null.</summary>
        public static Pawn With(Camp c, Pawn colonist) => colonist == null || colonist.Dead ? null : Up(c).FirstOrDefault(p => Together.Can(p, colonist));

        /// <summary>A free cell near their fire this one of them can reach and stand on, clear of the colony.</summary>
        public static IntVec3 ByFire(Camp c, Pawn p, int radius = 3) => CampRound2.Near(c, p, c.FireCell, radius);

        /// <summary>Where someone of the colony sent to sit with them stands this hour (an ask's visit to their camp).</summary>
        public enum Sitting
        {
            /// <summary>Not with them yet.</summary>
            Waiting,
            /// <summary>With them now, not long enough yet (or just sat down).</summary>
            Sitting,
            /// <summary>Long enough with them, or they sat together and have parted since: what it comes to is settled now.</summary>
            Done
        }

        /// <summary>
        /// The sitting this hour: the colonist and one of them within talking range (Together.Can) starts it (<paramref name="withFrom"/>);
        /// <paramref name="hours"/> together, or the colonist gone again after sitting down, ends it. <paramref name="them"/>: the one of
        /// them they are with (else the first of them up).
        /// </summary>
        public static Sitting Sit(Camp c, Pawn colonist, ref int withFrom, float hours, out Pawn them)
        {
            them = With(c, colonist);
            if (withFrom < 0)
            {
                if (them == null) return Sitting.Waiting;
                withFrom = Now;
                return Sitting.Sitting;
            }
            if (them == null)
            {
                them = Up(c).FirstOrDefault();
                return Sitting.Done;
            }
            return Now - withFrom >= hours * GenDate.TicksPerHour ? Sitting.Done : Sitting.Sitting;
        }

        /// <summary>Silver paid for something that never came about, brought back to the colony's gate by one of them.</summary>
        public static void Refund(Camp c, int silver, string key, Pawn colonist)
        {
            if (c == null || silver <= 0) return;
            try
            {
                // apart: an answer to the camp; one of them carries the silver back over to the gate.
                if (!c.StartBring(ThingDefOf.Silver, silver)) c.GiveGoods(ThingDefOf.Silver.defName, silver, ThingDefOf.Silver.defName);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 3 camps: silver back", e);
                return;
            }
            c.Note(c.Text(key, null, colonist, silver), MessageTypeDefOf.NeutralEvent, Importance.Notable, null, false);
        }
    }

    public partial class Camp
    {
        /// <summary>A beast of their own herd they could sell (not a dog at someone's heels), as HandOverBeast would give.</summary>
        public bool R3HasBeastToSell => herd.Any(x => x != null && !x.Dead && x.Spawned && !x.Downed && x.Map == map && GroupOf(x)?.follow != true);
    }

    // ===================== R3-C1. Rain-callers =====================

    public class RainState : CampWorkState
    {
        /// <summary>Asked about the fee; promised it; told no; a rite kept; the rain come; the fee carried out; something sent up waiting to go off.</summary>
        public bool asked, promised, refused, rite, rained, paid, launch;
        public int fee, riteDay = -1, launchTick = -1;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref asked, "asked");
            Scribe_Values.Look(ref promised, "promised");
            Scribe_Values.Look(ref refused, "refused");
            Scribe_Values.Look(ref rite, "rite");
            Scribe_Values.Look(ref rained, "rained");
            Scribe_Values.Look(ref paid, "paid");
            Scribe_Values.Look(ref launch, "launch");
            Scribe_Values.Look(ref fee, "fee");
            Scribe_Values.Look(ref riteDay, "riteDay", -1);
            Scribe_Values.Look(ref launchTick, "launchTick", -1);
        }
    }

    /// <summary>
    /// Rain-callers (R3-C1): while a drought or a hungry year runs in the region (else now and then in summer, while it isn't
    /// raining), three to five camp by the colony and keep their rite each evening: tribal and medieval callers dance round
    /// their fire, industrial and spacer ones work at their launcher or mast by it and send something up into the sky (smoke
    /// and a flash over their camp). On their first morning they ask the colony to promise a fee if the rain comes. The rain
    /// is the game's own weather, read and never made: when it rains on the colony's land after their first rite, they cheer
    /// at their fire; promised, the fee is carried out to them by someone of the colony; either way they pack up and go a few
    /// hours later. No rain before their days run out, and they go saying the sky wasn't ready. The colony's believers of the
    /// sky's god may say at the first rite that the sky isn't theirs to call (no god named).
    /// </summary>
    public class CampWorker_R3RainCallers : CampWorker
    {
        public const string FeeAsk = "fee";

        public static RainState StateOf(Camp c) => c?.workState as RainState;

        /// <summary>The game's weather on the colony's land is rain now.</summary>
        public static bool Raining(Map map) => Round3WorldMath.Rained(map?.weatherManager?.curWeather?.rainRate ?? 0f, true);

        public override bool FitsMap(Map map) => map != null && !Raining(map) && (CampWords.Situation(def) != null || GenLocalDate.Season(map) == Season.Summer);

        public override void Started(Camp c)
        {
            if (!(c.workState is RainState)) c.workState = new RainState { fee = Round3WorldMath.RainFee(LivingEvents.WealthFactor(c.map)) };
        }

        public override void Hourly(Camp c, int hour)
        {
            if (!(c.workState is RainState st))
            {
                Started(c);
                st = StateOf(c);
            }
            // The fee, asked on their first morning.
            if (!st.asked && c.DaysHere >= 0.3f && hour >= 7 && hour <= 11 && ColonyCouncil.Tell.CanAsk)
            {
                var ask = c.AskByKey(FeeAsk);
                st.asked = true;
                if (ask != null && c.AskBlocked(ask) == null) c.Request(ask);
            }
            if (!st.promised && !st.refused && c.asksMade.Contains(FeeAsk)) st.refused = true;
            // Something sent up at the rite goes off over their camp.
            if (st.launch && CampRound3.Now >= st.launchTick) Launch(c, st);
            if (!st.rained && Round3WorldMath.RiteHour(hour) && st.riteDay != GenDate.DaysPassed) Rite(c, st);
            if (!st.rained && Round3WorldMath.Rained(c.map?.weatherManager?.curWeather?.rainRate ?? 0f, st.rite)) Rain(c, st);
        }

        /// <summary>The evening's rite: the dancers round the fire, or the one at the launcher or the mast.</summary>
        private static void Rite(Camp c, RainState st)
        {
            var them = CampRound3.Up(c).Where(p => p != c.trader).ToList();
            if (them.Count == 0) return;
            st.riteDay = GenDate.DaysPassed;
            bool first = !st.rite;
            bool up = Round3WorldMath.SendsUp(CampRound3.AgeOf(c));
            int sent = 0;
            try
            {
                if (up)
                {
                    var p = them.OrderByDescending(x => x == c.leader ? 1 : 0).First();
                    var at = CampRound3.ByFire(c, p);
                    var job = CampRound2.Work("Rimfolk_R3_CampSkyWork", at, new LocalTargetInfo(c.FireCell));
                    if (job != null)
                    {
                        p.jobs.StartJob(job, JobCondition.InterruptOptional, null, true, true);
                        c.SayMoment(p, "rite");
                        st.launch = true;
                        st.launchTick = CampRound3.Now + GenDate.TicksPerHour;
                        sent++;
                    }
                }
                else
                {
                    var dance = DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_R2_DanceInYard");
                    foreach (var p in them)
                    {
                        var at = CampRound3.ByFire(c, p, 4);
                        if (dance == null || !at.IsValid) continue;
                        var job = JobMaker.MakeJob(dance, at);
                        job.count = GenDate.TicksPerHour;
                        p.jobs.StartJob(job, JobCondition.InterruptOptional, null, true, true);
                        if (sent == 0) c.SayMoment(p, "rite");
                        sent++;
                    }
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 3 camps: the rain-callers' rite", e);
            }
            if (sent == 0) return;
            st.rite = true;
            if (!first) return;
            c.Note(c.Text(up ? "CC_R3Rain_RiteUp" : "CC_R3Rain_Rite"), MessageTypeDefOf.NeutralEvent, Importance.Notable, them[0], false);
            // The colony's believers of the sky's god have their say, the god unnamed.
            var sky = CampRound2.Believers(c.map, Gods.ByKey("Cirrus")).FirstOrDefault(x => x.Awake() && !x.Downed && !x.InMentalState);
            if (sky != null) Speech.Say(sky, "CC_R3Rain_SkyLine".Translate().Resolve());
        }

        /// <summary>What the launcher or the mast sent up goes off over their camp: smoke and a flash, seen from the colony.</summary>
        private static void Launch(Camp c, RainState st)
        {
            st.launch = false;
            var map = c.map;
            if (map == null) return;
            var at = c.FireCell.ToVector3Shifted() + new Vector3(Rand.Range(-2f, 2f), 0f, Rand.Range(3f, 6f));
            FleckMaker.ThrowSmoke(at, map, 3f);
            FleckMaker.ThrowLightningGlow(at, map, 2.5f);
            FleckMaker.ThrowMicroSparks(at, map);
            var p = CampRound3.Up(c).FirstOrDefault();
            if (p != null) c.SayMoment(p, "launch");
        }

        /// <summary>It rains after their rite: they cheer, the promised fee is carried out to them, and they go a few hours later.</summary>
        public static void Rain(Camp c, RainState st)
        {
            st.rained = true;
            var them = CampRound3.Up(c);
            if (them.Count > 0) c.SayMoment(them.RandomElement(), "rain");
            c.untilTick = Mathf.Min(c.untilTick, CampRound3.Now + Round3WorldMath.GoAfterRainHours * GenDate.TicksPerHour);
            if (!st.promised)
            {
                c.Note(c.Text("CC_R3Rain_RainedUnpaid"), MessageTypeDefOf.NeutralEvent, Importance.Notable, them.FirstOrDefault(), true);
                return;
            }
            var map = c.map;
            var who = IncidentWorker_Happening.Colonists(map).Where(p => PlayOuts.CanTakePart(p, map) && !p.DevelopmentalStage.Juvenile()).RandomElementWithFallback();
            if (who == null || CampAnswers.Silver(map) < st.fee)
            {
                c.AddTension(10f);
                c.Note(c.Text("CC_R3Rain_RainedNoFee", null, null, st.fee), MessageTypeDefOf.NegativeEvent, Importance.Notable, them.FirstOrDefault(), true);
                return;
            }
            var ctx = new DilemmaContext { map = map, faction = c.faction, a = who, stranger = c.leader != null && c.Here.Contains(c.leader) ? c.leader : them.FirstOrDefault() };
            try
            {
                // The fee carried out to their camp by the one who goes (the play-out).
                new DilemmaEffect_CarryOut { what = "silver", count = st.fee, to = PlaySpot.Camp, who = PlayWho.A, steward = false, label = "CC_R3Rain_FeeLabel".Translate().Resolve() }.Apply(ctx);
                st.paid = true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 3 camps: the rain-callers' fee", e);
            }
            c.Note(c.Text("CC_R3Rain_RainedPaid", ctx.stranger, who, st.fee), MessageTypeDefOf.PositiveEvent, Importance.Notable, who, true);
        }

        public override Job DayJob(Camp c, Pawn p)
        {
            // By day the senders-up see to their gear by the fire now and then; the dancers rest.
            if (!Round3WorldMath.SendsUp(CampRound3.AgeOf(c)) || !Rand.Chance(0.25f)) return null;
            var at = CampRound3.ByFire(c, p);
            return at.IsValid ? CampRound2.Work("Rimfolk_R3_CampSkyWork", at, new LocalTargetInfo(c.FireCell)) : null;
        }

        public override void AddMarks(Camp c, HashSet<string> marks)
        {
            var st = StateOf(c);
            if (st == null) return;
            if (st.rained) marks.Add(st.paid ? "paid" : "rained");
            else if (st.rite) marks.Add("dry");
        }

        public override string Fill(Camp c, string text) => text?.Replace("{SILVER}", (StateOf(c)?.fee ?? Round3WorldMath.RainFee(LivingEvents.WealthFactor(c?.map))).ToString());

        public override string AskBlocked(Camp c, CampAsk a)
        {
            var st = StateOf(c);
            return a?.key == FeeAsk && (st == null || st.rained) ? "CC_R3Rain_TooLate".Translate().Resolve() : null;
        }
    }

    /// <summary>The rain-callers promised their fee if the rain comes (their ask): nothing is paid now.</summary>
    public class DilemmaEffect_R3RainFee : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var st = CampWorker_R3RainCallers.StateOf(CampOnMap.For(ctx));
            if (st != null) sheet.Add(ImpactKind.Goods, -1, "CC_R3Rain_FeeImpact".Translate(st.fee).Resolve(), "CC_R3Rain_FeeImpactBrief".Translate().Resolve(), "r3fee");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var st = CampWorker_R3RainCallers.StateOf(CampOnMap.For(ctx));
            if (st == null) return;
            st.promised = true;
            st.refused = false;
        }
    }

    // ===================== R3-C2. Dyers =====================

    public class DyeState : CampWorkState
    {
        /// <summary>The camp's colour (Round3WorldMath.Palette's key); the one sent over to have their clothes dyed, since when they sat with the dyers.</summary>
        public string colour;
        public Pawn sent;
        public int sentUntil = -1, withFrom = -1, dyed, fee;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref colour, "colour");
            Scribe_Values.Look(ref fee, "fee");
            Scribe_References.Look(ref sent, "sent");
            Scribe_Values.Look(ref sentUntil, "sentUntil", -1);
            Scribe_Values.Look(ref withFrom, "withFrom", -1);
            Scribe_Values.Look(ref dyed, "dyed");
        }
    }

    /// <summary>
    /// Dyers (R3-C2): a family of dyers camps by the colony in the warm months with their vats by the fire and their dyed cloth
    /// at their stall; they stir the vats by day. The colony may send someone over to have their clothes dyed (their ask, a
    /// walk to their camp): once that one has sat with the dyers an hour, their outer coat or shirt takes the camp's colour, of
    /// their people's age (woad, madder or weld before the mills, aniline colours after), the way the mourning colours go on
    /// (CompColorable), and stays so.
    /// </summary>
    public class CampWorker_R3Dyers : CampWorker
    {
        public const string DyeAsk = "dye";

        public static DyeState StateOf(Camp c) => c?.workState as DyeState;

        public override void Started(Camp c)
        {
            if (!(c.workState is DyeState)) c.workState = new DyeState { colour = Round3WorldMath.ColourOf(CampRound3.AgeOf(c), Rand.Value) };
        }

        public static Color ColourOf(DyeState st)
        {
            var rgb = Round3WorldMath.Rgb(st?.colour);
            return new Color(rgb[0], rgb[1], rgb[2]);
        }

        /// <summary>The piece that takes the dye: the outermost worn on the body that can be coloured.</summary>
        public static Apparel Outer(Pawn p) =>
            p?.apparel?.WornApparel.Where(ap => ap.TryGetComp<CompColorable>() != null && ap.def.apparel?.bodyPartGroups != null && ap.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Torso))
                .OrderByDescending(ap => ap.def.apparel.LastLayer.drawOrder).FirstOrDefault();

        public override void Hourly(Camp c, int hour)
        {
            if (!(c.workState is DyeState st))
            {
                Started(c);
                st = StateOf(c);
            }
            if (st.sent == null) return;
            if (st.sent.Dead || CampRound3.Now > st.sentUntil && st.withFrom < 0)
            {
                // Never came: the fee goes back.
                CampRound3.Refund(c, st.fee, "CC_R3Dye_Refund", st.sent);
                st.sent = null;
                st.withFrom = -1;
                return;
            }
            bool first = st.withFrom < 0;
            var sitting = CampRound3.Sit(c, st.sent, ref st.withFrom, Round3WorldMath.DyeHours, out var dyer);
            if (first && sitting == CampRound3.Sitting.Sitting) c.SayMoment(dyer, "vat");
            if (sitting != CampRound3.Sitting.Done) return;
            // together: the colonist walked to their camp (the ask's visit) and sat within talking range of a dyer (Together.Can).
            Dye(c, st, st.sent, dyer);
            st.sent = null;
            st.withFrom = -1;
        }

        private static void Dye(Camp c, DyeState st, Pawn p, Pawn dyer)
        {
            var outer = Outer(p);
            if (outer == null) return;
            try
            {
                outer.SetColor(ColourOf(st), false);
                p.Drawer?.renderer?.SetAllGraphicsDirty();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 3 camps: the dyers' vat", e);
                return;
            }
            st.dyed++;
            if (dyer != null) c.SayMoment(dyer, "dyed");
            c.Warm(p, 1);
            CouncilGameComponent.GiveThought(p, DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_R3_Dyed"));
            c.Note(c.Text("CC_R3Dye_Done", dyer, p, 0, outer.LabelNoCount), MessageTypeDefOf.PositiveEvent, Importance.Notable, p, true);
        }

        public override Job DayJob(Camp c, Pawn p)
        {
            // Their vats by the fire, stirred by turns.
            if (!Rand.Chance(0.55f)) return null;
            var at = CampRound3.ByFire(c, p);
            if (!at.IsValid) return null;
            c.SayMoment(p, "vat", 0.12f);
            return CampRound2.Work("Rimfolk_R3_CampStirVat", at, new LocalTargetInfo(c.FireCell));
        }

        public override void AddMarks(Camp c, HashSet<string> marks)
        {
            if ((StateOf(c)?.dyed ?? 0) > 0) marks.Add("dyed");
        }

        public override string Fill(Camp c, string text) =>
            text?.Replace("{KIND}", ("CC_R3Dye_Colour_" + (StateOf(c)?.colour ?? Round3WorldMath.ColourOf(CampRound3.AgeOf(c), 0f))).Translate().Resolve());

        public override string AskBlocked(Camp c, CampAsk a)
        {
            if (a?.key != DyeAsk) return null;
            var st = StateOf(c);
            if (st?.sent != null && !st.sent.Dead && CampRound3.Now <= st.sentUntil) return "CC_R3Dye_Busy".Translate().Resolve();
            var who = c.AskColonist(a);
            return who == null || Outer(who) == null ? "CC_R3Dye_Nothing".Translate().Resolve() : null;
        }

        /// <summary>The one sent over to have their clothes dyed (the ask's visit takes them there), and the fee paid.</summary>
        public static void Sent(Camp c, Pawn a, int fee)
        {
            var st = StateOf(c);
            if (st == null || a == null) return;
            st.sent = a;
            st.fee = Mathf.Max(0, fee);
            st.withFrom = -1;
            st.sentUntil = CampRound3.Now + GenDate.TicksPerDay / 2;
        }
    }

    /// <summary>Someone of the colony sent to the dyers with their clothes (the ask's walk over is its visit); dyed once they've sat with them an hour.</summary>
    public class DilemmaEffect_R3Dye : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override string Unavailable(DilemmaContext ctx) => ctx?.a == null || CampWorker_R3Dyers.Outer(ctx.a) == null ? "CC_R3Dye_Nothing".Translate().Resolve() : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var c = CampOnMap.For(ctx);
            var outer = CampWorker_R3Dyers.Outer(ctx?.a);
            if (c?.kind?.Worker is CampWorker_R3Dyers && outer != null)
                sheet.Add(ImpactKind.Other, 1, "CC_R3Dye_Impact".Translate(ctx.a.LabelShort, outer.LabelNoCount, c.kind.Worker.Fill(c, "{KIND}")).Resolve(), "CC_R3Dye_ImpactBrief".Translate().Resolve(), "r3dye");
        }

        public override void Apply(DilemmaContext ctx) => CampWorker_R3Dyers.Sent(CampOnMap.For(ctx), ctx?.a, ctx?.count ?? 0);
    }

    // ===================== R3-C3. Witch-finders =====================

    public class WitchState : CampWorkState
    {
        /// <summary>The one of the colony they suspect; asked; nobody fits; at their fire now (since); cleared; named; refused.</summary>
        public Pawn suspect;
        public bool looked, asked, nobody, cleared, named, refused, questioning, let;
        public int withFrom = -1, questionUntil = -1, mark;

        public override void ExposeData()
        {
            Scribe_References.Look(ref suspect, "suspect");
            Scribe_Values.Look(ref looked, "looked");
            Scribe_Values.Look(ref asked, "asked");
            Scribe_Values.Look(ref nobody, "nobody");
            Scribe_Values.Look(ref cleared, "cleared");
            Scribe_Values.Look(ref named, "named");
            Scribe_Values.Look(ref refused, "refused");
            Scribe_Values.Look(ref questioning, "questioning");
            Scribe_Values.Look(ref let, "let");
            Scribe_Values.Look(ref withFrom, "withFrom", -1);
            Scribe_Values.Look(ref questionUntil, "questionUntil", -1);
            Scribe_Values.Look(ref mark, "mark");
        }
    }

    /// <summary>
    /// Witch-finders (R3-C3): two or three of a people camp at the edge of the land, and by day one of them stands looking the
    /// colony over. On their first morning they choose whom they suspect (Round3WorldMath.MarkOf: one with a psychic link, else
    /// one of a people not baseline, else one psychically sensitive, else the healer in an age of herbs) and ask to question
    /// them at their fire. Let, the suspect walks there (a visit) and after an hour together the finders say what they make of
    /// it, by the suspect's way with words: cleared (they go the next morning, their people a little warmer) or named a witch
    /// (they go at once telling it; the suspect is upset some days; their people a little cooler). Refused, the camp's unease
    /// rises, and the camps' own rule may bring an evening of blows at their fire (CampBrawls). Nobody fits: they look the
    /// colony over a day or two and go, saying there is no witch here.
    /// </summary>
    public class CampWorker_R3WitchFinders : CampWorker
    {
        public const string QuestionAsk = "question";

        public static WitchState StateOf(Camp c) => c?.workState as WitchState;

        /// <summary>What marks this one of the colony in their eyes.</summary>
        public static Round3WorldMath.Mark MarkOf(Pawn p, bool herbAge)
        {
            if (p == null || p.Dead) return Round3WorldMath.Mark.None;
            try
            {
                bool psylink = ModsConfig.RoyaltyActive && p.GetPsylinkLevel() > 0;
                var xeno = ModsConfig.BiotechActive ? p.genes?.Xenotype : null;
                bool xenotype = ModsConfig.BiotechActive && (p.genes?.CustomXenotype != null || xeno != null && xeno != XenotypeDefOf.Baseliner);
                bool sensitive = p.GetStatValue(StatDefOf.PsychicSensitivity) >= Round3WorldMath.SensitiveFrom;
                var med = p.skills?.GetSkill(SkillDefOf.Medicine);
                bool healer = med != null && !med.TotallyDisabled && med.Level >= Round3WorldMath.HealerFrom;
                return Round3WorldMath.MarkOf(psylink, xenotype, sensitive, healer, herbAge);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 3 camps: the witch-finders' eye", e);
                return Round3WorldMath.Mark.None;
            }
        }

        /// <summary>The one they suspect: the colony's grown people by their mark, the strongest first, the best healer among healers.</summary>
        public static Pawn Suspect(Map map, bool herbAge, out Round3WorldMath.Mark mark)
        {
            mark = Round3WorldMath.Mark.None;
            Pawn best = null;
            foreach (var p in IncidentWorker_Happening.Colonists(map).Where(x => !x.DevelopmentalStage.Juvenile()))
            {
                var m = MarkOf(p, herbAge);
                if (m == Round3WorldMath.Mark.None || m < mark) continue;
                if (m == mark && best != null && (p.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0) <= (best.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0)) continue;
                mark = m;
                best = p;
            }
            return best;
        }

        public override void Started(Camp c)
        {
            if (!(c.workState is WitchState)) c.workState = new WitchState();
        }

        public override void Hourly(Camp c, int hour)
        {
            if (!(c.workState is WitchState st))
            {
                Started(c);
                st = StateOf(c);
            }
            var map = c.map;
            // Their first morning: whom they suspect, and the question; nobody fits, and they go after looking the colony over.
            if (!st.looked && c.DaysHere >= 0.3f && hour >= 7 && hour <= 11)
            {
                st.looked = true;
                st.suspect = Suspect(map, Avail.IsPreIndustrial || CampRound3.AgeOf(c) == "tribal" || CampRound3.AgeOf(c) == "medieval", out var mark);
                st.mark = (int)mark;
                if (st.suspect == null)
                {
                    st.nobody = true;
                    c.untilTick = Mathf.Min(c.untilTick, CampRound3.Now + (int)(Round3WorldMath.NobodyDays * GenDate.TicksPerDay));
                }
            }
            if (st.suspect != null && !st.asked && ColonyCouncil.Tell.CanAsk && hour >= 7 && hour <= 17)
            {
                var ask = c.AskByKey(QuestionAsk);
                st.asked = true;
                if (ask != null && c.AskBlocked(ask) == null) c.Request(ask);
            }
            if (!st.let && !st.refused && c.asksMade.Contains(QuestionAsk)) st.refused = true;
            Question(c, st);
        }

        /// <summary>The suspect at their fire: after an hour together, the finders say what they make of it.</summary>
        private static void Question(Camp c, WitchState st)
        {
            if (!st.questioning || st.suspect == null) return;
            if (st.suspect.Dead || CampRound3.Now > st.questionUntil && st.withFrom < 0)
            {
                st.questioning = false;
                return;
            }
            bool first = st.withFrom < 0;
            var sitting = CampRound3.Sit(c, st.suspect, ref st.withFrom, Round3WorldMath.QuestionHours, out var finder);
            if (first && sitting == CampRound3.Sitting.Sitting) c.SayMoment(finder, "question");
            if (sitting != CampRound3.Sitting.Done || finder == null) return;
            st.questioning = false;
            int social = st.suspect.skills?.GetSkill(SkillDefOf.Social) is SkillRecord r && !r.TotallyDisabled ? r.Level : 0;
            // together: the suspect walked to their fire (the ask's visit) and sits within talking range of a finder (Together.Can).
            if (Rand.Chance(Round3WorldMath.ClearedChance(social)))
            {
                st.cleared = true;
                c.SayMoment(finder, "cleared");
                CouncilGameComponent.GiveThought(st.suspect, DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_R3_Questioned"));
                if (c.faction != null) WorldSituationWorker.Goodwill(c.faction, Round3WorldMath.ClearedGoodwill, GoodwillWhy.CampWell, false);
                int toMorning = ((7 - GenLocalDate.HourOfDay(c.map)) + 24) % 24;
                c.untilTick = Mathf.Min(c.untilTick, CampRound3.Now + Mathf.Max(2, toMorning) * GenDate.TicksPerHour);
                c.Note(c.Text("CC_R3Witch_Cleared", finder, st.suspect), MessageTypeDefOf.PositiveEvent, Importance.Notable, st.suspect, true);
            }
            else
            {
                st.named = true;
                c.SayMoment(finder, "named");
                CouncilGameComponent.GiveThought(st.suspect, DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_R3_NamedWitch"));
                if (c.faction != null) WorldSituationWorker.Goodwill(c.faction, Round3WorldMath.NamedGoodwill, GoodwillWhy.CampSour, false);
                c.untilTick = Mathf.Min(c.untilTick, CampRound3.Now + GenDate.TicksPerHour);
                c.Note(c.Text("CC_R3Witch_Named", finder, st.suspect), MessageTypeDefOf.NegativeEvent, Importance.Notable, st.suspect, true);
            }
        }

        public override Job DayJob(Camp c, Pawn p)
        {
            // One of them stands on the colony's side of their camp, looking it over.
            if (p != c.leader || !Rand.Chance(0.4f) || c.map == null) return null;
            var home = CampSite.ColonyCenter(c.map);
            if (!home.IsValid) return null;
            var toward = c.center + new IntVec3((int)((home.x - c.center.x) * 0.4f), 0, (int)((home.z - c.center.z) * 0.4f));
            var at = CampRound2.Near(c, p, toward.ClampInsideMap(c.map), 6);
            if (!at.IsValid) return null;
            c.SayMoment(p, "look", 0.3f);
            return CampRound2.Work("Rimfolk_R3_CampLookOver", at, new LocalTargetInfo(home));
        }

        public override void AddMarks(Camp c, HashSet<string> marks)
        {
            var st = StateOf(c);
            if (st == null) return;
            if (st.cleared) marks.Add("cleared");
            else if (st.named) marks.Add("named");
            else if (st.nobody) marks.Add("nobody");
            else if (st.refused) marks.Add("refused");
        }

        public override string Fill(Camp c, string text)
        {
            var st = StateOf(c);
            var p = st?.suspect;
            text = text?.Replace("{WHO}", p != null ? p.LabelShort : "CC_R3Witch_Someone".Translate().Resolve());
            return text?.Replace("{WHY}", ("CC_R3Witch_Mark_" + (Round3WorldMath.Mark)(st?.mark ?? 0)).Translate().Resolve());
        }

        public override string AskBlocked(Camp c, CampAsk a)
        {
            if (a?.key != QuestionAsk) return null;
            var st = StateOf(c);
            return st?.suspect == null || st.suspect.Dead || !st.suspect.Spawned || st.suspect.Map != c.map ? "CC_R3Witch_NoSuspect".Translate().Resolve() : null;
        }

        /// <summary>Let them question the suspect: the suspect walks to their fire (a visit), and the questioning waits for them there.</summary>
        public static void Let(Camp c, DilemmaContext ctx)
        {
            var st = StateOf(c);
            var p = st?.suspect;
            if (st == null || p == null || p.Dead) return;
            st.questioning = true;
            st.let = true;
            st.refused = false;
            st.withFrom = -1;
            st.questionUntil = CampRound3.Now + GenDate.TicksPerDay;
            var walk = new HappeningContext { map = c.map, a = p, faction = c.faction, stranger = ctx?.stranger, placeName = c.place };
            PlayOuts.Run(new List<DilemmaEffect>
            {
                new DilemmaEffect_Visit { who = PlayWho.A, to = PlaySpot.Camp, label = "CC_R3Witch_VisitLabel".Translate().Resolve(), duration = new FloatRange(1.5f, 2f) }
            }, walk, "round 3 camps: the questioning");
        }
    }

    /// <summary>The witch-finders let question the one they suspect (their ask): that one walks to their fire, and is questioned there.</summary>
    public class DilemmaEffect_R3Question : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var p = CampWorker_R3WitchFinders.StateOf(CampOnMap.For(ctx))?.suspect;
            if (p == null) return;
            sheet.Play("CC_R3Witch_Impact".Translate(p.LabelShort).Resolve(), "CC_R3Witch_ImpactBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx) => CampWorker_R3WitchFinders.Let(CampOnMap.For(ctx), ctx);
    }

    // ===================== R3-C4. Rag-and-bone men =====================

    public class RagState : CampWorkState
    {
        /// <summary>How many worn-out things the colony sent out to them, all told.</summary>
        public int rags;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref rags, "rags");
        }
    }

    /// <summary>
    /// Rag-and-bone men (R3-C4): two or three with a cart camp by the colony a few days and sort rags and scrap at their fire.
    /// The colony may carry its worn-out things out to them (the scrap the errands know: clothes and weapons in the stores too
    /// far gone, and slag: PlayCarry.Scrap), and one of them brings the price to the gate; or buy a bundle of mended clothes of
    /// their age, carried to the gate. Now and then they mend a colonist's worn gear (the camps' own mending).
    /// </summary>
    public class CampWorker_R3RagPickers : CampWorker
    {
        public const string RagsAsk = "rags";

        public static RagState StateOf(Camp c) => c?.workState as RagState;

        /// <summary>The worn-out things in the colony's stores, as many as would go out at once.</summary>
        public static int Worn(Map map) => Mathf.Min(Round3WorldMath.MaxRags, PlayCarry.Have(map, PlayCarry.Scrap, null));

        public override void Started(Camp c)
        {
            if (!(c.workState is RagState)) c.workState = new RagState();
        }

        public override Job DayJob(Camp c, Pawn p)
        {
            if (!Rand.Chance(0.5f)) return null;
            var at = CampRound3.ByFire(c, p);
            if (!at.IsValid) return null;
            c.SayMoment(p, "sort", 0.12f);
            return CampRound2.Work("Rimfolk_R3_CampSort", at, new LocalTargetInfo(c.FireCell));
        }

        public override void AddMarks(Camp c, HashSet<string> marks)
        {
            if ((StateOf(c)?.rags ?? 0) > 0) marks.Add("rags");
        }

        public override string Fill(Camp c, string text)
        {
            int n = Worn(c?.map);
            return text?.Replace("{SILVER}", Round3WorldMath.RagPrice(n).ToString());
        }

        public override string AskBlocked(Camp c, CampAsk a) => a?.key == RagsAsk && Worn(c?.map) <= 0 ? "CC_R3Rags_Nothing".Translate().Resolve() : null;
    }

    /// <summary>The colony's worn-out things carried out to the rag-and-bone men; one of them brings the price to the gate.</summary>
    public class DilemmaEffect_R3Rags : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override string Unavailable(DilemmaContext ctx)
        {
            var c = CampOnMap.For(ctx);
            if (CampWorker_R3RagPickers.Worn(ctx?.map) <= 0) return "CC_R3Rags_Nothing".Translate().Resolve();
            return c == null || !c.CanBring ? "CC_CampErrand_No_gate".Translate().ToString() : null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            int n = CampWorker_R3RagPickers.Worn(ctx?.map);
            if (n <= 0 || CampOnMap.For(ctx) == null) return;
            sheet.Add(ImpactKind.Goods, -1, "CC_R3Rags_Impact".Translate(n).Resolve(), "CC_R3Rags_ImpactBrief".Translate().Resolve(), "r3rags");
            sheet.Silver(Round3WorldMath.RagPrice(n));
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampOnMap.For(ctx);
            var st = CampWorker_R3RagPickers.StateOf(c);
            int n = CampWorker_R3RagPickers.Worn(ctx?.map);
            if (c == null || st == null || n <= 0) return;
            try
            {
                // The worn-out things carried out to their camp by one of the colony (the play-out).
                new DilemmaEffect_CarryOut { what = PlayCarry.Scrap, count = n, to = PlaySpot.Camp, who = PlayWho.A, steward = false, label = "CC_R3Rags_Label".Translate().Resolve() }.Apply(ctx);
                st.rags += n;
                int price = Round3WorldMath.RagPrice(n);
                // apart: an answer to the camp; one of them carries the price over to the gate.
                if (!c.StartBring(ThingDefOf.Silver, price)) c.GiveGoods(ThingDefOf.Silver.defName, price, ThingDefOf.Silver.defName);
                var speaker = CampRound3.Up(c).FirstOrDefault();
                if (speaker != null) c.SayMoment(speaker, "rags");
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 3 camps: the rags", e);
            }
        }
    }

    /// <summary>A bundle of mended clothes from the rag-and-bone men, of their age, carried to the colony's gate by one of them.</summary>
    public class DilemmaEffect_R3Clothes : DilemmaEffect_CampBringsAny
    {
        /// <summary>
        /// What they mend and sell, by their age, from the player's mods and the game: Vanilla Factions Expanded - Tribals' light
        /// tribalwear and Vanilla Apparel Expanded's poncho and tunic, Rimsenal - Feral's patched shirt and Vanilla Apparel
        /// Expanded's fleece and casual shirts, the game's tribalwear and T-shirt last so every age has one.
        /// </summary>
        public static string[] ClothesFor(string ageTag)
        {
            switch (ageTag)
            {
                case "tribal": return new[] { "VFET_Apparel_TribalLight", "VAE_Apparel_TribalPoncho", "Apparel_TribalA" };
                case "medieval": return new[] { "Apparel_PatchedShirt", "VAE_Apparel_Tunic", "Apparel_TribalA" };
                case "industrial": return new[] { "Apparel_PatchedShirt", "VAE_Apparel_FleeceShirt", "Apparel_BasicShirt" };
                default: return new[] { "VAE_Apparel_CasualTShirt", "Apparel_BasicShirt" };
            }
        }

        /// <summary>One of those of their age that exist, the same for one camp all its stay (by the camp's id).</summary>
        protected override ThingDef Thing(DilemmaContext ctx)
        {
            var c = CampOnMap.For(ctx);
            var have = ClothesFor(CampRound3.AgeOf(c)).Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).Where(d => d != null && d.IsApparel).ToList();
            return have.Count > 0 ? have[Mathf.Abs(c?.id ?? 0) % have.Count] : base.Thing(ctx);
        }
    }

    // ===================== R3-C5. Gamblers =====================

    public class DiceState : CampWorkState
    {
        /// <summary>The one of the colony sent to play, their stake, since when they sat at the fire; how the evenings went.</summary>
        public Pawn player;
        public int stake, playUntil = -1, withFrom = -1, won, lost, caught;
        public bool ledgerSaid;

        public override void ExposeData()
        {
            Scribe_References.Look(ref player, "player");
            Scribe_Values.Look(ref stake, "stake");
            Scribe_Values.Look(ref playUntil, "playUntil", -1);
            Scribe_Values.Look(ref withFrom, "withFrom", -1);
            Scribe_Values.Look(ref won, "won");
            Scribe_Values.Look(ref lost, "lost");
            Scribe_Values.Look(ref caught, "caught");
            Scribe_Values.Look(ref ledgerSaid, "ledgerSaid");
        }
    }

    /// <summary>
    /// Gamblers (R3-C5): two to four camp by the colony and play dice or cards at their fire every evening. The colony may send
    /// someone to play an evening (their ask: the stake carried out, then a walk to their fire at dusk): after an hour and a half
    /// at the fire it is settled (Round3WorldMath.Gamble), won (one of them brings twice the stake to the gate), lost (the stake
    /// stays with them), or their loaded dice caught by a sharp eye (the stake brought back, and their camp's unease jumps, so
    /// the camps' own rule may bring blows at their fire). The colony's believers of the ledger's god say a word against dice.
    /// </summary>
    public class CampWorker_R3Gamblers : CampWorker
    {
        public static DiceState StateOf(Camp c) => c?.workState as DiceState;

        public override void Started(Camp c)
        {
            if (!(c.workState is DiceState)) c.workState = new DiceState();
        }

        public override void Hourly(Camp c, int hour)
        {
            if (!(c.workState is DiceState st))
            {
                Started(c);
                st = StateOf(c);
            }
            if (st.player == null) return;
            if (st.player.Dead || CampRound3.Now > st.playUntil && st.withFrom < 0)
            {
                // Never sat down to play: the stake goes back.
                CampRound3.Refund(c, st.stake, "CC_R3Dice_Refund", st.player);
                st.player = null;
                st.withFrom = -1;
                return;
            }
            bool first = st.withFrom < 0;
            var sitting = CampRound3.Sit(c, st.player, ref st.withFrom, Round3WorldMath.PlayHours, out var them);
            if (first && sitting == CampRound3.Sitting.Sitting) c.SayMoment(them, "dice");
            if (sitting != CampRound3.Sitting.Done || them == null) return;
            // together: the colonist walked to their fire (the ask's visit) and sat within talking range of them (Together.Can).
            Settle(c, st, st.player, them);
            st.player = null;
            st.withFrom = -1;
        }

        private static void Settle(Camp c, DiceState st, Pawn p, Pawn them)
        {
            int social = p.skills?.GetSkill(SkillDefOf.Social) is SkillRecord s && !s.TotallyDisabled ? s.Level : 0;
            int sharp = p.skills?.GetSkill(SkillDefOf.Intellectual) is SkillRecord i && !i.TotallyDisabled ? i.Level : 0;
            var play = Round3WorldMath.Gamble(social, sharp, Rand.Value, Rand.Value);
            int back = Round3WorldMath.Back(play, st.stake);
            try
            {
                // apart: an answer to the camp; one of them carries the silver over to the gate.
                if (back > 0 && !c.StartBring(ThingDefOf.Silver, back)) c.GiveGoods(ThingDefOf.Silver.defName, back, ThingDefOf.Silver.defName);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 3 camps: the gamblers' silver", e);
            }
            switch (play)
            {
                case Round3WorldMath.Play.Won:
                    st.won++;
                    c.SayMoment(them, "won");
                    CouncilGameComponent.GiveThought(p, DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_R3_WonAtDice"));
                    c.Note(c.Text("CC_R3Dice_Won", them, p, back), MessageTypeDefOf.PositiveEvent, Importance.Notable, p, true);
                    break;
                case Round3WorldMath.Play.Caught:
                    st.caught++;
                    c.SayMoment(them, "cheat");
                    c.AddTension(Round3WorldMath.CaughtTension);
                    c.Note(c.Text("CC_R3Dice_Caught", them, p, back), MessageTypeDefOf.NeutralEvent, Importance.Notable, p, true);
                    break;
                default:
                    st.lost++;
                    c.SayMoment(them, "lost");
                    CouncilGameComponent.GiveThought(p, DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_R3_LostAtDice"));
                    c.Note(c.Text("CC_R3Dice_Lost", them, p, st.stake), MessageTypeDefOf.NeutralEvent, Importance.Notable, p, true);
                    break;
            }
        }

        public override Job NightJob(Camp c, Pawn p, int hour)
        {
            // Every evening, dice or cards round their fire.
            if (hour < 19 || hour > 22 || p == c.trader || !Rand.Chance(0.7f)) return null;
            var at = CampRound3.ByFire(c, p, 2);
            if (!at.IsValid) return null;
            c.SayMoment(p, "dice", 0.1f);
            return CampRound2.Work("Rimfolk_R3_CampDice", at, new LocalTargetInfo(c.FireCell));
        }

        public override void AddMarks(Camp c, HashSet<string> marks)
        {
            var st = StateOf(c);
            if (st == null) return;
            if (st.caught > 0) marks.Add("caught");
            else if (st.won > st.lost) marks.Add("won");
            else if (st.lost > 0) marks.Add("lost");
        }

        public override string AskBlocked(Camp c, CampAsk a)
        {
            var st = StateOf(c);
            return a != null && st?.player != null && !st.player.Dead && CampRound3.Now <= st.playUntil ? "CC_R3Dice_Busy".Translate().Resolve() : null;
        }

        /// <summary>The one sent to play an evening, with the stake already carried out (the ask's silver).</summary>
        public static void Sent(Camp c, Pawn a, int stake)
        {
            var st = StateOf(c);
            if (st == null || a == null) return;
            st.player = a;
            st.stake = Mathf.Max(0, stake);
            st.withFrom = -1;
            st.playUntil = CampRound3.Now + GenDate.TicksPerDay / 2;
            if (st.ledgerSaid) return;
            var ledger = CampRound2.Believers(c.map, Gods.ByKey("Ducat")).FirstOrDefault(x => x != a && x.Awake() && !x.Downed && !x.InMentalState);
            if (ledger == null) return;
            st.ledgerSaid = true;
            Speech.Say(ledger, "CC_R3Dice_LedgerLine".Translate().Resolve());
        }
    }

    /// <summary>Someone of the colony sent to play an evening at the gamblers' fire (the ask's walk over is its visit; its silver the stake).</summary>
    public class DilemmaEffect_R3Play : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.a != null && CampOnMap.For(ctx)?.kind?.Worker is CampWorker_R3Gamblers)
                sheet.Add(ImpactKind.Other, 0, "CC_R3Dice_Impact".Translate(ctx.a.LabelShort).Resolve(), "CC_R3Dice_ImpactBrief".Translate().Resolve(), "r3dice");
        }

        public override string Unavailable(DilemmaContext ctx)
        {
            var st = CampWorker_R3Gamblers.StateOf(CampOnMap.For(ctx));
            return st?.player != null && !st.player.Dead && CampRound3.Now <= st.playUntil ? "CC_R3Dice_Busy".Translate().Resolve() : null;
        }

        public override void Apply(DilemmaContext ctx) => CampWorker_R3Gamblers.Sent(CampOnMap.For(ctx), ctx?.a, ctx?.count ?? 0);
    }

    // ===================== R3-C6. Dog-breeders =====================

    public class KennelState : CampWorkState
    {
        /// <summary>How many of their dogs the colony bought.</summary>
        public int sold;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref sold, "sold");
        }
    }

    /// <summary>
    /// Dog-breeders (R3-C6): two or three breeders with four or five dogs picketed by their tents (the player's mods' breeds first,
    /// the game's after); by day they put one dog or another through its paces. The colony may buy one (their ask): the youngest
    /// of their dogs not at someone's heels becomes the colony's where it stands (Camp.HandOverBeast) and keeps at the heels of
    /// the colony's best with animals for a day (DilemmaEffect_Follow); or learn from them how they train a dog. After a good
    /// stay they hand a pup over at the gate (the camps' own young beast).
    /// </summary>
    public class CampWorker_R3Kennel : CampWorker
    {
        public const string DogAsk = "dog";

        public static KennelState StateOf(Camp c) => c?.workState as KennelState;

        public override void Started(Camp c)
        {
            if (!(c.workState is KennelState)) c.workState = new KennelState();
        }

        public override Job DayJob(Camp c, Pawn p)
        {
            // One dog or another put through its paces by one of them.
            if (!Rand.Chance(0.35f) || c.map == null) return null;
            var dog = c.herd.Where(x => x != null && !x.Dead && x.Spawned && !x.Downed && x.Map == c.map && c.GroupOf(x)?.follow != true && p.CanReach(x, PathEndMode.Touch, Danger.Some))
                .RandomElementWithFallback();
            var def = DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_R3_CampTrainDog");
            if (dog == null || def == null) return null;
            c.SayMoment(p, "train", 0.25f);
            return JobMaker.MakeJob(def, dog);
        }

        public override void AddMarks(Camp c, HashSet<string> marks)
        {
            if ((StateOf(c)?.sold ?? 0) > 0) marks.Add("sold");
        }

        public override string AskBlocked(Camp c, CampAsk a) => a?.key == DogAsk && !c.R3HasBeastToSell ? "CC_R3Dog_NoneLeft".Translate().Resolve() : null;
    }

    /// <summary>One of the dog-breeders' dogs bought (their ask): the colony's where it stands, and at the heels of the one who chose it for a day.</summary>
    public class DilemmaEffect_R3BuyDog : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override string Unavailable(DilemmaContext ctx)
        {
            var c = CampOnMap.For(ctx);
            return c == null || !c.R3HasBeastToSell ? "CC_R3Dog_NoneLeft".Translate().Resolve() : null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var c = CampOnMap.For(ctx);
            if (c == null || !c.R3HasBeastToSell) return;
            sheet.Add(ImpactKind.People, 1, "CC_R3Dog_Impact".Translate((ctx.a?.LabelShort ?? "?").Named("PAWN")).Resolve(), "CC_R3Dog_ImpactBrief".Translate().Resolve(), "r3dog");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampOnMap.For(ctx);
            var st = CampWorker_R3Kennel.StateOf(c);
            if (c == null) return;
            Pawn dog;
            try
            {
                dog = c.HandOverBeast();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 3 camps: the dog sold", e);
                return;
            }
            if (dog == null) return;
            if (st != null) st.sold++;
            var breeder = CampRound3.Up(c).FirstOrDefault();
            if (breeder != null) c.SayMoment(breeder, "sold");
            // The dog goes to the one who chose it and keeps at their heels a while (the play-out of an animal at heel).
            if (ctx.a == null || ctx.a.Dead) return;
            ctx.pet = dog;
            new DilemmaEffect_Follow { hours = Round3WorldMath.DogHeelHours }.Apply(ctx);
        }
    }
}
