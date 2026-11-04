using System;

namespace ColonyCouncil
{
    /// <summary>What the colony can ask of a people that owes it a debt of honour, when it calls the debt in.</summary>
    public enum FavourAsk
    {
        /// <summary>A load of goods (food, medicine, wood, steel or silver), about 400 silver's worth by their town's fortune, brought by their porters.</summary>
        Goods,
        /// <summary>A word for the colony with a third people they are on good terms with: 10 goodwill, said in that people's letter.</summary>
        Word,
        /// <summary>A colonist of the colony they hold, walked to the colony's land.</summary>
        Captive,
        /// <summary>Their help toward peace with a people at war with the colony: a promise kept for the truce's answer (stage 2, release 2a.2).</summary>
        Peace
    }

    /// <summary>What a people asks when it calls in a debt of honour the colony owes it: only what the world shows they need.</summary>
    public enum FavourNeed
    {
        /// <summary>Medicine while a sickness runs among them.</summary>
        Medicine,
        /// <summary>Food in a famine (a famine, locusts, a long drought, a hard winter).</summary>
        Food,
        /// <summary>Hands at their harvest: the existing mission.</summary>
        Harvest,
        /// <summary>Their homeless taken in: the existing mission.</summary>
        Refugees,
        /// <summary>The colony's word on their side in a dispute (a blood feud, a contested seat).</summary>
        Dispute
    }

    /// <summary>
    /// The arithmetic of favours owed and called in (docs/design/stage2-diplomacy.md, round a, item 1), with no game types
    /// so Tools/Tests can check it. A debt of honour is the world ledger's own Favour tie, origin <see cref="Origin"/>
    /// (the outsiders' "bring it next time" favours keep theirs): from the one who owes (their people, or a notable in
    /// person) to the one owed (the colony, or the colonist who did it), its terms the deed it came from (key), how big
    /// (number) and who did it (about), until two years after. One open each way per people and colony; a new good turn
    /// while one is open renews it; a notable's personal favours twelve at most. While it is called in its terms also say
    /// what was asked (goods: <see cref="AskCode"/> or <see cref="NeedCode"/>) and by when (days: the day it is due).
    /// </summary>
    public static class FavourMath
    {
        public const string Origin = "debts";
        public const int DayTicks = 60000;

        // ---------------- Earning one ----------------

        /// <summary>A good turn this big in their memory, or bigger, is a debt of honour.</summary>
        public const int MinSize = 10;

        /// <summary>A debt never asked for is let go two years (120 days) after the good turn that made or renewed it.</summary>
        public const float KeepDays = 120f;

        /// <summary>A notable's personal favours to single colonists: twelve open at most, the oldest let go first.</summary>
        public const int MaxPersonal = 12;

        /// <summary>
        /// The colony's good turns that leave a people owing it: their own freed or tended, a quest or mission done for them,
        /// a hard time eased, a dignitary well hosted, refugees taken in, a side taken, a quarrel mediated, peace made, a
        /// wrong forgiven, a pilgrim escorted, word carried, a child found, a harvest brought in, a town rebuilt, people
        /// sheltered or healed, a warning heeded, a camp that troubled their roads broken, a hamlet of theirs protected or
        /// helped, their envoy well kept, a ward fostered, a hostage returned. Not a trade, a gift or a payment (those are
        /// paid for), nor the colony's word kept (that answers a debt of its own).
        /// </summary>
        public static readonly string[] OurGoodTurns =
        {
            "released", "tended", "quest", "mission", "campHelped", "hardship", "dignitary", "refugees", "sidedWith", "mediated", "madePeace", "forgave",
            "escorted", "carriedWord", "foundChild", "harvestHelp", "rebuilt", "sheltered", "campHealed", "warned", "brokeCamp", "hamlet", "hamletHelped",
            "envoyHonored", "fostered", "hostageReturned"
        };

        /// <summary>
        /// Their good turns that leave the colony owing them: their fighters' help, relief or medicine sent, teaching, a
        /// raiders' camp near the colony struck or burned by them, their protection.
        /// </summary>
        public static readonly string[] TheirGoodTurns = { "aid", "relief", "taught", "letterAid", "campStruck", "burnedCamp", "protected" };

        /// <summary>A deed of this kind and size makes (or renews) a debt: <paramref name="colonyDidIt"/> for the colony's good turns, else theirs.</summary>
        public static bool Earns(string kind, bool colonyDidIt, int weight) =>
            weight >= MinSize && kind != null && Array.IndexOf(colonyDidIt ? OurGoodTurns : TheirGoodTurns, kind) >= 0;

        /// <summary>When a debt made or renewed now is let go if nobody asks for it.</summary>
        public static int Until(int now) => now + (int)(KeepDays * DayTicks);

        // ---------------- Honouring it ----------------

        /// <summary>Their head (or the notable who owes) thinks this well of the colony or more: they always honour it.</summary>
        public const int HonourRegard = 20;

        /// <summary>The chance they honour a debt called in: always from 20 regard, seven in ten below that, four in ten below 0.</summary>
        public static float HonourChance(int regard) => regard >= HonourRegard ? 1f : regard >= 0 ? 0.7f : 0.4f;

        public static bool Honours(int regard, float roll) => roll < HonourChance(regard);

        // ---------------- Their calls on the colony ----------------

        /// <summary>A debt the colony owes is called in after this many days on average (a daily look), when a need shows.</summary>
        public const float CallMeanDays = 90f;

        public static float DailyChance => 1f / CallMeanDays;

        public static bool CallsToday(float roll) => roll < DailyChance;

        /// <summary>One ask a season at most from each people.</summary>
        public const float AskGapDays = 15f;

        /// <summary>No ask from them before (<paramref name="lastAsk"/> -1), or the last a season ago or more.</summary>
        public static bool AskAllowed(int lastAsk, int now) => lastAsk < 0 || now - lastAsk >= AskGapDays * DayTicks;

        /// <summary>Days to answer an ask for goods (or the colony's word) before it counts as refused.</summary>
        public const float AnswerDays = 3f;

        /// <summary>Days a mission they asked through (hands at harvest, refugees) has to run its course: answered by its own end.</summary>
        public const float MissionDays = 25f;

        /// <summary>Days the colony's word on their side has to reach them (the letter's walk there).</summary>
        public const float DisputeDays = 12f;

        /// <summary>How long their call stands: three days for goods, the mission's course for a mission, the letter's walk for the colony's word.</summary>
        public static float DueDays(FavourNeed n)
        {
            switch (n)
            {
                case FavourNeed.Harvest:
                case FavourNeed.Refugees: return MissionDays;
                case FavourNeed.Dispute: return DisputeDays;
                default: return AnswerDays;
            }
        }

        /// <summary>The day (in game days, from the start) a call made at <paramref name="now"/> is due.</summary>
        public static float DueDay(FavourNeed n, int now) => now / (float)DayTicks + DueDays(n);

        /// <summary>Past its due day, unanswered.</summary>
        public static bool Overdue(float dueDay, int now) => dueDay > 0f && now / (float)DayTicks >= dueDay;

        /// <summary>Goodwill lost when the colony leaves their call unanswered or refuses it (with the deed "refused").</summary>
        public const int RefusedGoodwill = -5;

        /// <summary>Asked through a mission of the game's: answered (kept) when the colony's deed for it is noted.</summary>
        public static bool ByMission(FavourNeed n) => n == FavourNeed.Harvest || n == FavourNeed.Refugees;

        /// <summary>The deed that answers what they asked: hands at the harvest, refugees taken in, the colony's word on their side.</summary>
        public static bool Answers(FavourNeed n, string deedKind) =>
            n == FavourNeed.Harvest && deedKind == "harvestHelp" || n == FavourNeed.Refugees && deedKind == "refugees" || n == FavourNeed.Dispute && deedKind == "sidedWith";

        /// <summary>The mission kind (Mission.kind) a need asked through a mission is: "harvest", "refugees"; null for the rest.</summary>
        public static string MissionKind(FavourNeed n) => n == FavourNeed.Harvest ? "harvest" : n == FavourNeed.Refugees ? "refugees" : null;

        /// <summary>How much medicine (of the plainest the colony has) and raw food they ask for.</summary>
        public const int MedicineCount = 8, FoodCount = 75;

        // ---------------- What the colony asks ----------------

        /// <summary>A load of goods is about this much silver's worth at a steady town.</summary>
        public const float GoodsValue = 400f;

        /// <summary>The load by their town's fortune (0 to 100): three quarters of it from a struggling town, a quarter more from a flourishing one.</summary>
        public static float GoodsWorth(float prosperity) => GoodsValue * (0.75f + Math.Max(0f, Math.Min(100f, prosperity)) / 200f);

        /// <summary>How many of a thing make up the load (at least one).</summary>
        public static int GoodsCount(float worth, float unitValue) => unitValue <= 0f ? 0 : Math.Max(1, (int)Math.Round(worth / unitValue));

        /// <summary>The kinds of goods they can bring, by their age: no steel from a tribe, no silver either (a tribe pays in goods).</summary>
        public static string[] GoodsKinds(WorldAge theirs)
        {
            switch (theirs)
            {
                case WorldAge.Tribal: return new[] { "food", "medicine", "wood" };
                case WorldAge.Medieval: return new[] { "food", "medicine", "wood", "steel", "silver" };
                default: return new[] { "food", "medicine", "steel", "silver" };
            }
        }

        /// <summary>Goodwill a word put in with a third people brings.</summary>
        public const int WordGoodwill = 10;

        /// <summary>How much better a people at war with the colony thinks of a truce asked with their help (read by 2a.2's truce answer).</summary>
        public const int PeaceRegard = 20;

        /// <summary>Their promise to speak for peace stands this long (a year).</summary>
        public const float PeaceDays = 60f;

        /// <summary>The key of the Promise tie a people's help toward peace leaves (from them to the colony, about the people at war).</summary>
        public const string PeaceKey = "peace";

        // ---------------- The ask in a tie's terms ----------------

        /// <summary>What the colony asked, in a tie's terms: "ask:Goods:Steel", "ask:Word:f12", "ask:Captive:p345", "ask:Peace:f7".</summary>
        public static string AskCode(FavourAsk a, string arg) => "ask:" + a + ":" + (arg ?? "");

        /// <summary>What they asked, in a tie's terms: "need:Medicine".</summary>
        public static string NeedCode(FavourNeed n) => "need:" + n;

        public static bool ParseAsk(string code, out FavourAsk ask, out string arg)
        {
            ask = FavourAsk.Goods;
            arg = null;
            if (code == null || !code.StartsWith("ask:", StringComparison.Ordinal)) return false;
            var parts = code.Split(new[] { ':' }, 3);
            if (parts.Length < 2 || !Enum.TryParse(parts[1], out ask) || !Enum.IsDefined(typeof(FavourAsk), ask)) return false;
            arg = parts.Length > 2 ? parts[2] : "";
            return true;
        }

        public static bool ParseNeed(string code, out FavourNeed need)
        {
            need = FavourNeed.Medicine;
            if (code == null || !code.StartsWith("need:", StringComparison.Ordinal)) return false;
            return Enum.TryParse(code.Substring(5), out need) && Enum.IsDefined(typeof(FavourNeed), need);
        }

        // ---------------- Which is the debt ----------------

        /// <summary>The colony owes it (the debt runs from one of its homes to a people).</summary>
        public static bool ColonyOwes(Ref from, Ref to) => from.kind == RefKind.Colony && to.kind == RefKind.People;

        /// <summary>A personal favour: a person owes a person (a notable to the colonist who did it).</summary>
        public static bool Personal(Ref from, Ref to) => from.kind == RefKind.Person && to.kind == RefKind.Person;
    }
}
