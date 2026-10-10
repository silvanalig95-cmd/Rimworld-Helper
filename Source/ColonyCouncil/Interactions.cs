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
