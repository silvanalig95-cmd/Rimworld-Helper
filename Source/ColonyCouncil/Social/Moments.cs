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

        [Unsaved] private bool? available;

        /// <summary>Its mods and defs are in this game.</summary>
        public bool Available
        {
            get
            {
                if (available == null)
                {
                    bool ok = mods.NullOrEmpty() || mods.Any(id => ModLister.GetActiveModWithIdentifier(id, true) != null);
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

        public override float RandomSelectionWeight(Pawn initiator, Pawn recipient)
        {
            try
            {
                var ext = Ext;
                if (ext == null || !ColonyCouncilMod.Settings.colonistMoments || !ext.Available) return 0f;
                if (initiator == null || recipient == null || !recipient.RaceProps.Humanlike || !initiator.RaceProps.Humanlike) return 0f;
                if (!Ours(initiator) || !Ours(recipient) || !People.HasInnerLife(initiator) || !People.HasInnerLife(recipient)) return 0f;
                int now = Find.TickManager.TicksGame;
                if (lastAny.TryGetValue(initiator.thingIDNumber, out int any) && now - any < MinGapTicks) return 0f;
                if (lastStarted.TryGetValue((initiator.thingIDNumber, interaction.shortHash), out int last) && now - last < ext.cooldownDays * GenDate.TicksPerDay) return 0f;
                if (!Fits(ext, initiator, recipient)) return 0f;
                return ext.weight;
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("moment weight " + interaction?.defName, e);
                return 0f;
            }
        }

        private static bool Ours(Pawn p) => p.IsColonist || p.IsSlaveOfColony;

        private static bool Fits(MomentExtension x, Pawn a, Pawn b)
        {
            int opinion = a.relations?.OpinionOf(b) ?? 0;
            if (opinion < x.minOpinion || opinion > x.maxOpinion) return false;
            if (x.mutualFriends && (opinion < 20 || (b.relations?.OpinionOf(a) ?? 0) < 20)) return false;
            var social = CouncilGameComponent.Instance?.social;
            if (!x.tie.NullOrEmpty() && System.Enum.TryParse(x.tie, out TieKind kind) && social?.Get(a, b, kind) == null) return false;
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
                var def = DefDatabase<NeedDef>.GetNamedSilentFail(x.need);
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
            if (!x.initiatorTags.NullOrEmpty() && Flavor.Match("[" + x.initiatorTags + "]", TagsOf(a)) < 0) return false;
            if (!x.recipientTags.NullOrEmpty() && Flavor.Match("[" + x.recipientTags + "]", TagsOf(b)) < 0) return false;
            return true;
        }

        private static Thought_Memory Remembered(Pawn p, List<string> names, float days)
        {
            var memories = p.needs?.mood?.thoughts?.memories?.Memories;
            if (memories == null) return null;
            int max = Mathf.RoundToInt(days * GenDate.TicksPerDay);
            foreach (var m in memories)
            {
                if (m?.def == null || m.age > max) continue;
                foreach (var n in names)
                {
                    if (n.EndsWith("*") ? m.def.defName.StartsWith(n.Substring(0, n.Length - 1)) : m.def.defName == n) return m;
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
}
