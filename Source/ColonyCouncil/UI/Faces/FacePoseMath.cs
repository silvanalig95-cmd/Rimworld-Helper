using System;

namespace ColonyCouncil
{
    /// <summary>Which way a portrait faces: the game's front view (south), or a side view (east, to the right; west, to the left).</summary>
    public enum FaceTurn
    {
        Front,
        Right,
        Left
    }

    /// <summary>How a person holds themselves in their portrait, by who they are and how they are.</summary>
    public enum FaceBearing
    {
        Plain,
        /// <summary>Chin up: a proud trait, a council seat, a people's head, an Empire title.</summary>
        Proud,
        /// <summary>The head a little down: sad, grieving someone close, in a sad break.</summary>
        Low,
        /// <summary>A lean: in an angry break or just out of a quarrel.</summary>
        Angry,
        /// <summary>Turned half away: a shy trait.</summary>
        Shy,
        /// <summary>A slight stoop: old for their kind.</summary>
        Old,
        /// <summary>The head low: a prisoner.</summary>
        Captive,
        /// <summary>Drawn grey.</summary>
        Dead
    }

    /// <summary>A portrait's pose: which way it faces and how the person holds themselves.</summary>
    public struct FacePose
    {
        public FaceTurn turn;
        public FaceBearing bearing;

        public FacePose(FaceTurn turn, FaceBearing bearing)
        {
            this.turn = turn;
            this.bearing = bearing;
        }

        public override string ToString() => turn + "/" + bearing;
    }

    /// <summary>
    /// Where the head sits on the body for a pose, drawn apart from it: moved by <see cref="dx"/> and <see cref="dy"/>
    /// head sizes (right and up), turned by <see cref="tilt"/> degrees about the neck (clockwise on the screen), and the
    /// whole figure leaning by <see cref="lean"/> degrees about its feet.
    /// </summary>
    public struct HeadShift
    {
        public float dx, dy, tilt, lean;

        /// <summary>The head moves on the body: it is drawn apart from it.</summary>
        public bool Apart => dx != 0f || dy != 0f || tilt != 0f;

        public bool None => !Apart && lean == 0f;
    }

    /// <summary>What the game says of a person that a pose is chosen from (gathered by the face engine).</summary>
    public struct BearingFacts
    {
        public bool dead, captive;
        /// <summary>In a mental state of anger (berserk, a tantrum, an insulting spree, a social fight) or of sadness (a sad wander, hiding in their room, giving up).</summary>
        public bool angryState, sadState;
        /// <summary>A memory of someone close dying (family by the game's relations, a friend, a bonded animal, a death in the family seen).</summary>
        public bool grieving;
        /// <summary>A fight or an insult within the last day.</summary>
        public bool quarrel;
        /// <summary>A council seat, the head of their people, an Empire title.</summary>
        public bool proudStanding;
        public bool shyTrait, proudTrait;
        public bool old;
        /// <summary>Their mood (0 to 1), or below 0 for none; and their minor break line.</summary>
        public float mood, minorBreak;
    }

    /// <summary>
    /// The face engine's poses and its little movement, without game types (Tools/Tests try them): the pose by who
    /// someone is and how they are, kept steady; where the head sits for it; two people facing each other; breathing, a
    /// slow sway and now and then a turn to a side view and back.
    /// </summary>
    public static class FacePoseMath
    {
        // ---------------- The pose ----------------

        /// <summary>A pose not caused by a state of the moment is held at least this long (an hour of the game) before another.</summary>
        public const int HoldTicks = 2500;

        /// <summary>Low by mood under the minor break line, and lifted again only this far above it.</summary>
        public const float MoodMargin = 0.05f;

        /// <summary>A fight or an insult counts as a fresh quarrel this long (a day of the game).</summary>
        public const int QuarrelTicks = 60000;

        /// <summary>Old for their kind: this share of their race's life expectancy (64 for a human).</summary>
        public const float OldShare = 0.8f;

        public static bool Old(float age, float lifeExpectancy) => lifeExpectancy > 1f && age >= OldShare * lifeExpectancy;

        /// <summary>
        /// The pose for these facts, the strongest first: dead, a prisoner, an angry break, a sad break or grief, a low
        /// mood (with a margin before it lifts, so it doesn't flicker at the line), a fresh quarrel, a standing (a seat, the
        /// head of a people, a title), a shy trait, a proud trait, old age; else plain.
        /// </summary>
        public static FaceBearing Bearing(BearingFacts f, FaceBearing current)
        {
            if (f.dead) return FaceBearing.Dead;
            if (f.captive) return FaceBearing.Captive;
            if (f.angryState) return FaceBearing.Angry;
            if (f.sadState || f.grieving) return FaceBearing.Low;
            if (f.mood >= 0f && (f.mood < f.minorBreak || (current == FaceBearing.Low && f.mood < f.minorBreak + MoodMargin))) return FaceBearing.Low;
            if (f.quarrel) return FaceBearing.Angry;
            if (f.proudStanding) return FaceBearing.Proud;
            if (f.shyTrait) return FaceBearing.Shy;
            if (f.proudTrait) return FaceBearing.Proud;
            if (f.old) return FaceBearing.Old;
            return FaceBearing.Plain;
        }

        /// <summary>Whether the pose comes from a state of the moment (death, captivity, a break), which shows at once.</summary>
        public static bool FromState(BearingFacts f, FaceBearing b) =>
            b == FaceBearing.Dead || b == FaceBearing.Captive || (b == FaceBearing.Angry && f.angryState) || (b == FaceBearing.Low && f.sadState);

        /// <summary>
        /// Whether a new pose is taken now: at once when it comes from a state of the moment or the one held did (a break
        /// that ends ends its pose); else only once the one held has lasted <see cref="HoldTicks"/>, so a person's pose
        /// doesn't change from one look to the next.
        /// </summary>
        public static bool TakeNow(bool nextFromState, bool currentFromState, int heldTicks) => nextFromState || currentFromState || heldTicks >= HoldTicks;

        /// <summary>The way a portrait faces: where it is asked (two facing each other), else a shy person turned half away (to a side of their own), else the front.</summary>
        public static FaceTurn TurnFor(FaceBearing b, FaceTurn asked, int seed)
        {
            if (asked != FaceTurn.Front) return asked;
            if (b == FaceBearing.Shy) return (seed & 1) == 0 ? FaceTurn.Right : FaceTurn.Left;
            return FaceTurn.Front;
        }

        /// <summary>Two people side by side face each other (the left one to the right); one alone, or three and more, face the front.</summary>
        public static FaceTurn PairTurn(int index, int count) =>
            count != 2 ? FaceTurn.Front : index == 0 ? FaceTurn.Right : FaceTurn.Left;

        /// <summary>+1 facing right, -1 facing left, 0 the front.</summary>
        public static int Facing(FaceTurn t) => t == FaceTurn.Right ? 1 : t == FaceTurn.Left ? -1 : 0;

        /// <summary>
        /// Where the head sits for a pose. In the front view the head moves up or down (and tips a little to one side,
        /// the person's own, when sad); in a side view it nods forward or back. Small: never more than a tenth of a head
        /// size, ten degrees of a nod or four of a lean.
        /// </summary>
        public static HeadShift Shift(FaceBearing b, FaceTurn t, int seed)
        {
            int f = Facing(t);
            float side = (seed & 2) == 0 ? 1f : -1f;
            var s = new HeadShift();
            if (f == 0)
            {
                switch (b)
                {
                    case FaceBearing.Proud:
                        s.dy = 0.025f;
                        break;
                    case FaceBearing.Low:
                        s.dy = -0.05f;
                        s.tilt = 3.5f * side;
                        break;
                    case FaceBearing.Angry:
                        s.dy = -0.02f;
                        s.tilt = -2f * side;
                        s.lean = 2.5f * side;
                        break;
                    case FaceBearing.Shy:
                        s.dy = -0.02f;
                        break;
                    case FaceBearing.Old:
                        s.dy = -0.035f;
                        break;
                    case FaceBearing.Captive:
                        s.dy = -0.08f;
                        break;
                }
                return s;
            }
            // A side view: a nod forward is clockwise facing right, the other way facing left.
            switch (b)
            {
                case FaceBearing.Proud:
                    s.dy = 0.015f;
                    s.tilt = -3f * f;
                    break;
                case FaceBearing.Low:
                    s.dy = -0.03f;
                    s.tilt = 6f * f;
                    break;
                case FaceBearing.Angry:
                    s.dy = -0.01f;
                    s.tilt = 2f * f;
                    s.lean = 3f * f;
                    break;
                case FaceBearing.Shy:
                    s.dy = -0.02f;
                    s.tilt = 3f * f;
                    break;
                case FaceBearing.Old:
                    s.dy = -0.02f;
                    s.tilt = 3f * f;
                    s.lean = 2.5f * f;
                    break;
                case FaceBearing.Captive:
                    s.dy = -0.05f;
                    s.tilt = 9f * f;
                    break;
            }
            return s;
        }

        /// <summary>The neck the head turns about: this many head sizes below the head's middle.</summary>
        public const float NeckBelow = 0.18f;

        // ---------------- The head and the body drawn apart ----------------

        /// <summary>The test picture's bands: the ends' share of its rows, and the crown's at the head's end.</summary>
        public const float EndShare = 0.3f, CrownShare = 0.08f;

        /// <summary>
        /// Whether the game drew the head and the body apart for this pawn, read from a small test picture of their whole
        /// figure (each row's alpha summed across, 0 to <paramref name="width"/>): the head picture has something in it and
        /// nothing at the feet; the body picture has the body at the feet and nothing at the crown above the hair. Which end
        /// is the head's is read from the head picture, so the rows may run either way. Anything else (a body part of a
        /// race drawn with the head, wings over the head, a game that drew both whole) keeps the whole picture.
        /// </summary>
        public static bool SplitLooksRight(float[] bodyRows, float[] headRows, float width)
        {
            if (bodyRows == null || headRows == null || bodyRows.Length != headRows.Length || bodyRows.Length < 10 || width <= 0f) return false;
            int n = bodyRows.Length, band = Math.Max(1, (int)(n * EndShare)), crown = Math.Max(1, (int)(n * CrownShare));
            float Sum(float[] rows, int from, int count)
            {
                float s = 0f;
                for (int i = from; i < from + count; i++) s += rows[i];
                return s;
            }
            float headTotal = Sum(headRows, 0, n);
            if (headTotal < 0.5f) return false;
            bool headFirst = Sum(headRows, 0, band) >= Sum(headRows, n - band, band);
            int feet = headFirst ? n - band : 0, top = headFirst ? 0 : n - crown;
            float bodyFeet = Sum(bodyRows, feet, band), headFeet = Sum(headRows, feet, band);
            float bodyCrown = Sum(bodyRows, top, crown);
            return bodyFeet >= 0.05f * band * width && headFeet <= 0.03f * bodyFeet && bodyCrown <= 0.02f * crown * width;
        }

        /// <summary>After this many pawns whose head and body didn't come apart, and none that did, the game is taken not to draw them apart at all.</summary>
        public const int SplitGiveUpAfter = 3;

        /// <summary>The dead's grey: a colour's brightness as the eye sees it, a little dimmed.</summary>
        public static byte GreyOf(byte r, byte g, byte b) => (byte)Math.Round(0.85 * (0.299 * r + 0.587 * g + 0.114 * b));

        /// <summary>Round portraits take a pose from this size up; smaller ones only face a way and show the dead grey.</summary>
        public const float PoseFrom = 64f;

        // ---------------- A little movement ----------------

        /// <summary>Breathing: the body a little taller and back, this much, every few seconds.</summary>
        public const float BreathDepth = 0.009f, BreathSeconds = 4.2f;

        /// <summary>A slow sway about the feet: this many degrees each way, over this many seconds.</summary>
        public const float SwayDegrees = 0.5f, SwaySeconds = 7.3f;

        /// <summary>Now and then a turn to a side view and back: once in a while of these seconds, for so long, faded in and out.</summary>
        public const float TurnEveryLeast = 24f, TurnEveryMost = 36f, TurnLasts = 2.6f, TurnFade = 0.3f;

        private static float Phase(int seed, int salt) => unchecked((uint)seed * 2654435761u + (uint)salt * 40503u) % 1000u / 1000f;

        /// <summary>How much taller the body is drawn at this moment (1 is as it is), anchored at its feet.</summary>
        public static float Breath(float clock, int seed) =>
            1f + BreathDepth * 0.5f * (1f + (float)Math.Sin(2.0 * Math.PI * (clock / BreathSeconds + Phase(seed, 1))));

        /// <summary>The sway at this moment, in degrees.</summary>
        public static float Sway(float clock, int seed) => SwayDegrees * (float)Math.Sin(2.0 * Math.PI * (clock / SwaySeconds + Phase(seed, 2)));

        /// <summary>The length of this person's cycle between turns, its own between the least and the most.</summary>
        public static float TurnCycle(int seed) => TurnEveryLeast + (TurnEveryMost - TurnEveryLeast) * Phase(seed, 3);

        /// <summary>
        /// How far into a turn they are at this moment: 0 their usual way, 1 turned, between while it fades; and to which
        /// side (it changes from one turn to the next).
        /// </summary>
        public static float Turned(float clock, int seed, out bool right)
        {
            float cycle = TurnCycle(seed);
            float t = clock + Phase(seed, 4) * cycle;
            if (t < 0f) t = 0f;
            int n = (int)Math.Floor(t / cycle);
            right = ((n + seed) & 1) == 0;
            float into = t - n * cycle;
            if (into >= TurnLasts) return 0f;
            float k = Math.Min(into / TurnFade, (TurnLasts - into) / TurnFade);
            k = Math.Max(0f, Math.Min(1f, k));
            return k * k * (3f - 2f * k);
        }

        /// <summary>The way they face during a turn: the side it goes to from the front, the front from a side view.</summary>
        public static FaceTurn TurnedTo(FaceTurn usual, bool right) =>
            usual != FaceTurn.Front ? FaceTurn.Front : right ? FaceTurn.Right : FaceTurn.Left;
    }
}
