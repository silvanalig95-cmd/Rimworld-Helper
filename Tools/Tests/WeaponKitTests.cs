using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// A full kit for every colonist (the player's words, 9 November 2026: "the pawns should always have a ranged and melee
/// weapon, assuming they have no restrictions ... also offhand if they have capacity &amp; compatibility";
/// Weapons/WeaponKitMath.cs): the kit as it stands for each colonist (filled, missing, not for them and why); the
/// restrictions that decide it (incapable of violence or of shooting, a brawler, a role's rule, Simple Sidearms' weights
/// and refusal, Dual Wield's hands and weapons); an empty slot filled even under the usual threshold but never with
/// something nearly useless, by the weapon's part in the kit (a shooter's light, quick blade better than their gun's butt; a
/// melee fighter's light gun); where it comes from (storage, their own pack, another's spare); the kit before anyone's
/// upgrade, the front line first when weapons are short; forbidden weapons only asked for; what was looked at and why each
/// was left out; who lacks a weapon's kind; the asks' "kit"; random colonies; and the seams (the setting, the jobs, the
/// colonists who fill a gap themselves and their lines in every age, the Defenses page, the card).
/// </summary>
public static class WeaponKitTests
{
    static WeaponRating R(string kind, float overall, float sidearm = -1f, float kiting = -1f, float inMelee = -1f, bool quick = false)
    {
        var r = new WeaponRating { Kind = kind, Role = kind == "ranged" ? "all-round" : "melee", Overall = overall, SkillGain = 1.2f };
        foreach (var k in new[] { "mechanoids", "heavily armoured", "crowds", "hunting", "indoors", "holding a choke", "capture alive" }) r.Situations[k] = overall;
        r.Situations["sidearm"] = sidearm >= 0f ? sidearm : overall;
        if (kind == "ranged")
        {
            r.Situations["kiting"] = kiting >= 0f ? kiting : overall;
            r.Situations["in melee"] = inMelee >= 0f ? inMelee : 3f;
        }
        if (quick) r.Tags.Add("quick");
        return r;
    }

    sealed class Colony
    {
        public readonly PickInput I = new PickInput();

        public Colony(bool sidearms = true, bool dual = false)
        {
            I.Rules.Sidearms = sidearms;
            I.Rules.DualWield = dual;
        }

        public PickColonist C(string key, int shooting = 8, int melee = 8, string holds = null, float fights = 1f)
        {
            var c = new PickColonist { Key = key, Name = key, Shooting = shooting, Melee = melee, Holds = holds, Fights = fights };
            I.Colonists.Add(c);
            return c;
        }

        /// <summary>A weapon: in storage (the default), held by someone, or carried in someone's pack (remembered unless said).</summary>
        public PickWeapon W(string key, WeaponRating r, float mass = 1f, string holder = null, string carrier = null, bool remembered = true)
        {
            var w = new PickWeapon { Key = key, Label = key, Rating = r, Mass = mass, Stored = holder == null && carrier == null, Holder = holder, CarriedBy = carrier, HuntingOk = r.Kind == "ranged" };
            I.Weapons.Add(w);
            if (holder != null) I.Colonist(holder).Holds = key;
            if (carrier != null)
            {
                I.Colonist(carrier).Carries.Add(key);
                if (!remembered) I.Colonist(carrier).Unremembered.Add(key);
            }
            return w;
        }

        public PickOption O(string c, string w, float rating, string cant = null, float? pair = null, string sidearmCant = null)
        {
            var o = new PickOption { Rating = rating, Cant = cant, Pair = pair, SidearmCant = sidearmCant };
            I.Options[(c, w)] = o;
            return o;
        }

        public List<PickSuggestion> Assign() => WeaponPickMath.Assign(I);

        public KitView Slot(string c, KitSlot slot) => WeaponKitMath.Kit(I, I.Colonist(c)).First(v => v.Slot == slot);
    }

    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "a full kit: " + what);
        }
        bool Near(float a, float b, float eps = 0.01f) => Math.Abs(a - b) <= eps;
        string root = Directory.GetCurrentDirectory();
        while (root != null && !File.Exists(Path.Combine(root, "CLAUDE.md"))) root = Path.GetDirectoryName(root);
        string Read(string rel) => root != null && File.Exists(Path.Combine(root, rel)) ? File.ReadAllText(Path.Combine(root, rel)) : "";

        // ---------- The minimums ----------
        C(Near(WeaponKitMath.Floor, 2f * WeaponPickMath.Unarmed), "the least worth carrying in an empty slot: twice bare hands (2.0)");
        C(WeaponKitMath.OffHandAdds > 0f && WeaponKitMath.OffHandAdds < WeaponPickMath.Threshold, "an off-hand weapon must add something, less than an upgrade asks");
        C(WeaponKitMath.OverBash > 0f, "a shooter's blade must beat clubbing with the gun");

        // ---------- Avila: an autopistol, no melee weapon ----------
        {
            var k = new Colony();
            k.C("avila", 9, 5);
            k.W("auto", R("ranged", 4.7f, inMelee: 3f), 1.2f, holder: "avila");
            k.O("avila", "auto", 4.7f);
            C(k.Slot("avila", KitSlot.Ranged).State == KitState.Filled && k.Slot("avila", KitSlot.Ranged).Weapon == "auto", "her ranged slot: the autopistol");
            C(k.Slot("avila", KitSlot.Melee).State == KitState.Missing, "her melee slot: missing");
            C(k.Slot("avila", KitSlot.OffHand).State == KitState.NotForThem && k.Slot("avila", KitSlot.OffHand).Bar == KitBar.NoDualWield, "no off hand without Dual Wield");
            C(k.Assign().Count == 0, "nothing in storage: nothing suggested");
            k.W("hatchet", R("melee", 3.5f, sidearm: 3.5f), 1f);
            k.O("avila", "hatchet", 3.5f);
            var s = k.Assign();
            C(s.Count == 1 && s[0].Kind == PickKind.Sidearm && s[0].Weapon == "hatchet" && s[0].Gap == KitSlot.Melee && Near(s[0].Gain, 0.5f),
                "a hatchet (3.5) fills it as a sidearm, though it adds only 0.5 over her pistol's butt, under the usual 0.6");
            C(s[0].Reasons.First().Kind == PickWhy.Kit && s[0].Reasons.First().Text == "melee", "its line first says she has nothing for close work");
            k.O("avila", "hatchet", 3.2f);
            C(k.Assign().Count == 0, "one no better than clubbing with her pistol (3.2 against 3.0 and 0.3) isn't pushed on her");
            k.W("knife", R("melee", 4f, sidearm: 4f, quick: true), 0.5f);
            k.W("club", R("melee", 4.2f, sidearm: 2.2f), 1.8f);
            k.O("avila", "knife", 4f);
            k.O("avila", "club", 4.2f);
            s = k.Assign();
            C(s.Count == 1 && s[0].Weapon == "knife", "the light, quick knife over a heavier club that rates a little higher: the part it plays in her kit");
        }

        // ---------- A melee fighter's gun ----------
        {
            var k = new Colony();
            k.C("bo", 6, 14);
            k.W("sword", R("melee", 6f), 2f, holder: "bo");
            k.W("ar", R("ranged", 5.8f, sidearm: 2.8f, kiting: 6f), 3.5f);
            k.W("auto", R("ranged", 4.7f, sidearm: 4.7f, kiting: 2.2f), 1.2f);
            k.W("revolver", R("ranged", 4.5f, sidearm: 4.5f, kiting: 2.5f), 1.4f);
            k.W("doomsday", R("ranged", 7f), 2f).OneUse = true;
            foreach (var (w, r) in new[] { ("sword", 6f), ("ar", 5f), ("auto", 4f), ("revolver", 3.95f), ("doomsday", 6f) }) k.O("bo", w, r);
            var s = k.Assign();
            C(s.Count == 1 && s[0].Kind == PickKind.Sidearm && s[0].Gap == KitSlot.Ranged && s[0].Weapon == "auto",
                "Bo's ranged slot: a light pistol as a sidearm; never the assault rifle (past Simple Sidearms' weight) or a one-use launcher");
            k.I.Options[("bo", "auto")].SidearmCant = "No room";
            s = k.Assign();
            C(s.Count == 1 && s[0].Weapon == "revolver", "Simple Sidearms refuses the pistol: the revolver instead");
        }

        // ---------- Restrictions decide the kit ----------
        {
            var k = new Colony(true, true);
            var bo = k.C("bo", 3, 12);
            bo.Brawler = true;
            k.W("sword", R("melee", 6f), 2f, holder: "bo");
            k.W("pistol", R("ranged", 5f), 1f);
            k.O("bo", "sword", 6f);
            k.O("bo", "pistol", 5f);
            C(k.Slot("bo", KitSlot.Ranged).State == KitState.NotForThem && k.Slot("bo", KitSlot.Ranged).Bar == KitBar.Brawler, "a brawler: no ranged slot");
            C(k.Assign().Count == 0, "and no gun suggested");
            bo.Brawler = false;
            bo.CanShoot = false;
            C(k.Slot("bo", KitSlot.Ranged).Bar == KitBar.CantShoot && k.Assign().Count == 0, "incapable of shooting: melee only");
            bo.CanShoot = true;
            bo.RoleNoRanged = true;
            C(k.Slot("bo", KitSlot.Ranged).Bar == KitBar.Role && k.Assign().Count == 0 && WeaponPickMath.Score(k.I, bo, k.I.Weapon("pistol")) == null, "a role that bars guns: none for him");
            bo.RoleNoRanged = false;
            bo.CanFight = false;
            C(WeaponKitMath.Kit(k.I, bo).All(v => v.State == KitState.NotForThem && v.Bar == KitBar.CantFight) && k.Assign().Count == 0, "incapable of violence: no kit at all");
            bo.CanFight = true;
            k.O("bo", "pistol", 5f, "Coded to Ana");
            C(k.Slot("bo", KitSlot.Ranged).State == KitState.Missing && k.Assign().Count == 0, "a gun the game refuses him: his slot stays empty");
        }

        // ---------- Without Simple Sidearms: one weapon in hand ----------
        {
            var k = new Colony(false);
            k.C("cy");
            k.W("rifle", R("ranged", 5f), 3.5f, holder: "cy");
            k.W("knife", R("melee", 4f), 0.5f);
            k.O("cy", "rifle", 5f);
            k.O("cy", "knife", 4f);
            C(k.Slot("cy", KitSlot.Melee).State == KitState.NotForThem && k.Slot("cy", KitSlot.Melee).Bar == KitBar.OneWeapon, "holding a rifle, no melee slot without Simple Sidearms");
            C(k.Assign().Count == 0, "so no knife beside it");
            k.I.Colonist("cy").Holds = null;
            k.I.Weapons.RemoveAll(w => w.Key == "rifle");
            C(k.Slot("cy", KitSlot.Ranged).State == KitState.Missing && k.Slot("cy", KitSlot.Melee).State == KitState.Missing, "nothing in hand: both missing, either fills it");
            var s = k.Assign();
            C(s.Count == 1 && s[0].Kind == PickKind.Primary && s[0].Gap == KitSlot.Hand && s[0].Weapon == "knife", "the knife in hand, filling the empty hand");
            k.O("cy", "knife", 1.8f);
            C(k.Assign().Count == 0, "one nearly useless in her hands (1.8, under 2.0) isn't pushed on her");
        }

        // ---------- Nothing at all, with Simple Sidearms ----------
        {
            var k = new Colony();
            k.C("dee", 12, 5);
            k.W("rifle", R("ranged", 5f), 3.5f);
            k.W("knife", R("melee", 4f), 0.5f);
            k.O("dee", "rifle", 5.5f);
            k.O("dee", "knife", 3.6f);
            var s = k.Assign();
            C(s.Count == 2 && s.Any(x => x.Kind == PickKind.Primary && x.Weapon == "rifle" && x.Gap == KitSlot.Ranged) && s.Any(x => x.Kind == PickKind.Sidearm && x.Weapon == "knife" && x.Gap == KitSlot.Melee),
                "a good shot with nothing: the rifle in hand, the knife as a sidearm");
            var dee = k.I.Colonist("dee");
            dee.Shooting = 4;
            dee.Melee = 13;
            k.O("dee", "rifle", 3.2f);
            k.O("dee", "knife", 5f);
            s = k.Assign();
            C(s.Any(x => x.Kind == PickKind.Primary && x.Weapon == "knife") && !s.Any(x => x.Weapon == "rifle"),
                "a melee fighter with nothing: the knife in hand, and the rifle (past the sidearm's weight) not as a sidearm");
        }

        // ---------- Their own pack ----------
        {
            var k = new Colony();
            k.C("eli", holds: null);
            k.W("rifle", R("ranged", 5f), 3.5f, holder: "eli");
            k.W("ownknife", R("melee", 4f), 0.5f, carrier: "eli", remembered: false);
            k.W("knife", R("melee", 4.1f), 0.5f);
            k.O("eli", "rifle", 5f);
            k.O("eli", "ownknife", 4f);
            k.O("eli", "knife", 4.1f);
            var eli = k.I.Colonist("eli");
            C(k.Slot("eli", KitSlot.Melee).State == KitState.Missing, "a knife in her pack Simple Sidearms doesn't remember is not in her kit (it would be unloaded)");
            var s = k.Assign();
            C(s.Count == 1 && s[0].Kind == PickKind.Keep && s[0].Weapon == "ownknife" && s[0].Gap == KitSlot.Melee && s[0].Reasons.Any(r => r.Kind == PickWhy.Pack),
                "it is made her sidearm, no walk, rather than a stored one barely better");
            eli.Unremembered.Clear();
            C(k.Slot("eli", KitSlot.Melee).State == KitState.Filled && k.Assign().Count == 0, "once remembered, her kit is full");
        }

        // ---------- Another colonist's spare ----------
        {
            var k = new Colony();
            k.C("ana");
            k.C("bo");
            k.W("rifle", R("ranged", 5f), 3.5f, holder: "ana");
            k.W("sword", R("melee", 6f), 2f, holder: "bo");
            k.W("pistol", R("ranged", 4.5f), 1.2f, carrier: "bo");
            k.W("knife", R("melee", 4f), 0.5f, carrier: "bo");
            k.W("lucky", R("melee", 4.5f), 0.5f, carrier: "bo");
            k.I.Colonist("bo").Keepsakes.Add(new PickKeepsake { Kind = KeepsakeKind.Lucky, Weapon = "lucky", Def = "lucky", Label = "lucky dagger", MoodWith = 3f, MoodWithout = -4f });
            foreach (var w in new[] { "rifle", "sword", "pistol", "knife", "lucky" })
            {
                k.O("ana", w, 4f);
                k.O("bo", w, 4f);
            }
            var spares = WeaponKitMath.Spares(k.I, k.I.Colonist("bo")).Select(w => w.Key).ToList();
            C(spares.SequenceEqual(new[] { "knife" }), "Bo's spares: the knife (he holds a sword); his pistol is his kit, his lucky dagger never");
            var s = k.Assign();
            C(s.Count == 1 && s[0].Kind == PickKind.Spare && s[0].Colonist == "ana" && s[0].Weapon == "knife" && s[0].Other == "bo" && s[0].Gap == KitSlot.Melee,
                "Ana's empty melee slot: Bo's spare knife, handed over");
            k.W("stored", R("melee", 4f), 0.5f);
            k.O("ana", "stored", 4f);
            s = k.Assign();
            C(s.Count == 1 && s[0].Kind == PickKind.Sidearm && s[0].Weapon == "stored", "storage first when it does as well: no one gives anything up");
        }

        // ---------- The kit before anyone's upgrade, the front line first ----------
        {
            var k = new Colony();
            k.C("ana", fights: 1f);
            k.C("cy", fights: 0.3f);
            k.W("ra", R("ranged", 5f), 3.5f, holder: "ana");
            k.W("rc", R("ranged", 5f), 3.5f, holder: "cy");
            k.W("knife", R("melee", 4f), 0.5f);
            foreach (var c in new[] { "ana", "cy" })
            {
                k.O(c, "ra", 5f);
                k.O(c, "rc", 5f);
                k.O(c, "knife", 4f);
            }
            var s = k.Assign();
            C(s.Count == 1 && s[0].Colonist == "ana", "one knife, two without a blade: it goes to the one who fights most");
        }
        {
            // The pistol is a big upgrade for Bo's hand, but Ana has nothing: Ana is armed first.
            var k = new Colony(false);
            k.C("ana");
            k.C("bo");
            k.W("old", R("ranged", 2.5f), 1f, holder: "bo");
            k.W("pistol", R("ranged", 5f), 1f);
            k.O("ana", "pistol", 3f);
            k.O("bo", "old", 2.5f);
            k.O("bo", "pistol", 7f);
            var s = k.Assign();
            C(s.Count == 1 && s[0].Colonist == "ana" && s[0].Gap == KitSlot.Hand, "the only pistol to the one with nothing in hand, not to Bo's upgrade (+4.5)");
            k.W("bow", R("ranged", 4f), 1f);
            k.O("ana", "bow", 2.9f);
            k.O("bo", "bow", 2.5f);
            s = k.Assign();
            C(s.Count == 2 && s.Any(x => x.Colonist == "ana" && x.Weapon == "bow") && s.Any(x => x.Colonist == "bo" && x.Weapon == "pistol"),
                "with a bow besides, Ana takes the bow and Bo the pistol: everyone armed, and the colony's most");
        }
        {
            // A weapon kept for an empty sidearm slot isn't given to another's hand.
            var k = new Colony();
            k.C("ana");
            k.C("bo");
            k.W("rifle", R("ranged", 5f), 3.5f, holder: "ana");
            k.W("dagger", R("melee", 3.6f), 0.5f, holder: "bo");
            k.W("knife", R("melee", 4.8f), 0.5f);
            foreach (var c in new[] { "ana", "bo" })
            {
                k.O(c, "rifle", 5f);
                k.O(c, "dagger", 3.6f);
                k.O(c, "knife", 4.8f);
            }
            var s = k.Assign();
            C(s.Count == 1 && s[0].Colonist == "ana" && s[0].Gap == KitSlot.Melee, "the knife fills Ana's empty slot rather than bettering Bo's dagger (+1.2)");
        }

        // ---------- Dual Wield: the off hand ----------
        {
            var k = new Colony(false, true);
            k.C("ana");
            k.W("knife", R("melee", 4f), 0.5f, holder: "ana");
            var off = k.W("knife2", R("melee", 4f), 0.5f);
            off.CanOffHand = true;
            k.O("ana", "knife", 4f);
            k.O("ana", "knife2", 4f, pair: 4.4f);
            C(k.Slot("ana", KitSlot.OffHand).State == KitState.Missing, "a one-handed knife, both hands whole: the off hand is empty");
            var s = k.Assign();
            C(s.Count == 1 && s[0].Kind == PickKind.OffHand && s[0].Gap == KitSlot.OffHand && Near(s[0].Gain, 0.4f), "a second knife in the off hand adds 0.4: filled, under the usual 0.6");
            k.O("ana", "knife2", 4f, pair: 4.2f);
            C(k.Assign().Count == 0, "one that adds only 0.2 isn't");
            k.O("ana", "knife2", 4f, pair: 4.4f);
            var ana = k.I.Colonist("ana");
            ana.OneHand = true;
            C(k.Slot("ana", KitSlot.OffHand).Bar == KitBar.OneHand && k.Assign().Count == 0, "a hand missing: no off hand");
            ana.OneHand = false;
            k.I.Weapon("knife").TwoHanded = true;
            C(k.Slot("ana", KitSlot.OffHand).Bar == KitBar.TwoHanded && k.Assign().Count == 0, "a two-handed main weapon: no off hand");
            k.I.Weapon("knife").TwoHanded = false;
            off.CanOffHand = false;
            C(k.Assign().Count == 0, "one Dual Wield doesn't allow in the off hand: never");
            off.CanOffHand = true;
            off.HandOnly = true;
            C(k.Assign().Count == 0, "one on an outfit stand: Dual Wield's job takes it from the ground");
            off.HandOnly = false;
            var gun = k.W("pistol", R("ranged", 5f), 1f);
            gun.CanOffHand = true;
            k.O("ana", "pistol", 5f, pair: 6f);
            C(!k.Assign().Any(x => x.Weapon == "pistol"), "never a gun beside a blade: both hands fire or swing together");
            ana.Holds = null;
            C(k.Slot("ana", KitSlot.OffHand).Bar == KitBar.NeedsMain, "nothing in hand: the off hand waits on it");
        }

        // ---------- Forbidden: only asked for ----------
        {
            var k = new Colony();
            k.C("ana");
            k.W("rifle", R("ranged", 5f), 3.5f, holder: "ana");
            k.W("knife", R("melee", 4f), 0.5f).Forbidden = true;
            k.O("ana", "rifle", 5f);
            k.O("ana", "knife", 4f);
            var s = k.Assign();
            C(s.Count == 0, "a forbidden knife is never suggested");
            var f = WeaponKitMath.ForbiddenFills(k.I, s);
            C(f.Count == 1 && f[0].Weapon == "knife" && f[0].Gap == KitSlot.Melee && f[0].Kind == PickKind.Sidearm, "but it would fill her empty slot: hers to ask for");
            k.W("longsword", R("melee", 6f), 3f);
            k.W("stick", R("melee", 1.8f), 0.5f);
            k.O("ana", "longsword", 6f);
            k.O("ana", "stick", 1.8f);
            var looked = WeaponKitMath.Looked(k.I, k.I.Colonist("ana"), KitSlot.Melee, s);
            C(looked.Any(l => l.Weapon == "knife" && l.Why == KitLeftOut.Forbidden) && looked.Any(l => l.Weapon == "longsword" && l.Why == KitLeftOut.TooHeavy && Near(l.Limit, 1.9f))
              && looked.Any(l => l.Weapon == "stick" && l.Why == KitLeftOut.TooWeak), "what was looked at and why each was left out: forbidden, past the weight, too weak");
        }

        // ---------- Who should carry this ----------
        {
            var k = new Colony();
            k.C("ana");
            k.C("bo");
            k.W("rifle", R("ranged", 5f), 3.5f, holder: "ana");
            k.W("sword", R("melee", 4f), 2f, holder: "bo");
            var knife = k.W("knife", R("melee", 4.5f), 0.5f);
            k.O("ana", "rifle", 5f);
            k.O("ana", "knife", 4f);
            k.O("bo", "sword", 4f);
            k.O("bo", "knife", 6f);
            var who = WeaponPickMath.WhoShouldCarry(k.I, knife);
            C(who.Count == 2 && who[0].c.Key == "ana" && who[0].lacks && !who[1].lacks, "who should carry a knife: Ana first, who has no melee weapon");
        }

        // ---------- A trade that hands over a weapon fills its kind ----------
        {
            var k = new Colony();
            k.C("ana");
            k.W("rifle", R("ranged", 5f), 3.5f, holder: "ana");
            k.W("ar", R("ranged", 6f), 3.5f);
            var trade = new PickSuggestion { Kind = PickKind.Swap, Colonist = "ana", Weapon = "ar" };
            C(WeaponKitMath.Fills(k.I, trade, KitSlot.Ranged) && !WeaponKitMath.Fills(k.I, trade, KitSlot.Melee), "with Simple Sidearms a trade fills only the kind it hands over");
            k.I.Rules.Sidearms = false;
            C(WeaponKitMath.Fills(k.I, trade, KitSlot.Melee), "without it, any weapon in hand fills the kit");
        }

        // ---------- The asks ----------
        {
            var gap = new PickSuggestion { Kind = PickKind.Sidearm, Gain = 0.4f, Gap = KitSlot.Melee };
            C(WeaponAskMath.Askable(gap) && WeaponPickMath.AskTag(gap) == "kit", "an empty slot is asked for whatever its gain, said as [kit]");
            C(!WeaponAskMath.Askable(new PickSuggestion { Kind = PickKind.Keep, Gain = 3f, Gap = KitSlot.Melee }) && !WeaponAskMath.Askable(new PickSuggestion { Kind = PickKind.Spare, Gain = 3f, Gap = KitSlot.Melee }),
                "never one already in their pack or another's spare");
        }

        // ---------- Random colonies ----------
        {
            var rnd = new Random(4242);
            int bad = 0, filled = 0, cases = 300;
            for (int t = 0; t < cases; t++)
            {
                var k = new Colony(rnd.NextDouble() < 0.8, rnd.NextDouble() < 0.5);
                int cs = rnd.Next(2, 6);
                for (int i = 0; i < cs; i++)
                {
                    var c = k.C("c" + i, rnd.Next(0, 17), rnd.Next(0, 17), fights: 0.2f + (float)rnd.NextDouble() * 0.8f);
                    c.Brawler = rnd.NextDouble() < 0.1;
                    c.CanShoot = rnd.NextDouble() > 0.05;
                    c.OneHand = rnd.NextDouble() < 0.1;
                    double roll = rnd.NextDouble();
                    if (roll < 0.6) k.W("h" + i, R(rnd.NextDouble() < 0.6 ? "ranged" : "melee", 2f + (float)rnd.NextDouble() * 5f), 0.5f + (float)rnd.NextDouble() * 3f, holder: "c" + i).CanOffHand = rnd.NextDouble() < 0.5;
                    if (rnd.NextDouble() < 0.4) k.W("p" + i, R(rnd.NextDouble() < 0.5 ? "ranged" : "melee", 2f + (float)rnd.NextDouble() * 5f), 0.4f + (float)rnd.NextDouble() * 2f, carrier: "c" + i, remembered: rnd.NextDouble() < 0.7);
                }
                for (int j = 0; j < rnd.Next(0, 6); j++)
                {
                    var w = k.W("s" + j, R(rnd.NextDouble() < 0.5 ? "ranged" : "melee", 1.5f + (float)rnd.NextDouble() * 6f), 0.4f + (float)rnd.NextDouble() * 3f);
                    w.CanOffHand = rnd.NextDouble() < 0.5;
                    w.Forbidden = rnd.NextDouble() < 0.1;
                }
                foreach (var c in k.I.Colonists)
                    foreach (var w in k.I.Weapons)
                        k.O(c.Key, w.Key, Math.Max(1f, w.Rating.Overall + (float)(rnd.NextDouble() * 3 - 1.5)), rnd.NextDouble() < 0.1 ? "can't" : null,
                            w.CanOffHand ? w.Rating.Overall + (float)rnd.NextDouble() * 1.5f : (float?)null);
                var s = k.Assign();
                // No weapon to two; one suggestion a slot; never under the minimum; never against the rules.
                var weapons = s.Where(x => x.Kind != PickKind.Swap).Select(x => x.Weapon).ToList();
                if (weapons.Distinct().Count() != weapons.Count) bad++;
                foreach (var g in s.Where(x => x.Gap.HasValue).GroupBy(x => (x.Colonist, x.Gap.Value)))
                    if (g.Count() > 1) bad++;
                foreach (var x in s.Where(x => x.Kind != PickKind.Swap))
                {
                    var c = k.I.Colonist(x.Colonist);
                    var w = k.I.Weapon(x.Weapon);
                    var score = WeaponPickMath.Score(k.I, c, w);
                    if (!score.HasValue || w.Forbidden) bad++;
                    if (x.Gap.HasValue && x.Gap != KitSlot.OffHand && score.HasValue && score.Value < WeaponKitMath.Floor - 1e-4f) bad++;
                    if (w.Ranged && (c.Brawler || !c.CanShoot)) bad++;
                    if ((x.Kind == PickKind.Sidearm || x.Kind == PickKind.Keep) && !WeaponKitMath.FitsSidearm(k.I, w)) bad++;
                    if (x.Kind == PickKind.OffHand && (c.OneHand || !w.CanOffHand || w.TwoHanded)) bad++;
                    if (x.Gap.HasValue) filled++;
                }
                // Every empty slot that a free, sensible weapon could fill alone is filled by something.
                foreach (var c in k.I.Colonists.Where(c => c.CanFight))
                    foreach (var v in WeaponKitMath.Kit(k.I, c).Where(v => v.State == KitState.Missing && v.Slot != KitSlot.OffHand))
                    {
                        bool ranged = v.Slot == KitSlot.Ranged;
                        // A trade can arm someone holding a weapon they mustn't use: that fills their hand too.
                        bool any = s.Any(x => x.Colonist == c.Key && WeaponKitMath.Fills(k.I, x, v.Slot));
                        bool hand = WeaponKitMath.HandEmpty(k.I, c) || !k.I.Rules.Sidearms;
                        bool couldAlone = k.I.Colonists.Count(o => o.CanFight && WeaponKitMath.Kit(k.I, o).Any(ov => ov.State == KitState.Missing)) == 1
                                          && k.I.Weapons.Any(w => w.Stored && !w.Forbidden && w.Ranged == ranged && WeaponPickMath.Score(k.I, c, w) is float sc
                                                                  && WeaponKitMath.Sensible(sc, w, WeaponKitMath.Main(k.I, c)) && (hand || (WeaponKitMath.FitsSidearm(k.I, w) && k.I.Option(c, w)?.SidearmCant == null)));
                        if (couldAlone && !any && !s.Any(x => x.Colonist == c.Key && x.Gap.HasValue)) bad++;
                    }
            }
            C(bad == 0, $"random colonies: no weapon to two, one a slot, never under the minimum or against the rules, every lone gap filled ({bad})");
            C(filled > cases, $"random colonies: empty slots do get filled ({filled})");
        }

        // ---------- The lines: asked for, and said as they take it, in every age ----------
        {
            var pools = new Dictionary<string, List<string>>();
            foreach (var file in Directory.GetFiles(Path.Combine(root, "Defs", "FlavorPoolDefs"), "*.xml"))
                foreach (var el in XDocument.Load(file).Root!.Elements("ColonyCouncil.FlavorPoolDef"))
                    pools[(string)el.Element("defName")] = el.Element("lines")?.Descendants("li").Select(li => li.Value).ToList() ?? new List<string>();
            var ask = pools.TryGetValue("Rimfolk_Pool_WeaponAsk", out var a) ? a : new List<string>();
            var take = pools.TryGetValue("Rimfolk_Pool_WeaponKitTake", out var t) ? t : new List<string>();
            C(take.Count >= 20, "colonists taking a weapon themselves have a pool of their own");
            bool Fits(List<string> lines, HashSet<string> ctx, HashSet<string> eras, string must) =>
                lines.Any(l => LineTags.Of(l) is var p && p != null && LineTags.Match(p, ctx) > 0 && LineTags.EraFits(p, eras) && p.tags.Contains(must));
            foreach (var era in new[] { "tribal", "medieval", "industrial", "spacer" })
            {
                var eras = new HashSet<string> { era };
                if (era == "tribal" || era == "medieval") eras.Add("lowtech");
                foreach (var kind in new[] { "ranged", "melee", "offhand" })
                    C(Fits(ask, new HashSet<string>(eras) { "kit", kind }, eras, "kit"), $"an empty slot asked for: [kit] has a line for {kind} in the {era} age");
                foreach (var how in new[] { "hand", "sidearm", "offhand", "keep" })
                    foreach (var kind in new[] { "ranged", "melee" })
                        C(Fits(take, new HashSet<string>(eras) { how, kind }, eras, how), $"taking it: [{how}] has a line for a {kind} weapon in the {era} age");
            }
            C(take.All(l => l.Contains("{WEAPON}") && new[] { "hand", "sidearm", "offhand", "keep" }.Count(h => LineTags.Of(l)?.tags?.Contains(h) == true) == 1),
                "every line names the weapon and says how it is carried, one way, so its words are true");
            C(Read("Tools/Tests/ImmersionTests.cs").Contains("\"Rimfolk_Pool_WeaponKitTake\""), "its lines are held to one sentence of 90 characters (ImmersionTests)");
        }

        // ---------- The seams ----------
        string self = Read("Source/ColonyCouncil/Weapons/WeaponKitSelf.cs");
        string pick = Read("Source/ColonyCouncil/Weapons/WeaponPick.cs");
        string ui = Read("Source/ColonyCouncil/Weapons/WeaponPickUi.cs");
        string kitUi = Read("Source/ColonyCouncil/Weapons/WeaponKitUi.cs");
        string book = Read("Source/ColonyCouncil/Weapons/WeaponPickBook.cs");
        string asks = Read("Source/ColonyCouncil/Weapons/WeaponAsks.cs");
        string mods = Read("Source/ColonyCouncil/Weapons/WeaponMods.cs");
        string swap = Read("Source/ColonyCouncil/Weapons/WeaponSwapJob.cs");
        string card = Read("Source/ColonyCouncil/Weapons/WeaponCardWindow.cs");
        string keys = Read("Languages/English/Keyed/Rimfolk_Weapons.xml");
        string settings = Read("Source/ColonyCouncil/CouncilSettings.cs");
        string catalog = Read("Source/ColonyCouncil/UI/SettingsCatalog.cs");
        C(settings.Contains("public bool weaponKitSelf = true;") && settings.Contains("Scribe_Values.Look(ref weaponKitSelf, \"weaponKitSelf\", true);"), "the setting: on at first, saved");
        C(catalog.Contains("Toggle(\"weaponKitSelf\", \"CC_Settings_WeaponKitSelf\", \"CC_SetNote_WeaponKitSelf\", \"weaponRatings,deepHooks\");")
          && keys.Contains("<CC_Settings_WeaponKitSelf>Colonists fill an empty weapon slot themselves</CC_Settings_WeaponKitSelf>") && keys.Contains("<CC_Settings_WeaponKitSelf_Tip>")
          && keys.Contains("<CC_SetNote_WeaponKitSelf>"), "on the settings page under Weapons, with its words");
        // Filled themselves: a real job, only in an empty slot, in free time and safe, once a day, a line as they take it.
        C(self.Contains("p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true)") && !self.Contains("TryTakeOrderedJob"), "they set off of their own accord, by a real job (not an order)");
        C(pick.Contains("case PickKind.Primary: return JobMaker.MakeJob(JobDefOf.Equip, t);") && pick.Contains("sidearmJob != null ? JobMaker.MakeJob(sidearmJob, t)")
          && pick.Contains("offHandJob != null ? JobMaker.MakeJob(offHandJob, t)"), "the game's Equip into an empty hand, Simple Sidearms' and Dual Wield's own jobs");
        C(self.Contains("if (p.equipment?.Primary != null) return false;") && self.Contains("s?.Gap == null"), "only an empty slot: never in place of a weapon they hold");
        C(self.Contains("WeaponAsks.FreeAndSafe(p, false)") && self.Contains("book.MayFetch(p, now)") && self.Contains("EachTicks = 60000"), "in their free time, safe at home, at most once a day");
        C(self.Contains("WeaponPick.Confirm(picks, s).NullOrEmpty()") && self.Contains("WeaponPick.WhyNot(picks, s) == null"), "never a persona weapon that would bond, never what something stops");
        C(self.Contains("AddFinishAction(") && self.Contains("JobCondition.Succeeded) Say(p, said, t)") && self.Contains("Speech.Say(p, line)"), "the line as they pick it up, through Speech");
        C(book.Contains("WeaponKitSelf.Look(p, picks, now)") && book.Contains("Scribe_Collections.Look(ref fetched, \"fetched\"") && Read("Tools/save_estimate.py").Contains("Weapons/WeaponPickBook.cs:fetched"),
            "on the suggestions' hourly slice, its day kept with the game and modelled in the save estimate");
        // The asks: only what they don't fill themselves; a forbidden one asked for, its row allowing it.
        C(asks.Contains("!WeaponKitSelf.On || !WeaponKitSelf.CanFetch(picks, x, p)") && asks.Contains("picks.kitAsks") && asks.Contains("CC_WeaponAsk_InboxAllow"),
            "an empty slot asked for only when they don't fill it themselves; a forbidden one asked for, the inbox allowing it");
        C(pick.Contains("picks.kitAsks = WeaponKitMath.ForbiddenFills(input, picks.list);") && pick.Contains("w.Forbidden = forbidden;"), "forbidden weapons in storage, only to ask for");
        // Simple Sidearms and Dual Wield, by name.
        C(mods.Contains("\"rememberedWeapons\"") && mods.Contains("\"InformOfAddedSidearm\"") && mods.Contains("BodyPartDefOf.Hand") && mods.Contains("RoleEffect_NoRangedWeapons"),
            "Simple Sidearms' memory read and told by name; Dual Wield's missing hand; a role's bar");
        C(pick.Contains("o.SidearmCant = SidearmWhyNot(t, p)"), "Simple Sidearms' own refusal asked of each stored weapon light enough");
        foreach (var f in new[] { self, kitUi, swap, pick })
            C(!f.Contains("using SimpleSidearms") && !f.Contains("using DualWield") && !f.Contains("using PeteTimesSix"), "no hard reference to the weapon mods");
        var jobs = Read("Defs/Misc/Rimfolk_WeaponPicks.xml");
        C(jobs.Contains("<defName>Rimfolk_TakeSpareWeapon</defName>") && jobs.Contains("<driverClass>ColonyCouncil.JobDriver_TakeSpareWeapon</driverClass>")
          && swap.Contains("class JobDriver_TakeSpareWeapon : JobDriver") && swap.Contains("Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch)"),
            "another's spare: a job walked out, the one taking it going over to the one who carries it");
        // Shown: the row, the quiet line, the Defenses page, the card.
        C(ui.Contains("CC_WeaponKit_TagMissing") && ui.Contains("WeaponPick.FillWords(picks, s, false)") && ui.Contains("CC_WeaponKit_Quiet_"), "the row's missing case and the honest quiet line");
        C(ui.Contains("WeaponKitWords.DrawChips(") && ui.Contains("WeaponPick.DoKit(modelPicks)") && keys.Contains("<CC_WeaponKit_Panel_Fill>Fill every kit ({COUNT})</CC_WeaponKit_Panel_Fill>"),
            "the Defenses page: each colonist's kit, and Fill every kit, which only gives the orders");
        C(card.Contains("CC_WeaponKit_Card_Lacks_"), "a weapon's card: who lacks its kind");
        foreach (var why in Enum.GetNames(typeof(KitLeftOut))) C(keys.Contains("<CC_WeaponKit_Left_" + why + ">"), $"why a weapon was left out: {why}");
        foreach (var bar in Enum.GetNames(typeof(KitBar)).Where(b => b != "None")) C(keys.Contains("<CC_WeaponKit_Bar_" + bar + ">"), $"why a slot isn't for them: {bar}");
        foreach (var slot in new[] { "Ranged", "Melee", "OffHand", "Hand" }) C(keys.Contains("<CC_WeaponKit_Gap_" + slot + ">"), $"an empty slot in words: {slot}");
        foreach (var kind in new[] { "Primary", "Sidearm", "OffHand", "Keep", "Spare" }) C(keys.Contains("<CC_WeaponKit_Fill_" + kind + ">"), $"how a slot is filled: {kind}");
        foreach (var w in new[] { "Hand", "Ranged", "Melee", "Offhand", "Pack", "Spare" }) C(keys.Contains("<CC_WeaponKit_Why_" + w + ">"), $"the reason: {w}");
        C(!keys.Contains("—"), "no em dashes");
        var almanac = Read("Defs/AlmanacArticleDefs/Almanac_Weapons.xml");
        C(almanac.Contains("<heading>A full kit for every colonist</heading>"), "the Almanac's page tells it");

        return n;
    }
}
