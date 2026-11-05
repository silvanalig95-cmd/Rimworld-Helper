using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The council's questions about someone who keeps causing trouble (Social/Banishment.cs): A the one it's about, B who
    /// speaks for the council (the speaker, the faith's leader, another of the council, the eldest). {WORD} the age's word
    /// for banishment, {BANISH} the answer that does it, {TITLE} what B is, {FIGHTS} how many fights, {WITH} with whom,
    /// {VOICES} the council's voices, an objection and their own say (the second question only).
    /// </summary>
    public class BanishContext : DilemmaContext
    {
        public string word, verb, title, fights, with, voices, step;
        public int started, insults;
        public bool toDie, guilty;

        protected override void AddArgs(List<NamedArgument> args)
        {
            base.AddArgs(args);
            args.Add((word ?? "").Named("WORD"));
            args.Add((verb ?? "").Named("BANISH"));
            args.Add((title ?? "").Named("TITLE"));
            args.Add((fights ?? "").Named("FIGHTS"));
            args.Add((with ?? "").Named("WITH"));
            args.Add((voices ?? "").Named("VOICES"));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref word, "word");
            Scribe_Values.Look(ref verb, "verb");
            Scribe_Values.Look(ref title, "title");
            Scribe_Values.Look(ref fights, "fights");
            Scribe_Values.Look(ref with, "with");
            Scribe_Values.Look(ref voices, "voices");
            Scribe_Values.Look(ref step, "step");
            Scribe_Values.Look(ref started, "started");
            Scribe_Values.Look(ref insults, "insults");
            Scribe_Values.Look(ref toDie, "toDie");
            Scribe_Values.Look(ref guilty, "guilty");
        }
    }

    /// <summary>
    /// Rimfolk_Dilemma_Trouble, the first question: someone in the window's trouble whom most of the colony dislikes, never
    /// asked about before (or not for a while). Tags: started (they threw a first punch) or joined (never), insults, the
    /// speaker's kind (speaker, leader, elder).
    /// </summary>
    public class DilemmaWorker_Trouble : DilemmaWorker
    {
        protected virtual BanishMath.Ask Ask => BanishMath.Ask.Word;

        public override bool CanHappenNow(Map map) => BanishTracker.On && base.CanHappenNow(map);

        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            var t = BanishTracker.Instance;
            if (t == null || map == null || !CanHappenNow(map)) return false;
            var a = t.Pick(map, Ask, out var trouble);
            if (a == null) return false;
            var b = BanishTracker.Teller(map, a, out string title);
            if (b == null) return false;
            var colony = BanishTracker.ColonyAt(map);
            var r = t.Of(a);
            int now = Find.TickManager.TicksGame;
            int from = Ask == BanishMath.Ask.Banish && r != null && r.stepTick >= 0 ? r.stepTick : BanishMath.WindowFrom(now);
            var c = new BanishContext
            {
                def = def, map = map, a = a, b = b, title = title, word = BanishTracker.AgeWord(), verb = BanishTracker.AgeVerb(a),
                fights = Count(trouble.fights), with = With(t.FoughtWith(a, colony, from)), step = r?.step,
                started = trouble.started, insults = trouble.insults, toDie = PawnBanishUtility.WouldBeLeftToDie(a, a.Tile), guilty = a.guilt?.IsGuilty ?? false
            };
            if (Ask == BanishMath.Ask.Banish) c.voices = Voices(c, colony, trouble);
            t.Asked(a);
            ctx = c;
            return true;
        }

        /// <summary>Who they fought, by name, or "others of the colony" when none of them is one of the colony now.</summary>
        private static string With(List<Pawn> who) => who.Count > 0 ? BanishTracker.Names(who) : "CC_Banish_WithOthers".Translate().Resolve();

        /// <summary>"two fights", "three fights", "several fights".</summary>
        private static string Count(int n) => (n >= 1 && n <= 6 ? "CC_Banish_Fights_" + n : "CC_Banish_Fights_Many").Translate().Resolve();

        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            if (!(ctx is BanishContext c)) return;
            tags.Add(c.started > 0 ? "started" : "joined");
            if (c.insults > 0) tags.Add("insults");
            if (!c.step.NullOrEmpty()) tags.Add(c.step == "apart" ? "apart" : "warned");
            if (c.toDie) tags.Add("todie");
            if (c.guilty) tags.Add("guilty");
            if (c.b != null)
            {
                var offices = ColonyCouncilMod.Settings.offices ? OfficeTracker.For(c.map) : null;
                if (offices?.HolderOf("Council_Office_Speaker") == c.b) tags.Add("speaker");
                else if (BanishTracker.FaithLeader(out _) == c.b) tags.Add("leader");
                else if (offices?.OfficeOf(c.b) != null) tags.Add("council");
                else tags.Add("elder");
            }
        }

        /// <summary>
        /// The second question's voices: up to three of the council (the office holders and the faith's leader, else those
        /// who feel most strongly), each by their own opinion of A; a partner, kin or friend who stands by A, by name; and
        /// A's own say.
        /// </summary>
        private static string Voices(BanishContext c, List<Pawn> colony, BanishMath.Trouble trouble)
        {
            var a = c.a;
            var lines = new List<string>();
            var council = new List<Pawn>();
            var offices = ColonyCouncilMod.Settings.offices ? OfficeTracker.For(c.map) : null;
            if (offices != null)
                foreach (var o in OfficeTracker.Offices)
                    council.AddRange(offices.HoldersOf(o));
            var leader = BanishTracker.FaithLeader(out _);
            if (leader != null) council.Add(leader);
            council = council.Where(p => p != null && p != a && colony.Contains(p)).Distinct().ToList();
            if (council.Count == 0)
            {
                var grown = colony.Where(p => p != a && p.DevelopmentalStage.Adult()).ToList();
                council.AddRange(grown.OrderBy(p => BanishTracker.Opinion(p, a)).Take(1));
                council.AddRange(grown.OrderByDescending(p => BanishTracker.Opinion(p, a)).Take(1));
                council = council.Distinct().ToList();
            }
            // The speaker first, then the strongest feelings either way.
            council = council.OrderByDescending(p => p == c.b).ThenByDescending(p => System.Math.Abs(BanishTracker.Opinion(p, a))).Take(3).ToList();
            foreach (var m in council)
            {
                string stance = BanishMath.Stance(BanishTracker.Opinion(m, a), BanishTracker.Close(m, a));
                string line = BanishTracker.Line("Rimfolk_Pool_BanishVoice", m, a, stance);
                if (!line.NullOrEmpty()) lines.Add(BanishTracker.W("CC_Banish_Voice_" + stance, m.Named("PAWN"), a.Named("OTHER")) + " \"" + line + "\"");
            }
            var objector = colony.Where(p => p != a && !council.Contains(p) && BanishMath.StandsBy(BanishTracker.Opinion(p, a), BanishTracker.Close(p, a)))
                .OrderByDescending(p => BanishTracker.Close(p, a)).ThenByDescending(p => BanishTracker.Opinion(p, a)).FirstOrDefault();
            if (objector != null)
            {
                bool close = BanishTracker.Close(objector, a);
                string relation = close ? Family.Relation(a, objector) : "";
                if (relation.NullOrEmpty()) relation = (close ? "CC_Banish_Kin" : "CC_Banish_Friend").Translate().Resolve();
                string line = BanishTracker.Line("Rimfolk_Pool_BanishObject", objector, a, close ? "kin" : "friend");
                if (!line.NullOrEmpty())
                    lines.Add(BanishTracker.W("CC_Banish_Objects", objector.Named("PAWN"), a.Named("OTHER"), relation.Named("RELATION")) + " \"" + line + "\"");
            }
            string say = BanishTracker.Line("Rimfolk_Pool_BanishPlea", a, c.b, BanishMath.Say(trouble.started, trouble.joined, a.needs?.mood?.CurLevelPercentage ?? 0.5f));
            if (!say.NullOrEmpty()) lines.Add(BanishTracker.W("CC_Banish_OwnSay", a.Named("OTHER")) + " \"" + say + "\"");
            return string.Join("\n\n", lines);
        }
    }

    /// <summary>
    /// Rimfolk_Dilemma_Banish, the second question: after a word or their nights apart, the trouble went on. Tags as the
    /// first's, and warned (after a word) or apart, todie (they'd be sent out to die by the game's own check), guilty
    /// (found guilty at the faith's trial: the game gives no mood for it).
    /// </summary>
    public class DilemmaWorker_Banish : DilemmaWorker_Trouble
    {
        protected override BanishMath.Ask Ask => BanishMath.Ask.Banish;
    }

    // ===================== The answers =====================

    /// <summary>B has a word with A face to face, a warning the council remembers (<see cref="last"/>: the last one).</summary>
    public class DilemmaEffect_TroubleWord : DilemmaEffect
    {
        public bool last;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.a == null || ctx.b == null) return;
            sheet.Play("CC_Impact_BanishWord".Translate(ctx.b.LabelShort, ctx.a.LabelShort).Resolve(), "CC_Impact_BanishWordBrief".Translate(ctx.b.LabelShort, ctx.a.LabelShort).Resolve());
            sheet.Thought(new List<Pawn> { ctx.a }, ctx.a.LabelShort, BanishDefOf.Rimfolk_Banish_Warned, p => ctx.b);
            sheet.Add(ImpactKind.Other, -1, "CC_Impact_BanishWordRisk".Translate(ctx.a.LabelShort).Resolve(), "CC_Impact_BanishWordRiskBrief".Translate(ctx.a.LabelShort).Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            // together: the word is said when the two are face to face (BanishTracker.SayWord, through Together).
            if (ctx?.a == null) return;
            BanishTracker.Instance?.Word(ctx.a, ctx.b, last);
        }
    }

    /// <summary>What the council remembers of an answer that isn't a word: their nights apart ("apart"), or "let it be" ("letbe").</summary>
    public class DilemmaEffect_TroubleStep : DilemmaEffect
    {
        public string kind = "letbe";

        public override bool ChanceEach => false;

        /// <summary>Their nights apart need the answers to play out on the map (the timetable is changed there), and hours not already changed.</summary>
        public override string Unavailable(DilemmaContext ctx)
        {
            if (kind != "apart" || ctx?.a == null) return null;
            if (!PlayOuts.On || (AnswerShifts.Instance?.Shifted(ctx.a) ?? false) || ctx.a.timetable == null)
                return "CC_Banish_WhyNot_Hours".Translate(ctx.a.LabelShort).Resolve();
            return null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.a == null) return;
            if (kind == "apart")
                sheet.Add(ImpactKind.Other, -1, "CC_Impact_BanishApartStep".Translate(ctx.a.LabelShort).Resolve(), "CC_Impact_BanishApartStepBrief".Translate(ctx.a.LabelShort).Resolve());
            else sheet.Add(ImpactKind.Other, 0, "CC_Impact_BanishLetBe".Translate(ctx.a.LabelShort).Resolve(), "CC_Impact_BanishLetBeBrief".Translate(ctx.a.LabelShort).Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            // apart: what the council remembers of its answer; nothing happens between two people here.
            var t = BanishTracker.Instance;
            if (t == null || ctx?.a == null) return;
            if (kind == "apart") t.Apart(ctx.a);
            else t.LetBe(ctx.a);
        }
    }

    /// <summary>
    /// The council sends A away: B tells them face to face, they gather their things in their room and walk out by the
    /// edge, through the game's own banishment (Social/Banishment.cs). The hints name who'd be glad and who'd grieve, the
    /// game's own mood (heavier when they'd be sent out to die), what the colony loses, and the risks of their coming back.
    /// </summary>
    public class DilemmaEffect_Banish : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override string Unavailable(DilemmaContext ctx)
        {
            var a = ctx?.a;
            if (a == null || !BanishTracker.Eligible(a)) return "CC_Banish_WhyNot_Held".Translate(a?.LabelShort ?? "?").Resolve();
            if (!BanishMath.BigEnough(BanishTracker.ColonyAt(ctx.map).Count)) return "CC_Banish_WhyNot_Small".Translate().Resolve();
            return null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var a = ctx?.a;
            if (a == null || ctx.map == null || a.Dead) return;
            string word = (ctx as BanishContext)?.word ?? BanishTracker.AgeWord();
            string by = ctx.b?.LabelShort ?? "?";
            sheet.Add(ImpactKind.People, -1, "CC_Impact_Banish".Translate(a.LabelShort, by, word).Resolve(), "CC_Impact_BanishBrief".Translate(a.LabelShort).Resolve());
            var colony = BanishTracker.ColonyAt(ctx.map);
            var glad = new List<Pawn>();
            BanishTracker.Views(a, colony, null, null, glad);
            var friends = colony.Where(o => o != a && !BanishTracker.Close(o, a) && BanishTracker.Opinion(o, a) >= BanishMath.StandByOpinion).ToList();
            var kin = colony.Where(o => o != a && BanishTracker.Close(o, a) && !BanishMath.Dislikes(BanishTracker.Opinion(o, a))).ToList();
            string aName = a.LabelShort;
            if (kin.Count > 0) sheet.Thought(kin, BanishTracker.Names(kin), BanishDefOf.Rimfolk_Banish_GriefKin, p => a);
            if (friends.Count > 0)
                sheet.Thought(friends, friends.Count <= 3 ? BanishTracker.Names(friends) : "CC_Impact_Who_FriendsOf".Translate(aName, friends.Count).Resolve(), BanishDefOf.Rimfolk_Banish_Grief, p => a);
            var hurt = friends.Concat(kin).ToList();
            if (hurt.Count > 0) sheet.Trust(hurt.Count <= 3 ? BanishTracker.Names(hurt) : "CC_Impact_Who_CloseTo".Translate(aName, hurt.Count).Resolve(), "banish|" + a.thingIDNumber, -4f, hurt.Count > 1);
            if (glad.Count > 0)
                sheet.Thought(glad, glad.Count <= 3 ? BanishTracker.Names(glad) : "CC_Impact_Who_RivalsOf".Translate(aName, glad.Count).Resolve(), BanishDefOf.Rimfolk_Banish_Glad, p => a);
            // The game's own moods for a banishment: for everyone, heavier when they'd be sent out to die; none for one found guilty.
            bool toDie = PawnBanishUtility.WouldBeLeftToDie(a, a.Tile);
            if (a.guilt != null && a.guilt.IsGuilty) sheet.Add(ImpactKind.Mood, 1, "CC_Impact_BanishGuilty".Translate(aName).Resolve(), "CC_Impact_BanishGuiltyBrief".Translate(aName).Resolve());
            else
            {
                var game = toDie ? ThoughtDefOf.ColonistBanishedToDie : ThoughtDefOf.ColonistBanished;
                var everyone = colony.Where(o => o != a && ThoughtUtility.CanGetThought(o, game, true)).ToList();
                if (everyone.Count > 0) sheet.Thought(everyone, everyone.Count <= 3 ? BanishTracker.Names(everyone) : "CC_Impact_Who_Others".Translate(everyone.Count).Resolve(), game, p => a);
                if (toDie) sheet.Add(ImpactKind.Mood, -1, "CC_Impact_BanishToDie".Translate(aName).Resolve(), "CC_Impact_BanishToDieBrief".Translate(aName).Resolve());
            }
            Skills(sheet, a, colony);
            var seat = OfficeTracker.HeldBy(a);
            if (seat != null && ColonyCouncilMod.Settings.offices) sheet.Add(ImpactKind.Other, -1, "CC_Impact_BanishSeat".Translate(aName, seat.label).Resolve(), "CC_Impact_BanishSeatBrief".Translate(aName, seat.label).Resolve());
            sheet.Add(ImpactKind.People, -1, "CC_Impact_BanishBack".Translate(aName).Resolve(), "CC_Impact_BanishBackBrief".Translate(aName).Resolve());
        }

        /// <summary>The skills the colony would lose a hand at (BanishMath.Lost), from the levels at home now.</summary>
        private static void Skills(ImpactSheet sheet, Pawn a, List<Pawn> colony)
        {
            if (a.skills == null) return;
            var theirs = new Dictionary<string, int>();
            var others = new Dictionary<string, int>();
            foreach (var s in a.skills.skills)
                if (s?.def != null && !s.TotallyDisabled)
                    theirs[s.def.defName] = s.Level;
            foreach (var o in colony)
            {
                if (o == a || o.skills == null || !o.DevelopmentalStage.Adult()) continue;
                foreach (var s in o.skills.skills)
                    if (s?.def != null && !s.TotallyDisabled)
                        others[s.def.defName] = System.Math.Max(others.TryGetValue(s.def.defName, out int v) ? v : 0, s.Level);
            }
            var best = new List<string>();
            var good = new List<string>();
            BanishMath.Lost(theirs, others, best, good);
            string Labels(List<string> names) => TextUtil.JoinAnd(names.Take(3).Select(n => DefDatabase<SkillDef>.GetNamedSilentFail(n)?.label ?? n).ToList());
            if (best.Count > 0)
                sheet.Add(ImpactKind.Skill, -1, "CC_Impact_BanishBest".Translate(Labels(best)).Resolve(), "CC_Impact_BanishBestBrief".Translate(Labels(best)).Resolve());
            if (good.Count > 0)
                sheet.Add(ImpactKind.Skill, -1, "CC_Impact_BanishGood".Translate(Labels(good)).Resolve(), "CC_Impact_BanishGoodBrief".Translate(Labels(good)).Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            // together: told face to face (BanishTracker.Told, through Together), then the walk out on the map.
            if (ctx?.a == null) return;
            BanishTracker.Instance?.Banish(ctx.a, ctx.b);
        }
    }
}
