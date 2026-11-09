using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>A line of what an answer costs or risks on its card: a cost, a gain within a risk, or the gamble's own line.</summary>
    internal struct EventCardRow
    {
        public string text;
        public int sign;
        public bool risk;
    }

    /// <summary>One of a letter's options on the card, with what the card shows of it.</summary>
    internal sealed class EventCardOption
    {
        public DiaOption opt;
        public CardOptionRole role;
        public string label;
        /// <summary>The short line under an answer (the flavour line, the hint in words or the numbers, as the setting has it).</summary>
        public string line;
        /// <summary>The full list on hover.</summary>
        public string tip;
        public readonly List<EventCardRow> rows = new List<EventCardRow>();
        public int moreRows;
        public bool disabled;
        public string why;
        /// <summary>Why it can't be chosen now, in the card's words; "and 2 more" under its lines; its tooltip (why not, then all it does).</summary>
        public string whyText, moreText, cardTip;
        public bool leaned;
        public string custom;
        public List<Pawn> backers;
        public string backersText;
        /// <summary>A link's def, for its icon.</summary>
        public Def linkDef;
    }

    /// <summary>Someone the letter is about, as the card shows them: read once, when it opens (the game is paused while it shows).</summary>
    internal sealed class EventCardPerson
    {
        public Pawn pawn;
        public string name;
        /// <summary>Who they are to the colony, for anyone but its own (a prisoner, a guest of a people, an animal).</summary>
        public string sub;
        /// <summary>Their mood, 0 to 1, or below 0 for none; how near a break it is (0 fine, 1 minor, 2 major, 3 extreme).</summary>
        public float mood = -1f;
        public int moodLevel;
        public string moodText;
        /// <summary>The heart, for the colony's own: the band's word, and whether it is the good side's.</summary>
        public bool heart;
        public StrainBand band;
        public GoodBand good;
        public string heartWord;
        public string tip;
        public bool canJump;
    }

    /// <summary>
    /// Everything Rimfolk's event card shows of a letter, built from the letter's own data when the card opens (its title,
    /// text, kind and colour, the people and the thing its look targets name, its people, its options as the game made them)
    /// and the facts its class adds (<see cref="RimfolkLetter.Describe"/>). Nothing here changes the letter.
    /// </summary>
    internal sealed class EventCardModel
    {
        public ChoiceLetter letter;
        public string bar;
        public string title;
        public string kind;
        public string kindWord;
        public Color kindColour;
        public string text;
        // The header's picture: people, a thing, an emblem or the kind's mark.
        public readonly List<Pawn> pictured = new List<Pawn>();
        public Thing thing;
        public ThingDef thingDef;
        public Faction faction;
        public string stance, stanceTip;
        // Who is involved, and what the council leans to.
        public readonly List<EventCardPerson> people = new List<EventCardPerson>();
        public string moreTip, moreText;
        public int more;
        public string leanText;
        public List<Pawn> leanBy;
        // The options, by what each is on the card.
        public readonly List<EventCardOption> answers = new List<EventCardOption>();
        public readonly List<EventCardOption> leave = new List<EventCardOption>();
        public readonly List<EventCardOption> acts = new List<EventCardOption>();
        public readonly List<EventCardOption> links = new List<EventCardOption>();
        public EventCardOption jump, quest;
        /// <summary>Where the letter points when it has no jump of its own: a "show on the map" button.</summary>
        public GlobalTargetInfo look = GlobalTargetInfo.Invalid;
        /// <summary>Its facts, kept for a node the letter leads on to (the people and the header stay; its answers don't).</summary>
        public EventCardFacts facts;

        /// <summary>The words the game gives its own options (close, postpone, reject; jump; the quest's page), read once a card.</summary>
        private static List<string> LeaveWords() =>
            new[] { "Close", "PostponeLetter", "RejectLetter" }.Where(k => k.CanTranslate()).Select(k => k.Translate().ToString()).ToList();

        private static string Word(string key) => key.CanTranslate() ? key.Translate().ToString() : null;

        /// <summary>The card for a letter opening: its first page (the letter's text and choices, as the game's own window takes them).</summary>
        public static EventCardModel Build(ChoiceLetter letter, DiaNode node)
        {
            var m = new EventCardModel { letter = letter };
            m.facts = new EventCardFacts();
            if (letter is RimfolkLetter own)
            {
                try
                {
                    own.Describe(m.facts);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("event card: what " + letter.GetType().Name + " is about", e);
                }
            }
            m.Header();
            m.Page(node, true);
            return m;
        }

        /// <summary>The letter's own words, its kind, and who and what it is about.</summary>
        private void Header()
        {
            var f = facts;
            // Who and what its look targets name, after what the letter itself said.
            var targets = letter.lookTargets?.targets;
            if (targets != null)
                foreach (var t in targets)
                {
                    if (!t.HasThing) continue;
                    if (t.Thing is Pawn p) f.Person(p);
                    else if (f.thing == null && !t.Thing.Destroyed) f.thing = t.Thing;
                }
            f.faction ??= letter.relatedFaction;
            if (f.faction != null && (f.faction.IsPlayer || f.faction.Hidden)) f.faction = null;
            thing = f.thing;
            thingDef = f.thingDef;
            faction = f.faction;
            if (faction != null)
            {
                stance = DipWords.Stance(faction);
                stanceTip = faction.HasGoodwill ? "CC_EventCard_Goodwill".Translate(faction.Name.Named("PEOPLE"), faction.PlayerGoodwill.Named("GOODWILL")).Resolve() : faction.Name;
            }
            title = (letter.title.NullOrEmpty() ? letter.Label.Resolve() : letter.title).StripTags();
            int now = Find.TickManager.TicksGame;
            int ago = Mathf.Max(0, now - letter.arrivalTick);
            bar = "CC_EventCard_Bar".Translate((ago < GenDate.TicksPerHour ? "CC_EventCard_JustNow".Translate().Resolve() : "CC_EventCard_Ago".Translate(TextUtil.DaysString(ago)).Resolve())
                .Named("CAME")).Resolve();
            kindColour = letter.def?.color ?? Color.white;
            // The people: the most important first, the dead and the gone left out of the picture, kept in the list.
            foreach (var p in f.people)
            {
                if (p == null || p.Discarded) continue;
                if (pictured.Count < EventCardMath.PictureMost && !p.Dead) pictured.Add(p);
                if (people.Count < EventCardMath.InvolvedMost) people.Add(Person(p));
                else
                {
                    more++;
                    moreTip = (moreTip == null ? "" : moreTip + "\n") + p.LabelShortCap;
                }
            }
            if (more > 0) moreText = "CC_EventCard_MorePeople".Translate(more.Named("COUNT")).Resolve();
        }

        /// <summary>A page of the letter: the first, or one an option led on to (its text and options; the header stays).</summary>
        public void Page(DiaNode node, bool first)
        {
            text = node?.text.Resolve() ?? "";
            answers.Clear();
            leave.Clear();
            acts.Clear();
            links.Clear();
            jump = quest = null;
            leanText = null;
            leanBy = null;
            var options = node?.options ?? new List<DiaOption>();
            var leaveWords = LeaveWords();
            string jumpWord = Word("JumpToLocation"), questWord = Word("ViewRelatedQuest");
            for (int i = 0; i < options.Count; i++)
            {
                var o = options[i];
                if (o == null) continue;
                bool linked = HasLink(o.hyperlink);
                string words = Answers.TextOf(o) ?? "";
                var c = new EventCardOption
                {
                    opt = o,
                    role = EventCardMath.RoleOf(words, o.resolveTree, o.link != null || o.linkLateBind != null, linked, leaveWords, jumpWord, questWord),
                    disabled = o.disabled,
                    why = o.disabledReason
                };
                c.label = o is DiaOption_Impact di ? di.bare : words;
                if (linked)
                {
                    c.linkDef = o.hyperlink.def;
                    if (c.label.NullOrEmpty()) c.label = o.hyperlink.Label;
                }
                c.label = (c.label ?? "").StripTags();
                if (c.disabled && !c.why.NullOrEmpty()) c.whyText = "CC_EventCard_CantNow".Translate(c.why.Named("WHY")).Resolve();
                if (c.role == CardOptionRole.Answer)
                {
                    Impact(c, o as DiaOption_Impact);
                    if (c.moreRows > 0) c.moreText = "CC_EventCard_MoreLines".Translate(c.moreRows.Named("COUNT")).Resolve();
                    c.cardTip = c.whyText == null ? c.tip : c.tip.NullOrEmpty() ? c.whyText : c.whyText + "\n\n" + c.tip;
                    if (first)
                    {
                        if (i == facts.lean) c.leaned = true;
                        if (i == facts.custom) c.custom = facts.customText;
                        if (facts.backers.TryGetValue(i, out var by) && by.Count > 0)
                        {
                            c.backers = by;
                            c.backersText = "CC_EventCard_Backers".Translate(by.Count.Named("COUNT"), TextUtil.JoinAnd(by.Select(p => p.LabelShort).ToList()).Named("WHO")).Resolve();
                        }
                    }
                    answers.Add(c);
                }
                else if (c.role == CardOptionRole.Leave) leave.Add(c);
                else if (c.role == CardOptionRole.Jump) jump ??= c;
                else if (c.role == CardOptionRole.Quest) quest ??= c;
                else if (c.role == CardOptionRole.Link) links.Add(c);
                else acts.Add(c);
            }
            if (first)
            {
                leanText = facts.leanText;
                if (facts.lean >= 0 && facts.backers.TryGetValue(facts.lean, out var lb)) leanBy = lb;
            }
            kind = EventCardMath.KindOf(letter.def?.defName, answers.Count > 0);
            kindWord = ("CC_EventCard_Kind_" + kind).Translate().Resolve();
            // No jump of the letter's own: the place it points at, when the camera can go there.
            look = GlobalTargetInfo.Invalid;
            if (jump == null && letter.lookTargets != null && letter.lookTargets.IsValid)
            {
                var t = letter.lookTargets.PrimaryTarget;
                if (t.IsValid && CameraJumper.CanJump(t)) look = t;
            }
        }

        private static bool HasLink(Dialog_InfoCard.Hyperlink h) =>
            h.def != null || h.thing != null || h.worldObject != null || h.titleDef != null || h.faction != null || h.quest != null || h.ideo != null || h.researchProject != null;

        /// <summary>What an answer does, as the setting "What each answer does" shows it: the short line, the full list on hover, and what it costs or risks.</summary>
        private static void Impact(EventCardOption c, DiaOption_Impact di)
        {
            if (di == null) return;
            c.line = di.compact;
            c.tip = di.full;
            var r = di.impact;
            if (r == null || r.Empty || !DilemmaImpact.On) return;
            bool exact = DilemmaImpact.Mode == ImpactShow.Exact;
            int costs = 0;
            foreach (var l in r.lines)
            {
                if (l.Sign >= 0) continue;
                string t = exact ? DilemmaImpact.LineText(l) : ImpactHints.Full(l, DilemmaImpact.Words);
                if (t.NullOrEmpty()) continue;
                if (costs++ < EventCardMath.CostLinesMost) c.rows.Add(new EventCardRow { text = t, sign = -1 });
                else c.moreRows++;
            }
            if (r.riskChance <= 0f) return;
            string head;
            if (exact)
            {
                string note = r.riskNote.NullOrEmpty() ? "" : " " + r.riskNote;
                head = (r.riskReplaces ? "CC_Impact_RiskInstead" : "CC_Impact_Risk").Translate(DilemmaImpact.Odds(r.riskChance), note).Resolve();
            }
            else head = ImpactHints.Rows(r, DilemmaImpact.Words).FirstOrDefault(x => x.risk)?.text;
            if (!head.NullOrEmpty()) c.rows.Add(new EventCardRow { text = head, risk = true });
            int risks = 0;
            foreach (var l in r.risk)
            {
                string t = exact ? DilemmaImpact.LineText(l) : ImpactHints.Full(l, DilemmaImpact.Words);
                if (t.NullOrEmpty()) continue;
                if (risks++ < EventCardMath.RiskLinesMost) c.rows.Add(new EventCardRow { text = t, sign = l.Sign, risk = true });
                else c.moreRows++;
            }
        }

        /// <summary>Someone the letter is about: their mood and how near a break, the heart's band for the colony's own, who they are to the colony.</summary>
        private static EventCardPerson Person(Pawn p)
        {
            var c = new EventCardPerson { pawn = p, name = p.LabelShortCap };
            var sb = new System.Text.StringBuilder(c.name);
            if (p.Dead) c.sub = "CC_EventCard_Dead".Translate().Resolve();
            else if (!p.RaceProps.Humanlike) c.sub = p.KindLabel.CapitalizeFirst();
            else if (p.IsPrisonerOfColony) c.sub = "CC_EventCard_Prisoner".Translate().Resolve();
            else if (p.IsSlaveOfColony) c.sub = "CC_EventCard_Slave".Translate().Resolve();
            else if (p.Faction != null && !p.Faction.IsPlayer) c.sub = p.Faction.Name;
            else if (p.Faction == null) c.sub = "CC_EventCard_NoPeople".Translate().Resolve();
            if (c.sub != null) sb.Append("\n").Append(c.sub);
            var need = p.Dead ? null : p.needs?.mood;
            if (need != null)
            {
                c.mood = need.CurLevelPercentage;
                var br = p.mindState?.mentalBreaker;
                if (br != null)
                    c.moodLevel = c.mood < br.BreakThresholdExtreme ? 3 : c.mood < br.BreakThresholdMajor ? 2 : c.mood < br.BreakThresholdMinor ? 1 : 0;
                c.moodText = "CC_EventCard_Mood".Translate(c.mood.ToStringPercent().Named("MOOD")).Resolve();
                sb.Append("\n").Append(c.moodText);
                if (br != null) sb.Append(" (").Append("CC_EventCard_MoodBreak".Translate(br.BreakThresholdMinor.ToStringPercent().Named("MINOR")).Resolve()).Append(")");
            }
            if (!p.Dead && Heart.Eligible(p))
            {
                var h = Heart.ForView(p);
                if (h != null)
                {
                    c.heart = true;
                    c.band = h.Band;
                    c.good = GoodHeart.Band(h);
                    bool goodSide = c.band == StrainBand.Steady && c.good >= GoodBand.GoodHeart;
                    c.heartWord = (goodSide ? GoodHeart.BandLabel(c.good) : HeartView.BandLabel(c.band)).CapitalizeFirst();
                    sb.Append("\n").Append("CC_EventCard_Heart".Translate(c.heartWord.Named("BAND")).Resolve());
                }
            }
            c.canJump = !p.Dead && CameraJumper.CanJump(new GlobalTargetInfo(p));
            sb.Append("\n\n").Append((c.canJump ? "CC_EventCard_PersonJump" : "CC_EventCard_PersonAway").Translate(p.Named("PAWN")).Resolve());
            c.tip = sb.ToString();
            return c;
        }
    }
}
