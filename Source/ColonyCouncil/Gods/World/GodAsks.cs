using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>One answer to a question of the gods in the world: its words, what it does, and why it can't be chosen now (null: it can).</summary>
    public sealed class GodAnswer
    {
        public string label;
        public Action<ChoiceLetter_GodWorld> act;
        public string disabled;

        public GodAnswer(string label, Action<ChoiceLetter_GodWorld> act, string disabled = null)
        {
            this.label = label;
            this.act = act;
            this.disabled = disabled;
        }
    }

    /// <summary>
    /// The questions the gods' doings put to the colony in the wider world (Gods/World/), each through Tell.Ask as one kind
    /// of letter (<see cref="ChoiceLetter_GodWorld"/>), never the dilemmas. A god never asks in its own name: a person
    /// does, and the player answers what that person asked (a neighbour's invitation to their feast, a people's holy war, a
    /// temple's envoy, a dream told at breakfast, a second dream asking for a gift back, one hunted asking to be hidden, a
    /// voice heard by someone desperate). Each kind's answers come from its own part; once what it asked about has moved
    /// on, only closing is left. Asks that can wait come only when one of Rimfolk's questions may (Tell.CanAsk); the
    /// Hunger's and the second dream come when they come.
    /// </summary>
    public static class GodAsks
    {
        public const string Invite = "invite", HolyWar = "holywar", Envoy = "envoy", Gift = "gift", Undo = "undo", Hide = "hide", Hunger = "hunger";

        /// <summary>Each kind's answers, or none when its question no longer stands.</summary>
        private static readonly Dictionary<string, Func<ChoiceLetter_GodWorld, IEnumerable<GodAnswer>>> Kinds = new Dictionary<string, Func<ChoiceLetter_GodWorld, IEnumerable<GodAnswer>>>
        {
            [Invite] = GodLetters.InviteAnswers,
            [HolyWar] = GodPeoples.HolyWarAnswers,
            [Envoy] = GodPeoples.EnvoyAnswers,
            [Gift] = GodGifts.OfferAnswers,
            [Undo] = GodGifts.UndoAnswers,
            [Hide] = GodDrama.HideAnswers,
            [Hunger] = GodVoid.BargainAnswers
        };

        public static IEnumerable<GodAnswer> AnswersOf(ChoiceLetter_GodWorld l)
        {
            if (l?.ask == null || !Kinds.TryGetValue(l.ask, out var f)) return Enumerable.Empty<GodAnswer>();
            try
            {
                return f(l)?.Where(a => a != null).ToList() ?? new List<GodAnswer>();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods in the world: a question's answers (" + l.ask + ")", e);
                return Enumerable.Empty<GodAnswer>();
            }
        }

        public static bool Known(string ask) => ask != null && Kinds.ContainsKey(ask);

        /// <summary>
        /// A question as a letter: its kind, the god behind it (hidden; and a second: a rival), the people and the colonist it
        /// is about, a number and a def name, what is under way it answers (a doing's id), at a home; it waits
        /// <paramref name="days"/> (0: until put away). Null when it couldn't be made.
        /// </summary>
        public static ChoiceLetter_GodWorld Ask(string ask, Map home, God god, string label, string text, LookTargets look = null, God other = null, Faction faction = null,
            Pawn pawn = null, int count = 0, string what = null, int doing = -1, float days = 2f)
        {
            var def = GodWorldDefOf.Rimfolk_GodWorldAsk;
            if (def == null || home == null || text.NullOrEmpty()) return null;
            try
            {
                var letter = LetterMaker.MakeLetter(label, text, def, look ?? LookTargets.Invalid, faction) as ChoiceLetter_GodWorld;
                if (letter == null) return null;
                letter.ask = ask;
                letter.home = Homes.KeyOf(home);
                letter.god = god?.id ?? -1;
                letter.other = other?.id ?? -1;
                letter.people = faction;
                letter.pawn = pawn;
                letter.count = count;
                letter.what = what;
                letter.doing = doing;
                letter.at = GodWorld.Now;
                if (days > 0f) letter.StartTimeout(Mathf.RoundToInt(days * GenDate.TicksPerDay));
                Tell.Ask(letter, home);
                return letter;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods in the world: a question asked (" + ask + ")", e);
                return null;
            }
        }
    }

    /// <summary>
    /// A question of the gods in the world, asked by a person (see <see cref="GodAsks"/>): which kind, the god behind it and
    /// a second, the people and the colonist it is about, a number and a def name, the doing it answers, at which home, asked
    /// when. Its answers are its kind's; once what it asked about has moved on, only closing is left. Written in the answers'
    /// log.
    /// </summary>
    public class ChoiceLetter_GodWorld : RimfolkLetter
    {
        public string ask;
        public int home = HomesMath.None;
        public int god = -1, other = -1;
        public Faction people;
        public Pawn pawn;
        public int count;
        public string what;
        public int doing = -1;
        public int at = -1;
        public bool answered;

        public Map HomeMap => Homes.MapOf(home) ?? Homes.Seat;

        public God God => Gods.ById(god);

        public God Other => Gods.ById(other);

        public GodDoing Doing => doing < 0 ? null : GodWorld.Book?.doings.FirstOrDefault(d => d != null && d.id == doing && !d.done);

        /// <summary>Still waits for the colony's answer (This week's Needs you counts it).</summary>
        public bool Waiting => !answered && !ArchivedOnly && GodAsks.AnswersOf(this).Any();

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                if (ArchivedOnly || answered)
                {
                    yield return Option_Close;
                    yield break;
                }
                var answers = GodAsks.AnswersOf(this).ToList();
                if (answers.Count == 0)
                {
                    yield return Option_Close;
                    yield break;
                }
                foreach (var a in answers)
                {
                    var answer = a;
                    var o = new DiaOption(answer.label)
                    {
                        action = () =>
                        {
                            answered = true;
                            Find.LetterStack.RemoveLetter(this);
                            try
                            {
                                answer.act?.Invoke(this);
                            }
                            catch (Exception e)
                            {
                                CouncilLog.ErrorOnce("gods in the world: an answer (" + ask + ")", e);
                            }
                        },
                        resolveTree = true
                    };
                    if (answer.disabled != null) o.Disable(answer.disabled);
                    yield return Answers.Recorded(o, this, "gods", people, pawn, () => HomeMap);
                }
                yield return Option_Postpone;
            }
        }

        public override void Describe(EventCardFacts facts)
        {
            if (facts == null) return;
            facts.Person(pawn);
            if (people != null) facts.faction = people;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ask, "ask");
            Scribe_Values.Look(ref home, "home", HomesMath.None);
            Scribe_Values.Look(ref god, "god", -1);
            Scribe_Values.Look(ref other, "other", -1);
            Scribe_References.Look(ref people, "people");
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref count, "count");
            Scribe_Values.Look(ref what, "what");
            Scribe_Values.Look(ref doing, "doing", -1);
            Scribe_Values.Look(ref at, "at", -1);
            Scribe_Values.Look(ref answered, "answered");
        }
    }

    [DefOf]
    public static class GodWorldDefOf
    {
        public static LetterDef Rimfolk_GodWorldAsk;

        static GodWorldDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(GodWorldDefOf));
        }
    }
}
