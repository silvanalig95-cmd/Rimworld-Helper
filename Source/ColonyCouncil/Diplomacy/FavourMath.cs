using System;

namespace ColonyCouncil
{
    /// <summary>What the colony can ask of a people that owes it a debt of honour, when it calls the debt in.</summary>
    public enum FavourAsk
    {
        /// <summary>A load of goods (food, medicine, wood, steel or silver), worth 30 silver for each point of the debt by their town's fortune and their means, brought by their porters.</summary>
        Goods,
        /// <summary>A word for the colony with a third people they are on good terms with: half the debt's size in goodwill, said in that people's letter.</summary>
        Word,
        /// <summary>A colonist of the colony they hold, walked to the colony's land: a debt of 15 or more.</summary>
        Captive,
        /// <summary>Their help toward peace with a people at war with the colony: a promise the truce's answer reads (PactMath.TruceScore); a debt of 12 or more.</summary>
        Peace,
        /// <summary>Their fighters, only while a threat is on a home: their war band walks in some hours later (Diplomacy/Pacts.cs, release 2a.2); a debt of 12 or more.</summary>
        Fighters
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
        Dispute,
        /// <summary>Fighters while their town within reach is attacked: two colonists or more set out for it (Diplomacy/CallsToArms.cs, release 2a.2).</summary>
        Fighters
    }

    /// <summary>
    /// Who owes a debt, for what goods they have to give (the player, 11 November 2026: "based on what are these favours
    /// created? this guy is a trader"): their people's town stores, or a notable in person by their trade.
    /// </summary>
    public enum GoodsFrom
    {
        /// <summary>Their people's debt: from their town's stores, by its fortune.</summary>
        Stores,
        /// <summary>A trader (their town's trader, or one who called at the colony): what they deal in.</summary>
        Trader,
        /// <summary>A healer: medicine.</summary>
        Healer,
        /// <summary>A farmer: food.</summary>
        Farmer,
        /// <summary>A herder: food.</summary>
        Herder,
        /// <summary>A smith (or a crafter of a tribe): metal (a tribe's, wood).</summary>
        Smith,
        /// <summary>Anyone else in person: what a household can spare, food (and silver past the tribal age).</summary>
        Person
    }

    /// <summary>A debt's weight in words: "a small debt", "a debt", "a great debt".</summary>
    public enum DebtWeight
    {
        Small,
        Debt,
        Great
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
        /// helped, their envoy well kept, a ward fostered, a hostage returned, hands lent to their town, one of theirs lost
        /// on the colony's land found, a work built together with them. Not a trade, a gift or a payment (those are
        /// paid for), nor the colony's word kept (that answers a debt of its own).
        /// </summary>
        public static readonly string[] OurGoodTurns =
        {
            "released", "tended", "quest", "mission", "campHelped", "hardship", "dignitary", "refugees", "sidedWith", "mediated", "madePeace", "forgave",
            "escorted", "carriedWord", "foundChild", "harvestHelp", "rebuilt", "sheltered", "campHealed", "warned", "brokeCamp", "hamlet", "hamletHelped",
            "envoyHonored", "fostered", "hostageReturned", "lentHands", "foundLost", "builtTogether"
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
                case FavourNeed.Fighters: return PactMath.SetOutDays;
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

        /// <summary>
        /// A load of goods is worth this much silver for each point of the debt's size, at a steady town of an industrial
        /// people (the player, 10 November 2026: "balanced, appropriate to the debt i actually owe"): a warning's 10, 300; a
        /// family of refugees taken in (20), 600. Until then any debt brought 400 whatever it was for.
        /// </summary>
        public const float GoodsPerSize = 30f;

        /// <summary>Their town's fortune (0 to 100) as a factor: three quarters from a struggling town, one at a steady one, a quarter more from a flourishing one.</summary>
        public static float FortuneFactor(float prosperity) => 0.75f + Math.Max(0f, Math.Min(100f, prosperity)) / 200f;

        /// <summary>Their means, by their age: what their deliveries carry (DealingMath.TheirLoadValue) against an industrial people's: tribal 0.55, medieval 0.75, industrial 1, spacer 1.25.</summary>
        public static float Means(WorldAge theirs) => DealingMath.TheirLoadValue(theirs) / DealingMath.TheirLoadValue(WorldAge.Industrial);

        /// <summary>The load's worth: the debt's size, by their town's fortune and their means.</summary>
        public static float GoodsWorth(int size, float prosperity, float means) => Math.Max(0, size) * GoodsPerSize * FortuneFactor(prosperity) * Math.Max(0f, means);

        /// <summary>A notable's personal favour gives half of what their people's debt of the same size would.</summary>
        public static int Effective(int size, bool personal) => personal ? Math.Max(1, size / 2) : Math.Max(0, size);

        /// <summary>A word put in with a third people brings half the debt's size in goodwill (a warning's 10: 5; refugees' 20: 10).</summary>
        public static int WordGoodwill(int size) => Math.Max(1, size / 2);

        /// <summary>Their help toward peace and their fighters need a debt of 12 or more; a colonist they hold set free, 15 or more.</summary>
        public const int PeaceSize = 12, FightersSize = 12, CaptiveSize = 15;

        /// <summary>The smallest debt that can ask this of them (goods and a word: any).</summary>
        public static int NeedsSize(FavourAsk a)
        {
            switch (a)
            {
                case FavourAsk.Peace: return PeaceSize;
                case FavourAsk.Fighters: return FightersSize;
                case FavourAsk.Captive: return CaptiveSize;
                default: return 0;
            }
        }

        public static bool Allows(FavourAsk a, int size) => size >= NeedsSize(a);

        /// <summary>A debt's weight in words: small below 12, a debt from 12, a great debt from 18.</summary>
        public const int DebtFrom = 12, GreatFrom = 18;

        public static DebtWeight WeightOf(int size) => size >= GreatFrom ? DebtWeight.Great : size >= DebtFrom ? DebtWeight.Debt : DebtWeight.Small;

        /// <summary>
        /// A debt's size: its terms' number, as the good turn that made it weighed (renewed, the greater); an older one with
        /// none, the weight of the deed it came from (refugees 20, a hardship eased 15 and so on: FactionMemory.Kinds), else
        /// <see cref="MinSize"/>.
        /// </summary>
        public static int SizeOf(float number, int kindWeight) => number >= 1f ? (int)Math.Round(number) : kindWeight > 0 ? kindWeight : MinSize;

        /// <summary>One debt of theirs called in a season at most, by letter or face to face (as their own asks: <see cref="AskGapDays"/>).</summary>
        public const float CallGapDays = AskGapDays;

        /// <summary>No call on them before (<paramref name="lastCall"/> -1), or the last a season ago or more.</summary>
        public static bool CallAllowed(int lastCall, int now) => lastCall < 0 || now - lastCall >= CallGapDays * DayTicks;

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

        // ---------------- Goods by who owes (the player, 11 November 2026: "4 yoghurts is a funny option?") ----------------

        /// <summary>A skill this high (and their best of medicine, plants, animals and crafting) makes someone a healer, farmer, herder or smith for what they give.</summary>
        public const int TradeSkill = 8;

        /// <summary>Who owes, for the goods: their people's stores; in person, a trader by their trade, else by their best skill when it is a trade.</summary>
        public static GoodsFrom From(bool personal, bool trader, int medicine, int plants, int animals, int crafting)
        {
            if (!personal) return GoodsFrom.Stores;
            if (trader) return GoodsFrom.Trader;
            int best = Math.Max(Math.Max(medicine, plants), Math.Max(animals, crafting));
            if (best < TradeSkill) return GoodsFrom.Person;
            if (medicine == best) return GoodsFrom.Healer;
            if (plants == best) return GoodsFrom.Farmer;
            if (animals == best) return GoodsFrom.Herder;
            return GoodsFrom.Smith;
        }

        /// <summary>
        /// The goods they can give, by who owes: a trader what they deal in (and silver past the tribal age), a healer medicine,
        /// a farmer or herder food, a smith steel (a tribe's crafter, wood), anyone else food (and silver past the tribal age);
        /// their people's stores by its age (<see cref="GoodsKinds"/>), what their people are known for first (a people of
        /// healers, medicine; of herders, hunters, fishers, grovekeepers or tamers, food; of traders or brokers, trade goods; of
        /// drillers or salvagers, steel).
        /// </summary>
        public static string[] GoodsKindsFrom(GoodsFrom from, WorldAge age, string calling)
        {
            bool tribal = age == WorldAge.Tribal;
            switch (from)
            {
                case GoodsFrom.Trader: return tribal ? new[] { "trade" } : new[] { "trade", "silver" };
                case GoodsFrom.Healer: return new[] { "medicine" };
                case GoodsFrom.Farmer:
                case GoodsFrom.Herder: return new[] { "food" };
                case GoodsFrom.Smith: return tribal ? new[] { "wood" } : new[] { "steel" };
                case GoodsFrom.Person: return tribal ? new[] { "food" } : new[] { "food", "silver" };
            }
            var kinds = new System.Collections.Generic.List<string>(GoodsKinds(age));
            string first = CallingGoods(calling);
            if (first != null && (first != "steel" || !tribal))
            {
                kinds.Remove(first);
                kinds.Insert(0, first);
            }
            return kinds.ToArray();
        }

        /// <summary>What a people of this calling (Peoples/) is known to have to give, or null.</summary>
        public static string CallingGoods(string calling)
        {
            switch (calling)
            {
                case "healers": return "medicine";
                case "herders":
                case "hunters":
                case "fishers":
                case "grovekeepers":
                case "tamers": return "food";
                case "traders":
                case "brokers": return "trade";
                case "drillers":
                case "salvagers": return "steel";
                default: return null;
            }
        }

        /// <summary>Food in a load is a staple: no fine or lavish dish, at most this much silver for each unit of nutrition (a simple meal 17, grain 22, a survival meal 27).</summary>
        public const float StapleValuePerNutrition = 60f;

        /// <summary>And never a handful of it: at least this many in the load.</summary>
        public const int StapleMinCount = 10;

        /// <summary>A food that makes a sensible staple load worth <paramref name="worth"/>: plain, cheap for what it feeds, and not a handful.</summary>
        public static bool Staple(bool fineOrBetter, float value, float nutrition, float worth) =>
            !fineOrBetter && nutrition > 0f && value > 0f && value / nutrition <= StapleValuePerNutrition && GoodsCount(worth, value) >= StapleMinCount;

        /// <summary>Trade goods in a load: a material that comes in loads, worth this much a unit at most (cloth, leather, metal, blocks; no gold bars by the handful).</summary>
        public const float TradeGoodsMaxValue = 15f;

        public static bool TradeGood(float value, int stackLimit) => value >= 0.3f && value <= TradeGoodsMaxValue && stackLimit >= 10;

        // ---------------- A word with a third people ----------------

        /// <summary>A word's peoples shown at once (a handful); the rest are folded under "Others".</summary>
        public const int WordsShown = 4;

        /// <summary>Which peoples a word helps most come first: the coolest toward the colony and the nearest (lower first).</summary>
        public static float WordRank(int goodwill, float tiles) => goodwill + Math.Max(0f, tiles);

        /// <summary>A word does nothing for a people already at full goodwill toward the colony.</summary>
        public static bool WordHelps(int goodwill) => goodwill < 100;

        /// <summary>How much a word raises their goodwill, in words: "a little" below 6, "a fair amount" below 9, else "a good deal".</summary>
        public static string WordMuchKey(int goodwill) => goodwill >= 9 ? "Much" : goodwill >= 6 ? "Some" : "Little";

        // ---------------- The menu (the player, 11 November 2026: "this favour menu is also a bit overwhelming") ----------------

        /// <summary>
        /// The order of what can be asked: by kind (goods, a word, a captive, peace, fighters), what can be asked now first;
        /// the words past the first handful under "Others", and what can't be asked now under "Not now" with why. Each item is
        /// (its kind's order, can be asked now, folded under Others); returns the indexes of those shown, under Others and under
        /// Not now, each in order.
        /// </summary>
        public static void MenuOrder(System.Collections.Generic.IList<(int group, bool can, bool more)> items, out System.Collections.Generic.List<int> shown,
            out System.Collections.Generic.List<int> others, out System.Collections.Generic.List<int> notNow)
        {
            shown = new System.Collections.Generic.List<int>();
            others = new System.Collections.Generic.List<int>();
            notNow = new System.Collections.Generic.List<int>();
            if (items == null) return;
            // A stable sort by kind: each kind's own order kept.
            var sorted = new System.Collections.Generic.List<int>();
            for (int i = 0; i < items.Count; i++) sorted.Add(i);
            sorted.Sort((a, b) => items[a].group != items[b].group ? items[a].group.CompareTo(items[b].group) : a.CompareTo(b));
            foreach (int i in sorted)
            {
                if (!items[i].can) notNow.Add(i);
                else if (items[i].more) others.Add(i);
                else shown.Add(i);
            }
        }

        /// <summary>Days of the road for a deed done far away (a captive's walk home, their word reaching another people): at least half a day.</summary>
        public static float DeedDays(float roadDays) => Math.Max(0.5f, roadDays);

        /// <summary>A debt honoured by letter waits for its answer to reach the colony, or this many days after it was read at most (an answer lost on the road).</summary>
        public const float LetterDeedFallbackDays = 30f;

        /// <summary>The deeds that wait for the road (or the answer): a word with another people, a captive let go, their help toward peace.</summary>
        public static bool DoneLater(FavourAsk a) => a == FavourAsk.Word || a == FavourAsk.Captive || a == FavourAsk.Peace;

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
