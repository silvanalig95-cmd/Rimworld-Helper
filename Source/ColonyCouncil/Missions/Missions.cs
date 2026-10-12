using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // Missions: tracked objectives a neighbor asks of the colony, with clear terms, a deadline once accepted, a reward
    // and a cost for failing. The offer is a Rimfolk event (Defs/MissionDefs/), drawn by the storyteller; it shows up in
    // the game's Quests tab as a real quest to accept or let expire (Settings: "Missions in the Quests tab"; without it,
    // or if the quest can't be made, the offer comes as a letter). Rimfolk keeps the mission itself (here, saved in
    // StoriesComponent): it checks the goals, ends the quest with success or failure, and shows the mission and its
    // progress on the World page. The three missions are in Workers_Missions.cs.

    public enum MissionState
    {
        Offered,
        Active,
        Done,
        Failed,
        Declined
    }

    /// <summary>Something to deliver: a number of things of one kind ("meals", "art", "blocks", "textiles", or a def name).</summary>
    public class MissionGoal : IExposable
    {
        public string what;
        public int count;
        /// <summary>How many the colony has ready (checked every few seconds, for the World page).</summary>
        public int have;

        public void ExposeData()
        {
            Scribe_Values.Look(ref what, "what");
            Scribe_Values.Look(ref count, "count");
            Scribe_Values.Look(ref have, "have");
        }
    }

    /// <summary>A mission the colony was offered or took on.</summary>
    public class Mission : IExposable
    {
        public int id;
        public string kind;
        /// <summary>The offer: its texts and its worker.</summary>
        public IncidentDef def;
        public MissionState state;
        /// <summary>Its entry in the Quests tab, or -1 (offered by letter).</summary>
        public int questId = -1;
        public string title;
        public Faction faction;
        /// <summary>The colonist who dealt with them.</summary>
        public Pawn a;
        /// <summary>The runaway animal.</summary>
        public Pawn x;
        public string name;
        public string place;
        /// <summary>What {TALE} says in its texts when it isn't the goods (the shrine's quality).</summary>
        public string tale;
        /// <summary>A variant of the mission ("funeral", "wedding"; the shrine's quality).</summary>
        public string variant;
        public int offered;
        public int started = -1;
        public int deadline = -1;
        public int ended = -1;
        /// <summary>How long it runs once accepted.</summary>
        public float days;
        public int reward;
        public int goodwill;
        public int penalty;
        /// <summary>A step of the mission's own: 0 working on it, 1 goal met and the neighbors on their way, 2 waiting for an answer.</summary>
        public int stage;
        public int due = -1;
        public bool reminded;
        public List<MissionGoal> goals = new List<MissionGoal>();
        /// <summary>How it ended (a tag for the texts).</summary>
        public string outcome;

        public bool Open => state == MissionState.Offered || state == MissionState.Active;

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Defs.Look(ref def, "def");
            Scribe_Values.Look(ref state, "state");
            Scribe_Values.Look(ref questId, "questId", -1);
            Scribe_Values.Look(ref title, "title");
            Scribe_References.Look(ref faction, "faction");
            Scribe_References.Look(ref a, "a");
            Scribe_References.Look(ref x, "x");
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref place, "place");
            Scribe_Values.Look(ref tale, "tale");
            Scribe_Values.Look(ref variant, "variant");
            Scribe_Values.Look(ref offered, "offered");
            Scribe_Values.Look(ref started, "started", -1);
            Scribe_Values.Look(ref deadline, "deadline", -1);
            Scribe_Values.Look(ref ended, "ended", -1);
            Scribe_Values.Look(ref days, "days");
            Scribe_Values.Look(ref reward, "reward");
            Scribe_Values.Look(ref goodwill, "goodwill");
            Scribe_Values.Look(ref penalty, "penalty");
            Scribe_Values.Look(ref stage, "stage");
            Scribe_Values.Look(ref due, "due", -1);
            Scribe_Values.Look(ref reminded, "reminded");
            Scribe_Collections.Look(ref goals, "goals", LookMode.Deep);
            Scribe_Values.Look(ref outcome, "outcome");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (goals == null) goals = new List<MissionGoal>();
                goals.RemoveAll(g => g == null || g.what == null);
            }
        }
    }

    /// <summary>A mission's texts beyond the offer: its terms, the moments along the way, and how it ends (tagged by outcome).</summary>
    public class MissionText : HappeningMoreText
    {
        /// <summary>Under the offer: what must be done, by when, for what. {ITEM}: the goods; {DAYS}: the time once accepted.</summary>
        [MustTranslate] public List<string> terms;
        /// <summary>A message when the colony takes it on.</summary>
        [MustTranslate] public List<string> accepted;
        /// <summary>A message when the goal is met and the neighbors set out.</summary>
        [MustTranslate] public List<string> ready;
        /// <summary>Names the mission gives things (the runaway animal).</summary>
        [MustTranslate] public List<string> names;
    }

    /// <summary>The colony's missions: offering them (as quests or letters), taking them on, checking them, ending them.</summary>
    public static class Missions
    {
        public const string QuestRoot = "Council_MissionQuest";

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        private static int Now => Find.TickManager.TicksGame;

        public static List<Mission> All => StoriesComponent.Instance?.missions ?? new List<Mission>();

        public static Mission ById(int id) => All.FirstOrDefault(m => m.id == id);

        public static string Note(Mission m) => "mission:" + m.id;

        public static Mission Of(HappeningContext ctx)
        {
            string n = ctx?.note;
            if (n == null || !n.StartsWith("mission:") || !int.TryParse(n.Substring(8), out int id)) return null;
            return ById(id);
        }

        public static HappeningWorker_Mission Worker(Mission m) => m?.def?.Worker as HappeningWorker_Mission;

        public static MissionText Text(Mission m) => m?.def?.GetModExtension<MissionText>();

        /// <summary>One mission at a time, and the same kind not again for a while.</summary>
        public static bool CanOffer(string kind, float restDays)
        {
            int now = Now;
            return !All.Any(m => m.Open) && !All.Any(m => m.kind == kind && now - m.offered < restDays * GenDate.TicksPerDay);
        }

        public static bool Holds(Pawn p) => p != null && All.Any(m => m.Open && m.x == p);

        public static void Register(Mission m)
        {
            var comp = StoriesComponent.Instance;
            if (comp == null) return;
            m.id = comp.NextId();
            m.offered = Now;
            comp.missions.Add(m);
        }

        /// <summary>The colony's people, places and sums for a mission's texts.</summary>
        public static HappeningContext Context(Mission m, Map map)
        {
            var ctx = new HappeningContext { incident = m.def, map = map, a = m.a != null && !m.a.Dead && m.a.IsColonist ? m.a : null, faction = m.faction, placeName = m.place, count = m.reward, note = Note(m) };
            if (ctx.a == null && map != null) ctx.a = VarietyUtil.Best(map, SkillDefOf.Social) ?? StorytellerEvents.Adults(map).RandomElementWithFallback();
            ctx.beast = m.name;
            ctx.dead = m.name;
            ctx.thing = m.x?.def;
            ctx.tale = m.tale ?? GoodsText(m);
            return ctx;
        }

        // ---------------- The Quests tab ----------------

        public static bool AsQuests => S == null || S.missionsAsQuests;

        public static Quest QuestOf(Mission m) =>
            m == null || m.questId < 0 ? null : Find.QuestManager?.QuestsListForReading.FirstOrDefault(q => q.id == m.questId);

        /// <summary>
        /// Puts the offer in the Quests tab, with its own letter: accepted there, it starts; left alone, it expires there.
        /// False (nothing made) when the game won't take it, so the offer can come as a letter instead.
        /// </summary>
        public static bool TryOfferAsQuest(Mission m, string title, string description, float answerDays, int rating)
        {
            var root = DefDatabase<QuestScriptDef>.GetNamedSilentFail(QuestRoot);
            if (root == null || Find.QuestManager == null) return false;
            Quest quest = null;
            try
            {
                quest = Quest.MakeRaw();
                quest.root = root;
                quest.name = title;
                quest.description = description;
                quest.challengeRating = Mathf.Clamp(rating, 1, 5);
                quest.appearanceTick = Now;
                quest.acceptanceExpireTick = Now + Mathf.RoundToInt(Mathf.Max(0.5f, answerDays) * GenDate.TicksPerDay);
                quest.AddPart(new QuestPart_RimfolkMission { missionId = m.id });
                Find.QuestManager.Add(quest);
                m.questId = quest.id;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("mission quest", e);
                try
                {
                    if (quest != null && Find.QuestManager.Contains(quest)) Find.QuestManager.Remove(quest);
                }
                catch
                {
                }
                m.questId = -1;
                return false;
            }
            try
            {
                QuestUtility.SendLetterQuestAvailable(quest);
            }
            catch (Exception e)
            {
                // The quest is in the tab; a plain letter says so.
                CouncilLog.ErrorOnce("mission quest letter", e);
                Find.LetterStack.ReceiveLetter(title, description, LetterDefOf.NeutralEvent);
            }
            return true;
        }

        /// <summary>The quest is over: neither waiting for an answer nor under way.</summary>
        public static bool Ended(Quest q) => q.State != QuestState.NotYetAccepted && q.State != QuestState.Ongoing;

        private static void EndQuest(Mission m, QuestEndOutcome outcome)
        {
            var q = QuestOf(m);
            if (q == null) return;
            if (!Ended(q))
            {
                try
                {
                    q.End(outcome, false, false);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("mission quest end", e);
                }
            }
            Unlink(q);
        }

        /// <summary>
        /// An ended quest keeps only the game's own parts: in the history it reads the same, and a save that later loses
        /// Rimfolk has no part of ours to complain about in its old quests.
        /// </summary>
        private static void Unlink(Quest q)
        {
            if (q == null || !Ended(q)) return;
            try
            {
                var part = q.GetFirstPartOfType<QuestPart_RimfolkMission>();
                if (part != null) q.RemovePart(part);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("mission quest unlink", e);
            }
        }

        /// <summary>Opens the mission's quest in the Quests tab.</summary>
        public static void ShowQuest(Mission m)
        {
            var q = QuestOf(m);
            if (q == null) return;
            Find.MainTabsRoot.SetCurrentTab(MainButtonDefOf.Quests);
            (MainButtonDefOf.Quests.TabWindow as MainTabWindow_Quests)?.Select(q);
        }

        // ---------------- Taking it on, and ending it ----------------

        public static void Accept(Mission m)
        {
            if (m == null || m.state != MissionState.Offered) return;
            var map = HappeningTracker.HomeMap();
            var w = Worker(m);
            bool ok = false;
            if (map != null && w != null && LivingEvents.Friendly(m.faction))
            {
                try
                {
                    ok = w.Start(m, map);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("mission start " + m.kind, e);
                }
            }
            if (!ok)
            {
                // Taken on just as it fell through: their people are at war with the colony now (a letter says so), or
                // there's no way to begin (the quest simply ends).
                Finish(m, MissionState.Declined, LivingEvents.Friendly(m.faction) ? "declined" : "cancelled", null);
                return;
            }
            m.state = MissionState.Active;
            m.started = Now;
            m.deadline = Now + Mathf.RoundToInt(m.days * GenDate.TicksPerDay);
            var ctx = Context(m, map);
            string line = VarietyUtil.Line(Text(m)?.accepted, ctx, Tags(m), GoodsText(m), TextUtil.DaysString(m.deadline - Now));
            if (!line.NullOrEmpty()) Messages.Message(line, w.Targets(m), MessageTypeDefOf.PositiveEvent, false);
        }

        /// <summary>Turned down, or the offer ran out: nothing more to it.</summary>
        public static void Decline(Mission m)
        {
            if (m == null || m.state != MissionState.Offered) return;
            Finish(m, MissionState.Declined, "declined", null);
        }

        /// <summary>
        /// The end of a mission: its letter (none for an offer turned down), its quest ended, and the chronicle (through the
        /// quest, or straight to it). <paramref name="item"/> fills {ITEM}.
        /// </summary>
        public static void Finish(Mission m, MissionState state, string outcome, string item, LookTargets look = null, LetterDef letter = null)
        {
            if (m == null || !m.Open) return;
            bool wasActive = m.state == MissionState.Active;
            m.state = state;
            m.outcome = outcome;
            m.ended = Now;
            var w = Worker(m);
            try
            {
                w?.Release(m);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("mission cleanup " + m.kind, e);
            }
            var map = HappeningTracker.HomeMap();
            bool success = state == MissionState.Done;
            if (wasActive || outcome == "cancelled")
            {
                var text = Text(m);
                var ctx = Context(m, map);
                var tags = Tags(m);
                tags.Add(outcome);
                string body = outcome == "cancelled"
                    ? StoryChains.Line(text?.cancelled, ctx, null, tags, item)
                    : StoryChains.Line(success ? text?.done : text?.fail, ctx, outcome, tags, item);
                string title = StoryChains.Line(success ? text?.doneTitles : text?.failTitles, ctx, outcome, tags, item) ?? m.title;
                if (!body.NullOrEmpty())
                    Find.LetterStack.ReceiveLetter(title, body, letter ?? (success ? LetterDefOf.PositiveEvent : LetterDefOf.NegativeEvent), look ?? LookTargets.Invalid);
            }
            if (m.questId >= 0)
            {
                // Rimfolk's watch on finished quests writes the chronicle and the colony's pride.
                var q = QuestOf(m);
                EndQuest(m, success ? QuestEndOutcome.Success : wasActive && state == MissionState.Failed ? QuestEndOutcome.Fail
                    : q != null && q.EverAccepted ? QuestEndOutcome.Unknown : QuestEndOutcome.InvalidPreAcceptance);
            }
            else if (wasActive && state != MissionState.Declined && S.worldReactions)
            {
                var comp = CouncilGameComponent.Instance;
                if (comp != null) WorldReactions.QuestEnded(comp, m.title, success);
            }
        }

        /// <summary>A letter about this mission still waits for an answer.</summary>
        public static bool LetterOut(Mission m)
        {
            string note = Note(m);
            return Find.LetterStack.LettersListForReading.Any(l => l is ChoiceLetter_Happening h && !h.resolved && h.ctx?.note == note);
        }

        public static List<string> Tags(Mission m)
        {
            var tags = new List<string>();
            if (!m.kind.NullOrEmpty()) tags.Add(m.kind);
            if (!m.variant.NullOrEmpty()) tags.Add(m.variant);
            return tags;
        }

        /// <summary>Goodwill with the neighbor, never enough to turn them hostile.</summary>
        public static void Goodwill(Mission m, int amount) => StoryChains.Goodwill(m.faction, amount);

        // ---------------- Every few seconds, and every hour ----------------

        public static void Tick(StoriesComponent comp, int now)
        {
            foreach (var m in comp.missions.Where(m => m.Open).ToList())
            {
                try
                {
                    // Its offer is gone from the game (an update dropped it): nothing can carry it on.
                    if (Worker(m) == null)
                    {
                        Finish(m, m.state == MissionState.Active ? MissionState.Failed : MissionState.Declined, "ended", null);
                        continue;
                    }
                    if (m.state == MissionState.Offered)
                    {
                        // Offered by letter: answered there. If the letter is gone without an answer, so is the offer.
                        if (m.questId < 0)
                        {
                            if (now - m.offered > GenDate.TicksPerDay * 4 && !LetterOut(m)) Decline(m);
                            continue;
                        }
                        var q = QuestOf(m);
                        if (q == null) Decline(m);
                        else if (q.State == QuestState.Ongoing) Accept(m);
                        else if (q.State != QuestState.NotYetAccepted) Decline(m);
                        continue;
                    }
                    // A quest ended by someone else (the colony moved on): the mission ends with it, without a word.
                    var quest = QuestOf(m);
                    if (m.questId >= 0 && (quest == null || Ended(quest)))
                    {
                        Finish(m, MissionState.Failed, "ended", null);
                        continue;
                    }
                    var map = HappeningTracker.HomeMap();
                    if (map == null)
                    {
                        // No home to do it from (the colony packed up and left): once its time is well past, it lapses.
                        if (m.deadline > 0 && now > m.deadline + GenDate.TicksPerDay * 2) Finish(m, MissionState.Failed, "ended", null);
                        continue;
                    }
                    if (!LivingEvents.Friendly(m.faction))
                    {
                        Finish(m, MissionState.Failed, "cancelled", null);
                        continue;
                    }
                    Worker(m)?.Check(m, map, now);
                }
                catch (Exception e)
                {
                    // Something it relies on is broken: it ends quietly rather than sitting in the Quests tab forever.
                    CouncilLog.ErrorOnce("mission " + m.kind, e);
                    try
                    {
                        Finish(m, m.state == MissionState.Active ? MissionState.Failed : MissionState.Declined, "ended", null);
                    }
                    catch (Exception e2)
                    {
                        CouncilLog.ErrorOnce("mission end " + m.kind, e2);
                        m.state = MissionState.Failed;
                        m.ended = now;
                    }
                }
            }
        }

        public static void Hourly(StoriesComponent comp, int now)
        {
            foreach (var m in comp.missions.Where(m => m.state == MissionState.Active && m.stage == 0 && !m.reminded && m.deadline > 0 && now >= m.deadline - GenDate.TicksPerDay).ToList())
            {
                m.reminded = true;
                var map = HappeningTracker.HomeMap();
                string line = VarietyUtil.Line(Text(m)?.reminder, Context(m, map), Tags(m), GoodsText(m), TextUtil.DaysString(Mathf.Max(0, m.deadline - now)));
                if (!line.NullOrEmpty()) Messages.Message(line, Worker(m)?.Targets(m) ?? LookTargets.Invalid, MessageTypeDefOf.NeutralEvent, false);
            }
            // Finished missions stay on the World page for a day, then only in the chronicle.
            comp.missions.RemoveAll(m => !m.Open && now - m.ended > GenDate.TicksPerDay * 2);
            // A Rimfolk quest with no mission behind it (should never happen): ended, so it can't sit in the tab forever.
            var quests = Find.QuestManager?.QuestsListForReading;
            if (quests == null) return;
            foreach (var q in quests.Where(q => q.root?.defName == QuestRoot).ToList())
            {
                if (comp.missions.Any(m => m.Open && m.questId == q.id)) continue;
                if (!Ended(q))
                {
                    try
                    {
                        q.End(q.EverAccepted ? QuestEndOutcome.Unknown : QuestEndOutcome.InvalidPreAcceptance, false, false);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("mission orphan quest", e);
                    }
                }
                Unlink(q);
            }
        }

        // ---------------- Goods ----------------

        /// <summary>Stored, unforbidden things that count for a goal.</summary>
        public static IEnumerable<Thing> Matching(Map map, string what)
        {
            if (map == null) yield break;
            if (what == "art")
            {
                // Sculptures packed up and ready to carry: anywhere on the map, as long as nobody has forbidden them.
                // One the colony has already placed for installing is spoken for.
                foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.MinifiedThing))
                    if (t.Spawned && !t.IsForbidden(Faction.OfPlayer) && t is MinifiedThing mt && mt.InnerThing?.TryGetComp<CompArt>() != null
                        && InstallBlueprintUtility.ExistingBlueprintFor(t) == null)
                        yield return t;
                yield break;
            }
            var def = what == "meals" || what == "blocks" || what == "textiles" ? null : DefDatabase<ThingDef>.GetNamedSilentFail(what);
            IEnumerable<Thing> pool = def != null ? map.listerThings.ThingsOfDef(def) : map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver);
            foreach (var t in pool)
            {
                if (!t.Spawned || t.IsForbidden(Faction.OfPlayer) || !t.IsInAnyStorage()) continue;
                if (def == null && !Fits(t, what)) continue;
                yield return t;
            }
        }

        private static bool Fits(Thing t, string what)
        {
            var d = t.def;
            switch (what)
            {
                case "meals":
                    if (d.ingestible == null || !d.IsNutritionGivingIngestible || d.IsDrug || d.ingestible.preferability < FoodPreferability.MealSimple) return false;
                    var rot = t.TryGetComp<CompRottable>();
                    return rot == null || rot.Stage == RotStage.Fresh;
                case "blocks":
                    return d.thingCategories != null && d.thingCategories.Contains(ThingCategoryDefOf.StoneBlocks);
                case "textiles":
                    return d.thingCategories != null && d.thingCategories.Contains(ThingCategoryDefOf.Textiles);
            }
            return false;
        }

        public static int Count(Map map, string what) => Matching(map, what).Sum(t => t.stackCount);

        /// <summary>
        /// Takes up to <paramref name="n"/> of a goal's goods from the colony. Returns how many were taken. Never what someone
        /// is about to use (a meal a colonist is going to eat, an ingredient a cook has claimed, a stack being hauled).
        /// </summary>
        public static int Take(Map map, string what, int n)
        {
            int left = n;
            foreach (var t in Matching(map, what).Where(t => !Claimed(t, map)).OrderBy(t => t.stackCount).ToList())
            {
                if (left <= 0) break;
                int take = Mathf.Min(left, t.stackCount);
                t.SplitOff(take).Destroy();
                left -= take;
            }
            return n - left;
        }

        private static bool Claimed(Thing t, Map map) =>
            map.reservationManager.IsReserved(t) || map.physicalInteractionReservationManager.IsReserved(t);

        /// <summary>What one of a goal's goods is worth, roughly (for the price).</summary>
        public static float UnitValue(string what)
        {
            switch (what)
            {
                case "meals": return 15f;
                case "art": return 220f;
                case "blocks": return 1.5f;
                case "textiles": return 2f;
            }
            return DefDatabase<ThingDef>.GetNamedSilentFail(what)?.BaseMarketValue ?? 1f;
        }

        /// <summary>"30 meals (simple or better), 2 sculptures and 80 stone blocks".</summary>
        public static string GoodsText(Mission m)
        {
            if (m?.goals.NullOrEmpty() ?? true) return "";
            return TextUtil.JoinAnd(m.goals.Select(Need).ToList());
        }

        public static string Need(MissionGoal g)
        {
            switch (g.what)
            {
                case "meals": return "CC_Mission_Need_Meals".Translate(g.count);
                case "art": return g.count == 1 ? "CC_Mission_Need_Art1".Translate().ToString() : "CC_Mission_Need_Art".Translate(g.count).ToString();
                case "blocks": return "CC_Mission_Need_Blocks".Translate(g.count);
                case "textiles": return "CC_Mission_Need_Textiles".Translate(g.count);
            }
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(g.what);
            return "CC_Mission_Need_Thing".Translate(g.count, def?.label ?? g.what);
        }

        public static string GoalLabel(MissionGoal g)
        {
            switch (g.what)
            {
                case "meals":
                case "art":
                case "blocks":
                case "textiles":
                    return ("CC_Mission_Goal_" + g.what).Translate();
            }
            return DefDatabase<ThingDef>.GetNamedSilentFail(g.what)?.LabelCap ?? g.what;
        }

        // ---------------- The World page and the quest ----------------

        /// <summary>The progress of a mission, a line per goal (for the quest's description).</summary>
        public static string ProgressText(Mission m)
        {
            if (m == null) return null;
            if (m.state != MissionState.Active) return null;
            var lines = new List<string>();
            foreach (var g in m.goals) lines.Add("  - " + GoalLabel(g) + ": " + g.have + " / " + g.count);
            string status = Worker(m)?.Status(m);
            if (!status.NullOrEmpty()) lines.Add(status);
            if (m.deadline > 0) lines.Add("CC_Missions_TimeLeft".Translate(TextUtil.DaysString(Mathf.Max(0, m.deadline - Now))));
            return "CC_Missions_Progress".Translate() + "\n" + string.Join("\n", lines);
        }
    }

    /// <summary>
    /// The quest's link back to Rimfolk's mission: the mission's progress under the quest's description, the animal to
    /// look at, and the people it holds kept out of other quests' hands.
    /// </summary>
    public class QuestPart_RimfolkMission : QuestPart
    {
        public int missionId;

        private Mission Mission => Missions.ById(missionId);

        public override string DescriptionPart
        {
            get
            {
                try
                {
                    return Missions.ProgressText(Mission);
                }
                catch
                {
                    return null;
                }
            }
        }

        public override IEnumerable<GlobalTargetInfo> QuestLookTargets
        {
            get
            {
                var m = Mission;
                if (m?.x != null && m.x.Spawned) yield return m.x;
            }
        }

        public override bool QuestPartReserves(Pawn p) => p != null && Mission?.x == p;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref missionId, "missionId");
        }
    }
}
