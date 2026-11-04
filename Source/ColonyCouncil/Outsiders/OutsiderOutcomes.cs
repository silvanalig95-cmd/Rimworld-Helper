using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>A meal or a drink shared with an outsider, under way (the game's own eating; not saved).</summary>
    public class OutsiderMeal
    {
        public Pawn c, x;
        public bool drink;
        public int start;
        public float foodC, foodX;
        public bool cOn, xOn, cDone, xDone;
        public int said;
    }

    /// <summary>A bout between a colonist (a) and an outsider (b), under way: who landed what, the terms, who watches (not saved).</summary>
    public class OutsiderBout
    {
        public Pawn a, b;
        public BoutKind kind;
        public DuelTerms terms;
        public IntVec3 ring;
        public int start, fightStart = -1, endedAt = -1;
        public bool over;
        public int swingsA, swingsB, hitsA, hitsB;
        /// <summary>A practice bout's one harder blow comes on or after this swing (-1: none this bout); who took it.</summary>
        public int hardAt = -1;
        public Pawn hurt;
        public float painA, painB;
        public HashSet<Hediff> before = new HashSet<Hediff>();
        public Pawn winner;
        public bool draw, off;
        public List<Pawn> watchers = new List<Pawn>();

        public Pawn Other(Pawn p) => p == a ? b : a;
        public bool Has(Pawn p) => p != null && (p == a || p == b);
    }

    public static partial class Outsiders
    {
        private static readonly List<OutsiderMeal> meals = new List<OutsiderMeal>();
        private static readonly List<OutsiderBout> bouts = new List<OutsiderBout>();

        /// <summary>Anything under way that the 250-tick look watches.</summary>
        public static bool AnyLive => meals.Count > 0 || bouts.Count > 0;

        // ================= The talk's outcome =================

        /// <summary>
        /// The talk is over (from the talk job, the two face to face): what comes of it. A meal, a lesson or a bout
        /// returns the colonist's next job (the outsider's own is started here); everything else is done.
        /// </summary>
        public static Job Talked(Pawn c, Pawn x, OutsiderAct act, int extra, ThingDef thing)
        {
            if (c == null || x == null) return null;
            try
            {
                // Asked again within its wait: "you asked me that already" was the answer, and nothing more comes of it.
                var plan = OutsiderTalk.TakePlan(c);
                if (plan != null && (plan.x != x || plan.act != act)) plan = null;
                if (plan != null && plan.repeat) return null;
                switch (act)
                {
                    case OutsiderAct.Chat: Chat(c, x, plan); return null;
                    case OutsiderAct.AskHome: AskHome(c, x, plan); return null;
                    case OutsiderAct.Meal: return Meal(c, x, false);
                    case OutsiderAct.Drink: return Meal(c, x, true);
                    case OutsiderAct.GoodWord: GoodWord(c, x); return null;
                    case OutsiderAct.Letter: LetterHanded(c, x); return null;
                    case OutsiderAct.Teach: return Teach(c, x);
                    case OutsiderAct.Bring: Bring(c, x, thing); return null;
                    case OutsiderAct.Invite: Invite(c, x); return null;
                    case OutsiderAct.Befriend: Befriend(c, x, plan); return null;
                    case OutsiderAct.Court: Court(c, x); return null;
                    case OutsiderAct.Faith: FaithTalk(c, x); return null;
                    case OutsiderAct.Spar: return Spar(c, x);
                    case OutsiderAct.Provoke: Provoke(c, x); return null;
                    case OutsiderAct.Duel: return Duel(c, x, (DuelTerms)Mathf.Clamp(extra, 0, 1));
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("outsider talk " + act, e);
            }
            return null;
        }

        private static void Msg(string key, Pawn look, params NamedArgument[] args) =>
            Notify.Message(key.Translate(args).Resolve(), look != null && look.Spawned ? new LookTargets(look) : LookTargets.Invalid, MessageTypeDefOf.NeutralEvent, Importance.Important);

        private static void Good(string key, Pawn look, params NamedArgument[] args) =>
            Notify.Message(key.Translate(args).Resolve(), look != null && look.Spawned ? new LookTargets(look) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent, Importance.Important);

        /// <summary>One of Rimfolk's opinion memories, of the other.</summary>
        private static void Memory(Pawn p, ThoughtDef def, Pawn other, int stage)
        {
            if (p == null || other == null || def == null || p == other) return;
            // together: every caller's two have just been face to face (a talk, a meal, a lesson, a bout).
            CouncilGameComponent.GiveThought(p, def, other, -1f, null, stage);
        }

        /// <summary>What each keeps of how the talk went (its last word was said in the scene, OutsiderTalk.Begin).</summary>
        private static void Close(Pawn c, Pawn x, TalkTone tone)
        {
            int stage = OutsiderMath.ToneStage(tone);
            if (stage < 0) return;
            // together: said and felt face to face at the talk's end.
            Memory(x, OutsiderDefOf.Rimfolk_OutsiderTalk, c, stage);
            Memory(c, OutsiderDefOf.Rimfolk_OutsiderTalk, x, stage);
        }

        // ---------------- A chat ----------------

        private static void Chat(Pawn c, Pawn x, OutsiderTalk.Plan plan)
        {
            // How it went was settled when they met (the scene was said by it); after a load, worked out now.
            var tone = plan?.tone ?? OutsiderMath.Tone(Input(c, x, OutsiderAct.Chat), Rand.Value);
            // together: the two have just talked face to face, in bubbles only (no message: the player asked for fewer).
            Close(c, x, tone);
            Remember(c, x, OutsiderAct.Chat, Note(OutsiderAct.Chat, c, ToneWord(tone)), OutsiderMath.RegardShift(tone), OutsiderMath.WarmthShift(tone));
            OutsiderTalk.Told(c, x, plan?.choice);
        }

        // ---------------- News of home ----------------

        /// <summary>
        /// News of home, told in the scene's bubbles (a piece about what is going on around their town, chosen by what is
        /// true now): what it tells goes into the town's "Heard lately" on the World page and the region map, with who told
        /// whom, instead of a letter. A sour talk tells nothing.
        /// </summary>
        private static void AskHome(Pawn c, Pawn x, OutsiderTalk.Plan plan)
        {
            var tone = plan?.tone ?? OutsiderMath.Tone(Input(c, x, OutsiderAct.AskHome), Rand.Value);
            // together: the news was told face to face, in the scene's bubbles.
            Remember(c, x, OutsiderAct.AskHome, Note(OutsiderAct.AskHome, c, ToneWord(tone)), OutsiderMath.RegardShift(tone) * 0.5f, OutsiderMath.WarmthShift(tone) * 0.5f);
            if (tone != TalkTone.Sour) OutsiderTalk.Told(c, x, plan?.choice);
        }

        // ---------------- A good word at home ----------------

        private static void GoodWord(Pawn c, Pawn x)
        {
            var to = WordTo(x);
            int ofColony = OfColony(x), ofColonist = OfColonist(x, c);
            var tone = OutsiderMath.Tone(Input(c, x, OutsiderAct.GoodWord), Rand.Value);
            bool yes = to != null && OutsiderMath.WillPutInWord(ofColony, ofColonist) && tone <= TalkTone.Plain;
            Remember(c, x, OutsiderAct.GoodWord, yes ? Note(OutsiderAct.GoodWord, c, to.Name) : null, OutsiderMath.RegardShift(tone) * 0.5f, OutsiderMath.WarmthShift(tone) * 0.5f);
            if (!yes)
            {
                // together: asked and answered face to face, in a bubble only.
                Say(x, "wordno", c);
                return;
            }
            var book = Book;
            float shift = OutsiderMath.GoodWordShift(ofColony, ofColonist, c.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 4) * (tone == TalkTone.Warm ? 1.25f : 1f);
            book?.words.Add(new OutsiderWord { outsider = x, outsiderName = x.LabelShort, notable = to.id, by = c, byName = c.LabelShort, shift = shift, asked = Now });
            while (book != null && book.words.Count > OutsiderBook.MaxWords) book.words.RemoveAt(0);
            Say(x, "wordyes", c);
            // together: promised face to face.
            Good("CC_Out_Msg_WordYes", c, c.Named("PAWN"), x.Named("OTHER"), NotableBook.Called(to).Named("NAME"));
        }

        // ---------------- A letter carried home ----------------

        private static void LetterHanded(Pawn c, Pawn x)
        {
            var l = LetterFor(x);
            if (l == null) return;
            Say(x, "letteryes", c);
            NeighbourLetters.HandedToBearer(l, c, x);
            Remember(c, x, OutsiderAct.Letter, Note(OutsiderAct.Letter, c), 1f, 2f);
        }

        // ---------------- A favour: a lesson ----------------

        /// <summary>The skill they'd teach the colonist (where they lead by most, and enough), or null.</summary>
        public static SkillDef TeachSkill(Pawn c, Pawn x)
        {
            if (c?.skills == null || x?.skills == null) return null;
            var defs = DefDatabase<SkillDef>.AllDefsListForReading;
            var teacher = new List<int>();
            var learner = new List<int>();
            var barred = new List<bool>();
            foreach (var d in defs)
            {
                var t = x.skills.GetSkill(d);
                var l = c.skills.GetSkill(d);
                teacher.Add(t?.Level ?? 0);
                learner.Add(l?.Level ?? 0);
                barred.Add(t == null || l == null || t.TotallyDisabled || l.TotallyDisabled);
            }
            int i = OutsiderMath.TeachPick(teacher, learner, barred);
            return i >= 0 ? defs[i] : null;
        }

        private static Job Teach(Pawn c, Pawn x)
        {
            var skill = TeachSkill(c, x);
            var tone = OutsiderMath.Tone(Input(c, x, OutsiderAct.Teach), Rand.Value);
            bool yes = skill != null && OutsiderMath.WillDoFavour(OfColony(x), OfColonist(x, c)) && tone <= TalkTone.Plain;
            Remember(c, x, OutsiderAct.Teach, null, 0f, 0f);
            if (!yes)
            {
                // together: asked and answered face to face, in a bubble only.
                Say(x, "teachno", c);
                return null;
            }
            Say(x, "teachyes", c);
            var spot = LessonSpot(c, x, skill);
            var job = JobMaker.MakeJob(OutsiderDefOf.Council_OutsiderLesson, x, spot);
            job.count = DefDatabase<SkillDef>.AllDefsListForReading.IndexOf(skill);
            job.playerForced = true;
            return job;
        }

        /// <summary>Where the lesson is held: a bench of the skill's work (its working spot), else where they stand.</summary>
        private static LocalTargetInfo LessonSpot(Pawn c, Pawn x, SkillDef skill)
        {
            var map = c.Map;
            try
            {
                Building best = null;
                float bestD = float.MaxValue;
                foreach (var b in map.listerBuildings.allBuildingsColonist)
                {
                    if (b == null || !b.Spawned || !b.def.hasInteractionCell || b.IsForbidden(c) || b.Position.Fogged(map) || !c.CanReserve(b)) continue;
                    bool fits = b is Building_ResearchBench ? skill == SkillDefOf.Intellectual
                        : b.def.AllRecipes != null && b.def.AllRecipes.Any(r => r?.workSkill == skill);
                    if (!fits) continue;
                    float d = b.Position.DistanceToSquared(c.Position);
                    if (d >= bestD || !c.CanReach(b.InteractionCell, PathEndMode.OnCell, Danger.Some)) continue;
                    best = b;
                    bestD = d;
                }
                if (best != null) return best;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("outsider lesson bench", e);
            }
            return c.Position;
        }

        /// <summary>The lesson is over (from the learner's job): what the colony owes them for it, and what each thinks of the other.</summary>
        public static void LessonOver(Pawn c, Pawn x, SkillDef skill, int ticks)
        {
            if (c == null || x == null || skill == null) return;
            // together: the lesson they began side by side ended before it was one; nothing comes of it.
            if (ticks < 500) return;
            Say(x, "lessondone", c);
            Remember(c, x, OutsiderAct.Teach, Note(OutsiderAct.Teach, c, skill.label), 2f, 5f);
            // together: the two spent the lesson side by side.
            Memory(c, OutsiderDefOf.Rimfolk_OutsiderTaught, x, 0);
            Memory(x, OutsiderDefOf.Rimfolk_OutsiderTaught, c, 1);
            var f = PeopleOf(x);
            if (f != null) KinMemory.Note(f, "taught", c, null);
        }

        // ---------------- A favour: something brought next time ----------------

        /// <summary>What their people could bring: what anyone of their era trades, nothing the colony couldn't have.</summary>
        public static List<ThingDef> BringChoices(Pawn x)
        {
            var list = new List<ThingDef>();
            var f = PeopleOf(x);
            if (f == null) return list;
            int tech = (int)f.def.techLevel;
            void Add(string name, int minTech)
            {
                var d = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                if (d != null && tech >= minTech && !list.Contains(d)) list.Add(d);
            }
            Add("MedicineHerbal", 0);
            Add("Pemmican", 0);
            Add("Cloth", 0);
            Add("Leather_Plain", 0);
            Add("WoodLog", 0);
            Add("Beer", 0);
            Add("Steel", OutsiderMath.Medieval);
            Add("MedicineIndustrial", NotableMath.Industrial);
            Add("ComponentIndustrial", NotableMath.Industrial);
            return list;
        }

        private static void Bring(Pawn c, Pawn x, ThingDef thing)
        {
            var f = PeopleOf(x);
            var tone = OutsiderMath.Tone(Input(c, x, OutsiderAct.Bring), Rand.Value);
            int ofColony = OfColony(x);
            bool yes = thing != null && f != null && OutsiderMath.WillBring(ofColony, OfColonist(x, c)) && tone <= TalkTone.Plain;
            Remember(c, x, OutsiderAct.Bring, yes ? Note(OutsiderAct.Bring, c, thing.label) : null, 0.5f, 1f);
            if (!yes)
            {
                // together: asked and answered face to face, in a bubble only.
                Say(x, "bringno", c);
                return;
            }
            int count = OutsiderMath.BringCount(thing.BaseMarketValue, ofColony);
            var book = Book;
            if (book != null) Favours.Ask(x, f, c, thing, count);
            Say(x, "bringyes", c);
            // together: promised face to face.
            Good("CC_Out_Msg_BringYes", c, c.Named("PAWN"), x.Named("OTHER"), Couriers.Amount(count, null, thing).Named("THING"));
        }

        // ---------------- An invitation to stay ----------------

        /// <summary>Their reasons to stay with the colony: love, family, hard times at home, the colony's faith (Integrations/GuestWill.cs), and a friend here.</summary>
        public static int Reasons(Pawn x, Pawn c)
        {
            int n = 0;
            try
            {
                n = GuestWill.Of(x).reasons.Count(r => r != "?");
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("outsider reasons", e);
            }
            if (Book?.AnyFriend(x) ?? false) n++;
            return n;
        }

        private static void Invite(Pawn c, Pawn x)
        {
            int reasons = Reasons(x, c);
            int ties = 0;
            try
            {
                ties = GuestWill.Of(x).ties.Count;
            }
            catch
            {
                ties = 0;
            }
            var f = PeopleOf(x);
            float chance = OutsiderMath.InviteChance(reasons, ties, OfColony(x), OfColonist(x, c), c.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 4);
            Remember(c, x, OutsiderAct.Invite, null, 0.5f, 1f);
            if (x.TraderKind != null || !Rand.Chance(chance))
            {
                // together: asked and answered face to face, in a bubble only.
                Say(x, "inviteno", c);
                return;
            }
            Say(x, "inviteyes", c);
            if (f != null) WorldSituationWorker.Goodwill(f, OutsiderMath.InviteGoodwill(reasons), true);
            InteractionWorker_RecruitAttempt.DoRecruit(c, x, true);
            // together: asked and accepted face to face.
            Good("CC_Out_Msg_InviteYes", x, c.Named("PAWN"), x.Named("OTHER"), (f?.Name ?? "").Named("FACTION"));
        }

        // ---------------- Getting to know them ----------------

        private static void Befriend(Pawn c, Pawn x, OutsiderTalk.Plan plan)
        {
            var tone = plan?.tone ?? OutsiderMath.Tone(Input(c, x, OutsiderAct.Befriend), Rand.Value);
            Close(c, x, tone);
            var pair = Remember(c, x, OutsiderAct.Befriend, Note(OutsiderAct.Befriend, c, ToneWord(tone)), OutsiderMath.RegardShift(tone), OutsiderMath.WarmthShift(tone) * 1.5f);
            OutsiderTalk.Told(c, x, plan?.choice);
            if (pair == null) return;
            if (!pair.friends && OutsiderMath.FriendsNow(pair.warmth, pair.fam, pair.visits))
            {
                pair.friends = true;
                // together: they became friends while spending the time side by side.
                Memory(c, OutsiderDefOf.Rimfolk_OutsiderFriend, x, 0);
                Memory(x, OutsiderDefOf.Rimfolk_OutsiderFriend, c, 0);
                Say(c, "friends", x);
                var n = NotableOf(x);
                n?.Remember("CC_Out_Note_Friends".Translate(c.LabelShort.Named("PAWN")).Resolve(), 2f);
                Good("CC_Out_Msg_Friends", c, c.Named("PAWN"), x.Named("OTHER"));
                return;
            }
            if (pair.friends)
            {
                Memory(c, OutsiderDefOf.Rimfolk_OutsiderFriend, x, 0);
                Memory(x, OutsiderDefOf.Rimfolk_OutsiderFriend, c, 0);
            }
            // Not friends yet: the time spent shows in bubbles only; how far they are from it is on the pair's record.
        }

        // ---------------- Courting ----------------

        private static void Court(Pawn c, Pawn x)
        {
            var pair = Book?.Pair(c, x, true);
            float game;
            try
            {
                game = InteractionWorker_RomanceAttempt.SuccessChance(c, x, 0.8f);
            }
            catch
            {
                game = 0.3f;
            }
            bool ok = Rand.Chance(OutsiderMath.CourtChance(game, pair?.fam ?? 0f));
            if (!ok)
            {
                Say(x, "courtno", c);
                Remember(c, x, OutsiderAct.Court, Note(OutsiderAct.Court, c, "CC_Out_CourtWord_No".Translate().Resolve()), -1f, -4f);
                // together: turned down face to face (the game's own rebuff memories).
                if (ThoughtDefOf.RebuffedMyRomanceAttempt != null) c.needs?.mood?.thoughts?.memories?.TryGainMemory(ThoughtDefOf.RebuffedMyRomanceAttempt, x);
                if (ThoughtDefOf.FailedRomanceAttemptOnMe != null) x.needs?.mood?.thoughts?.memories?.TryGainMemory(ThoughtDefOf.FailedRomanceAttemptOnMe, c);
                Msg("CC_Out_Msg_CourtNo", c, c.Named("PAWN"), x.Named("OTHER"));
                return;
            }
            Say(x, "courtyes", c);
            pair = Remember(c, x, OutsiderAct.Court, Note(OutsiderAct.Court, c, "CC_Out_CourtWord_Yes".Translate().Resolve()), 2f, 8f);
            if (pair == null) return;
            // together: courting face to face.
            Memory(c, OutsiderDefOf.Rimfolk_OutsiderSweet, x, 0);
            Memory(x, OutsiderDefOf.Rimfolk_OutsiderSweet, c, 0);
            if (pair.court == 0)
            {
                pair.court = 1;
                pair.courtVisits = pair.visits;
                Good("CC_Out_Msg_Sweet", c, c.Named("PAWN"), x.Named("OTHER"));
                return;
            }
            if (pair.court == 1 && pair.visits - pair.courtVisits >= OutsiderMath.LoversVisits && pair.warmth >= OutsiderMath.LoversWarmth
                && RelationsUtility.RomanceEligiblePair(c, x, false).Accepted)
            {
                pair.court = 2;
                c.relations.AddDirectRelation(PawnRelationDefOf.Lover, x);
                Say(c, "lovers", x);
                Good("CC_Out_Msg_Lovers", c, c.Named("PAWN"), x.Named("OTHER"));
                return;
            }
            Good("CC_Out_Msg_CourtYes", c, c.Named("PAWN"), x.Named("OTHER"));
        }

        // ---------------- Faith ----------------

        private static void FaithTalk(Pawn c, Pawn x)
        {
            if (!ModsConfig.IdeologyActive || c.Ideo == null || x.Ideo == null || c.ideo == null || x.ideo == null) return;
            bool same = c.Ideo == x.Ideo;
            var end = OutsiderMath.Faith(same, x.ideo.Certainty, c.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 4, x.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 4,
                Affinity(c, x), Rand.Value);
            // The game's own certainty, a little either way: never a change of faith.
            float sx = OutsiderMath.CertaintyShift(end, true), sc = OutsiderMath.CertaintyShift(end, false);
            if (sx != 0f) x.ideo.OffsetCertainty(sx);
            if (sc != 0f) c.ideo.OffsetCertainty(sc);
            Say(x, "faith" + end.ToString().ToLowerInvariant(), c);
            var tone = end == FaithEnd.Closer ? TalkTone.Warm : end == FaithEnd.Softened ? TalkTone.Good : end == FaithEnd.Agreed ? TalkTone.Plain : TalkTone.Cool;
            int stage = OutsiderMath.ToneStage(tone);
            if (stage >= 0)
            {
                // together: the talk of faith was face to face.
                Memory(x, OutsiderDefOf.Rimfolk_OutsiderTalk, c, stage);
                Memory(c, OutsiderDefOf.Rimfolk_OutsiderTalk, x, stage);
            }
            Remember(c, x, OutsiderAct.Faith, Note(OutsiderAct.Faith, c, ("CC_Out_FaithWord_" + end).Translate().Resolve()), OutsiderMath.RegardShift(tone), OutsiderMath.FaithWarmth(end));
            // A faith moved shows in their certainty; the talk itself in bubbles only, but a debate that hardened both is worth a word.
            if (end == FaithEnd.Hardened) Msg("CC_Out_Msg_Faith_" + end, c, c.Named("PAWN"), x.Named("OTHER"), x.Ideo.name.Named("IDEO"), c.Ideo.name.Named("OURS"));
        }

        // ---------------- Hard words ----------------

        private static void Provoke(Pawn c, Pawn x)
        {
            var f = PeopleOf(x);
            // together: the insult was thrown face to face.
            Memory(x, OutsiderDefOf.Rimfolk_OutsiderInsulted, c, 0);
            Remember(c, x, OutsiderAct.Provoke, Note(OutsiderAct.Provoke, c), OutsiderMath.ProvokeRegard, OutsiderMath.ProvokeWarmth);
            if (f != null) KinMemory.Note(f, "slighted", c, x.LabelShort);
            float game = 0f;
            try
            {
                var insult = InteractionDefOf.Insult;
                if (insult != null && x.interactions != null) game = x.interactions.SocialFightChance(insult, c);
            }
            catch
            {
                game = 0f;
            }
            float chance = OutsiderMath.FightChance(Temper(x), Temper(c), OfColonist(x, c), game);
            if (!Peaceful(x) && !Peaceful(c) && x.interactions != null && Rand.Chance(chance))
            {
                var bout = new OutsiderBout { a = c, b = x, kind = BoutKind.Brawl, start = Now };
                bouts.Add(bout);
                Say(x, "provokefight", c);
                x.interactions.StartSocialFight(c);
                if (x.MentalStateDef == MentalStateDefOf.SocialFighting) return;
                bouts.Remove(bout);
            }
            Say(x, "provokecalm", c);
            Msg("CC_Out_Msg_Provoke", c, c.Named("PAWN"), x.Named("OTHER"), (f?.Name ?? "").Named("FACTION"));
        }

        /// <summary>A brawl after hard words is over: their people hear of it.</summary>
        private static void BrawlOver(OutsiderBout bout)
        {
            var c = bout.a;
            var x = bout.b;
            var f = PeopleOf(x);
            if (f != null)
            {
                KinMemory.Note(f, "brawled", c, x?.LabelShort);
                WorldSituationWorker.Goodwill(f, OutsiderMath.BrawlGoodwill, true);
            }
            var n = NotableOf(x);
            if (n != null)
            {
                n.Remember("CC_Out_Note_Brawl".Translate((c?.LabelShort ?? "?").Named("PAWN")).Resolve(), -3f);
                if (KinMemory.Ours(c)) n.RegardOf(c, true).personal -= 4f;
                NotableBook.Changed();
            }
            // apart: the fight's end, told in a message (the two fought on the map; their people hear of it).
            Msg("CC_Out_Msg_BrawlOver", c, (c?.LabelShort ?? "?").Named("PAWN"), (x?.LabelShort ?? "?").Named("OTHER"), (f?.Name ?? "").Named("FACTION"));
        }

        // ================= A gift =================

        /// <summary>A thing in the stores the colonist could give them, how much, what it's worth, and how it would suit them.</summary>
        public class GiftChoice
        {
            public Thing thing;
            public int count;
            public float value;
            public float fit;
            public bool suits, fine, against;
        }

        /// <summary>What's in the stores to give, the things that would suit them first; one stack of each kind.</summary>
        public static List<GiftChoice> GiftChoices(Pawn c, Pawn x)
        {
            var list = new List<GiftChoice>();
            var map = c?.Map;
            if (map == null || x?.inventory == null) return list;
            var seen = new HashSet<string>();
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver))
            {
                if (t == null || !t.Spawned || t is Corpse || t is MinifiedThing || t.IsForbidden(c) || t.Position.Fogged(map) || !t.IsInAnyStorage()) continue;
                if (t.def.thingCategories != null && t.def.thingCategories.Any(cat => cat == ThingCategoryDefOf.StoneChunks)) continue;
                float unit = t.MarketValue;
                if (unit <= 0.01f) continue;
                QualityUtility.TryGetQuality(t, out var q);
                string key = t.def.defName + "|" + (t.Stuff?.defName ?? "") + "|" + (int)q;
                if (seen.Contains(key)) continue;
                seen.Add(key);
                int n = OutsiderMath.GiftCount(unit, t.stackCount);
                if (n <= 0) continue;
                var g = new GiftChoice { thing = t, count = n, value = unit * n };
                Judge(x, t, g);
                list.Add(g);
                if (seen.Count > 400) break;
            }
            return list.OrderByDescending(g => g.against ? -1f : g.fit).ThenByDescending(g => g.value).Take(40).ToList();
        }

        private static void Judge(Pawn x, Thing t, GiftChoice g)
        {
            var d = t.def;
            g.fine = QualityUtility.TryGetQuality(t, out var q) && q >= QualityCategory.Good;
            if (d.IsIngestible) g.against = Beliefs.MindsEating(x, t);
            if (d.IsWeapon && x.WorkTagIsDisabled(WorkTags.Violent)) g.against = true;
            g.suits = Suits(x, t);
            g.fit = OutsiderMath.GiftFit(g.suits, g.fine, g.against);
        }

        private static bool Keen(Pawn p, SkillDef s, int level = 8)
        {
            var r = p?.skills?.GetSkill(s);
            return r != null && !r.TotallyDisabled && (r.passion != RimWorld.Passion.None || r.Level >= level);
        }

        /// <summary>Whether it suits them: a drink for a drinker, a book for a scholar, a weapon for a fighter, medicine for a healer, silver or art for the greedy.</summary>
        public static bool Suits(Pawn x, Thing t)
        {
            var d = t.def;
            var traits = x.story?.traits;
            bool drinker = traits != null && TraitDefOf.DrugDesire != null && traits.HasTrait(TraitDefOf.DrugDesire) && traits.DegreeOfTrait(TraitDefOf.DrugDesire) > 0;
            if (d.IsDrug && d.IsNonMedicalDrug) return drinker;
            if (t is Book) return Keen(x, SkillDefOf.Intellectual);
            if (d.IsWeapon) return d.IsMeleeWeapon ? Keen(x, SkillDefOf.Melee) || Temper(x) > 0 : Keen(x, SkillDefOf.Shooting) || Temper(x) > 0;
            if (d.IsMedicine) return Keen(x, SkillDefOf.Medicine) || (WorldSituations.Instance?.HardTimes(PeopleOf(x)) ?? false);
            if (d.IsIngestible) return (x.needs?.food?.CurLevelPercentage ?? 1f) < 0.4f || HasNamed(x, "Gourmand");
            bool greedy = HasNamed(x, "Greedy") || HasNamed(x, "Jealous");
            if (d == ThingDefOf.Silver || d == ThingDefOf.Gold) return greedy;
            if (t.TryGetComp<CompArt>() is CompArt art && art.Active) return greedy || Keen(x, SkillDefOf.Artistic);
            if (d.IsApparel) return greedy && t.MarketValue >= 150f || Keen(x, SkillDefOf.Crafting) && QualityUtility.TryGetQuality(t, out var q) && q >= QualityCategory.Good;
            if (d.IsStuff && d.stuffProps?.categories != null && d.stuffProps.categories.Any(cat => cat?.defName == "Fabric" || cat?.defName == "Leathery")) return Keen(x, SkillDefOf.Crafting);
            return false;
        }

        private static MethodInfo giftBase, giftPost;
        private static bool giftLooked;

        /// <summary>The game's own goodwill for a gift (as for one sent to their town), at the share a gift into one pair of hands counts for.</summary>
        private static int GiftGoodwill(Thing t, int count, Faction f, float fit)
        {
            if (f == null || !f.HasGoodwill || t == null || count <= 0) return 0;
            float value = t.MarketValue * count;
            float share = OutsiderMath.HandGiftShare * Mathf.Clamp(fit, 1f, 1.5f);
            if (!giftLooked)
            {
                giftLooked = true;
                giftBase = AccessTools.Method(typeof(FactionGiftUtility), "GetBaseGoodwillChange", new[] { typeof(Thing), typeof(int), typeof(float), typeof(Faction) });
                giftPost = AccessTools.Method(typeof(FactionGiftUtility), "PostProcessedGoodwillChange", new[] { typeof(float), typeof(Faction) });
            }
            try
            {
                if (giftBase != null && giftPost != null)
                {
                    float b = (float)giftBase.Invoke(null, new object[] { t, count, t.MarketValue, f });
                    return (int)giftPost.Invoke(null, new object[] { b * share, f });
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("outsider gift goodwill", e);
            }
            return Mathf.RoundToInt(OutsiderMath.GiftGoodwill(value, f.PlayerGoodwill) * share);
        }

        /// <summary>
        /// The colonist has carried the gift over (from the gift job): into their pack it goes (not for sale), and it
        /// warms them toward the giver as much as it suits them; their people count it as the game counts a gift.
        /// False: they couldn't take it (it stays in the colonist's hands).
        /// </summary>
        public static bool GiftHanded(Pawn c, Pawn x, Thing carried)
        {
            if (c == null || x?.inventory == null || carried == null) return false;
            var g = new GiftChoice { thing = carried, count = carried.stackCount, value = carried.MarketValue * carried.stackCount };
            Judge(x, carried, g);
            if (g.against)
            {
                // together: offered face to face, and handed back (the colonist still holds it).
                Say(x, "giftno", c);
                return false;
            }
            var f = PeopleOf(x);
            string what = carried.stackCount > 1 ? Couriers.Amount(carried.stackCount, null, carried.def) : Find.ActiveLanguageWorker.WithIndefiniteArticle(PlayGive.Label(carried));
            // Gifts in a row on one visit count for less and less (OutsiderTalkMath.GiftFactor).
            float share = OutsiderTalk.GiftShare(x);
            int gw = Mathf.RoundToInt(GiftGoodwill(carried, carried.stackCount, f, g.fit) * share);
            c.carryTracker.innerContainer.Remove(carried);
            x.inventory.TryAddItemNotForSale(carried);
            if (!x.inventory.innerContainer.Contains(carried) && !carried.Destroyed && !carried.Spawned)
                GenPlace.TryPlaceThing(carried, x.Position, x.Map, ThingPlaceMode.Near);
            float warmth = OutsiderMath.GiftWarmth(g.value, g.fit, LivingEvents.WealthFactor(c.Map)) * share;
            Say(x, g.suits && share >= 0.5f ? "giftsuits" : "giftthanks", c);
            // together: handed over face to face; a gift after several on one visit leaves no memory of its own.
            if (share >= OutsiderTalkMath.GiftMemoryFloor) Memory(x, OutsiderDefOf.Rimfolk_OutsiderGift, c, share >= 0.5f ? OutsiderMath.GiftStage(g.fit) : 1);
            Remember(c, x, OutsiderAct.Gift, Note(OutsiderAct.Gift, c, what), warmth * 0.5f, warmth);
            if (f != null)
            {
                FactionMemory.Note(f, "gaveGift", x.LabelShort);
                if (gw > 0)
                    try
                    {
                        f.TryAffectGoodwillWith(Faction.OfPlayer, gw, true, false, HistoryEventDefOf.GaveGift);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("outsider gift goodwill change", e);
                    }
            }
            return true;
        }

        // ================= A meal or a drink =================

        /// <summary>
        /// Something in the stores for each of them: the best meal (or a drink) each would have, by their own food
        /// policy, diet and beliefs (a guest's as Hospitality sets it). False: nothing that suits both.
        /// </summary>
        public static bool Portions(Pawn c, Pawn x, bool drink, out Thing forC, out Thing forX)
        {
            forC = forX = null;
            var map = c?.Map;
            if (map == null || x == null) return false;
            var candidates = new List<Thing>();
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver))
            {
                var d = t?.def;
                if (d?.ingestible == null || !t.Spawned || t.IsForbidden(c) || t.Position.Fogged(map) || !t.IsInAnyStorage()) continue;
                if (drink)
                {
                    if (!d.IsDrug || d.ingestible.drugCategory != DrugCategory.Social || !Beliefs.IsAlcohol(d)) continue;
                }
                else if (d.IsDrug || d.ingestible.preferability < FoodPreferability.MealAwful || d.ingestible.preferability > FoodPreferability.MealLavish) continue;
                candidates.Add(t);
                if (candidates.Count > 200) break;
            }
            var ordered = candidates.OrderByDescending(t => (int)t.def.ingestible.preferability).ThenBy(t => t.Position.DistanceToSquared(c.Position)).ToList();
            foreach (var t in ordered)
            {
                if (forC == null && !Beliefs.MindsEating(c, t) && c.CanReach(t, PathEndMode.ClosestTouch, Danger.Some)) forC = t;
                if (forX == null && !Beliefs.MindsEating(x, t) && x.CanReach(t, PathEndMode.ClosestTouch, Danger.Some) && (t != forC || t.stackCount >= 2)) forX = t;
                if (forC != null && forX != null) break;
            }
            return forC != null && forX != null;
        }

        private static Job Meal(Pawn c, Pawn x, bool drink)
        {
            var act = drink ? OutsiderAct.Drink : OutsiderAct.Meal;
            var tone = OutsiderMath.Tone(Input(c, x, act), Rand.Value);
            Remember(c, x, act, null, 0f, 0f);
            string no = drink ? "drinkno" : "mealno";
            if (tone >= TalkTone.Cool || x.jobs == null)
            {
                // together: asked and answered face to face, in a bubble only.
                Say(x, no, c);
                return null;
            }
            if (!Portions(c, x, drink, out var fc, out var fx))
            {
                Msg(drink ? "CC_Out_Msg_NoDrink" : "CC_Out_Msg_NoFood", c, c.Named("PAWN"), x.Named("OTHER"));
                return null;
            }
            Say(x, drink ? "drinkyes" : "mealyes", c);
            // The game's own eating, each with their portion from the stores: side by side at one table when there are
            // two chairs free at it, else each at the table nearest their food, as the game picks.
            bool seated = Seats(c, x, out var seatC, out var seatX);
            var def = seated ? OutsiderDefOf.Council_OutsiderMeal : JobDefOf.Ingest;
            var jx = seated ? JobMaker.MakeJob(def, fx, seatX) : JobMaker.MakeJob(def, fx);
            jx.count = 1;
            x.jobs.StartJob(jx, JobCondition.InterruptForced, null, false, true);
            meals.Add(new OutsiderMeal { c = c, x = x, drink = drink, start = Now, foodC = c.needs?.food?.CurLevel ?? 0f, foodX = x.needs?.food?.CurLevel ?? 0f });
            var jc = seated ? JobMaker.MakeJob(def, fc, seatC) : JobMaker.MakeJob(def, fc);
            jc.count = 1;
            jc.playerForced = true;
            return jc;
        }

        /// <summary>Two free chairs at one of the colony's tables, the nearest to the colonist first, one each.</summary>
        private static bool Seats(Pawn c, Pawn x, out IntVec3 seatC, out IntVec3 seatX)
        {
            seatC = seatX = IntVec3.Invalid;
            var map = c.Map;
            try
            {
                foreach (var t in map.listerBuildings.allBuildingsColonist.Where(b => b.Spawned && Avail.IsEatingTable(b.def) && !b.IsForbidden(c) && !b.Position.Fogged(map))
                             .OrderBy(b => b.Position.DistanceToSquared(c.Position)).Take(12))
                {
                    Thing forC = null, forX = null;
                    foreach (var cell in GenAdj.CellsAdjacent8Way(t))
                    {
                        var chair = cell.InBounds(map) ? cell.GetEdifice(map) : null;
                        if (chair == null || !(chair.def.building?.isSittable ?? false) || chair.IsForbidden(c) || cell.GetFirstPawn(map) != null) continue;
                        if (forC == null && c.CanReserveAndReach(chair, PathEndMode.OnCell, Danger.Some)) forC = chair;
                        else if (forX == null && chair != forC && x.CanReserveAndReach(chair, PathEndMode.OnCell, Danger.Some)) forX = chair;
                        if (forC != null && forX != null) break;
                    }
                    if (forC == null || forX == null) continue;
                    seatC = forC.Position;
                    seatX = forX.Position;
                    return true;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("outsider meal seats", e);
            }
            return false;
        }

        private static bool Eating(Pawn p) => p != null && p.Spawned && (p.CurJobDef == JobDefOf.Ingest || p.CurJobDef == OutsiderDefOf.Council_OutsiderMeal);

        private static void MealsRare(int now)
        {
            foreach (var m in meals.ToList())
            {
                bool cNow = Eating(m.c);
                bool xNow = Eating(m.x);
                if (cNow) m.cOn = true;
                else if (m.cOn) m.cDone = true;
                if (xNow) m.xOn = true;
                else if (m.xOn) m.xDone = true;
                // A word now and then at the table, if they're sitting together.
                if (cNow && xNow && m.said < 2 && Rand.Chance(0.2f) && Together.Can(m.c, m.x))
                {
                    m.said++;
                    Say(Rand.Bool ? m.c : m.x, m.drink ? "drinktalk" : "mealtalk", m.said % 2 == 0 ? m.c : m.x);
                }
                bool cOver = m.cDone || !m.cOn && now - m.start > 1200;
                bool xOver = m.xDone || !m.xOn && now - m.start > 1200;
                if (cOver && xOver || now - m.start > 6000 || m.c == null || m.x == null || m.c.Dead || m.x.Dead)
                {
                    meals.Remove(m);
                    MealOver(m);
                }
            }
        }

        private static void MealOver(OutsiderMeal m)
        {
            var c = m.c;
            var x = m.x;
            if (c == null || x == null || c.Dead || x.Dead) return;
            bool cAte = m.cDone && (m.drink || (c.needs?.food?.CurLevel ?? 0f) > m.foodC + 0.05f);
            bool xAte = m.xDone && (m.drink || (x.needs?.food?.CurLevel ?? 0f) > m.foodX + 0.05f);
            var act = m.drink ? OutsiderAct.Drink : OutsiderAct.Meal;
            // apart: the meal didn't come to sitting down together; nothing comes of it.
            if (!cAte || !xAte) return;
            var tone = OutsiderMath.Tone(Input(c, x, act), Rand.Value);
            if (tone > TalkTone.Plain) tone = TalkTone.Plain;
            if (Together.Can(c, x)) Say(x, m.drink ? "drinkend" : "mealend", c);
            // together: they ate (or drank) at the same time, side by side at the table.
            Memory(c, OutsiderDefOf.Rimfolk_OutsiderMeal, x, m.drink ? 1 : 0);
            Memory(x, OutsiderDefOf.Rimfolk_OutsiderMeal, c, m.drink ? 1 : 0);
            Remember(c, x, act, Note(act, c, ToneWord(tone)), OutsiderMath.RegardShift(tone) + 1f, OutsiderMath.WarmthShift(tone) + 3f);
        }

        // ================= Bouts =================

        /// <summary>The two are in a bout together now (or just were): their people don't count its blows as an attack on them.</summary>
        public static bool InBout(Pawn p, Pawn q)
        {
            if (p == null || q == null || bouts.Count == 0) return false;
            int now = Now;
            foreach (var b in bouts)
                if (b.Has(p) && b.Has(q) && (!b.over || now - b.endedAt < 120))
                    return true;
            return false;
        }

        public static OutsiderBout BoutOf(Pawn p) => p == null ? null : bouts.FirstOrDefault(b => b.Has(p) && b.kind != BoutKind.Brawl);

        private static Job Spar(Pawn c, Pawn x)
        {
            float chance = OutsiderMath.SparAcceptChance(Temper(x), Melee(x), Melee(c)) * (OfColonist(x, c) < -10 ? 0.6f : 1f);
            Remember(c, x, OutsiderAct.Spar, null, 0f, 0f);
            if (!Rand.Chance(chance))
            {
                // together: asked and answered face to face, in a bubble only.
                Say(x, "sparno", c);
                return null;
            }
            Say(x, "sparyes", c);
            return BeginBout(c, x, BoutKind.Spar, DuelTerms.Yield);
        }

        private static Job Duel(Pawn c, Pawn x, DuelTerms terms)
        {
            float chance = OutsiderMath.DuelAcceptChance(Temper(x), Melee(x), Melee(c), OfColony(x), terms);
            Remember(c, x, OutsiderAct.Duel, null, 0f, 0f);
            if (!Rand.Chance(chance))
            {
                Say(x, "duelno", c);
                // together: the challenge was made and declined face to face.
                Msg("CC_Out_Msg_DuelNo", c, c.Named("PAWN"), x.Named("OTHER"));
                return null;
            }
            Say(x, "duelyes", c);
            var job = BeginBout(c, x, BoutKind.Duel, terms);
            var bout = BoutOf(c);
            if (bout != null) CallWitnesses(bout);
            Msg("CC_Out_Msg_DuelYes", c, c.Named("PAWN"), x.Named("OTHER"), ("CC_Out_TermsWord_" + terms).Translate().Named("TERMS"));
            return job;
        }

        /// <summary>A bout begins: a clear place near the colonist, the outsider's way there, and the colonist's job for it (returned).</summary>
        private static Job BeginBout(Pawn c, Pawn x, BoutKind kind, DuelTerms terms)
        {
            var map = c.Map;
            var ring = Ring(c, map);
            var theirs = IntVec3.Invalid;
            foreach (var cell in GenAdj.CellsAdjacent8Way(new TargetInfo(ring, map)))
                if (cell.InBounds(map) && cell.Standable(map) && x.CanReach(cell, PathEndMode.OnCell, Danger.Some))
                {
                    theirs = cell;
                    break;
                }
            if (!theirs.IsValid) theirs = ring;
            var bout = new OutsiderBout { a = c, b = x, kind = kind, terms = terms, ring = ring, start = Now };
            foreach (var h in c.health.hediffSet.hediffs) bout.before.Add(h);
            foreach (var h in x.health.hediffSet.hediffs) bout.before.Add(h);
            if (kind == BoutKind.Spar && Rand.Chance(OutsiderMath.SparHardBlowChance(Temper(c), Temper(x))))
                bout.hardAt = Rand.RangeInclusive(3, OutsiderMath.SparSwings * 2 - 2);
            bouts.RemoveAll(b => b.Has(c) || b.Has(x));
            bouts.Add(bout);
            var jx = JobMaker.MakeJob(OutsiderDefOf.Council_OutsiderBout, c, theirs);
            jx.count = (int)kind;
            x.jobs.StartJob(jx, JobCondition.InterruptForced, null, false, true);
            var jc = JobMaker.MakeJob(OutsiderDefOf.Council_OutsiderBout, x, ring);
            jc.count = (int)kind;
            jc.playerForced = true;
            return jc;
        }

        /// <summary>A clear place for a bout near the colonist: a standable cell with room around it, nobody on it.</summary>
        private static IntVec3 Ring(Pawn c, Map map)
        {
            foreach (var cell in GenRadial.RadialCellsAround(c.Position, 8f, true))
            {
                if (!cell.InBounds(map) || !cell.Standable(map) || cell.GetFirstPawn(map) != null || cell.Fogged(map)) continue;
                bool room = true;
                foreach (var n in GenAdj.CellsAdjacent8Way(new TargetInfo(cell, map)))
                    if (!n.InBounds(map) || !n.Standable(map))
                    {
                        room = false;
                        break;
                    }
                if (room && c.CanReach(cell, PathEndMode.OnCell, Danger.Some)) return cell;
            }
            return c.Position;
        }

        /// <summary>The colony's people nearby with nothing pressing, and the outsider's own company, gather round to watch a duel.</summary>
        private static void CallWitnesses(OutsiderBout bout)
        {
            var map = bout.a?.Map;
            if (map == null) return;
            var spots = GenRadial.RadialCellsAround(bout.ring, 5.5f, false)
                .Where(cell => cell.InBounds(map) && cell.DistanceTo(bout.ring) >= 3f && cell.Standable(map) && cell.GetFirstPawn(map) == null).ToList();
            int i = 0;
            void Call(Pawn p)
            {
                if (i >= spots.Count || p?.jobs == null) return;
                var cell = spots[i++];
                var job = JobMaker.MakeJob(OutsiderDefOf.Council_OutsiderWatch, bout.a, cell);
                job.locomotionUrgency = LocomotionUrgency.Jog;
                p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                if (p.CurJobDef == OutsiderDefOf.Council_OutsiderWatch) bout.watchers.Add(p);
            }
            foreach (var p in map.mapPawns.FreeColonistsSpawned.Where(p => p != bout.a && Together.Able(p) && !p.DevelopmentalStage.Baby() && PlayOuts.CanJoin(p)
                                                                           && PlayOuts.Interruptible(p) && !PlayOuts.NeedsPress(p) && p.Position.InHorDistOf(bout.ring, 45f)
                                                                           && p.CanReach(bout.ring, PathEndMode.Touch, Danger.Some))
                         .OrderBy(p => p.Position.DistanceToSquared(bout.ring)).Take(6).ToList())
                Call(p);
            var lord = bout.b.GetLord();
            if (lord != null)
                foreach (var p in lord.ownedPawns.Where(p => p != bout.b && IsOutsider(p) && Together.Able(p) && Unwilling(p, bout.a, OutsiderAct.Duel) == null
                                                             && p.CanReach(bout.ring, PathEndMode.Touch, Danger.Some)).Take(4).ToList())
                    Call(p);
        }

        /// <summary>A practice swing (from the bout job): the game's lunge, a hit by melee skill against the other's dodge, a blunt blow from nobody's weapon.</summary>
        public static void Swing(OutsiderBout bout, Pawn attacker, Pawn defender)
        {
            if (bout == null || bout.over || attacker?.Map == null || defender == null) return;
            try
            {
                attacker.Drawer?.Notify_MeleeAttackOn(defender);
                bool isA = attacker == bout.a;
                if (isA) bout.swingsA++;
                else bout.swingsB++;
                float chance = OutsiderMath.HitChance(attacker.GetStatValue(StatDefOf.MeleeHitChance), defender.GetStatValue(StatDefOf.MeleeDodgeChance));
                bool hit = Rand.Chance(chance);
                attacker.skills?.Learn(SkillDefOf.Melee, hit ? OutsiderMath.SparXpHit : OutsiderMath.SparXpSwing);
                if (!hit)
                {
                    SoundDefOf.Pawn_Melee_Punch_Miss?.PlayOneShot(new TargetInfo(defender.Position, defender.Map));
                    return;
                }
                if (isA) bout.hitsA++;
                else bout.hitsB++;
                bool hard = bout.hardAt >= 0 && bout.swingsA + bout.swingsB >= bout.hardAt;
                if (hard)
                {
                    bout.hardAt = -1;
                    bout.hurt = defender;
                }
                // Nobody's weapon and no instigator: practice, not an attack their people would count.
                var dinfo = new DamageInfo(DamageDefOf.Blunt, OutsiderMath.SparDamage(Rand.Value, hard), 0f, -1f, null, null, null, DamageInfo.SourceCategory.ThingOrUnknown, null, false);
                defender.TakeDamage(dinfo);
                SoundDefOf.Pawn_Melee_Punch_HitPawn?.PlayOneShot(new TargetInfo(defender.Position, defender.Map));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("outsider spar swing", e);
            }
        }

        /// <summary>The fighter is still in it: at the bout, or (a duel) fighting back on the game's own.</summary>
        private static bool StillIn(OutsiderBout bout, Pawn p)
        {
            if (p == null || p.Dead || !p.Spawned) return false;
            var d = p.CurJobDef;
            if (d == OutsiderDefOf.Council_OutsiderBout) return true;
            return bout.kind == BoutKind.Duel && d == JobDefOf.AttackMelee && p.CurJob.targetA.Thing == bout.Other(p);
        }

        /// <summary>New wounds since the bout began, the worst first: (bleeding, severity).</summary>
        private static bool Blooded(OutsiderBout bout, Pawn p)
        {
            foreach (var h in p.health.hediffSet.hediffs)
            {
                if (!(h is Hediff_Injury inj) || bout.before.Contains(h)) continue;
                if (inj.BleedRate >= OutsiderMath.FirstBloodBleed || inj.Severity >= OutsiderMath.FirstBloodSeverity) return true;
            }
            return false;
        }

        private static bool Yields(Pawn p, float pain) =>
            p.Downed || p.health.hediffSet.PainTotal >= pain || p.health.hediffSet.BleedRateTotal >= 0.5f;

        private static float YieldAt(Pawn p) =>
            OutsiderMath.YieldPain(HasNamed(p, "Tough"), HasNamed(p, "Wimp"), HasNamed(p, "Bloodlust"));

        /// <summary>The referee's look (the colonist's bout job, every half second): is it over by its terms?</summary>
        public static void Referee(OutsiderBout bout)
        {
            if (bout == null || bout.over) return;
            int now = Now;
            var a = bout.a;
            var b = bout.b;
            if (a == null || b == null || a.Dead || b.Dead || !a.Spawned || !b.Spawned || a.Map != b.Map)
            {
                bout.off = !(a?.Dead ?? true) && !(b?.Dead ?? true);
                bout.winner = a != null && !a.Dead && (b == null || b.Dead) ? a : b != null && !b.Dead && (a == null || a.Dead) ? b : null;
                End(bout);
                return;
            }
            if (bout.fightStart < 0) return;
            if (!StillIn(bout, a) || !StillIn(bout, b))
            {
                bout.off = true;
                End(bout);
                return;
            }
            if (a.Downed || b.Downed)
            {
                bout.winner = a.Downed && !b.Downed ? b : b.Downed && !a.Downed ? a : null;
                bout.draw = bout.winner == null;
                End(bout);
                return;
            }
            if (bout.kind == BoutKind.Spar)
            {
                bool yieldA = a.health.hediffSet.PainTotal >= OutsiderMath.SparYieldPain, yieldB = b.health.hediffSet.PainTotal >= OutsiderMath.SparYieldPain;
                bool done = bout.swingsA >= OutsiderMath.SparSwings && bout.swingsB >= OutsiderMath.SparSwings || now - bout.fightStart > OutsiderMath.SparMaxTicks;
                if (!yieldA && !yieldB && !done) return;
                if (yieldA != yieldB) bout.winner = yieldA ? b : a;
                else bout.winner = bout.hitsA > bout.hitsB ? a : bout.hitsB > bout.hitsA ? b : null;
                bout.draw = bout.winner == null;
                End(bout);
                return;
            }
            // A duel, by its terms.
            bool lostA, lostB;
            if (bout.terms == DuelTerms.FirstBlood)
            {
                lostA = Blooded(bout, a);
                lostB = Blooded(bout, b);
            }
            else
            {
                lostA = Yields(a, YieldAt(a));
                lostB = Yields(b, YieldAt(b));
            }
            if (lostA || lostB)
            {
                if (lostA && lostB)
                {
                    float pa = a.health.hediffSet.PainTotal, pb = b.health.hediffSet.PainTotal;
                    bout.winner = Mathf.Abs(pa - pb) < 0.05f ? null : pa < pb ? a : b;
                }
                else bout.winner = lostA ? b : a;
                bout.draw = bout.winner == null;
                End(bout);
                return;
            }
            if (now - bout.fightStart > OutsiderMath.DuelMaxTicks)
            {
                bout.draw = true;
                End(bout);
            }
        }

        /// <summary>The bout never came to blows (one of them never got to the ring, or was pulled away first): called off.</summary>
        public static void CallOff(OutsiderBout bout)
        {
            if (bout == null || bout.over) return;
            bout.off = true;
            End(bout);
        }

        /// <summary>The bout ends: both stop where they stand (no blow after the terms), and what comes of it.</summary>
        private static void End(OutsiderBout bout)
        {
            if (bout.over) return;
            bout.over = true;
            bout.endedAt = Now;
            foreach (var p in new[] { bout.a, bout.b })
            {
                if (p?.mindState == null) continue;
                if (p.mindState.meleeThreat == bout.Other(p)) p.mindState.meleeThreat = null;
                if (p.Spawned && p.CurJobDef == JobDefOf.AttackMelee && p.CurJob.targetA.Thing == bout.Other(p)) p.jobs.EndCurrentJob(JobCondition.InterruptForced);
                p.stances?.CancelBusyStanceSoft();
            }
            try
            {
                if (bout.kind == BoutKind.Spar) SparOver(bout);
                else if (bout.kind == BoutKind.Duel) DuelOver(bout);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("outsider bout over", e);
            }
        }

        private static void SparOver(OutsiderBout bout)
        {
            var c = bout.a;
            var x = bout.b;
            if (bout.off || c == null || x == null)
            {
                // apart: the bout never came to blows; said in a message.
                if (c != null && x != null) Msg("CC_Out_Msg_BoutOff", c, c.Named("PAWN"), x.Named("OTHER"));
                return;
            }
            if (bout.winner != null) Say(bout.winner, "sparwin", bout.Other(bout.winner));
            var loser = bout.winner != null ? bout.Other(bout.winner) : null;
            if (loser != null) Say(loser, "sparlose", bout.winner);
            // together: they sparred face to face; each remembers a good bout, unless the hard blow fell on them.
            if (bout.hurt != c) Memory(c, OutsiderDefOf.Rimfolk_OutsiderBout, x, 0);
            if (bout.hurt != x) Memory(x, OutsiderDefOf.Rimfolk_OutsiderBout, c, 0);
            Remember(c, x, OutsiderAct.Spar, Note(OutsiderAct.Spar, c), 1.5f, bout.hurt != null ? 0f : 4f);
            // Who got the better of it is in their bubbles; a real injury is worth a message.
            if (bout.hurt != null)
                Notify.Message("CC_Out_Msg_SparHard".Translate(bout.hurt.Named("HURT")).Resolve(), new LookTargets(bout.hurt), MessageTypeDefOf.NegativeHealthEvent, Importance.Important);
        }

        private static void DuelOver(OutsiderBout bout)
        {
            var c = bout.a;
            var x = bout.b;
            if (c == null || x == null) return;
            if (bout.off)
            {
                // apart: the duel stopped short; said in a message.
                Msg("CC_Out_Msg_DuelOff", c, c.Named("PAWN"), x.Named("OTHER"));
                return;
            }
            var f = PeopleOf(x);
            int theirs = bout.watchers.Count(w => w != null && w.Faction == x.Faction && !w.Dead);
            if (bout.winner != null && !bout.winner.Dead) Say(bout.winner, "duelwin", bout.Other(bout.winner));
            var loser = bout.winner != null ? bout.Other(bout.winner) : null;
            if (loser != null && !loser.Dead && !loser.Downed) Say(loser, "duelyield", bout.winner);
            // together: both fought it out face to face; each respects the other for a fair fight.
            if (!x.Dead) Memory(x, OutsiderDefOf.Rimfolk_OutsiderBout, c, 1);
            if (!c.Dead) Memory(c, OutsiderDefOf.Rimfolk_OutsiderBout, x, 1);
            bool won = bout.winner == c;
            bool lost = bout.winner == x;
            // Those of the colony who watched remember it.
            foreach (var w in bout.watchers)
                if (KinMemory.Ours(w) && !w.Dead && (won || lost))
                    CouncilGameComponent.GiveThought(w, OutsiderDefOf.Rimfolk_SawDuel, null, -1f, null, won ? 0 : 1);
            float regard = won ? OutsiderMath.HonourRegard(true) : lost ? OutsiderMath.HonourRegard(false) : 3f;
            Remember(c, x, OutsiderAct.Duel, Note(OutsiderAct.Duel, c, ("CC_Out_DuelWord_" + (won ? "Won" : lost ? "Lost" : "Draw")).Translate().Resolve()), regard, won || lost ? 4f : 3f);
            // Their notables who watched remember it too.
            string end = bout.winner != null ? "CC_Out_SawWon".Translate(bout.winner.LabelShort.Named("WINNER")).Resolve() : "CC_Out_SawDraw".Translate().Resolve();
            foreach (var w in bout.watchers)
            {
                var n = NotableOf(w);
                if (n == null || w == x) continue;
                n.Remember("CC_Out_Note_SawDuel".Translate(c.LabelShort.Named("PAWN"), x.LabelShort.Named("OTHER"), end.Named("HOW")).Resolve(), won ? 3f : 0f);
                n.RegardOf(c, true).personal += won ? 5f : 2f;
            }
            if (f != null)
            {
                if (won || lost) WorldSituationWorker.Goodwill(f, OutsiderMath.HonourGoodwill(won, theirs), true);
                if (won) KinMemory.Note(f, "duel", c, x.LabelShort);
            }
            NotableBook.Changed();
            if (bout.draw || bout.winner == null)
                Msg("CC_Out_Msg_DuelDraw", c, c.Named("PAWN"), x.Named("OTHER"));
            else
                Notify.Message((won ? "CC_Out_Msg_DuelWon" : "CC_Out_Msg_DuelLost").Translate(c.Named("PAWN"), x.Named("OTHER"), (f?.Name ?? "").Named("FACTION")).Resolve(), new LookTargets(c),
                    won ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NeutralEvent, Importance.Important);
        }

        // ================= The 250-tick look =================

        public static void Rare(int now)
        {
            if (meals.Count > 0) MealsRare(now);
            foreach (var b in bouts.ToList())
            {
                if (b.kind == BoutKind.Brawl)
                {
                    bool fighting = b.a?.MentalStateDef == MentalStateDefOf.SocialFighting || b.b?.MentalStateDef == MentalStateDefOf.SocialFighting;
                    if (!b.over && (!fighting || now - b.start > GenDate.TicksPerHour * 3))
                    {
                        b.over = true;
                        b.endedAt = now;
                        BrawlOver(b);
                    }
                }
                else if (!b.over)
                {
                    // A bout whose people never got to it (pulled away before the first blow) is called off.
                    bool aIn = b.a != null && b.a.Spawned && (b.a.CurJobDef == OutsiderDefOf.Council_OutsiderBout || b.a.CurJobDef == JobDefOf.AttackMelee);
                    bool bIn = b.b != null && b.b.Spawned && (b.b.CurJobDef == OutsiderDefOf.Council_OutsiderBout || b.b.CurJobDef == JobDefOf.AttackMelee);
                    if (b.fightStart < 0 && (now - b.start > 600 && (!aIn || !bIn) || now - b.start > 3000)) CallOff(b);
                    else if (b.fightStart >= 0 && (!aIn || !bIn)) Referee(b);
                    if (!b.over && now - b.start > OutsiderMath.DuelMaxTicks + 4000) CallOff(b);
                }
                if (b.over && now - b.endedAt > 600) bouts.Remove(b);
            }
        }
    }
}
