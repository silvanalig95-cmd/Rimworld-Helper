using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The rules of the content round 3 without game types (docs/content-round-3-events-gods.md): when its eight map events may
    /// come (Happenings/Workers_Round3.cs) and how long what they keep on the map lasts, and how often a snake lifted away on a
    /// stick bites, before the shared harm budget's scale. The gods' six on the land are below, in OwnHappeningsMath.
    /// </summary>
    public static class Round3EventsMath
    {
        // ================= The mast year (R1) =================

        /// <summary>Trees near the colony for a mast year: this many within this reach of its middle, out of its home ground.</summary>
        public const int TreesFrom = 25;
        public const float TreesWithin = 45f;

        /// <summary>The boar that come in, and how many the hunters take.</summary>
        public const int BoarMin = 3, BoarMax = 5, BoarTaken = 2;

        /// <summary>Let feed, or left while the hunters take two, the rest stay this long among the trees and then go.</summary>
        public const float BoarStayDays = 1.5f;

        /// <summary>A mast year: in autumn, with the trees and a boar that can live here now.</summary>
        public static bool MastFits(bool autumn, int trees, bool boarCanLive) => autumn && trees >= TreesFrom && boarCanLive;

        // ================= A snake in the woodpile (R2) =================

        /// <summary>The heat that sends a snake into the shade of the logs, and the pile it hides in.</summary>
        public const float SnakeHeatFrom = 28f;
        public const int PileFrom = 20;

        /// <summary>Lifted away on a long stick: one chance in twelve it bites the one holding the stick, before the budget's scale.</summary>
        public const float BiteChance = 0.08f;

        /// <summary>A small bite on the hand or arm.</summary>
        public const float BiteLow = 2f, BiteHigh = 5f;

        /// <summary>It is carried at least this far out from the colony's middle before it is let go.</summary>
        public const float LeadFar = 30f;

        /// <summary>Dusk, when a snake left alone moves off by itself.</summary>
        public const int SnakeDusk = 18;

        /// <summary>The woodpile left alone: the snake moves off at dusk (from <see cref="SnakeDusk"/> until the small hours).</summary>
        public static bool SnakeGoes(int hour) => hour >= SnakeDusk || hour < 4;

        // ================= Snow on the roof (R3) =================

        /// <summary>Snow this deep (of the game's 0 to 1) on average round a room, and a room this big under a built roof.</summary>
        public const float DeepSnow = 0.55f;
        public const int RoomCellsFrom = 24;

        /// <summary>Three shovel it off in about an hour (each step of a shoveller at the wall counts).</summary>
        public const float ShovelHours = 1f;
        public const int Shovellers = 3;

        /// <summary>Left, the roof comes down later that day over at most this many cells nobody stands under.</summary>
        public const float CollapseAfterMin = 5f, CollapseAfterMax = 12f;
        public const int CollapseMost = 3;

        /// <summary>The roof is heavy with snow: a big room under a built roof with deep snow lying round it.</summary>
        public static bool RoofHeavy(bool built, int cells, float meanSnow) => built && cells >= RoomCellsFrom && meanSnow >= DeepSnow;

        // ================= The way across the marsh (R4) =================

        /// <summary>The way crosses this many wet cells at least (and at most this many: a short way, not a lake).</summary>
        public const int WetFrom = 4, WetMost = 30;

        /// <summary>A way worth planking: enough wet ground on it, none of it frozen.</summary>
        public static bool PathFits(int wet, float outdoorTemp) => wet >= WetFrom && wet <= WetMost && outdoorTemp > 0f;

        /// <summary>Only over the worst of it: the wettest half of the wet cells, two at least.</summary>
        public static int Worst(int wet) => wet <= 0 ? 0 : Math.Max(Math.Min(2, wet), (wet + 1) / 2);

        // ================= The cold room under the rock (R5) =================

        /// <summary>The food is kept warm (spoiling at the game's full rate from ten degrees), the deep room cold, by this much.</summary>
        public const float StoreWarmFrom = 14f, DeepColdBelow = 8f, ColderBy = 6f;

        /// <summary>The deep room has this much free floor for a store.</summary>
        public const int StoreCellsFrom = 6;

        /// <summary>A cold room worth using: the food warm where it is, the deep room cold, room on its floor.</summary>
        public static bool ColdRoomFits(float storeTemp, float deepTemp, int freeCells) =>
            storeTemp >= StoreWarmFrom && deepTemp <= DeepColdBelow && storeTemp - deepTemp >= ColderBy && freeCells >= StoreCellsFrom;

        // ================= Seals at the fishing ground (R6) =================

        /// <summary>The seals (or otters) that come up on the shore, how many the hunters take, and how long the rest stay.</summary>
        public const int SealsMin = 3, SealsMax = 4, SealsTaken = 2;
        public const float SealsStayDays = 1.5f;

        // ================= Dinner in the field (R7) =================

        /// <summary>The late morning the cook asks in, and the hour dinner is carried out (or the workers come in).</summary>
        public const int DinnerFrom = 9, DinnerTo = 11, DinnerAt = 12;

        /// <summary>The field is this far from the nearest dining table, with this many at work in it.</summary>
        public const float FieldFar = 30f;
        public const int WorkersFrom = 2, WorkersMost = 6;

        /// <summary>Eating at the field's edge: about this long together.</summary>
        public const float EatHours = 0.6f;

        /// <summary>The cook asks: the late morning, enough at work far out, a meal for each in the stores.</summary>
        public static bool DinnerFits(int hour, int workers, float distance, int meals) =>
            hour >= DinnerFrom && hour <= DinnerTo && workers >= WorkersFrom && distance >= FieldFar && meals >= workers;

        // ================= Clothes worn through (R8) =================

        /// <summary>Worn past half its life: the game's own tattered line.</summary>
        public const float WornBelow = 0.5f;

        /// <summary>This many of the colony in worn clothes for the tailor to ask; the bill makes one each, this many at most.</summary>
        public const int RaggedFrom = 3, BillMost = 4;

        /// <summary>A spare piece in the stores is good enough: at least this much of its life left.</summary>
        public const float SpareFrom = 0.8f;

        public static bool Worn(int hitPoints, int maxHitPoints) => maxHitPoints > 0 && hitPoints < maxHitPoints * WornBelow;

        public static bool Spare(int hitPoints, int maxHitPoints) => maxHitPoints > 0 && hitPoints >= maxHitPoints * SpareFrom;

        /// <summary>One garment each for those in worn clothes, four at most.</summary>
        public static int BillCount(int ragged) => Math.Max(1, Math.Min(BillMost, ragged));
    }

    /// <summary>
    /// The content round 3's six happenings of the gods on the land (Gods/OnMap/OwnRound3.cs), added after round 7's: their
    /// pace and what calls each, pure so Tools/Tests reads the same rules. Each is its own god's.
    /// </summary>
    public static partial class OwnHappeningsMath
    {
        /// <summary>The first of the content round 3's happenings on the land in the shared list (added last, so a save keeps its order).</summary>
        public const OwnHappening FirstLand3 = OwnHappening.BellwetherNewborn;

        /// <summary>A happening is one of the content round 3's on the land.</summary>
        public static bool IsLand3(OwnHappening h) => h >= FirstLand3;

        /// <summary>The six, in order.</summary>
        public static readonly OwnHappening[] Land3 =
        {
            OwnHappening.BellwetherNewborn, OwnHappening.HeliosRipen, OwnHappening.MorelFoxfire, OwnHappening.PipFinished, OwnHappening.SeleneTide,
            OwnHappening.CirrusGlory
        };

        /// <summary>Their pace (PaceOf's own for them): the ring in the snow follows a birth at its own chance; the rest are drawn.</summary>
        public static (float gap, float mean, float ramp) Land3PaceOf(OwnHappening h)
        {
            switch (h)
            {
                case OwnHappening.BellwetherNewborn: return (40f, 0f, 0f);
                case OwnHappening.HeliosRipen: return (40f, 15f, 20f);
                case OwnHappening.MorelFoxfire: return (30f, 10f, 20f);
                case OwnHappening.PipFinished: return (25f, 12f, 20f);
                case OwnHappening.SeleneTide: return (30f, 3f, 10f);
                case OwnHappening.CirrusGlory: return (30f, 6f, 20f);
                default: return (30f, 0f, 0f);
            }
        }

        // ================= Bellwether: the ring in the snow (G1) =================

        /// <summary>Born in the night (this young at dawn at most), out of doors, in a frost, with snow lying round it.</summary>
        public const float NewbornHours = 14f, NewbornSnow = 0.2f, NewbornRing = 2.9f;

        /// <summary>The dawn hour it is looked for.</summary>
        public const int NewbornHour = 6;

        public static bool NewbornFits(float ageHours, bool outdoors, float outdoorTemp, float snow) =>
            ageHours >= 0f && ageHours <= NewbornHours && outdoors && outdoorTemp < 0f && snow >= NewbornSnow;

        /// <summary>Its own chance at such a dawn: half, past its gap, a little more the nearer the white beast is.</summary>
        public const float NewbornChanceBase = 0.5f;

        public static float NewbornChance(float daysSince, float near) =>
            !Free(OwnHappening.BellwetherNewborn, daysSince) ? 0f : Clamp01(NewbornChanceBase * GodNearMath.WorldFactor(near));

        // ================= Helios: ripe at noon (G2) =================

        /// <summary>A hot clear noon, and a field four fifths grown on average with none of it ripe yet.</summary>
        public const float RipenHeat = 30f, RipenGrown = 0.8f;
        public const int RipenFrom = 11, RipenTo = 14;

        /// <summary>The field ripens at a hot clear summer noon, more readily where two or more hold the sun.</summary>
        public static GodPaceMath.Trigger RipenTrigger(bool summer, bool clear, float outdoorTemp, float meanGrowth, bool anyRipe, int holders)
        {
            if (!summer || !clear || float.IsNaN(outdoorTemp) || outdoorTemp < RipenHeat || float.IsNaN(meanGrowth) || meanGrowth < RipenGrown || anyRipe)
                return GodPaceMath.Trigger.None;
            return holders >= 2 ? GodPaceMath.Trigger.Strong : GodPaceMath.Trigger.Ordinary;
        }

        // ================= Morel: the light on the wet ground (G3) =================

        /// <summary>After this many wet days running, above this warmth (degrees), at night.</summary>
        public const int FoxfireWetDays = 2;
        public const float FoxfireAbove = 2f;

        /// <summary>The soil turned rich by morning, at most.</summary>
        public const int FoxfireSoilMost = 8;

        /// <summary>The night hours it is looked for (nine in the evening to two in the morning), and when it fades (first light).</summary>
        public static bool FoxfireHour(int hour) => hour >= 21 || hour < 2;

        public const int FoxfireDawn = 5;

        /// <summary>A wet run, not freezing, a patch of wet or wooded ground near the colony; strongly with something rotting on its ground.</summary>
        public static GodPaceMath.Trigger FoxfireTrigger(int wetRun, float outdoorTemp, bool patch, bool rotting)
        {
            if (wetRun < FoxfireWetDays || float.IsNaN(outdoorTemp) || outdoorTemp <= FoxfireAbove || !patch) return GodPaceMath.Trigger.None;
            return rotting ? GodPaceMath.Trigger.Strong : GodPaceMath.Trigger.Ordinary;
        }

        // ================= Pip: done in the night (G4) =================

        /// <summary>The small hours it is looked for, and how much of the work left stays (a tenth).</summary>
        public const int FinishedFrom = 1, FinishedTo = 4;
        public const float FinishedLeft = 0.1f;

        /// <summary>The sparks at the bench last about an hour; the maker looks in the morning, from this hour.</summary>
        public const int FinishedMorning = 6;

        /// <summary>The work left once it is done in the night: a tenth of it, one unit at least.</summary>
        public static float FinishedWork(float left) => float.IsNaN(left) || left <= 0f ? 0f : Math.Max(1f, left * FinishedLeft);

        /// <summary>An unfinished piece at a bench with nobody at it; strongly when its maker holds the Hum.</summary>
        public static GodPaceMath.Trigger FinishedTrigger(bool piece, bool nobodyAt, bool makerHolds)
        {
            if (!piece || !nobodyAt) return GodPaceMath.Trigger.None;
            return makerHolds ? GodPaceMath.Trigger.Strong : GodPaceMath.Trigger.Ordinary;
        }

        // ================= Selene: the full-moon tide (G5) =================

        /// <summary>The sea comes up over this many cells of empty shore at most, this far from the water, and needs this many to show.</summary>
        public const int TideCellsMost = 40, TideReach = 4, TideCellsFrom = 6;

        /// <summary>The night hours it comes in (nine in the evening to one), and the first light it goes back by.</summary>
        public static bool TideHour(int hour) => hour >= 21 || hour < 1;

        public const int TideBack = 5;

        /// <summary>A full moon at a home on the coast, with empty shore enough for it.</summary>
        public static GodPaceMath.Trigger TideTrigger(bool fullMoon, bool coast, int cells) =>
            fullMoon && coast && cells >= TideCellsFrom ? GodPaceMath.Trigger.Ordinary : GodPaceMath.Trigger.None;

        // ================= Cirrus: the giant in the mist (G6) =================

        /// <summary>The morning hours it is looked for.</summary>
        public const int GloryFrom = 6, GloryTo = 10;

        /// <summary>A foggy morning on high ground with someone out in the open; strongly for one who watches the sky.</summary>
        public static GodPaceMath.Trigger GloryTrigger(bool fog, bool high, bool someoneOut, bool watcher)
        {
            if (!fog || !high || !someoneOut) return GodPaceMath.Trigger.None;
            return watcher ? GodPaceMath.Trigger.Strong : GodPaceMath.Trigger.Ordinary;
        }
    }
}
