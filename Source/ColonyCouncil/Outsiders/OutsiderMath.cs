using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>What a colonist can do with someone from outside the colony (Outsiders/Outsiders.cs), in the menu's order.</summary>
    public enum OutsiderAct
    {
        Chat,
        AskHome,
        Meal,
        Drink,
        Gift,
        GoodWord,
        Letter,
        Teach,
        Bring,
        Invite,
        Befriend,
        Court,
        Faith,
        Spar,
        Provoke,
        Duel,
        /// <summary>Ask a favour of their people: call in a debt of honour they owe (Diplomacy/DiplomacyActs.cs; later stage 2 acts follow it).</summary>
        AskFavour,
        /// <summary>Speak of a pact between their people and the colony, face to face (Diplomacy/DiplomacyActs.cs, release 2a.2).</summary>
        SpeakPact,
        /// <summary>Offer an exchange of captives, one of theirs in the colony's cells for one of the colony's they hold (Diplomacy/DiplomacyActs.cs, release 2a.3).</summary>
        OfferExchange,
        /// <summary>Speak for a match between one of the colony's and one of theirs, face to face (Diplomacy/MatchActs.cs, release 2b.2).</summary>
        SpeakMatch,
        /// <summary>
        /// Obsolete: the keeper of secrets' sounding out (release 2c.1), dropped with the seat on 5 November 2026. Kept in
        /// its place, as a talk under way keeps its act by its number (JobDriver_OutsiderTalk); never offered, and a talk an
        /// older save holds under way ends quietly (<see cref="OutsiderMath.Retired"/>).
        /// </summary>
        SoundOut
    }

    /// <summary>How a talk went, best first.</summary>
    public enum TalkTone
    {
        Warm,
        Good,
        Plain,
        Cool,
        Sour
    }

    /// <summary>When a duel of honour stops.</summary>
    public enum DuelTerms
    {
        /// <summary>The first wound that bleeds (or, with blunt weapons, the first that tells).</summary>
        FirstBlood,
        /// <summary>When one of them can't go on: hurt enough that they give in, or down.</summary>
        Yield
    }

    /// <summary>A fight between two: practice, a duel of honour, or a brawl after hard words.</summary>
    public enum BoutKind
    {
        Spar,
        Duel,
        Brawl
    }

    /// <summary>How a talk about faith ended.</summary>
    public enum FaithEnd
    {
        /// <summary>The same faith: it brought them closer.</summary>
        Closer,
        /// <summary>Different faiths, and the outsider came away a little less sure of theirs.</summary>
        Softened,
        /// <summary>Different faiths, and they agreed to differ.</summary>
        Agreed,
        /// <summary>Different faiths, and it hardened them both.</summary>
        Hardened
    }

    /// <summary>What a talk is worked out from (no game types).</summary>
    public struct TalkInput
    {
        public OutsiderAct act;
        /// <summary>How the two get on (Bonds.Affinity: the game's compatibility, faith, temper, shared passions), about -3 to 3.</summary>
        public float affinity;
        /// <summary>How the outsider regards the colony, -100 to 100 (their people's goodwill and memory, or their own as a notable).</summary>
        public int ofColony;
        /// <summary>How the outsider regards the colonist (the game's opinion), -100 to 100.</summary>
        public int ofColonist;
        /// <summary>The colonist's Social skill, 0 to 20.</summary>
        public int social;
        /// <summary>The outsider's nature, -2 (cold, abrasive) to 2 (kind).</summary>
        public int warmth;
        /// <summary>How well the two know each other from their talks before, 0 to 100.</summary>
        public float familiarity;
    }

    /// <summary>
    /// The pure rules of talking with people from outside the colony (Outsiders/): the wait between two things done
    /// together, how a talk goes and what it changes on both sides, a bond that grows over visits, gifts that suit, a
    /// good word, favours, an invitation to stay, faith, practice bouts, hard words and duels of honour. No game types,
    /// so Tools/Tests can check it.
    /// </summary>
    public static class OutsiderMath
    {
        public const int TicksPerHour = 2500;
        public const int TicksPerDay = 60000;

        /// <summary>An act no longer offered (the keeper's sounding out): a talk an older save holds under way with it ends quietly.</summary>
        public static bool Retired(OutsiderAct a) => a == OutsiderAct.SoundOut;

        // ---------------- The wait between two things done together ----------------

        /// <summary>Hours before the same two do the same thing again (or, for <see cref="PerOutsider"/>, anyone does it with the outsider).</summary>
        public static float WaitHours(OutsiderAct a)
        {
            switch (a)
            {
                case OutsiderAct.Chat: return 6f;
                case OutsiderAct.AskHome: return 24f;
                case OutsiderAct.Meal: return 10f;
                case OutsiderAct.Drink: return 8f;
                case OutsiderAct.Gift: return 48f;
                case OutsiderAct.GoodWord: return 120f;
                case OutsiderAct.Letter: return 24f;
                case OutsiderAct.Teach: return 48f;
                case OutsiderAct.Bring: return 72f;
                case OutsiderAct.Invite: return 72f;
                case OutsiderAct.Befriend: return 12f;
                case OutsiderAct.Court: return 24f;
                case OutsiderAct.Faith: return 24f;
                case OutsiderAct.Spar: return 12f;
                case OutsiderAct.Provoke: return 24f;
                default: return 240f;
            }
        }

        /// <summary>Things an outsider does once for the whole colony a while, whoever asks: news of home, a good word, a favour, an invitation, a duel.</summary>
        public static bool PerOutsider(OutsiderAct a) =>
            a == OutsiderAct.AskHome || a == OutsiderAct.GoodWord || a == OutsiderAct.Teach || a == OutsiderAct.Bring || a == OutsiderAct.Invite || a == OutsiderAct.Duel;

        /// <summary>The pointed ones: a practice bout, hard words, a duel.</summary>
        public static bool Pointed(OutsiderAct a) => a == OutsiderAct.Spar || a == OutsiderAct.Provoke || a == OutsiderAct.Duel;

        /// <summary>Waited long enough since <paramref name="last"/> (-1: never).</summary>
        public static bool Waited(int last, int now, OutsiderAct a) => last < 0 || now - last >= WaitHours(a) * TicksPerHour;

        /// <summary>How long the talk itself takes, in ticks (a chat a few minutes, a debate or a proposal longer).</summary>
        public static int TalkTicks(OutsiderAct a)
        {
            switch (a)
            {
                case OutsiderAct.Chat:
                case OutsiderAct.Befriend: return 700;
                case OutsiderAct.AskHome:
                case OutsiderAct.Faith:
                case OutsiderAct.Court:
                case OutsiderAct.Invite: return 900;
                case OutsiderAct.Provoke: return 400;
                case OutsiderAct.Gift:
                case OutsiderAct.Letter: return 300;
                default: return 450;
            }
        }

        // ---------------- How a talk goes ----------------

        /// <summary>What the talk is about, before anything else: an easy chat, news freely given, a harder thing asked.</summary>
        public static float ActBase(OutsiderAct a)
        {
            switch (a)
            {
                case OutsiderAct.Chat: return 6f;
                case OutsiderAct.AskHome: return 4f;
                case OutsiderAct.Meal:
                case OutsiderAct.Drink: return 10f;
                case OutsiderAct.Befriend: return 2f;
                case OutsiderAct.Court: return -4f;
                case OutsiderAct.Faith: return -2f;
                default: return 0f;
            }
        }

        /// <summary>
        /// How well it lands, before luck: how they regard the colonist (0.3) and the colony (0.2), how the two get on (8
        /// a step), the colonist's way with words (1.2 a level above 6), the outsider's nature (4 a step), how well they
        /// know each other (up to 6), and what it is about.
        /// </summary>
        public static float Score(TalkInput x) =>
            Clamp(x.ofColonist, -100, 100) * 0.3f + Clamp(x.ofColony, -100, 100) * 0.2f + Math.Max(-3f, Math.Min(3f, x.affinity)) * 8f
            + (Clamp(x.social, 0, 20) - 6) * 1.2f + Clamp(x.warmth, -2, 2) * 4f + Math.Max(0f, Math.Min(100f, x.familiarity)) * 0.06f + ActBase(x.act);

        /// <summary>How far luck moves a talk, either way.</summary>
        public const float Spread = 18f;

        /// <summary>How it went, from how well it lands and a roll (0..1).</summary>
        public static TalkTone Tone(TalkInput x, float roll)
        {
            float s = Score(x) + (Clamp01(roll) * 2f - 1f) * Spread;
            if (s >= 28f) return TalkTone.Warm;
            if (s >= 10f) return TalkTone.Good;
            if (s >= -8f) return TalkTone.Plain;
            if (s >= -24f) return TalkTone.Cool;
            return TalkTone.Sour;
        }

        /// <summary>The stage of the memory each keeps of the talk (Rimfolk_OutsiderTalk), or -1 for a plain one that leaves none.</summary>
        public static int ToneStage(TalkTone t)
        {
            switch (t)
            {
                case TalkTone.Warm: return 0;
                case TalkTone.Good: return 1;
                case TalkTone.Cool: return 2;
                case TalkTone.Sour: return 3;
                default: return -1;
            }
        }

        /// <summary>What the talk adds to the bond between the two (their record, which grows over visits).</summary>
        public static float WarmthShift(TalkTone t)
        {
            switch (t)
            {
                case TalkTone.Warm: return 8f;
                case TalkTone.Good: return 4f;
                case TalkTone.Plain: return 1f;
                case TalkTone.Cool: return -3f;
                default: return -6f;
            }
        }

        /// <summary>How a notable's regard of the colonist moves with it (the colony gets half).</summary>
        public static float RegardShift(TalkTone t)
        {
            switch (t)
            {
                case TalkTone.Warm: return 4f;
                case TalkTone.Good: return 2f;
                case TalkTone.Plain: return 0.5f;
                case TalkTone.Cool: return -1.5f;
                default: return -3f;
            }
        }

        /// <summary>How much better the two know each other after it: a talk a little, a meal, a lesson or a bout more, easier for two who get on.</summary>
        public static float FamiliarityGain(OutsiderAct a, float affinity)
        {
            float b;
            switch (a)
            {
                case OutsiderAct.Befriend: b = 12f; break;
                case OutsiderAct.Meal:
                case OutsiderAct.Drink:
                case OutsiderAct.Teach:
                case OutsiderAct.Spar:
                case OutsiderAct.Duel: b = 8f; break;
                case OutsiderAct.Court:
                case OutsiderAct.Faith: b = 6f; break;
                case OutsiderAct.Provoke: b = 2f; break;
                default: b = 4f; break;
            }
            float f = 1f + Math.Max(-0.5f, Math.Min(0.5f, affinity * 0.2f));
            return b * f;
        }

        public const float KnowMax = 100f;
        public const float WarmthMax = 100f;

        /// <summary>The bond between a colonist and an outsider halves in this many days without a visit.</summary>
        public const float HalfLifeDays = 60f;

        public static float Fade(float v, float days) => days <= 0f ? v : (float)(v * Math.Pow(0.5, days / HalfLifeDays));

        /// <summary>A talk more than this many days after the last one between the two counts as a new visit.</summary>
        public const float NewVisitDays = 2f;

        public static bool NewVisit(int lastTick, int now) => lastTick < 0 || now - lastTick > NewVisitDays * TicksPerDay;

        // ---------------- Who is willing ----------------

        /// <summary>Someone who regards the colonist this badly, or the colony this badly, won't stop for them.</summary>
        public const int ShunColonist = -40;
        public const int ShunColony = -60;

        public static bool Shuns(int ofColony, int ofColonist) => ofColonist <= ShunColonist || ofColony <= ShunColony;

        // ---------------- A gift ----------------

        /// <summary>
        /// How much a gift counts, by whether it suits them (beer for a drinker, a book for a scholar, a weapon for a
        /// fighter) and is fine (good quality or better): 1, 1.5 suiting, 2 suiting and fine. Against their beliefs or
        /// their diet, it counts for nothing.
        /// </summary>
        public static float GiftFit(bool suits, bool fine, bool against)
        {
            if (against) return 0f;
            if (!suits) return fine ? 1.15f : 1f;
            return fine ? 2f : 1.5f;
        }

        /// <summary>The share of a stack given: worth about this much silver, at least one, at most the stack.</summary>
        public const float GiftWorth = 120f;

        public static int GiftCount(float unitValue, int stack)
        {
            if (stack <= 1) return Math.Max(0, stack);
            if (unitValue <= 0f) return Math.Min(stack, 10);
            int n = (int)Math.Ceiling(GiftWorth / unitValue);
            return Math.Max(1, Math.Min(stack, n));
        }

        /// <summary>
        /// How much warmer the gift makes them toward the giver: by its worth against the colony's wealth (a colony with
        /// little gives more by giving the same), times how it suits them, 1 to 20.
        /// </summary>
        public static float GiftWarmth(float value, float fit, float wealthFactor)
        {
            if (fit <= 0f || value <= 0f) return 0f;
            float w = value / Math.Max(0.5f, wealthFactor) / 12f;
            return Math.Max(1f, Math.Min(20f, w * fit));
        }

        /// <summary>The stage of the gift's memory: 0 it suited me, 1 a kind gift.</summary>
        public static int GiftStage(float fit) => fit >= 1.5f ? 0 : 1;

        /// <summary>
        /// Goodwill for a gift when the game's own reckoning can't be read: its worth over forty, and less the better
        /// their people already think of the colony (as the game does it).
        /// </summary>
        public static int GiftGoodwill(float value, int goodwill)
        {
            if (value <= 0f) return 0;
            float g = value / 40f;
            float f = goodwill >= 75 ? 0.25f : goodwill >= 50 ? 0.5f : goodwill >= 0 ? 0.75f : 1f;
            return (int)Math.Round(g * f);
        }

        /// <summary>A gift handed to one person counts for this share of the game's goodwill for a gift to their people.</summary>
        public const float HandGiftShare = 0.5f;

        // ---------------- A good word ----------------

        /// <summary>They put in a good word only when they think this well of the colony and of the one asking.</summary>
        public const int GoodWordColony = 20;
        public const int GoodWordColonist = 10;

        public static bool WillPutInWord(int ofColony, int ofColonist) => ofColony >= GoodWordColony && ofColonist >= GoodWordColonist;

        /// <summary>How much better their notable at home thinks of the colony for it: 2 to 10, by their regard and the asker's words.</summary>
        public static float GoodWordShift(int ofColony, int ofColonist, int social)
        {
            float v = 2f + Clamp(ofColony, 0, 100) * 0.05f + Clamp(ofColonist, 0, 100) * 0.04f + (Clamp(social, 0, 20) - 6) * 0.2f;
            return Math.Max(2f, Math.Min(10f, v));
        }

        /// <summary>A good word not yet carried home after this many days (they stayed on, or never went) is let go.</summary>
        public const float GoodWordDays = 20f;

        // ---------------- A favour: a lesson ----------------

        /// <summary>A lesson: they know the skill to this level at least, and this many levels better than the colonist.</summary>
        public const int TeachLevel = 6;
        public const int TeachLead = 3;
        /// <summary>An hour.</summary>
        public const int TeachTicks = 2500;

        /// <summary>The skill worth teaching: where the teacher leads by most (and enough). -1: none.</summary>
        public static int TeachPick(IList<int> teacher, IList<int> learner, IList<bool> barred)
        {
            int best = -1, bestLead = 0;
            if (teacher == null || learner == null) return -1;
            for (int i = 0; i < teacher.Count && i < learner.Count; i++)
            {
                if (barred != null && i < barred.Count && barred[i]) continue;
                int lead = teacher[i] - learner[i];
                if (teacher[i] < TeachLevel || lead < TeachLead) continue;
                if (lead > bestLead)
                {
                    best = i;
                    bestLead = lead;
                }
            }
            return best;
        }

        /// <summary>Experience a tick of the lesson, by how far the teacher leads: 0.08 to 0.25 (an hour gives 200 to 625).</summary>
        public static float TeachXpPerTick(int teacher, int learner) => Math.Max(0.08f, Math.Min(0.25f, 0.08f + 0.012f * (teacher - learner)));

        /// <summary>They do the colony a favour when they think at least this well of it and don't mind the one asking.</summary>
        public const int FavourColony = 5;

        public static bool WillDoFavour(int ofColony, int ofColonist) => ofColony >= FavourColony && ofColonist >= 0;

        // ---------------- A favour: something brought next time ----------------

        /// <summary>Asking them to bring something next time needs a little more regard.</summary>
        public const int BringColony = 15;

        public static bool WillBring(int ofColony, int ofColonist) => ofColony >= BringColony && ofColonist >= 0;

        /// <summary>How many they bring: worth 60 silver, up to 160 the better they regard the colony; at least one.</summary>
        public static int BringCount(float unitValue, int ofColony)
        {
            float worth = 60f + Clamp(ofColony, 0, 100) * 1f;
            if (unitValue <= 0f) return 10;
            return Math.Max(1, (int)Math.Round(worth / unitValue));
        }

        /// <summary>Their people come next time no sooner than this many days after asking (not the same visit).</summary>
        public const float BringSoonestDays = 1f;
        /// <summary>A favour nobody came for in this many days is let go.</summary>
        public const float BringGiveUpDays = 30f;

        // ---------------- An invitation to stay ----------------

        /// <summary>
        /// The chance they accept: each reason to stay (love here, family here, hard times at home, the colony's faith, a
        /// friend here) adds, each tie at home takes away; how they regard the colony and the one asking, and the asker's
        /// words. Never without a reason, 85% at most.
        /// </summary>
        public static float InviteChance(int reasons, int ties, int ofColony, int ofColonist, int social)
        {
            if (reasons <= 0) return 0f;
            float c = 0.12f + reasons * 0.18f - ties * 0.15f + Clamp(ofColony, -100, 100) * 0.003f + Clamp(ofColonist, -100, 100) * 0.003f
                      + (Clamp(social, 0, 20) - 6) * 0.01f;
            return Math.Max(0.02f, Math.Min(0.85f, c));
        }

        /// <summary>Goodwill their people lose when one of theirs stays: less with a reason that speaks for it.</summary>
        public static int InviteGoodwill(int reasons) => reasons >= 2 ? -2 : -4;

        // ---------------- A bond over visits ----------------

        /// <summary>Friends from away: a bond this warm, known this well, over this many visits.</summary>
        public const float FriendWarmth = 35f;
        public const float FriendKnowing = 30f;
        public const int FriendVisits = 2;

        public static bool FriendsNow(float warmth, float familiarity, int visits) => warmth >= FriendWarmth && familiarity >= FriendKnowing && visits >= FriendVisits;

        /// <summary>Courting needs them to know each other this well first.</summary>
        public const float CourtKnowing = 15f;
        /// <summary>Sweethearts become lovers with a bond this warm, over this many visits since they became sweethearts.</summary>
        public const float LoversWarmth = 45f;
        public const int LoversVisits = 2;

        /// <summary>The chance a courting talk goes well: the game's own romance chance, more the better they know each other.</summary>
        public static float CourtChance(float gameChance, float familiarity) =>
            Clamp01(gameChance) * (0.6f + 0.4f * Math.Max(0f, Math.Min(KnowMax, familiarity)) / KnowMax);

        // ---------------- Faith ----------------

        /// <summary>
        /// How a talk about faith ends: the same faith brings them closer; different faiths make a debate that softens
        /// the outsider (the colonist's words, their own doubt), ends in agreeing to differ, or hardens them both.
        /// </summary>
        public static FaithEnd Faith(bool same, float theirCertainty, int social, int theirSocial, float affinity, float roll)
        {
            if (same) return FaithEnd.Closer;
            float s = (Clamp(social, 0, 20) - Clamp(theirSocial, 0, 20)) * 0.025f + (0.6f - Clamp01(theirCertainty)) * 0.6f + Math.Max(-3f, Math.Min(3f, affinity)) * 0.06f;
            float r = Clamp01(roll) + s;
            if (r >= 0.72f) return FaithEnd.Softened;
            if (r >= 0.3f) return FaithEnd.Agreed;
            return FaithEnd.Hardened;
        }

        /// <summary>How much surer (or less sure) of their faith each comes away: small, the game's own certainty; never a change of faith.</summary>
        public static float CertaintyShift(FaithEnd e, bool outsider)
        {
            switch (e)
            {
                case FaithEnd.Closer: return 0.02f;
                case FaithEnd.Softened: return outsider ? -0.04f : 0.01f;
                case FaithEnd.Hardened: return 0.02f;
                default: return 0f;
            }
        }

        /// <summary>What the faith talk adds to the bond between them.</summary>
        public static float FaithWarmth(FaithEnd e)
        {
            switch (e)
            {
                case FaithEnd.Closer: return 7f;
                case FaithEnd.Softened: return 3f;
                case FaithEnd.Agreed: return 2f;
                default: return -4f;
            }
        }

        // ---------------- Temper ----------------

        // ---------------- A practice bout ----------------

        /// <summary>The chance they agree to a bout: a fighting temper more, a fighter better than the colonist more, a wimp seldom.</summary>
        public static float SparAcceptChance(int temper, int theirMelee, int colonistMelee)
        {
            float c = 0.5f + Clamp(temper, -2, 4) * 0.12f + (Clamp(theirMelee, 0, 20) - Clamp(colonistMelee, 0, 20)) * 0.02f;
            return Math.Max(0.05f, Math.Min(0.95f, c));
        }

        /// <summary>Swings each, at most, and the bout's longest.</summary>
        public const int SparSwings = 8;
        public const int SparMaxTicks = 2000;
        /// <summary>Ticks between one fighter's swings in practice.</summary>
        public const int SparSwingTicks = 110;
        /// <summary>Someone hurting this much gives the bout up.</summary>
        public const float SparYieldPain = 0.2f;

        /// <summary>The chance a swing lands: the attacker's melee hit chance against the other's dodge, 15% to 95%.</summary>
        public static float HitChance(float hit, float dodge) => Math.Max(0.15f, Math.Min(0.95f, Clamp01(hit) * (1f - Clamp01(dodge))));

        /// <summary>
        /// The chance, once a bout, that one blow goes in harder than meant (a real injury: a cracked rib, a bloody nose
        /// gone bad): 4%, a point more for each step of temper between them above none, 8% at most. As often as a social
        /// fight leaves more than bruises in the game.
        /// </summary>
        public static float SparHardBlowChance(int temperA, int temperB) =>
            Math.Max(0.02f, Math.Min(0.08f, 0.04f + (Math.Max(0, temperA) + Math.Max(0, temperB)) * 0.01f));

        /// <summary>A practice blow's force (blunt): 1 to 2.5; a hard one 6 to 9.</summary>
        public static float SparDamage(float roll, bool hard) => hard ? 6f + Clamp01(roll) * 3f : 1f + Clamp01(roll) * 1.5f;

        /// <summary>Melee experience for each swing, and more for one that lands.</summary>
        public const float SparXpSwing = 15f;
        public const float SparXpHit = 25f;

        // ---------------- Hard words ----------------

        /// <summary>
        /// The chance hard words end in a fistfight: the game's own chance for an insult between them, much raised by a
        /// hot temper on their side (or the colonist's), lowered by a kind one; less the better they thought of the
        /// colonist. 0 to 70%.
        /// </summary>
        public static float FightChance(int theirTemper, int colonistTemper, int ofColonist, float gameChance)
        {
            float c = Clamp01(gameChance) * 2f + Math.Max(0, theirTemper) * 0.14f + Math.Max(0, colonistTemper) * 0.05f + Math.Min(0, theirTemper) * 0.05f
                      - Clamp(ofColonist, -100, 100) * 0.002f;
            return Math.Max(0f, Math.Min(0.7f, c));
        }

        /// <summary>What hard words take from the bond between them, and from a notable's regard of the colonist (the colony half).</summary>
        public const float ProvokeWarmth = -12f;
        public const float ProvokeRegard = -6f;

        /// <summary>Goodwill their people lose for a fistfight with one of theirs.</summary>
        public const int BrawlGoodwill = -4;

        // ---------------- A duel of honour ----------------

        public const int Neolithic = 2;
        public const int Medieval = 3;

        /// <summary>A people whose ways know the duel of honour: tribes and medieval folk, and an empire's nobility.</summary>
        public static bool DuelFits(int tech, bool royalTitles, bool titled) => tech <= Medieval || royalTitles && titled;

        /// <summary>
        /// The chance they take up the challenge: a fighting temper more, being the better fighter more, a low regard for
        /// the colony more (a matter of honour), first blood more readily than to the yield.
        /// </summary>
        public static float DuelAcceptChance(int temper, int theirMelee, int colonistMelee, int ofColony, DuelTerms terms)
        {
            float c = 0.45f + Clamp(temper, -2, 4) * 0.1f + (Clamp(theirMelee, 0, 20) - Clamp(colonistMelee, 0, 20)) * 0.025f - Clamp(ofColony, -100, 100) * 0.002f
                      + (terms == DuelTerms.FirstBlood ? 0.1f : -0.05f);
            return Math.Max(0.05f, Math.Min(0.95f, c));
        }

        /// <summary>Pain at which a duellist gives in under the yield terms: 0.35, a tough one 0.5, a wimp 0.2, bloodlust 0.45.</summary>
        public static float YieldPain(bool tough, bool wimp, bool bloodlust)
        {
            float p = 0.35f;
            if (tough) p += 0.15f;
            if (bloodlust) p += 0.1f;
            if (wimp) p -= 0.15f;
            return Math.Max(0.15f, Math.Min(0.6f, p));
        }

        /// <summary>Bleeding this fast counts as first blood (or, with blunt weapons, an injury this bad).</summary>
        public const float FirstBloodBleed = 0.01f;
        public const float FirstBloodSeverity = 4f;
        /// <summary>A duel that runs this long without an end is called a draw.</summary>
        public const int DuelMaxTicks = 4000;

        /// <summary>Goodwill a duel moves with their people: a win gains honour (more with more of theirs watching), a loss costs a little.</summary>
        public static int HonourGoodwill(bool won, int theirWitnesses) => won ? Math.Min(5, 2 + Math.Max(0, theirWitnesses)) : -1;

        /// <summary>How a notable's regard of the colonist moves: a win much, a loss still a little (they stood up), the colony half of it.</summary>
        public static float HonourRegard(bool won) => won ? 8f : 2f;

        // ---------------- Arithmetic ----------------

        public static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;

        public static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
