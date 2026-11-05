using System;
using System.Collections.Generic;
using System.Linq;
using ColonyCouncil;
using static ColonyCouncil.JourneyAdviceMath;

/// <summary>
/// Who to bring on a journey and what to pack (Source/ColonyCouncil/Journeys/JourneyAdviceMath.cs): the danger of a place,
/// the party's size by what the journey is for and the colony's size, each colonist placed with one reason, the home never
/// left without a doctor, a cook or enough defenders while another choice would do, pack animals by the load and mounts by
/// the people, medicine by the party and the danger, silver and trade goods for trade, and which of the stores and the
/// stalls an item is taken from.
/// </summary>
public static class JourneyAdviceTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void Check(bool ok, string what)
        {
            n++;
            check(ok, "journey advice: " + what);
        }

        Danger(Check);
        Party(Check);
        Placing(Check);
        Home(Check);
        Purposes(Check);
        Beasts(Check);
        Bringing(Check);
        Sources(Check);
        return n;
    }

    private static Person P(int id, int shoot = 5, int melee = 5, int med = 2, int social = 4, bool armed = true, bool ranged = true, bool doctor = false, bool cook = false)
    {
        var p = new Person { id = id, armed = armed, ranged = ranged, doctor = doctor, cook = cook };
        p.skills = new[] { shoot, melee, med, social, 3, 3, 3 };
        return p;
    }

    private static JourneyWants W(JourneyAim aim, JourneyDanger danger = JourneyDanger.None, float days = 2f, int minPeople = 0, int named = -1, int skill = -1) =>
        new JourneyWants { aim = aim, danger = danger, days = days, minPeople = minPeople, named = named, skill = skill, doctor = WantsDoctor(aim, danger, days) };

    // ---------------- The danger ----------------

    private static void Danger(Action<bool, string> Check)
    {
        Check(DangerOf(0f, 500f) == JourneyDanger.None, "no hostiles: no danger");
        Check(DangerOf(200f, 500f) == JourneyDanger.Low, "under half the home's threat: low");
        Check(DangerOf(450f, 500f) == JourneyDanger.Some, "about the home's threat: some");
        Check(DangerOf(800f, 500f) == JourneyDanger.High, "well past the home's threat: high");
        Check(DangerOf(100f, 0f) == JourneyDanger.Some, "a home without threat points still reads hostiles as some danger");
        Check(WantsDoctor(JourneyAim.Fight, JourneyDanger.Low, 1f), "a fight wants a doctor");
        Check(!WantsDoctor(JourneyAim.Trade, JourneyDanger.None, 2f), "a short trade trip wants none");
        Check(WantsDoctor(JourneyAim.Trade, JourneyDanger.None, 5f), "a road of five days wants one");
        Check(WantsDoctor(JourneyAim.Talk, JourneyDanger.Some, 1f), "danger on the way wants one");
    }

    // ---------------- The party ----------------

    private static void Party(Action<bool, string> Check)
    {
        Check(KeepHome(1) == 0 && KeepHome(2) == 1 && KeepHome(3) == 1, "one keeps nobody home, two or three keep one");
        Check(KeepHome(5) == 2 && KeepHome(10) == 4, $"two in five stay home ({KeepHome(5)}, {KeepHome(10)})");
        for (int g = 0; g <= 20; g++)
        {
            Check(MostToSend(g) + KeepHome(g) == Math.Max(g, KeepHome(g)), $"what goes and what stays add up for {g}");
            foreach (JourneyAim aim in Enum.GetValues(typeof(JourneyAim)))
                foreach (JourneyDanger d in Enum.GetValues(typeof(JourneyDanger)))
                {
                    var (min, ideal) = PartySize(aim, d, g, 0);
                    Check(min >= 1 && ideal >= min, $"{aim} {d} with {g} grown: at least one, the ideal no smaller ({min}, {ideal})");
                    Check(ideal <= Math.Max(1, g), $"{aim} {d} with {g} grown: never more than there are ({ideal})");
                    if (g >= 2) Check(ideal <= Math.Max(1, MostToSend(g)), $"{aim} {d} with {g} grown: the home keeps its defenders ({ideal})");
                }
        }
        var low = PartySize(JourneyAim.Fight, JourneyDanger.Low, 10, 0);
        var high = PartySize(JourneyAim.Fight, JourneyDanger.High, 10, 0);
        Check(high.ideal > low.ideal && high.min > low.min, $"a stronger place takes more fighters ({low.ideal} against {high.ideal})");
        Check(PartySize(JourneyAim.Fight, JourneyDanger.High, 3, 0).ideal == 2, "a colony of three sends two to a hard fight and keeps one home");
        Check(PartySize(JourneyAim.Escort, JourneyDanger.None, 2, 2).min == 2, "an escort's own two go even from a colony of two");
        Check(PartySize(JourneyAim.Talk, JourneyDanger.None, 8, 0).ideal == 1, "talks take one");
        Check(PartySize(JourneyAim.Talk, JourneyDanger.High, 8, 0).ideal == 3, "talks on a dangerous road take guards");
        Check(PartySize(JourneyAim.Trade, JourneyDanger.None, 8, 0).ideal == 2, "trade takes a negotiator and a carrier");
    }

    // ---------------- Placing each colonist ----------------

    private static void Placing(Action<bool, string> Check)
    {
        var w = W(JourneyAim.Fight, JourneyDanger.Some);
        Check(BetterNot(new Person { downed = true }, w) == AdviceWhy.Downed, "down: better not");
        Check(BetterNot(new Person { inBed = true }, w) == AdviceWhy.InBed, "in bed: better not");
        Check(BetterNot(new Person { sick = true }, w) == AdviceWhy.Sick, "ill: better not");
        Check(BetterNot(new Person { moving = 0.4f }, w) == AdviceWhy.Slow, "slow on their feet: better not");
        Check(BetterNot(new Person { pain = 0.5f }, w) == AdviceWhy.Pain, "in pain: better not");
        Check(BetterNot(new Person { pregnant = true }, w) == AdviceWhy.Pregnant, "with child: better not");
        Check(BetterNot(new Person { grown = false }, w) == AdviceWhy.ChildDanger, "a child to a fight: better not");
        Check(BetterNot(new Person { grown = false }, W(JourneyAim.Trade)) == AdviceWhy.None, "a child on a safe trade trip may go");
        Check(BetterNot(new Person { band = 3 }, w) == AdviceWhy.Breaking, "a breaking heart: better not");
        Check(BetterNot(new Person { mood = 0.2f, minor = 0.35f }, w) == AdviceWhy.Breaking, "a mood under the break line: better not");
        Check(BetterNot(new Person { violent = false }, w) == AdviceWhy.NoViolence, "one who won't fight, to a fight: better not");
        Check(BetterNot(new Person { violent = false }, W(JourneyAim.Trade)) == AdviceWhy.None, "one who won't fight may trade");
        Check(BetterNot(new Person(), w) == AdviceWhy.None, "a well colonist may go");

        var shot = P(1, shoot: 14);
        var brawler = P(2, shoot: 2, melee: 12, ranged: false);
        var bare = P(3, shoot: 14, armed: false);
        Check(FighterScore(shot) > FighterScore(bare), "a weapon in hand makes a better fighter");
        Check(FighterScore(new Person { violent = false, skills = new[] { 20, 20, 0, 0, 0, 0, 0 } }) == 0f, "one who won't fight counts for nothing in a fight");
        var armoured = P(4, shoot: 14);
        armoured.armour = 1f;
        Check(FighterScore(armoured) > FighterScore(shot), "armour counts");
        Check(FighterScore(brawler) > FighterScore(P(5, shoot: 2, melee: 12, ranged: true)), "a melee weapon counts whole for a brawler");

        // A fight at a colony of six: the best fighters, a doctor along, never the home's only doctor.
        var people = new List<Person>
        {
            P(1, shoot: 15), P(2, shoot: 12), P(3, shoot: 3, med: 12, doctor: true), P(4, shoot: 8, med: 7, doctor: true), P(5, shoot: 1, armed: false, cook: true), P(6, shoot: 6)
        };
        var a = Pick(people, w);
        Check(a.recommended.Contains(1) && a.recommended.Contains(2), "the two best shots are recommended");
        Check(a.recommended.Count == a.ideal, $"as many as the ideal ({a.recommended.Count} of {a.ideal})");
        Check(a.recommended.Any(id => people.First(p => p.id == id).Skill(Medicine) >= DoctorSkill), "a doctor goes to a fight");
        Check(!a.warnings.Contains(HomeWarning.NoDoctor), "a doctor stays home");
        Check(a.Of(1).why == AdviceWhy.BestFighter, $"the best shot is said to be the best fighter ({a.Of(1).why})");
        Check(a.Of(1).group == AdviceGroup.Recommended && a.placed.Count == people.Count, "everyone is placed once");
        Check(a.Of(5).group != AdviceGroup.Recommended, "the unarmed cook is not sent to a fight");

        // The only doctor isn't the journey's doctor while the home has nobody else.
        var one = new List<Person> { P(1, shoot: 15), P(2, shoot: 12), P(3, shoot: 3, med: 14, doctor: true), P(4, shoot: 6), P(5, shoot: 5) };
        var b = Pick(one, w);
        Check(!b.recommended.Contains(3), "the only doctor stays home");
        Check(b.Of(3).group == AdviceGroup.BetterNot && b.Of(3).why == AdviceWhy.OnlyDoctor, $"and is said to be the only doctor ({b.Of(3).why})");
        Check(!b.warnings.Contains(HomeWarning.NoDoctor), "so the home keeps its doctor");

        // Trade: the best talker leads; talks: the speaker leads.
        var traders = new List<Person> { P(1, social: 3), P(2, social: 13), P(3, social: 8), P(4, social: 2) };
        var t = Pick(traders, W(JourneyAim.Trade));
        Check(t.recommended.FirstOrDefault() == 2 && t.Of(2).why == AdviceWhy.Negotiator, "the best talker goes to trade");
        var talkers = new List<Person> { P(1, social: 13), P(2, social: 9), P(3, social: 2) };
        talkers[1].office = Speaker;
        var k = Pick(talkers, W(JourneyAim.Talk));
        Check(k.recommended.FirstOrDefault() == 2 && k.Of(2).why == AdviceWhy.Speaker, "the speaker goes to talks");
        talkers[1].skills[Social] = 2;
        Check(Pick(talkers, W(JourneyAim.Talk)).recommended.FirstOrDefault() == 1, "a speaker who can't talk gives way to the best talker");

        // An errand's named one goes first; a harvest's best hands.
        var named = Pick(traders, W(JourneyAim.Errand, named: 4));
        Check(named.recommended.FirstOrDefault() == 4 && named.Of(4).why == AdviceWhy.Named, "the errand's named one is recommended first");
        var hands = new List<Person> { P(1), P(2), P(3) };
        hands[2].skills[Plants] = 14;
        var h = Pick(hands, W(JourneyAim.Errand, minPeople: 2, skill: Plants));
        Check(h.recommended.FirstOrDefault() == 3 && h.Of(3).why == AdviceWhy.Hands && h.recommended.Count >= 2, "a harvest takes its best hands, two of them at least");

        // Children never fill a party; a child is said to be one.
        var family = new List<Person> { P(1), P(2), new Person { id = 3, grown = false, skills = new[] { 9, 9, 9, 9, 9, 9, 9 } } };
        var f = Pick(family, W(JourneyAim.Trade));
        Check(!f.recommended.Contains(3) && f.Of(3).why == AdviceWhy.Child, "a child can go but isn't recommended");

        // Better not stays better not, whatever the party lacks.
        var hurt = new List<Person> { P(1), P(2) };
        hurt[1].inBed = true;
        var u = Pick(hurt, W(JourneyAim.Escort, minPeople: 2));
        Check(!u.recommended.Contains(2) && u.Of(2).group == AdviceGroup.BetterNot, "one in bed is never recommended, even short of the escort's two");
        Check(Pick(new List<Person>(), w).recommended.Count == 0, "nobody at home: nobody recommended");
    }

    // ---------------- The home left behind ----------------

    private static void Home(Action<bool, string> Check)
    {
        var w = W(JourneyAim.Travel);
        var people = new List<Person> { P(1, doctor: true), P(2, cook: true), P(3), P(4), P(5) };
        Check(HomeLeft(people, new HashSet<int> { 1 }, w).Contains(HomeWarning.NoDoctor), "the only doctor away: no doctor at home");
        Check(HomeLeft(people, new HashSet<int> { 2 }, w).Contains(HomeWarning.NoCook), "the only cook away: no cook at home");
        Check(HomeLeft(people, new HashSet<int> { 3 }, w).Count == 0, "one of the rest away: nothing missed");
        Check(HomeLeft(people, new HashSet<int> { 1, 2, 3, 4, 5 }, w).SequenceEqual(new[] { HomeWarning.Empty }), "everyone away: nobody home");
        Check(HomeLeft(people, new HashSet<int> { 2, 3, 4, 5 }, w).Contains(HomeWarning.FewDefenders), $"four of five away: too few defenders (keep {KeepHome(5)})");
        Check(!HomeLeft(people, new HashSet<int> { 3, 4, 5 }, w).Contains(HomeWarning.FewDefenders), "three of five away: two stay, enough");
        Check(!HomeLeft(people, new HashSet<int> { 3, 4 }, w).Contains(HomeWarning.FewDefenders), "two of five away: enough stay");
        var grow = new List<Person> { P(1), P(2) };
        grow[0].grower = true;
        Check(HomeLeft(grow, new HashSet<int> { 1 }, new JourneyWants { growing = true }).Contains(HomeWarning.NoGrower), "in the growing season the only grower is missed");
        Check(!HomeLeft(grow, new HashSet<int> { 1 }, new JourneyWants { growing = false }).Contains(HomeWarning.NoGrower), "out of it, not");
        var cells = new List<Person> { P(1), P(2) };
        cells[0].warden = true;
        Check(HomeLeft(cells, new HashSet<int> { 1 }, new JourneyWants { prisoners = true }).Contains(HomeWarning.NoWarden), "with prisoners the only warden is missed");
        var baby = new List<Person> { P(1), P(2), P(3) };
        baby[0].babyAlone = true;
        Check(HomeLeft(baby, new HashSet<int> { 1 }, w).Contains(HomeWarning.BabyAlone), "a baby's only parent at home away: said");
        Check(BetterNot(baby[0], w) == AdviceWhy.BabyAlone, "and they had better not go");
        Check(HomeLeft(people, new HashSet<int>(), w).Count == 0, "nobody going: nothing missed");

        // The home's need gives way only to the party's least size.
        var two = new List<Person> { P(1, shoot: 15, doctor: true, cook: true), P(2, shoot: 2, armed: false) };
        var a = Pick(two, W(JourneyAim.Escort, minPeople: 2));
        Check(a.recommended.Count == 2 && a.warnings.Contains(HomeWarning.Empty), "an escort's two from a colony of two: both go, and the warning says nobody stays");
        var cost = new Person { id = 9, office = Marshal, loverStays = true };
        Check(HomeCost(cost, W(JourneyAim.Trade)) > HomeCost(cost, W(JourneyAim.Fight)), "the marshal's seat costs less on a fight");
        Check(Score(P(1), w) > Score(new Person { id = 2, skills = P(1).skills, armed = true, ranged = true, loverStays = true, band = 2 }, w), "a worn heart and a lover at home count against going");
    }

    // ---------------- Whole journeys ----------------

    private static void Purposes(Action<bool, string> Check)
    {
        var colony = new List<Person>();
        for (int i = 1; i <= 8; i++) colony.Add(P(i, shoot: i, melee: 9 - i, med: i % 4 * 3, social: (i * 5) % 11, doctor: i % 3 == 0, cook: i == 2));
        foreach (JourneyAim aim in Enum.GetValues(typeof(JourneyAim)))
            foreach (JourneyDanger d in Enum.GetValues(typeof(JourneyDanger)))
            {
                var a = Pick(colony, W(aim, d));
                Check(a.recommended.Count >= a.min && a.recommended.Count <= Math.Max(a.ideal, a.min), $"{aim} {d}: between the least and the ideal ({a.recommended.Count}: {a.min} to {a.ideal})");
                Check(a.recommended.Distinct().Count() == a.recommended.Count, $"{aim} {d}: nobody twice");
                Check(!a.warnings.Contains(HomeWarning.NoDoctor) && !a.warnings.Contains(HomeWarning.NoCook) && !a.warnings.Contains(HomeWarning.FewDefenders),
                    $"{aim} {d}: a colony of eight keeps its doctor, cook and defenders ({string.Join(",", a.warnings)})");
                Check(a.placed.All(p => (p.group == AdviceGroup.Recommended) == a.recommended.Contains(p.id)), $"{aim} {d}: the groups agree with the picks");
                Check(a.placed.All(p => p.why != AdviceWhy.None), $"{aim} {d}: everyone has a reason");
            }
    }

    // ---------------- Animals ----------------

    private static void Beasts(Action<bool, string> Check)
    {
        var beasts = new List<Beast>
        {
            new Beast { id = 1, pack = true, capacity = 140f }, new Beast { id = 2, pack = true, capacity = 70f }, new Beast { id = 3, capacity = 20f },
            new Beast { id = 4, pack = true, capacity = 100f, pregnant = true }, new Beast { id = 5, pack = true, capacity = 90f, young = true },
            new Beast { id = 6, pack = true, rideable = true, ride = 1.6f, capacity = 80f }, new Beast { id = 7, pack = true, capacity = 50f, hurt = true }
        };
        var light = PickBeasts(beasts, 50f, 100f, 0);
        Check(light.All(b => b.group != AdviceGroup.Recommended), "a load the people carry wants no pack animal");
        Check(light.First(b => b.id == 3).why == AdviceWhy.Pet && light.First(b => b.id == 3).group == AdviceGroup.BetterNot, "a pet that carries nothing: better not");
        Check(light.First(b => b.id == 4).why == AdviceWhy.Pregnant, "with young: better not");
        Check(light.First(b => b.id == 5).why == AdviceWhy.Young, "too young: better not");
        Check(light.First(b => b.id == 7).why == AdviceWhy.AnimalHurt, "hurt: better not");
        var heavy = PickBeasts(beasts, 260f, 100f, 0);
        var packs = heavy.Where(b => b.group == AdviceGroup.Recommended).Select(b => b.id).ToList();
        Check(packs.Contains(1) && packs.Contains(6) && !packs.Contains(2), $"a heavy load takes the strongest first ({string.Join(",", packs)})");
        float room = 100f + beasts.Where(b => packs.Contains(b.id)).Sum(b => b.capacity);
        Check(260f <= room * LoadMargin, $"enough room for the load with the margin ({room})");
        Check(!packs.Contains(4) && !packs.Contains(5) && !packs.Contains(7), "never one that had better not");
        var ride = PickBeasts(beasts, 50f, 100f, 2);
        Check(ride.First(b => b.id == 6).why == AdviceWhy.Mount, "a mount for a traveller");
        Check(PickBeasts(beasts, 50f, 100f, 0).First(b => b.id == 6).group == AdviceGroup.CanGo, "no travellers, no mounts");
        Check(PickBeasts(null, 10f, 0f, 1).Count == 0, "no animals: nothing");
    }

    // ---------------- What to bring ----------------

    private static void Bringing(Action<bool, string> Check)
    {
        int plain = JourneyMath.Medicine(3, 2f);
        Check(MedicineFor(3, 2f, JourneyDanger.None, false) == plain, "no danger: the planner's own medicine");
        Check(MedicineFor(3, 2f, JourneyDanger.Some, false) == plain + 2, $"some danger: half a dose more each ({MedicineFor(3, 2f, JourneyDanger.Some, false)})");
        Check(MedicineFor(3, 2f, JourneyDanger.High, true) == plain + 3, "high danger: a dose more each");
        Check(MedicineFor(3, 2f, JourneyDanger.Low, true) > plain, "a fight takes more even where it looks easy");
        Check(MedicineFor(0, 2f, JourneyDanger.High, true) == 0, "nobody going: none");
        Check(SilverFor(0) == 0 && SilverFor(60) == 60 && SilverFor(500) == 250 && SilverFor(150) == 100, "half the silver, in tens, at least a hundred where there is that much");

        var goods = new List<Goods>
        {
            new Goods { mass = 1f, value = 5f, count = 100 }, new Goods { mass = 0.5f, value = 40f, count = 10 }, new Goods { mass = 2f, value = 10f, count = 20, stall = true }
        };
        var picked = PickGoods(goods, 50f);
        Check(picked.Count > 0 && picked[0].goods == 2, "the stall's goods first, set out for sale already");
        Check(picked.Sum(g => g.count * goods[g.goods].mass) <= 50f * GoodsRoom + 0.01f, "within the room kept for goods");
        Check(picked.Select(g => g.goods).Distinct().Count() == picked.Count, "each kind once");
        Check(PickGoods(goods, 0f).Count == 0, "no room: no goods");
        Check(StillToAdd(5, 3) == 2 && StillToAdd(5, 7) == 0 && StillToAdd(5, -1) == 5, "the count still to add");
    }

    // ---------------- Stores or stall ----------------

    private static void Sources(Action<bool, string> Check)
    {
        var a = TakeFrom(10, 6, new[] { 3, 5 });
        Check(a.stores == 6 && a.stalls[0] == 3 && a.stalls[1] == 1 && a.shortBy == 0, "the stores first, then the stalls in turn");
        var b = TakeFrom(4, 9, new[] { 5 });
        Check(b.stores == 4 && b.stalls[0] == 0, "the stores have enough: nothing off the stall");
        var c = TakeFrom(10, 0, new[] { 4 });
        Check(c.stores == 0 && c.stalls[0] == 4 && c.shortBy == 6, "short of what the stall holds: said");
        var d = TakeFrom(0, 5, new[] { 5 });
        Check(d.stores == 0 && d.stalls.Sum() == 0 && d.shortBy == 0, "nothing wanted: nothing taken");
        var e = TakeFrom(3, -2, null);
        Check(e.stores == 0 && e.stalls.Length == 0 && e.shortBy == 3, "no stalls and nothing stored: all short");
    }
}
