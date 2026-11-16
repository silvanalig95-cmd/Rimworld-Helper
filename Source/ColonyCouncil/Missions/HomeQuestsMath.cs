using System;

namespace ColonyCouncil
{
    /// <summary>The six single quests at home (quests, round 1: docs/quests-proposals.md, Q1, Q2, Q5, Q7, Q9 and Q10).</summary>
    public enum HomeQuest
    {
        /// <summary>Q1: a neighbour's prisoner kept in the colony's prison for some days.</summary>
        Prisoner,
        /// <summary>Q2: two of their people bring a quarrel to the colony to judge.</summary>
        Dispute,
        /// <summary>Q5: a wounded messenger at the gate, treated, and their letter kept until it is collected.</summary>
        Messenger,
        /// <summary>Q7: an outlaw hiding on the colony's land, captured or killed.</summary>
        Outlaw,
        /// <summary>Q9: a keepsake lost somewhere in a marked area of the colony's land, searched for.</summary>
        Keepsake,
        /// <summary>Q10: a particular wild animal on the colony's land, tamed and handed over.</summary>
        TameFor
    }

    /// <summary>How the prisoner's stay stands.</summary>
    public enum PrisonerEnd
    {
        Holding,
        /// <summary>Held the days agreed, still the colony's prisoner: their people collect them.</summary>
        Held,
        Escaped,
        Died,
        /// <summary>Recruited or enslaved by the colony.</summary>
        Kept,
        Released
    }

    /// <summary>The colony's judgement in a quarrel.</summary>
    public enum Ruling
    {
        /// <summary>For the one who came first ({X}).</summary>
        First,
        /// <summary>For the other ({DEAD}).</summary>
        Second,
        /// <summary>Split between them.</summary>
        Split,
        /// <summary>No judgement given before they gave up waiting.</summary>
        None
    }

    /// <summary>How the messenger's stay stands.</summary>
    public enum MessengerEnd
    {
        Waiting,
        /// <summary>The letter collected by their people.</summary>
        Delivered,
        /// <summary>The messenger died at the colony.</summary>
        Died,
        /// <summary>The letter is gone (burned, lost, taken away).</summary>
        Lost
    }

    /// <summary>How the hunt for the outlaw stands.</summary>
    public enum OutlawEnd
    {
        Hiding,
        Captured,
        Killed,
        /// <summary>Off the colony's land.</summary>
        Fled,
        /// <summary>Taken into the colony (recruited or enslaved) instead of handed over.</summary>
        Kept
    }

    /// <summary>How the animal they want stands.</summary>
    public enum TameEnd
    {
        Wild,
        Tamed,
        Killed,
        /// <summary>Wandered off the colony's land, or gone some other way.</summary>
        Gone,
        /// <summary>Tamed and bonded to someone of the colony: they leave it where it is loved.</summary>
        Bonded
    }

    /// <summary>
    /// The pure rules of the six quests at home (the workers in Workers_MissionHome.cs and Workers_MissionHome2.cs): their
    /// share of the missions' one draw, the reward by the asking people's age, the days, the odds, and what counts as done.
    /// No game types, so Tools/Tests can check it.
    /// </summary>
    public static class HomeQuestsMath
    {
        public const string PrisonerKind = "prisoner", DisputeKind = "dispute", MessengerKind = "messenger", OutlawKind = "outlaw", KeepsakeKind = "keepsake",
            TameKind = "tamefor";

        /// <summary>Their mission kinds, in the order of <see cref="HomeQuest"/>.</summary>
        public static readonly string[] Kinds = { PrisonerKind, DisputeKind, MessengerKind, OutlawKind, KeepsakeKind, TameKind };

        public static string KindOf(HomeQuest q) => Kinds[(int)q];

        // ---------------- Pace ----------------

        /// <summary>
        /// Each one's base chance: six of them take 1.8 of the missions' one draw (MissionPool), so missions as a whole
        /// come no oftener than before; each of the others gives up a little of its share.
        /// </summary>
        public const float BaseChance = 0.3f;

        /// <summary>The same quest not again for this long (days).</summary>
        public const int RestDays = 75;

        // ---------------- The reward ----------------

        /// <summary>The asking people's age as a number: 0 tribal, 1 medieval, 2 industrial, 3 spacer.</summary>
        public const int Tribal = 0, Medieval = 1, Industrial = 2, Spacer = 3;

        /// <summary>What their pay is worth by their age: a tribe has less to give, a spacer people more; the Empire as a spacer people.</summary>
        public static float AgeFactor(int age, bool empire)
        {
            if (empire) return 1.3f;
            switch (age)
            {
                case Tribal: return 0.8f;
                case Medieval: return 1f;
                case Industrial: return 1.15f;
                default: return 1.3f;
            }
        }

        /// <summary>A tribe pays in goods of its own (things of the colony's era worth as much); everyone else in silver.</summary>
        public static bool InGoods(int age, bool empire) => !empire && age <= Tribal;

        /// <summary>The silver each quest is worth before the people's age and the colony's wealth.</summary>
        public static void Base(HomeQuest q, out float min, out float max)
        {
            switch (q)
            {
                case HomeQuest.Prisoner: min = 160f; max = 260f; break;
                case HomeQuest.Dispute: min = 100f; max = 180f; break;
                case HomeQuest.Messenger: min = 180f; max = 300f; break;
                case HomeQuest.Outlaw: min = 200f; max = 320f; break;
                case HomeQuest.Keepsake: min = 120f; max = 220f; break;
                default: min = 200f; max = 340f; break;
            }
        }

        /// <summary>
        /// The reward: the quest's own range (by <paramref name="roll"/>, 0 to 1) times the people's age and the colony's
        /// wealth (LivingEvents.WealthFactor, 0.6 to 2), rounded to tens, thirty at least.
        /// </summary>
        public static int Reward(HomeQuest q, int age, bool empire, float wealth, float roll)
        {
            Base(q, out float min, out float max);
            float r = Math.Max(0f, Math.Min(1f, roll));
            float w = Math.Max(0.6f, Math.Min(2f, wealth));
            return Tens(Math.Max(30f, (min + (max - min) * r) * AgeFactor(age, empire) * w));
        }

        /// <summary>Rounded to tens.</summary>
        public static int Tens(float v) => (int)Math.Round(v / 10f, MidpointRounding.AwayFromZero) * 10;

        /// <summary>A captured outlaw is worth half as much again as a dead one: they want to try them.</summary>
        public const float CaptureFactor = 1.5f;

        public static int OutlawPay(int reward, bool captured) => captured ? Tens(reward * CaptureFactor) : reward;

        /// <summary>The goodwill a quest done well brings (the people thank the colony for the help, World/GoodwillWhy.cs).</summary>
        public static int Goodwill(HomeQuest q)
        {
            switch (q)
            {
                case HomeQuest.Prisoner: return 10;
                case HomeQuest.Dispute: return 8;
                case HomeQuest.Messenger: return 12;
                case HomeQuest.Outlaw: return 12;
                case HomeQuest.Keepsake: return 10;
                default: return 10;
            }
        }

        /// <summary>The goodwill a quest failed costs, before what failed it (a death is twice this).</summary>
        public static int Penalty(HomeQuest q) => q == HomeQuest.Dispute ? 4 : q == HomeQuest.Keepsake ? 4 : 8;

        // ---------------- The days ----------------

        /// <summary>
        /// The whole days the colony has (or keeps someone) once it agrees, by <paramref name="roll"/> (0 to 1): the
        /// prisoner held 4 to 7, the quarrel settled within 2, the messenger kept 4 to 6 until their people come, the
        /// outlaw found within 5 to 7, the keepsake within 3 to 4, the animal tamed within 6 to 9.
        /// </summary>
        public static int Days(HomeQuest q, float roll)
        {
            int min, max;
            switch (q)
            {
                case HomeQuest.Prisoner: min = 4; max = 7; break;
                case HomeQuest.Dispute: min = 2; max = 2; break;
                case HomeQuest.Messenger: min = 4; max = 6; break;
                case HomeQuest.Outlaw: min = 5; max = 7; break;
                case HomeQuest.Keepsake: min = 3; max = 4; break;
                default: min = 6; max = 9; break;
            }
            float r = Math.Max(0f, Math.Min(0.999f, roll));
            return min + (int)(r * (max - min + 1));
        }

        /// <summary>Days before the people a quest brings come to the colony after it agrees: about half a day, a day at most.</summary>
        public const float ComeMin = 0.4f, ComeMax = 1f;

        // ---------------- The prisoner ----------------

        /// <summary>
        /// Where the prisoner's stay stands, in this order: dead, taken in by the colony, let go, off the map (escaped),
        /// the days held, else still holding.
        /// </summary>
        public static PrisonerEnd PrisonerOutcome(bool dead, bool ours, bool released, bool onMap, bool heldDays)
        {
            if (dead) return PrisonerEnd.Died;
            if (ours) return PrisonerEnd.Kept;
            if (released) return PrisonerEnd.Released;
            if (!onMap) return PrisonerEnd.Escaped;
            return heldDays ? PrisonerEnd.Held : PrisonerEnd.Holding;
        }

        // ---------------- The quarrel ----------------

        /// <summary>
        /// How many times the one the judgement went against tries the game's own insult on the other before they leave
        /// (each a roll of the game's own social fight): three when one side lost, one when nobody judged it, none when
        /// it was split between them.
        /// </summary>
        public static int Insults(Ruling r) => r == Ruling.Split ? 0 : r == Ruling.None ? 1 : 3;

        /// <summary>The goodwill a ruling brings: all of it for a split, three quarters when one side lost, the penalty when nobody judged.</summary>
        public static int RulingGoodwill(Ruling r, int goodwill, int penalty) =>
            r == Ruling.Split ? goodwill : r == Ruling.None ? -penalty : (int)Math.Round(goodwill * 0.75f, MidpointRounding.AwayFromZero);

        /// <summary>The ruling's tag for the texts.</summary>
        public static string Tag(Ruling r) => r == Ruling.First ? "first" : r == Ruling.Second ? "second" : r == Ruling.Split ? "split" : "none";

        /// <summary>Hours they wait at the gate for the colony's judgement once the question is put.</summary>
        public const float JudgeHours = 12f;

        // ---------------- The messenger ----------------

        /// <summary>Wounds the messenger comes in with: two or three.</summary>
        public const int WoundsMin = 2, WoundsMax = 3;

        /// <summary>Each wound takes this share of its part's hit points.</summary>
        public const float WoundShareMin = 0.2f, WoundShareMax = 0.45f;

        /// <summary>Never closer to bleeding to death than this when they reach the gate (days): time to be treated.</summary>
        public const float MinBleedDays = 1.5f;

        /// <summary>Where the messenger's stay stands: the messenger dead, the letter gone, the letter collected, else waiting.</summary>
        public static MessengerEnd MessengerOutcome(bool dead, bool letterGone, bool collected)
        {
            if (dead) return MessengerEnd.Died;
            if (collected) return MessengerEnd.Delivered;
            return letterGone ? MessengerEnd.Lost : MessengerEnd.Waiting;
        }

        // ---------------- The outlaw ----------------

        /// <summary>
        /// The outlaw's strength, scaled to the colony: a third of the storyteller's threat points, between an ordinary
        /// fighter (45) and a hardened one (180). The game's own pawn of that strength or the nearest below it.
        /// </summary>
        public static float OutlawPower(float threatPoints) => Math.Max(45f, Math.Min(180f, threatPoints * 0.35f));

        /// <summary>
        /// Found, the outlaw fights back this often (through the shared harm budget, HarmKinds.Outlaw); the rest of the
        /// time they run for the edge of the map and fight only when cornered.
        /// </summary>
        public const float FightChance = 0.75f;

        /// <summary>One of the colony's people within this many cells, in sight, has found the outlaw.</summary>
        public const float FoundRange = 10f;

        /// <summary>The hiding place is at least this far from the middle of the colony.</summary>
        public const float HideMin = 35f;

        /// <summary>Where the outlaw stands: dead, taken into the colony, held as its prisoner, off the map, else hiding.</summary>
        public static OutlawEnd OutlawOutcome(bool dead, bool ours, bool prisoner, bool onMap)
        {
            if (dead) return OutlawEnd.Killed;
            if (ours) return OutlawEnd.Kept;
            if (prisoner) return OutlawEnd.Captured;
            return onMap ? OutlawEnd.Hiding : OutlawEnd.Fled;
        }

        // ---------------- The keepsake ----------------

        /// <summary>The marked area's radius in cells: about 150 cells to search.</summary>
        public const float AreaRadius = 7f;

        /// <summary>One look covers the ground this far around where it is made.</summary>
        public const float LookRadius = 2.9f;

        /// <summary>One look takes this long (ticks, about an hour of game time).</summary>
        public const int LookTicks = 2000;

        /// <summary>The keepsake lies at least this far inside the area's edge, so a search of the area finds it.</summary>
        public const float AreaMargin = 2f;

        /// <summary>Hours of rain or snow on it, lying out in the open, before it is ruined.</summary>
        public const int WetHoursToSpoil = 6;

        /// <summary>Rain or snow is falling (the game's weather: its rain and snow rates).</summary>
        public static bool Wet(float rainRate, float snowRate) => rainRate > 0.1f || snowRate > 0.1f;

        /// <summary>A look made <paramref name="dx"/>, <paramref name="dz"/> cells from the keepsake finds it.</summary>
        public static bool Covers(int dx, int dz) => dx * dx + dz * dz <= LookRadius * LookRadius;

        /// <summary>Ruined by the weather after this many wet hours.</summary>
        public static bool Spoiled(int wetHours) => wetHours >= WetHoursToSpoil;

        // ---------------- The animal ----------------

        /// <summary>Where the animal they want stands: dead, tamed (bonded or not), off the map, else wild.</summary>
        public static TameEnd TameOutcome(bool dead, bool ours, bool onMap, bool bonded)
        {
            if (dead) return TameEnd.Killed;
            if (ours) return bonded ? TameEnd.Bonded : TameEnd.Tamed;
            return onMap ? TameEnd.Wild : TameEnd.Gone;
        }

        /// <summary>Hours after it is tamed before their handler comes for it.</summary>
        public const float HandlerHours = 12f;
    }
}
