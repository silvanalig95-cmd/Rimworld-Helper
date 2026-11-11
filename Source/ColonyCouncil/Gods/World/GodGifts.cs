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
    /// Relics and gifts (docs/design/gods.md, stage 2, brainstorm item 21), in the shape of the peoples' gifts (one of a kind
    /// a colonist, spaced with theirs: PeopleEventsBook):
    /// <list type="bullet">
    /// <item>A god the colony stands favoured with now and then offers one of its people a lasting gift, and names its
    /// price (GodWorldMath.PickGift, PriceOf): a blessing of its domain on their body (a blessed hand, storm-sense, a
    /// healer's touch: Rimfolk_GodGift_*), a beast of its own (a tame wolf), a class of A RimWorld of Magic fitting the
    /// domain, a single spell or a gifted trait for one who already casts, a psylink and a Vanilla Psycasts Expanded path,
    /// or its relic (the colony faith's own lost relic with Ideology, else a small sculpture of the god in gold). The price
    /// is paid first: silver carried out and left, the god's mark, years of their life, their blood (under the harm
    /// budget), or its rivals' anger.</item>
    /// <item>Accepted, they walk out under the open sky and wait; the gift comes to them there.</item>
    /// <item>A trickster's gift turns to a curse after a while, and back (at most three turns).</item>
    /// <item>A rival the colony knows may send its avatar to undo the gift: the colony lets it take it back, or sends it off.</item>
    /// </list>
    /// Only where the gods are more than believed (Felt and Walking).
    /// </summary>
    public static class GodGifts
    {
        private static int Now => GodWorld.Now;

        public static string GiftKey(God g) => "god:" + (g?.domain ?? GodDomain.None).ToString().ToLowerInvariant();

        // ================= Relics =================

        /// <summary>The relics the colony holds on its homes: the gods' relics given, and Ideology's relics of any faith; with the god (or the faith) each is sacred to.</summary>
        public static IEnumerable<(Thing thing, God god, Ideo ideo, Map map)> Relics()
        {
            var b = GodWorld.Book;
            if (b != null)
                foreach (var g in b.gifts.Where(x => x.kind == GodGiftKind.Relic && x.thing != null && !x.thing.Destroyed && !x.undone))
                {
                    var map = g.thing.MapHeld;
                    if (map != null && Homes.IsHome(map)) yield return (g.thing, Gods.ById(g.god), null, map);
                }
            if (!ModsConfig.IdeologyActive || Find.IdeoManager == null) yield break;
            foreach (var ideo in Find.IdeoManager.IdeosListForReading)
            {
                if (ideo?.PreceptsListForReading == null) continue;
                foreach (var pr in ideo.PreceptsListForReading.OfType<Precept_Relic>())
                {
                    var t = pr.GeneratedRelic;
                    var map = t?.MapHeld;
                    if (t == null || t.Destroyed || map == null || !Homes.IsHome(map)) continue;
                    yield return (t, Gods.OfIdeo(ideo).FirstOrDefault(x => x.kind != GodKind.Dead), ideo, map);
                }
            }
        }

        /// <summary>A relic given away: no longer the colony's gift.</summary>
        public static void Gone(Thing t)
        {
            var b = GodWorld.Book;
            if (b == null || t == null) return;
            foreach (var g in b.gifts.Where(x => x.thing == t)) g.undone = true;
        }

        /// <summary>The colony faith's relic not yet found, of a faith that holds this god (Ideology): the god may give it back.</summary>
        private static Precept_Relic LostRelic(God g)
        {
            if (!ModsConfig.IdeologyActive || g == null) return null;
            foreach (var ideo in g.ideos)
            {
                if (ideo == null || Faction.OfPlayer?.ideos?.Has(ideo) != true) continue;
                var r = ideo.PreceptsListForReading.OfType<Precept_Relic>().FirstOrDefault(x => x.CanGenerateRelic);
                if (r != null) return r;
            }
            return null;
        }

        // ================= Who, what, and the price =================

        /// <summary>The skill a god of the domain looks for in whom it gives to (null: anyone, the devout first).</summary>
        private static SkillDef SkillOf(GodDomain d) => d switch
        {
            GodDomain.Harvest => SkillDefOf.Plants,
            GodDomain.Hunt => SkillDefOf.Shooting,
            GodDomain.Healing => SkillDefOf.Medicine,
            GodDomain.War => SkillDefOf.Melee,
            GodDomain.Craft => SkillDefOf.Crafting,
            GodDomain.Way => SkillDefOf.Social,
            GodDomain.Night => SkillDefOf.Artistic,
            GodDomain.Hearth => SkillDefOf.Cooking,
            GodDomain.Wild => SkillDefOf.Animals,
            GodDomain.Waters => SkillDefOf.Plants,
            GodDomain.Machine => SkillDefOf.Intellectual,
            _ => null
        };

        /// <summary>The colonist a god would give to: grown, up and well, no gift of its kind yet; one of its own faith first, then the best at its work.</summary>
        private static Pawn Chosen(God g, Map map)
        {
            var skill = SkillOf(g.domain);
            return GodWorld.Grown(map).Where(p => !p.InMentalState && PeopleEvents.MayGive(GiftKey(g), p))
                .OrderByDescending(p => Gods.SwearBy(p) == g ? 1 : 0)
                .ThenByDescending(p => skill != null ? p.skills?.GetSkill(skill)?.Level ?? 0 : p.GetStatValue(StatDefOf.PsychicSensitivity) * 10f)
                .FirstOrDefault();
        }

        private static HediffDef HandDef(God g) => DefDatabase<HediffDef>.GetNamedSilentFail(GodWays.Hand(g) ?? "");

        private static PawnKindDef BeastKind(God g)
        {
            var sacred = g.DomainDef?.Animals?.Where(a => a.race != null && a.GetStatValueAbstract(StatDefOf.Wildness) <= 0.8f).Select(a => DefDatabase<PawnKindDef>.GetNamedSilentFail(a.defName));
            var kinds = GodWays.Beasts(g).Select(n => DefDatabase<PawnKindDef>.GetNamedSilentFail(n)).Concat(sacred ?? Enumerable.Empty<PawnKindDef>())
                .Where(k => k?.RaceProps?.Animal == true).ToList();
            return kinds.FirstOrDefault();
        }

        private static string ClassFor(God g, Pawn p)
        {
            if (p?.story?.traits == null) return null;
            // One class a pawn, as the mod keeps it: none to one who already has one of its classes.
            if (MagicClasses.Level(p, false) >= 0 || MagicClasses.Level(p, true) >= 0) return null;
            return GodWays.Classes(g).FirstOrDefault(t => DefDatabase<TraitDef>.GetNamedSilentFail(t) != null && PeoplesGearMath.MagicSetting(t) != null);
        }

        private static ThingDef SpellFor(God g, Pawn p) =>
            MagicClasses.Level(p, false) < 0 ? null : GodWays.Spells(g).Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).Where(d => d != null).RandomElementWithFallback();

        private static TraitDef ManaFor(Pawn p)
        {
            bool casts = MagicClasses.Level(p, false) >= 0, fights = MagicClasses.Level(p, true) >= 0;
            if (!casts && !fights || p.story?.traits == null) return null;
            return GodWorldMath.ManaTraits(!casts).Select(n => DefDatabase<TraitDef>.GetNamedSilentFail(n))
                .Where(d => d != null && !p.story.traits.HasTrait(d) && !p.story.traits.allTraits.Any(t => t.def.ConflictsWith(d) || d.ConflictsWith(t.def))).RandomElementWithFallback();
        }

        private static bool PathOpen(Pawn p) =>
            ModsConfig.RoyaltyActive && DefDatabase<HediffDef>.GetNamedSilentFail("VPE_PsycastAbilityImplant") != null && p.GetMainPsylinkSource() == null
            && p.health?.hediffSet?.GetBrain() != null;

        /// <summary>What the god would give this colonist now, and the def it comes as (null: nothing).</summary>
        private static (GodGiftKind kind, string what, string label) Pick(God g, Pawn p)
        {
            var hand = HandDef(g);
            bool hasHand = hand != null && !p.health.hediffSet.HasHediff(hand);
            var beast = BeastKind(g);
            string cls = ClassFor(g, p);
            var spell = SpellFor(g, p);
            var mana = ManaFor(p);
            bool path = PathOpen(p) && GodWays.Paths(g).Count > 0;
            var lost = LostRelic(g);
            var sculpture = Sculpture;
            var kind = GodWorldMath.PickGift(hasHand, beast != null, cls != null, spell != null, mana != null, path, lost != null || sculpture != null, Rand.Value);
            switch (kind)
            {
                case GodGiftKind.Hand: return (kind, hand.defName, hand.label);
                case GodGiftKind.Beast: return (kind, beast.defName, "CC_GW_Gift_Beast".Translate(beast.label.Named("BEAST")).Resolve());
                case GodGiftKind.Class: return (kind, cls, "CC_GW_Gift_Class".Translate(DefDatabase<TraitDef>.GetNamed(cls).degreeDatas[0].label.Named("CLASS")).Resolve());
                case GodGiftKind.Spell: return (kind, spell.defName, "CC_GW_Gift_Spell".Translate(spell.label.Named("SPELL")).Resolve());
                case GodGiftKind.Mana: return (kind, mana.defName, "CC_GW_Gift_Mana".Translate(mana.degreeDatas[0].label.Named("TRAIT")).Resolve());
                case GodGiftKind.Path: return (kind, null, "CC_GW_Gift_Path".Translate().Resolve());
                case GodGiftKind.Relic:
                    return lost != null
                        ? (kind, "precept:" + lost.Id, "CC_GW_Gift_LostRelic".Translate(lost.LabelCap.Named("RELIC")).Resolve())
                        : (kind, sculpture.defName, "CC_GW_Gift_Sculpture".Translate(g.name.Named("GOD")).Resolve());
                default: return (GodGiftKind.None, null, null);
            }
        }

        /// <summary>The price in words, for the letter.</summary>
        private static string PriceWords(God g, GodPrice price, int silver, int years)
        {
            switch (price)
            {
                case GodPrice.Offering: return "CC_GW_Price_Offering".Translate(silver.Named("COUNT")).Resolve();
                case GodPrice.Mark: return GodWorld.T("CC_GW_Price_Mark", g);
                case GodPrice.Years: return "CC_GW_Price_Years".Translate(years.Named("YEARS")).Resolve();
                case GodPrice.Blood: return "CC_GW_Price_Blood".Translate().Resolve();
                default:
                    var rivals = Gods.Rivals(g).Where(Gods.Known).Select(x => x.name).ToList();
                    return "CC_GW_Price_Rival".Translate((rivals.Count > 0 ? rivals.ToCommaList(true) : "CC_GW_TheOthers".Translate().Resolve()).Named("RIVALS")).Resolve();
            }
        }

        // ================= The day: offers, the trickster, the rival =================

        public static void Daily(GodWorldBook b, int now)
        {
            GodWorld.Safe("a gift offered", () => Offer(b));
            foreach (var gift in b.gifts.Where(x => x.Holds).ToList())
            {
                GodWorld.Safe("the trickster's turn", () => Trickster(gift));
                GodWorld.Safe("a rival's coming", () => Rival(b, gift));
            }
        }

        internal static void Offer(GodWorldBook b)
        {
            if (!GodWorld.CanAsk || !b.Gap("gift", GodWorldMath.GiftGap) || !PeopleEventsBook.GiftOpen) return;
            foreach (var map in Homes.All.ToList())
            {
                foreach (var g in GodWorld.Known.Where(x => x.kind != GodKind.Dead && x.kind != GodKind.Archotech).ToList())
                {
                    var word = Gods.Favour(g, map);
                    if (word < FavourWord.Favoured) continue;
                    float days = word == FavourWord.Beloved ? GodWorldMath.GiftDays / 2f : GodWorldMath.GiftDays;
                    if (!GodWorld.Roll(GodWorldMath.DayChance(days, GodWorld.Factor(true)))) continue;
                    var p = Chosen(g, map);
                    if (p == null) continue;
                    var (kind, what, label) = Pick(g, p);
                    if (kind == GodGiftKind.None) continue;
                    var price = GodWays.Price(g, Rand.Value);
                    if (price == GodPrice.Blood && !Harm.May(p)) price = GodPrice.Mark;
                    if (price == GodPrice.Rival && !Gods.Rivals(g).Any(Gods.Known)) price = GodPrice.Offering;
                    int silver = GodWorldMath.PriceSilver(kind, GodWorld.Wealth(map));
                    int years = GodWorldMath.PriceYears(g.kind, Rand.Value);
                    var d = b.Add(new GodDoing
                    {
                        kind = "offer", god = g.id, pawn = p, home = Homes.KeyOf(map), what = kind + "|" + what + "|" + label, stage = (int)price,
                        count = price == GodPrice.Years ? years : silver, until = Now + 3 * GenDate.TicksPerDay
                    });
                    string text = GodWorld.T("CC_GW_Offer_Text_" + GodWorldMath.Voice(Gods.Realness), g, p.Named("PAWN"), label.Named("GIFT"),
                        PriceWords(g, price, silver, years).Named("PRICE"));
                    if (g.kind == GodKind.Trickster) text += "\n\n" + GodWorld.T("CC_GW_Offer_Trickster", g);
                    if (GodAsks.Ask(GodAsks.Gift, map, g, GodWorld.T("CC_GW_Offer_Label", g, p.Named("PAWN")), text, new LookTargets(p), null, null, p, d.count, d.what, d.id, 2f) == null)
                    {
                        d.done = true;
                        continue;
                    }
                    b.Came("gift");
                    GodWorld.Remember(g, map, GodActKind.Act, GodWorld.T("CC_GW_Offer_Act", g, p.Named("PAWN"), label.Named("GIFT")));
                    return;
                }
            }
        }

        private static (GodGiftKind kind, string what, string label) Parse(string s)
        {
            var parts = (s ?? "").Split('|');
            Enum.TryParse(parts.Length > 0 ? parts[0] : "", out GodGiftKind k);
            return (k, parts.Length > 1 && parts[1] != "" ? parts[1] : null, parts.Length > 2 ? parts[2] : "");
        }

        public static IEnumerable<GodAnswer> OfferAnswers(ChoiceLetter_GodWorld l)
        {
            var d = l.Doing;
            var g = l.God;
            if (d == null || g == null || d.stage < 0 || d.pawn == null || d.pawn.Dead || !d.pawn.Spawned || d.pawn.Map != d.Map) yield break;
            var price = (GodPrice)d.stage;
            string disabled = null;
            if (price == GodPrice.Offering && PlayCarry.Have(d.Map, "silver", ThingDefOf.Silver) < d.count)
                disabled = "CC_Dilemma_NotEnough".Translate(d.count, ThingDefOf.Silver.label).ToString();
            if (price == GodPrice.Blood && !Harm.May(d.pawn)) disabled = "CC_GW_Offer_NotNow".Translate(d.pawn.Named("PAWN")).Resolve();
            yield return new GodAnswer("CC_GW_Offer_Accept".Translate(d.pawn.Named("PAWN")).Resolve(), Accept, disabled);
            yield return new GodAnswer("CC_GW_Offer_Decline".Translate().Resolve(), Decline);
        }

        private static void Decline(ChoiceLetter_GodWorld l)
        {
            var d = l.Doing;
            var g = l.God;
            if (d == null || g == null) return;
            d.done = true;
            float by = GodWorldMath.GiftRefused(g.nature);
            if (by != 0f) Gods.AddFavour(g, d.Map, by, GodWorld.T("CC_GW_Offer_WhyDeclined", g, d.pawn.Named("PAWN")));
        }

        /// <summary>The price paid, then they walk out under the sky to receive it.</summary>
        private static void Accept(ChoiceLetter_GodWorld l)
        {
            var d = l.Doing;
            var g = l.God;
            var p = d?.pawn;
            if (d == null || g == null || p == null) return;
            var map = d.Map;
            var price = (GodPrice)d.stage;
            try
            {
                switch (price)
                {
                    case GodPrice.Offering:
                        // apart: one of the colony carries the god's price out past the gate and leaves it there; by morning it is gone.
                        new DilemmaEffect_CarryOut { what = "silver", count = d.count, steward = false, who = PlayWho.None, to = PlaySpot.Gate, label = GodWorld.T("CC_GW_Price_Label", g) }
                            .Apply(new DilemmaContext { map = map });
                        break;
                    case GodPrice.Mark:
                        var mark = DefDatabase<HediffDef>.GetNamedSilentFail("Rimfolk_GodMark");
                        if (mark != null && !p.health.hediffSet.HasHediff(mark)) p.health.AddHediff(mark);
                        break;
                    case GodPrice.Years:
                        p.ageTracker.AgeBiologicalTicks += (long)d.count * GenDate.TicksPerYear;
                        break;
                    case GodPrice.Blood:
                        Harm.Injure(p, HurtKind.Cut, HurtWhere.Arm, 3f, 6f, HarmKinds.GodPrice);
                        break;
                    case GodPrice.Rival:
                        foreach (var r in Gods.Rivals(g).Where(Gods.Known))
                            Gods.AddFavour(r, map, GodWorldMath.RivalPrice, GodWorld.T("CC_GW_Price_WhyRival", r, p.Named("PAWN"), g.name.Named("OTHER")), FavourSource.Spill, GodOffence.Rivals);
                        break;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods in the world: a gift's price", e);
            }
            d.kind = "receive";
            d.at = Now;
            d.until = Now + GodWorldMath.GiftWaitTicks + GenDate.TicksPerDay;
            Send(d);
        }

        /// <summary>Out under the open sky near where they are, kept there facing up (the people's keep job), to wait for it.</summary>
        private static void Send(GodDoing d)
        {
            var p = d.pawn;
            var keep = PeopleWorkOn.KeepJob;
            if (keep == null || p == null || !p.Spawned || p.Downed || p.InMentalState) return;
            var cell = PeopleWorkOn.SkyCell(p.Map, p.Position, p);
            if (!cell.IsValid) return;
            d.cell = cell;
            try
            {
                var job = JobMaker.MakeJob(keep, cell, cell);
                job.count = GodWorldMath.GiftKeepTicks;
                job.locomotionUrgency = LocomotionUrgency.Walk;
                p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods in the world: out under the sky", e);
            }
        }

        // ================= The hour: the gift received =================

        public static void Hourly(GodWorldBook b, int now)
        {
            foreach (var d in b.Of("receive").ToList())
            {
                var p = d.pawn;
                if (p == null || p.Dead || now > d.until)
                {
                    d.done = true;
                    continue;
                }
                if (!p.Spawned || p.Map != d.Map) continue;
                bool there = d.cell.IsValid && p.Position == d.cell && p.CurJobDef == PeopleWorkOn.KeepJob;
                if (!there && now - d.at < GodWorldMath.GiftWaitTicks)
                {
                    if (p.CurJobDef != PeopleWorkOn.KeepJob && !p.Downed && !p.Drafted && now - d.at > GenDate.TicksPerHour) Send(d);
                    continue;
                }
                d.done = true;
                Give(b, d);
            }
            foreach (var d in b.Of("undo").ToList()) GodWorld.Safe("the rival's avatar", () => UndoHour(d));
        }

        private static void Give(GodWorldBook b, GodDoing d)
        {
            var g = Gods.ById(d.god);
            var p = d.pawn;
            var map = p.Map;
            if (g == null) return;
            var (kind, what, label) = Parse(d.what);
            Thing thing = null;
            bool ok = false;
            try
            {
                switch (kind)
                {
                    case GodGiftKind.Hand:
                        var hd = DefDatabase<HediffDef>.GetNamedSilentFail(what);
                        if (hd != null && !p.health.hediffSet.HasHediff(hd))
                        {
                            p.health.AddHediff(hd);
                            ok = true;
                        }
                        break;
                    case GodGiftKind.Beast:
                        var bk = DefDatabase<PawnKindDef>.GetNamedSilentFail(what);
                        if (bk != null)
                        {
                            var beast = PawnGenerator.GeneratePawn(bk, Faction.OfPlayer);
                            GenSpawn.Spawn(beast, CellFinder.RandomClosewalkCellNear(p.Position, map, 3), map);
                            p.relations?.AddDirectRelation(PawnRelationDefOf.Bond, beast);
                            if (beast.training != null)
                            {
                                if (beast.training.CanAssignToTrain(TrainableDefOf.Tameness).Accepted) beast.training.Train(TrainableDefOf.Tameness, p, true);
                                if (beast.training.CanAssignToTrain(TrainableDefOf.Obedience).Accepted) beast.training.Train(TrainableDefOf.Obedience, p, true);
                            }
                            thing = beast;
                            ok = true;
                        }
                        break;
                    case GodGiftKind.Class:
                        ok = PeopleGifts.GiveClass(p, what);
                        break;
                    case GodGiftKind.Spell:
                        var sd = DefDatabase<ThingDef>.GetNamedSilentFail(what);
                        if (sd != null)
                        {
                            thing = ThingMaker.MakeThing(sd);
                            if (!p.inventory.innerContainer.TryAdd(thing)) GenPlace.TryPlaceThing(thing, p.Position, map, ThingPlaceMode.Near);
                            ok = true;
                        }
                        break;
                    case GodGiftKind.Mana:
                        var td = DefDatabase<TraitDef>.GetNamedSilentFail(what);
                        if (td != null && !p.story.traits.HasTrait(td))
                        {
                            p.story.traits.GainTrait(new Trait(td, td.degreeDatas.NullOrEmpty() ? 0 : td.degreeDatas[0].degree));
                            ok = true;
                        }
                        break;
                    case GodGiftKind.Path:
                        if (PathOpen(p))
                        {
                            p.ChangePsylinkLevel(1, false);
                            PsycastPaths.Follow(p, GodWays.Paths(g), 1);
                            ok = true;
                        }
                        break;
                    case GodGiftKind.Relic:
                        thing = MakeRelic(g, what);
                        if (thing != null)
                        {
                            GenPlace.TryPlaceThing(thing, p.Position, map, ThingPlaceMode.Near);
                            ok = true;
                        }
                        break;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods in the world: a gift given", e);
            }
            if (!ok)
            {
                ColonyCouncil.Tell.Message(NewsKind.People, Importance.Notable, map, GodWorld.Voice(GodWorld.T("CC_GW_Gift_Nothing", g, p.Named("PAWN"))), new LookTargets(p),
                    MessageTypeDefOf.NeutralEvent);
                return;
            }
            PeopleEventsBook.Gave(GiftKey(g), p);
            b.AddGift(new GodGift { god = g.id, pawn = p, kind = kind, what = what, label = label, thing = thing });
            Gods.AddFavour(g, map, GodWorldMath.GiftTaken, GodWorld.T("CC_GW_Gift_Why", g, p.Named("PAWN"), label.Named("GIFT")));
            FleckMaker.ThrowMetaIcon(p.Position, map, FleckDefOf.PsycastAreaEffect);
            GodWorld.Moment(g, map, PresenceKind.Blessing, p.Position, null, GodWorld.T("CC_GW_Gift_Label", g, p.Named("PAWN")),
                GodWorld.Voice(GodWorld.T("CC_GW_Gift_Text", g, p.Named("PAWN"), label.Named("GIFT"))), true, LetterDefOf.PositiveEvent, new LookTargets(p));
            GodWorld.Remark(map, p.Position, "gift", g, p, p.Named("GIFTED"));
            GodWorld.Chronicle(GodWorld.T("CC_GW_Gift_Chronicle", g, p.Named("PAWN"), label.Named("GIFT")), p);
        }

        private static ThingDef Sculpture => DefDatabase<ThingDef>.GetNamedSilentFail("SculptureSmall");

        /// <summary>The god's relic: the colony faith's own (Ideology), or a small sculpture of the god in gold, of the finest make.</summary>
        private static Thing MakeRelic(God g, string what)
        {
            if (what != null && what.StartsWith("precept:", StringComparison.Ordinal))
            {
                string id = what.Substring("precept:".Length);
                var pr = g.ideos.SelectMany(i => i.PreceptsListForReading.OfType<Precept_Relic>()).FirstOrDefault(x => x.Id.ToString() == id && x.CanGenerateRelic);
                if (pr != null) return pr.GenerateRelic();
            }
            var def = Sculpture;
            if (def == null) return null;
            var stuff = ThingDefOf.Gold != null && def.MadeFromStuff && GenStuff.AllowedStuffsFor(def).Contains(ThingDefOf.Gold) ? ThingDefOf.Gold : GenStuff.DefaultStuffFor(def);
            var t = ThingMaker.MakeThing(def, stuff);
            t.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Legendary, ArtGenerationContext.Outsider);
            return MinifyUtility.MakeMinified(t) ?? t;
        }

        // ================= The trickster's turn =================

        internal static void Trickster(GodGift gift)
        {
            var g = Gods.ById(gift.god);
            if (g == null || g.kind != GodKind.Trickster || !gift.pawn.Spawned) return;
            int days = (Now - (gift.turnedAt >= 0 ? gift.turnedAt : gift.since)) / GenDate.TicksPerDay;
            if (!GodWorldMath.TricksterTurnsToday(days, gift.cursed, gift.turns, Rand.Value)) return;
            var curse = DefDatabase<HediffDef>.GetNamedSilentFail("Rimfolk_GodCurse");
            var hand = gift.kind == GodGiftKind.Hand ? DefDatabase<HediffDef>.GetNamedSilentFail(gift.what) : null;
            var p = gift.pawn;
            try
            {
                if (!gift.cursed)
                {
                    if (hand != null && p.health.hediffSet.GetFirstHediffOfDef(hand) is Hediff h) p.health.RemoveHediff(h);
                    if (curse != null && !p.health.hediffSet.HasHediff(curse)) p.health.AddHediff(curse);
                }
                else
                {
                    if (curse != null && p.health.hediffSet.GetFirstHediffOfDef(curse) is Hediff c) p.health.RemoveHediff(c);
                    if (hand != null && !p.health.hediffSet.HasHediff(hand)) p.health.AddHediff(hand);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods in the world: the trickster's turn", e);
                return;
            }
            gift.cursed = !gift.cursed;
            gift.turns++;
            gift.turnedAt = Now;
            GodWorld.Moment(g, p.Map, PresenceKind.Act, p.Position, null, GodWorld.T(gift.cursed ? "CC_GW_Trick_CurseLabel" : "CC_GW_Trick_BackLabel", g, p.Named("PAWN")),
                GodWorld.Voice(GodWorld.T(gift.cursed ? "CC_GW_Trick_Curse" : "CC_GW_Trick_Back", g, p.Named("PAWN"), (gift.label ?? "").Named("GIFT"))), false, null, new LookTargets(p));
            GodWorld.Say(p, gift.cursed ? "trickcurse" : "trickback", g);
        }

        // ================= A rival comes to undo it =================

        internal static void Rival(GodWorldBook b, GodGift gift)
        {
            // Only what can be taken back: a blessing on the body, a beast, a gifted trait (a craft learned stays learned).
            if (gift.undoTried || gift.kind != GodGiftKind.Hand && gift.kind != GodGiftKind.Beast && gift.kind != GodGiftKind.Mana || !gift.pawn.Spawned || !Homes.IsHome(gift.pawn.Map)) return;
            int days = (Now - gift.since) / GenDate.TicksPerDay;
            if (days < GodWorldMath.UndoAfterDays || days > GodWorldMath.UndoUntilDays) return;
            var g = Gods.ById(gift.god);
            var r = g == null ? null : Gods.Rivals(g).Where(x => Gods.Known(x) && x.kind != GodKind.Dead).FirstOrDefault(x => x.factions.Any(Arrivals.Usable));
            if (r == null || !GodWorld.Roll(GodWorldMath.UndoDayChance * GodWorld.Factor(true))) return;
            gift.undoTried = true;
            var map = gift.pawn.Map;
            var f = r.factions.First(Arrivals.Usable);
            var avatar = GodWorld.MakeAvatar(r, f, Gods.Realness == GodRealness.Felt);
            if (avatar == null || Arrivals.Visit(new List<Pawn> { avatar }, map, f, null, 12f) == null)
            {
                if (avatar != null) Arrivals.Discard(avatar);
                return;
            }
            var d = b.Add(new GodDoing { kind = "undo", god = r.id, other = g.id, home = Homes.KeyOf(map), pawn = avatar, who = gift.pawn, count = gift.id, faction = f, until = Now + GenDate.TicksPerDay });
            GodWorld.Moment(r, map, PresenceKind.Appear, avatar.Position, avatar, GodWorld.T("CC_GW_Undo_ComesLabel", r, gift.pawn.Named("PAWN")),
                GodWorld.Voice(GodWorld.T("CC_GW_Undo_Comes", r, gift.pawn.Named("PAWN"), g.name.Named("OTHER"), (gift.label ?? "").Named("GIFT"))), false, null, new LookTargets(avatar));
            // The rival's question comes when it comes: it is at the gate now.
            GodAsks.Ask(GodAsks.Undo, map, r, GodWorld.T("CC_GW_Undo_Label", r, gift.pawn.Named("PAWN")),
                GodWorld.Voice(GodWorld.T("CC_GW_Undo_Text", r, gift.pawn.Named("PAWN"), g.name.Named("OTHER"), (gift.label ?? "").Named("GIFT"))), new LookTargets(avatar), g, f,
                gift.pawn, gift.id, null, d.id, 1f);
        }

        private static GodGift GiftById(int id) => GodWorld.Book?.gifts.FirstOrDefault(x => x.id == id);

        public static IEnumerable<GodAnswer> UndoAnswers(ChoiceLetter_GodWorld l)
        {
            var d = l.Doing;
            var gift = GiftById(l.count);
            if (d == null || d.stage != 0 || gift == null || !gift.Holds || !PeoplesG.Up(d.pawn, d.Map)) yield break;
            yield return new GodAnswer("CC_GW_Undo_LetGo".Translate((gift.label ?? "").Named("GIFT")).Resolve(), x => UndoAnswer(x, 1));
            yield return new GodAnswer("CC_GW_Undo_SendOff".Translate().Resolve(), x => UndoAnswer(x, 2));
        }

        private static void UndoAnswer(ChoiceLetter_GodWorld l, int how)
        {
            var d = l.Doing;
            var gift = GiftById(l.count);
            var r = Gods.ById(d?.god ?? -1);
            var g = Gods.ById(d?.other ?? -1);
            if (d == null || gift == null) return;
            var map = d.Map;
            if (how == 2)
            {
                d.done = true;
                if (r != null) Gods.AddFavour(r, map, GodWorldMath.UndoSentOff, GodWorld.T("CC_GW_Undo_WhySent", r, gift.pawn.Named("PAWN")));
                if (g != null) Gods.AddFavour(g, map, GodWorldMath.UndoStood, GodWorld.T("CC_GW_Undo_WhyStood", g, gift.pawn.Named("PAWN"), (r?.name ?? "?").Named("OTHER")));
                if (r != null) GodHealth.Grudge(r, gift.pawn, map, "undo");
                Arrivals.SendAway(new[] { d.pawn });
                return;
            }
            // The avatar walks to them; when it reaches them the gift is gone (the hour sees to it).
            d.stage = 1;
            d.until = Now + GenDate.TicksPerDay / 2;
        }

        private static void UndoHour(GodDoing d)
        {
            var map = d.Map;
            var gift = GiftById(d.count);
            if (!PeoplesG.Up(d.pawn, map) || gift == null || !gift.Holds || Now > d.until)
            {
                d.done = true;
                if (PeoplesG.Up(d.pawn, map)) Arrivals.SendAway(new[] { d.pawn });
                return;
            }
            if (d.stage != 1) return;
            var p = gift.pawn;
            if (!p.Spawned || p.Map != map) return;
            if (!d.pawn.Position.InHorDistOf(p.Position, 2f))
            {
                if (d.pawn.CurJobDef != JobDefOf.Goto)
                {
                    var job = JobMaker.MakeJob(JobDefOf.Goto, p.Position);
                    job.locomotionUrgency = LocomotionUrgency.Walk;
                    d.pawn.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                }
                return;
            }
            d.done = true;
            Undo(gift);
            var r = Gods.ById(d.god);
            var g = Gods.ById(d.other);
            if (r != null) Gods.AddFavour(r, map, GodWorldMath.UndoLetGo, GodWorld.T("CC_GW_Undo_WhyLetGo", r, p.Named("PAWN")));
            if (g != null) Gods.AddFavour(g, map, GodWorldMath.UndoGiverLoss, GodWorld.T("CC_GW_Undo_WhyLost", g, p.Named("PAWN"), (r?.name ?? "?").Named("OTHER")));
            FleckMaker.ThrowMetaIcon(p.Position, map, FleckDefOf.PsycastAreaEffect);
            if (r != null)
                GodWorld.Moment(r, map, PresenceKind.Act, p.Position, d.pawn, GodWorld.T("CC_GW_Undo_DoneLabel", r, p.Named("PAWN")),
                    GodWorld.Voice(GodWorld.T("CC_GW_Undo_Done", r, p.Named("PAWN"), (gift.label ?? "").Named("GIFT"))), true, LetterDefOf.NegativeEvent, new LookTargets(p));
            Arrivals.SendAway(new[] { d.pawn });
        }

        /// <summary>A gift taken back: its blessing on the body, the beast's bond (it goes wild), or the gifted trait.</summary>
        private static void Undo(GodGift gift)
        {
            var p = gift.pawn;
            gift.undone = true;
            try
            {
                switch (gift.kind)
                {
                    case GodGiftKind.Hand:
                        var hd = DefDatabase<HediffDef>.GetNamedSilentFail(gift.what);
                        if (hd != null && p.health.hediffSet.GetFirstHediffOfDef(hd) is Hediff h) p.health.RemoveHediff(h);
                        var curse = DefDatabase<HediffDef>.GetNamedSilentFail("Rimfolk_GodCurse");
                        if (curse != null && p.health.hediffSet.GetFirstHediffOfDef(curse) is Hediff c) p.health.RemoveHediff(c);
                        break;
                    case GodGiftKind.Beast:
                        if (gift.thing is Pawn beast && !beast.Dead && beast.Spawned)
                        {
                            p.relations?.TryRemoveDirectRelation(PawnRelationDefOf.Bond, beast);
                            beast.SetFaction(null);
                        }
                        break;
                    case GodGiftKind.Mana:
                        var td = DefDatabase<TraitDef>.GetNamedSilentFail(gift.what);
                        var trait = td == null ? null : p.story?.traits?.GetTrait(td);
                        if (trait != null) p.story.traits.RemoveTrait(trait);
                        break;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods in the world: a gift undone", e);
            }
        }
    }
}
