using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // S8, the children from the burned village (docs/quests-proposals.md; words in Defs/MissionDefs/Rimfolk_Storyline_Children.xml).
    // Children of a people at peace with the colony, whose small village burned down, come to the gate with one grown-up of
    // theirs. Part 1: they are hosted for a few days (eating at the colony's table and sleeping in its beds, as every hosting
    // mission: Missions/Hosting.cs), then a card asks what becomes of them: taken in (they join the colony as colonists;
    // the colony is the family, so nothing more is tied to it), a home found with neighbours (whose people come to the gate
    // for them), or sent on with food (carried out to them). Part 2, two to four seasons later, the eldest comes back grown:
    // to visit with a gift, to trade, or to ask to join. Needs Biotech (children).

    /// <summary>The children's storyline beyond its parts: who asks, the neighbours, the eldest, its endings.</summary>
    public class ChildrenRules : StorylineRules
    {
        /// <summary>Any people at peace with the colony that has people to send (children exist with Biotech).</summary>
        public override Faction Giver(Map map) => ModsConfig.BiotechActive ? Arrivals.Friendly(VarietyUtil.OnGoodTerms) : null;

        /// <summary>The neighbours who give the children a home: another people at peace, else another town of their own people.</summary>
        public static Faction Neighbours(Faction giver) => Arrivals.Friendly(f => f != giver && VarietyUtil.OnGoodTerms(f)) ?? giver;

        /// <summary>The eldest child (kept first), the one who comes back.</summary>
        public static Pawn Eldest(Storyline s) => s.group.FirstOrDefault();

        /// <summary>Once the children have gone on, their own people no longer matter: it is the eldest who comes back.</summary>
        public override bool NeedsGiver(Storyline s) => !(s.Has("place") || s.Has("sendon"));

        /// <summary>Waiting for the eldest: if they no longer can come (dead, their people gone or at war), it ends without them.</summary>
        public override void Hourly(Storyline s, Map map, int now)
        {
            if (s.missionId >= 0 || s.next != "back") return;
            var e = Eldest(s);
            if (e == null || e.Dead || e.Discarded || e.Faction == null || e.Faction.defeated || e.Faction.HostileTo(Faction.OfPlayer))
                Storylines.End(s, StorylineMath.Faded, map);
        }

        /// <summary>Their people are at war with the colony: the children and their grown-up, if here, go on.</summary>
        public override void Gone(Storyline s, Map map)
        {
            var here = new[] { s.x }.Concat(s.group).Where(p => p != null && p.Spawned && p.Map == map && p.Faction != Faction.OfPlayer).ToList();
            if (here.Count > 0) Arrivals.SendAway(here);
        }

        /// <summary>The eldest, grown: at least the game's adult age when they come back.</summary>
        public static void Grown(Pawn e)
        {
            if (e?.ageTracker == null || e.ageTracker.AgeBiologicalYearsFloat >= StorylineMath.GrownAge) return;
            long ticks = (long)((StorylineMath.GrownAge + Rand.Range(0.05f, 0.4f)) * GenDate.TicksPerYear);
            e.ageTracker.AgeBiologicalTicks = ticks;
            if (e.ageTracker.AgeChronologicalTicks < ticks) e.ageTracker.AgeChronologicalTicks = ticks;
        }

        /// <summary>The goods of a trader of their people in their pack, so the colony can trade with them at the gate. False: their people don't trade.</summary>
        public static bool MakeTrader(Pawn p, Map map)
        {
            var f = p?.Faction;
            var kind = f?.def?.caravanTraderKinds?.RandomElementByWeightWithFallback(t => t.CalculatedCommonality);
            if (kind == null) return false;
            p.mindState.wantsToTradeWithColony = true;
            PawnComponentsUtility.AddAndRemoveDynamicComponents(p, true);
            if (p.trader == null) return false;
            p.trader.traderKind = kind;
            var stock = ThingSetMakerDefOf.TraderStock.root.Generate(new ThingSetMakerParams { traderDef = kind, tile = map.Tile, makingFaction = f });
            foreach (var t in stock)
            {
                // Beasts for sale come with a caravan, not with one person on foot.
                if (t is Pawn) continue;
                if (!p.inventory.innerContainer.TryAdd(t) && !t.Destroyed) t.Destroy();
            }
            return true;
        }
    }

    // ===================== Part 1: shelter the children =====================

    /// <summary>
    /// Two to four children and one grown-up of theirs stay three or four days, fed at the colony's table and sleeping in
    /// its beds (a hosting mission: Missions/Workers_MissionRemembered.cs); then the grown-up asks what becomes of them (the
    /// card), and the answer plays out before the part ends: they join, their new people come for them, or food is carried
    /// out to them and they go on.
    /// </summary>
    public class HappeningWorker_ChildrenShelter : HappeningWorker_MissionHost, IStorylinePartWorker
    {
        public const string Card = "Rimfolk_Storyline_ChildrenChoice";

        public StorylinePart P => def.GetModExtension<StorylinePart>();

        protected override string Kind => "storyline:" + P?.story + ":" + P?.part;

        protected override float RestDays => 0f;

        protected override int Rating => 2;

        public bool CanOffer(IncidentParms parms) => CanFireNowSub(parms);

        protected override bool CanHappen(Map map, HappeningExtension ext) => StorylineKit.CanHappen(P, map, Possible);

        protected static Storyline Story(Mission m) => (m != null ? Storylines.OfMission(m.id) : null) ?? StorylineKit.Offering;

        protected override bool Possible(Map map) => Storylines.Rules(StorylineMath.Children).Giver(map) != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            StorylineKit.Cast(P, ctx.map);
            return base.Cast(ctx, ext);
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) => base.Begin(ctx, ext) && StorylineKit.Begin(P, ctx);

        protected override void Abort(HappeningContext ctx)
        {
            base.Abort(ctx);
            StorylineKit.Abort(P);
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            base.AddTags(ctx, tags);
            StorylineKit.AddTags(Missions.Of(ctx) ?? Making, tags);
        }

        protected override string Extra(HappeningContext ctx)
        {
            var m = Missions.Of(ctx) ?? Making;
            return StorylineKit.Terms(P, ctx, m, null);
        }

        protected override void Send(HappeningContext ctx, HappeningExtension ext) => StorylineKit.Send(this, ctx, ext, Rating, Extra(ctx), Look(ctx));

        private static IEnumerable<Pawn> Children(Mission m) => m.group.Where(p => p != null);

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var s = Storylines.making;
            var map = ctx.map;
            var f = Storylines.Rules(StorylineMath.Children).Giver(map);
            if (s == null || f == null) return false;
            m.a = VarietyUtil.Best(map, SkillDefOf.Social) ?? StorytellerEvents.Adults(map).RandomElementWithFallback();
            if (m.a == null) return false;
            var kind = Arrivals.PersonKind(f);
            var adult = Make(f, new FloatRange(28f, 55f));
            if (adult == null || kind == null) return false;
            m.x = adult;
            int n = StorylineMath.ChildCount(IncidentWorker_Happening.Colonists(map).Count, Rand.Value);
            for (int i = 0; i < n; i++)
            {
                var age = i == 0 ? new FloatRange(StorylineMath.EldestMinAge, StorylineMath.EldestMaxAge) : new FloatRange(6f, 11.5f);
                Pawn c = null;
                try
                {
                    c = Arrivals.Make(kind, f, age, null, null, true);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("storyline: a child of the burned village", e);
                }
                if (c == null || !c.DevelopmentalStage.Child()) continue;
                Arrivals.Hold(c);
                m.group.Add(c);
            }
            if (m.group.Count == 0)
            {
                Arrivals.Discard(adult);
                return false;
            }
            s.giver = f;
            s.x = adult;
            s.xName = adult.LabelShort;
            s.group.AddRange(m.group);
            s.name = m.group[0].LabelShort;
            s.place = LivingEvents.TownOf(f, map) ?? f.Name;
            s.count = m.group.Count;
            m.faction = f;
            m.place = s.place;
            m.name = s.name;
            m.target = m.group.Count + 1;
            m.wait = Rand.Range(0.4f, 0.8f);
            float stay = Rand.Range(StorylineMath.StayMinDays, StorylineMath.StayMaxDays);
            m.days = m.wait + stay;
            // Each eats two or three meals a day; the children a little less.
            m.goals.Add(new MissionGoal { what = "meals", count = Mathf.CeilToInt(2.2f * m.target * stay) });
            m.goodwill = 15;
            m.penalty = 5;
            Fill(m, ctx);
            ctx.stranger = adult;
            ctx.count = s.count;
            return true;
        }

        public override bool Start(Mission m, Map map)
        {
            foreach (var g in m.goals) g.have = Missions.Count(map, g.what);
            if (!base.Start(m, map)) return false;
            StorylineKit.Accepted(P, Story(m), m, map, null);
            return true;
        }

        private static List<Building_Bed> Beds(Mission m, Map map) => Hosting.Beds(map, o => Ours(m, o), m.x);

        public override void Check(Mission m, Map map, int now)
        {
            var s = Story(m);
            if (s == null)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            foreach (var g in m.goals) g.have = Missions.Count(map, g.what);
            if (m.stage >= 1 && Children(m).Any() && Children(m).Count(c => c.Dead || c.IsPrisonerOfColony) * 2 >= Children(m).Count())
            {
                Missions.Goodwill(m, -m.penalty * 2);
                End(m, MissionState.Failed, "harmed");
                return;
            }
            switch (m.stage)
            {
                case 0:
                {
                    if (now < m.due) return;
                    var spot = Hosting.Spot(Beds(m, map), map);
                    // They stay until the colony has answered and they have gone on: their visit lasts the stay and two days more.
                    float hours = (m.deadline - now) / (float)GenDate.TicksPerHour + 48f;
                    if (!Arrive(m, map, spot.IsValid ? spot : PeoplesD.GateSpot(map), hours))
                    {
                        End(m, MissionState.Failed, "ended");
                        return;
                    }
                    m.stage = 1;
                    m.lastLook = now;
                    m.lacks = 0;
                    StorylineKit.Note(P, s, m, map, "came", Targets(m));
                    return;
                }
                case 1:
                {
                    var here = Here(m, map).ToList();
                    m.lacks |= (int)Tend(m, map);
                    if (now - m.lastLook >= GenDate.TicksPerDay)
                    {
                        m.lastLook += GenDate.TicksPerDay;
                        Day(m, map, here);
                    }
                    if (now < m.deadline && here.Count > 0) return;
                    if (m.lacks != 0 || now - m.lastLook >= GenDate.TicksPerDay / 2) Day(m, map, here);
                    var kids = Children(m).Where(c => !c.Dead && c.Spawned && c.Map == map).ToList();
                    if (kids.Count == 0)
                    {
                        End(m, MissionState.Failed, "ended");
                        return;
                    }
                    s.Set(m.misses == 0 ? "fed" : m.tally >= m.misses ? "partfed" : "hungry");
                    Ask(m, s, map, now, kids.Count);
                    return;
                }
                case 2:
                    // Waiting for the answer, still fed and housed; a card lost without an answer finds them a home.
                    m.lacks |= (int)Tend(m, map);
                    if (!StorylineKit.CardOut(s) && now - m.lastLook > GenDate.TicksPerHour * 2)
                    {
                        if (now - m.lastLook > GenDate.TicksPerDay || !Ask(m, s, map, now, Children(m).Count(c => !c.Dead && c.Spawned)))
                            Answered(m, s, StorylineKit.Context(s, m, map), 1, true);
                    }
                    return;
                case 3:
                {
                    // Their new people are on their way.
                    m.lacks |= (int)Tend(m, map);
                    if (now < m.due) return;
                    var fetchers = new List<Pawn>();
                    var other = s.other ?? s.giver;
                    for (int i = 0; i < 2; i++)
                    {
                        var p = Arrivals.Make(Arrivals.PersonKind(other), other, new FloatRange(25f, 55f));
                        if (p != null) fetchers.Add(p);
                    }
                    var kids = Children(m).Where(c => !c.Dead && c.Spawned && c.Map == map).ToList();
                    var at = kids.FirstOrDefault()?.Position;
                    if (fetchers.Count > 0 && Arrivals.Visit(fetchers, map, other, at, 6f) != null)
                    {
                        s.Set("fetched");
                        m.group.AddRange(fetchers);
                        m.stage = 4;
                        m.due = now + GenDate.TicksPerHour * 4;
                        StorylineKit.Note(P, s, m, map, "fetchers", new LookTargets(fetchers));
                        return;
                    }
                    // Nobody could come: their grown-up takes them on to their new home.
                    Leave(m, s, map, other);
                    End(m, MissionState.Done, "place", null);
                    return;
                }
                case 4:
                {
                    // Their new people take them by the hand and they all go.
                    m.lacks |= (int)Tend(m, map);
                    var other = s.other ?? s.giver;
                    var fetchers = m.group.Where(p => p != null && p.Faction == other && !p.DevelopmentalStage.Child() && p.Spawned).ToList();
                    var kids = Children(m).Where(c => !c.Dead && c.Spawned && c.Map == map && c.DevelopmentalStage.Child()).ToList();
                    bool met = fetchers.Any(f => kids.Any(k => k.Position.InHorDistOf(f.Position, 12f)));
                    if (!met && now < m.due) return;
                    Leave(m, s, map, other);
                    StorylineKit.Say(fetchers.FirstOrDefault(), def, s, m, "fetch");
                    End(m, MissionState.Done, "place", fetchers.Count > 0 ? new LookTargets(fetchers) : null);
                    return;
                }
                case 5:
                    // Food is carried out to them; then they go on.
                    m.lacks |= (int)Tend(m, map);
                    if (now < m.due) return;
                    Leave(m, s, map, null);
                    End(m, MissionState.Done, "sendon", null);
                    return;
            }
        }

        /// <summary>The card: what becomes of the children. False: nobody of the colony to answer now.</summary>
        private bool Ask(Mission m, Storyline s, Map map, int now, int kids)
        {
            bool asked = StorylineKit.Ask(Card, s, m, map, c =>
            {
                c.stranger = s.x != null && s.x.Spawned ? s.x : Children(m).FirstOrDefault(k => k.Spawned);
                c.count = kids;
                c.variant = kids * StorylineMath.MealsEach;
            });
            m.stage = 2;
            m.lastLook = now;
            if (asked) StorylineKit.Say(s.x, def, s, m, "ask");
            return asked;
        }

        /// <summary>They go: the children to <paramref name="newPeople"/> when a home was found for them, their grown-up back to their own.</summary>
        private void Leave(Mission m, Storyline s, Map map, Faction newPeople)
        {
            var kids = Children(m).Where(c => !c.Dead && c.Faction != Faction.OfPlayer && c.DevelopmentalStage.Child()).ToList();
            if (newPeople != null)
                foreach (var c in kids.Where(c => c.Faction != newPeople))
                    c.SetFaction(newPeople);
            Arrivals.SendAway(Guests(m).Concat(m.group).Where(p => p != null && p.Spawned && p.Map == map).Distinct().ToList());
        }

        public void Answered(Mission m, Storyline s, HappeningContext ctx, int option, bool byDefault)
        {
            var map = m.Map;
            int now = Find.TickManager.TicksGame;
            var kids = Children(m).Where(c => !c.Dead && c.Spawned && c.Map == map && c.Faction != Faction.OfPlayer).ToList();
            switch (option)
            {
                case 0:
                {
                    // Taken in: each child joins the colony; their grown-up goes home.
                    var joined = new List<Pawn>();
                    foreach (var c in kids)
                    {
                        Hosting.GetUp(c);
                        Hosting.GiveBack(c, map);
                        if (Arrivals.Join(c, map)) joined.Add(c);
                    }
                    if (s.x != null && s.x.Spawned) Arrivals.SendAway(new List<Pawn> { s.x });
                    s.Set("takein");
                    Missions.Goodwill(m, 10);
                    FactionMemory.Note(m.faction, "refugees", m.place);
                    StorylineKit.Say(s.x, def, s, m, "takein");
                    End(m, MissionState.Done, "takein", joined.Count > 0 ? new LookTargets(joined) : null, TextUtil.JoinAnd(joined.Select(p => p.LabelShort).ToList()));
                    return;
                }
                case 2:
                    // Sent on with food (the card's own carrying brings it out to them); they go a few hours later.
                    s.Set("sendon");
                    Missions.Goodwill(m, 4);
                    m.stage = 5;
                    m.due = now + GenDate.TicksPerHour * 6;
                    StorylineKit.Say(s.x, def, s, m, "sendon");
                    return;
                default:
                {
                    // A home with neighbours: their people come for the children within a day.
                    s.other = ChildrenRules.Neighbours(s.giver);
                    if (s.other == s.giver) s.Set("samepeople");
                    s.Set("place");
                    Missions.Goodwill(m, m.goodwill);
                    if (s.other != null && s.other != s.giver) StoryChains.Goodwill(s.other, 5, GoodwillWhy.Task(5));
                    FactionMemory.Note(m.faction, "refugees", m.place);
                    m.stage = 3;
                    m.due = now + Mathf.RoundToInt(Rand.Range(0.4f, 0.9f) * GenDate.TicksPerDay);
                    StorylineKit.Note(P, s, m, map, "placed", Targets(m), MessageTypeDefOf.PositiveEvent);
                    StorylineKit.Say(s.x, def, s, m, "place");
                    return;
                }
            }
        }

        public void Describe(Mission m, Storyline s, string choice, HappeningContext ctx, ImpactSheet sheet)
        {
            switch (choice)
            {
                case "takein":
                    sheet.Play("CC_Storyline_Impact_TakeIn".Translate(ctx.count.Named("COUNT")).Resolve(), "CC_Storyline_Impact_TakeInBrief".Translate().Resolve());
                    sheet.Goodwill(s.giver, 10);
                    break;
                case "place":
                    sheet.Play("CC_Storyline_Impact_Place".Translate().Resolve(), "CC_Storyline_Impact_PlaceBrief".Translate().Resolve());
                    sheet.Goodwill(s.giver, m.goodwill);
                    break;
                case "sendon":
                    sheet.Goodwill(s.giver, 4);
                    break;
            }
        }

        /// <summary>The ending of the part: the children are the storyline's no more, except the eldest who may come back.</summary>
        public void End(Mission m, MissionState state, string outcome, LookTargets look = null, string item = null)
        {
            var s = Story(m);
            StorylineKit.End(P, m, state, outcome, look, item);
            if (s == null) return;
            // Once the mission is over: only the eldest is still the storyline's, and only if they may come back.
            var eldest = ChildrenRules.Eldest(s);
            bool back = s.Running && (outcome == "place" || outcome == "sendon");
            var others = s.group.Where(c => c != eldest || !back).ToList();
            s.group.RemoveAll(c => c != eldest || !back);
            var adult = s.x;
            s.x = null;
            foreach (var p in others) Storylines.Release(p);
            Storylines.Release(adult);
        }

        /// <summary>A day of their stay: fed and sheltered, or short (told).</summary>
        private void Day(Mission m, Map map, List<Pawn> here)
        {
            var lack = (Hosting.Lack)m.lacks;
            m.lacks = 0;
            if (here.Count == 0) return;
            bool fed = (lack & Hosting.Lack.Food) == 0;
            bool roofed = (lack & Hosting.Lack.Bed) == 0 && Hosting.Slots(Beds(m, map)) >= here.Count;
            if (fed && roofed)
            {
                m.tally++;
                return;
            }
            m.misses++;
            string key = !fed && !roofed ? "CC_Storyline_Children_Neither" : !fed ? "CC_Storyline_Children_Hungry" : "CC_Storyline_Children_NoRoof";
            ColonyCouncil.Tell.Message(NewsKind.World, Importance.Notable, map, key.Translate((m.faction?.Name ?? "?").Named("FACTION")).Resolve(), new LookTargets(here), MessageTypeDefOf.NegativeEvent);
        }

        protected override Thing FoodFor(Mission m, Map map, Pawn p) => Hosting.Meal(p, false) ?? Hosting.Raw(p);

        /// <summary>The bed they had last night; else a free one (single ones first); else a free side of another of theirs.</summary>
        protected override Building_Bed BedFor(Mission m, Map map, Pawn p)
        {
            var beds = Beds(m, map);
            var own = p.ownership?.OwnedBed;
            if (own != null && beds.Contains(own)) return own;
            return beds.Where(b => b.OwnersForReading.Count == 0).OrderBy(b => b.SleepingSlotsCount).ThenBy(b => b.Position.DistanceToSquared(p.Position)).FirstOrDefault()
                   ?? beds.Where(b => b.OwnersForReading.Count < b.SleepingSlotsCount && Hosting.CanShare(b, p)).OrderBy(b => b.Position.DistanceToSquared(p.Position)).FirstOrDefault();
        }

        /// <summary>Its people stay the storyline's: nothing is let go here but beds (the part's end lets the rest go).</summary>
        public override void Release(Mission m)
        {
            var map = m.Map;
            foreach (var p in Guests(m).Concat(m.group).Where(p => p != null).Distinct().ToList())
            {
                Hosting.GetUp(p);
                Hosting.GiveBack(p, map);
                // Never came (the offer was turned down, or ran out): they were only ever names.
                if (!p.Dead && !p.Spawned && m.started < 0 && p.GetCaravan() == null) Arrivals.Discard(p);
            }
        }

        public override string Status(Mission m)
        {
            var map = m.Map;
            int now = Find.TickManager.TicksGame;
            int beds = map == null ? 0 : Hosting.Slots(Beds(m, map));
            string roof = "CC_Mission_Refugees_Beds".Translate(beds, m.target).Resolve();
            switch (m.stage)
            {
                case 0: return "CC_Storyline_Children_Coming".Translate(m.target.Named("COUNT"), TextUtil.DaysString(Mathf.Max(0, m.due - now)).Named("DAYS")).Resolve() + " " + roof;
                case 1: return "CC_Mission_Refugees_Staying".Translate(m.tally, m.misses).Resolve() + " " + roof + " " + MealsText(m);
                case 2: return "CC_Storyline_Children_Asking".Translate().Resolve();
                case 3:
                case 4: return "CC_Storyline_Children_Fetch".Translate((Story(m)?.other?.Name ?? "?").Named("OTHERPEOPLE")).Resolve();
                default: return "CC_Storyline_Children_Going".Translate().Resolve();
            }
        }
    }

    // ===================== Part 2: one of them comes back grown =====================

    /// <summary>
    /// Two to four seasons after they went on, the eldest comes back to the gate, grown (the game's adult age): to visit and
    /// leave a gift, to trade from a pack of their people's goods, or to ask to join (a card). Turned down, they pass by.
    /// </summary>
    public class HappeningWorker_ChildrenBack : HappeningWorker_StorylinePart
    {
        public const string Card = "Rimfolk_Storyline_ChildrenJoin";

        protected override int Rating => 1;

        protected override bool Possible(Map map)
        {
            var e = Storylines.current != null ? ChildrenRules.Eldest(Storylines.current) : null;
            return e != null && StoryChains.CanVisit(e);
        }

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var s = Storylines.current;
            var map = ctx.map;
            var e = s != null ? ChildrenRules.Eldest(s) : null;
            if (e?.Faction == null) return false;
            m.a = VarietyUtil.Best(map, SkillDefOf.Social) ?? StorytellerEvents.Adults(map).RandomElementWithFallback();
            if (m.a == null) return false;
            string form = StorylineMath.ReturnForm(s.Has("place") ? "place" : "sendon", Rand.Value);
            if (form == "trader" && !FactorRules.Trading(e.Faction)) form = "visitor";
            s.flags.RemoveAll(f => f == "visitor" || f == "trader" || f == "joiner");
            s.Set(form);
            m.faction = e.Faction;
            m.place = LivingEvents.TownOf(e.Faction, map) ?? e.Faction.Name;
            m.x = e;
            m.variant = form;
            m.days = 2f;
            Fill(m, ctx);
            ctx.stranger = e;
            ctx.faction = e.Faction;
            return true;
        }

        public override bool Start(Mission m, Map map)
        {
            var s = Story(m);
            if (s == null) return false;
            m.stage = 0;
            m.due = Find.TickManager.TicksGame + Mathf.RoundToInt(Rand.Range(0.3f, 0.7f) * GenDate.TicksPerDay);
            StorylineKit.Accepted(P, s, m, map, null);
            return true;
        }

        public override void Check(Mission m, Map map, int now)
        {
            var s = Story(m);
            var e = s != null ? ChildrenRules.Eldest(s) : null;
            if (s == null || e == null || e.Dead || e.Discarded)
            {
                End(m, MissionState.Failed, StorylineMath.Ended);
                return;
            }
            if (m.stage == 0)
            {
                if (now < m.due) return;
                ChildrenRules.Grown(e);
                float hours = s.Has("trader") ? 14f : s.Has("joiner") ? 10f : 8f;
                if (Arrivals.Visit(new List<Pawn> { e }, map, e.Faction, PeoplesD.GateSpot(map), hours) == null)
                {
                    End(m, MissionState.Failed, StorylineMath.Ended);
                    return;
                }
                if (s.Has("trader") && !ChildrenRules.MakeTrader(e, map))
                {
                    s.flags.Remove("trader");
                    s.Set("visitor");
                }
                if (s.Has("visitor"))
                {
                    // A gift of thanks, in their own hands.
                    string what = StoryChains.Gift(map, e.Position, StorytellerEvents.Silver(map, new FloatRange(150f, 350f)), false, false, e.Faction, e);
                    if (!what.NullOrEmpty()) s.tale = what;
                }
                m.stage = 1;
                m.lastLook = now;
                StorylineKit.Note(P, s, m, map, "came", new LookTargets(e));
                Say(e, m, "came");
                return;
            }
            if (m.stage == 1)
            {
                if (s.Has("joiner"))
                {
                    var gate = PeoplesD.GateSpot(map);
                    bool there = e.Spawned && (gate == null || e.Position.InHorDistOf(gate.Value, 14f) || now - m.lastLook > GenDate.TicksPerHour * 3);
                    if (!there)
                    {
                        if (!e.Spawned && now - m.lastLook > GenDate.TicksPerHour * 3) End(m, MissionState.Done, "visited");
                        return;
                    }
                    if (StorylineKit.Ask(Card, s, m, map, c => c.stranger = e))
                    {
                        m.stage = 2;
                        m.lastLook = now;
                    }
                    else if (now - m.lastLook > GenDate.TicksPerDay) End(m, MissionState.Done, "visited");
                    return;
                }
                // A visit or a trade: over once they have gone on.
                if (!e.Spawned || now >= m.deadline) End(m, MissionState.Done, s.Has("trader") ? "traded" : "visited", null, s.tale);
                return;
            }
            // The card: answered, or lost without an answer (they go on).
            if (now >= m.deadline || !StorylineKit.CardOut(s) && now - m.lastLook > GenDate.TicksPerHour * 2)
            {
                Arrivals.SendAway(new List<Pawn> { e });
                End(m, MissionState.Done, "notjoined");
            }
        }

        public override void Answered(Mission m, Storyline s, HappeningContext ctx, int option, bool byDefault)
        {
            var map = m.Map;
            var e = ChildrenRules.Eldest(s);
            if (option == 0 && e != null && map != null && Arrivals.Join(e, map))
            {
                s.group.Remove(e);
                Say(e, m, "joined");
                End(m, MissionState.Done, "joined", new LookTargets(e));
                return;
            }
            if (e != null) Arrivals.SendAway(new List<Pawn> { e });
            Say(e, m, "notjoined");
            End(m, MissionState.Done, "notjoined", e != null && e.Spawned ? new LookTargets(e) : null);
        }

        public override void Describe(Mission m, Storyline s, string choice, HappeningContext ctx, ImpactSheet sheet)
        {
            if (choice == "join") sheet.Play("CC_Storyline_Impact_Join".Translate((ChildrenRules.Eldest(s)?.LabelShort ?? s.name ?? "?").Named("NAME")).Resolve(), "CC_Storyline_Impact_JoinBrief".Translate().Resolve());
        }

        public override LookTargets Targets(Mission m)
        {
            var e = Story(m) != null ? ChildrenRules.Eldest(Story(m)) : null;
            return e != null && e.Spawned ? new LookTargets(e) : LookTargets.Invalid;
        }

        public override string Status(Mission m) =>
            m.stage == 0 ? "CC_Storyline_Back_Coming".Translate((Story(m)?.name ?? "?").Named("NAME")).Resolve() : null;
    }
}
