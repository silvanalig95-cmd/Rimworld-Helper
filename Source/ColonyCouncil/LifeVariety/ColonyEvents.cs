using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // Twelve events of the colony's everyday life (the content update's variety, the player's go of 20 November 2026: "normal
    // storyteller events"; Defs/IncidentDefs/Rimfolk_Events_ColonyLife.xml). Each grows out of what is on the map and is tied
    // into what exists: the seats (the peacemaker, the quartermaster, the reeve, the herdmaster, the speaker), the groups,
    // the wishes and promises, the calendar and the colony's elders and children, and the gods as the colony believes them.
    // Each answer plays out with the engine's own (a word at the table, a visit, a lesson, a work party, a vigil, a group's
    // evening). None brings harm.

    /// <summary>Shared lookups of the colony's own events.</summary>
    public static class ColonyEvents
    {
        public static List<Pawn> Adults(Map map) => StorytellerEvents.Adults(map).ToList();

        /// <summary>A seat's holder at this home, when they are about and not one of <paramref name="not"/>.</summary>
        public static Pawn Holder(Map map, string office, params Pawn[] not)
        {
            var h = OfficeTracker.For(map)?.HoldersOf(office).FirstOrDefault(p => p.Spawned && p.Map == map && StorytellerEvents.Able(p) && !not.Contains(p));
            return h;
        }

        public static Pawn Best(IEnumerable<Pawn> among, SkillDef skill, params Pawn[] not) =>
            among.Where(p => !not.Contains(p) && p.skills?.GetSkill(skill) is SkillRecord s && !s.TotallyDisabled).OrderByDescending(p => p.skills.GetSkill(skill).Level).FirstOrDefault();

        public static int Opinion(Pawn a, Pawn b) => a?.relations != null && b != null ? a.relations.OpinionOf(b) : 0;

        public static float DaysHere(Pawn p) => (p.records?.GetValue(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? 0f) / GenDate.TicksPerDay;
    }

    // ===================== 1. A quarrel at supper (the peacemaker) =====================

    /// <summary>
    /// Two at odds (both ways) argue at the supper hours; the peacemaker at home, else the colony's best talker, offers to sit
    /// them down. A and B the two; the one who would mediate is the event's group ({HOLDER_...} in the words).
    /// </summary>
    public class HappeningWorker_QuarrelAtSupper : IncidentWorker_Happening
    {
        private static bool Pair(Map map, out Pawn a, out Pawn b)
        {
            a = b = null;
            int hour = GenLocalDate.HourOfDay(map);
            if (hour < ColonyEventMath.SupperFrom || hour > ColonyEventMath.SupperTo) return false;
            var adults = ColonyEvents.Adults(map);
            int worst = 0;
            foreach (var x in adults)
                foreach (var y in adults)
                {
                    if (x == y || x.thingIDNumber > y.thingIDNumber) continue;
                    int ab = ColonyEvents.Opinion(x, y), ba = ColonyEvents.Opinion(y, x);
                    if (!ColonyEventMath.AtOdds(ab, ba) || ab + ba >= worst) continue;
                    worst = ab + ba;
                    a = x;
                    b = y;
                }
            return a != null;
        }

        private static Pawn Mediator(Map map, Pawn a, Pawn b) =>
            ColonyEvents.Holder(map, "Council_Office_Peacemaker", a, b) ?? ColonyEvents.Best(ColonyEvents.Adults(map), SkillDefOf.Social, a, b);

        protected override bool CanHappen(Map map, HappeningExtension ext) => !MapFacts.ThreatOn(map) && Pair(map, out var a, out var b) && Mediator(map, a, b) != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            if (!Pair(ctx.map, out var a, out var b)) return false;
            var m = Mediator(ctx.map, a, b);
            if (m == null) return false;
            ctx.a = a;
            ctx.b = b;
            ctx.holder = m;
            ctx.group = new List<Pawn> { m };
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.holder != null && Interplay.Holds(ctx.holder, "Council_Office_Peacemaker")) tags.Add("peacemaker");
        }
    }

    /// <summary>A quarrel sat down over: one more mended for the one who sat them down (the peacemaker's work), and the two a little less at odds.</summary>
    public class DilemmaEffect_QuarrelMended : DilemmaEffect
    {
        public float amount = 8f;

        public override bool ChanceEach => false;

        public override void Apply(DilemmaContext ctx)
        {
            if (ctx?.a == null || ctx.b == null) return;
            var mediator = (ctx as HappeningContext)?.group?.FirstOrDefault();
            if (mediator != null) CouncilRecords.Add(mediator, CouncilRecords.QuarrelsMended);
            CouncilGameComponent.Instance?.social?.Weaken(ctx.a, ctx.b, TieKind.Rivals, amount, "CC_Life2_MendNote".Translate().Resolve());
        }
    }

    // ===================== 2. The newcomer at the edge (the groups) =====================

    /// <summary>
    /// Someone who came within twenty days stands outside every group; a group's anchor who doesn't mind them would bring them
    /// along. A the newcomer, B the anchor, {TALE} the group (its id kept in the context's note).
    /// </summary>
    public class HappeningWorker_NewcomerAtEdge : IncidentWorker_Happening
    {
        private static bool Find(Map map, out Pawn newcomer, out SocialGroup group, out Pawn anchor)
        {
            newcomer = anchor = null;
            group = null;
            if (!GroupLife.Active || Groups.All.Count == 0) return false;
            var adults = ColonyEvents.Adults(map);
            foreach (var p in adults.Where(p => ColonyEvents.DaysHere(p) <= ColonyEventMath.NewcomerDays && Groups.Of(p).Count == 0).InRandomOrder())
                foreach (var g in Groups.All.Where(g => g.Pawns.Any(x => x.MapHeld == map)).InRandomOrder())
                {
                    var an = g.Holder(GroupRole.Anchor) ?? g.Pawns.FirstOrDefault();
                    if (an == null || an == p || !adults.Contains(an) || ColonyEvents.Opinion(an, p) < 0) continue;
                    newcomer = p;
                    group = g;
                    anchor = an;
                    return true;
                }
            return false;
        }

        protected override bool CanHappen(Map map, HappeningExtension ext) => !MapFacts.ThreatOn(map) && Find(map, out _, out _, out _);

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            if (!Find(ctx.map, out var p, out var g, out var an)) return false;
            ctx.a = p;
            ctx.b = an;
            ctx.tale = g.Name;
            ctx.note = g.id.ToString();
            return true;
        }
    }

    /// <summary>
    /// An evening of a group's own (its anchor B leads), with <see cref="withA"/> the newcomer A brought along; the group of
    /// the context's note. One of the colony's own gatherings, as a group's evening is when its anchor calls it.
    /// </summary>
    public class DilemmaEffect_GroupEvening : DilemmaEffect_PlayOut
    {
        public bool withA;

        public DilemmaEffect_GroupEvening()
        {
            hours = new IntRange(17, 22);
            duration = new FloatRange(1.5f, 2f);
            waitHours = 24f;
        }

        protected override PlayKind Kind => PlayKind.Gathering;

        private static SocialGroup GroupOf(DilemmaContext ctx) =>
            int.TryParse((ctx as HappeningContext)?.note, out int id) ? GroupTracker.Instance?.Get(id) : null;

        protected override bool Fill(PlayOut p, DilemmaContext ctx)
        {
            var g = GroupOf(ctx);
            var map = ctx.map;
            if (g == null || map == null) return false;
            var invited = g.Pawns.Where(x => PlayOuts.OurPerson(x) && x.MapHeld == map).ToList();
            if (withA && ctx.a != null && !invited.Contains(ctx.a)) invited.Add(ctx.a);
            if (invited.Count < 2) return false;
            var habit = g.Habits.FirstOrDefault(h => h.Play != null) ?? g.Habits.FirstOrDefault();
            p.kind = habit?.Play ?? PlayKind.Gathering;
            if (p.kind == PlayKind.Bonfire)
            {
                p.buildFire = true;
                p.music = true;
            }
            p.invited = invited;
            p.organizer = ctx.b != null && invited.Contains(ctx.b) ? ctx.b : invited[0];
            p.spots = habit != null && habit.Spots.Count > 0 ? habit.Spots : new List<PlaySpot> { PlaySpot.Rec, PlaySpot.Dining, PlaySpot.Gather };
            p.pool = habit?.pool ?? "Council_Pool_PlayParty";
            if (GroupDefs.Habit != null)
                foreach (var x in invited)
                    p.moods.Add(new HeldMood { pawn = x, thought = GroupDefs.Habit, cause = "CC_Cause_GroupHabit".Translate(habit?.label ?? "", g.Name).Resolve() });
            g.lastArranged = Find.TickManager.TicksGame;
            return true;
        }

        public override void Apply(DilemmaContext ctx)
        {
            var p = Create(ctx);
            var g = GroupOf(ctx);
            if (p != null && g != null && g.playId < 0) g.playId = p.id;
        }

        protected override string DefaultLabel(DilemmaContext ctx) => "CC_Life2_GroupEveningLabel".Translate((GroupOf(ctx)?.Name ?? "").Named("GROUP")).Resolve();
    }

    // ===================== 3. The student's first piece (the mentorships) =====================

    /// <summary>
    /// A student of the colony's (a mentorship in the social web, as the coming-of-age apprenticeships start) made something
    /// excellent or better lately. A the teacher, B the student, {THING} what they made.
    /// </summary>
    public class HappeningWorker_FirstPiece : IncidentWorker_Happening
    {
        private static SocialTie Tie(Map map)
        {
            var social = CouncilGameComponent.Instance?.social;
            if (social == null || !ColonyCouncilMod.Settings.socialWeb) return null;
            var adults = ColonyEvents.Adults(map);
            return social.ties.Where(t => t.kind == TieKind.Mentor && t.announced && t.a != null && t.b != null && adults.Contains(t.a) && t.b.Spawned && t.b.Map == map
                                          && SocialEvents.FineOf(t.b, 3f) != null).InRandomOrder().FirstOrDefault();
        }

        protected override bool CanHappen(Map map, HappeningExtension ext) => !MapFacts.ThreatOn(map) && Tie(map) != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var t = Tie(ctx.map);
            if (t == null) return false;
            ctx.a = t.a;
            ctx.b = t.b;
            ctx.thing = SocialEvents.FineOf(t.b, 3f);
            ctx.skill = t.skill;
            return ctx.thing != null;
        }
    }

    // ===================== 4. Short in the stores (the quartermaster) =====================

    /// <summary>
    /// The colony's food runs short (fewer than five days of it) while there is something to bring in: ripe crops or wild food
    /// in reach. A the quartermaster, else the colony's sharpest mind; B the best grower.
    /// </summary>
    public class HappeningWorker_StoresShort : IncidentWorker_Happening
    {
        private static bool SomethingToBringIn(Map map) => LastField.Ripe(map) > 0 || YearFour.Forage(map, 1).Count > 0;

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            !MapFacts.ThreatOn(map) && AdvisorWorker_Food.DaysOfFood(map) < ColonyEventMath.ShortDays && SomethingToBringIn(map);

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var adults = ColonyEvents.Adults(ctx.map);
            ctx.a = ColonyEvents.Holder(ctx.map, "Council_Office_Quartermaster") ?? ColonyEvents.Best(adults, SkillDefOf.Intellectual);
            ctx.b = ColonyEvents.Best(adults, SkillDefOf.Plants, ctx.a);
            ctx.count = Mathf.Max(1, Mathf.FloorToInt(AdvisorWorker_Food.DaysOfFood(ctx.map)));
            return ctx.a != null && ctx.b != null;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.a != null && Interplay.Holds(ctx.a, "Council_Office_Quartermaster")) tags.Add("quartermaster");
            if (LastField.Ripe(ctx.map) > 0) tags.Add("ripe");
        }
    }

    // ===================== 5. A wish left waiting (the wishes) =====================

    /// <summary>
    /// A wish has waited more than half its time with no promise, and a friend of the one who asked brings it up. A the one who
    /// asked, B the friend, {TALE} the wish.
    /// </summary>
    public class HappeningWorker_WishWaiting : IncidentWorker_Happening
    {
        public static Wish Waiting(Map map, out Pawn friend)
        {
            friend = null;
            var comp = CouncilGameComponent.Instance;
            if (comp == null || !ColonyCouncilMod.Settings.enableWishes) return null;
            var adults = ColonyEvents.Adults(map);
            foreach (var w in comp.wishes.Where(w => w?.def != null && w.state == WishState.Open && !w.promised && w.pawn != null && adults.Contains(w.pawn)
                                                     && w.Elapsed >= ColonyEventMath.WaitedShare).InRandomOrder())
            {
                friend = adults.Where(o => o != w.pawn && ColonyEvents.Opinion(o, w.pawn) >= 20 && !w.Involves(o)).OrderByDescending(o => ColonyEvents.Opinion(o, w.pawn)).FirstOrDefault();
                if (friend != null) return w;
            }
            return null;
        }

        protected override bool CanHappen(Map map, HappeningExtension ext) => Waiting(map, out _) != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var w = Waiting(ctx.map, out var friend);
            if (w == null) return false;
            ctx.a = w.pawn;
            ctx.b = friend;
            ctx.tale = w.LabelInline;
            ctx.note = w.id.ToString();
            return true;
        }
    }

    /// <summary>The council promises the wish the event is about (the context's note): as the player's own "Promise" does.</summary>
    public class DilemmaEffect_PromiseWish : DilemmaEffect
    {
        public override bool ChanceEach => false;

        private static Wish WishOf(DilemmaContext ctx)
        {
            var comp = CouncilGameComponent.Instance;
            return comp != null && int.TryParse((ctx as HappeningContext)?.note, out int id) ? comp.wishes.FirstOrDefault(w => w.id == id && w.state == WishState.Open) : null;
        }

        public override string Unavailable(DilemmaContext ctx) => ctx?.a != null && WishOf(ctx) == null ? "CC_Life2_WishGone".Translate().Resolve() : null;

        public override void Apply(DilemmaContext ctx)
        {
            var w = WishOf(ctx);
            if (w != null) CouncilGameComponent.Instance.Promise(w);
        }
    }

    // ===================== 6. The old song (the elders) =====================

    /// <summary>
    /// One of the colony's elders remembers a song from where they came from, of an evening. A the elder, B a child of the colony
    /// if there is one, else whoever is fondest of them.
    /// </summary>
    public class HappeningWorker_OldSong : IncidentWorker_Happening
    {
        private static Pawn Elder(Map map) =>
            ColonyEvents.Adults(map).Where(p => Flavor.HumanAge(p) >= ColonyEventMath.SingerAge).InRandomOrder().FirstOrDefault();

        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            int hour = GenLocalDate.HourOfDay(map);
            return hour >= 15 && hour <= 21 && !MapFacts.ThreatOn(map) && Elder(map) != null;
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.a = Elder(ctx.map);
            if (ctx.a == null) return false;
            var people = IncidentWorker_Happening.Colonists(ctx.map).Where(p => p != ctx.a && StorytellerEvents.Able(p) || p.DevelopmentalStage.Child()).Where(p => p != ctx.a).ToList();
            ctx.b = people.Where(p => p.DevelopmentalStage.Child() && p.Spawned).InRandomOrder().FirstOrDefault()
                    ?? people.Where(p => !p.DevelopmentalStage.Juvenile()).OrderByDescending(p => ColonyEvents.Opinion(p, ctx.a)).FirstOrDefault();
            return ctx.b != null;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.b != null && ctx.b.DevelopmentalStage.Child()) tags.Add("child");
        }
    }

    // ===================== 7. A vow in the storm (the gods as the colony believes them) =====================

    /// <summary>
    /// After a storm, a colonist who holds to a god says they vowed to it in the night to keep watch at its shrine if the storm
    /// passed. Their belief, not the colony's knowledge: the god is named as they call it ({TALE}), and nothing says the god
    /// heard. A the believer, B a friend; the shrine is the event's place.
    /// </summary>
    public class HappeningWorker_StormVow : IncidentWorker_Happening
    {
        private static bool Find(Map map, out Pawn believer, out God god, out Thing shrine)
        {
            believer = null;
            god = null;
            shrine = null;
            if (!Gods.On) return false;
            foreach (var p in ColonyEvents.Adults(map).InRandomOrder())
            {
                var g = PawnGods.Held(p);
                if (g == null) continue;
                var s = Shrines.Nearest(p, g);
                if (s == null || !s.Spawned || s.Map != map) continue;
                believer = p;
                god = g;
                shrine = s;
                return true;
            }
            return false;
        }

        protected override bool CanHappen(Map map, HappeningExtension ext) => !MapFacts.ThreatOn(map) && Find(map, out _, out _, out _);

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            if (!Find(ctx.map, out var p, out var g, out var shrine)) return false;
            ctx.a = p;
            ctx.place = shrine;
            ctx.placeName = shrine.LabelShort;
            ctx.tale = Worship.Called(g, p);
            ctx.b = ColonyEvents.Adults(ctx.map).Where(o => o != p).OrderByDescending(o => ColonyEvents.Opinion(o, p)).FirstOrDefault();
            return ctx.b != null;
        }
    }

    /// <summary>A night's watch at the event's place (the shrine of the vow): one of them (or both) sits there for a few hours.</summary>
    public class DilemmaEffect_WatchAtPlace : DilemmaEffect_Vigil
    {
        protected override bool Fill(PlayOut p, DilemmaContext ctx)
        {
            var place = (ctx as HappeningContext)?.place;
            if (place == null || !place.Spawned || place.Map != ctx.map || !base.Fill(p, ctx)) return false;
            p.place = place;
            p.to = PlaySpot.Place;
            return true;
        }
    }

    // ===================== 8. A group's evening off (the groups) =====================

    /// <summary>
    /// A close group asks for an evening together: three or more of it at home, not gathered for a week. A its anchor, B another
    /// of it, {TALE} the group.
    /// </summary>
    public class HappeningWorker_GroupEveningOff : IncidentWorker_Happening
    {
        private static SocialGroup Group(Map map, out Pawn anchor, out Pawn other)
        {
            anchor = other = null;
            if (!GroupLife.Active) return null;
            int now = Find.TickManager.TicksGame;
            var adults = ColonyEvents.Adults(map);
            foreach (var g in Groups.All.Where(g => g.cohesion >= GroupMath.CloseAt && g.playId < 0 && now - g.lastArranged > GenDate.TicksPerDay * 7).InRandomOrder())
            {
                var here = g.Pawns.Where(adults.Contains).ToList();
                if (here.Count < 3) continue;
                anchor = g.Holder(GroupRole.Anchor);
                if (anchor == null || !here.Contains(anchor)) anchor = here[0];
                var a = anchor;
                other = here.Where(x => x != a).RandomElement();
                return g;
            }
            return null;
        }

        protected override bool CanHappen(Map map, HappeningExtension ext) => !MapFacts.ThreatOn(map) && PlayOuts.On && Group(map, out _, out _) != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var g = Group(ctx.map, out var anchor, out var other);
            if (g == null) return false;
            // B leads the evening (DilemmaEffect_GroupEvening): the anchor who asks for it.
            ctx.a = other;
            ctx.b = anchor;
            ctx.tale = g.Name;
            ctx.note = g.id.ToString();
            return true;
        }
    }

    // ===================== 9. The child's question (the calendar and the children) =====================

    /// <summary>
    /// A child of the colony asks a grown-up fond of them why the colony keeps one of its days. A the grown-up, B the child,
    /// {TALE} the day (else the colony's beginning).
    /// </summary>
    public class HappeningWorker_ChildsQuestion : IncidentWorker_Happening
    {
        private static bool Find(Map map, out Pawn grown, out Pawn child)
        {
            grown = child = null;
            if (!ModsConfig.BiotechActive) return false;
            var adults = ColonyEvents.Adults(map);
            foreach (var c in IncidentWorker_Happening.Colonists(map).Where(p => p.DevelopmentalStage.Child() && Together.Able(p)).InRandomOrder())
            {
                var g = adults.Where(a => ColonyEvents.Opinion(a, c) >= 20 || Flavor.HumanAge(a) >= EldersMath.ElderAge).OrderByDescending(a => ColonyEvents.Opinion(a, c)).FirstOrDefault();
                if (g == null) continue;
                grown = g;
                child = c;
                return true;
            }
            return false;
        }

        protected override bool CanHappen(Map map, HappeningExtension ext) => !MapFacts.ThreatOn(map) && Find(map, out _, out _);

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            if (!Find(ctx.map, out var g, out var c)) return false;
            ctx.a = g;
            ctx.b = c;
            var day = ColonyCalendar.For(ctx.map)?.days.Where(d => d != null && !d.Personal && d.Dated && d.timesKept > 0).InRandomOrder().FirstOrDefault();
            ctx.tale = day?.label;
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags) => tags.Add(ctx.tale.NullOrEmpty() ? "noday" : "day");
    }

    // ===================== 10. A broken promise still sore (the wishes and the speaker) =====================

    /// <summary>
    /// Someone still minds a promise the council broke (within ten days), and a friend asks the council to make it right. A the
    /// one who minds, B the friend; the speaker at home (else the colony's best talker) is the event's group ({HOLDER_...}).
    /// </summary>
    public class HappeningWorker_PromiseSore : IncidentWorker_Happening
    {
        private static bool Find(Map map, out Pawn sore, out Pawn friend, out Pawn speaker)
        {
            sore = friend = speaker = null;
            var adults = ColonyEvents.Adults(map);
            foreach (var p in adults.Where(p => LifeChecks.HasThought(p, new List<string> { "Council_BrokenPromise" }, ColonyEventMath.SoreDays)).InRandomOrder())
            {
                var f = adults.Where(o => o != p && ColonyEvents.Opinion(o, p) >= 20).OrderByDescending(o => ColonyEvents.Opinion(o, p)).FirstOrDefault();
                if (f == null) continue;
                var s = ColonyEvents.Holder(map, "Council_Office_Speaker", p, f) ?? ColonyEvents.Best(adults, SkillDefOf.Social, p, f);
                if (s == null) continue;
                sore = p;
                friend = f;
                speaker = s;
                return true;
            }
            return false;
        }

        protected override bool CanHappen(Map map, HappeningExtension ext) => ColonyCouncilMod.Settings.enableWishes && Find(map, out _, out _, out _);

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            if (!Find(ctx.map, out var a, out var b, out var s)) return false;
            ctx.a = a;
            ctx.b = b;
            ctx.holder = s;
            ctx.group = new List<Pawn> { s };
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.holder != null && Interplay.Holds(ctx.holder, "Council_Office_Speaker")) tags.Add("speaker");
        }
    }

    // ===================== 11. Hands for the last field (the reeve) =====================

    /// <summary>The colony's ripe crops in its growing zones.</summary>
    public static class LastField
    {
        public static int Ripe(Map map)
        {
            if (map?.zoneManager == null) return 0;
            int n = 0;
            foreach (var z in map.zoneManager.AllZones.OfType<Zone_Growing>())
                foreach (var c in z.Cells)
                    if (c.GetPlant(map) is Plant p && p.HarvestableNow && p.def.plant.harvestedThingDef != null)
                        n++;
            return n;
        }
    }

    /// <summary>
    /// In autumn the last field is ripe, thirty plants or more, and the reeve (else the best grower) asks for hands before the
    /// cold. A the reeve, B the next best grower.
    /// </summary>
    public class HappeningWorker_LastField : IncidentWorker_Happening
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            !MapFacts.ThreatOn(map) && !HarvestAskedLately(map) && LastField.Ripe(map) >= ColonyEventMath.RipePlants;

        /// <summary>The council asked "every hand to the harvest" (Rimfolk_DlmE_HarvestHands) lately at this home.</summary>
        private static bool HarvestAskedLately(Map map)
        {
            var tracker = DilemmaTracker.For(map) ?? CouncilGameComponent.Instance?.dilemmas;
            int last = tracker?.LastAsked("Rimfolk_DlmE_HarvestHands") ?? -1;
            return ColonyEventMath.Within(last, Find.TickManager.TicksGame, ColonyEventMath.HarvestQuestionGapDays, GenDate.TicksPerDay);
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var adults = ColonyEvents.Adults(ctx.map);
            ctx.a = ColonyEvents.Holder(ctx.map, "Council_Office_Reeve") ?? ColonyEvents.Best(adults, SkillDefOf.Plants);
            ctx.b = ColonyEvents.Best(adults, SkillDefOf.Plants, ctx.a);
            ctx.count = LastField.Ripe(ctx.map);
            return ctx.a != null && ctx.b != null;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.a != null && Interplay.Holds(ctx.a, "Council_Office_Reeve")) tags.Add("reeve");
        }
    }

    // ===================== 12. A night with the herd (the herdmaster) =====================

    /// <summary>
    /// One of the colony's animals is near its time; the herdmaster (else the best handler) would sit up with it. A them, the
    /// animal the event's pet ({PET_...}).
    /// </summary>
    public class HappeningWorker_HerdNight : IncidentWorker_Happening
    {
        public static Pawn Expecting(Map map) =>
            map.mapPawns.SpawnedColonyAnimals.Where(a => !a.Dead && !a.Downed && a.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.Pregnant) is Hediff_Pregnant h
                                                         && h.GestationProgress >= ColonyEventMath.NearTerm).InRandomOrder().FirstOrDefault();

        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            int hour = GenLocalDate.HourOfDay(map);
            return hour >= 16 && hour <= 22 && !MapFacts.ThreatOn(map) && Expecting(map) != null;
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.pet = Expecting(ctx.map);
            if (ctx.pet == null) return false;
            var adults = ColonyEvents.Adults(ctx.map);
            ctx.a = ColonyEvents.Holder(ctx.map, "Council_Office_Herdmaster") ?? ColonyEvents.Best(adults, SkillDefOf.Animals);
            ctx.beast = ctx.pet.kindDef.label;
            return ctx.a != null;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.a != null && Interplay.Holds(ctx.a, "Council_Office_Herdmaster")) tags.Add("herdmaster");
        }
    }
}
