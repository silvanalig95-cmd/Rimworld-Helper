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
    /// a colonist, spaced with theirs: PeopleEventsBook). A gift and its price arrive without explanation:
    /// <list type="bullet">
    /// <item>A god the colony stands favored with now and then chooses one of its people (GodWays: whom, what, for what):
    /// they tell a dream at breakfast (the god unnamed, in its own shape: a woman of green leaves, a warm voice in the rock,
    /// a white beast's calm eye), and what it asked of them: to wait a night under the open sky, and for some, an offering
    /// left past the gate. The colony lets them go or tells them it was only a dream.</item>
    /// <item>Let go, they carry out the offering (silver, meals, raw food, components, as the god takes it) and wait the
    /// night out under the sky. By morning they have changed, and nobody says why: Hollin's Druid's craft or a green hand,
    /// Smoulder's gold beside them, a passion found after Bellwether, Ennu's blessing on the kindest, Pip's Technomancer's
    /// craft, Cirrus's Storm Born's, Hush's Heart of Frost's or winter in the blood, Morel's Necromancer's craft or her soft
    /// hand (A RimWorld of Magic's classes, spells and gifted traits, Vanilla Psycasts Expanded's paths, the colony faith's
    /// lost relic). Some come back marked, or older, or cut on the arm (the price, under the harm budget for blood), or the
    /// gods it quarrels with turn colder. Undertow gives nothing this way: its gifts are its bargains (GodVoid).</item>
    /// <item>A god that quarrels with the giver may want the gift back: the one who has it tells a second, colder dream,
    /// and the colony leaves an offering past the gate, or lets the gift go.</item>
    /// </list>
    /// Only where the gods are more than believed (Felt and Walking).
    /// </summary>
    public static class GodWorldGifts
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
                    yield return (t, Gods.OfIdeo(ideo).FirstOrDefault(), ideo, map);
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
            if (!ModsConfig.IdeologyActive || g == null || Faction.OfPlayer?.ideos == null) return null;
            foreach (var ideo in Faction.OfPlayer.ideos.AllIdeos)
            {
                if (ideo == null) continue;
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

        /// <summary>
        /// The colonist a god would give to: grown, up and well, no gift of its kind yet; whom its ways say (the kindest, the
        /// youngest, the oldest, a night owl, the most sensitive, anyone at all, the best at a skill), else one of its own faith first, then the best at its domain's work.
        /// </summary>
        private static Pawn Chosen(God g, Map map)
        {
            string chooses = GodWays.Chooses(g);
            var skill = chooses != null ? DefDatabase<SkillDef>.GetNamedSilentFail(chooses) : SkillOf(g.domain);
            float Score(Pawn p)
            {
                switch (chooses)
                {
                    case "kindest": return Kindest(p);
                    case "youngest": return -p.ageTracker.AgeBiologicalYearsFloat;
                    case "oldest": return p.ageTracker.AgeBiologicalYearsFloat;
                    // Whom she likes, and nobody knows why (Mog).
                    case "any": return Rand.ValueSeeded(p.thingIDNumber ^ GenDate.DaysPassed / 15);
                    // One who keeps the night watch: a night owl first (Selene).
                    case "nightowl": return (p.story?.traits?.HasTrait(DefDatabase<TraitDef>.GetNamedSilentFail("NightOwl")) == true ? 100f : 0f) + Rand.ValueSeeded(p.thingIDNumber);
                    case "sensitive": return p.GetStatValue(StatDefOf.PsychicSensitivity) * 10f;
                }
                return skill != null ? p.skills?.GetSkill(skill)?.Level ?? 0 : p.GetStatValue(StatDefOf.PsychicSensitivity) * 10f;
            }
            return GodWorld.Grown(map).Where(p => !p.InMentalState && PeopleEvents.MayGive(GiftKey(g), p))
                .OrderByDescending(p => chooses == null && Gods.SwearBy(p) == g ? 1 : 0).ThenByDescending(Score).FirstOrDefault();
        }

        /// <summary>The kindest score: the Kind trait first, then the best at talk.</summary>
        private static float Kindest(Pawn p)
        {
            var kind = DefDatabase<TraitDef>.GetNamedSilentFail("Kind");
            return (kind != null && p.story?.traits?.HasTrait(kind) == true ? 100f : 0f) + (p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0);
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

        /// <summary>A skill they have no passion in, their best of those (Bellwether's: a passion found).</summary>
        private static SkillRecord PassionFor(Pawn p) =>
            p.skills?.skills?.Where(sk => sk != null && !sk.TotallyDisabled && sk.passion == Passion.None).OrderByDescending(sk => sk.Level).FirstOrDefault();

        /// <summary>Gold for silver: the god's offering must be silver, and gold in the game.</summary>
        private static bool HoardOpen(God g) => ThingDefOf.Gold != null && GodWays.Offering(g) == "silver";

        private static bool PathOpen(Pawn p) =>
            ModsConfig.RoyaltyActive && DefDatabase<HediffDef>.GetNamedSilentFail("VPE_PsycastAbilityImplant") != null && p.GetMainPsylinkSource() == null
            && p.health?.hediffSet?.GetBrain() != null;

        /// <summary>What the god would give this colonist now, and the def it comes as (null: nothing).</summary>
        private static (GodGiftKind kind, string what, string label) Pick(God g, Pawn p)
        {
            var hand = GodWays.Gives(g, GodGiftKind.Hand) ? HandDef(g) : null;
            bool hasHand = hand != null && !p.health.hediffSet.HasHediff(hand);
            var beast = GodWays.Gives(g, GodGiftKind.Beast) ? BeastKind(g) : null;
            string cls = GodWays.Gives(g, GodGiftKind.Class) ? ClassFor(g, p) : null;
            var spell = GodWays.Gives(g, GodGiftKind.Spell) ? SpellFor(g, p) : null;
            var mana = GodWays.Gives(g, GodGiftKind.Mana) ? ManaFor(p) : null;
            bool path = GodWays.Gives(g, GodGiftKind.Path) && PathOpen(p) && GodWays.Paths(g).Count > 0;
            var lost = GodWays.Gives(g, GodGiftKind.Relic) ? LostRelic(g) : null;
            var passion = GodWays.Gives(g, GodGiftKind.Passion) ? PassionFor(p) : null;
            bool hoard = GodWays.Gives(g, GodGiftKind.Hoard) && HoardOpen(g);
            var kind = GodWorldMath.PickGift(hasHand, beast != null, cls != null, spell != null, mana != null, path, lost != null, passion != null, hoard, Rand.Value);
            switch (kind)
            {
                case GodGiftKind.Hand: return (kind, hand.defName, hand.label);
                case GodGiftKind.Beast: return (kind, beast.defName, "CC_GW_Gift_Beast".Translate(beast.label.Named("BEAST")).Resolve());
                case GodGiftKind.Class: return (kind, cls, "CC_GW_Gift_Class".Translate(DefDatabase<TraitDef>.GetNamed(cls).degreeDatas[0].label.Named("CLASS")).Resolve());
                case GodGiftKind.Spell: return (kind, spell.defName, "CC_GW_Gift_Spell".Translate(spell.label.Named("SPELL")).Resolve());
                case GodGiftKind.Mana: return (kind, mana.defName, "CC_GW_Gift_Mana".Translate(mana.degreeDatas[0].label.Named("TRAIT")).Resolve());
                case GodGiftKind.Path: return (kind, null, "CC_GW_Gift_Path".Translate().Resolve());
                case GodGiftKind.Relic: return (kind, "precept:" + lost.Id, "CC_GW_Gift_LostRelic".Translate(lost.LabelCap.Named("RELIC")).Resolve());
                case GodGiftKind.Passion: return (kind, passion.def.defName, "CC_GW_Gift_Passion".Translate(passion.def.label.Named("SKILL")).Resolve());
                case GodGiftKind.Hoard: return (kind, ThingDefOf.Gold.defName, "CC_GW_Gift_Hoard".Translate().Resolve());
                default: return (GodGiftKind.None, null, null);
            }
        }

        /// <summary>How many of its offering a gift's price is, by the gift's worth in silver: silver itself, meals, raw food, or a thing by its worth.</summary>
        public static int OfferingCount(God g, int silver)
        {
            string what = GodWays.Offering(g);
            switch (what)
            {
                case "silver": return silver;
                case "meals": return Mathf.Max(3, silver / 30);
                case "rawfood": return Mathf.Max(20, silver / 2);
            }
            var def = PlayCarry.DefOf(what, null);
            return def == null ? silver : Mathf.Max(1, Mathf.RoundToInt(silver / Mathf.Max(1f, def.BaseMarketValue)));
        }

        // ================= The day: offers, and the rival =================

        public static void Daily(GodWorldBook b, int now)
        {
            GodWorld.Safe("a gift offered", () => Offer(b));
            foreach (var gift in b.gifts.Where(x => x.Holds).ToList())
            {
                GodWorld.Safe("a rival's coming", () => Rival(b, gift));
            }
        }

        internal static void Offer(GodWorldBook b)
        {
            if (!GodWorld.CanAsk || !b.Gap("gift", GodWorldMath.GiftGap) || !PeopleEventsBook.GiftOpen) return;
            foreach (var map in Homes.All.ToList())
            {
                // The Hunger gives only by its bargains (GodVoid).
                foreach (var g in GodWorld.Known.Where(x => x.kind != GodKind.Hunger).ToList())
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
                    // Smoulder's old way: the silver thrown to him is the price, gold the gift.
                    if (kind == GodGiftKind.Hoard) price = GodPrice.Offering;
                    int offering = OfferingCount(g, silver);
                    int years = GodWorldMath.PriceYears(g.kind, Rand.Value);
                    var d = b.Add(new GodDoing
                    {
                        kind = "offer", god = g.id, pawn = p, home = Homes.KeyOf(map), what = kind + "|" + what + "|" + label, stage = (int)price,
                        count = price == GodPrice.Years ? years : offering, until = Now + 3 * GenDate.TicksPerDay
                    });
                    // A dream told at breakfast: the god unnamed, in its own shape; what it asked of the dreamer, in their words.
                    string offered = GodWays.Offering(g);
                    string ask = price == GodPrice.Offering
                        ? "CC_GW_Dream_AskOffering".Translate(p.Named("PAWN"), offering.Named("COUNT"), PlayCarry.Label(offered, PlayCarry.DefOf(offered, null)).Named("WHAT")).Resolve()
                        : "CC_GW_Dream_AskSky".Translate(p.Named("PAWN")).Resolve();
                    // A dream that comes true tells the gift (Selene's).
                    if (GodWays.TrueDreams(g)) ask = "CC_GW_Dream_True".Translate(p.Named("PAWN"), label.Named("GIFT")).Resolve() + " " + ask;
                    string text = "CC_GW_Dream_Text".Translate(p.Named("PAWN"), GodWays.Dream(g).Named("DREAM"), ask.Named("ASK")).Resolve();
                    if (GodAsks.Ask(GodAsks.Gift, map, g, "CC_GW_Dream_Label".Translate(p.Named("PAWN")).Resolve(), text, new LookTargets(p), null, null, p, d.count, d.what, d.id, 2f) == null)
                    {
                        d.done = true;
                        continue;
                    }
                    b.Came("gift");
                    GodWorld.Remember(g, map, GodActKind.Omen, "CC_GW_Dream_Act".Translate(p.Named("PAWN"), GodWays.Dream(g).Named("DREAM")).Resolve());
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
            string what = GodWays.Offering(g);
            var whatDef = PlayCarry.DefOf(what, null);
            if (price == GodPrice.Offering && PlayCarry.Have(d.Map, what, whatDef) < d.count)
                disabled = "CC_Dilemma_NotEnough".Translate(d.count, PlayCarry.Label(what, whatDef)).ToString();
            if (price == GodPrice.Blood && !Harm.May(d.pawn)) disabled = "CC_GW_Dream_NotNow".Translate(d.pawn.Named("PAWN")).Resolve();
            yield return new GodAnswer((price == GodPrice.Offering ? "CC_GW_Dream_GoOffering" : "CC_GW_Dream_Go").Translate(d.pawn.Named("PAWN")).Resolve(), Accept, disabled);
            yield return new GodAnswer("CC_GW_Dream_Only".Translate(d.pawn.Named("PAWN")).Resolve(), Decline);
        }

        private static void Decline(ChoiceLetter_GodWorld l)
        {
            var d = l.Doing;
            var g = l.God;
            if (d == null || g == null) return;
            d.done = true;
            float by = GodWorldMath.GiftRefused(g.nature);
            if (by != 0f) Gods.AddFavour(g, d.Map, by, GodWorld.T("CC_GW_Dream_WhyDeclined", g, d.pawn.Named("PAWN")));
        }

        /// <summary>The offering carried out (or the rivals' anger taken on), then they walk out under the sky and wait the night; the rest of the price is paid in the night, with the gift.</summary>
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
                        // apart: one of the colony carries what the dream asked for out past the gate and leaves it there; by morning it is gone.
                        new DilemmaEffect_CarryOut
                            {
                                what = GodWays.Offering(g), count = d.count, steward = false, who = PlayWho.None, to = PlaySpot.Gate,
                                label = "CC_GW_Dream_CarryLabel".Translate(p.Named("PAWN")).Resolve()
                            }
                            .Apply(new DilemmaContext { map = map });
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
        }

        /// <summary>The price paid in the night, without a word: a mark, years, a cut on the arm. The words for what is seen in the morning, or null.</summary>
        private static string Pay(GodDoing d, Pawn p)
        {
            try
            {
                switch ((GodPrice)d.stage)
                {
                    case GodPrice.Mark:
                        var mark = DefDatabase<HediffDef>.GetNamedSilentFail("Rimfolk_GodMark");
                        if (mark == null || p.health.hediffSet.HasHediff(mark)) return null;
                        p.health.AddHediff(mark);
                        return "CC_GW_Paid_Mark".Translate(p.Named("PAWN")).Resolve();
                    case GodPrice.Years:
                        p.ageTracker.AgeBiologicalTicks += (long)d.count * GenDate.TicksPerYear;
                        return "CC_GW_Paid_Years".Translate(p.Named("PAWN")).Resolve();
                    case GodPrice.Blood:
                        if (!Harm.May(p)) return null;
                        Harm.Injure(p, HurtKind.Cut, HurtWhere.Arm, 3f, 6f, HarmKinds.GodPrice);
                        return "CC_GW_Paid_Blood".Translate(p.Named("PAWN")).Resolve();
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods in the world: a gift's price", e);
            }
            return null;
        }

        private static void Give(GodWorldBook b, GodDoing d)
        {
            var g = Gods.ById(d.god);
            var p = d.pawn;
            var map = p.Map;
            if (g == null) return;
            var (kind, what, label) = Parse(d.what);
            string paid = Pay(d, p);
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
                    case GodGiftKind.Passion:
                        var sk = p.skills?.GetSkill(DefDatabase<SkillDef>.GetNamedSilentFail(what ?? ""));
                        if (sk != null && !sk.TotallyDisabled && sk.passion == Passion.None)
                        {
                            sk.passion = Passion.Minor;
                            ok = true;
                        }
                        break;
                    case GodGiftKind.Hoard:
                        int gold = GodWorldMath.HoardGold(GodWorldMath.PriceSilver(GodGiftKind.Hoard, GodWorld.Wealth(map)), ThingDefOf.Gold.BaseMarketValue);
                        int left = gold;
                        while (left > 0)
                        {
                            var t = ThingMaker.MakeThing(ThingDefOf.Gold);
                            t.stackCount = Mathf.Min(left, ThingDefOf.Gold.stackLimit);
                            left -= t.stackCount;
                            GenPlace.TryPlaceThing(t, p.Position, map, ThingPlaceMode.Near);
                            thing ??= t;
                        }
                        ok = gold > 0;
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
                string nothing = "CC_GW_Gift_Nothing".Translate(p.Named("PAWN")).Resolve();
                if (paid != null) nothing += " " + paid;
                ColonyCouncil.Tell.Message(NewsKind.People, Importance.Notable, map, nothing, new LookTargets(p), MessageTypeDefOf.NeutralEvent);
                return;
            }
            PeopleEventsBook.Gave(GiftKey(g), p);
            b.AddGift(new GodGiftHeld { god = g.id, pawn = p, kind = kind, what = what, label = label, thing = thing, price = (GodPrice)d.stage });
            Gods.AddFavour(g, map, GodWorldMath.GiftTaken, GodWorld.T("CC_GW_Gift_Why", g, p.Named("PAWN"), label.Named("GIFT")));
            FleckMaker.ThrowMetaIcon(p.Position, map, FleckDefOf.PsycastAreaEffect);
            // What is seen in the morning, and nothing of why.
            string text = "CC_GW_Gift_Text".Translate(p.Named("PAWN")).Resolve() + " " + ("CC_GW_Morning_" + kind).Translate(p.Named("PAWN"), label.Named("GIFT")).Resolve();
            if (paid != null) text += " " + paid;
            GodWorld.Moment(g, map, PresenceKind.Blessing, p.Position, null, "CC_GW_Gift_Label".Translate(p.Named("PAWN")).Resolve(), text, true, LetterDefOf.NeutralEvent,
                thing != null && thing.Spawned ? new LookTargets(new Thing[] { p, thing }) : new LookTargets(p));
            GodWorld.Remark(map, p.Position, "gift", g, p, p.Named("GIFTED"));
            GodWorld.Chronicle("CC_GW_Gift_Chronicle".Translate(p.Named("PAWN"), label.Named("GIFT")).Resolve(), p);
        }

        /// <summary>The god's relic: the colony faith's own lost relic (Ideology), found again.</summary>
        private static Thing MakeRelic(God g, string what)
        {
            if (what == null || !what.StartsWith("precept:", StringComparison.Ordinal)) return null;
            string id = what.Substring("precept:".Length);
            var pr = Find.IdeoManager?.IdeosListForReading.SelectMany(i => i.PreceptsListForReading.OfType<Precept_Relic>()).FirstOrDefault(x => x.Id.ToString() == id && x.CanGenerateRelic);
            return pr?.GenerateRelic();
        }

        // ================= A rival comes to undo it =================

        /// <summary>
        /// A god that quarrels with the giver wants the gift back, and the one who has it tells a second, colder dream (the god
        /// unnamed, in its own shape): the colony leaves its offering past the gate, or lets the gift go. Only what can be
        /// taken back: a blessing on the body, a beast, a gifted trait.
        /// </summary>
        internal static void Rival(GodWorldBook b, GodGiftHeld gift)
        {
            if (gift.undoTried || gift.kind != GodGiftKind.Hand && gift.kind != GodGiftKind.Beast && gift.kind != GodGiftKind.Mana || !gift.pawn.Spawned
                || !Homes.IsHome(gift.pawn.Map))
                return;
            int days = (Now - gift.since) / GenDate.TicksPerDay;
            if (days < GodWorldMath.UndoAfterDays || days > GodWorldMath.UndoUntilDays) return;
            var g = Gods.ById(gift.god);
            var r = g == null ? null : Gods.Rivals(g).FirstOrDefault(x => Gods.Known(x) && x.kind != GodKind.Hunger);
            if (r == null || !GodWorld.Roll(GodWorldMath.UndoDayChance * GodWorld.Factor(true))) return;
            gift.undoTried = true;
            var map = gift.pawn.Map;
            int count = OfferingCount(r, GodWorldMath.PriceSilver(GodGiftKind.Hand, GodWorld.Wealth(map)));
            string what = GodWays.Offering(r);
            var d = b.Add(new GodDoing { kind = "undo", god = r.id, other = g.id, home = Homes.KeyOf(map), pawn = gift.pawn, count = gift.id, stage = count, until = Now + GenDate.TicksPerDay });
            GodAsks.Ask(GodAsks.Undo, map, r, "CC_GW_Undo_Label".Translate(gift.pawn.Named("PAWN")).Resolve(),
                "CC_GW_Undo_Text".Translate(gift.pawn.Named("PAWN"), GodWays.Dream(r).Named("DREAM"), (gift.label ?? "").Named("GIFT"), count.Named("COUNT"),
                    PlayCarry.Label(what, PlayCarry.DefOf(what, null)).Named("WHAT")).Resolve(), new LookTargets(gift.pawn), g, null, gift.pawn, gift.id, null, d.id, 1f);
        }

        private static GodGiftHeld GiftById(int id) => GodWorld.Book?.gifts.FirstOrDefault(x => x.id == id);

        public static IEnumerable<GodAnswer> UndoAnswers(ChoiceLetter_GodWorld l)
        {
            var d = l.Doing;
            var gift = GiftById(l.count);
            var r = l.God;
            if (d == null || gift == null || !gift.Holds || r == null) yield break;
            string what = GodWays.Offering(r);
            var def = PlayCarry.DefOf(what, null);
            yield return new GodAnswer("CC_GW_Undo_Pay".Translate(d.stage.Named("COUNT"), PlayCarry.Label(what, def).Named("WHAT")).Resolve(), x => UndoAnswer(x, 2),
                PlayCarry.Have(d.Map, what, def) < d.stage ? "CC_Dilemma_NotEnough".Translate(d.stage, PlayCarry.Label(what, def)).ToString() : null);
            yield return new GodAnswer("CC_GW_Undo_LetGo".Translate((gift.label ?? "").Named("GIFT")).Resolve(), x => UndoAnswer(x, 1));
        }

        private static void UndoAnswer(ChoiceLetter_GodWorld l, int how)
        {
            var d = l.Doing;
            var gift = GiftById(l.count);
            var r = Gods.ById(d?.god ?? -1);
            var g = Gods.ById(d?.other ?? -1);
            if (d == null || gift == null || r == null) return;
            d.done = true;
            var map = d.Map;
            var p = gift.pawn;
            if (how == 2)
            {
                // apart: the offering is carried out past the gate and left for the god that came for the gift.
                new DilemmaEffect_CarryOut
                    {
                        what = GodWays.Offering(r), count = d.stage, steward = false, who = PlayWho.None, to = PlaySpot.Gate,
                        label = "CC_GW_Dream_CarryLabel".Translate(p.Named("PAWN")).Resolve()
                    }
                    .Apply(new DilemmaContext { map = map });
                Gods.AddFavour(r, map, GodWorldMath.UndoPaid, GodWorld.T("CC_GW_Undo_WhyPaid", r, p.Named("PAWN")), FavourSource.Offering);
                if (g != null) Gods.AddFavour(g, map, GodWorldMath.UndoStood, GodWorld.T("CC_GW_Undo_WhyStood", g, p.Named("PAWN"), r.name.Named("OTHER")));
                return;
            }
            Undo(gift);
            Gods.AddFavour(r, map, GodWorldMath.UndoLetGo, GodWorld.T("CC_GW_Undo_WhyLetGo", r, p.Named("PAWN")));
            if (g != null) Gods.AddFavour(g, map, GodWorldMath.UndoGiverLoss, GodWorld.T("CC_GW_Undo_WhyLost", g, p.Named("PAWN"), r.name.Named("OTHER")));
            if (p.Spawned) FleckMaker.ThrowMetaIcon(p.Position, p.Map, FleckDefOf.PsycastAreaEffect);
            GodWorld.Moment(r, map, PresenceKind.Act, p.Spawned ? p.Position : IntVec3.Invalid, null, "CC_GW_Undo_DoneLabel".Translate(p.Named("PAWN")).Resolve(),
                "CC_GW_Undo_Done".Translate(p.Named("PAWN"), (gift.label ?? "").Named("GIFT")).Resolve(), true, LetterDefOf.NegativeEvent, p.Spawned ? new LookTargets(p) : null);
        }

        /// <summary>A gift taken back: its blessing on the body, the beast's bond (it goes wild), or the gifted trait.</summary>
        private static void Undo(GodGiftHeld gift)
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
