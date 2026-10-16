using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Which setting a setback answers to.</summary>
    public enum SetbackGroup
    {
        /// <summary>The council's doing (a wish passed over, an idea turned down): the stakes' downsides, like the council's own.</summary>
        Council,
        /// <summary>Things going wrong in the colony's life (work wrecked, a crop lost, a worry): "Life's ups and downs".</summary>
        Life,
        /// <summary>Small irritations between people (outshone, a noisy neighbour): the everyday frictions setting.</summary>
        Friction
    }

    /// <summary>On a setback's ThoughtDef: which setting it answers to, and whether the gentle setting leaves it out.</summary>
    public class SetbackExtension : DefModExtension
    {
        public SetbackGroup group = SetbackGroup.Life;
        /// <summary>One of the fussier ones: the gentle "Life's ups and downs" leaves it out (as half-built work).</summary>
        public bool notGentle;
        /// <summary>Envy: also needs the jealousy setting.</summary>
        public bool envy;
    }

    /// <summary>
    /// Setbacks: the bad moods that come from what actually happens in play, so the good ones have something to balance
    /// against. A wish that keeps being passed over, an idea the council turned down, a disliked decree that drags on,
    /// work torn down or wrecked, a crop lost to blight or frost, a botched job in a colonist's own passion, a feast that
    /// ran out of food, a party rained on, a friend gone quiet, a sick friend, a loved one's caravan in trouble. Each
    /// says what caused it and (in the Mood tab) what helps, most are small (-1 to -4), and none comes from nowhere.
    /// Council setbacks follow the stakes' downsides; the colony's life follows "Life's ups and downs" (and the
    /// colonist's standards, never less than a whole point); irritations between people follow the frictions setting.
    /// Events are hooked where they already happen (Setbacks/SetbackHooks.cs, a few calls from the council's own code);
    /// what lasts is worked out once an hour by <see cref="SetbackTracker"/>.
    /// </summary>
    public static partial class Setbacks
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public static SetbackTracker Tracker => SetbackTracker.Instance;

        /// <summary>Who feels these: the colony's free people with an inner life (as for the everyday pressures).</summary>
        public static bool Eligible(Pawn p) => Pressures.Eligible(p);

        public static SetbackExtension Ext(ThoughtDef def) => def?.GetModExtension<SetbackExtension>();

        /// <summary>Whether a group's setbacks are on at all.</summary>
        public static bool On(SetbackGroup group)
        {
            var s = S;
            if (s == null || !s.MoodEffects) return false;
            switch (group)
            {
                case SetbackGroup.Council: return s.Negatives;
                case SetbackGroup.Friction: return s.frictions;
                default: return true;
            }
        }

        /// <summary>Whether this setback is on: its group, the gentle setting for the fussy ones, the jealousy setting for envy.</summary>
        public static bool On(ThoughtDef def)
        {
            var ext = Ext(def);
            if (ext == null) return On(SetbackGroup.Life);
            if (!On(ext.group)) return false;
            if (ext.notGentle && Pressures.Gentle) return false;
            if (ext.envy && !(S?.jealousy ?? true)) return false;
            return true;
        }

        /// <summary>The factor a bad setback memory is given with (see <see cref="SetbackGroup"/>); scaled: it's final.</summary>
        private static float Factor(Pawn p, ThoughtDef def, float baseOffset, out bool scaled)
        {
            var group = Ext(def)?.group ?? SetbackGroup.Life;
            switch (group)
            {
                case SetbackGroup.Council:
                    scaled = true;
                    return S.PenaltyScale * Standards.MoodFactor(p, false);
                case SetbackGroup.Friction:
                    // As the moments' frictions: felt with the colonist's standards (off on gentle life pressures).
                    scaled = false;
                    return 1f;
                default:
                    scaled = true;
                    return SetbackMath.AtLeastOnePoint(baseOffset, Pressures.LifeFactor * Pressures.Contrast(p));
            }
        }

        /// <summary>
        /// Gives a setback's memory: at a stage (or its first), about someone (a social memory needs them), with what
        /// caused it for the tooltip. Nothing when the setback is off or the colonist doesn't feel these.
        /// </summary>
        public static bool Give(Pawn p, ThoughtDef def, Pawn other = null, string cause = null, int stage = -1)
        {
            try
            {
                if (def?.stages == null || def.stages.Count == 0 || p?.needs?.mood?.thoughts?.memories == null || !Eligible(p) || !On(def)) return false;
                if (def.IsSocial && (other == null || other == p)) return false;
                if (stage >= def.stages.Count) stage = def.stages.Count - 1;
                var t = stage >= 0 ? ThoughtMaker.MakeThought(def, stage) : ThoughtMaker.MakeThought(def) as Thought_Memory;
                if (t == null) return false;
                float baseOffset = def.stages[Math.Max(0, stage)]?.baseMoodEffect ?? 0f;
                bool scaled = false;
                float factor = 1f;
                if (baseOffset < 0f)
                {
                    factor = Factor(p, def, baseOffset, out scaled);
                    if (factor <= 0f) return false;
                }
                t.moodPowerFactor = factor;
                if (t is ICouncilMood mood) mood.Scaled = scaled;
                if (t is ICausedThought caused) caused.Cause = cause ?? Causes.Current;
                var memories = p.needs.mood.thoughts.memories;
                // A worse stage of the same setback replaces a milder one still on their mind (the game would only renew it).
                if (stage > 0)
                {
                    var had = memories.Memories.FirstOrDefault(m => m.def == def && m.otherPawn == (other == p ? null : other) && m.CurStageIndex < stage);
                    if (had != null) memories.RemoveMemory(had);
                }
                memories.TryGainMemory(t, other == p ? null : other);
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("setback " + def?.defName, e);
                return false;
            }
        }

        /// <summary>A lasting setback's bad side as it's felt (see <see cref="SetbackGroup"/>).</summary>
        public static float Felt(Pawn p, ThoughtDef def, float m)
        {
            if (m >= 0f) return m;
            var group = Ext(def)?.group ?? SetbackGroup.Life;
            switch (group)
            {
                case SetbackGroup.Council: return m * S.PenaltyScale * Standards.MoodFactor(p, false);
                case SetbackGroup.Friction: return m * Pressures.Contrast(p);
                default: return Pressures.Bad(p, m);
            }
        }

        // ---------------- Who is close to whom ----------------

        /// <summary>How close two colonists are: 0 not especially, 1 a close friend, 2 a partner or family by blood.</summary>
        public static int Closeness(Pawn p, Pawn o)
        {
            if (p == null || o == null || p == o || p.relations == null) return 0;
            try
            {
                if (LovePartnerRelationUtility.LovePartnerRelationExists(p, o)) return 2;
                foreach (var f in p.relations.FamilyByBlood)
                    if (f == o) return 2;
            }
            catch
            {
                // Relations of a pawn half torn down (just died, leaving the world): not close.
            }
            if (Pressures.SparedMissingFriends(p)) return 0;
            var web = S.socialWeb ? CouncilGameComponent.Instance?.social : null;
            if (web != null)
            {
                var t = web.Get(p, o, TieKind.Friends);
                if (t != null && t.announced && t.strength >= 40f) return 1;
            }
            else if (o.relations != null && p.relations.OpinionOf(o) >= 60 && o.relations.OpinionOf(p) >= 60) return 1;
            return 0;
        }

        /// <summary>A rival: an announced rivalry, or someone they can't stand (opinion -25 or lower).</summary>
        public static bool Rival(Pawn p, Pawn o)
        {
            if (p?.relations == null || o == null || p == o) return false;
            var web = S.socialWeb ? CouncilGameComponent.Instance?.social : null;
            var t = web?.Get(p, o, TieKind.Rivals);
            if (t != null && t.announced) return true;
            return p.relations.OpinionOf(o) <= -25;
        }

        /// <summary>The colony's people on any map or in caravans who feel setbacks.</summary>
        public static List<Pawn> Everyone()
        {
            var list = new List<Pawn>();
            var all = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists_NoCryptosleep;
            for (int i = 0; i < all.Count; i++)
                if (Eligible(all[i])) list.Add(all[i]);
            return list;
        }

        /// <summary>Has a trait by name (a mod's trait that isn't there is never matched).</summary>
        public static bool HasTrait(Pawn p, string defName)
        {
            var all = p?.story?.traits?.allTraits;
            if (all == null) return false;
            for (int i = 0; i < all.Count; i++)
                if (all[i].def.defName == defName) return true;
            return false;
        }

        /// <summary>A trait's degree by name (0 when they don't have it, or the game doesn't).</summary>
        public static int TraitDegree(Pawn p, string defName)
        {
            var all = p?.story?.traits?.allTraits;
            if (all == null) return 0;
            for (int i = 0; i < all.Count; i++)
                if (all[i].def.defName == defName) return all[i].Degree;
            return 0;
        }

        public static string Days(int ticks) => TextUtil.DaysString(ticks);

        /// <summary>A short speech line from a pool, said over their head (only when speech is on and they're in view).</summary>
        public static void Say(Pawn p, string pool, params NamedArgument[] args)
        {
            if (p == null || !p.Spawned) return;
            try
            {
                string line = Flavor.FromPool(pool, p);
                if (line.NullOrEmpty()) return;
                var all = new List<NamedArgument> { p.Named("PAWN") };
                if (args != null) all.AddRange(args);
                CouncilGameComponent.Speak(p, line.Formatted(all.ToArray()).Resolve(), new UnityEngine.Color(1f, 0.7f, 0.6f));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("setback line " + pool, e);
            }
        }

        /// <summary>A journal entry (sometimes): the bigger setbacks are written down.</summary>
        public static void Write(Pawn p, string pool, float chance, params NamedArgument[] args)
        {
            if (p == null || !Rand.Chance(chance)) return;
            CouncilGameComponent.Instance?.journals.Write(p, pool, null, args);
        }

        /// <summary>Somewhere the game knows: on a map, or in a caravan.</summary>
        public static bool Somewhere(Pawn p) => p.MapHeld != null || CaravanUtility.GetCaravan(p) != null;
    }
}
