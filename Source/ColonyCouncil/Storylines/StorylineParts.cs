using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // A storyline's parts and its choice cards (Storylines.cs). A part is a mission (Missions.cs) offered by an event of its
    // own: the first part is drawn by the storyteller, the later ones fired by the storyline when they're due. Its texts
    // live on its event (Defs/MissionDefs/Rimfolk_Storyline_*.xml): the HappeningExtension's titles, texts (opening with
    // what happened before), answers and spoken lines, and the StorylinePart's terms, messages and how it ended. A card is
    // an event with answers fired while a part runs; the answer goes back to the part, which plays it out and ends.

    /// <summary>Which storyline and part an event offers, and the part's own words beyond the offer.</summary>
    public class StorylinePart : DefModExtension
    {
        public string story;
        public string part;
        /// <summary>The first part: drawn by the storyteller, and it starts the storyline.</summary>
        public bool start;
        /// <summary>Under the offer: who asks, what, by when, what the colony gets, and what happens if it fails.</summary>
        [MustTranslate] public List<string> terms = new List<string>();
        /// <summary>A message once the colony takes it on.</summary>
        [MustTranslate] public List<string> accepted = new List<string>();
        /// <summary>A day before the time runs out.</summary>
        [MustTranslate] public List<string> reminder = new List<string>();
        /// <summary>Messages along the way, tagged with what happened ("wet", "spoiled", "came").</summary>
        [MustTranslate] public List<string> notes = new List<string>();
        /// <summary>How it ended, as a letter: tagged with the outcome.</summary>
        [MustTranslate] public List<string> endTitles = new List<string>();
        [MustTranslate] public List<string> ends = new List<string>();
        /// <summary>Outcomes told in a good letter, and in a bad one (the rest are neutral).</summary>
        public List<string> good = new List<string>();
        public List<string> bad = new List<string>();

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (!StorylineMath.Kinds.Contains(story)) yield return "storyline part names no storyline: " + story;
            else if (!StorylineMath.Parts(story).Contains(part)) yield return "storyline " + story + " has no part " + part;
        }
    }

    /// <summary>A storyline's choice card: which storyline it belongs to.</summary>
    public class StorylineCard : DefModExtension
    {
        public string story;
    }

    /// <summary>
    /// What every part shares, for the parts that are a mission of their own kind and for those built on another mission's
    /// worker (the children's stay is a hosting mission): when it may be offered, the storyline it belongs to, its title
    /// and description, its terms, its messages and its end.
    /// </summary>
    public static class StorylineKit
    {
        private static int Now => Find.TickManager.TicksGame;

        /// <summary>One mission at a time; the first part when the pace allows, a later one when its storyline is due for it.</summary>
        public static bool CanHappen(StorylinePart p, Map map, Func<Map, bool> possible)
        {
            if (p == null || Missions.All.Any(m => m.Open)) return false;
            if (p.start) return Storylines.CanStart(p.story) && StorytellerEvents.Adults(map).Any() && possible(map);
            var s = Storylines.current;
            return s != null && s.Running && s.kind == p.story && s.next == p.part && possible(map);
        }

        /// <summary>The storyline a part is being offered for: one being started, or one whose part is due.</summary>
        public static Storyline Offering => Storylines.making ?? Storylines.current;

        public static void Cast(StorylinePart p, Map map)
        {
            if (p != null && p.start) Storylines.making = new Storyline { kind = p.story, mapId = map?.uniqueID ?? -1 };
        }

        /// <summary>After the mission is registered: a new storyline registered, and the part noted as offered.</summary>
        public static bool Begin(StorylinePart p, HappeningContext ctx)
        {
            var s = p.start ? Storylines.making : Storylines.current;
            var m = Missions.Of(ctx);
            if (s == null || m == null) return false;
            if (p.start)
            {
                Storylines.Register(s, ctx.map);
                Storylines.making = null;
            }
            Storylines.PartOffered(s, m, p.part);
            Storylines.Pin(s);
            return true;
        }

        /// <summary>A first part that couldn't be offered after all: the people it made are let go.</summary>
        public static void Abort(StorylinePart p)
        {
            if (p == null || !p.start) return;
            var s = Storylines.making;
            Storylines.making = null;
            if (s == null) return;
            foreach (var x in new[] { s.x, s.y }.Concat(s.group))
                if (x != null && !x.Spawned)
                    Arrivals.Discard(x);
        }

        public static void AddTags(Mission m, HashSet<string> tags)
        {
            var s = m != null ? Storylines.OfMission(m.id) ?? Offering : Offering;
            if (s == null) return;
            foreach (var t in Storylines.Tags(s)) tags.Add(t);
        }

        /// <summary>The storyline's tags and the mission's, for every line of a part.</summary>
        public static List<string> Tags(Storyline s, Mission m)
        {
            var tags = s != null ? Storylines.Tags(s) : new List<string>();
            if (m != null) tags.AddRange(Missions.Tags(m));
            return tags;
        }

        /// <summary>
        /// Into the Quests tab (or a letter with "accept" and "decline"): titled "Part 2: The rival's offer", its description
        /// the storyline's name and part, then the part's text (which opens with what happened before), then its terms.
        /// </summary>
        public static void Send(IncidentWorker_Happening w, HappeningContext ctx, HappeningExtension ext, int rating, string terms, LookTargets look)
        {
            var m = Missions.Of(ctx);
            var s = m != null ? Storylines.OfMission(m.id) : null;
            var tags = Tags(s, m);
            tags.AddRange(w.TextTags(ctx));
            string partTitle = Storylines.Line(ext.titles, ctx, s, tags) ?? w.def.label;
            int n = s?.steps.Count ?? 1;
            string label = "CC_Storyline_PartTitle".Translate(n.Named("N"), partTitle.CapitalizeFirst().Named("TITLE")).Resolve();
            if (m != null) m.title = label;
            if (s?.Last != null) s.Last.title = label;
            string header = s != null ? "CC_Storyline_Header".Translate(Storylines.Label(s).Named("STORY"), n.Named("N")).Resolve() : null;
            string text = Storylines.Line(ext.texts, ctx, s, tags) ?? "";
            ctx.text = text;
            ctx.why = label;
            ctx.cause = label;
            string body = (header.NullOrEmpty() ? "" : header + "\n\n") + text + (terms.NullOrEmpty() ? "" : "\n\n" + terms);
            if (m != null && Missions.AsQuests && Missions.TryOfferAsQuest(m, label, body, ext.answerDays, rating)) return;
            // A letter with the answers, as every Rimfolk event with a choice.
            int ticks = Mathf.RoundToInt(Mathf.Max(0.1f, ext.answerDays) * GenDate.TicksPerDay);
            body += "\n\n" + "CC_Evt_Deadline".Translate(TextUtil.DaysString(ticks), ctx.Format(ext.options[Mathf.Clamp(ext.defaultOption, 0, ext.options.Count - 1)].label)).Resolve();
            var letter = (ChoiceLetter_Happening)LetterMaker.MakeLetter(label, body, CouncilDefOf.Council_EventLetter, look ?? LookTargets.Invalid);
            letter.ctx = ctx;
            letter.StartTimeout(ticks);
            ColonyCouncil.Tell.Ask(letter, ctx.map);
        }

        /// <summary>The part's terms, filled in: {ITEM} the goods, {DAYS} the time once taken on, {SILVER} a sum.</summary>
        public static string Terms(StorylinePart p, HappeningContext ctx, Mission m, string silver)
        {
            var s = m != null ? Storylines.OfMission(m.id) ?? Offering : Offering;
            if (p == null || m == null) return null;
            return Storylines.Line(p.terms, ctx, s, Tags(s, m), Missions.GoodsText(m), TextUtil.DaysString(Mathf.RoundToInt(m.days * GenDate.TicksPerDay)), silver);
        }

        /// <summary>The context for a part's words while it runs.</summary>
        public static HappeningContext Context(Storyline s, Mission m, Map map)
        {
            var ctx = Storylines.Context(s, map);
            ctx.incident = m?.def;
            if (m?.a != null && !m.a.Dead && m.a.IsColonist) ctx.a = m.a;
            ctx.note = m != null ? Missions.Note(m) : ctx.note;
            return ctx;
        }

        /// <summary>A message of the part's own (tagged <paramref name="key"/>), shown at once.</summary>
        public static void Note(StorylinePart p, Storyline s, Mission m, Map map, string key, LookTargets look, MessageTypeDef type = null, string item = null, string silver = null,
            string days = null)
        {
            if (p == null || s == null) return;
            var ctx = Context(s, m, map);
            var tags = Tags(s, m);
            tags.Add(key);
            string line = Storylines.Line(p.notes?.Where(l => HasTag(l, key)).ToList(), ctx, s, tags, item, days, silver);
            if (!line.NullOrEmpty()) ColonyCouncil.Tell.Message(NewsKind.World, Importance.Important, map, line, look ?? LookTargets.Invalid, type ?? MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>A letter of the part's own along the way (tagged <paramref name="key"/>; its title from the end titles with the same tag).</summary>
        public static void NoteLetter(StorylinePart p, Storyline s, Mission m, Map map, string key, LookTargets look, LetterDef letter)
        {
            if (p == null || s == null) return;
            var ctx = Context(s, m, map);
            var tags = Tags(s, m);
            tags.Add(key);
            string body = Storylines.Line(p.notes?.Where(l => HasTag(l, key)).ToList(), ctx, s, tags);
            if (body.NullOrEmpty()) return;
            string title = Storylines.Line(p.endTitles?.Where(l => HasTag(l, key)).ToList(), ctx, s, tags) ?? m?.title ?? Storylines.Label(s);
            ColonyCouncil.Tell.Letter(NewsKind.World, Importance.Important, map, title, body, letter, look ?? LookTargets.Invalid);
        }

        /// <summary>The accepted message.</summary>
        public static void Accepted(StorylinePart p, Storyline s, Mission m, Map map, LookTargets look, string silver = null)
        {
            if (p == null || s == null) return;
            var ctx = Context(s, m, map);
            string line = Storylines.Line(p.accepted, ctx, s, Tags(s, m), Missions.GoodsText(m), TextUtil.DaysString(Mathf.RoundToInt(m.days * GenDate.TicksPerDay)), silver);
            // direct: the answer to the player's click taking the part on.
            if (!line.NullOrEmpty()) Messages.Message(line, look ?? LookTargets.Invalid, MessageTypeDefOf.PositiveEvent, false);
        }

        /// <summary>A day before its time runs out, once.</summary>
        public static void Remind(StorylinePart p, Storyline s, Mission m, Map map, int now, string silver = null)
        {
            if (p == null || s == null || m.reminded || m.deadline < 0 || now < m.deadline - GenDate.TicksPerDay) return;
            m.reminded = true;
            var ctx = Context(s, m, map);
            string line = Storylines.Line(p.reminder, ctx, s, Tags(s, m), Missions.GoodsText(m), TextUtil.DaysString(Mathf.Max(0, m.deadline - now)), silver);
            if (!line.NullOrEmpty()) ColonyCouncil.Tell.Message(NewsKind.World, Importance.Notable, map, line, LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>
        /// The part ends: its letter (the part's own words for this outcome), then the mission's end, which tells the storyline
        /// how it ended (Storylines.MissionEnded) so the next part or the ending follows.
        /// </summary>
        public static void End(StorylinePart p, Mission m, MissionState state, string outcome, LookTargets look = null, string item = null, string silver = null)
        {
            var s = Storylines.OfMission(m.id);
            var map = m.Map;
            if (p != null && s != null)
            {
                try
                {
                    var ctx = Context(s, m, map);
                    var tags = Tags(s, m);
                    tags.Add(outcome);
                    string body = Storylines.Line(p.ends?.Where(l => HasTag(l, outcome)).ToList(), ctx, s, tags, item, null, silver);
                    if (!body.NullOrEmpty())
                    {
                        string title = Storylines.Line(p.endTitles?.Where(l => HasTag(l, outcome)).ToList(), ctx, s, tags, item, null, silver) ?? m.title;
                        var letter = p.good.Contains(outcome) ? LetterDefOf.PositiveEvent : p.bad.Contains(outcome) ? LetterDefOf.NegativeEvent : LetterDefOf.NeutralEvent;
                        ColonyCouncil.Tell.Letter(NewsKind.World, Importance.Notable, map, title, body, letter, look ?? LookTargets.Invalid);
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("storyline part end " + p.story + " " + p.part, e);
                }
            }
            Missions.Finish(m, state, outcome, item, look);
        }

        public static bool HasTag(string line, string key)
        {
            Flavor.Strip(line, out var t);
            return t != null && t.Any(x => x.Split('|').Any(a => a.Trim() == key));
        }

        /// <summary>A line of the event's own spoken lines (tagged <paramref name="key"/>), said over their head.</summary>
        public static void Say(Pawn who, IncidentDef def, Storyline s, Mission m, string key)
        {
            var ext = def?.GetModExtension<HappeningExtension>();
            if (who == null || !who.Spawned || who.Dead || ext?.lines == null || s == null) return;
            var ctx = Context(s, m, who.Map);
            var tags = Tags(s, m);
            tags.Add(key);
            string line = Storylines.Line(ext.lines.Where(l => HasTag(l, key)).ToList(), ctx, s, tags);
            if (!line.NullOrEmpty()) Speech.Say(who, line);
        }

        /// <summary>
        /// A choice card now, for this part: its event fired with the storyline and the mission in hand (the card reads
        /// them). False when it couldn't be asked (nobody of the colony at home to answer).
        /// </summary>
        public static bool Ask(string cardDef, Storyline s, Mission m, Map map, Action<HappeningContext> fill)
        {
            var def = DefDatabase<IncidentDef>.GetNamedSilentFail(cardDef);
            if (def == null || map == null) return false;
            HappeningWorker_StorylineCard.asking = s;
            HappeningWorker_StorylineCard.askingFor = m;
            HappeningWorker_StorylineCard.fill = fill;
            try
            {
                var parms = StorytellerUtility.DefaultParmsNow(def.category ?? IncidentCategoryDefOf.Misc, map);
                parms.forced = true;
                return def.Worker.TryExecute(parms);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("storyline card " + cardDef, e);
                return false;
            }
            finally
            {
                HappeningWorker_StorylineCard.asking = null;
                HappeningWorker_StorylineCard.askingFor = null;
                HappeningWorker_StorylineCard.fill = null;
            }
        }

        /// <summary>A card of this storyline still waiting for an answer.</summary>
        public static bool CardOut(Storyline s)
        {
            string note = Storylines.Note(s) + ":";
            return Find.LetterStack.LettersListForReading.Any(l => l is ChoiceLetter_Happening h && !h.resolved && h.ctx?.note != null && h.ctx.note.StartsWith(note));
        }
    }

    /// <summary>
    /// A part of a storyline that is a mission of its own kind. The storyline it belongs to is the one being started or
    /// the one whose part is due while it is offered, and the one whose mission it is afterwards (<see cref="Story"/>).
    /// </summary>
    public abstract class HappeningWorker_StorylinePart : HappeningWorker_Mission, IStorylinePartWorker
    {
        public StorylinePart P => def.GetModExtension<StorylinePart>();

        protected override string Kind => "storyline:" + P?.story + ":" + P?.part;

        protected override float RestDays => 0f;

        protected override int Rating => 3;

        protected bool IsStart => P?.start ?? false;

        /// <summary>The event's own checks for a part that's due (settings, colony, and whether it can go on).</summary>
        public bool CanOffer(IncidentParms parms) => CanFireNowSub(parms);

        protected override bool CanHappen(Map map, HappeningExtension ext) => StorylineKit.CanHappen(P, map, Possible);

        /// <summary>The storyline of this mission (or the one it is being offered for).</summary>
        protected static Storyline Story(Mission m) => (m != null ? Storylines.OfMission(m.id) : null) ?? StorylineKit.Offering;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            StorylineKit.Cast(P, ctx.map);
            return base.Cast(ctx, ext);
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) => base.Begin(ctx, ext) && StorylineKit.Begin(P, ctx);

        protected override void Abort(HappeningContext ctx)
        {
            base.Abort(ctx);
            StorylineKit.Abort(P);
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            base.AddTags(ctx, tags);
            StorylineKit.AddTags(Missions.Of(ctx) ?? Making, tags);
        }

        /// <summary>A second sum the terms name ({SILVER}).</summary>
        protected virtual string Silver(Mission m, Storyline s) => s != null ? s.amount.ToString() : null;

        protected override string Extra(HappeningContext ctx)
        {
            var m = Missions.Of(ctx) ?? Making;
            return StorylineKit.Terms(P, ctx, m, Silver(m, Story(m)));
        }

        protected override void Send(HappeningContext ctx, HappeningExtension ext) => StorylineKit.Send(this, ctx, ext, Rating, Extra(ctx), Look(ctx));

        /// <summary>A card's answer for this part (the card's own option index and whether it fell to the default).</summary>
        public virtual void Answered(Mission m, Storyline s, HappeningContext ctx, int option, bool byDefault)
        {
        }

        /// <summary>What a card's answer will do, for the card's impact lines.</summary>
        public virtual void Describe(Mission m, Storyline s, string choice, HappeningContext ctx, ImpactSheet sheet)
        {
        }

        /// <summary>Its people are the storyline's: nothing of theirs is let go when the part ends.</summary>
        public override void Release(Mission m)
        {
        }

        protected void Note(Mission m, Map map, string key, LookTargets look, MessageTypeDef type = null, string item = null, string silver = null, string days = null) =>
            StorylineKit.Note(P, Story(m), m, map, key, look, type, item, silver, days);

        protected void End(Mission m, MissionState state, string outcome, LookTargets look = null, string item = null, string silver = null) =>
            StorylineKit.End(P, m, state, outcome, look, item, silver);

        protected void Say(Pawn who, Mission m, string key) => StorylineKit.Say(who, def, Story(m), m, key);
    }

    /// <summary>
    /// A storyline's choice card: an event letter with answers, asked while a part runs (StorylineKit.Ask). The answer's
    /// effects show what it will do (DilemmaEffect_StorylineChoice, and any play-out such as goods carried out); the part
    /// then plays it out and ends.
    /// </summary>
    public class HappeningWorker_StorylineCard : IncidentWorker_Happening
    {
        internal static Storyline asking;
        internal static Mission askingFor;
        internal static Action<HappeningContext> fill;

        protected override bool CanHappen(Map map, HappeningExtension ext) => asking != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var s = asking;
            if (s == null) return false;
            var c = StorylineKit.Context(s, askingFor, ctx.map);
            ctx.a = c.a;
            ctx.faction = c.faction;
            ctx.enemy = c.enemy;
            ctx.stranger = c.stranger;
            ctx.placeName = c.placeName;
            ctx.beast = c.beast;
            ctx.tale = c.tale;
            ctx.count = c.count;
            ctx.note = Storylines.Note(s) + ":" + (askingFor?.id ?? -1);
            fill?.Invoke(ctx);
            return ctx.a != null;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            var s = asking ?? StorylineOf(ctx);
            if (s == null) return;
            foreach (var t in Storylines.Tags(s)) tags.Add(t);
        }

        /// <summary>The card's storyline from its note ("storyline:5:12").</summary>
        public static Storyline StorylineOf(HappeningContext ctx)
        {
            string n = ctx?.note;
            if (n == null || !n.StartsWith("storyline:")) return null;
            var bits = n.Substring(10).Split(':');
            return int.TryParse(bits[0], out int id) ? Storylines.ById(id) : null;
        }

        /// <summary>The part's mission the card was asked for.</summary>
        public static Mission MissionOf(HappeningContext ctx)
        {
            string n = ctx?.note;
            if (n == null || !n.StartsWith("storyline:")) return null;
            var bits = n.Substring(10).Split(':');
            return bits.Length > 1 && int.TryParse(bits[1], out int id) ? Missions.ById(id) : null;
        }

        /// <summary>The card's own words, with the storyline's names filled in.</summary>
        protected override void Send(HappeningContext ctx, HappeningExtension ext)
        {
            var s = asking ?? StorylineOf(ctx);
            var tags = TextTags(ctx).ToList();
            string label = (Storylines.Line(ext.titles, ctx, s, tags) ?? def.label).CapitalizeFirst();
            ctx.text = Storylines.Line(ext.texts, ctx, s, tags) ?? "";
            ctx.why = label;
            ctx.cause = label;
            int ticks = Mathf.RoundToInt(Mathf.Max(0.1f, ext.answerDays) * GenDate.TicksPerDay);
            string body = ctx.text + "\n\n" + "CC_Evt_Deadline".Translate(TextUtil.DaysString(ticks), ctx.Format(ext.options[Mathf.Clamp(ext.defaultOption, 0, ext.options.Count - 1)].label)).Resolve();
            var letter = (ChoiceLetter_Happening)LetterMaker.MakeLetter(label, body, ext.threat ? CouncilDefOf.Council_EventLetterThreat : CouncilDefOf.Council_EventLetter, Look(ctx));
            letter.ctx = ctx;
            letter.StartTimeout(ticks);
            ColonyCouncil.Tell.Ask(letter, ctx.map);
            Asked(ctx, ext);
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            var s = StorylineOf(ctx);
            var m = MissionOf(ctx);
            if (s != null && m != null && m.Open && Missions.Worker(m) is IStorylinePartWorker w)
            {
                if (ctx.map == null || !Find.Maps.Contains(ctx.map)) ctx.map = m.Map;
                try
                {
                    w.Answered(m, s, ctx, option, byDefault);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("storyline card answer " + def.defName, e);
                }
            }
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }

        /// <summary>The people the card is about stay the storyline's when the letter is done with.</summary>
        public override void Cleanup(HappeningContext ctx)
        {
        }
    }

    /// <summary>A part that answers a card (both kinds of part worker).</summary>
    public interface IStorylinePartWorker
    {
        /// <summary>The event's own checks for a part that's due (settings, colony, and whether it can go on).</summary>
        bool CanOffer(IncidentParms parms);

        void Answered(Mission m, Storyline s, HappeningContext ctx, int option, bool byDefault);

        void Describe(Mission m, Storyline s, string choice, HappeningContext ctx, ImpactSheet sheet);
    }

    /// <summary>
    /// An answer of a storyline's card, for what it will do under the answer (silver, goodwill, who joins, what plays out
    /// on the map). The part's worker plays the answer out when it is given; this only describes it.
    /// </summary>
    public class DilemmaEffect_StorylineChoice : DilemmaEffect
    {
        public string choice;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return;
            var s = HappeningWorker_StorylineCard.asking ?? HappeningWorker_StorylineCard.StorylineOf(hc);
            var m = HappeningWorker_StorylineCard.askingFor ?? HappeningWorker_StorylineCard.MissionOf(hc);
            if (s == null || m == null || !(Missions.Worker(m) is IStorylinePartWorker w)) return;
            try
            {
                w.Describe(m, s, choice, hc, sheet);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("storyline choice " + choice, e);
            }
        }

        public override void Apply(DilemmaContext ctx)
        {
        }
    }
}
