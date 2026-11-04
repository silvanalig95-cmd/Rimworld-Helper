using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>Where a home's hamlet stands in its story.</summary>
    public enum HamletStage : byte
    {
        /// <summary>No settlers have asked yet.</summary>
        None,
        /// <summary>The settlers' letter is out: the colony has not answered.</summary>
        Asked,
        /// <summary>Taken under the colony's protection: they camp at the gate until a caravan of the colony's sets out.</summary>
        Waiting,
        /// <summary>On the road beside the colony's caravan.</summary>
        Escorted,
        /// <summary>On the road alone.</summary>
        Alone,
        /// <summary>The hamlet stands, under the colony's protection.</summary>
        Standing,
        /// <summary>The hamlet stands and has sworn fealty to the colony (without Faction Territories' vassals).</summary>
        Sworn,
        /// <summary>Its story is over (see <see cref="HamletEnd"/>).</summary>
        Over
    }

    /// <summary>Where the settlers came from.</summary>
    public enum HamletWay : byte
    {
        /// <summary>A holding that has lived a year on the colony's land and wants a place of its own.</summary>
        Holding,
        /// <summary>Families of a town given up, offered a place near the colony at its gate.</summary>
        Refugees,
        /// <summary>Settlers from a poor or struggling town of a people at peace.</summary>
        Town
    }

    /// <summary>How a hamlet's story ended.</summary>
    public enum HamletEnd : byte
    {
        None,
        /// <summary>The colony said no, or never answered.</summary>
        Refused,
        /// <summary>Going alone, they were turned back on the road.</summary>
        TurnedBack,
        /// <summary>Every one of them died on the way.</summary>
        Lost,
        /// <summary>Its people gave it up (the region's rise and fall): a ruin.</summary>
        Failed,
        /// <summary>Refused twice in a hard season: the tie is broken, and it is just their town.</summary>
        Broken,
        /// <summary>Their people turned on the colony, and its families came to the colony instead.</summary>
        Joined,
        /// <summary>Their people turned on the colony, and its families stayed with them.</summary>
        Stayed,
        /// <summary>It swore fealty as Faction Territories' vassal: the mod keeps it from here.</summary>
        Vassal,
        /// <summary>Gone some other way (taken, destroyed, the home lost).</summary>
        Gone
    }

    /// <summary>What a hamlet asks for in a hard season.</summary>
    public enum HamletNeed : byte
    {
        Food,
        Doctor,
        Tools
    }

    /// <summary>
    /// The small rules of the settlers' hamlet (docs/design/stage1-region.md, item 6): who asks and when (one ask a year per
    /// home, from day 90, five grown colonists, the settlers' people at peace; a holding a year settled with low tension,
    /// the refugees of a town given up, or settlers from a poor or struggling town), where it stands (6 to 10 tiles off by
    /// the posts' rules), the escort (five days at the gate, a caravan of two grown colonists or more bound for the place,
    /// a night there before the first fires), the founding's one roll of the road's dangers, going alone (a third turned
    /// back), its life (the harvest carter, the hard season's asks, two refusals in a season break the tie), fealty (60
    /// days at 40 or more with no ask refused in 60 days), and its second year. Pure (no game types): Tools/Tests
    /// (HamletTests) checks them.
    /// </summary>
    public static class HamletMath
    {
        public const int DayTicks = 60000;
        public const int HourTicks = 2500;

        // ---------------- Who asks, and when ----------------

        /// <summary>No settlers ask before this day of the colony.</summary>
        public const int FirstDay = 90;
        /// <summary>A home needs this many grown colonists to protect a hamlet.</summary>
        public const int MinAdults = 5;
        /// <summary>At most one ask a year (60 days) per home.</summary>
        public const int AskGapDays = 60;
        /// <summary>Each home is looked at every so many days.</summary>
        public const int LookDays = 10;
        /// <summary>The chance at a look when some settlers qualify (an ask in about 70 days of qualifying).</summary>
        public const float Chance = 0.15f;
        /// <summary>The colony has this long to answer the settlers' letter before they look elsewhere.</summary>
        public const int AnswerDays = 10;

        /// <summary>A holding asks after this many days on the colony's land (a year)...</summary>
        public const float HoldingDays = 60f;
        /// <summary>...with its unease under this.</summary>
        public const float HoldingTension = 30f;
        /// <summary>A town of theirs under this fortune (poor or struggling) sends settlers out.</summary>
        public const float PoorUnder = 40f;

        public static bool LookDue(int lookedAt, int now) => lookedAt < 0 || now - lookedAt >= (long)LookDays * DayTicks;

        /// <summary>
        /// Settlers may ask at a home now: from day 90, with five grown colonists, no hamlet of the colony's standing there
        /// and none on its way (<paramref name="busy"/>), and no ask there in the last year (<paramref name="lastAsk"/> -1: never).
        /// </summary>
        public static bool AskAllowed(int day, int adults, bool busy, int lastAsk, int now) =>
            day >= FirstDay && adults >= MinAdults && !busy && (lastAsk < 0 || now - lastAsk >= (long)AskGapDays * DayTicks);

        /// <summary>A holding wants a place of its own: a year on the colony's land, its unease low, its people at peace.</summary>
        public static bool HoldingFits(float daysSettled, float tension, bool atPeace) => atPeace && daysSettled >= HoldingDays && tension < HoldingTension;

        /// <summary>A town sends settlers: its fortune poor or struggling, its people at peace with the colony.</summary>
        public static bool TownFits(float prosperity, bool atPeace) => atPeace && prosperity >= 0f && prosperity < PoorUnder;

        /// <summary>The settlers' people may found one at all: humanlike, at peace, people to send, a naming rule for towns.</summary>
        public static bool PeopleFits(bool humanlike, bool hostile, bool canSend, bool namesTowns) => humanlike && !hostile && canSend && namesTowns;

        // ---------------- Where ----------------

        /// <summary>The hamlet stands this far from the home (tiles, in a straight line).</summary>
        public const float MinTiles = 6f, MaxTiles = 10f;
        /// <summary>At least this far from any other town.</summary>
        public const float TownGap = 3f;
        /// <summary>A tile by a river weighs this much more, one on a road this much (water first, for a hamlet's fields).</summary>
        public const float RiverWeight = 2f, RoadWeight = 1.5f;

        /// <summary>
        /// A tile a hamlet may stand on, by the trading posts' rules: the game holds it valid for a town with nothing on it
        /// (<paramref name="valid"/>), 6 to 10 tiles from the home, clear of every one of the colony's settlements by the
        /// game's goodwill reach (RiseFallMath.ClearOfHomes), 3 tiles or more from any other town (<paramref name="nearestTown"/>
        /// negative: none), on land where the game would put a town at all.
        /// </summary>
        public static bool PlaceFits(bool valid, float fromHome, float nearestOurs, int goodwillTiles, float nearestTown, float settlementWeight) =>
            valid && fromHome >= MinTiles && fromHome <= MaxTiles && RiseFallMath.ClearOfHomes(nearestOurs, goodwillTiles)
            && (nearestTown < 0f || nearestTown >= TownGap) && settlementWeight > 0f;

        /// <summary>How much a tile weighs: its land's settlement weight, twice by a river, half again on a road.</summary>
        public static float PlaceWeight(bool road, bool river, float settlementWeight) => Math.Max(0f, settlementWeight) * (river ? RiverWeight : road ? RoadWeight : 1f);

        // ---------------- The escort ----------------

        /// <summary>They wait at the gate up to so many days for a caravan.</summary>
        public const int WaitDays = 5;
        /// <summary>A caravan of so many grown colonists or more can take them along.</summary>
        public const int MinEscort = 2;
        /// <summary>A caravan this near the home (tiles), bound for the place, is one that set out from it.</summary>
        public const float PickupTiles = 3f;

        public static bool WaitOver(int since, int now) => since >= 0 && now - since >= (long)WaitDays * DayTicks;

        /// <summary>A caravan takes the waiting settlers along: two grown colonists or more, bound for the place, just out of the home.</summary>
        public static bool EscortFits(int adults, bool boundThere, float tilesFromHome) => adults >= MinEscort && boundThere && tilesFromHome >= 0f && tilesFromHome <= PickupTiles;

        /// <summary>A night at the place with the caravan: the first fires are lit after eight hours there in the morning, or a full day.</summary>
        public const int NightHours = 8;
        public const int MorningFrom = 5, MorningTo = 11;

        /// <summary>The settlers have spent their first night at the place (arrived at <paramref name="arrivedAt"/>, the place's hour now <paramref name="hour"/>).</summary>
        public static bool FirstFires(int arrivedAt, int now, int hour)
        {
            if (arrivedAt < 0 || now < arrivedAt) return false;
            long there = now - (long)arrivedAt;
            if (there >= 24L * HourTicks) return true;
            return there >= NightHours * (long)HourTicks && hour >= MorningFrom && hour < MorningTo;
        }

        // ---------------- The road's dangers at the founding ----------------

        /// <summary>The one roll at the founding: the game's own caravan demand or ambush, this likely...</summary>
        public const float RiskChance = 0.15f;
        /// <summary>...or this likely with a raiders' camp near the home or a town of a hostile people within <see cref="DangerTiles"/> of the place.</summary>
        public const float DangerChance = 0.35f;
        public const float DangerTiles = 15f;

        /// <summary>Dangerous country: a raiders' camp stands near the home, or a hostile people's town is within 15 tiles of the place (negative: none).</summary>
        public static bool Danger(bool raidersCamp, float nearestHostileTown) => raidersCamp || nearestHostileTown >= 0f && nearestHostileTown <= DangerTiles;

        public static float Risk(bool danger) => danger ? DangerChance : RiskChance;

        // ---------------- Going alone ----------------

        /// <summary>Of those who go alone, this share are turned back on the road.</summary>
        public const float TurnedBackShare = 1f / 3f;

        public static bool TurnedBack(float roll) => roll < TurnedBackShare;

        /// <summary>How long the road alone takes: a caravan's pace over the distance, and a day to choose the ground (ticks).</summary>
        public static int AloneTicks(float tiles) => (int)Math.Round((RegionMath.WalkDays(tiles) + 1f) * DayTicks);

        // ---------------- What it is ----------------

        /// <summary>A hamlet's fortune starts here, escorted...</summary>
        public const float Start = 25f;
        /// <summary>...and lower when they came alone, with what the road left them.</summary>
        public const float AloneStart = 12f;
        /// <summary>The settlers kept on their way, at most.</summary>
        public const int MaxSettlers = 8;

        // ---------------- Its life ----------------

        /// <summary>The harvest's carter comes once a year at most (the gap in days), not in its first days, not while it struggles.</summary>
        public const int HarvestGapDays = 45;
        public const int HarvestFirstDays = 10;
        public const float HungryUnder = 20f;
        /// <summary>The colony's goodwill with their people when the carter comes.</summary>
        public const int HarvestGoodwill = 3;

        public static bool HarvestDue(bool harvestTime, int lastAt, int founded, int now, float prosperity) =>
            harvestTime && founded >= 0 && now - founded >= (long)HarvestFirstDays * DayTicks && (lastAt < 0 || now - lastAt >= (long)HarvestGapDays * DayTicks)
            && (prosperity < 0f || prosperity >= HungryUnder);

        /// <summary>What the carter brings (raw food), more as the hamlet prospers.</summary>
        public static int Produce(float prosperity) => 20 + (int)Math.Round(Math.Max(0f, Math.Min(100f, prosperity < 0f ? Start : prosperity)) * 0.6f);

        /// <summary>In a hard season (struggling) it asks for help, at most once every so many days.</summary>
        public const int HelpGapDays = 5;
        /// <summary>Help sent lifts its fortune this much.</summary>
        public const float HelpLift = 6f;

        public static bool HelpDue(float prosperity, int lastAt, int founded, int now) =>
            prosperity >= 0f && prosperity < HungryUnder && founded >= 0 && now - founded >= (long)HelpGapDays * DayTicks
            && (lastAt < 0 || now - lastAt >= (long)HelpGapDays * DayTicks);

        /// <summary>What it asks for, by a roll: food half the time, a doctor or tools the rest; never the same as the last ask when it can help it.</summary>
        public static HamletNeed NeedFor(float roll, int last)
        {
            var need = roll < 0.5f ? HamletNeed.Food : roll < 0.75f ? HamletNeed.Doctor : HamletNeed.Tools;
            if ((int)need == last) need = need == HamletNeed.Food ? HamletNeed.Doctor : HamletNeed.Food;
            return need;
        }

        /// <summary>A season (days): refused twice within one, the tie is broken.</summary>
        public const int SeasonDays = 15;
        public const int BreakRefusals = 2;
        /// <summary>Refusals kept at most (the newest).</summary>
        public const int MaxRefused = 4;

        /// <summary>Refused twice within a season: the tie breaks.</summary>
        public static bool Broken(IList<int> refused, int now)
        {
            if (refused == null) return false;
            int n = 0;
            foreach (int t in refused)
                if (t >= 0 && now - t < (long)SeasonDays * DayTicks)
                    n++;
            return n >= BreakRefusals;
        }

        /// <summary>A refusal noted: the newest kept, at most <see cref="MaxRefused"/>, none older than a year.</summary>
        public static void NoteRefused(List<int> refused, int now)
        {
            if (refused == null) return;
            refused.Add(now);
            refused.RemoveAll(t => now - t >= (long)AskGapDays * DayTicks);
            while (refused.Count > MaxRefused) refused.RemoveAt(0);
        }

        /// <summary>The last ask refused (-1: none).</summary>
        public static int LastRefused(IList<int> refused)
        {
            int last = -1;
            if (refused != null)
                foreach (int t in refused)
                    if (t > last)
                        last = t;
            return last;
        }

        // ---------------- Fealty ----------------

        /// <summary>After so many days at <see cref="FealtyAt"/> or more...</summary>
        public const int FealtyDays = 60;
        public const float FealtyAt = 40f;
        /// <summary>...with no ask refused in so many days, it asks to swear fealty; told "not yet", it asks again after <see cref="OathAgainDays"/>.</summary>
        public const int RefusedGapDays = 60;
        public const int OathAgainDays = 30;

        /// <summary>Since when it has stood at 40 or more: kept while it does, from now when it first does, -1 once it falls under.</summary>
        public static int HighSince(float prosperity, int was, int now) => prosperity >= FealtyAt ? (was >= 0 ? was : now) : -1;

        public static bool FealtyDue(int highSince, int lastRefused, int oathAt, int now) =>
            highSince >= 0 && now - highSince >= (long)FealtyDays * DayTicks && (lastRefused < 0 || now - lastRefused >= (long)RefusedGapDays * DayTicks)
            && (oathAt < 0 || now - oathAt >= (long)OathAgainDays * DayTicks);

        // ---------------- Its second year ----------------

        /// <summary>A hamlet's second year ends this many days after its founding (two of the game's years).</summary>
        public const int SecondYearDays = 120;

        public static bool SecondYearOver(int founded, int now) => founded >= 0 && now - founded >= (long)SecondYearDays * DayTicks;

        // ---------------- Memory and history ----------------

        /// <summary>The settlers walked out and settled under the colony's protection: a good turn their people remember.</summary>
        public const string DeedProtected = "hamlet";
        /// <summary>Their hamlet's harvest brought to the colony's gate: a small good turn of theirs.</summary>
        public const string DeedHarvest = "hamletHarvest";
        /// <summary>Help sent to the hamlet in a hard season, or refused.</summary>
        public const string DeedHelped = "hamletHelped", DeedRefused = "hamletRefused";

        /// <summary>The history's kinds, of the family world:hamlet: founded (a milestone), turned back, sworn, broken, joined, stayed, given up, the harvest, help sent and refused.</summary>
        public const string Family = "world:hamlet";
        public const string KindFounded = "world:hamlet:founded", KindTurnedBack = "world:hamlet:turnedback", KindSworn = "world:hamlet:sworn",
            KindBroken = "world:hamlet:broken", KindJoined = "world:hamlet:joined", KindStayed = "world:hamlet:stayed", KindFailed = "world:hamlet:failed",
            KindHarvest = "world:hamlet:harvest", KindHelped = "world:hamlet:helped", KindRefused = "world:hamlet:refused";

        public static readonly string[] Kinds = { KindFounded, KindTurnedBack, KindSworn, KindBroken, KindJoined, KindStayed, KindFailed, KindHarvest, KindHelped, KindRefused };

        /// <summary>How an end goes into the history (null: none of its own; what took it told it).</summary>
        public static string EndKind(HamletEnd e)
        {
            switch (e)
            {
                case HamletEnd.TurnedBack: return KindTurnedBack;
                case HamletEnd.Broken: return KindBroken;
                case HamletEnd.Joined: return KindJoined;
                case HamletEnd.Stayed: return KindStayed;
                case HamletEnd.Failed: return KindFailed;
                case HamletEnd.Vassal: return KindSworn;
                default: return null;
            }
        }

        /// <summary>The hamlet's founding, its oath and its end are milestones of the colony's; the harvest and the asks are everyday.</summary>
        public static bool Milestone(string kind) => kind != KindHarvest && kind != KindHelped && kind != KindRefused && kind != KindTurnedBack;

        /// <summary>A hamlet's story is under way (an ask out, settlers on their way, or the hamlet standing): no other may begin at that home.</summary>
        public static bool Busy(HamletStage s) => s != HamletStage.None && s != HamletStage.Over;

        /// <summary>The hamlet stands as the colony's.</summary>
        public static bool Stands(HamletStage s) => s == HamletStage.Standing || s == HamletStage.Sworn;

        /// <summary>The settlers are still the colony's to keep in the world (asked, waiting, on the road).</summary>
        public static bool OnTheirWay(HamletStage s) => s == HamletStage.Asked || s == HamletStage.Waiting || s == HamletStage.Escorted || s == HamletStage.Alone;
    }
}
