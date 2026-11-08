using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>Why the order to take a bleeding animal's body is dropped while it still lives, or that it stands.</summary>
    public enum BodyMarkEnd
    {
        /// <summary>It stands: the animal lives, still bleeding, on the map it was marked on.</summary>
        None,
        /// <summary>Its wounds aged out, were tended or healed: it won't bleed to death now.</summary>
        StoppedBleeding,
        /// <summary>A wild one tamed by the colony.</summary>
        Tamed,
        /// <summary>Another people's animal that came over to the colony.</summary>
        Joined,
        /// <summary>It walked off the map, fled or was taken away.</summary>
        LeftMap,
        /// <summary>It died: the death itself carried the order over to its body (no message here).</summary>
        Died,
        /// <summary>Someone else's order replaced it (the game's Cancel, a hunt or taming order): nothing to tell.</summary>
        Cancelled,
        /// <summary>The setting or the deeper hooks were switched off: dropped without a word.</summary>
        Off
    }

    /// <summary>How a bleeding animal is likely to go, by the game's own rules of blood loss.</summary>
    public struct BleedOutlook
    {
        /// <summary>The health tab's own figure: blood left over the bleeding rate now (int.MaxValue when not bleeding).</summary>
        public int gameTicks;
        /// <summary>When it bleeds to death if nobody tends it and nothing else hurts it, or -1 when its wounds stop first.</summary>
        public int diesIn;
        /// <summary>When the last of its wounds stops bleeding on its own (int.MaxValue for a wound that never does by age).</summary>
        public int closesIn;
        /// <summary>The bleeding rate now, a share of the blood a day.</summary>
        public float rate;

        public bool Dies => diesIn >= 0;
    }

    /// <summary>
    /// Take a bleeding wild animal's body when it dies (the player's request, 8 November 2026: "if i see a wild animal is
    /// bleeding out, can i give a command to haul it's corpse once it's dead? not to hunt it, in case it's a strong one, but
    /// just to wait for it to die. if the animal recovers from bleeind comehow, this job shouild be cancelled."). The pure
    /// rules behind <see cref="BodyWhenItDies"/>: when the button shows, when the order is dropped, the blood-loss outlook in
    /// its tooltip (the game's own rules: HediffGiver_Bleeding, Hediff_Injury, Hediff_MissingPart), and which words say so.
    /// </summary>
    public static class BodyWhenItDiesMath
    {
        /// <summary>The game's floor for bleeding at all (HealthUtility.TicksUntilDeathDueToBloodLoss).</summary>
        public const float NoBleeding = 0.0001f;
        /// <summary>Under this rate the blood comes back instead of running out (HediffGiver_Bleeding, HealthTuning.MinBleedingRateToBleed).</summary>
        public const float BloodLossGrows = 0.1f;
        /// <summary>A day of the game.</summary>
        public const int DayTicks = 60000;
        /// <summary>A missing part stops bleeding this long after it was lost (Hediff_MissingPart's freshness).</summary>
        public const int MissingPartFreshTicks = 90000;
        /// <summary>The health tab's "won't bleed out soon": a day or more at the rate now.</summary>
        public const int SoonTicks = 60000;

        /// <summary>
        /// The button shows on a living, spawned animal that isn't the colony's, still bleeding, not marked already, with the
        /// setting and the deeper hooks on.
        /// </summary>
        public static bool ButtonShows(bool on, bool animal, bool ofColony, bool dead, bool spawned, float bleedRate, bool marked) =>
            on && animal && !ofColony && !dead && spawned && bleedRate >= NoBleeding && !marked;

        /// <summary>
        /// What becomes of a marked animal, looked at on the wild's short pass. <paramref name="stillMarked"/> is whether its
        /// order is still on it; <paramref name="onMap"/> whether it is still held on the map it was marked on (carried or
        /// spawned); <paramref name="ofColony"/> whether it is the colony's now; <paramref name="wasWild"/> whether it had no
        /// people when marked. The game removes every order on an animal that changes hands, so a tamed one is told by its
        /// faction even with the order gone; with the order gone otherwise, someone cancelled or replaced it.
        /// </summary>
        public static BodyMarkEnd Judge(bool on, bool stillMarked, bool dead, bool onMap, bool ofColony, bool wasWild, float bleedRate)
        {
            if (dead) return BodyMarkEnd.Died;
            if (!on) return BodyMarkEnd.Off;
            if (ofColony) return wasWild ? BodyMarkEnd.Tamed : BodyMarkEnd.Joined;
            if (!onMap) return BodyMarkEnd.LeftMap;
            if (!stillMarked) return BodyMarkEnd.Cancelled;
            if (bleedRate < NoBleeding) return BodyMarkEnd.StoppedBleeding;
            return BodyMarkEnd.None;
        }

        /// <summary>Whether the player is told the order was dropped (the animal named, and why).</summary>
        public static bool Told(BodyMarkEnd end) =>
            end == BodyMarkEnd.StoppedBleeding || end == BodyMarkEnd.Tamed || end == BodyMarkEnd.Joined || end == BodyMarkEnd.LeftMap;

        /// <summary>The message's key for a dropped order, or null when nothing is said.</summary>
        public static string MessageKey(BodyMarkEnd end) => Told(end) ? "CC_BodyMark_Off_" + end : null;

        /// <summary>Whether the order is taken off the animal (it stands only while nothing changed).</summary>
        public static bool Drop(BodyMarkEnd end) => end != BodyMarkEnd.None;

        /// <summary>When an injury stops bleeding by its age: 1.5 days for a scratch, up to 3 for a wound of 30 (Hediff_Injury.AgeTicksToStopBleeding).</summary>
        public static int InjuryStopsAt(float severity)
        {
            float t = (severity - 1f) / 29f;
            if (t < 0f) t = 0f;
            if (t > 1f) t = 1f;
            return 90000 + (int)Math.Round(90000f * t, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// How it is likely to go: the health tab's figure, and, by the game's rules, whether it bleeds to death before its
        /// wounds stop on their own. Each bleeding wound's share of the rate (<paramref name="rates"/>, as the game sums them
        /// before its factors) and the ticks until it stops (<paramref name="ticksLeft"/>, int.MaxValue for one that doesn't
        /// by age). Above 10% a day the blood loss grows by the rate a day; below it, the blood comes back, so the rest is safe.
        /// </summary>
        public static BleedOutlook Outlook(float bloodLoss, float totalRate, IList<float> rates, IList<int> ticksLeft)
        {
            var o = new BleedOutlook { rate = totalRate, diesIn = -1, closesIn = 0, gameTicks = int.MaxValue };
            if (totalRate < NoBleeding) return o;
            if (bloodLoss < 0f) bloodLoss = 0f;
            o.gameTicks = (int)((1f - bloodLoss) / totalRate * DayTicks);
            int n = Math.Min(rates?.Count ?? 0, ticksLeft?.Count ?? 0);
            var wounds = new List<(float rate, int left)>();
            float raw = 0f;
            for (int i = 0; i < n; i++)
            {
                if (rates[i] <= 0f) continue;
                wounds.Add((rates[i], Math.Max(0, ticksLeft[i])));
                raw += rates[i];
            }
            if (wounds.Count == 0)
            {
                // Nothing known of the wounds: by the rate now, as the health tab does.
                o.closesIn = int.MaxValue;
                o.diesIn = totalRate >= BloodLossGrows ? o.gameTicks : -1;
                return o;
            }
            o.closesIn = wounds.Max(w => w.left);
            // The game's factors (a stage's bleeding factor, the body's health scale) scale every wound alike.
            float scale = totalRate / raw;
            float loss = bloodLoss;
            long at = 0;
            foreach (var w in wounds.OrderBy(w => w.left))
            {
                float now = scale * raw;
                if (now < BloodLossGrows) return o;
                if (w.left > at)
                {
                    double span = w.left == int.MaxValue ? double.MaxValue : w.left - at;
                    double toDeath = (1f - loss) / now * DayTicks;
                    if (toDeath <= span)
                    {
                        long dies = at + (long)toDeath;
                        o.diesIn = dies > int.MaxValue - 1 ? int.MaxValue - 1 : (int)dies;
                        return o;
                    }
                    loss += (float)(now * span / DayTicks);
                    at = w.left;
                }
                raw -= w.rate;
                if (raw < 0f) raw = 0f;
            }
            return o;
        }

        /// <summary>
        /// The tooltip's line on how it will go, as a key: it bleeds to death (at the health tab's figure); it shouldn't, its
        /// wounds stopping on their own in so long; or it shouldn't, its blood coming back faster than it runs out.
        /// </summary>
        public static string OutlookKey(BleedOutlook o)
        {
            if (o.Dies) return "CC_BodyMark_Outlook_Dies";
            return o.closesIn == int.MaxValue ? "CC_BodyMark_Outlook_LivesSlow" : "CC_BodyMark_Outlook_Lives";
        }

        /// <summary>The order carried over to its body: unforbidden, and a haul order unless it already lies in storage.</summary>
        public static bool HaulOrder(bool inStorage, bool hasHaulOrder) => !inStorage && !hasHaulOrder;

        /// <summary>The player is told once, at the death, when no storage on the map takes the body (and it isn't stored already).</summary>
        public static bool TellNoStorage(bool inStorage, bool storageFound) => !inStorage && !storageFound;
    }
}
