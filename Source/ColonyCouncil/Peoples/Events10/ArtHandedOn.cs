using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // ===================== 100. Their art handed on (every people) =====================

    /// <summary>
    /// Their art handed on (item 100, a gift): once a game, a people of Rimfolk's at good terms with the colony (goodwill 50 or more) offers
    /// the colonist it remembers best for a good turn (KinMemory.Friend: the deeds that name them) its own art. One of theirs comes to the
    /// gate and spends an afternoon showing the colonist their art (three hours, the two of them at the gate). With A RimWorld of Magic and
    /// the colonist gifted for it (its latent trait, a mage's or a fighter's), that is the people's class book, read by the mod's own rules
    /// (the game's use of the item: the colonist takes it up and reads it, and the mod gives the class); otherwise a passion in their
    /// calling's skill (none becomes an interest, an interest a burning passion), or, where it burns already, a long lesson in it. Their
    /// art is a lesson the colony owes them for (the memory's "taught", which also moves their age by the teaching rule). Or the colony
    /// gives a gift in return and they think the better of it. One of the events' visits (kind "pev:art"): x the one of theirs, a the
    /// colonist, cell where they sit, text "people|tome, passion or lesson|gift".
    /// </summary>
    public static class ArtHandedOn
    {
        public const string Kind = "pev:art";
        public const string GiftKind = "art";

        private static int Now => Find.TickManager.TicksGame;

        private static string[] Parts(Visit v) => (v?.text ?? "").Split('|');

        /// <summary>The calling's art (its book, class, skill and who hands it on), or null for a people that isn't one of Rimfolk's.</summary>
        public static (string tome, string trait, bool fighter, string skill, string teacher)? ArtOf(Faction f) =>
            Peoples.CallingOf(f) is string c && PeopleEvents10Math.Arts.TryGetValue(c, out var a) ? a : null;

        public static SkillDef SkillOf(Faction f) => ArtOf(f) is { } a ? DefDatabase<SkillDef>.GetNamedSilentFail(a.skill) : null;

        public static ThingDef TomeOf(Faction f) => ArtOf(f) is { } a ? DefDatabase<ThingDef>.GetNamedSilentFail(a.tome) : null;

        /// <summary>
        /// The colonist can read the people's class book by A RimWorld of Magic's own rules: the mod is there, the class is switched on for
        /// this people in its settings, and they carry the latent gift its book needs (a mage's tome the magically gifted, a fighter's book
        /// the physically adept: a class already learned takes that trait's place).
        /// </summary>
        public static bool TomeReadable(Faction f, Pawn p)
        {
            var art = ArtOf(f);
            if (art == null || p?.story?.traits == null || !PeoplesMagic.Loaded || TomeOf(f) == null || DefDatabase<TraitDef>.GetNamedSilentFail(art.Value.trait) == null) return false;
            string gift = art.Value.fighter ? PeopleEvents10Math.FighterGift : PeopleEvents10Math.MageGift;
            return p.story.traits.allTraits.Any(t => t?.def?.defName == gift) && PeopleGifts.ClassOn(art.Value.trait, f);
        }

        /// <summary>What this people would hand this colonist: "tome", "passion", "lesson", or null.</summary>
        public static string ArtKind(Faction f, Pawn p)
        {
            var skill = SkillOf(f);
            var rec = skill == null ? null : p?.skills?.GetSkill(skill);
            return PeopleEvents10Math.ArtKind(TomeReadable(f, p), rec != null && !rec.TotallyDisabled, rec == null ? 0 : (int)rec.passion);
        }

        /// <summary>The colonist this people remembers best for a good turn, if they are at home on this map, grown and free for a gift.</summary>
        public static Pawn Friend(Faction f, Map map, out FactionDeed deed)
        {
            deed = null;
            var p = KinMemory.Friend(f, out deed);
            if (p == null || !p.Spawned || p.Map != map || !PeoplesG.Grown(map).Contains(p) || !PeopleEvents.MayGive(GiftKind, p)) return null;
            return p;
        }

        /// <summary>The peoples that would hand on their art here now, each with its colonist and what it would be.</summary>
        public static List<(Faction f, Pawn p, string art)> Offers(Map map)
        {
            var list = new List<(Faction, Pawn, string)>();
            var book = PeopleEvents10Book.Instance;
            if (map == null || book == null) return list;
            foreach (var f in Find.FactionManager.AllFactionsListForReading)
            {
                if (f == null || !Peoples.IsOurs(f) || !Arrivals.Usable(f) || !f.HasGoodwill || f.PlayerGoodwill < PeopleEvents10Math.ArtGoodwill || book.ArtGiven(f)) continue;
                var art = ArtOf(f);
                if (art == null || PeoplesD.KindNamed(f, art.Value.teacher) == null) continue;
                var p = Friend(f, map, out _);
                string kind = p == null ? null : ArtKind(f, p);
                if (kind != null) list.Add((f, p, kind));
            }
            return list;
        }

        /// <summary>The answer taken: the afternoon at the gate with the colonist (or at once, with the play-outs off).</summary>
        public static void Begin(HappeningContext ctx, bool gift)
        {
            var map = ctx?.map;
            var t = CouncilGameComponent.Instance?.happenings;
            if (map == null || t == null || ctx.faction == null || ctx.a == null || ctx.stranger == null) return;
            var v = t.AddVisit(Kind, ctx.incident, map, ctx.stranger, ctx.a, null, Now + PeopleEvents10Math.HearWaitHours * GenDate.TicksPerHour + PeopleEvents10Math.ArtTicks + 2 * GenDate.TicksPerHour);
            v.text = ctx.faction.GetUniqueLoadID() + "|" + (ctx.note ?? "passion") + "|" + (gift ? "gift" : "");
            v.cell = ctx.stranger.Spawned ? ctx.stranger.Position : PeoplesD.GateSpot(map) ?? ctx.a.Position;
            v.stake = 0;
            if (!PlayOuts.On)
            {
                Hand(v);
                return;
            }
            TryShow(v);
        }

        /// <summary>The afternoon begun: the one of theirs and the colonist together at the gate, the art shown.</summary>
        private static void TryShow(Visit v)
        {
            var map = v.map;
            var p = v.a;
            if (map == null || !PeoplesG.Up(p, map) || p.Drafted || MapFacts.ThreatOn(map) || !PeoplesG.Up(v.x, map)) return;
            if (!PeopleEvents10.Send(p, PeopleEvents10.LearnArtJob, v.cell, PeopleEvents10Math.ArtTicks)) return;
            v.stake = 1;
            PeopleEvents10.Errand(v.x, map, v.cell, PeopleEvents10.ShowArtJob, PeopleEvents10Math.ArtTicks + GenDate.TicksPerHour, PeopleEvents10Math.ArtTicks / (float)GenDate.TicksPerHour + 3f);
            PeoplesG.SayLine(v.x, v.incident, "show");
        }

        public static bool Hour(Visit v, int now)
        {
            if (v.done) return false;
            if (now > v.until || v.a == null || v.a.Dead || !PeoplesG.Up(v.x, v.map))
            {
                // Never shown: they go home with their art, and the offer may come again another time.
                v.done = true;
                var f = PeopleEvents10.PeopleById(Parts(v).ElementAtOrDefault(0));
                Tell.Message(NewsKind.People, Importance.Flavor, v.map, "CC_PEv10_ArtLeft".Translate((v.x?.LabelShort ?? "?").Named("TEACHER"), (f?.Name ?? "?").Named("FACTION")).Resolve(),
                    LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
                if (v.x != null && v.x.Spawned) PeoplesG.Home(new List<Pawn> { v.x });
                return false;
            }
            if (v.stake == 0) TryShow(v);
            else if (!v.a.Spawned || v.a.CurJobDef != PeopleEvents10.LearnArtJob) v.stake = 0;
            return !v.done;
        }

        /// <summary>The colonist's afternoon done (round 9's work at a place): the art handed on.</summary>
        public static void Learned(Pawn p)
        {
            var v = PeopleEvents10.OfColonist(Kind, p);
            if (v != null) Hand(v);
        }

        /// <summary>
        /// The art handed on, as the card said: the book handed over and read by the mod's own use (or set down if it can't be read now),
        /// the passion found or deepened, or the long lesson; the gift noted, their thanks, the colony's debt of a lesson, told.
        /// </summary>
        private static void Hand(Visit v)
        {
            v.done = true;
            var parts = Parts(v);
            var f = PeopleEvents10.PeopleById(parts.ElementAtOrDefault(0));
            var p = v.a;
            var x = v.x;
            var map = p?.MapHeld ?? v.map;
            if (f == null || p == null || p.Dead) return;
            string kind = parts.ElementAtOrDefault(1) ?? "passion";
            // What they would hand on may have changed since (the gift used by another tome, a passion grown on its own).
            if (kind == "tome" && !TomeReadable(f, p)) kind = ArtKind(f, p) ?? "lesson";
            var skill = SkillOf(f);
            var rec = skill == null ? null : p.skills?.GetSkill(skill);
            string text;
            switch (kind)
            {
                case "tome":
                {
                    var def = TomeOf(f);
                    var tome = ThingMaker.MakeThing(def);
                    var at = p.Spawned ? p.Position : x != null && x.Spawned ? x.Position : v.cell;
                    if (map != null) GenPlace.TryPlaceThing(tome, at, map, ThingPlaceMode.Near);
                    tome.SetForbidden(false, false);
                    bool reading = false;
                    try
                    {
                        var use = tome.TryGetComp<CompUsable>();
                        if (use != null && p.Spawned && use.CanBeUsedBy(p).Accepted)
                        {
                            use.TryStartUseJob(p, LocalTargetInfo.Invalid);
                            reading = p.CurJobDef != null && p.CurJob?.targetA.Thing == tome;
                        }
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("peoples' events 10: the class book read", e);
                    }
                    text = (reading ? "CC_PEv10_ArtTome" : "CC_PEv10_ArtTomeLater").Translate((x?.LabelShort ?? "?").Named("TEACHER"), p.Named("PAWN"), def.label.Named("TOME"),
                        f.Name.Named("FACTION")).Resolve();
                    break;
                }
                case "passion":
                    if (rec != null && !rec.TotallyDisabled && PeopleEvents10Math.PassionAfter((int)rec.passion) is int next && next >= 0)
                    {
                        rec.passion = (Passion)next;
                        text = (next == 2 ? "CC_PEv10_ArtBurning" : "CC_PEv10_ArtPassion").Translate((x?.LabelShort ?? "?").Named("TEACHER"), p.Named("PAWN"),
                            (skill?.label ?? "?").Named("SKILL"), f.Name.Named("FACTION")).Resolve();
                        break;
                    }
                    goto default;
                default:
                    rec?.Learn(PeopleEvents10Math.GreatLessonXp, true);
                    text = "CC_PEv10_ArtLesson".Translate((x?.LabelShort ?? "?").Named("TEACHER"), p.Named("PAWN"), (skill?.label ?? "?").Named("SKILL"), f.Name.Named("FACTION")).Resolve();
                    break;
            }
            PeopleEvents10Book.Instance?.GaveArt(f);
            PeopleEventsBook.Gave(GiftKind, p);
            PeopleEvents10.Thought(p, "Rimfolk_Thought_ArtHandedOn");
            bool gift = parts.ElementAtOrDefault(2) == "gift";
            PeopleEvents10.Goodwill(f, PeopleEvents10Math.ArtGoodwillGain + (gift ? PeopleEvents10Math.ReturnGiftGoodwill : 0), gift ? GoodwillWhy.Gift : GoodwillWhy.Friendship);
            // The colony owes them a lesson (their masters teaching the colony's people), which also moves their age by the teaching rule.
            FactionMemory.Note(f, "taught", p.LabelShort);
            PeoplesG.SayLine(x, v.incident, "handed");
            Tell.Letter(NewsKind.People, Importance.Notable, map, "CC_PEv10_ArtLetterLabel".Translate(p.LabelShort).Resolve(), text, LetterDefOf.PositiveEvent,
                p.Spawned ? new LookTargets(p) : LookTargets.Invalid, f);
            CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.World, "CC_PEv10_ArtChronicle".Translate(p.Named("PAWN"), f.Name.Named("FACTION")).Resolve(), p));
            if (x != null && x.Spawned) PeoplesG.Home(new List<Pawn> { x });
        }
    }

    /// <summary>
    /// One of a people of Rimfolk's ({FACTION}, {X}) at the gate, offering {A}, the colonist they remember best ({TALE}: what for), the
    /// people's own art: their class book {THING} (A RimWorld of Magic, {A} gifted for it), else a passion (or a long lesson) in {SKILL}.
    /// </summary>
    public class HappeningWorker_ArtHandedOn : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) => ArtHandedOn.Offers(map).Count > 0 && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var offers = ArtHandedOn.Offers(ctx.map);
            if (offers.Count == 0) return false;
            var (f, p, art) = offers.RandomElement();
            ctx.faction = f;
            ctx.a = p;
            ctx.note = art;
            var kind = ArtHandedOn.ArtOf(f);
            if (ctx.faction == null || ctx.a == null || kind == null) return false;
            ctx.skill = ArtHandedOn.SkillOf(f);
            ctx.thing = art == "tome" ? ArtHandedOn.TomeOf(f) : null;
            ArtHandedOn.Friend(f, ctx.map, out var deed);
            ctx.tale = deed != null ? FactionMemory.Text(deed) : null;
            ctx.variant = PeopleEvents10Math.ReturnGift(LivingEvents.WealthFactor(ctx.map));
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(f, kind.Value.teacher), f, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.placeName = LivingEvents.TownOf(f, ctx.map) ?? f.Name;
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            tags.Add(ctx.note ?? "passion");
            tags.Add(ctx.tale.NullOrEmpty() ? "nodeed" : "deed");
            var calling = Peoples.CallingOf(ctx.faction);
            if (calling != null) tags.Add(calling);
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option > 1) return null;
            if (ctx.a == null || ctx.a.Dead || !ctx.a.Spawned || ctx.a.Map != ctx.map) return "CC_PEv10_ArtColonistGone".Translate().Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "offer" : "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The art taken: the afternoon at the gate, then the book, the passion or the lesson; with <see cref="gift"/>, the colony's gift in return.</summary>
    public class DilemmaEffect_ArtHandedOn : DilemmaEffect
    {
        public bool gift;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.a == null || hc.faction == null) return;
            sheet.Play("CC_PEv10_Impact_Art".Translate(hc.stranger?.LabelShort ?? "?", hc.a.LabelShort).Resolve(), "CC_PEv10_Impact_ArtBrief".Translate().Resolve());
            switch (hc.note)
            {
                case "tome":
                    sheet.Add(ImpactKind.Skill, 1, "CC_PEv10_Impact_ArtTome".Translate(hc.a.LabelShort, hc.thing?.label ?? "?").Resolve(), "CC_PEv10_Impact_ArtTomeBrief".Translate().Resolve(),
                        "pev10art");
                    break;
                case "passion":
                    sheet.Add(ImpactKind.Skill, 1, "CC_PEv10_Impact_ArtPassion".Translate(hc.a.LabelShort, hc.skill?.label ?? "?").Resolve(),
                        "CC_PEv10_Impact_ArtPassionBrief".Translate().Resolve(), "pev10art");
                    break;
                default:
                    sheet.Add(ImpactKind.Skill, 1, "CC_PEv10_Impact_ArtLesson".Translate(hc.a.LabelShort, hc.skill?.label ?? "?").Resolve(),
                        "CC_PEv10_Impact_ArtLessonBrief".Translate().Resolve(), "pev10art");
                    break;
            }
            sheet.Add(ImpactKind.Goodwill, 0, "CC_PEv10_Impact_ArtOwed".Translate(hc.faction.Name).Resolve(), "CC_PEv10_Impact_ArtOwedBrief".Translate().Resolve(), "pev10owed");
            sheet.Goodwill(hc.faction, PeopleEvents10Math.ArtGoodwillGain + (gift ? PeopleEvents10Math.ReturnGiftGoodwill : 0));
        }

        public override void Apply(DilemmaContext ctx) => ArtHandedOn.Begin(ctx as HappeningContext, gift);
    }
}
