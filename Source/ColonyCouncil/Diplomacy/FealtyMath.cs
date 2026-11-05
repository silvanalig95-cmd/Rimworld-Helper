using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>How much the colony's own vassals bring at harvest, as the council sets it (heavy costs their goodwill each season).</summary>
    public enum TributeLevel
    {
        Light,
        Usual,
        Heavy
    }

    /// <summary>How a vassal came to the colony: the hamlet that swore, a struggling town taken under its protection, or Faction Territories' own.</summary>
    public enum VassalKind
    {
        Hamlet,
        Protected,
        Territories
    }

    /// <summary>What a vassal's head asks of the colony.</summary>
    public enum VassalAsk
    {
        /// <summary>Food or medicine in a hard season.</summary>
        Help,
        /// <summary>The colony's judgement in a quarrel with a neighbouring people.</summary>
        Judgement,
        /// <summary>A raiders' camp of the colony's enemies near them: the colony may break it.</summary>
        Camp,
        /// <summary>Faction Territories' invasion of their outpost: the colony may join the fight through the mod's own arrival.</summary>
        Defend,
        /// <summary>Their head thought little of the colony for a season and asks to be released.</summary>
        Release,
        /// <summary>A struggling town, not yet a vassal, asks to be taken under the colony's protection.</summary>
        Protection
    }

    /// <summary>Who stands up before the council against swearing fealty, the first that holds.</summary>
    public enum FealtyObjector
    {
        None,
        /// <summary>One of the colony sworn to vengeance on the liege.</summary>
        Avenger,
        /// <summary>A proud colonist who won't bend the knee.</summary>
        Proud,
        /// <summary>A devout colonist of another faith than the liege's.</summary>
        Faith
    }

    /// <summary>
    /// Fealty to a stronger neighbour and vassals of the colony's own (docs/design/stage2-diplomacy.md, round c, items 9
    /// and 10; decisions 9, 11 and 12; release 2c.2), with no game types so Tools/Tests can check it.
    /// <list type="bullet">
    /// <item>The liege: a Fealty tie from the colony to the people (origin <see cref="Origin"/>), its terms: key
    /// <see cref="LiegeKey"/> (or <see cref="FailedKey"/> after a season in which they failed to protect it, which lets the
    /// colony leave once without blame), number the game day the next tribute is due, count the silver still owed (the
    /// tie called in while it is), days the day their band last came, goods a call under way (their band "band:DAY", the
    /// colony's levies asked for "arms:TOWN:DAY", PactMath's codes), about the enemy of that call.</item>
    /// <item>The colony's own vassals: the hamlet sworn (its tie, origin "hamlet") and up to two struggling towns taken
    /// under its protection (origin <see cref="VassalOrigin"/>, key <see cref="ProtectedKey"/>), each with a record on its
    /// home's part (DiplomacyBook: VassalTerms); Faction Territories' vassals are views, nothing saved but their head.</item>
    /// </list>
    /// </summary>
    public static class FealtyMath
    {
        public const int DayTicks = 60000;
        public const float SeasonDays = 15f, YearDays = 60f;

        public const string Origin = "fealty", VassalOrigin = "vassal";
        public const string LiegeKey = "liege", FailedKey = "liege:failed", ProtectedKey = "protected";

        // ================= Swearing fealty =================

        /// <summary>A people this well disposed (goodwill), with this many towns within reach, can take the colony's oath.</summary>
        public const int SwearGoodwill = 40, MinTowns = 2;

        /// <summary>
        /// The colony may swear to them: at peace, not the Empire (decision 9: its titles and permits are its own), goodwill
        /// 40 or more, two towns or more within reach, and no liege already (one a colony).
        /// </summary>
        public static bool MaySwear(int goodwill, bool hostile, bool empire, int towns, bool hasLiege) =>
            !hostile && !empire && !hasLiege && goodwill >= SwearGoodwill && towns >= MinTowns;

        /// <summary>They take the colony's oath: half the time at a mean of 40 between their goodwill and their head's regard, nearly always among close friends.</summary>
        public static float AcceptChance(int goodwill, int headRegard) => Clamp(0.5f + ((goodwill + headRegard) / 2f - SwearGoodwill) / 60f, 0.2f, 0.95f);

        public static bool Accepts(int goodwill, int headRegard, float roll) => Clamp01(roll) < AcceptChance(goodwill, headRegard);

        /// <summary>Their lord and retinue come to the gate this many hours after their word (a letter adds its walk back).</summary>
        public const float OathHoursMin = 8f, OathHoursMax = 20f;

        /// <summary>The retinue waits at the gate this long for the colony's speaker, then goes home without the oath.</summary>
        public const float OathWaitHours = 20f;

        /// <summary>The oath that can't be sworn this many days after it was due (no way in, the colony beset) lapses.</summary>
        public const float OathGiveUpDays = 4f;

        // ---------------- Their offer of protection ----------------

        /// <summary>The colony is struggling: raided twice or more in the last season with five grown fighters or fewer.</summary>
        public const int StruggleRaids = 2, FewFighters = 5;

        public static bool Struggling(int raidsThisSeason, int fighters) => raidsThisSeason >= StruggleRaids && fighters <= FewFighters;

        /// <summary>Their offer comes at most once a year to a home; while it may, a daily look finds it about once in four days.</summary>
        public const float OfferGapDays = 60f, OfferDaily = 0.25f;

        public static bool MayOffer(int goodwill, bool hostile, bool empire, int towns, bool hasLiege, int lastOffer, int now) =>
            MaySwear(goodwill, hostile, empire, towns, hasLiege) && (lastOffer < 0 || now - lastOffer >= OfferGapDays * DayTicks);

        // ---------------- Who objects ----------------

        /// <summary>The proud stand up against it from this pride on (NatureMath.Threshold).</summary>
        public const float ProudAt = 30f;

        /// <summary>Who objects, the first that holds: one sworn to vengeance on them, the proud, a devout colonist of another faith.</summary>
        public static FealtyObjector Objector(bool avenger, float pride, bool devoutOtherFaith)
        {
            if (avenger) return FealtyObjector.Avenger;
            if (pride >= ProudAt) return FealtyObjector.Proud;
            return devoutOtherFaith ? FealtyObjector.Faith : FealtyObjector.None;
        }

        // ================= Tribute to the liege =================

        /// <summary>Tribute each season: 150 silver's worth for a poor colony, 600 at most, by its wealth, in tens.</summary>
        public const int TributeMin = 150, TributeMax = 600;

        public const float WealthLow = 15000f, WealthHigh = 150000f;

        public static int Tribute(float wealth)
        {
            float t = Clamp01((wealth - WealthLow) / (WealthHigh - WealthLow));
            float v = TributeMin + t * (TributeMax - TributeMin);
            return (int)Math.Round(v / 10f) * 10;
        }

        /// <summary>The season's tribute is due on its day.</summary>
        public static bool TributeDue(float dueDay, float nowDay) => nowDay >= dueDay;

        /// <summary>Their collector comes between these hours of the day.</summary>
        public const int CollectFrom = 9, CollectTo = 16;

        public static bool CollectHour(int hour) => hour >= CollectFrom && hour < CollectTo;

        /// <summary>What is still owed after what was handed over.</summary>
        public static int Owed(int value, int paid) => Math.Max(0, value - Math.Max(0, paid));

        /// <summary>
        /// The season's day comes with tribute still owed from the last: the oath is broken. Their goodwill drops by this
        /// much, and they remember the tribute short.
        /// </summary>
        public const int BrokenGoodwill = 20;

        public static bool BreaksOath(bool owedFromLastSeason) => owedFromLastSeason;

        /// <summary>The colony's quartermaster seeing to the tribute: paid on time and in full, their head thinks this much better of the colony.</summary>
        public const float PunctualRegard = 3f;

        // ================= Levies =================

        /// <summary>When their town within reach is attacked: two fighters of the colony's, set out within five days (CallsToArms, PactMath's rule).</summary>
        public const int LevyFighters = PactMath.MinFighters;

        public const float LevyDays = PactMath.SetOutDays;

        /// <summary>Refused, or nobody set out in time: their goodwill drops by this much, and they remember the levies refused.</summary>
        public const int LeviesRefusedGoodwill = 10;

        // ================= Protection =================

        /// <summary>When a home is raided their band comes without a call (a war band, half the threat), at most once a season, nine times in ten.</summary>
        public const float ProtectChance = 0.9f;

        public static bool MayProtect(float lastBandDay, float nowDay) => lastBandDay <= 0f || nowDay - lastBandDay >= SeasonDays;

        /// <summary>They come: not when the raiders are their friends, and nine times in ten otherwise.</summary>
        public static bool Protects(bool attackerTheirFriend, float roll) => !attackerTheirFriend && Clamp01(roll) < ProtectChance;

        // ================= Leaving =================

        /// <summary>Renouncing the oath: their grudge (30 goodwill and the deed), unless they failed to protect the colony in a season.</summary>
        public const int RenounceGoodwill = 30;

        public static int RenounceCost(bool failedToProtect) => failedToProtect ? 0 : RenounceGoodwill;

        // ================= Vassals of the colony's own =================

        /// <summary>A home takes two struggling towns under its protection at most, besides its hamlet.</summary>
        public const int MaxProtected = 2;

        /// <summary>A home keeps a record for its hamlet and each protected town.</summary>
        public const int MaxVassals = MaxProtected + 1;

        /// <summary>A town under this prosperity for 30 days is struggling, and may ask for the colony's protection.</summary>
        public const float StruggleProsperity = 20f;

        public const float StruggleDays = 30f;

        /// <summary>One such ask a year to a home at most; while one may come, a daily look finds it about once in eight days.</summary>
        public const float ProtectAskGapDays = 60f, ProtectAskDaily = 0.125f;

        /// <summary>
        /// A struggling town asks to be taken under the colony's protection: under 20 prosperity for 30 days, its people at
        /// peace with the colony and at war with someone else, not the Empire's, room for one more, and none asked this year.
        /// </summary>
        public static bool AsksProtection(float prosperity, int lowSince, int now, bool hostileToUs, bool atWarElsewhere, bool empire, int protectedCount, int lastAsk) =>
            prosperity >= 0f && prosperity < StruggleProsperity && lowSince >= 0 && now - lowSince >= StruggleDays * DayTicks && !hostileToUs && atWarElsewhere && !empire
            && protectedCount < MaxProtected && (lastAsk < 0 || now - lastAsk >= ProtectAskGapDays * DayTicks);

        // ---------------- Tribute from vassals ----------------

        /// <summary>The produce at harvest by the council's level: light six tenths, usual as it is, heavy eight fifths.</summary>
        public static float TributeFactor(TributeLevel level) => level == TributeLevel.Light ? 0.6f : level == TributeLevel.Heavy ? 1.6f : 1f;

        public static int Produce(int usual, TributeLevel level) => Math.Max(1, (int)Math.Round(Math.Max(1, usual) * TributeFactor(level)));

        /// <summary>A protected town's produce at harvest: 20 for a poor town, 50 for one doing well.</summary>
        public static int ProtectedProduce(float prosperity) => 20 + (int)Math.Round(Clamp(prosperity, 0f, 100f) * 0.3f);

        /// <summary>Heavy tribute costs their people this much goodwill each season, and their head this much regard.</summary>
        public const int HeavyGoodwill = 3;

        public const float HeavyRegard = 4f;

        /// <summary>Light tribute warms their head a little each season.</summary>
        public const float LightRegard = 2f;

        // ---------------- The quartermaster's dues ----------------

        /// <summary>
        /// A season's dues in silver, collected by the quartermaster (once a season a vassal): 40 for a poor vassal, 100 for
        /// one doing well, by the council's level; struggling (under 20), they pay half and explain why.
        /// </summary>
        public static int Dues(float prosperity, TributeLevel level)
        {
            float v = (40f + Clamp(prosperity, 0f, 100f) * 0.6f) * TributeFactor(level);
            if (Explained(prosperity)) v *= 0.5f;
            return Math.Max(10, (int)Math.Round(v / 5f) * 5);
        }

        public static bool Explained(float prosperity) => prosperity >= 0f && prosperity < StruggleProsperity;

        /// <summary>The dues collected once a season from each vassal.</summary>
        public static bool DuesDue(int lastDues, int now) => lastDues < 0 || now - lastDues >= SeasonDays * DayTicks;

        // ---------------- Their levies ----------------

        /// <summary>A vassal's war band is a third of the threat: two thirds of the half a war band brings (WarCamps.MakeFighters).</summary>
        public const float LevyStrength = 2f / 3f;

        public static bool MayCallLevies(int lastCall, int now) => lastCall < 0 || now - lastCall >= SeasonDays * DayTicks;

        /// <summary>Their dead weigh on their head's regard: six for each who fell, twenty at most.</summary>
        public static float FallenRegard(int fell) => -Math.Min(20f, 6f * Math.Max(0, fell));

        // ---------------- Their asks ----------------

        /// <summary>One ask a season from each vassal at most; while it may, a daily look finds one about once in ten days.</summary>
        public const float AskGapDays = 15f, AskDaily = 0.1f;

        public static bool MayAsk(int lastAsk, int now) => lastAsk < 0 || now - lastAsk >= AskGapDays * DayTicks;

        /// <summary>A raiders' camp this near their town (tiles) is a threat near them.</summary>
        public const float CampTiles = 8f;

        /// <summary>The regard their head gives or takes for each answer.</summary>
        public const float HelpRegard = 8f, RefusedRegard = -8f, SidedRegard = 10f, SidedAgainstRegard = -10f, KeptOutRegard = -3f, CampKeptRegard = 15f,
            CampBrokenRegard = -10f, CampDeclinedRegard = -5f, DefendRegard = 12f;

        /// <summary>The colony's word to break a camp holds a season.</summary>
        public const float CampPromiseDays = SeasonDays;

        /// <summary>The neighbour in the quarrel: siding with them, or against them, moves their goodwill this much.</summary>
        public const int JudgementGoodwill = 5;

        /// <summary>What they need in a hard season: food, or medicine when a sickness runs near them.</summary>
        public static string HelpNeed(bool sickness) => sickness ? "medicine" : "rawfood";

        public static int HelpCount(bool sickness) => sickness ? 5 : 30;

        /// <summary>
        /// What they ask now, the first that fits: release (their head long sour), a camp near them, an invasion of their
        /// outpost (Faction Territories), help in a hard season, the colony's judgement (a neighbour at odds with them).
        /// </summary>
        public static VassalAsk? AskOf(bool release, bool camp, bool invaded, bool hardSeason, bool quarrel, bool territories)
        {
            if (release && !territories) return VassalAsk.Release;
            if (invaded && territories) return VassalAsk.Defend;
            if (camp) return VassalAsk.Camp;
            if (hardSeason) return VassalAsk.Help;
            if (quarrel) return VassalAsk.Judgement;
            return null;
        }

        // ---------------- Release ----------------

        /// <summary>Their head thinking this little of the colony (-20 or lower) for a season asks to be released.</summary>
        public const int ReleaseRegard = -20;

        public const float ReleaseDays = SeasonDays;

        public static int LowSince(int regard, int was, int now) => regard <= ReleaseRegard ? (was >= 0 ? was : now) : -1;

        public static bool AsksRelease(int lowSince, int now) => lowSince >= 0 && now - lowSince >= ReleaseDays * DayTicks;

        /// <summary>Refused, they break away: the tie is broken, and their people's goodwill drops by this much.</summary>
        public const int BreakAwayGoodwill = 15;

        // ================= The terms =================

        public static bool Failed(string key) => key == FailedKey;

        public static string Code(TributeLevel l) => l.ToString().ToLowerInvariant();

        public static bool ParseLevel(string s, out TributeLevel l)
        {
            l = TributeLevel.Usual;
            if (string.IsNullOrEmpty(s)) return false;
            foreach (TributeLevel x in Enum.GetValues(typeof(TributeLevel)))
                if (string.Equals(Code(x), s, StringComparison.OrdinalIgnoreCase))
                {
                    l = x;
                    return true;
                }
            return false;
        }

        // ================= The facts =================

        /// <summary>
        /// The talk's own tags beside the ties' (tie:fealty, :ours, :theirs, :due, {FEALTYTO}, which a land grant's fealty
        /// sets too): the colony's oath sworn to them (fealty:oath), its levies sent to their call this season
        /// (fealty:levies, and fealty:levies:sent beside it: the older lines' tag says they came, so it is set only when they
        /// did), refused or never set out (fealty:levies:refused), their band come to protect it this season
        /// (fealty:protected), tribute short and owed (fealty:short); a town of theirs the colony's own vassal (vassal:town),
        /// under heavy tribute (vassal:heavy), its levies called this season (vassal:levy).
        /// </summary>
        public const string OathTag = "fealty:oath", LeviesTag = "fealty:levies", ProtectedTag = "fealty:protected", ShortTag = "fealty:short", VassalTag = "vassal:town",
            HeavyTag = "vassal:heavy", VassalLevyTag = "vassal:levy", LeviesSentTag = "fealty:levies:sent", LeviesRefusedTag = "fealty:levies:refused";

        public static readonly string[] Tags = { OathTag, LeviesTag, ProtectedTag, ShortTag, VassalTag, HeavyTag, VassalLevyTag, LeviesSentTag, LeviesRefusedTag };

        /// <summary>
        /// What became of their calls on the colony's levies this season, as the history keeps each (dip:levy): their people
        /// For it when the levies came to their town, Against it when the colony refused or nobody set out in time.
        /// </summary>
        public static void LevyOutcome(bool theirsFor, bool theirsAgainst, ref bool sent, ref bool refused)
        {
            if (theirsFor) sent = true;
            if (theirsAgainst) refused = true;
        }

        /// <summary>A season's worth of days: what "this season" means for the tags.</summary>
        public static bool ThisSeason(int at, int now) => at >= 0 && now - at < SeasonDays * DayTicks;

        // ================= Helpers =================

        public static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
