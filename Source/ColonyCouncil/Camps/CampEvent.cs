using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
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
            // Their people remember one of the colony's by name (who set theirs free, who tended them): they ask for them.
            ctx.a = KinMemory.Greeter(setup.faction, ctx.map) ?? Colonists(ctx.map).Where(p => !p.DevelopmentalStage.Juvenile())
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

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) => (started = CampTracker.Instance?.Start(setup)) != null;

        /// <summary>The camp this event brought, while its letter is written (with camps side by side, never just "the camp here").</summary>
        private Camp started;

        /// <summary>The camp this event's letter is about: the one it brought, else the one its stranger or party belongs to, else one of its kind here.</summary>
        private static Camp CampOfEvent(HappeningContext ctx)
        {
            var c = CampTracker.CampOf(ctx?.stranger);
            if (c != null) return c;
            foreach (var p in ctx?.group ?? new List<Pawn>())
                if ((c = CampTracker.CampOf(p)) != null) return c;
            return CampTracker.VisitingAt(ctx?.map, x => x.kind?.defName == ctx?.note);
        }

        /// <summary>The camp's own tags for its letter: faith, the world event behind it.</summary>
        protected override void AddTags(HappeningContext ctx, HashSet<string> tags) =>
            CampWords.AddTags(tags, setup?.kind ?? DefDatabase<CampKindDef>.GetNamedSilentFail(ctx.note ?? "") ?? CampOfEvent(ctx)?.kind, ctx.faction, ctx.map);

        protected override void Abort(HappeningContext ctx)
        {
            CampTracker.Discard(setup);
            setup = null;
            ctx.stranger = null;
            ctx.group.Clear();
        }

        protected override void Send(HappeningContext ctx, HappeningExtension ext)
        {
            var camp = started ?? CampOfEvent(ctx);
            started = null;
            var kind = camp?.kind;
            if (camp == null || kind == null || ext.options.NullOrEmpty())
            {
                base.Send(ctx, ext);
                return;
            }
            string label = ctx.Format(CampWords.Fill(Pick(kind.titles, ctx), kind, camp.faction) ?? def.label).CapitalizeFirst();
            ctx.text = ctx.Format(CampWords.Fill(Pick(kind.texts, ctx), kind, camp.faction));
            // The same people back again (CampMemory.cs): told so, with news of what became of them.
            if (camp.Familiar)
            {
                var past = CampTracker.Instance?.past.FirstOrDefault(x => x.id == camp.returnOf);
                label = camp.Text(camp.returns >= 2 ? "CC_Camp_ReturnTitle_Again" : "CC_Camp_ReturnTitle");
                ctx.text = camp.Text(past != null && past.invited ? "CC_Camp_Return_Invited" : "CC_Camp_Return") + " " + CampReturns.News(camp, past);
            }
            // What brought them, read from the land near the home (CampSeekers.cs): in their words, and looked at.
            string seek = camp.SeekLine(ctx.stranger, ctx.a);
            if (!seek.NullOrEmpty()) ctx.text += " " + seek;
            // The colonist who met them first.
            camp.Warm(ctx.a, 1);
            CouncilRecords.Add(ctx.a, CouncilRecords.CampsGreeted);
            ctx.why = label;
            ctx.cause = label;
            string body = ctx.text + "\n\n" + Facts(camp);
            int ticks = Mathf.RoundToInt(Mathf.Max(0.1f, ext.answerDays) * GenDate.TicksPerDay);
            body += "\n\n" + "CC_Evt_Deadline".Translate(TextUtil.DaysString(ticks), ctx.Format(ext.options[Mathf.Clamp(ext.defaultOption, 0, ext.options.Count - 1)].label)).Resolve();
            var look = Look(ctx);
            var at = camp.SeekTarget;
            if (at.IsValid) look = new LookTargets((look?.targets ?? new List<GlobalTargetInfo>()).Concat(new[] { at }).ToList());
            var letter = (ChoiceLetter_Happening)LetterMaker.MakeLetter(label, body, CouncilDefOf.Council_EventLetter, look);
            letter.ctx = ctx;
            letter.StartTimeout(ticks);
            Tell.Ask(letter, ctx.map);
            setup = null;
        }

        /// <summary>How many, how long, what they'll take from the land, who trades; and where to talk to them later.</summary>
        private static string Facts(Camp c)
        {
            var parts = new List<string> { c.Text("CC_Camp_Facts", null, null, c.members.Count) };
            // An old camp's site they mean to move into (CampGround.cs): what still stands there for them, and whose it was.
            string ground = CampGrounds.FactsLine(c);
            if (!ground.NullOrEmpty()) parts.Add(ground);
            // Dressed and pitched for the weather of their stay (CampWeather.cs): what they wear and build, as it is.
            var w = c.pitch;
            if (w != null)
            {
                if (!w.clothes.NullOrEmpty()) parts.Add(c.Text(w.clothesForCold ? "CC_Camp_Facts_DressedCold" : "CC_Camp_Facts_DressedHeat", null, null, 0, w.clothes));
                if (w.cold) parts.Add(c.Text(w.windbreak ? "CC_Camp_Facts_PitchColdWind" : "CC_Camp_Facts_PitchCold"));
                else if (w.windbreak) parts.Add(c.Text("CC_Camp_Facts_PitchWind"));
                if (w.shade) parts.Add(c.Text("CC_Camp_Facts_PitchShade"));
                // What they put up to sleep in, as the layout has it (CampSite.Layout).
                if (!w.shelter.NullOrEmpty() && ("CC_Camp_Facts_Shelter_" + w.shelter).CanTranslate()) parts.Add(c.Text("CC_Camp_Facts_Shelter_" + w.shelter));
            }
            // The day's work they mean to do round their camp (CampWork.Day): their callings and the camp's common work.
            string labour = c.WorkWords();
            if (!labour.NullOrEmpty()) parts.Add(c.Text("CC_Camp_Facts_Work", null, null, 0, labour));
            if (c.trader != null) parts.Add(c.Text("CC_Camp_Facts_Trader", c.trader));
            // A land kind's own work (CampKindsLand.cs): where they fish, what they strip, the fields they'd reap.
            string work = c.LandFacts();
            if (work != null) parts.Add(work);
            string animals = c.AnimalsText();
            if (animals != null) parts.Add(c.Text("CC_Camp_Facts_Animals", null, null, 0, animals));
            if (c.kind.asks?.Any(x => x != null && !x.theyAsk) ?? false) parts.Add(c.Text("CC_CampAsk_Facts"));
            parts.Add(c.Text("CC_Camp_Facts_Talk"));
            return string.Join(" ", parts);
        }

        /// <summary>Already leaving (a raid, the weather) or at war by the time the colony answers: only letting them be is left.</summary>
        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option == 0) return null;
            var camp = CampOfEvent(ctx);
            return camp != null && camp.CanTalk ? null : "CC_Camp_NotTalking".Translate().ToString();
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            var camp = CampOfEvent(ctx);
            if (camp == null || !camp.CanTalk) return outcome;
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
