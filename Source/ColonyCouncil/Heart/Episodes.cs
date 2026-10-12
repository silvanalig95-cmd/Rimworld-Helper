using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// "Worn out: needs a day." Started on purpose by the heart layer, never by the game's break picker (no MentalBreakDef
    /// names it): the colonist keeps to their room for six to twelve hours, eating, sleeping and resting as they need.
    /// It ends early if a threat appears. When it's over, strain drops by about 40 and they feel lighter for a while.
    /// Built on vanilla's hide-in-room state, so its room and wandering work as they do there. The only trace it leaves
    /// in a save is a colonist in the middle of one.
    /// </summary>
    public class MentalState_HeartWornOut : MentalState_WanderOwnRoom
    {
        /// <summary>Ended because danger came: less relief, no letting it out.</summary>
        public bool interrupted;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref interrupted, "interrupted");
        }

        public override void MentalStateTick(int delta)
        {
            base.MentalStateTick(delta);
            if (pawn == null || pawn.Dead || pawn.MentalState != this) return;
            // A threat ends it: nobody sits in their room while the colony fights.
            if (pawn.IsHashIntervalTick(250, delta) && pawn.Spawned && GenHostility.AnyHostileActiveThreatToPlayer(pawn.Map, false, false))
            {
                interrupted = true;
                RecoverFromState();
            }
        }

        public override void PostEnd()
        {
            base.PostEnd();
            try
            {
                Episodes.Ended(pawn, interrupted);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("heart episode end", e);
            }
        }

        public override RandomSocialMode SocialModeMax() => RandomSocialMode.Off;
    }

    public static class Episodes
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        private static MentalStateDef def;
        private static ThoughtDef letItOut;
        private static bool looked;

        /// <summary>The episode's mental state (Council_Heart_WornOut), looked up quietly.</summary>
        public static MentalStateDef Def
        {
            get
            {
                Look();
                return def;
            }
        }

        private static void Look()
        {
            if (looked) return;
            looked = true;
            def = DefDatabase<MentalStateDef>.GetNamedSilentFail("Council_Heart_WornOut");
            letItOut = DefDatabase<ThoughtDef>.GetNamedSilentFail("Council_Heart_LetItOut");
        }

        public const int ColonyCooldown = GenDate.TicksPerDay * 2;
        public const int PawnCooldown = GenDate.TicksPerDay * 10;

        /// <summary>Why an episode can't start for this colonist now, or null if it could.</summary>
        public static string Blocked(Pawn p, PawnHeart h, HeartMapContext m, int now)
        {
            if (!S.heartEpisodes) return "off";
            if (Def == null) return "no def";
            if (h.strain < HeartMath.EpisodeAt) return "strain";
            if (!p.Spawned || p.Map == null || !p.Map.IsPlayerHome) return "away";
            if (!p.IsFreeNonSlaveColonist || p.IsQuestLodger() || p.IsPrisoner || p.guest != null && p.guest.IsPrisoner) return "not free";
            if (!p.DevelopmentalStage.Adult()) return "child";
            if (p.Drafted || p.Downed || p.InMentalState) return "busy";
            if (p.mindState?.mentalStateHandler == null || p.mindState.mentalStateHandler.MentalBreaksBlocked()) return "blocked";
            if (m.threat || GenHostility.AnyHostileActiveThreatToPlayer(p.Map, false, false)) return "danger";
            if (now - h.lastEpisodeTick < PawnCooldown) return "pawn cooldown";
            var tracker = HeartTracker.Instance;
            if (tracker == null || now - tracker.lastColonyEpisodeTick < ColonyCooldown) return "colony cooldown";
            int able = 0;
            foreach (var o in p.Map.mapPawns.FreeColonistsSpawned)
            {
                if (o == p || o.Downed || o.InMentalState || o.IsQuestLodger() || !o.DevelopmentalStage.Adult()) continue;
                if (o.workSettings == null || !o.workSettings.EverWork) continue;
                able++;
            }
            if (able < 2) return "too few";
            return null;
        }

        public static void MaybeStart(Pawn p, PawnHeart h, HeartMapContext m, int now)
        {
            if (h.strain < HeartMath.EpisodeAt || !S.heartEpisodes) return;
            float chance = HeartMath.EpisodeChancePerHour(h.strain) * Heart.EpisodeFactor(h);
            if (!Rand.Chance(chance)) return;
            if (Blocked(p, h, m, now) != null) return;
            Start(p, h, now);
        }

        /// <summary>Starts an episode (also from dev mode). Returns whether it began.</summary>
        public static bool Start(Pawn p, PawnHeart h, int now)
        {
            if (Def == null || p?.mindState?.mentalStateHandler == null) return false;
            string sources = HeartView.SourcesText(h, 3);
            string reason = "CC_Heart_Episode_Reason".Translate().Resolve();
            // Started silently: the heart writes its own letter, the game's own begin letter never comes.
            if (!p.mindState.mentalStateHandler.TryStartMentalState(Def, reason, false, false, false, null, true)) return false;
            h.lastEpisodeTick = now;
            var tracker = HeartTracker.Instance;
            if (tracker != null) tracker.lastColonyEpisodeTick = now;
            string title = "CC_Heart_Episode_Title".Translate(p.Named("PAWN")).Resolve();
            string text = "CC_Heart_Episode_Text".Translate(p.Named("PAWN"), (sources.NullOrEmpty() ? "CC_Heart_Episode_NoSources".Translate().Resolve() : sources).Named("SOURCES")).Resolve();
            string help = HeartView.HelpText(p, h, 3);
            if (!help.NullOrEmpty()) text += "\n\n" + "CC_Heart_Episode_Helps".Translate(p.Named("PAWN"), help.Named("HELP")).Resolve();
            Find.LetterStack.ReceiveLetter(title, text, LetterDefOf.NegativeEvent, p);
            var comp = CouncilGameComponent.Instance;
            comp?.AddChronicle(new ChronicleEntry(ChronicleKind.Life, "CC_Heart_Episode_Chronicle".Translate(p.Named("PAWN")).Resolve(), p));
            comp?.journals?.WriteText(p, "CC_Heart_Episode_Journal".Translate(p.Named("PAWN")).Resolve());
            return true;
        }

        /// <summary>The episode is over: strain drops, and if it ran its course they feel lighter.</summary>
        public static void Ended(Pawn p, bool interrupted)
        {
            if (p == null || p.Dead || !Heart.On) return;
            Look();
            LogicChange(p, interrupted ? -HeartMath.EpisodeRelief * 0.5f : -HeartMath.EpisodeRelief);
            if (interrupted)
            {
                Messages.Message("CC_Heart_Episode_Interrupted".Translate(p.Named("PAWN")).Resolve(), p, MessageTypeDefOf.NeutralEvent, false);
                return;
            }
            if (letItOut != null && p.needs?.mood?.thoughts?.memories != null) p.needs.mood.thoughts.memories.TryGainMemory(letItOut);
            Messages.Message("CC_Heart_Episode_Over".Translate(p.Named("PAWN")).Resolve(), p, MessageTypeDefOf.PositiveEvent, false);
        }

        private static void LogicChange(Pawn p, float amount) => HeartLogic.Change(p, amount, StrainRelief.Episode);

        public static void ResetCaches()
        {
            looked = false;
            def = null;
            letItOut = null;
        }
    }
}
