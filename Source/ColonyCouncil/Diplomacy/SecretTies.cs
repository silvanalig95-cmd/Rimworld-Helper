using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Secret ties in use (docs/design/stage2-diplomacy.md, "Shared rules" and items 8 and 11; release 2c.1): who knows
    /// what about whom, a world ledger tie of the kind Secret from whoever knows it to the one it is about (the colony's
    /// keeper learns them: from the colony to a notable of theirs; the key is what it is, the number how much it weighs,
    /// <c>about</c> the colonist who learned it), twenty live at most and each for a year. Leverage that can be called in:
    /// pressed by letter or face to face, the notable gives in (kept: they send silver to keep it quiet) or refuses (broken:
    /// the colony tells it, their regard falls hard and their people remember it as the colony's scheme). Ended when the
    /// notable is gone or the year is out.
    /// <para>Release 2c.3's schemes use the same ties both ways: <see cref="Learn"/> with any knower (their people's spy
    /// about a colonist), <see cref="Of"/> and <see cref="About(Ref)"/> to read them, and calling in through the ledger's
    /// own states.</para>
    /// </summary>
    public static class SecretTies
    {
        private static int Now => Find.TickManager?.TicksGame ?? 0;

        // ================= Reading =================

        /// <summary>The ledger's live Secret ties (any knower), oldest first.</summary>
        public static List<Tie> Live() => WorldLedger.LiveTies(Tie.Kind.Secret);

        /// <summary>The colony's own (learned by its keeper), live, oldest first.</summary>
        public static List<Tie> Ours() => Live().Where(t => t.origin == KeeperMath.Origin && t.from.kind == RefKind.Colony).ToList();

        /// <summary>Every Secret tie about this party (live and closed).</summary>
        public static List<Tie> About(Ref r) => WorldLedger.OwnTies(Tie.Kind.Secret).Where(t => t.to == r).ToList();

        public static List<Tie> About(Notable n) => n == null ? new List<Tie>() : About(n.Person);

        /// <summary>Every Secret tie this party knows (live and closed).</summary>
        public static List<Tie> Of(Ref knower) => WorldLedger.OwnTies(Tie.Kind.Secret).Where(t => t.from == knower).ToList();

        public static Notable NotableOf(Tie t)
        {
            var book = NotableBook.Instance;
            if (t == null || book == null) return null;
            return book.notables.FirstOrDefault(n => n != null && n.Person == t.to);
        }

        public static Pawn Learner(Tie t) => t == null || t.terms.about.IsNone ? null : WorldLedger.PawnOf(t.terms.about);

        public static int Weight(Tie t) => Math.Max(1, (int)(t?.terms?.number ?? 1f));

        /// <summary>What it is, in words: "owes more than the town can pay back".</summary>
        public static string What(Tie t) => t == null ? "?" : TaskWords.Secret(t.terms.key);

        // ================= Learning one =================

        /// <summary>A secret about a notable, learned by the colony (or, for release 2c.3, by anyone): the tie, or null with no room.</summary>
        public static Tie Learn(Ref knower, Notable about, string kind, Pawn by) => about == null ? null : Learn(knower, about.Person, kind, by);

        public static Tie Learn(Ref knower, Ref about, string kind, Pawn by)
        {
            if (knower.IsNone || about.IsNone || !KeeperMath.IsKind(kind)) return null;
            try
            {
                // Twenty live at most: the oldest let go (it no longer matters as much as a fresh one).
                var live = Live();
                int drop = KeeperMath.ToLetGo(live.Count);
                for (int i = 0; i < drop && i < live.Count; i++) WorldLedger.Set(live[i], Tie.State.Ended);
                var terms = new TieTerms { key = kind, number = KeeperMath.Weight(kind), about = by != null ? WorldLedger.PersonOf(by) : Ref.None };
                var t = WorldLedger.AddTie(Tie.Kind.Secret, knower, about, terms, Now + KeeperMath.Days * KeeperMath.DayTicks, KeeperMath.Origin);
                Diplomacy.Dirty();
                return t;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("secret ties: learning one", e);
                return null;
            }
        }

        // ================= Pressing it =================

        /// <summary>Why it can't be pressed now, or null.</summary>
        public static string PressWhyNot(Tie t)
        {
            if (!KeeperOfSecrets.On) return TaskWords.P("CC_Task_Why_Off");
            if (t == null || t.state != Tie.State.Open) return TaskWords.P("CC_Keeper_Why_Pressed");
            var n = NotableOf(t);
            if (n == null || !n.Alive) return TaskWords.P("CC_Task_Why_Gone");
            if (Diplomacy.Hostile(n.faction)) return TaskWords.P("CC_Task_Why_War", TaskWords.People(n.faction).Named("PEOPLE"));
            return null;
        }

        /// <summary>The council's question first: what is asked, and what a refusal brings (the secret told, their lasting anger).</summary>
        public static string PressAsk(Tie t, Pawn writer)
        {
            var n = NotableOf(t);
            return TaskWords.P("CC_Keeper_Press_Ask", (n?.Name ?? "?").Named("NAME"), What(t).Named("SECRET"), KeeperMath.Hush(Weight(t)).Named("SILVER"),
                (writer?.LabelShort ?? "?").Named("PAWN"), TaskWords.People(n?.faction).Named("PEOPLE"));
        }

        /// <summary>Pressed by letter (the card's act): the keeper writes (else the colony's usual writer); the tie is called in until their answer.</summary>
        public static void PressByLetter(Tie t, Map home)
        {
            if (PressWhyNot(t) != null || home == null) return;
            var n = NotableOf(t);
            var writer = KeeperOfSecrets.At(home);
            if (writer == null || writer.MapHeld != home) writer = NeighbourLetters.DefaultWriter(home);
            if (writer == null || n == null) return;
            var w = writer;
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(PressAsk(t, w), () =>
            {
                if (PressWhyNot(t) != null) return;
                var l = TaskLetters.Send(n, w, TaskLetters.PressKey(t), TaskWords.P("CC_Keeper_Press_Topic"));
                if (l == null)
                {
                    // apart: the answer to the player's choice, told in a message.
                    Tell.Message(NewsKind.World, Importance.Notable, home, NeighbourLetters.WhyNot(n, LetterPurpose.Task) ?? TaskWords.P("CC_Keeper_Why_NoLetter"), LookTargets.Invalid,
                        MessageTypeDefOf.RejectInput);
                    return;
                }
                if (WorldLedger.Set(t, Tie.State.CalledIn))
                {
                    t.terms.count = l.id;
                    WorldLedger.Touch(t);
                }
                Diplomacy.Dirty();
            }));
        }

        public static string PressByLetterWhyNot(Tie t, Map home)
        {
            string why = PressWhyNot(t);
            if (why != null) return why;
            var n = NotableOf(t);
            if (!NeighbourLetters.On) return "CC_Letter_Why_Off".Translate().Resolve();
            if (n?.pawn != null && n.pawn.Spawned) return TaskWords.P("CC_Keeper_Why_Here", n.Name.Named("NAME"));
            return null;
        }

        /// <summary>
        /// Their answer to a letter that pressed it (TaskLetters.Read): given in, the silver comes with their answer; refused,
        /// the colony tells it. No longer pressed by the time they read it (answered face to face, or let go): curt, and
        /// <paramref name="moot"/> so their words are picked as a moot answer's (no silver promised).
        /// </summary>
        internal static string PressRead(int id, Notable n, Map home, ref AnswerTone tone, List<Thing> things, out bool moot)
        {
            moot = false;
            var t = WorldLedger.Get(id);
            if (t == null || t.kind != Tie.Kind.Secret || t.state != Tie.State.CalledIn)
            {
                moot = true;
                tone = AnswerTone.Curt;
                return TaskWords.T("CC_Keeper_Ans_Moot", n.Name.Named("NAME"));
            }
            var answer = Answer(t, n, home, false, out string line);
            if (answer == PressAnswer.Complied)
            {
                things.AddRange(Couriers.Make(ThingDefOf.Silver, KeeperMath.Hush(Weight(t))));
                tone = AnswerTone.Curt;
            }
            else tone = AnswerTone.Refusal;
            return line;
        }

        /// <summary>Pressed face to face (the keeper's "Sound out"): their answer at once; given in, they hand the silver over themselves.</summary>
        public static void PressFace(Tie t, Pawn keeper, Pawn x, Map home)
        {
            if (t == null || x == null || PressWhyNot(t) != null) return;
            var n = NotableOf(t);
            if (n == null) return;
            WorldLedger.Set(t, Tie.State.CalledIn);
            var answer = Answer(t, n, home, true, out string line);
            // together: asked and answered face to face, at the end of the talk that brought them together.
            Outsiders.Say(x, answer == PressAnswer.Complied ? "pressyes" : "pressno", keeper);
            if (answer == PressAnswer.Complied && home != null) Couriers.PaySilver(home, x.Position, KeeperMath.Hush(Weight(t)), n.faction, x);
            Tell.Message(NewsKind.World, Importance.Important, home, line, new LookTargets(x), answer == PressAnswer.Complied ? MessageTypeDefOf.NeutralEvent : MessageTypeDefOf.NegativeEvent);
        }

        /// <summary>
        /// Their answer, by how much it weighs, their regard and their nature: given in (kept), or refused (broken) and the
        /// colony tells it; either way they think less of the colony, and remember being pressed, and how: by letter, or face
        /// to face (<paramref name="face"/>), for their talk (them:pressed, and them:pressed:letter or them:pressed:face).
        /// </summary>
        private static PressAnswer Answer(Tie t, Notable n, Map home, bool face, out string line)
        {
            int opinion = NotableBook.Opinion(n);
            var answer = KeeperMath.Answer(Weight(t), opinion, NotableBook.Warmth(n.pawn), Rand.Value);
            string what = What(t);
            n.personal += KeeperMath.PressedRegard;
            foreach (var key in KeeperMath.PressedKeys(face))
                if (!n.done.Contains(key))
                    n.done.Add(key);
            while (n.done.Count > 12) n.done.RemoveAt(0);
            var learner = Learner(t);
            var actors = new List<HistoryActor> { new HistoryActor(t.from, HistoryRole.Did), new HistoryActor(n.Person, HistoryRole.Against) };
            // The one who learned it, as its line names them: a secret told is their house's wrong (Diplomacy/Renown.cs; paid, nobody hears of it).
            if (!t.terms.about.IsNone) actors.Add(new HistoryActor(t.terms.about, HistoryRole.Did));
            if (answer == PressAnswer.Complied)
            {
                WorldLedger.Set(t, Tie.State.Kept);
                n.Remember(TaskWords.P("CC_Keeper_Note_Pressed"), KeeperMath.PressedRegard);
                line = TaskWords.T("CC_Keeper_Press_Yes", n.Name.Named("NAME"), what.Named("SECRET"), KeeperMath.Hush(Weight(t)).Named("SILVER"));
            }
            else
            {
                WorldLedger.Set(t, Tie.State.Broken);
                n.personal += KeeperMath.ToldRegard;
                n.Remember(TaskWords.P("CC_Keeper_Note_Told"), KeeperMath.PressedRegard + KeeperMath.ToldRegard);
                if (n.faction != null) FactionMemory.Note(n.faction, KeeperMath.ToldDeed, n.Name);
                line = TaskWords.T("CC_Keeper_Press_No", n.Name.Named("NAME"), what.Named("SECRET"), TaskWords.People(n.faction).Named("PEOPLE"));
            }
            NotableBook.Changed();
            Diplomacy.Write(TaskMath.HistoryKind, actors, n.Person, TaskWords.P(answer == PressAnswer.Complied ? "CC_Keeper_Hist_Kept" : "CC_Keeper_Hist_Told",
                n.Name.Named("NAME"), (learner?.LabelShort ?? "?").Named("PAWN")), answer == PressAnswer.Refused);
            Diplomacy.Dirty();
            return answer;
        }

        // ================= The day =================

        /// <summary>Once a day: a secret about someone gone no longer matters (ended); one pressed whose letter never went is open again.</summary>
        public static void Daily(int now)
        {
            foreach (var t in Live())
            {
                try
                {
                    if (t.origin != KeeperMath.Origin) continue;
                    var n = NotableOf(t);
                    if (n == null || !n.Alive)
                    {
                        WorldLedger.Set(t, Tie.State.Ended);
                        continue;
                    }
                    if (t.state == Tie.State.CalledIn && t.terms.count > 0)
                    {
                        var l = NeighbourLetters.Get(t.terms.count);
                        if (l == null || l.stage == LetterStage.Cancelled)
                        {
                            WorldLedger.Set(t, Tie.State.Open);
                            t.terms.count = 0;
                            // Called in, it lost its end: half a year more, as a fresh one would have.
                            t.until = now + KeeperMath.Days * KeeperMath.DayTicks / 2;
                            WorldLedger.Touch(t);
                        }
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("secret ties: the day", e);
                }
            }
        }
    }
}
