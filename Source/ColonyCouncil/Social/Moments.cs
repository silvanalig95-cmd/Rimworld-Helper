using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// When a moment between two colonists can happen, written in XML on an InteractionDef. Moments are ordinary
    /// conversations: the game's own social system picks them among all the others, they go into the social log, and
    /// mods that show the log (Interaction Bubbles) show them too. Everything here is optional; a moment with no
    /// conditions can happen any time two colonists talk.
    /// </summary>
    public class MomentExtension : DefModExtension
    {
        /// <summary>How likely, next to the game's own conversations (chitchat is 1, a deep talk 0.075).</summary>
        public float weight = 0.03f;
        /// <summary>The same person won't start this same moment again for this long.</summary>
        public float cooldownDays = 3f;
        /// <summary>Only when one of these mods is active (package ids). Leave empty for every game.</summary>
        public List<string> mods;
        /// <summary>Only when the game has one of these defs (by name): a trait, need or thought a mod adds.</summary>
        public List<string> needsDefs;
        /// <summary>Tag expressions (see Flavor): "evening|night,!child", "trait:Kind", "skill:Cooking", "afterraid".</summary>
        public string initiatorTags;
        public string recipientTags;
        /// <summary>The initiator's opinion of the recipient, from..to.</summary>
        public int minOpinion = -100;
        public int maxOpinion = 100;
        /// <summary>Both think well of each other (opinion 20 or more both ways).</summary>
        public bool mutualFriends;
        /// <summary>A bond between them first: Friends, Rivals, Comrades, Mentor.</summary>
        public string tie;
        /// <summary>The initiator (or recipient) has one of these memories, recently (defNames; "Prefix*" for a family).</summary>
        public List<string> initiatorMemories;
        public List<string> recipientMemories;
        public float memoryDays = 2f;
        /// <summary>Both remember the same thing (any of initiatorMemories), e.g. both grieving the same loss.</summary>
        public bool sharedMemory;
        /// <summary>A skill they are both passionate about.</summary>
        public bool sharedPassion;
        /// <summary>The recipient is the colony's best cook.</summary>
        public bool recipientBestCook;
        /// <summary>The initiator's need (NeedDef name, e.g. a hygiene need from a mod) is below needBelow, or above needAbove.</summary>
        public string need;
        public float needBelow = -1f;
        public float needAbove = 2f;
        /// <summary>Same or different ideoligion (only with Ideology).</summary>
        public bool sameIdeo;
        public bool otherIdeo;
        /// <summary>The colony's ways: "proud" (the initiator approves), "chafing", "clash" (one approves, the other chafes).</summary>
        public string ways;
        /// <summary>The initiator has a bonded animal.</summary>
        public bool bondedPet;
        /// <summary>The initiator was hurt by a nemesis who is still out there.</summary>
        public bool nemesisVictim;
        /// <summary>A friendly contest decided by this skill (plus luck). Nobody gets hurt.</summary>
        public SkillDef contestSkill;
        public ThoughtDef winnerThought;
        public ThoughtDef loserThought;
        /// <summary>Lines added to the log entry saying who won.</summary>
        public RulePackDef initiatorWonPack;
        public RulePackDef recipientWonPack;
        /// <summary>Afterwards: a bond grows (Friends, Rivals, Comrades, Mentor).</summary>
        public string bumpTie;
        public float bumpAmount = 2f;
        /// <summary>What they share: "bedroom" (they sleep in the same room: a barracks, a shared bedroom or bed) or "work" (the same kind of work, right now).</summary>
        public string shares;
        /// <summary>The initiator is clearly better (4 levels or more) at the work the recipient is doing right now.</summary>
        public bool moreSkilled;
        /// <summary>
        /// What the initiator (or recipient) feels: the heart's FeelingDef keys (Fear, Gratitude...), at least
        /// <see cref="minFeeling"/> of full strength. Never with the heart off.
        /// </summary>
        public List<string> initiatorFeelings;
        public List<string> recipientFeelings;
        public float minFeeling = 0.3f;
        /// <summary>The initiator's feeling (initiatorFeelings) is about the recipient: the one they're grateful to, angry at.</summary>
        public bool feelingAboutRecipient;
        /// <summary>Has one of Rimfolk's ailments or illnesses, visible (Ailing), or none at all (Well).</summary>
        public bool initiatorAiling;
        public bool initiatorWell;
        public bool recipientAiling;
        public bool recipientWell;
        /// <summary>Has one of these conditions, visible (HediffDef names; "Prefix*" for a family).</summary>
        public List<string> initiatorHediffs;
        public List<string> recipientHediffs;
        /// <summary>A camp (world events) is set up on their map.</summary>
        public bool camp;
        /// <summary>A world event of one of these kinds ("sickness", "hunger", "fair") or defNames is running.</summary>
        public List<string> situations;
        /// <summary>
        /// What the moment does between the two: "warm" (the default), "friction" (a small irritation), "mend" (making up
        /// after a friction) or "neutral". See <see cref="MomentTones"/>: after a friction, warm moments between the same
        /// two wait until it has worn off or they have made up; after a warm one, a friction is less likely for a while.
        /// </summary>
        public string tone = "warm";
        /// <summary>A friction: how long warm moments between the two stay off afterwards (longer with abrasive or volatile people).</summary>
        public float soreHours = 12f;
        /// <summary>Only between two who had a friction lately and haven't made up yet.</summary>
        public bool stillSore;

        public bool Friction => tone == "friction";
        public bool Mend => tone == "mend";

        [Unsaved] private bool? available;
        [Unsaved] private string initiatorExpr, recipientExpr;
        [Unsaved] private bool namesLooked;
        [Unsaved] private TieKind? tieKind;
        [Unsaved] private NeedDef needDef;

        public string InitiatorExpr => initiatorExpr ??= "[" + initiatorTags + "]";
        public string RecipientExpr => recipientExpr ??= "[" + recipientTags + "]";

        /// <summary>
        /// The tie and need named in XML, read once (the game weighs every moment each time two colonists talk): the tie
        /// kind, or null when none is named (or the name is unknown); the need's def, or null.
        /// </summary>
        private void LookNames()
        {
            if (namesLooked) return;
            tieKind = !tie.NullOrEmpty() && System.Enum.TryParse(tie, out TieKind k) ? k : (TieKind?)null;
            needDef = need.NullOrEmpty() ? null : DefDatabase<NeedDef>.GetNamedSilentFail(need);
            namesLooked = true;
        }

        public TieKind? TieNamed
        {
            get
            {
                LookNames();
                return tieKind;
            }
        }

        public NeedDef NeedNamed
        {
            get
            {
                LookNames();
                return needDef;
            }
        }

        /// <summary>Its mods and defs are in this game.</summary>
        public bool Available
        {
            get
            {
                if (available == null)
                {
                    bool ok = mods.NullOrEmpty() || mods.Any(People.ModActive);
                    if (ok && !needsDefs.NullOrEmpty())
                        ok = needsDefs.Any(n => DefDatabase<TraitDef>.GetNamedSilentFail(n) != null || DefDatabase<NeedDef>.GetNamedSilentFail(n) != null
                                                || DefDatabase<ThoughtDef>.GetNamedSilentFail(n) != null || DefDatabase<ThingDef>.GetNamedSilentFail(n) != null);
                    available = ok;
                }
                return available.Value;
            }
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            foreach (var t in new[] { tie, bumpTie })
                if (!t.NullOrEmpty() && !System.Enum.TryParse(t, out TieKind _)) yield return "unknown tie kind " + t;
            if (!ways.NullOrEmpty() && ways != "proud" && ways != "chafing" && ways != "clash") yield return "ways must be proud, chafing or clash";
            if (contestSkill != null && (winnerThought == null || loserThought == null)) yield return "a contest needs winnerThought and loserThought";
            if (!shares.NullOrEmpty() && shares != "bedroom" && shares != "work") yield return "shares must be bedroom or work";
            if (!tone.NullOrEmpty() && tone != "warm" && tone != "friction" && tone != "mend" && tone != "neutral") yield return "tone must be warm, friction, mend or neutral";
            if (stillSore && !Friction) yield return "stillSore is for frictions (a mend always follows one)";
        }
    }

    /// <summary>
    /// A moment between two colonists: shared grief, talking shop, praising the cook, old stories, a friendly contest.
    /// Its conditions come from the def's <see cref="MomentExtension"/>.
    /// </summary>
    public class InteractionWorker_Moment : InteractionWorker
    {
        // Who started which moment when (not saved: after a load, moments can simply come up again).
        private static readonly Dictionary<(int, ushort), int> lastStarted = new Dictionary<(int, ushort), int>();
        // Any moment at all, per person: moments stay special, not every other sentence.
        private static readonly Dictionary<int, int> lastAny = new Dictionary<int, int>();
        private const int MinGapTicks = GenDate.TicksPerHour * 6;
        // Tags are worked out at most every few seconds of game time per colonist.
        private static readonly Dictionary<int, (int tick, HashSet<string> tags)> tagCache = new Dictionary<int, (int, HashSet<string>)>();

        private MomentExtension Ext => interaction.GetModExtension<MomentExtension>();

        /// <summary>
        /// A hook for other parts of the mod to nudge how likely moments are (a colonist's mood of the day, say): a factor
        /// on the weight, given the initiator, the recipient, whether it's a friction, and its kind ("grief" for grief
        /// shared, else null). Unset, it changes nothing. The heart's feelings use it (Heart.MomentWeightFactor).
        /// </summary>
        public static System.Func<Pawn, Pawn, bool, string, float> WeightHook;

        /// <summary>The factor from <see cref="WeightHook"/>: 1 when nothing is hooked in, never below 0.</summary>
        public static float MomentWeightFactor(Pawn initiator, Pawn recipient, bool friction, string kind)
        {
            if (WeightHook == null) return 1f;
            float f = WeightHook(initiator, recipient, friction, kind);
            return f > 0f ? f : 0f;
        }

        private string kind;
        private bool kindLooked;

        /// <summary>"grief" for moments of grief shared, which the heart weighs on their own (worked out once).</summary>
        private string Kind
        {
            get
            {
                if (!kindLooked)
                {
                    kind = interaction?.defName != null && interaction.defName.IndexOf("Grief", System.StringComparison.Ordinal) >= 0 ? "grief" : null;
                    kindLooked = true;
                }
                return kind;
            }
        }

        public override float RandomSelectionWeight(Pawn initiator, Pawn recipient)
        {
            try
            {
                var ext = Ext;
                if (ext == null || !ColonyCouncilMod.Settings.colonistMoments || !ext.Available) return 0f;
                if (ext.Friction && !ColonyCouncilMod.Settings.frictions) return 0f;
                if (initiator == null || recipient == null || !recipient.RaceProps.Humanlike || !initiator.RaceProps.Humanlike) return 0f;
                if (!Ours(initiator) || !Ours(recipient) || !People.HasInnerLife(initiator) || !People.HasInnerLife(recipient)) return 0f;
                int now = Find.TickManager.TicksGame;
                // (A time in the future means an older save was loaded: that wait is over.)
                if (lastAny.TryGetValue(initiator.thingIDNumber, out int any) && any <= now && now - any < MinGapTicks) return 0f;
                if (lastStarted.TryGetValue((initiator.thingIDNumber, interaction.shortHash), out int last) && last <= now && now - last < ext.cooldownDays * GenDate.TicksPerDay) return 0f;
                if (!Fits(ext, initiator, recipient)) return 0f;
                float tone = MomentTones.Factor(ext, initiator, recipient);
                if (tone <= 0f) return 0f;
                return ext.weight * tone * ColonyCouncilMod.Settings.momentFrequency * MomentWeightFactor(initiator, recipient, ext.Friction, Kind);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("moment weight " + interaction?.defName, e);
                return 0f;
            }
        }

        private static bool Ours(Pawn p) => p.IsColonist || p.IsSlaveOfColony;

        /// <summary>Forget the last game's people (called when a game loads).</summary>
        public static void Reset()
        {
            lastStarted.Clear();
            lastAny.Clear();
            tagCache.Clear();
            MomentTones.ResetCache();
        }

        private static bool Fits(MomentExtension x, Pawn a, Pawn b)
        {
            var social = CouncilGameComponent.Instance?.social;
            if (x.TieNamed is TieKind kind && social?.Get(a, b, kind) == null) return false;
            if (x.bondedPet && !(a.relations?.DirectRelations.Any(r => r.def == PawnRelationDefOf.Bond && r.otherPawn != null && !r.otherPawn.Dead) ?? false)) return false;
            if (x.nemesisVictim && !(CouncilGameComponent.Instance?.nemeses?.Of(a).Any(n => !n.ended) ?? false)) return false;
            if (x.sameIdeo || x.otherIdeo)
            {
                if (!ModsConfig.IdeologyActive || a.Ideo == null || b.Ideo == null) return false;
                if (x.sameIdeo && a.Ideo != b.Ideo) return false;
                if (x.otherIdeo && a.Ideo == b.Ideo) return false;
            }
            if (!x.need.NullOrEmpty())
            {
                var def = x.NeedNamed;
                var n = def != null ? a.needs?.TryGetNeed(def) : null;
                if (n == null) return false;
                if (x.needBelow >= 0f && n.CurLevelPercentage >= x.needBelow) return false;
                if (x.needAbove <= 1f && n.CurLevelPercentage <= x.needAbove) return false;
            }
            if (!x.ways.NullOrEmpty())
            {
                var ch = CouncilGameComponent.Instance?.character;
                if (ch == null || !ColonyCouncilMod.Settings.colonyCharacter) return false;
                float ma = ch.Approval(a), mb = ch.Approval(b);
                float line = ThoughtWorker_ColonyWays.Mild;
                if (x.ways == "proud" && ma < line) return false;
                if (x.ways == "chafing" && ma > -line) return false;
                if (x.ways == "clash" && !((ma >= line && mb <= -line) || (ma <= -line && mb >= line))) return false;
            }
            if (x.initiatorMemories != null && Remembered(a, x.initiatorMemories, x.memoryDays) == null) return false;
            if (x.recipientMemories != null && Remembered(b, x.recipientMemories, x.memoryDays) == null) return false;
            if (x.sharedMemory && x.initiatorMemories != null)
            {
                // The same kind of memory, about the same person if it's about someone.
                var mine = Remembered(a, x.initiatorMemories, x.memoryDays);
                var theirs = mine == null ? null : b.needs?.mood?.thoughts?.memories?.Memories
                    .FirstOrDefault(m => m.def == mine.def && m.otherPawn == mine.otherPawn && m.age < x.memoryDays * GenDate.TicksPerDay);
                if (theirs == null) return false;
            }
            if (x.sharedPassion && !SharedPassion(a, b)) return false;
            if (x.recipientBestCook && !BestCook(b)) return false;
            if (x.minOpinion > -100 || x.maxOpinion < 100 || x.mutualFriends)
            {
                int opinion = a.relations?.OpinionOf(b) ?? 0;
                if (opinion < x.minOpinion || opinion > x.maxOpinion) return false;
                if (x.mutualFriends && (opinion < 20 || (b.relations?.OpinionOf(a) ?? 0) < 20)) return false;
            }
            if (!x.initiatorTags.NullOrEmpty() && Flavor.Match(x.InitiatorExpr, TagsOf(a)) < 0) return false;
            if (!x.recipientTags.NullOrEmpty() && Flavor.Match(x.RecipientExpr, TagsOf(b)) < 0) return false;
            if (x.shares == "bedroom" && !SameBedroom(a, b)) return false;
            if (x.shares == "work" && !SameWork(a, b)) return false;
            if (x.moreSkilled && !MoreSkilled(a, b)) return false;
            return FitsLife(x, a, b);
        }

        /// <summary>What's going on in their lives: feelings, ailments, a camp, the region (see <see cref="LifeConditions"/>).</summary>
        private static bool FitsLife(MomentExtension x, Pawn a, Pawn b)
        {
            if ((x.initiatorAiling || x.initiatorWell) && LifeConditions.Ailing(a) != x.initiatorAiling) return false;
            if ((x.recipientAiling || x.recipientWell) && LifeConditions.Ailing(b) != x.recipientAiling) return false;
            if (x.initiatorHediffs != null && !LifeConditions.Has(a, x.initiatorHediffs)) return false;
            if (x.recipientHediffs != null && !LifeConditions.Has(b, x.recipientHediffs)) return false;
            if (x.camp && !LifeConditions.CampOn(a.MapHeld)) return false;
            if (x.situations != null && !LifeConditions.Situation(x.situations)) return false;
            if (x.initiatorFeelings != null)
            {
                var felt = LifeConditions.Feels(a, x.initiatorFeelings, x.minFeeling, x.feelingAboutRecipient);
                if (felt == null || (x.feelingAboutRecipient && felt.target != b)) return false;
            }
            if (x.recipientFeelings != null && LifeConditions.Feels(b, x.recipientFeelings, x.minFeeling) == null) return false;
            return true;
        }

        /// <summary>They sleep in the same room: a barracks, a shared bedroom, or the same bed.</summary>
        private static bool SameBedroom(Pawn a, Pawn b)
        {
            var room = RoomUtil.BedRoom(a);
            return room != null && !room.PsychologicallyOutdoors && room == RoomUtil.BedRoom(b);
        }

        /// <summary>They're doing the same kind of work right now (two cooks in the kitchen, two crafters at the benches).</summary>
        private static bool SameWork(Pawn a, Pawn b)
        {
            var work = a.CurJob?.workGiverDef?.workType;
            return work != null && work == b.CurJob?.workGiverDef?.workType;
        }

        /// <summary>The initiator is 4 or more levels better at a skill the recipient's work right now uses.</summary>
        private static bool MoreSkilled(Pawn a, Pawn b)
        {
            var skills = b.CurJob?.workGiverDef?.workType?.relevantSkills;
            if (skills.NullOrEmpty() || a.skills == null || b.skills == null) return false;
            foreach (var s in skills)
            {
                var mine = a.skills.GetSkill(s);
                var theirs = b.skills.GetSkill(s);
                if (mine != null && theirs != null && !mine.TotallyDisabled && !theirs.TotallyDisabled && mine.Level >= theirs.Level + 4) return true;
            }
            return false;
        }

        private static Thought_Memory Remembered(Pawn p, List<string> names, float days)
        {
            var memories = p.needs?.mood?.thoughts?.memories?.Memories;
            if (memories == null) return null;
            int max = Mathf.RoundToInt(days * GenDate.TicksPerDay);
            foreach (var m in memories)
            {
                if (m?.def == null || m.age > max) continue;
                string name = m.def.defName;
                foreach (var n in names)
                {
                    if (n.NullOrEmpty()) continue;
                    // "Prefix*" compared in place: this runs each time the game weighs a moment, so nothing is allocated.
                    bool match = n[n.Length - 1] == '*'
                        ? name.Length >= n.Length - 1 && string.CompareOrdinal(name, 0, n, 0, n.Length - 1) == 0
                        : name == n;
                    if (match) return m;
                }
            }
            return null;
        }

        private static bool SharedPassion(Pawn a, Pawn b)
        {
            if (a.skills == null || b.skills == null) return false;
            foreach (var s in a.skills.skills)
            {
                if (s.TotallyDisabled || !People.Passionate(s)) continue;
                var other = b.skills.GetSkill(s.def);
                if (other != null && !other.TotallyDisabled && People.Passionate(other)) return true;
            }
            return false;
        }

        private static bool BestCook(Pawn p)
        {
            var cooking = DefDatabase<WorkTypeDef>.GetNamedSilentFail("Cooking");
            var mine = p.skills?.GetSkill(SkillDefOf.Cooking);
            if (mine == null || mine.TotallyDisabled || (cooking != null && p.WorkTypeIsDisabled(cooking)) || mine.Level < 6) return false;
            var map = p.MapHeld;
            if (map == null) return false;
            foreach (var other in map.mapPawns.FreeColonists)
            {
                if (other == p || other.skills == null || (cooking != null && other.WorkTypeIsDisabled(cooking))) continue;
                var s = other.skills.GetSkill(SkillDefOf.Cooking);
                if (s != null && !s.TotallyDisabled && s.Level > mine.Level) return false;
            }
            return true;
        }

        private static HashSet<string> TagsOf(Pawn p)
        {
            int now = Find.TickManager.TicksGame;
            if (tagCache.TryGetValue(p.thingIDNumber, out var c) && now - c.tick < 250 && now >= c.tick) return c.tags;
            if (tagCache.Count > 300) tagCache.Clear();
            var tags = Flavor.TagsFor(p);
            tagCache[p.thingIDNumber] = (now, tags);
            return tags;
        }

        public override void Interacted(Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks, out string letterText, out string letterLabel, out LetterDef letterDef, out LookTargets lookTargets)
        {
            base.Interacted(initiator, recipient, extraSentencePacks, out letterText, out letterLabel, out letterDef, out lookTargets);
            var ext = Ext;
            if (ext == null || initiator == null || recipient == null) return;
            int now = Find.TickManager.TicksGame;
            lastStarted[(initiator.thingIDNumber, interaction.shortHash)] = now;
            lastAny[initiator.thingIDNumber] = now;
            if (lastStarted.Count > 2000) lastStarted.Clear();
            try
            {
                if (ext.contestSkill != null) Contest(ext, initiator, recipient, extraSentencePacks);
                if (!ext.bumpTie.NullOrEmpty() && System.Enum.TryParse(ext.bumpTie, out TieKind kind))
                    CouncilGameComponent.Instance?.social?.Bump(initiator, recipient, kind, ext.bumpAmount, interaction.label, true);
                MomentTones.Record(ext, initiator, recipient, extraSentencePacks);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("moment " + interaction.defName, e);
            }
        }

        /// <summary>A friendly contest: skill and luck decide; the winner is pleased, the loser takes it in good spirit (mostly).</summary>
        private void Contest(MomentExtension x, Pawn a, Pawn b, List<RulePackDef> packs)
        {
            float Score(Pawn p) => (p.skills?.GetSkill(x.contestSkill)?.Level ?? 0) + Rand.Range(0f, 8f);
            bool initiatorWins = Score(a) >= Score(b);
            var winner = initiatorWins ? a : b;
            var loser = initiatorWins ? b : a;
            var pack = initiatorWins ? x.initiatorWonPack : x.recipientWonPack;
            if (pack != null) packs?.Add(pack);
            CouncilGameComponent.GiveThought(winner, x.winnerThought, loser, -1f, "CC_Cause_Contest".Translate(interaction.label, loser.LabelShort).Resolve());
            CouncilGameComponent.GiveThought(loser, x.loserThought, winner, -1f, "CC_Cause_Contest".Translate(interaction.label, winner.LabelShort).Resolve());
            winner.skills?.Learn(x.contestSkill, 30f);
            loser.skills?.Learn(x.contestSkill, 30f);
        }
    }

    /// <summary>How things stand between two colonists lately (see <see cref="MomentTones"/>). Ticks; -1 for never.</summary>
    public class PairTone : IExposable
    {
        /// <summary>The two, by thingIDNumber, lower first.</summary>
        public int a, b;
        public int friction = -1;
        public int soreUntil = -1;
        public int warm = -1;
        public int mend = -1;

        public int Latest => Mathf.Max(Mathf.Max(friction, soreUntil), Mathf.Max(warm, mend));

        public void ExposeData()
        {
            Scribe_Values.Look(ref a, "a");
            Scribe_Values.Look(ref b, "b");
            Scribe_Values.Look(ref friction, "friction", -1);
            Scribe_Values.Look(ref soreUntil, "soreUntil", -1);
            Scribe_Values.Look(ref warm, "warm", -1);
            Scribe_Values.Look(ref mend, "mend", -1);
        }
    }

    /// <summary>
    /// Keeps moments between the same two colonists making sense over time. Each pair remembers its last friction (a
    /// Rimfolk one, or the game's own insult, slight or social fight), how long it stays sore, its last warm moment (or a
    /// deep talk or kind words) and when they last made up.
    /// <list type="bullet">
    /// <item>After a friction, warm moments between them wait until it has worn off (half a day to a day, half as long
    /// again when one of them is abrasive or volatile), unless they make up first.</item>
    /// <item>Making up ("mend" moments) needs a friction they haven't made up for, a few hours old at least, and is
    /// likelier between friends and when one of them is kind.</item>
    /// <item>After a warm moment or making up, a friction between them is much less likely for a few hours (less so
    /// when the one starting it is abrasive or volatile).</item>
    /// <item>A friction soon after another gets a line saying things have been tense; the first warm moment after
    /// making up gets one saying they've patched things up.</item>
    /// </list>
    /// Pairs are kept a few days, saved with the social web and pruned daily.
    /// </summary>
    public class MomentTones : IExposable
    {
        private List<PairTone> pairs = new List<PairTone>();
        [Unsaved] private Dictionary<(int, int), PairTone> byPair;

        private const int KeepTicks = GenDate.TicksPerDay * 4;
        private const int MendAfterTicks = GenDate.TicksPerHour * 3;
        private const int UnmendedTicks = GenDate.TicksPerDay * 2;
        private const int CalmTicks = GenDate.TicksPerHour * 6;

        private struct State
        {
            public int friction, soreUntil, warm, mend;
        }

        // Every moment's weight is asked for one pair at a time, so a pair's state is worked out once per tick.
        private static int cacheTick = -1, cacheA = -1, cacheB = -1;
        private static State cached;

        private static MomentTones Current => CouncilGameComponent.Instance?.social?.tones;

        private static (int, int) Key(Pawn x, Pawn y) =>
            x.thingIDNumber < y.thingIDNumber ? (x.thingIDNumber, y.thingIDNumber) : (y.thingIDNumber, x.thingIDNumber);

        /// <summary>Abrasive or volatile: slower to let a friction go, quicker to start one.</summary>
        private static bool Temper(Pawn p) => PeopleTags.Has(p, "trait:Abrasive") || PeopleTags.Has(p, "trait:Volatile");

        public static void ResetCache() => cacheTick = -1;

        public PairTone Get(Pawn x, Pawn y, bool create)
        {
            if (byPair == null)
            {
                byPair = new Dictionary<(int, int), PairTone>();
                foreach (var p in pairs) byPair[(p.a, p.b)] = p;
            }
            var key = Key(x, y);
            if (byPair.TryGetValue(key, out var t) || !create) return t;
            t = new PairTone { a = key.Item1, b = key.Item2 };
            pairs.Add(t);
            byPair[key] = t;
            return t;
        }

        private static State StateOf(Pawn x, Pawn y)
        {
            int now = Find.TickManager.TicksGame;
            var key = Key(x, y);
            if (cacheTick == now && cacheA == key.Item1 && cacheB == key.Item2) return cached;
            var t = Current?.Get(x, y, false);
            var s = new State { friction = t?.friction ?? -1, soreUntil = t?.soreUntil ?? -1, warm = t?.warm ?? -1, mend = t?.mend ?? -1 };
            bool hot = Temper(x) || Temper(y);
            FromGame(x, y, now, hot, ref s);
            FromGame(y, x, now, hot, ref s);
            cacheTick = now;
            cacheA = key.Item1;
            cacheB = key.Item2;
            cached = s;
            return s;
        }

        /// <summary>The game's own signals, from p's memories of the other: an insult, slight or social fight is a friction; a deep talk or kind words are warm.</summary>
        private static void FromGame(Pawn p, Pawn other, int now, bool hot, ref State s)
        {
            var memories = p.needs?.mood?.thoughts?.memories?.Memories;
            if (memories == null) return;
            for (int i = 0; i < memories.Count; i++)
            {
                var m = memories[i];
                if (m?.def == null || m.otherPawn != other || m.age > UnmendedTicks) continue;
                int when = now - m.age;
                float soreDays;
                switch (m.def.defName)
                {
                    case "Insulted":
                    case "HadAngeringFight":
                        soreDays = 1f;
                        break;
                    case "Slighted":
                    case "HadCatharticFight":
                        soreDays = 0.5f;
                        break;
                    case "DeepTalk":
                    case "KindWords":
                    case "KindWordsMood":
                        if (when > s.warm) s.warm = when;
                        continue;
                    default:
                        continue;
                }
                if (when > s.friction) s.friction = when;
                int until = when + Mathf.RoundToInt(soreDays * GenDate.TicksPerDay * (hot ? 1.5f : 1f));
                if (until > s.soreUntil) s.soreUntil = until;
            }
        }

        private static bool Unmended(State s, int now) => s.friction >= 0 && s.mend < s.friction && now - s.friction < UnmendedTicks;

        /// <summary>A factor on a moment's weight from how things stand between the two (0 = not now).</summary>
        public static float Factor(MomentExtension x, Pawn a, Pawn b)
        {
            if (x.tone == "neutral") return 1f;
            int now = Find.TickManager.TicksGame;
            var s = StateOf(a, b);
            bool unmended = Unmended(s, now);
            bool sore = unmended && now < s.soreUntil;
            if (x.Mend)
            {
                if (!unmended || now - s.friction < MendAfterTicks) return 0f;
                float f = 1f;
                int ab = a.relations?.OpinionOf(b) ?? 0, ba = b.relations?.OpinionOf(a) ?? 0;
                if (ab >= 20 && ba >= 20) f *= 2f;
                else if (Mathf.Min(ab, ba) < 0) f *= 0.5f;
                if (PeopleTags.Has(a, "trait:Kind") || PeopleTags.Has(b, "trait:Kind")) f *= 2f;
                return f;
            }
            if (x.Friction)
            {
                if (x.stillSore && !sore) return 0f;
                int calm = Mathf.Max(s.warm, s.mend);
                if (calm >= 0 && now - calm < CalmTicks) return Temper(a) ? 0.6f : 0.2f;
                return 1f;
            }
            return sore ? 0f : 1f;
        }

        /// <summary>
        /// Whether a social event of this tone fits between the two now, by the same rules as moments: a warm one waits
        /// out a spat that is still sore, making up needs a spat they haven't made up for, and a friction right after a
        /// warm moment is put off. Neutral (or no tone) always fits.
        /// </summary>
        public static bool Allows(string tone, Pawn a, Pawn b)
        {
            if (tone.NullOrEmpty() || tone == "neutral" || a == null || b == null) return true;
            int now = Find.TickManager.TicksGame;
            var s = StateOf(a, b);
            bool unmended = Unmended(s, now);
            if (tone == "mend") return unmended && now - s.friction >= MendAfterTicks;
            if (tone == "friction")
            {
                int calm = Mathf.Max(s.warm, s.mend);
                return calm < 0 || now - calm >= CalmTicks;
            }
            return !(unmended && now < s.soreUntil);
        }

        /// <summary>A social event happened between them: remembered like a moment of the same tone.</summary>
        public static void RecordEvent(string tone, Pawn a, Pawn b, float soreHours)
        {
            var tones = Current;
            if (tones == null || tone.NullOrEmpty() || tone == "neutral" || a == null || b == null) return;
            int now = Find.TickManager.TicksGame;
            var t = tones.Get(a, b, true);
            if (tone == "friction")
            {
                t.friction = now;
                int until = now + Mathf.RoundToInt(soreHours * GenDate.TicksPerHour * (Temper(a) || Temper(b) ? 1.5f : 1f));
                if (until > t.soreUntil) t.soreUntil = until;
            }
            else if (tone == "mend")
            {
                t.mend = now;
                CouncilRecords.Add(a, CouncilRecords.QuarrelsMended);
            }
            else t.warm = now;
            cacheTick = -1;
        }

        /// <summary>A moment happened between them: remember it, and add a line about how things have been lately.</summary>
        public static void Record(MomentExtension x, Pawn a, Pawn b, List<RulePackDef> packs)
        {
            var tones = Current;
            if (tones == null || x.tone == "neutral") return;
            int now = Find.TickManager.TicksGame;
            var s = StateOf(a, b);
            var t = tones.Get(a, b, true);
            if (x.Friction)
            {
                if (!x.stillSore && Unmended(s, now)) AddPack(packs, "Council_Sent_StillTense");
                // Still sore over the last spat is not a new spat: it keeps the spat's time, so a grudge can't keep
                // itself going (two days after the last real friction, it's over either way).
                if (!x.stillSore) t.friction = now;
                bool hot = Temper(a) || Temper(b);
                int until = now + Mathf.RoundToInt(x.soreHours * GenDate.TicksPerHour * (hot ? 1.5f : 1f));
                if (until > t.soreUntil) t.soreUntil = until;
            }
            else if (x.Mend)
            {
                t.mend = now;
                CouncilRecords.Add(a, CouncilRecords.QuarrelsMended);
            }
            else
            {
                if (x.contestSkill == null && s.mend >= 0 && s.mend >= s.friction && s.warm < s.mend && now - s.mend < GenDate.TicksPerDay)
                    AddPack(packs, "Council_Sent_MadeUp");
                t.warm = now;
            }
            cacheTick = -1;
        }

        private static void AddPack(List<RulePackDef> packs, string name)
        {
            var pack = DefDatabase<RulePackDef>.GetNamedSilentFail(name);
            if (pack != null && packs != null && !packs.Contains(pack)) packs.Add(pack);
        }

        /// <summary>Once a day: forget pairs with nothing in the last few days, and pairs with someone who's no longer ours or alive.</summary>
        public void Prune()
        {
            int now = Find.TickManager.TicksGame;
            var ours = new HashSet<int>(PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive
                .Where(p => p != null && (p.IsColonist || p.IsSlaveOfColony)).Select(p => p.thingIDNumber));
            pairs.RemoveAll(p => p == null || now - p.Latest > KeepTicks || !ours.Contains(p.a) || !ours.Contains(p.b));
            byPair = null;
        }

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                pairs.RemoveAll(p => p == null || Find.TickManager.TicksGame - p.Latest > KeepTicks);
                byPair = null;
            }
            Scribe_Collections.Look(ref pairs, "pairs", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (pairs == null) pairs = new List<PairTone>();
                pairs.RemoveAll(p => p == null);
                byPair = null;
            }
        }
    }
}
