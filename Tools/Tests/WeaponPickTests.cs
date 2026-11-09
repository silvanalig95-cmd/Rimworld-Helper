using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Step 3 of the weapons (docs/design/weapons.md, points 8 and 9): the best weapon for each colonist
/// (Weapons/WeaponPickMath.cs): the threshold, one assignment for the whole colony (never one weapon to two, near the best
/// total, a weapon moved on so a second colonist can have one), what stops a suggestion (can't, a brawler's gun, a despised
/// weapon, a bonded persona weapon left unused, another kind), trades better for both, Simple Sidearms' sidearms and Dual
/// Wield's off hands, the colony's fights from its raids, a hunter, passions, faith, the reasons and their order; when a
/// colonist asks; who should carry a weapon; and the seams (the settings, Rimfolk's card first, the hooks, the clock, the
/// saved book, the pool's lines in every age, the keys).
/// </summary>
public static class WeaponPickTests
{
    static readonly string[] RangedSits = { "mechanoids", "heavily armoured", "crowds", "hunting", "indoors", "holding a choke", "kiting", "siege or long duel", "capture alive", "sidearm" };
    static readonly string[] MeleeSits = { "mechanoids", "heavily armoured", "duel", "crowds", "hunting", "indoors", "holding a choke", "capture alive", "sidearm" };

    static WeaponRating R(string kind, float overall, float skillGain = 1.2f, params (string sit, float v)[] sits)
    {
        var r = new WeaponRating { Kind = kind, Role = kind == "ranged" ? "all-round" : "melee", Overall = overall, SkillGain = skillGain };
        foreach (var k in kind == "ranged" ? RangedSits : MeleeSits) r.Situations[k] = overall;
        foreach (var (sit, v) in sits) r.Situations[sit] = v;
        return r;
    }

    /// <summary>A small colony built by hand: colonists, weapons, and each one's own rating of each.</summary>
    sealed class Colony
    {
        public readonly PickInput I = new PickInput();

        public PickColonist C(string key, int shooting = 8, int melee = 8, string holds = null)
        {
            var c = new PickColonist { Key = key, Name = key, Shooting = shooting, Melee = melee, Holds = holds };
            I.Colonists.Add(c);
            return c;
        }

        public PickWeapon W(string key, WeaponRating r, bool stored = true, string holder = null, float mass = 3f, string group = null)
        {
            var w = new PickWeapon { Key = key, Label = key, Rating = r, Stored = stored, Holder = holder, Mass = mass, Group = group ?? "", HuntingOk = r.Kind == "ranged" };
            I.Weapons.Add(w);
            return w;
        }

        public PickOption O(string c, string w, float rating, string cant = null, bool noble = false, float? pair = null)
        {
            var o = new PickOption { Rating = rating, Cant = cant, Noble = noble, Pair = pair };
            I.Options[(c, w)] = o;
            return o;
        }

        public List<PickSuggestion> Assign() => WeaponPickMath.Assign(I);
    }

    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "weapon suggestions: " + what);
        }
        bool Near(float a, float b, float eps = 0.01f) => Math.Abs(a - b) <= eps;
        string root = Directory.GetCurrentDirectory();
        while (root != null && !File.Exists(Path.Combine(root, "CLAUDE.md"))) root = Path.GetDirectoryName(root);
        string Read(string rel) => File.Exists(Path.Combine(root, rel)) ? File.ReadAllText(Path.Combine(root, rel)) : "";

        // ---------- The threshold ----------
        C(Near(WeaponPickMath.Threshold, 0.6f) && WeaponPickMath.AskThreshold >= 2f * WeaponPickMath.Threshold - 1e-4f, "suggested from 0.6 of a point; asked for from twice that");
        {
            var k = new Colony();
            k.C("ana", holds: "ar");
            k.W("ar", R("ranged", 5.8f), false, "ana");
            k.W("lmg", R("ranged", 6.0f));
            k.O("ana", "ar", 5.8f);
            k.O("ana", "lmg", 6.0f);
            C(k.Assign().Count == 0, "an assault rifle to an LMG (+0.2) isn't suggested");
            k.W("charge", R("ranged", 6.7f));
            k.O("ana", "charge", 6.7f);
            var s = k.Assign();
            C(s.Count == 1 && s[0].Kind == PickKind.Primary && s[0].Weapon == "charge" && Near(s[0].Gain, 0.9f) && s[0].Instead == "ar",
                "an assault rifle to a charge rifle (+0.9) is suggested, instead of what she holds");
            C(Near(s[0].Rating, 6.7f) && Near(s[0].Before, 5.8f), "the badges: its rating for her and what she holds");
        }

        // ---------- One weapon, two colonists ----------
        {
            var k = new Colony();
            k.C("ana", holds: "a");
            k.C("bo", holds: "b");
            k.W("a", R("ranged", 4f), false, "ana");
            k.W("b", R("ranged", 5f), false, "bo");
            k.W("x", R("ranged", 7f));
            k.O("ana", "a", 4f);
            k.O("bo", "b", 5f);
            k.O("ana", "x", 7f);
            k.O("bo", "x", 7.5f);
            var s = k.Assign();
            C(s.Count == 1 && s[0].Colonist == "ana" && s[0].Weapon == "x", "one weapon, two who'd gain: the one who gains most (3.0 against 2.5)");
        }

        // ---------- A weapon moved on so a second colonist can have one ----------
        {
            var k = new Colony();
            k.C("ana");
            k.C("bo");
            k.W("x", R("ranged", 8f));
            k.W("y", R("ranged", 8f));
            k.O("ana", "x", 7f);
            k.O("ana", "y", 7f, "coded to someone else");
            k.O("bo", "x", 9f);
            k.O("bo", "y", 8.5f);
            var s = k.Assign();
            C(s.Count == 2 && s.Any(x => x.Colonist == "ana" && x.Weapon == "x") && s.Any(x => x.Colonist == "bo" && x.Weapon == "y"),
                "the greedy gives Bo the best, then Ana takes it while Bo takes the other: both armed");
        }

        // ---------- Near the best total, never one weapon to two (random colonies) ----------
        {
            var rnd = new Random(1234);
            int optimal = 0, cases = 300, worse = 0;
            for (int t = 0; t < cases; t++)
            {
                var k = new Colony();
                int cs = rnd.Next(2, 5), ws = rnd.Next(2, 6);
                for (int i = 0; i < cs; i++)
                {
                    bool holds = rnd.NextDouble() < 0.6;
                    k.C("c" + i, holds: holds ? "h" + i : null);
                    if (holds)
                    {
                        k.W("h" + i, R("ranged", 3f + (float)rnd.NextDouble() * 3f), false, "c" + i);
                        k.O("c" + i, "h" + i, 3f + (float)rnd.NextDouble() * 3f);
                    }
                }
                for (int j = 0; j < ws; j++) k.W("s" + j, R("ranged", 4f + (float)rnd.NextDouble() * 4f));
                foreach (var c in k.I.Colonists)
                    foreach (var w in k.I.Weapons.Where(w => w.Stored))
                        k.O(c.Key, w.Key, 3f + (float)rnd.NextDouble() * 6f, rnd.NextDouble() < 0.15 ? "can't" : null);
                var s = k.Assign();
                var stored = s.Where(x => x.Kind != PickKind.Swap).Select(x => x.Weapon).ToList();
                if (stored.Distinct().Count() != stored.Count) worse += 1000;
                float total = s.Where(x => x.Kind == PickKind.Primary).Sum(x => x.Gain);
                float best = Best(k.I, 0, new HashSet<string>());
                if (total >= best - 0.05f) optimal++;
                else if (total < best * 0.75f) worse++;
                if (s.Any(x => x.Kind == PickKind.Primary && x.Gain < WeaponPickMath.Threshold - 1e-4f)) worse += 1000;
            }
            C(worse < 1000, "random colonies: never one weapon to two, never a gain under the threshold");
            C(optimal >= cases * 0.9, $"random colonies: the colony's best total in {optimal} of {cases}");
            C(worse == 0, $"random colonies: never under three quarters of the best total ({worse})");
        }

        // ---------- What stops a suggestion ----------
        {
            var k = new Colony();
            var ana = k.C("ana", holds: "pistol");
            k.W("pistol", R("ranged", 3f), false, "ana");
            k.W("lance", R("ranged", 8f));
            k.O("ana", "pistol", 3f);
            k.O("ana", "lance", 8f, "despised by her faith");
            C(k.Assign().Count == 0, "a weapon she mustn't carry (despised, coded to another, too heavy) is never suggested");
            ana.CanFight = false;
            k.O("ana", "lance", 8f);
            C(k.Assign().Count == 0, "nothing for someone incapable of violence");
        }
        {
            var k = new Colony();
            var bo = k.C("bo", holds: "rifle");
            bo.Brawler = true;
            k.W("rifle", R("ranged", 5f), false, "bo");
            k.W("gun", R("ranged", 9f));
            k.W("club", R("melee", 4f));
            k.O("bo", "rifle", 5f);
            k.O("bo", "gun", 9f);
            k.O("bo", "club", 4f);
            var s = k.Assign();
            C(!s.Any(x => x.Weapon == "gun"), "a brawler is never handed a gun");
            C(s.Count == 1 && s[0].Weapon == "club" && Near(s[0].Gain, 4f - WeaponPickMath.Unarmed), "a brawler with a gun in hand counts as empty-handed: a club is suggested");
        }
        {
            var k = new Colony();
            var ana = k.C("ana", holds: "persona");
            ana.Bonded = "persona";
            k.W("persona", R("melee", 6f), false, "ana");
            k.W("sword", R("melee", 7f));
            k.O("ana", "persona", 6f);
            k.O("ana", "sword", 7f);
            C(k.Assign().Count == 0, "her bonded persona weapon: a gain of 1.0 isn't enough to leave it unused");
            k.O("ana", "sword", 7.8f);
            ana.BondJealous = true;
            var s = k.Assign();
            C(s.Count == 1 && s[0].Costs.Any(r => r.Kind == PickWhy.Bonded) && s[0].Costs.Any(r => r.Kind == PickWhy.Jealous),
                "a gain of 1.8 is: what it costs is said (the bond unused, the jealous persona)");
        }
        {
            var k = new Colony();
            k.C("ana", holds: "rifle");
            k.W("rifle", R("ranged", 5f), false, "ana");
            k.W("sword", R("melee", 5.8f));
            k.O("ana", "rifle", 5f);
            k.O("ana", "sword", 5.8f);
            C(k.Assign().Count == 0, "another kind than she holds asks half a point more (0.8 isn't enough)");
            k.O("ana", "sword", 6.2f);
            C(k.Assign().Count == 1, "1.2 is");
            k.I.Rules.Sidearms = true;
            var s = k.Assign();
            C(s.All(x => x.Kind != PickKind.Primary), "with Simple Sidearms a weapon of the other kind isn't one for her hand");
        }

        // ---------- What they carry themselves ----------
        {
            var k = new Colony();
            var ana = k.C("ana", holds: "rifle");
            ana.Carries.Add("carbine");
            k.W("rifle", R("ranged", 5f), false, "ana");
            k.W("carbine", R("ranged", 6f), false).CarriedBy = "ana";
            k.O("ana", "rifle", 5f);
            k.O("ana", "carbine", 6f);
            var s = k.Assign();
            C(s.Count == 1 && s[0].Kind == PickKind.Primary && s[0].Weapon == "carbine", "a better weapon in her own inventory: take it in hand");
            k.I.Rules.Sidearms = true;
            C(k.Assign().Count == 0, "with Simple Sidearms it is her sidearm, which it switches to itself");
        }

        // ---------- Trades better for both ----------
        {
            var k = new Colony();
            k.C("ana", 14, 3, "sword");
            k.C("bo", 3, 14, "rifle");
            k.W("sword", R("melee", 5f), false, "ana");
            k.W("rifle", R("ranged", 5f), false, "bo");
            k.O("ana", "sword", 3f);
            k.O("ana", "rifle", 7f);
            k.O("bo", "rifle", 3f);
            k.O("bo", "sword", 7f);
            var s = k.Assign();
            C(s.Count == 2 && s.All(x => x.Kind == PickKind.Swap) && s.Any(x => x.Colonist == "ana" && x.Weapon == "rifle" && x.Other == "bo" && Near(x.OtherGain, 4f)),
                "a shooter with a sword and a fighter with a rifle trade, each 4 better");
            C(s.All(x => x.Reasons.Any(r => r.Kind == PickWhy.Swap)), "a trade's line names the other and their gain");
            k.I.Colonist("bo").Bonded = "rifle";
            C(k.Assign().Count == 0, "a bonded persona weapon is never traded");
        }

        // ---------- Simple Sidearms: a sidearm ----------
        {
            var k = new Colony();
            k.I.Rules.Sidearms = true;
            k.C("ana", holds: "rifle");
            k.W("rifle", R("ranged", 6f), false, "ana");
            k.W("knife", R("melee", 4.5f), mass: 0.5f);
            k.W("longsword", R("melee", 6f), mass: 3f);
            k.O("ana", "rifle", 6f);
            k.O("ana", "knife", 4.5f);
            k.O("ana", "longsword", 6f);
            var s = k.Assign();
            C(s.Count == 1 && s[0].Kind == PickKind.Sidearm && s[0].Weapon == "knife" && Near(s[0].Gain, 4.5f - WeaponPickMath.SidearmFloor),
                "a knife as a sidearm beside her rifle; a longsword is past Simple Sidearms' default weight");
            C(s[0].Reasons.First().Kind == PickWhy.Sidearm, "its line says it is carried beside her rifle");
            k.I.Colonist("ana").Carries.Add("dagger");
            k.W("dagger", R("melee", 4.3f), false, mass: 0.5f).CarriedBy = "ana";
            k.O("ana", "dagger", 4.3f);
            C(k.Assign().Count == 0, "not when she already carries one nearly as good");
        }
        {
            var k = new Colony();
            k.I.Rules.Sidearms = true;
            var bo = k.C("bo", holds: "sword");
            bo.Brawler = true;
            k.W("sword", R("melee", 6f), false, "bo");
            k.W("pistol", R("ranged", 6f), mass: 1.2f);
            k.O("bo", "sword", 6f);
            k.O("bo", "pistol", 6f);
            C(k.Assign().Count == 0, "never a gun as a brawler's sidearm");
        }

        // ---------- Dual Wield: the off hand ----------
        {
            var k = new Colony();
            k.I.Rules.DualWield = true;
            k.C("ana", holds: "knife");
            k.W("knife", R("melee", 4f), false, "ana", 0.5f);
            var off = k.W("knife2", R("melee", 4f), mass: 0.5f);
            off.CanOffHand = true;
            k.O("ana", "knife", 4f);
            k.O("ana", "knife2", 4f, pair: 5.2f);
            var s = k.Assign();
            C(s.Count == 1 && s[0].Kind == PickKind.OffHand && Near(s[0].Gain, 1.2f) && Near(s[0].Rating, 5.2f), "a second knife in the off hand where both together add 1.2");
            off.TwoHanded = true;
            C(k.Assign().Count == 0, "never a two-handed weapon in the off hand");
        }

        // ---------- The colony's fights ----------
        {
            int day = 60000, now = 200 * day;
            C(WeaponPickMath.Recency(now, now - 10 * day) == 1f && WeaponPickMath.Recency(now, now - 50 * day) == 0.5f && WeaponPickMath.Recency(now, now - 100 * day) == 0.25f
              && WeaponPickMath.Recency(now, now - 150 * day) == 0f, "raids weigh in full for a month, then less, then not at all");
            var mechs = Enumerable.Range(0, 3).Select(i => new RaidSeen { Tick = now - i * day, Count = 6, Mechs = 6 }).ToList();
            var sit = WeaponPickMath.ColonySituations(mechs, now, false);
            C(Near(sit["mechanoids"], 0.5f) && !sit.ContainsKey("crowds"), "three mechanoid raids: mechanoids weigh 0.5");
            var tribe = new List<RaidSeen> { new RaidSeen { Tick = now, Count = 14, Armoured = 7, Tribal = true }, new RaidSeen { Tick = now, Count = 5, Siege = true } };
            sit = WeaponPickMath.ColonySituations(tribe, now, true);
            C(Near(sit["crowds"], 0.15f) && Near(sit["heavily armoured"], 0.1f) && Near(sit["siege or long duel"], 0.125f) && Near(sit["holding a choke"], 0.15f),
                "a crowd, half of them armoured, and a siege; a gate held");
            var letters = new List<RaidSeen> { new RaidSeen { Tick = now, FromLetter = true, Mechs = 1 }, new RaidSeen { Tick = now, FromLetter = true, ArmouredShare = 0.4f } };
            sit = WeaponPickMath.ColonySituations(letters, now, false);
            C(Near(sit["mechanoids"], 0.25f) && Near(sit["heavily armoured"], 0.08f), "raids read from the game's letters, by their people");
            C(WeaponPickMath.ColonySituations(new List<RaidSeen>(), now, false).Count == 0, "no raids, no situations");

            var k = new Colony();
            k.C("ana", holds: "rifle");
            k.W("rifle", R("ranged", 6f), false, "ana");
            var emp = R("ranged", 6.2f, 1.2f, ("mechanoids", 9f));
            emp.Specials.Add(new WeaponSpecial { Key = "emp", Label = "EMP: stuns mechanoids (5 s), breaks shield belts", Counted = true });
            k.W("emp", emp);
            k.O("ana", "rifle", 6f);
            k.O("ana", "emp", 6.2f);
            C(k.Assign().Count == 0, "no mechanoids met: an EMP launcher a fifth of a point better isn't suggested");
            k.I.Colony = WeaponPickMath.ColonySituations(mechs, now, false);
            var s = k.Assign();
            C(s.Count == 1 && Near(s[0].Gain, 0.2f + 0.5f * (9f - 6.2f)), "mechanoids met: it is, by half its edge against them");
            C(s[0].Reasons.First().Kind == PickWhy.MechsEmp && WeaponPickMath.AskTag(s[0]) == "mechanoids", "its line: its EMP stops the mechanoids you keep meeting");
            var mix = WeaponPickMath.Mix(new PickColonist { Hunter = true, Worker = "doctor" }, k.I.Weapon("emp"),
                new Dictionary<string, float> { ["mechanoids"] = 0.5f, ["crowds"] = 0.3f, ["heavily armoured"] = 0.4f });
            C(mix.Values.Sum() <= WeaponPickMath.MixMax + 1e-4f && mix["hunting"] > 0f && mix["sidearm"] > 0f, "the mix: the colony's fights, a hunter's hunts and a doctor's light weapon, 0.6 at most");
        }

        // ---------- A hunter, passions, faith, skill ----------
        {
            var k = new Colony();
            var ana = k.C("ana", 15, 4, "rifle");
            k.W("rifle", R("ranged", 5f, 1.2f), false, "ana");
            k.W("sword", R("melee", 7f));
            k.W("sniper", R("ranged", 6f, 1.7f));
            k.O("ana", "rifle", 5f);
            k.O("ana", "sword", 4.5f);
            k.O("ana", "sniper", 6f);
            float plain = WeaponPickMath.Score(k.I, ana, k.I.Weapon("sword")).Value;
            ana.Hunter = true;
            C(Near(WeaponPickMath.Score(k.I, ana, k.I.Weapon("sword")).Value, plain - WeaponPickMath.HunterLoss), "a hunter loses 0.8 on a weapon they can't hunt with");
            ana.ShootingPassion = 2;
            C(Near(WeaponPickMath.Score(k.I, ana, k.I.Weapon("sniper")).Value - WeaponPickMath.Score(k.I, ana, k.I.Weapon("rifle")).Value, 1f), "a burning passion lifts both her guns alike");
            ana.Hunter = false;
            var s = k.Assign();
            var sn = s.FirstOrDefault(x => x.Weapon == "sniper");
            C(sn != null && sn.Reasons.First().Kind == PickWhy.SkillHigh && sn.Reasons.First().Level == 15, "her Shooting 15 makes the most of a sniper rifle that rewards it");
            k.O("ana", "sniper", 6f, noble: true);
            s = k.Assign();
            C(s.First(x => x.Weapon == "sniper").Reasons.Any(r => r.Kind == PickWhy.Noble) && Near(s.First(x => x.Weapon == "sniper").Gain, 1.4f),
                "a weapon her faith holds noble: 0.4 more, and it is said");
            var steps = new List<(string, float)> { ("Shooting 15", 0.6f), ("careful shooter", 0.4f), ("body and gear", 0f) };
            var why = WeaponPickMath.Reasons(k.I, s.First(x => x.Weapon == "sniper"), steps);
            var shown = why.Where(r => !r.Cost).ToList();
            C(shown.Count == 2 && shown[0].Kind == PickWhy.SkillHigh && shown[1].Kind == PickWhy.Noble,
                "the reasons: two at most, the strongest first (her skill over her faith over her careful shooting)");
            steps = new List<(string, float)> { ("Shooting 15", 0.6f), ("careful shooter", 0.9f), ("body and gear", 0f) };
            why = WeaponPickMath.Reasons(k.I, s.First(x => x.Weapon == "sniper"), steps);
            C(why.Any(r => r.Kind == PickWhy.Trait && r.Text == "careful shooter" && Near(r.Value, 0.9f)), "a trait that adds much to it is said, by name and how much");
        }

        // ---------- Who should carry this ----------
        {
            var k = new Colony();
            k.C("ana", holds: "a");
            k.C("bo", holds: "b");
            k.C("cy").CanFight = false;
            k.W("a", R("melee", 4f), false, "ana");
            k.W("b", R("melee", 6f), false, "bo");
            var x = k.W("x", R("melee", 7f));
            k.O("ana", "a", 4f);
            k.O("bo", "b", 6f);
            k.O("ana", "x", 6.5f);
            k.O("bo", "x", 7.5f);
            k.O("cy", "x", 7f);
            var who = WeaponPickMath.WhoShouldCarry(k.I, x);
            C(who.Count == 2 && who[0].c.Key == "ana" && Near(who[0].gain, 2.5f) && who[1].c.Key == "bo", "who should carry it: the biggest gain first, never someone who can't fight");
        }

        // ---------- Words ----------
        C(WeaponPickMath.GainText(1.44f) == "+1.4" && WeaponPickMath.Short("EMP: stuns mechanoids (5 s)") == "EMP" && WeaponPickMath.Short("grants shock lance (3 charges)") == "grants shock lance",
            "a gain and an effect's short words");
        foreach (var (why, tag) in new[] { (PickWhy.ArmourPierce, "armoured"), (PickWhy.SkillLow, "skill"), (PickWhy.Hunting, "hunting"), (PickWhy.Worker, "worker"), (PickWhy.Better, "better") })
        {
            var s = new PickSuggestion { Kind = PickKind.Primary, Why = { new PickReason { Kind = why } } };
            C(WeaponPickMath.AskTag(s) == tag, $"{why}: said as [{tag}]");
        }

        // ---------- When a colonist asks ----------
        {
            int day = WeaponAskMath.DayTicks, now = 100 * day;
            C(WeaponAskMath.MayAsk(now, -1, -1, -1, 7, 0), "never asked: may ask");
            C(!WeaponAskMath.MayAsk(now, now - 4 * day, -1, 3, 7, 0) && WeaponAskMath.MayAsk(now, now - 6 * day, -1, 3, 7, 0), "once in five days each");
            C(!WeaponAskMath.MayAsk(now, -1, now - 2500 * 20, -1, 7, 0) && WeaponAskMath.MayAsk(now, -1, now - 2500 * 25, -1, 7, 0), "once a day in the colony");
            C(!WeaponAskMath.MayAsk(now, now - 10 * day, -1, 7, 7, 0) && WeaponAskMath.MayAsk(now, now - 16 * day, -1, 7, 7, 0), "the same weapon not again for fifteen days");
            C(!WeaponAskMath.MayAsk(now, -1, -1, -1, 7, now + 1), "never while set aside");
            C(WeaponAskMath.Askable(new PickSuggestion { Kind = PickKind.Primary, Gain = 1.2f }) && !WeaponAskMath.Askable(new PickSuggestion { Kind = PickKind.Primary, Gain = 1.1f })
              && !WeaponAskMath.Askable(new PickSuggestion { Kind = PickKind.Swap, Gain = 3f }), "only a clearly better weapon lying in storage, never a trade");
            C(WeaponAskMath.Shows(now, now - day, 0) && !WeaponAskMath.Shows(now, now - 4 * day, 0) && !WeaponAskMath.Shows(now, now - day, now + 5), "the inbox keeps it three days, not while set aside");
            C(WeaponAskMath.Chance > 0f && WeaponAskMath.Chance <= 0.2f, "and only now and then");
        }

        // ---------- The pool: a line for every reason, in every age ----------
        {
            var pools = new Dictionary<string, List<string>>();
            foreach (var file in Directory.GetFiles(Path.Combine(root, "Defs", "FlavorPoolDefs"), "*.xml"))
                foreach (var el in XDocument.Load(file).Root!.Elements("ColonyCouncil.FlavorPoolDef"))
                    pools[(string)el.Element("defName")] = el.Element("lines")?.Descendants("li").Select(li => li.Value).ToList() ?? new List<string>();
            C(pools.TryGetValue("Rimfolk_Pool_WeaponAsk", out var lines) && lines.Count >= 40, "the colonists' asks have a pool of their own");
            lines ??= new List<string>();
            string[] reasons = { "mechanoids", "armoured", "crowds", "siege", "choke", "skill", "trait", "hunting", "passion", "noble", "worker", "special", "sidearm", "offhand", "better" };
            foreach (var era in new[] { "tribal", "medieval", "industrial", "spacer" })
            {
                var eras = new HashSet<string> { era };
                if (era == "tribal" || era == "medieval") eras.Add("lowtech");
                foreach (var why in reasons)
                    foreach (var kind in new[] { "ranged", "melee" })
                    {
                        var ctx = new HashSet<string>(eras) { why, kind };
                        bool fits = lines.Any(l => LineTags.Of(l) is var p && p != null && LineTags.Match(p, ctx) > 0 && LineTags.EraFits(p, eras) && p.tags.Contains(why));
                        C(fits, $"[{why}] has a line for a {kind} weapon in the {era} age");
                    }
            }
            C(lines.All(l => l.Contains("{WEAPON}") && l.Contains("{PLACE}")), "every line names the weapon and where it lies");
            C(lines.All(l => reasons.Any(r => LineTags.Of(l)?.tags?.Contains(r) == true)), "every line has the reason it is said for, so its words are true");
            C(Read("Tools/Tests/ImmersionTests.cs").Contains("\"Rimfolk_Pool_WeaponAsk\""), "its lines are held to one sentence of 90 characters (ImmersionTests)");
        }

        // ---------- The seams ----------
        string pick = Read("Source/ColonyCouncil/Weapons/WeaponPick.cs");
        string ui = Read("Source/ColonyCouncil/Weapons/WeaponPickUi.cs");
        string book = Read("Source/ColonyCouncil/Weapons/WeaponPickBook.cs");
        string first = Read("Source/ColonyCouncil/Weapons/WeaponCardFirst.cs");
        string asks = Read("Source/ColonyCouncil/Weapons/WeaponAsks.cs");
        string swap = Read("Source/ColonyCouncil/Weapons/WeaponSwapJob.cs");
        string card = Read("Source/ColonyCouncil/Weapons/WeaponCardWindow.cs");
        string settings = Read("Source/ColonyCouncil/CouncilSettings.cs");
        string catalog = Read("Source/ColonyCouncil/UI/SettingsCatalog.cs");
        string keys = Read("Languages/English/Keyed/Rimfolk_Weapons.xml");
        foreach (var (field, label, needs) in new[] { ("weaponCardFirst", "WeaponCardFirst", "weaponRatings,weaponCard,deepHooks"), ("weaponSuggestions", "WeaponSuggestions", "weaponRatings,deepHooks"),
                     ("weaponAsks", "WeaponAsks", "weaponRatings,deepHooks") })
        {
            C(settings.Contains("public bool " + field + " = true;") && settings.Contains("Scribe_Values.Look(ref " + field + ", \"" + field + "\", true);"), $"{field}: a setting, on at first, saved");
            C(catalog.Contains("Toggle(\"" + field + "\", \"CC_Settings_" + label + "\", \"CC_SetNote_" + label + "\", \"" + needs + "\");"), $"{field}: on the settings page, under Weapons ({needs})");
            C(keys.Contains("<CC_Settings_" + label + ">") && keys.Contains("<CC_Settings_" + label + "_Tip>") && keys.Contains("<CC_SetNote_" + label + ">"), $"{field}: its label, tip and note");
        }
        // Rimfolk's card first.
        C(first.Contains("[HarmonyPatch(typeof(WindowStack), nameof(WindowStack.Add))]") && first.Contains("if (broken || !(window is Dialog_InfoCard card)) return true;"),
            "Rimfolk's card first: one hook on the window stack, every window but an info card passes untouched");
        C(first.Contains("[HarmonyPatch(typeof(Dialog_InfoCard), nameof(Dialog_InfoCard.DoWindowContents))]") && first.Contains("insideGameCard"),
            "a card opened from inside the game's card (its links, its back button) stays the game's");
        C(first.Contains("letThrough") && card.Contains("WeaponCardFirst.OpenGameCard(thing, def, stuff)") && keys.Contains("<CC_Weapon_GameCard>All the game's stats</CC_Weapon_GameCard>"),
            "the weapon card's button to all the game's stats, let through");
        C(first.Contains("WeaponCardWindow.On && (ColonyCouncilMod.Settings?.weaponCardFirst ?? true)"), "only while the weapon card is on");
        C(card.Contains("CC_Weapon_Card_RatedForKind") && keys.Contains("<CC_Weapon_Card_Made>{QUALITY} quality, {STUFF}</CC_Weapon_Card_Made>"), "a kind's card says it is rated at normal quality in its stuff");
        C(Read("Source/ColonyCouncil/Weapons/WeaponInfoCard.cs").Contains("if (WeaponCardFirst.On) card.Close(false);"), "the game's card's button back closes it when the weapon card opens first");
        // The gear tab, Nice Inventory Tab, the Defenses page, the card, the inbox.
        C(ui.Contains("[HarmonyPatch(typeof(ITab_Pawn_Gear), \"TryDrawComfyTemperatureRange\")]") && ui.Contains("ref float curY"), "the game's gear tab: the lines under the temperatures, the tab's height following");
        int at = ui.IndexOf("class Patch_WeaponPickNiceBox", StringComparison.Ordinal);
        string nice = at >= 0 ? ui.Substring(at, Math.Min(1200, ui.Length - at)) : "";
        C(nice.Contains("Prepare()") && nice.Contains("TargetMethod()") && nice.Contains("StandsDownWhy()") && ui.Contains("NiceInventoryTab.GroupBox"), "Nice Inventory Tab's mark: by name, skipped cleanly without the mod");
        C(Read("Source/ColonyCouncil/UI/DefensePanel.cs").Contains("WeaponPickPanel.Draw(s.map, x, y, w, folds)"), "the colony's view on the Defenses page");
        C(card.Contains("WhoCarries(w, y, r)") && keys.Contains("<CC_WeaponPick_Card_Who>Who should carry this</CC_WeaponPick_Card_Who>"), "who should carry a weapon, on its card");
        C(Read("Source/ColonyCouncil/UI/InboxProviders.cs").Contains("Inbox.Register(\"weapon-asks\", InboxSection.People, 40f, WeaponAsks.InboxRows);") && asks.Contains("waits = false"),
            "a quiet entry in the inbox, counted nowhere");
        C(asks.Contains("Speech.Say(p, line)") && asks.Contains("WeaponPick.PlaceOf(t)") && asks.Contains("FreeAndSafe(p)"), "the ask is said through Speech, where the weapon lies now, in free time and safe");
        // The orders are real jobs, the mods' own by name.
        C(pick.Contains("JobDefOf.Equip") && pick.Contains("\"EquipSecondary\"") && pick.Contains("\"DW_EquipOffhand\"") && pick.Contains("\"Rimfolk_SwapWeapons\""),
            "the orders: the game's Equip, Simple Sidearms' and Dual Wield's own jobs, a trade");
        C(pick.Contains("TryTakeOrderedJob(job, JobTag.Misc, queue)") && pick.Contains("GetPersonaWeaponConfirmationText"), "equip all only queues the orders; a persona weapon asks first");
        C(!pick.Contains("using PeteTimesSix") && !pick.Contains("using DualWield") && !swap.Contains("using SimpleSidearms"), "no hard reference to the weapon mods");
        var jobs = Read("Defs/Misc/Rimfolk_WeaponPicks.xml");
        C(jobs.Contains("<defName>Rimfolk_SwapWeapons</defName>") && jobs.Contains("<driverClass>ColonyCouncil.JobDriver_SwapWeapons</driverClass>") && swap.Contains("class JobDriver_SwapWeapons : JobDriver"),
            "the trade is a job walked out on the map");
        C(pick.Contains("EquipmentUtility.CanEquip(t, p, out var why, true)") && pick.Contains("IdeoWeaponDisposition.Despised") && pick.Contains("IdeoWeaponDisposition.Noble"),
            "the game's own refusals, and the faith's despised and noble weapons");
        // The clock and the book.
        C(Sched.RareOffsets.Count(o => o == Sched.RareWeaponPicks) == 1 && Sched.HourlyOffsets.All(h => h % Sched.RareTicks != Sched.RareWeaponPicks), "a 250-tick slot of its own, off every hourly pass");
        C(PassTable.All.Any(p => p.pass == Pass.RareWeaponPicks && p.offset == Sched.RareWeaponPicks && p.period == Sched.RareTicks), "timed by Rimfolk's clock");
        C(book.Contains("Sched.SliceTurn(now, Sched.HourTicks, Sched.Slices, Sched.RareWeaponPicks)") && book.Contains("PassClock.Time(Pass.RareWeaponPicks)"), "a tenth of the colony each turn, each colonist once an hour");
        C(book.Contains("[HarmonyPatch(typeof(IncidentWorker_Raid), \"PostProcessSpawnedPawns\")]") && book.Contains("ArchivablesListForReading"),
            "the raids: Rimfolk's own record, and the game's letters for the rest");
        C(Regex.IsMatch(book, @"if \(raids\.Count > MaxRaids\)") && book.Contains("Scribe_Collections.Look(ref notNow") && Read("Tools/save_estimate.py").Contains("Weapons/WeaponPickBook.cs:raids"),
            "the saved book is bounded and modelled in the save estimate");
        // The keys every reason uses.
        foreach (var key in new[] { "SkillHigh_Ranged", "SkillHigh_Melee", "SkillLow_Ranged", "SkillLow_Melee", "Trait", "Mechs", "MechsEmp", "Armour", "ArmourPierce", "Crowds", "Siege", "Choke",
                     "Hunting", "Passion", "Noble", "Worker_Doctor", "Worker_Crafter", "Special", "Better", "Unarmed", "Sidearm", "OffHand", "Swap" })
            C(keys.Contains("<CC_WeaponPick_Why_" + key + ">"), $"the words for {key}");
        foreach (var key in new[] { "Bonded", "Jealous", "NoHunting" }) C(keys.Contains("<CC_WeaponPick_Cost_" + key + ">"), $"the words for what {key} costs");
        foreach (var kind in Enum.GetNames(typeof(PickKind)))
            C(keys.Contains("<CC_WeaponPick_Act_" + kind + ">") && keys.Contains("<CC_WeaponPick_Act_" + kind + "_Tip>") && keys.Contains("<CC_WeaponPick_Head_" + kind + ">"), $"{kind}: its head and its button");
        C(!keys.Contains("—"), "the keys: no em dashes");
        var almanac = Read("Defs/AlmanacArticleDefs/Almanac_Weapons.xml");
        C(almanac.Contains("<heading>The best weapon for each colonist</heading>") && almanac.Contains("<heading>Colonists who ask</heading>") && almanac.Contains("All the game's stats"),
            "the Almanac's page tells it");
        return n;
    }

    /// <summary>The best total of gains by brute force: each colonist one stored weapon worth it, or none.</summary>
    static float Best(PickInput input, int i, HashSet<string> used)
    {
        if (i >= input.Colonists.Count) return 0f;
        var c = input.Colonists[i];
        float best = Best(input, i + 1, used);
        foreach (var w in input.Weapons.Where(w => w.Stored && !used.Contains(w.Key)))
        {
            var g = WeaponPickMath.PrimaryGain(input, c, w);
            if (!g.HasValue || g.Value.gain < g.Value.need - 1e-4f) continue;
            used.Add(w.Key);
            best = Math.Max(best, g.Value.gain + Best(input, i + 1, used));
            used.Remove(w.Key);
        }
        return best;
    }
}
