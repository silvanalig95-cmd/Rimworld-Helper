using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A colonist with an open wish brings it up in conversation now and then. The line goes into the
    /// social log, like any other vanilla interaction.
    /// </summary>
    public class InteractionWorker_TalkAboutWish : InteractionWorker
    {
        public override float RandomSelectionWeight(Pawn initiator, Pawn recipient)
        {
            if (!ColonyCouncilMod.Settings.enableWishes) return 0f;
            var comp = CouncilGameComponent.Instance;
            var wish = comp?.OpenWishBy(initiator);
            if (wish == null || wish.def.talkInteraction != interaction) return 0f;
            if (recipient == null || !recipient.RaceProps.Humanlike) return 0f;
            float w = 0.06f;
            if (wish.TicksLeft < GenDate.TicksPerDay * 2) w *= 2.5f;
            if (wish.other == recipient) w *= 2f;
            return w;
        }
    }

    /// <summary>The colonist who proposed the current ambition keeps talking it up.</summary>
    public class InteractionWorker_TalkAboutAmbition : InteractionWorker
    {
        public override float RandomSelectionWeight(Pawn initiator, Pawn recipient)
        {
            if (!ColonyCouncilMod.Settings.enableAmbitions) return 0f;
            var active = CouncilGameComponent.Instance?.ambitions?.active;
            if (active == null || recipient == null || !recipient.RaceProps.Humanlike) return 0f;
            if (initiator.MapHeld != active.map) return 0f;
            return initiator == active.proposer ? 0.05f : 0.01f;
        }
    }

    /// <summary>
    /// Talk about the council itself. Bitter colonists run it down and their doubt is catching (a little trust
    /// lost by whoever listens, when downsides are on); devoted ones talk it up. Which side a def is for comes
    /// from its defName ending in "Bitter" or "Devoted".
    /// </summary>
    public class InteractionWorker_CouncilGossip : InteractionWorker
    {
        private bool Bitter => interaction.defName.EndsWith("Bitter");

        public override float RandomSelectionWeight(Pawn initiator, Pawn recipient)
        {
            if (!ColonyCouncilMod.Settings.enableWishes) return 0f;
            var comp = CouncilGameComponent.Instance;
            if (comp == null || recipient == null || !recipient.RaceProps.Humanlike || !recipient.IsColonist) return 0f;
            var level = comp.standings.LevelOf(initiator);
            if (Bitter) return level == TrustLevel.Bitter ? 0.05f : level == TrustLevel.Doubtful ? 0.015f : 0f;
            return level == TrustLevel.Devoted ? 0.03f : level == TrustLevel.Trusting ? 0.008f : 0f;
        }

        public override void Interacted(Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks, out string letterText, out string letterLabel, out LetterDef letterDef, out LookTargets lookTargets)
        {
            base.Interacted(initiator, recipient, extraSentencePacks, out letterText, out letterLabel, out letterDef, out lookTargets);
            var comp = CouncilGameComponent.Instance;
            if (comp == null || recipient == null || !recipient.IsColonist) return;
            if (Bitter)
            {
                if (ColonyCouncilMod.Settings.Negatives && comp.standings.LevelOf(recipient) > TrustLevel.Bitter)
                    comp.standings.Change(recipient, -1f * ColonyCouncilMod.Settings.PenaltyScale, "CC_Log_Gossip".Translate(initiator.LabelShort));
            }
            else
            {
                comp.standings.Change(recipient, 0.5f, null);
            }
        }
    }

    /// <summary>
    /// Talk about the decrees in force. "Gripe" defs: two people who both dislike a decree grumble together and
    /// grow closer. "Argue" defs: someone who likes a decree defends it to someone who hates it, which can sour
    /// things (and occasionally come to blows). Which kind a def is comes from its defName ending.
    /// </summary>
    public class InteractionWorker_DecreeTalk : InteractionWorker
    {
        private bool Gripe => interaction.defName.EndsWith("Gripe");

        public override float RandomSelectionWeight(Pawn initiator, Pawn recipient)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null || comp.decrees.active.Count == 0 || recipient == null || !recipient.IsColonist || !initiator.IsColonist) return 0f;
            foreach (var a in comp.decrees.active)
            {
                var mine = a.def.ReactionOf(initiator);
                var theirs = a.def.ReactionOf(recipient);
                if (Gripe && mine <= DecreeReaction.Dislike && theirs <= DecreeReaction.Dislike)
                    return mine == DecreeReaction.Hate ? 0.08f : 0.04f;
                if (!Gripe && mine >= DecreeReaction.Like && theirs == DecreeReaction.Hate)
                    return 0.03f;
            }
            return 0f;
        }
    }

    /// <summary>"The council suggests..." in the alerts column when something is urgent.</summary>
    public class Alert_CouncilAdvice : Alert
    {
        private AdvisorSignal top;

        public Alert_CouncilAdvice()
        {
            defaultPriority = AlertPriority.Medium;
        }

        public override string GetLabel() => top == null ? "CC_Alert_Advice".Translate() : "CC_Alert_AdviceLabel".Translate(top.title);

        public override TaggedString GetExplanation()
        {
            if (top == null) return "";
            string s = top.title + "\n\n" + top.detail;
            if (!top.Tip.NullOrEmpty()) s += "\n\n" + "CC_WhyItMatters".Translate() + ": " + top.Tip;
            return s + "\n\n" + "CC_SeeCouncilTab".Translate();
        }

        public override AlertReport GetReport()
        {
            top = null;
            var s = ColonyCouncilMod.Settings;
            if (!s.enableAdvisor || !s.advisorAlert) return false;
            var map = Find.CurrentMap;
            if (map == null || !map.IsPlayerHome) return false;
            var signals = CouncilMapComponent.For(map)?.Signals;
            if (signals == null || signals.Count == 0) return false;
            var first = signals[0];
            if (first.Level < AdviceLevel.High) return false;
            top = first;
            if (first.look != null && first.look.IsValid) return AlertReport.CulpritsAre(first.look.targets);
            return true;
        }

        protected override void OnClick()
        {
            if (top?.look != null && top.look.IsValid)
            {
                base.OnClick();
                return;
            }
            Find.MainTabsRoot.SetCurrentTab(CouncilDefOf.Council_MainButton);
        }
    }

    /// <summary>A gentle reminder that colonists are waiting on wishes.</summary>
    public class Alert_CouncilWishes : Alert
    {
        private readonly List<Pawn> culprits = new List<Pawn>();

        public Alert_CouncilWishes()
        {
            defaultPriority = AlertPriority.Medium;
        }

        public override string GetLabel() => "CC_Alert_Wishes".Translate(culprits.Count);

        public override TaggedString GetExplanation()
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null) return "";
            var lines = new List<string>();
            foreach (var w in comp.OpenWishes)
                lines.Add("- " + w.pawn.LabelShort + ": " + w.Label + " (" + TextUtil.DaysString(w.TicksLeft) + ")");
            return "CC_Alert_WishesDesc".Translate() + "\n\n" + string.Join("\n", lines);
        }

        public override AlertReport GetReport()
        {
            culprits.Clear();
            var s = ColonyCouncilMod.Settings;
            if (!s.enableWishes || !s.wishAlert) return false;
            var comp = CouncilGameComponent.Instance;
            if (comp == null) return false;
            foreach (var w in comp.OpenWishes)
                if (w.pawn != null && w.pawn.Spawned) culprits.Add(w.pawn);
            if (culprits.Count == 0) return false;
            return AlertReport.CulpritsAre(culprits);
        }
    }
}
