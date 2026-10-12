using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A group arrives to make camp on the colony's map (the storyteller's event, Council_Evt_Camp). The kind of camp,
    /// its people and its spot are chosen when it fires; they walk in at once, and the letter asks what the colony
    /// makes of it. Answers: let them stay, set terms, ask them to move on (with a gift or without), drive them off.
    /// </summary>
    public class HappeningWorker_Camp : IncidentWorker_Happening
    {
        private CampSetup setup;

        protected override bool CanHappen(Map map, HappeningExtension ext) => CampTracker.Instance?.CanStartOn(map) ?? false;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var tracker = CampTracker.Instance;
            if (tracker == null) return false;
            setup = tracker.Prepare(ctx.map);
            if (setup == null) return false;
            ctx.a = Colonists(ctx.map).Where(p => !p.DevelopmentalStage.Juvenile())
                .RandomElementByWeightWithFallback(p => 1f + (p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0) * 0.2f);
            if (ctx.a == null) return false;
            ctx.faction = setup.faction;
            ctx.stranger = setup.pawns[0];
            ctx.group = setup.pawns.Skip(1).ToList();
            ctx.note = setup.kind.defName;
            ctx.placeName = setup.place;
            ctx.spot = setup.center;
            ctx.count = CampAnswers.Round10((40f + 15f * setup.pawns.Count) * LivingEvents.WealthFactor(ctx.map));
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) => CampTracker.Instance?.Start(setup) != null;

        protected override void Abort(HappeningContext ctx)
        {
            CampTracker.Discard(setup);
            setup = null;
            ctx.stranger = null;
            ctx.group.Clear();
        }

        protected override void Send(HappeningContext ctx, HappeningExtension ext)
        {
            var camp = CampTracker.Instance?.camp;
            var kind = camp?.kind;
            if (camp == null || kind == null || ext.options.NullOrEmpty())
            {
                base.Send(ctx, ext);
                return;
            }
            string label = ctx.Format(Pick(kind.titles, ctx) ?? def.label).CapitalizeFirst();
            ctx.text = ctx.Format(Pick(kind.texts, ctx));
            // The colonist who met them first.
            camp.Warm(ctx.a, 1);
            ctx.why = label;
            ctx.cause = label;
            string body = ctx.text + "\n\n" + Facts(camp);
            int ticks = Mathf.RoundToInt(Mathf.Max(0.1f, ext.answerDays) * GenDate.TicksPerDay);
            body += "\n\n" + "CC_Evt_Deadline".Translate(TextUtil.DaysString(ticks), ctx.Format(ext.options[Mathf.Clamp(ext.defaultOption, 0, ext.options.Count - 1)].label)).Resolve();
            var letter = (ChoiceLetter_Happening)LetterMaker.MakeLetter(label, body, CouncilDefOf.Council_EventLetter, Look(ctx));
            letter.ctx = ctx;
            letter.StartTimeout(ticks);
            Find.LetterStack.ReceiveLetter(letter);
            setup = null;
        }

        /// <summary>How many, how long, what they'll take from the land, who trades; and where to talk to them later.</summary>
        private static string Facts(Camp c)
        {
            var parts = new List<string> { c.Text("CC_Camp_Facts", null, null, c.members.Count) };
            var k = c.kind;
            if (k.huntsPerDay > 0f && k.chopsPerDay > 0f) parts.Add(c.Text("CC_Camp_Facts_Both"));
            else if (k.chopsPerDay > 0f) parts.Add(c.Text("CC_Camp_Facts_Wood"));
            else if (k.huntsPerDay > 0f) parts.Add(c.Text("CC_Camp_Facts_Game"));
            if (c.trader != null) parts.Add(c.Text("CC_Camp_Facts_Trader", c.trader));
            parts.Add(c.Text("CC_Camp_Facts_Talk"));
            return string.Join(" ", parts);
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            var camp = CampTracker.Instance?.camp;
            if (camp == null || !camp.Active) return outcome;
            switch (option)
            {
                case 1:
                    if (!byDefault) CampAnswers.Talk(camp, true);
                    break;
                case 2:
                    CampAnswers.AskToLeave(camp, ctx.count);
                    break;
                case 3:
                    CampAnswers.AskToLeave(camp, 0);
                    // A refusal comes as its own letter; the outcome here is only for when they go.
                    if (!camp.asked) return null;
                    break;
                case 4:
                    return CampAnswers.DriveOff(camp) ?? outcome;
            }
            return outcome;
        }
    }
}
