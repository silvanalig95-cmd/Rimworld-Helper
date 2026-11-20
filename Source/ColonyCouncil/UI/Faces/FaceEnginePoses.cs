using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// The face engine's poses (Portraits, phase 2): what the game says of a person (dead, a prisoner, a break, grief, a
    /// low mood, a fresh quarrel, a seat, a title or the head of a people, a shy or proud trait, old age) read into a pose
    /// by <see cref="FacePoseMath.Bearing"/>, looked at again every two seconds and kept steady: a pose from a state of
    /// the moment shows at once, any other only after the one held has lasted an hour of the game.
    /// </summary>
    public static partial class FaceEngine
    {
        private struct Held
        {
            public FaceBearing bearing;
            public bool fromState;
            public int since;
            public float nextLook;
        }

        private static readonly Dictionary<int, Held> bearings = new Dictionary<int, Held>();

        /// <summary>How often a person's pose is looked at again, in seconds.</summary>
        private const float LookEvery = 2f;

        /// <summary>
        /// The pose a portrait draws them in: <paramref name="asked"/> is the way the place wants them to face (two facing
        /// each other), else the front or, for the shy, half away. With <paramref name="posed"/> off (a small portrait) only
        /// the way they face and the grey of the dead; with poses switched off, the front or the way asked.
        /// </summary>
        public static FacePose PoseOf(Pawn p, FaceTurn asked, bool posed)
        {
            if (p == null) return new FacePose(asked, FaceBearing.Plain);
            var b = Poses ? BearingOf(p) : FaceBearing.Plain;
            if (!posed && b != FaceBearing.Dead) b = FaceBearing.Plain;
            return new FacePose(FacePoseMath.TurnFor(b, asked, p.thingIDNumber), b);
        }

        /// <summary>Whether a dead person is pictured, grey, in a letter: with poses on, while their body isn't bones.</summary>
        public static bool PicturesDead(Pawn p)
        {
            if (p == null || !p.Dead || p.Discarded || !Poses) return false;
            try
            {
                return p.Drawer?.renderer != null && p.Drawer.renderer.CurRotDrawMode != RotDrawMode.Dessicated;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Their pose now, kept steady.</summary>
        private static FaceBearing BearingOf(Pawn p)
        {
            NewGameCheck();
            float now = Time.realtimeSinceStartup;
            bool had = bearings.TryGetValue(p.thingIDNumber, out var h);
            if (had && now < h.nextLook) return h.bearing;
            int tick = Find.TickManager?.TicksGame ?? 0;
            try
            {
                var facts = Facts(p);
                var next = FacePoseMath.Bearing(facts, had ? h.bearing : FaceBearing.Plain);
                bool fromState = FacePoseMath.FromState(facts, next);
                if (!had || (next != h.bearing && FacePoseMath.TakeNow(fromState, h.fromState, tick - h.since)))
                {
                    h.bearing = next;
                    h.fromState = fromState;
                    h.since = tick;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("portraits: a person's pose", e);
            }
            h.nextLook = now + LookEvery;
            bearings[p.thingIDNumber] = h;
            return h.bearing;
        }

        /// <summary>What the game says of them, for their pose.</summary>
        private static BearingFacts Facts(Pawn p)
        {
            var f = new BearingFacts { mood = -1f, minorBreak = 0.35f };
            f.dead = p.Dead;
            if (f.dead) return f;
            f.captive = p.IsPrisoner;
            var state = p.MentalState?.def;
            if (state != null)
            {
                f.angryState = state.category == MentalStateCategory.Aggro || state.category == MentalStateCategory.Malicious || state == SocialFighting;
                f.sadState = SadStates.Contains(state);
            }
            var mood = p.needs?.mood;
            if (mood != null)
            {
                f.mood = mood.CurLevelPercentage;
                f.minorBreak = p.mindState?.mentalBreaker?.BreakThresholdMinor ?? 0.35f;
            }
            var memories = mood?.thoughts?.memories?.Memories;
            if (memories != null)
            {
                for (int i = 0; i < memories.Count; i++)
                {
                    var m = memories[i];
                    if (m?.def == null) continue;
                    if (Grief.Contains(m.def)) f.grieving = true;
                    else if (Quarrels.Contains(m.def) && m.age < FacePoseMath.QuarrelTicks) f.quarrel = true;
                }
            }
            f.proudStanding = p.Faction?.leader == p || OfficeTracker.HeldBy(p) != null || p.royalty?.MostSeniorTitle != null;
            var traits = p.story?.traits?.allTraits;
            if (traits != null)
            {
                for (int i = 0; i < traits.Count; i++)
                {
                    var t = traits[i];
                    if (t?.def == null) continue;
                    if (ShyTraits.Contains(t.def) || (t.def == Nerves && t.Degree == -1)) f.shyTrait = true;
                    if (ProudTraits.Contains(t.def) || (t.def == Nerves && t.Degree == 2) || (t.def == Beauty && t.Degree == 2)) f.proudTrait = true;
                }
            }
            if (p.ageTracker != null && p.RaceProps != null) f.old = FacePoseMath.Old(p.ageTracker.AgeBiologicalYearsFloat, p.RaceProps.lifeExpectancy);
            return f;
        }

        // The game's and the player's mods' defs the poses read, found by name once (those a list doesn't have are left out).
        private static HashSet<ThoughtDef> grief, quarrels;
        private static HashSet<TraitDef> shyTraits, proudTraits;
        private static HashSet<MentalStateDef> sadStates;
        private static MentalStateDef socialFighting;
        private static TraitDef nerves, beauty;
        private static bool named;

        private static HashSet<ThoughtDef> Grief
        {
            get
            {
                Name();
                return grief;
            }
        }

        private static HashSet<ThoughtDef> Quarrels
        {
            get
            {
                Name();
                return quarrels;
            }
        }

        private static HashSet<TraitDef> ShyTraits
        {
            get
            {
                Name();
                return shyTraits;
            }
        }

        private static HashSet<TraitDef> ProudTraits
        {
            get
            {
                Name();
                return proudTraits;
            }
        }

        private static HashSet<MentalStateDef> SadStates
        {
            get
            {
                Name();
                return sadStates;
            }
        }

        private static MentalStateDef SocialFighting
        {
            get
            {
                Name();
                return socialFighting;
            }
        }

        private static TraitDef Nerves
        {
            get
            {
                Name();
                return nerves;
            }
        }

        private static TraitDef Beauty
        {
            get
            {
                Name();
                return beauty;
            }
        }

        private static void Name()
        {
            if (named) return;
            named = true;
            // Grief: the game's own thought for each family tie's death (its relations), and a friend's, a bonded animal's
            // and a death in the family seen.
            grief = new HashSet<ThoughtDef>();
            foreach (var r in DefDatabase<PawnRelationDef>.AllDefsListForReading)
            {
                if (r.diedThought != null) grief.Add(r.diedThought);
                if (r.diedThoughtFemale != null) grief.Add(r.diedThoughtFemale);
            }
            foreach (var n in new[] { "PawnWithGoodOpinionDied", "BondedAnimalDied", "WitnessedDeathFamily" })
            {
                var d = DefDatabase<ThoughtDef>.GetNamedSilentFail(n);
                if (d != null) grief.Add(d);
            }
            quarrels = new HashSet<ThoughtDef>();
            foreach (var n in new[] { "HadAngeringFight", "Insulted" })
            {
                var d = DefDatabase<ThoughtDef>.GetNamedSilentFail(n);
                if (d != null) quarrels.Add(d);
            }
            sadStates = new HashSet<MentalStateDef>();
            foreach (var n in new[] { "Wander_Sad", "Wander_OwnRoom", "GiveUpExit" })
            {
                var d = DefDatabase<MentalStateDef>.GetNamedSilentFail(n);
                if (d != null) sadStates.Add(d);
            }
            socialFighting = DefDatabase<MentalStateDef>.GetNamedSilentFail("SocialFighting");
            // Shy: Biotech's recluse and Vanilla Traits Expanded's anxious, coward, submissive and schizoid (the game's
            // nervous, a degree of Nerves, too). Proud: Vanilla Traits Expanded's snob and brave, the game's jealous (the
            // iron-willed and the beautiful, degrees of Nerves and Beauty, too).
            shyTraits = new HashSet<TraitDef>();
            foreach (var n in new[] { "Recluse", "VTE_Anxious", "VTE_Coward", "VTE_Submissive", "VTE_Schizoid" })
            {
                var d = DefDatabase<TraitDef>.GetNamedSilentFail(n);
                if (d != null) shyTraits.Add(d);
            }
            proudTraits = new HashSet<TraitDef>();
            foreach (var n in new[] { "VTE_Snob", "VTE_Brave", "Jealous" })
            {
                var d = DefDatabase<TraitDef>.GetNamedSilentFail(n);
                if (d != null) proudTraits.Add(d);
            }
            nerves = DefDatabase<TraitDef>.GetNamedSilentFail("Nerves");
            beauty = DefDatabase<TraitDef>.GetNamedSilentFail("Beauty");
        }
    }
}
