using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The pure rules of release D's four peoples (docs/design/peoples.md, "Release D"; docs/design/peoples-next.md), with
    /// no game types so Tools/Tests can check them: the Lantern Wardens' hunt and their pelt-buyer, the Quiet Bell's bell
    /// for the dead and its funerals, the Circuit of Tamers' bouts and eggs, the Wheel's scrap, its wrecks and its towns
    /// that move; and, for all eleven peoples, the age a people lives in and whether a gift or a kit is of that age.
    /// </summary>
    public static class PeoplesDMath
    {
        public const int DayTicks = 60000;

        // ================= The age a people lives in =================

        /// <summary>An age range that names no age (-1~-1): any age.</summary>
        public static bool AnyAge(int lo, int hi) => lo < 0 && hi < 0;

        /// <summary>A gift or a kit of these ages (0 tribal to 3 spacer) fits a people of this age; an unset range fits every age.</summary>
        public static bool AgeFits(int age, int lo, int hi)
        {
            if (AnyAge(lo, hi)) return true;
            if (lo < 0) lo = 0;
            if (hi < 0) hi = 3;
            return age >= lo && age <= hi;
        }

        /// <summary>
        /// The age a people of one of Rimfolk's templates lives in: its own template's, unless it arose (release B) from
        /// something of another age within the span its template arises in: survivors and faithful who broke away keep
        /// the age of the people they came from (<paramref name="parentAge"/>), a band of the banished the colony's
        /// (<paramref name="colonyAge"/>); a league, or a people that came from nothing known, its template's own. Kept
        /// within the template's span (<paramref name="lo"/> to <paramref name="hi"/>).
        /// </summary>
        public static int PeopleAge(string arisenKind, int parentAge, int colonyAge, int own, int lo, int hi)
        {
            int age = own;
            if (arisenKind == ArisingMath.Refugees || arisenKind == ArisingMath.Splinter) age = parentAge >= 0 ? parentAge : own;
            else if (arisenKind == ArisingMath.Exiles) age = colonyAge >= 0 ? colonyAge : own;
            if (lo < 0 || hi < lo) return Clamp(age, 0, 3);
            return Clamp(age, lo, hi);
        }

        private static int Clamp(int v, int lo, int hi) => Math.Max(lo, Math.Min(hi, v));

        // ================= The Lantern Wardens =================

        /// <summary>Two or three Wardens come for a great beast on the colony's land.</summary>
        public const int HuntersMin = 2, HuntersMax = 3;

        /// <summary>The Wardens watch for the beast's end this long after the answer before they give it up and go.</summary>
        public const int HuntDays = 2;

        /// <summary>
        /// The beast they come for: an alpha of a pack, a beast nemesis, or a legend of the region, on the colony's land,
        /// wild and on its feet. The legend first (the biggest story), then the nemesis, then the alpha.
        /// </summary>
        public static int QuarryRank(bool legend, bool nemesis, bool alpha) => legend ? 3 : nemesis ? 2 : alpha ? 1 : 0;

        /// <summary>The trophy shared: the hide is the Wardens' for their book, the meat the colony's (their share of a beast: this much of its leather).</summary>
        public const float HideShare = 1f;

        /// <summary>A Warden works on the carcass this long before the hide is off (two hours).</summary>
        public const int SkinTicks = 2 * 2500;

        /// <summary>The pelt-buyer comes when the colony has this many pelts at least, and buys this many at most.</summary>
        public const int PeltsMin = 20, PeltsMax = 60;

        /// <summary>The Wardens pay the book's rate for good pelts: a little over their worth.</summary>
        public const float PeltRate = 1.2f;

        /// <summary>How many pelts they buy of so many in the stores: all of them, up to the most they carry.</summary>
        public static int Pelts(int have) => have < PeltsMin ? 0 : Math.Min(have, PeltsMax);

        /// <summary>What the pelts fetch, to the nearest five silver, never less than 20.</summary>
        public static int PeltPrice(int count, float each)
        {
            if (count <= 0 || float.IsNaN(each) || each <= 0f) return 0;
            int v = (int)Math.Round(count * each * PeltRate / 5f) * 5;
            return Math.Max(20, v);
        }

        /// <summary>A beast took one of the colony this many days ago at most when a Warden comes to light a lantern for them.</summary>
        public const int LanternDays = 30;

        public static bool LanternFor(int killedTick, int now) => killedTick >= 0 && now >= killedTick && now - killedTick <= (long)LanternDays * DayTicks;

        // ================= The Quiet Bell =================

        /// <summary>Ringing for the raiders' dead and burying them: so much goodwill with each people among the dead, for three peoples at most.</summary>
        public const int BellGoodwill = 6, BellPeoples = 3;

        /// <summary>Goodwill with one of the dead's peoples: more for more of theirs seen to, within the bell's own.</summary>
        public static int GoodwillFor(int theirDead) => theirDead <= 0 ? 0 : Math.Min(BellGoodwill + 4, BellGoodwill + (theirDead - 1) / 2);

        /// <summary>A bell-keeper comes to lead a funeral for one of the colony who died within this many days.</summary>
        public const int FuneralDays = 6;

        public static bool Fresh(int deadTicks) => deadTicks >= 0 && deadTicks <= (long)FuneralDays * DayTicks;

        /// <summary>With Anomaly: they warn of the void's signs when none of its threats has come for this many days.</summary>
        public const int VoidQuietDays = 8;

        public static bool VoidStirring(int level, int daysSinceThreat) => level >= 1 && daysSinceThreat >= VoidQuietDays;

        // ================= The Circuit of Tamers =================

        /// <summary>
        /// A friendly bout, by the Fighting-type spar's rules (Animals/AnimalPowersMath.cs): practice swings each way in
        /// turn, melee practice for the colony's trainer, and one bout in 25 the companion's blow lands, a bruise of 3 to
        /// 6 through the harm setting's cap and scale.
        /// </summary>
        public const float BoutBlowChance = 1f / 25f;

        public const float BoutLow = 3f, BoutHigh = 6f;

        public const int BoutMinTicks = 900, BoutMaxTicks = 1500, BoutSwingTicks = 110;

        public const float BoutXpSwing = 15f, BoutAnimalsXp = 400f;

        /// <summary>
        /// The colony's trainer beats the tamer's companion fairly, by their way with beasts and their own hands: a fifth
        /// of the bouts for someone green, four in five at most for a master of both.
        /// </summary>
        public static float WinChance(int animals, int melee)
        {
            float p = 0.2f + 0.03f * (animals - 6) + 0.02f * (melee - 6);
            return Math.Max(0.15f, Math.Min(0.8f, p));
        }

        /// <summary>A stranger who beats a tamer fairly is the Circuit's highest honour: their token, and this much goodwill.</summary>
        public const int WinGoodwill = 6, LoseGoodwill = 3;

        /// <summary>An egg of the Circuit's hatchery: one or two, at half again their worth, to the nearest five silver.</summary>
        public static int EggPrice(float each, int count)
        {
            if (count <= 0 || float.IsNaN(each) || each <= 0f) return 0;
            return Math.Max(20, (int)Math.Round(each * count * 1.5f / 5f) * 5);
        }

        /// <summary>A ranger brings home one of the colony's animals that strayed, if it went within this many days.</summary>
        public const int StrayDays = 120;

        public static bool StrayRecent(int goneTick, int now) => goneTick >= 0 && now >= goneTick && now - goneTick <= (long)StrayDays * DayTicks;

        /// <summary>What the colony may give the ranger for their trouble: a tenth of the animal's worth, 15 silver at least.</summary>
        public static int Finder(float worth) => float.IsNaN(worth) || worth <= 0f ? 15 : Math.Max(15, (int)Math.Round(worth * 0.1f / 5f) * 5);

        // ================= The Wheel =================

        /// <summary>The scrap-runners come for this many pieces of scrap at least (slag, and clothes and weapons too far gone to wear), and take this many at most.</summary>
        public const int ScrapMin = 6, ScrapMax = 30;

        /// <summary>A chunk of slag fetches this much; a broken piece a share of what it was worth new.</summary>
        public const float SlagPrice = 4f, BrokenShare = 0.15f;

        /// <summary>A piece too far gone to wear or fight with: under two fifths of its hit points, or worn by the dead.</summary>
        public const float BrokenAt = 0.4f;

        public static bool Broken(float hitPointsShare, bool tainted) => tainted || !float.IsNaN(hitPointsShare) && hitPointsShare < BrokenAt;

        /// <summary>What the scrap fetches: so much a slag chunk, a share of the broken pieces' worth, to the nearest five.</summary>
        public static int ScrapPrice(int slag, float brokenWorth)
        {
            float v = Math.Max(0, slag) * SlagPrice + (float.IsNaN(brokenWorth) ? 0f : Math.Max(0f, brokenWorth)) * BrokenShare;
            return v <= 0f ? 0 : Math.Max(10, (int)Math.Round(v / 5f) * 5);
        }

        /// <summary>A mechanoid's wreck stripped where it lies: half its parts for the colony, half for the Wheel.</summary>
        public const float WreckShare = 0.5f;

        /// <summary>The wreck sold whole: three fifths of what its parts are worth, to the nearest ten.</summary>
        public static int WreckPrice(float partsWorth) => float.IsNaN(partsWorth) || partsWorth <= 0f ? 0 : Math.Max(20, (int)Math.Round(partsWorth * 0.6f / 10f) * 10);

        /// <summary>How many of a part the colony keeps of so many (half, rounded down, at least one when there are two).</summary>
        public static int Half(int n) => n <= 0 ? 0 : n == 1 ? 0 : n / 2;

        /// <summary>The Wheel's runners strip a wreck for this long (three hours).</summary>
        public const int StripTicks = 3 * 2500;

        /// <summary>
        /// A Wheel town moves on: looked at on the region's day, each town on its own day of every 30 (by its id), a few
        /// tiles along (2 to 5), never while the colony has a map of it or a caravan bound for it.
        /// </summary>
        public const int MoveEveryDays = 30, MoveMin = 2, MoveMax = 5;

        public static bool MoveDay(int day, int id) => day >= 0 && (day + (id & 0x7fffffff) % MoveEveryDays) % MoveEveryDays == 0;
    }
}
