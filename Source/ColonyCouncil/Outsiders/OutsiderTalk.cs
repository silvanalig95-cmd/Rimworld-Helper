using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Talking with outsiders the way people do (the player's request: no spamming, colonists talking by themselves, bubbles
    /// rather than pop-ups, and a history that builds over time):
    /// <list type="bullet">
    /// <item>Patience: each outsider has patience for a few real things with the colony a visit (3, more when they think
    /// well of it, have a friend here or a kind nature; fewer when they think ill of it), coming back slowly for one who
    /// stays (<see cref="OutsiderTalkMath.Patience"/>). Asking the same thing again within its wait gets "you asked me that
    /// already" and nothing else; a colonist who asks three things in a row is pushy and liked a little less for it; gifts
    /// on one visit count for less and less. The right-click menu says plainly when they've had enough.</item>
    /// <item>Talks on their own: a colonist and an outsider near each other, both free, sometimes start a conversation
    /// by themselves (the sociable more, the shy less), paced with the colony's other exchanges (<see cref="SocialPace"/>),
    /// never with danger about, never with someone leaving or busy.</item>
    /// <item>Bubbles: every conversation is a short scene of two to six lines said in turn over their heads, at a pace
    /// to read them (<see cref="Play"/>). Only what matters comes as a message or letter.</item>
    /// <item>A history: conversation pieces (OutsiderTalkDef) chosen by what is true right now (<see cref="OutsiderFacts"/>),
    /// what's current first; what was said is kept: news heard goes into the town's "Heard lately" (the World page and
    /// the region map), the pair's marks are picked up by later talks with what became of them since.</item>
    /// </list>
    /// Lines under way aren't saved: after a load a talk under way is simply over.
    /// </summary>
    public static class OutsiderTalk
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>Colonists start talks with outsiders by themselves (the setting).</summary>
        public static bool ChatterOn => Outsiders.On && (S?.outsiderChatter ?? true);

        public static float ChatterFrequency => S?.outsiderChatterFrequency ?? 1f;

        /// <summary>The lines of a scene under way are looked at every this many ticks, at this tick of the cycle.</summary>
        public const int LinePeriod = 15, LineOffset = 11;

        public const string UseChat = "chat", UseHome = "home", UseKnow = "know";

        private static int Now => Find.TickManager.TicksGame;

        private static OutsiderBook Book => OutsiderBook.Instance;

        private static readonly ExchangeQueue<Pawn> scenes = new ExchangeQueue<Pawn>(10);
        private static readonly Dictionary<int, Action> done = new Dictionary<int, Action>();
        /// <summary>Pieces said at the colony lately (by anyone), newest last.</summary>
        private static readonly List<string> lately = new List<string>();
        /// <summary>What each ordered talk is to be, by the colonist's id, from the moment the two meet to its end.</summary>
        private static readonly Dictionary<int, Plan> plans = new Dictionary<int, Plan>();
        /// <summary>When each colonist last started a talk with an outsider by themselves.</summary>
        private static readonly Dictionary<int, int> started = new Dictionary<int, int>();

        public static bool Live => scenes.Count > 0;

        /// <summary>Someone is in a scene with an outsider under way.</summary>
        public static bool Busy(Pawn p) => p != null && scenes.Count > 0 && scenes.Busy(p);

        /// <summary>A new game or a load: nothing under way carries over.</summary>
        public static void Reset()
        {
            scenes.Clear();
            done.Clear();
            lately.Clear();
            plans.Clear();
            started.Clear();
        }

        // ================= Scenes =================

        /// <summary>
        /// The lines said in turn over the two's heads, each when the one before has been read (<see cref="OutsiderTalkMath.LineGap"/>);
        /// <paramref name="onDone"/> once the last is said with the two still together. Returns the ticks until the last
        /// line has been read, or -1 if it couldn't start.
        /// </summary>
        public static int Play(Pawn c, Pawn x, IList<(bool us, string text)> lines, Action onDone = null)
        {
            if (c == null || x == null || lines == null) return -1;
            int now = Now;
            var turns = new List<(Pawn, Pawn, string)>();
            var dues = new List<int>();
            int due = now + OutsiderTalkMath.FirstGap;
            foreach (var (us, text) in lines)
            {
                if (text.NullOrEmpty()) continue;
                turns.Add(us ? (c, x, text) : (x, c, text));
                dues.Add(due);
                due += OutsiderTalkMath.LineGap(text.Length);
            }
            if (turns.Count == 0) return -1;
            // An ordered talk comes first: whatever else either was saying is let go.
            scenes.DropWith(c);
            scenes.DropWith(x);
            int id = scenes.AddAt(turns, dues);
            if (id == 0) return -1;
            if (onDone != null) done[id] = onDone;
            return due - now;
        }

        /// <summary>Every 15 ticks while a scene is under way: the lines due are said, if the two are still together.</summary>
        public static void Step(int now)
        {
            List<(int id, ExchangeQueue<Pawn>.Line line)> due;
            try
            {
                due = scenes.Due(now);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("outsider scene lines", e);
                scenes.Clear();
                done.Clear();
                return;
            }
            foreach (var (id, line) in due)
            {
                // They parted, or one was called away: the rest of it isn't said, and it leaves nothing.
                if (!Together.Can(line.speaker, line.listener))
                {
                    scenes.Drop(id);
                    done.Remove(id);
                    continue;
                }
                try
                {
                    // together: checked just above.
                    Speech.Say(line.speaker, line.text, line.listener);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("outsider scene line", e);
                }
            }
            foreach (var (id, _) in due)
            {
                if (!done.TryGetValue(id, out var a) || scenes.Find(id) != null) continue;
                done.Remove(id);
                try
                {
                    a();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("outsider scene end", e);
                }
            }
            if (scenes.Count == 0) done.Clear();
        }

        // ================= Patience =================

        /// <summary>Their visit's record; a new visit when nothing passed with them for two days.</summary>
        public static OutsiderVisit VisitOf(Pawn x, bool make)
        {
            var book = Book;
            if (book == null || x == null) return null;
            int now = Now;
            var v = book.visits.FirstOrDefault(r => r.outsider == x);
            if (v != null)
            {
                int last = Mathf.Max(v.start, Mathf.Max(v.last, v.chatterLast));
                if (OutsiderMath.NewVisit(last, now))
                {
                    v.start = now;
                    v.last = -1;
                    v.used = 0f;
                    v.gifts = 0;
                    v.chatters = 0;
                    v.chatterLast = -1;
                    v.asks.Clear();
                }
                return v;
            }
            if (!make) return null;
            v = new OutsiderVisit { outsider = x, start = now };
            book.visits.Add(v);
            while (book.visits.Count > OutsiderBook.MaxVisits) book.visits.RemoveAt(0);
            return v;
        }

        /// <summary>How many things they have patience for with the colony this visit.</summary>
        public static int MaxPatience(Pawn x) => OutsiderTalkMath.Patience(Outsiders.OfColony(x), NotableBook.Warmth(x), Book?.AnyFriend(x) ?? false);

        private static float Used(OutsiderVisit v, int now) =>
            v == null ? 0f : OutsiderTalkMath.Rested(v.used, v.last < 0 ? 0f : (now - v.last) / (float)GenDate.TicksPerHour);

        /// <summary>Things left that they have patience for now.</summary>
        public static int Left(Pawn x) => OutsiderTalkMath.Left(MaxPatience(x), Used(VisitOf(x, false), Now));

        /// <summary>They had enough of the colony for now for a thing that asks their patience; null, or why not in words.</summary>
        public static string Tired(Pawn x, OutsiderAct act) =>
            OutsiderTalkMath.Cost(act) > 0f && Left(x) <= 0 ? "CC_Out_Why_Enough".Translate().Resolve() : null;

        /// <summary>Their patience in words, for the menu ("glad of the company" ... "has had enough talk for now").</summary>
        public static string PatienceWords(Pawn x) => ("CC_Out_Patience_" + OutsiderTalkMath.PatienceWord(Left(x))).Translate().Resolve();

        public static void Spend(Pawn x, float cost)
        {
            if (cost <= 0f) return;
            var v = VisitOf(x, true);
            if (v == null) return;
            int now = Now;
            v.used = Used(v, now) + cost;
            v.last = now;
        }

        // ================= Asking again, and asking too much =================

        /// <summary>
        /// The same thing asked again within its wait (by anyone, for the things they do once for the whole colony): true,
        /// with who asked before.
        /// </summary>
        public static bool Repeat(Pawn c, Pawn x, OutsiderAct act, out Pawn asker)
        {
            asker = null;
            var book = Book;
            if (book == null || !OutsiderTalkMath.Asks(act)) return false;
            int last = -1;
            if (OutsiderMath.PerOutsider(act))
            {
                foreach (var r in book.pairs)
                    if (r.outsider == x && r.Last(act) > last)
                    {
                        last = r.Last(act);
                        asker = r.colonist;
                    }
            }
            else
            {
                last = book.Pair(c, x, false)?.Last(act) ?? -1;
                asker = c;
            }
            return !OutsiderMath.Waited(last, Now, act);
        }

        private static int AsksInRow(OutsiderVisit v, Pawn c, int now)
        {
            int n = 0;
            foreach (var s in v.asks)
            {
                var parts = s.Split('|');
                if (parts.Length < 3 || !int.TryParse(parts[0], out int id) || !int.TryParse(parts[2], out int t)) continue;
                if (id == c.thingIDNumber && now - t <= OutsiderTalkMath.PushyHours * GenDate.TicksPerHour) n++;
            }
            return n;
        }

        private static void NoteAsk(OutsiderVisit v, Pawn c, OutsiderAct act, int now)
        {
            v.asks.Add(c.thingIDNumber + "|" + (int)act + "|" + now);
            while (v.asks.Count > 8) v.asks.RemoveAt(0);
        }

        /// <summary>A pushy ask: a little off the bond between them, and off a notable's regard of the colonist.</summary>
        private static void Pushed(Pawn c, Pawn x)
        {
            var pair = Book?.Pair(c, x, true);
            if (pair != null) pair.warmth = Mathf.Clamp(pair.warmth + OutsiderTalkMath.PushyWarmth, -OutsiderMath.WarmthMax, OutsiderMath.WarmthMax);
            var n = Outsiders.NotableOf(x);
            if (n == null) return;
            n.RegardOf(c, true).personal += OutsiderTalkMath.PushyRegard;
            NotableBook.Changed();
        }

        /// <summary>The share a gift on this visit still counts for (the first fully, then less and less), counted.</summary>
        public static float GiftShare(Pawn x)
        {
            var v = VisitOf(x, true);
            if (v == null) return 1f;
            float share = OutsiderTalkMath.GiftFactor(v.gifts);
            v.gifts++;
            Spend(x, OutsiderTalkMath.Cost(OutsiderAct.Gift));
            return share;
        }

        // ================= An ordered talk =================

        /// <summary>What an ordered talk is to be: how it goes, the piece said, or that it was asked before.</summary>
        public sealed class Plan
        {
            public Pawn c, x;
            public OutsiderAct act;
            public TalkTone tone;
            public TalkChoice choice;
            public bool repeat;
        }

        /// <summary>The plan for the colonist's talk now ending (taken: asked once).</summary>
        public static Plan TakePlan(Pawn c)
        {
            if (c == null || !plans.TryGetValue(c.thingIDNumber, out var p)) return null;
            plans.Remove(c.thingIDNumber);
            return p;
        }

        /// <summary>
        /// The two have met for an ordered talk (from the talk job): their patience is used, the ask noted (and a pushy
        /// one felt), and the scene begins: "you asked me that already" for the same thing again; a conversation piece for
        /// a chat, news of home or getting to know them, chosen by how it goes; for the rest, the asking and a word each
        /// while they talk, the answer coming at the end. Returns how long the scene runs, in ticks.
        /// </summary>
        public static int Begin(Pawn c, Pawn x, OutsiderAct act)
        {
            int now = Now;
            var plan = new Plan { c = c, x = x, act = act };
            plans[c.thingIDNumber] = plan;
            var v = VisitOf(x, true);
            plan.repeat = Repeat(c, x, act, out var asker);
            bool pushy = false;
            if (v != null && OutsiderTalkMath.Asks(act))
            {
                pushy = OutsiderTalkMath.Pushy(AsksInRow(v, c, now));
                NoteAsk(v, c, act, now);
            }
            Spend(x, OutsiderTalkMath.Cost(act));
            if (pushy) Pushed(c, x);
            var lines = new List<(bool us, string text)>();
            string opening = Outsiders.Line(c, Outsiders.Opening(act), x);
            if (plan.repeat)
            {
                lines.Add((true, opening));
                lines.Add((false, Outsiders.Line(x, asker == c || asker == null ? "already" : "alreadyother", c, asker)));
            }
            else if (act == OutsiderAct.Chat || act == OutsiderAct.AskHome || act == OutsiderAct.Befriend)
            {
                plan.tone = OutsiderMath.Tone(Outsiders.Input(c, x, act), Rand.Value);
                if (act == OutsiderAct.AskHome && plan.tone == TalkTone.Sour)
                {
                    lines.Add((true, opening));
                    lines.Add((false, Outsiders.Line(x, "homenothing", c)));
                }
                else
                {
                    string use = act == OutsiderAct.AskHome ? UseHome : act == OutsiderAct.Befriend ? UseKnow : UseChat;
                    int max = act == OutsiderAct.AskHome ? Mathf.Max(3, OutsiderTalkMath.MaxLines(plan.tone)) : OutsiderTalkMath.MaxLines(plan.tone);
                    plan.choice = Choose(c, x, use, plan.tone, max);
                    if (plan.choice != null)
                    {
                        // The colonist walked over: if the piece opens with the other, the colonist's word comes first.
                        if (!plan.choice.lines[0].us && plan.choice.lines.Count < OutsiderTalkMath.MaxTurns) lines.Add((true, opening));
                        lines.AddRange(plan.choice.lines);
                    }
                    else
                    {
                        lines.Add((true, opening));
                        lines.Add((false, Outsiders.Line(x, Outsiders.During(act), c)));
                    }
                    // How it went, in their last word, when there's room for it.
                    if (act != OutsiderAct.AskHome && lines[lines.Count - 1].us && lines.Count < OutsiderTalkMath.MaxTurns)
                        lines.Add((false, Outsiders.Line(x, "end" + plan.tone.ToString().ToLowerInvariant(), c)));
                }
            }
            else
            {
                lines.Add((true, opening));
                lines.Add((false, Outsiders.Line(x, Outsiders.During(act), c)));
                lines.Add((true, Outsiders.Line(c, Outsiders.During(act), x)));
            }
            // Asked too much: they say so before anything else.
            if (pushy && lines.Count > 0 && lines[0].us && lines.Count < OutsiderTalkMath.MaxTurns) lines.Insert(0, (false, Outsiders.Line(x, "pushy", c)));
            lines.RemoveAll(l => l.text.NullOrEmpty());
            return Play(c, x, Alternate(lines));
        }

        /// <summary>Two lines in a row by the same speaker (one had nothing to say): the second goes.</summary>
        private static List<(bool us, string text)> Alternate(List<(bool us, string text)> lines)
        {
            var list = new List<(bool, string)>();
            foreach (var l in lines)
                if (list.Count == 0 || list[list.Count - 1].Item1 != l.us)
                    list.Add(l);
            return list;
        }

        // ================= The pieces =================

        /// <summary>A colonist's line about guns or power stays unsaid in a colony that has never had them.</summary>
        private static bool LineOk(TalkLine l) => !l.us || Flavor.TechFits(l.text);

        /// <summary>
        /// The conversation piece for the two now, by what is true (OutsiderFacts) and how it goes: a talk gone cool or
        /// sour with a piece written for it (else any short one), getting to know them with a personal piece (else any).
        /// </summary>
        public static TalkChoice Choose(Pawn c, Pawn x, string use, TalkTone tone, int maxLines)
        {
            var all = OutsiderTalkDef.All;
            if (all.Count == 0) return null;
            var facts = OutsiderFacts.For(c, x, tone);
            var pair = Book?.Pair(c, x, false);
            var topics = pair?.topics != null ? Enumerable.Reverse(pair.topics).ToList() : null;
            bool toneOnly = use != UseHome && tone >= TalkTone.Cool;
            var ch = OutsiderTalkMath.Choose(all, facts, use, maxLines, toneOnly, pair?.said, lately, topics, () => Rand.Value, LineOk);
            if (ch == null && toneOnly) ch = OutsiderTalkMath.Choose(all, facts, use, Mathf.Min(maxLines, 3), false, pair?.said, lately, topics, () => Rand.Value, LineOk);
            if (ch == null && use != UseChat) ch = OutsiderTalkMath.Choose(all, facts, UseChat, maxLines, false, pair?.said, lately, topics, () => Rand.Value, LineOk);
            return ch;
        }

        /// <summary>A piece was said between the two: kept on their record (said, its subject, its mark), and its news heard.</summary>
        public static void Told(Pawn c, Pawn x, TalkChoice ch)
        {
            if (ch?.piece == null || c == null || x == null) return;
            var pair = Book?.Pair(c, x, true);
            if (pair != null)
            {
                pair.talks++;
                OutsiderTalkMath.Remember(pair.said, ch.piece.name, OutsiderTalkMath.MaxSaid);
                if (!ch.piece.topic.NullOrEmpty()) OutsiderTalkMath.Remember(pair.topics, ch.piece.topic, 3);
                // A callback picked an earlier talk up: settled if it changed since, else brought up to date.
                if (ch.mark != null) OutsiderTalkMath.PickedUp(pair.marks, ch.mark, Now);
                if (ch.piece.mark != null) OutsiderTalkMath.AddMark(pair.marks, ch.piece.mark, Now, ch.markSubject, ch.markFact);
            }
            OutsiderTalkMath.Remember(lately, ch.piece.name, 30);
            if (!ch.heard.NullOrEmpty()) Heard(x, c, ch.heard);
        }

        // ================= Heard lately =================

        /// <summary>News of their town told to a colonist: into the town's "Heard lately", the same news brought up to date.</summary>
        public static void Heard(Pawn teller, Pawn told, string text)
        {
            var book = Book;
            if (book == null || teller == null || text.NullOrEmpty()) return;
            var town = Outsiders.TownOf(teller);
            var f = Outsiders.PeopleOf(teller);
            if (town == null && f == null) return;
            int now = Now;
            int id = town?.ID ?? -1;
            var same = book.heard.FirstOrDefault(h => h.town == id && h.faction == f && h.text == text && now - h.tick < OutsiderTalkMath.HeardSameDays * GenDate.TicksPerDay);
            if (same != null) book.heard.Remove(same);
            book.heard.Add(new HeardNews
            {
                town = id, townName = town?.Name ?? f?.Name, faction = f, text = text, teller = teller.LabelShort, told = told?.LabelShort, tick = now
            });
            while (book.heard.Count > OutsiderTalkMath.HeardMax) book.heard.RemoveAt(0);
            NotableBook.Changed();
        }

        /// <summary>A town's news, newest first.</summary>
        public static List<HeardNews> HeardAt(int town, int max) =>
            Book?.heard.Where(h => h != null && h.town == town).OrderByDescending(h => h.tick).Take(max).ToList() ?? new List<HeardNews>();

        // ================= Talks they start themselves =================

        /// <summary>
        /// Every 250 ticks with outsiders on the map being looked at: a colonist free and near one may start a talk with
        /// them by themselves (one at most a look). Never with danger about; the visit's own pace and the colony's other
        /// exchanges hold it back.
        /// </summary>
        public static void Rare(int now)
        {
            if (!ChatterOn || OutsiderTalkDef.All.Count == 0) return;
            var map = Find.CurrentMap;
            if (map == null || !map.IsPlayerHome || map.dangerWatcher.DangerRating != StoryDanger.None) return;
            List<Pawn> outsiders = null;
            foreach (var p in map.mapPawns.AllPawnsSpawned)
                if (p != null && p.Faction != Faction.OfPlayer && Outsiders.IsOutsider(p) && FreeOutsider(p))
                    (outsiders ??= new List<Pawn>()).Add(p);
            if (outsiders == null) return;
            var colonists = map.mapPawns.FreeColonistsSpawned.Where(FreeColonist).ToList();
            if (colonists.Count == 0) return;
            outsiders.Shuffle();
            foreach (var x in outsiders)
            {
                var v = VisitOf(x, false);
                if (v != null && (v.chatters >= OutsiderTalkMath.ChatterPerVisit || Left(x) <= 0)) continue;
                foreach (var c in colonists)
                {
                    if (!c.Position.InHorDistOf(x.Position, OutsiderTalkMath.ChatterRange) || !Together.Can(c, x)) continue;
                    var pair = Book?.Pair(c, x, false);
                    if (!OutsiderTalkMath.ChatterWaited(v?.chatterLast ?? -1, pair?.chatterLast ?? -1, now)) continue;
                    if (started.TryGetValue(c.thingIDNumber, out int t) && t <= now && now - t < GenDate.TicksPerHour) continue;
                    if (Busy(c) || Busy(x) || !SocialPace.Ready(c, x)) continue;
                    bool lingering = Lingering(c) && Lingering(x);
                    if (!Rand.Chance(OutsiderTalkMath.ChatterChance(Input(c, x, lingering)))) continue;
                    if (Start(c, x, lingering)) return;
                }
            }
        }

        private static OutsiderTalkMath.ChatterInput Input(Pawn c, Pawn x, bool lingering)
        {
            var social = c.skills?.GetSkill(SkillDefOf.Social);
            bool Has(Pawn p, TraitDef t) => t != null && (p.story?.traits?.HasTrait(t) ?? false);
            bool Named(Pawn p, string n) => p.story?.traits?.allTraits?.Any(tr => tr?.def?.defName == n || tr?.def?.defName != null && tr.def.defName.EndsWith("_" + n)) ?? false;
            float warmth = 0f;
            try
            {
                if (Persona.On) warmth = Persona.Values(c)[(int)PersonaAxis.Warmth];
            }
            catch
            {
                warmth = 0f;
            }
            return new OutsiderTalkMath.ChatterInput
            {
                social = social?.Level ?? 0, passion = social == null ? 0 : (int)social.passion, warmth = warmth, kind = Has(c, TraitDefOf.Kind), abrasive = Has(c, TraitDefOf.Abrasive),
                psychopath = Named(c, "Psychopath"), shy = Named(c, "Shy") || Named(c, "Recluse") || Named(c, "Loner") || Named(c, "Introvert"), lingering = lingering,
                theyKind = Has(x, TraitDefOf.Kind), theyAbrasive = Has(x, TraitDefOf.Abrasive), ofColonist = Outsiders.OfColonist(x, c), frequency = ChatterFrequency
            };
        }

        /// <summary>Staying where they are a while: not walking anywhere (at leisure, idle, eating, standing about).</summary>
        private static bool Lingering(Pawn p) => p.pather == null || !p.pather.Moving;

        /// <summary>A colonist free for a word: grown, up and about, able to talk, doing nothing that matters more (eating at a table is fine).</summary>
        private static bool FreeColonist(Pawn c)
        {
            if (c == null || c.DevelopmentalStage.Juvenile() || !Together.Able(c) || c.IsQuestLodger()) return false;
            if (!(c.health?.capacities?.CapableOf(PawnCapacityDefOf.Talking) ?? false)) return false;
            if (Busy(c)) return false;
            var job = c.CurJob;
            if (job == null) return true;
            var d = job.def;
            if (d == OutsiderDefOf.Council_OutsiderTalk || d == OutsiderDefOf.Council_OutsiderGift || d == OutsiderDefOf.Council_OutsiderLesson
                || d == OutsiderDefOf.Council_OutsiderTeach || d == OutsiderDefOf.Council_OutsiderBout || d == OutsiderDefOf.Council_OutsiderMeal)
                return false;
            if (d == JobDefOf.Ingest) return !job.playerForced;
            return PlayOuts.Interruptible(c);
        }

        /// <summary>An outsider free for a word: grown, up, not leaving, not trading, not busy with their own (eating is fine).</summary>
        private static bool FreeOutsider(Pawn x)
        {
            if (x.DevelopmentalStage.Juvenile() || !Together.Able(x) || Busy(x)) return false;
            if (!(x.health?.capacities?.CapableOf(PawnCapacityDefOf.Talking) ?? false)) return false;
            return Outsiders.Busy(x, null, OutsiderAct.Meal) == null && x.CurJobDef != OutsiderDefOf.Council_OutsiderListen;
        }

        /// <summary>A talk the colonist starts by themselves: how it goes, a piece for it, the scene; what it leaves once it's said.</summary>
        private static bool Start(Pawn c, Pawn x, bool lingering)
        {
            var tone = OutsiderMath.Tone(Outsiders.Input(c, x, OutsiderAct.Chat), Rand.Value);
            int max = Mathf.Min(OutsiderTalkMath.ChatterMaxLines(lingering), OutsiderTalkMath.MaxLines(tone));
            var ch = Choose(c, x, UseChat, tone, Mathf.Max(OutsiderTalkMath.MinTurns, max));
            if (ch == null) return false;
            int ticks = Play(c, x, ch.lines, () => Chatted(c, x, ch, tone));
            if (ticks < 0) return false;
            int now = Now;
            var v = VisitOf(x, true);
            if (v != null)
            {
                v.chatterLast = now;
                v.chatters++;
            }
            var pair = Book?.Pair(c, x, true);
            if (pair != null) pair.chatterLast = now;
            started[c.thingIDNumber] = now;
            if (started.Count > 300) started.Clear();
            SocialPace.Started(c);
            return true;
        }

        /// <summary>A talk of their own is over, every line said with the two together: what it leaves, as a chat does, at half.</summary>
        private static void Chatted(Pawn c, Pawn x, TalkChoice ch, TalkTone tone)
        {
            if (c == null || x == null || c.Dead || x.Dead) return;
            // together: the scene's every line was said with the two side by side (Step checks each one).
            Outsiders.Remember(c, x, OutsiderAct.Chat, tone == TalkTone.Warm || tone == TalkTone.Sour ? Outsiders.Note(OutsiderAct.Chat, c, Outsiders.ToneWord(tone)) : null,
                OutsiderMath.RegardShift(tone) * 0.5f, OutsiderMath.WarmthShift(tone) * 0.5f, false, 0.5f);
            if (tone == TalkTone.Warm || tone == TalkTone.Sour)
            {
                int stage = OutsiderMath.ToneStage(tone);
                CouncilGameComponent.GiveThought(c, OutsiderDefOf.Rimfolk_OutsiderTalk, x, -1f, null, stage);
                CouncilGameComponent.GiveThought(x, OutsiderDefOf.Rimfolk_OutsiderTalk, c, -1f, null, stage);
            }
            Told(c, x, ch);
        }

        // ================= The hour =================

        /// <summary>Visits let go once the outsider is gone; old news let go; the world's wars looked over once a day; battles on the colony's land noted.</summary>
        public static void Hourly(OutsiderBook book, int now)
        {
            book.visits.RemoveAll(v => v.outsider == null || v.outsider.Dead || v.outsider.Discarded
                                       || !v.outsider.Spawned && now - Mathf.Max(v.start, Mathf.Max(v.last, v.chatterLast)) > GenDate.TicksPerDay * 3);
            book.heard.RemoveAll(h => now - h.tick > OutsiderTalkMath.HeardDays * GenDate.TicksPerDay || h.tick > now);
            if (book.warsLooked < 0 || now - book.warsLooked >= GenDate.TicksPerDay || now < book.warsLooked) LookWars(book, now);
            foreach (var map in Find.Maps)
                if (map.IsPlayerHome)
                    Battles(book, map, now);
        }

        /// <summary>
        /// Which peoples are at war with which, by the game's own relations (whatever set them: the game, Rimfolk's world
        /// events, a mod's wars): a war newly hostile is noted with each side's towns then, one no longer hostile is over.
        /// The first look notes those already on as "since before".
        /// </summary>
        public static void LookWars(OutsiderBook book, int now)
        {
            bool first = book.warsLooked < 0;
            book.warsLooked = now;
            var peoples = Find.FactionManager.AllFactionsListForReading.Where(OutsiderFacts.Counts).ToList();
            foreach (var w in book.wars)
                if (w.end < 0 && (!OutsiderFacts.Counts(w.a) || !OutsiderFacts.Counts(w.b) || !w.a.HostileTo(w.b)))
                    w.end = now;
            for (int i = 0; i < peoples.Count; i++)
            for (int j = i + 1; j < peoples.Count; j++)
            {
                var a = peoples[i];
                var b = peoples[j];
                if (!a.HostileTo(b) || book.wars.Any(w => w.end < 0 && w.Of(a) && w.Of(b))) continue;
                book.wars.Add(new WarSeen { a = a, b = b, start = first ? -1 : now, townsA = OutsiderFacts.Towns(a), townsB = OutsiderFacts.Towns(b) });
            }
            book.wars.RemoveAll(w => w.a == null || w.b == null || w.end >= 0 && now - w.end > GenDate.TicksPerDay * 60);
            while (book.wars.Count > OutsiderBook.MaxWars)
            {
                var drop = book.wars.Where(w => w.end >= 0).OrderBy(w => w.end).FirstOrDefault() ?? book.wars.OrderBy(w => w.start).First();
                book.wars.Remove(drop);
            }
        }

        /// <summary>Fighters of two peoples at war with each other on the colony's land now (a battle passing through, a mod's war): noted.</summary>
        private static void Battles(OutsiderBook book, Map map, int now)
        {
            Dictionary<Faction, int> here = null;
            foreach (var p in map.mapPawns.AllPawnsSpawned)
            {
                var f = p?.Faction;
                if (f == null || f.IsPlayer || p.Downed || p.RaceProps == null || !p.RaceProps.Humanlike || !OutsiderFacts.Counts(f)) continue;
                here ??= new Dictionary<Faction, int>();
                here.TryGetValue(f, out int n);
                here[f] = n + 1;
            }
            if (here == null || here.Count < 2) return;
            var fs = here.Keys.ToList();
            for (int i = 0; i < fs.Count; i++)
            for (int j = i + 1; j < fs.Count; j++)
            {
                if (!fs[i].HostileTo(fs[j])) continue;
                var w = book.wars.FirstOrDefault(x => x.end < 0 && x.Of(fs[i]) && x.Of(fs[j]));
                if (w == null)
                {
                    w = new WarSeen { a = fs[i], b = fs[j], start = book.warsLooked < 0 ? -1 : now, townsA = OutsiderFacts.Towns(fs[i]), townsB = OutsiderFacts.Towns(fs[j]) };
                    book.wars.Add(w);
                }
                w.fought = now;
            }
        }
    }
}
