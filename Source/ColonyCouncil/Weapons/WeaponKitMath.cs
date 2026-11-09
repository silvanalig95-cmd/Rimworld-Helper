using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>A slot of a colonist's kit: a ranged weapon, a melee weapon, the off hand; without Simple Sidearms, the one weapon in hand.</summary>
    public enum KitSlot
    {
        Ranged,
        Melee,
        OffHand,
        Hand
    }

    /// <summary>How a slot stands: something in it, empty (a gap the colony should fill), or not for them.</summary>
    public enum KitState
    {
        Filled,
        Missing,
        NotForThem
    }

    /// <summary>Why a slot isn't for them.</summary>
    public enum KitBar
    {
        None,
        /// <summary>Incapable of violence: no kit at all.</summary>
        CantFight,
        /// <summary>Incapable of shooting (a backstory, a trait, a gene).</summary>
        CantShoot,
        /// <summary>A brawler: a gun in their hands costs them mood.</summary>
        Brawler,
        /// <summary>Their Ideology role bars the kind.</summary>
        Role,
        /// <summary>Without Simple Sidearms a colonist carries one weapon, and they hold the other kind.</summary>
        OneWeapon,
        /// <summary>Without Dual Wield there is no off hand.</summary>
        NoDualWield,
        /// <summary>A hand or an arm missing: Dual Wield allows nothing in the off hand.</summary>
        OneHand,
        /// <summary>Nothing in the main hand yet: the off hand pairs with it.</summary>
        NeedsMain,
        /// <summary>What they hold takes both hands.</summary>
        TwoHanded
    }

    /// <summary>One slot of a colonist's kit as it stands: its state, what fills it (a weapon's key), or why it isn't for them.</summary>
    public sealed class KitView
    {
        public KitSlot Slot;
        public KitState State;
        public string Weapon;
        public KitBar Bar;
    }

    /// <summary>Why a weapon the colony has was left out of an empty slot (the quiet line's tooltip).</summary>
    public enum KitLeftOut
    {
        /// <summary>Nothing stops it (it fills the slot, or would).</summary>
        Fits,
        Forbidden,
        /// <summary>The colonist can't or mustn't carry it (Text: why, in the game's words).</summary>
        Cant,
        /// <summary>Past Simple Sidearms' weight for a sidearm (Value: its mass, Limit: the weight).</summary>
        TooHeavy,
        /// <summary>Simple Sidearms refuses it as a sidearm for them now (Text: its words).</summary>
        SidearmRefused,
        /// <summary>A one-use launcher: never a sidearm.</summary>
        OneUse,
        /// <summary>On an outfit stand: Simple Sidearms' and Dual Wield's jobs take a weapon from the ground or a shelf.</summary>
        OnStand,
        /// <summary>It scores under the least worth carrying for them (Value: its score, Limit: <see cref="WeaponKitMath.Floor"/>).</summary>
        TooWeak,
        /// <summary>A blade no better in close work than their gun's own blows (Value: its score, Limit: the gun's).</summary>
        NoBetterThanBash,
        /// <summary>In the off hand it adds too little to both hands together (Value: what it adds).</summary>
        AddsLittle,
        /// <summary>Dual Wield doesn't allow it in the off hand.</summary>
        NotOffHand,
        /// <summary>It takes both hands.</summary>
        TwoHanded,
        /// <summary>The colony's choice gives it to another colonist (Text: their key).</summary>
        Given
    }

    /// <summary>One weapon looked at for an empty slot, and why it was left out.</summary>
    public sealed class KitLook
    {
        public string Weapon;
        public KitLeftOut Why;
        public float Value, Limit;
        public string Text = "";
        /// <summary>In the colonist's own pack; carried by another colonist as a spare (their key), else null.</summary>
        public bool Own;
        public string Owner;
    }

    /// <summary>
    /// A full kit for every colonist (the player's words, 9 November 2026: "the pawns should always have a ranged and melee
    /// weapon, assuming they have no restrictions, and suggest/equip them accordingly. also offhand if they have capacity &amp;
    /// compatibility"), without game types (Tools/Tests try it). With Simple Sidearms every colonist who can fight carries
    /// one ranged and one melee weapon, one in hand and the other as a sidearm; with Dual Wield, a weapon in the off hand
    /// where their hands, the weapons and the mod's rules allow it and it adds something. Without Simple Sidearms the kit is
    /// the one weapon in hand, without Dual Wield there is no off hand.
    /// <para>
    /// Restrictions decide the kit, and the kit never fights them: incapable of violence, no kit; incapable of shooting or a
    /// brawler, melee only; a kind their Ideology role bars, none of it; a weapon the game refuses them (coded, bonded, a
    /// race's or a role's rule), one too heavy for them, one their faith despises, never (WeaponPickMath.Score); a sidearm
    /// only within Simple Sidearms' weights and when it doesn't refuse it; an off-hand weapon only where Dual Wield allows
    /// it, of the main hand's kind, never two-handed, never beside a two-handed main weapon, never with a hand or arm missing.
    /// </para>
    /// <para>
    /// An empty slot is a gap, filled before anyone's upgrade and even under the usual threshold: anything sensible
    /// (<see cref="Floor"/>) is better than nothing. The weapon chosen is the best for the part it plays (<see cref="RoleScore"/>):
    /// in hand, their own score; a shooter's melee sidearm light and quick, and better than clubbing with their gun; a melee
    /// fighter's ranged sidearm a light gun to finish or kite with; the off hand, what it adds to both hands together. It comes
    /// from storage, their own pack (a weapon Simple Sidearms doesn't remember, made a sidearm), or another colonist's spare
    /// (a weapon they carry beyond their own kit), the colony's choice weighing who fights most when weapons are short.
    /// </para>
    /// </summary>
    public static class WeaponKitMath
    {
        /// <summary>
        /// The least a weapon filling an empty slot must score for them: 2.0, twice bare hands (WeaponPickMath.Unarmed). On the
        /// rating's scale "poor" runs up to 3.5; under 2.0 a weapon is nearly useless in their hands (a smoke launcher, a short
        /// bow for someone who can't aim) and isn't pushed on anyone. Their own score: their skill, traits and body, their
        /// passion, the fights you meet.
        /// </summary>
        public const float Floor = 2f;

        /// <summary>
        /// A shooter's melee sidearm must beat their gun's own blows (its "in melee" rating: a rifle's butt is about 3.0) by
        /// this much: one no better than clubbing with the gun adds nothing.
        /// </summary>
        public const float OverBash = 0.3f;

        /// <summary>An off-hand weapon must add this much to both hands together over the main hand alone: half the usual threshold, something felt.</summary>
        public const float OffHandAdds = 0.3f;

        /// <summary>
        /// Choosing among the weapons that may fill a sidearm's slot (never the minimum): half the weapon's "sidearm" situation
        /// over its own overall (light and handy beside the main weapon), a fifth of a point for a quick blade (a shooter's
        /// melee sidearm), a tenth of its "kiting" situation over its overall for a gun (a melee fighter's ranged sidearm, to
        /// finish or kite with).
        /// </summary>
        public const float LightShare = 0.5f, QuickBonus = 0.2f, ReachShare = 0.1f;

        /// <summary>Choosing where it comes from: a weapon already in their pack costs no walk (+0.3); another's spare asks someone to hand it over (-0.3).</summary>
        public const float OwnBonus = 0.3f, SpareCost = 0.3f;

        // ---------------------------------------------------------------- what is for them

        /// <summary>Why a kind isn't for a colonist, or <see cref="KitBar.None"/>: incapable of violence, of shooting, a brawler's guns, their role's rule.</summary>
        public static KitBar KindBar(PickColonist c, bool ranged)
        {
            if (c == null || !c.CanFight) return KitBar.CantFight;
            if (ranged)
            {
                if (!c.CanShoot) return KitBar.CantShoot;
                if (c.Brawler) return KitBar.Brawler;
                if (c.RoleNoRanged) return KitBar.Role;
            }
            else if (c.RoleNoMelee) return KitBar.Role;
            return KitBar.None;
        }

        public static bool Allowed(PickColonist c, bool ranged) => KindBar(c, ranged) == KitBar.None;

        public static KitSlot SlotOf(bool ranged) => ranged ? KitSlot.Ranged : KitSlot.Melee;

        /// <summary>The kind a colonist would rather hold: a gun when they shoot at least as well as they fight hand to hand (and may), else a blade.</summary>
        public static bool Prefers(PickColonist c) => Allowed(c, true) && (c.Shooting >= c.Melee || !Allowed(c, false));

        private static bool Usable(PickInput input, PickColonist c, PickWeapon w) => w?.Rating != null && WeaponPickMath.Score(input, c, w).HasValue;

        /// <summary>The weapon in their hand they may use; one they mustn't (a gun in a brawler's hands, a despised one) counts as none.</summary>
        public static PickWeapon Main(PickInput input, PickColonist c)
        {
            var held = c != null ? input.Weapon(c.Holds) : null;
            return Usable(input, c, held) ? held : null;
        }

        /// <summary>The best usable weapon of a kind they carry as a sidearm (Simple Sidearms remembers it; a one-use launcher doesn't count), or null.</summary>
        private static PickWeapon Carried(PickInput input, PickColonist c, bool ranged)
        {
            if (!input.Rules.Sidearms) return null;
            PickWeapon best = null;
            float bs = float.MinValue;
            foreach (var k in c.Carries)
            {
                if (c.Unremembered.Contains(k)) continue;
                var w = input.Weapon(k);
                if (w?.Rating == null || w.Ranged != ranged || (ranged && w.OneUse)) continue;
                var s = WeaponPickMath.Score(input, c, w);
                if (s.HasValue && s.Value > bs) (bs, best) = (s.Value, w);
            }
            return best;
        }

        /// <summary>What fills a kind in their kit now: the weapon in hand of that kind, or with Simple Sidearms the best of it they carry as a sidearm; null when nothing does.</summary>
        public static PickWeapon Has(PickInput input, PickColonist c, bool ranged)
        {
            var main = Main(input, c);
            return main != null && main.Ranged == ranged ? main : Carried(input, c, ranged);
        }

        /// <summary>
        /// Nothing at all to fight with: no weapon in hand they may use, and (with Simple Sidearms) none in their pack either.
        /// Such a colonist is armed before anyone's upgrade.
        /// </summary>
        public static bool HandEmpty(PickInput input, PickColonist c)
        {
            if (c == null || !c.CanFight || Main(input, c) != null) return false;
            if (!input.Rules.Sidearms) return true;
            foreach (var k in c.Carries)
                if (input.Weapon(k) is PickWeapon w && !(w.Ranged && w.OneUse) && Usable(input, c, w))
                    return false;
            return true;
        }

        /// <summary>Whether their kit lacks a kind they should carry: none of it (with Simple Sidearms), or nothing in hand (without).</summary>
        public static bool Lacks(PickInput input, PickColonist c, bool ranged) =>
            c != null && Allowed(c, ranged) && (input.Rules.Sidearms ? Has(input, c, ranged) == null : Main(input, c) == null);

        /// <summary>A gun's own blows (its "in melee" rating), or bare hands' worth for anything else.</summary>
        public static float Bash(PickWeapon main) =>
            main != null && main.Ranged && main.Rating != null && main.Rating.Situations.TryGetValue("in melee", out var v) ? v : WeaponPickMath.Unarmed;

        /// <summary>What a colonist has in an empty slot of a kind: a shooter in close work their gun's blows, anyone else bare hands.</summary>
        public static float Baseline(PickWeapon main, bool ranged) => !ranged && main != null && main.Ranged ? Bash(main) : WeaponPickMath.Unarmed;

        /// <summary>Within Simple Sidearms' weights for a sidearm (its default preset unless set otherwise), not a one-use launcher, not on an outfit stand.</summary>
        public static bool FitsSidearm(PickInput input, PickWeapon w) =>
            w != null && !w.HandOnly && !(w.Ranged && w.OneUse) && w.Mass <= (w.Ranged ? input.Rules.SidearmRangedMass : input.Rules.SidearmMeleeMass) + 0.001f;

        /// <summary>Worth carrying in an empty slot: at least <see cref="Floor"/>, and a shooter's blade better than their gun's blows by <see cref="OverBash"/>.</summary>
        public static bool Sensible(float score, PickWeapon w, PickWeapon main) =>
            score >= Floor - 1e-4f && (w.Ranged || main == null || !main.Ranged || score >= Bash(main) + OverBash - 1e-4f);

        /// <summary>
        /// The worth of a weapon in the part it plays in the kit, for choosing among those that may fill a slot: in hand, their
        /// own score (WeaponPickMath.Score); as a sidearm, light and handy (<see cref="LightShare"/>), a quick blade
        /// (<see cref="QuickBonus"/>), a gun with reach (<see cref="ReachShare"/>). Null when they can't or mustn't carry it.
        /// </summary>
        public static float? RoleScore(PickInput input, PickColonist c, PickWeapon w, bool sidearm)
        {
            var s = WeaponPickMath.Score(input, c, w);
            if (!s.HasValue || !sidearm) return s;
            float v = s.Value + LightShare * (w.Situation("sidearm") - w.Overall);
            if (w.Ranged) v += ReachShare * (w.Situation("kiting") - w.Overall);
            else if (w.HasTag("quick")) v += QuickBonus;
            return v;
        }

        // ---------------------------------------------------------------- the kit as it stands

        /// <summary>A colonist's kit as it stands: a ranged weapon, a melee weapon and the off hand, each filled, missing or not for them.</summary>
        public static List<KitView> Kit(PickInput input, PickColonist c)
        {
            var list = new List<KitView>();
            var main = Main(input, c);
            foreach (bool ranged in new[] { true, false })
            {
                var v = new KitView { Slot = SlotOf(ranged), Bar = KindBar(c, ranged) };
                if (v.Bar != KitBar.None) v.State = KitState.NotForThem;
                else if (Has(input, c, ranged) is PickWeapon w)
                {
                    v.State = KitState.Filled;
                    v.Weapon = w.Key;
                }
                else if (!input.Rules.Sidearms && main != null)
                {
                    v.State = KitState.NotForThem;
                    v.Bar = KitBar.OneWeapon;
                }
                else v.State = KitState.Missing;
                list.Add(v);
            }
            var off = new KitView { Slot = KitSlot.OffHand, State = KitState.NotForThem };
            if (c == null || !c.CanFight) off.Bar = KitBar.CantFight;
            else if (!input.Rules.DualWield) off.Bar = KitBar.NoDualWield;
            else if (c.OffHand != null)
            {
                off.State = KitState.Filled;
                off.Weapon = c.OffHand;
            }
            else if (c.OneHand) off.Bar = KitBar.OneHand;
            else if (main == null) off.Bar = KitBar.NeedsMain;
            else if (main.TwoHanded) off.Bar = KitBar.TwoHanded;
            else off.State = KitState.Missing;
            list.Add(off);
            return list;
        }

        /// <summary>Whether a suggestion fills this slot of the kit (one in hand fills either kind without Simple Sidearms).</summary>
        public static bool Fills(PickSuggestion s, KitSlot slot) =>
            s?.Gap != null && (s.Gap == slot || (s.Gap == KitSlot.Hand && (slot == KitSlot.Ranged || slot == KitSlot.Melee)));

        /// <summary>
        /// Whether a suggestion fills this slot as the map would be after it: an empty slot's own (<see cref="Fills(PickSuggestion, KitSlot)"/>),
        /// or a trade that puts a weapon of the slot's kind in their hand (someone holding a weapon they mustn't use; without
        /// Simple Sidearms any weapon in hand fills the kit).
        /// </summary>
        public static bool Fills(PickInput input, PickSuggestion s, KitSlot slot)
        {
            if (Fills(s, slot)) return true;
            if (s?.Kind != PickKind.Swap || slot == KitSlot.OffHand || slot == KitSlot.Hand) return false;
            var w = input.Weapon(s.Weapon);
            return w != null && (!input.Rules.Sidearms || w.Ranged == (slot == KitSlot.Ranged));
        }

        /// <summary>
        /// The weapons a colonist carries beyond their own kit, which another colonist's empty slot may take (with Simple
        /// Sidearms): each kind they don't hold keeps its best usable one (one it remembers first); the rest are spares: another
        /// of a kind they hold or keep, one they can't use. Never a lucky weapon or an heirloom, their bonded weapon, or a one-use
        /// launcher; none from someone who doesn't fight or whose suggestions are set aside.
        /// </summary>
        public static List<PickWeapon> Spares(PickInput input, PickColonist c)
        {
            var list = new List<PickWeapon>();
            if (c == null || !input.Rules.Sidearms || !c.CanFight || c.NotNow || c.Carries.Count == 0) return list;
            var main = Main(input, c);
            var keep = new HashSet<string>();
            foreach (bool ranged in new[] { true, false })
            {
                if (main != null && main.Ranged == ranged) continue;
                PickWeapon best = null;
                float bs = float.MinValue;
                foreach (var k in c.Carries)
                {
                    var w = input.Weapon(k);
                    if (w?.Rating == null || w.Ranged != ranged || (ranged && w.OneUse)) continue;
                    var s = WeaponPickMath.Score(input, c, w);
                    if (!s.HasValue) continue;
                    float v = s.Value + (c.Unremembered.Contains(k) ? 0f : 100f);
                    if (v > bs) (bs, best) = (v, w);
                }
                if (best != null) keep.Add(best.Key);
            }
            foreach (var k in c.Carries)
            {
                if (keep.Contains(k) || k == c.Bonded || c.Keepsakes.Any(x => x?.Weapon == k)) continue;
                if (input.Weapon(k) is PickWeapon w && w.Rating != null && !(w.Ranged && w.OneUse)) list.Add(w);
            }
            return list;
        }

        // ---------------------------------------------------------------- filling the empty slots

        private sealed class Fill
        {
            public PickColonist c;
            public bool ranged;
            public PickWeapon w, main;
            public PickKind kind;
            public float value, score;
        }

        /// <summary>
        /// The weapons that may fill an empty slot of a kind for a colonist, with how they would be taken and their worth for
        /// choosing: in hand (<paramref name="hand"/>: their hand is empty) or as a sidearm (within Simple Sidearms' weights,
        /// not one it refuses them); from their own pack (a weapon it doesn't remember: taken in hand or made a sidearm, no
        /// walk, <see cref="OwnBonus"/>), from storage, or another colonist's spare (handed over, <see cref="SpareCost"/>); each
        /// one sensible for them (<see cref="Sensible"/>).
        /// </summary>
        private static IEnumerable<(PickWeapon w, PickKind kind, float value, float score)> Candidates(PickInput input, PickColonist c, bool ranged, bool hand,
            PickWeapon main, List<PickWeapon> free, HashSet<string> taken, List<PickWeapon> spares, bool storageOnly)
        {
            (float v, float s)? Fit(PickWeapon w)
            {
                var s = WeaponPickMath.Score(input, c, w);
                if (!s.HasValue || !Sensible(s.Value, w, main)) return null;
                return (RoleScore(input, c, w, !hand) ?? s.Value, s.Value);
            }
            if (!storageOnly)
                foreach (var k in c.Carries)
                {
                    if (!c.Unremembered.Contains(k) || taken.Contains(k)) continue;
                    var w = input.Weapon(k);
                    if (w?.Rating == null || w.Ranged != ranged || !FitsSidearm(input, w)) continue;
                    if (Fit(w) is var f && f.HasValue) yield return (w, hand ? PickKind.Primary : PickKind.Keep, f.Value.v + OwnBonus, f.Value.s);
                }
            foreach (var w in WeaponPickMath.Heads(free, taken))
            {
                if (w.Ranged != ranged) continue;
                if (!hand && (!FitsSidearm(input, w) || input.Option(c, w)?.SidearmCant != null)) continue;
                if (Fit(w) is var f && f.HasValue) yield return (w, hand ? PickKind.Primary : PickKind.Sidearm, f.Value.v, f.Value.s);
            }
            if (storageOnly || spares == null) yield break;
            foreach (var w in spares)
            {
                if (w.CarriedBy == c.Key || taken.Contains(w.Key) || w.Ranged != ranged) continue;
                if (!hand && !FitsSidearm(input, w)) continue;
                if (Fit(w) is var f && f.HasValue) yield return (w, PickKind.Spare, f.Value.v - SpareCost, f.Value.s);
            }
        }

        /// <summary>
        /// Simple Sidearms: each kind a colonist should carry and lacks, filled (see the class); with nothing in hand, the kind
        /// they would rather hold goes in hand (unless nothing of it will do), the other as a sidearm. The colony's choice:
        /// greedily the most worth, each one's worth by how much they fight (<see cref="PickColonist.Fights"/>), so the front
        /// line is served first when weapons are short; no weapon to two. <paramref name="holds"/>: what each will hold.
        /// <paramref name="skip"/>: slots already filled; <paramref name="storageOnly"/>: only what lies in
        /// <paramref name="free"/>.
        /// </summary>
        internal static List<PickSuggestion> FillKinds(PickInput input, List<PickColonist> fighters, List<PickWeapon> free, HashSet<string> taken,
            Dictionary<string, PickWeapon> holds, HashSet<(string, KitSlot)> skip = null, bool storageOnly = false)
        {
            var result = new List<PickSuggestion>();
            if (!input.Rules.Sidearms) return result;
            var spares = new List<PickWeapon>();
            if (!storageOnly)
                foreach (var g in input.Colonists.OrderBy(x => x.Key, StringComparer.Ordinal))
                    foreach (var w in Spares(input, g))
                        if (!taken.Contains(w.Key))
                            spares.Add(w);
            var options = new List<Fill>();
            foreach (var c in fighters)
            {
                var main = holds.TryGetValue(c.Key, out var m) && Usable(input, c, m) ? m : null;
                var lacking = new List<bool>();
                foreach (bool ranged in new[] { true, false })
                {
                    if (!Allowed(c, ranged) || (main != null && main.Ranged == ranged) || Carried(input, c, ranged) != null) continue;
                    if (skip != null && skip.Contains((c.Key, SlotOf(ranged)))) continue;
                    lacking.Add(ranged);
                }
                if (lacking.Count == 0) continue;
                // With nothing in hand, one of the kinds they lack goes in hand: the one they prefer, unless nothing of it will do.
                bool? hand = null;
                if (main == null && !(skip != null && skip.Contains((c.Key, KitSlot.Hand))))
                {
                    bool pref = lacking.Count == 1 ? lacking[0] : Prefers(c);
                    hand = pref;
                    if (lacking.Count > 1 && !Candidates(input, c, pref, true, null, free, taken, spares, storageOnly).Any()) hand = !pref;
                }
                foreach (bool ranged in lacking)
                    foreach (var (w, kind, value, score) in Candidates(input, c, ranged, hand == ranged, main, free, taken, spares, storageOnly))
                        options.Add(new Fill { c = c, ranged = ranged, w = w, main = main, kind = kind, value = c.Fights * value, score = score });
            }
            var done = new HashSet<(string, bool)>();
            foreach (var o in options.OrderByDescending(o => o.value).ThenBy(o => o.c.Key, StringComparer.Ordinal).ThenBy(o => o.w.Key, StringComparer.Ordinal))
            {
                if (done.Contains((o.c.Key, o.ranged))) continue;
                var w = o.w.Stored ? WeaponPickMath.Untaken(o.w, free, taken) : taken.Contains(o.w.Key) ? null : o.w;
                if (w == null) continue;
                done.Add((o.c.Key, o.ranged));
                taken.Add(w.Key);
                result.Add(new PickSuggestion
                {
                    Kind = o.kind, Colonist = o.c.Key, Weapon = w.Key, Instead = o.main?.Key, Other = o.kind == PickKind.Spare ? w.CarriedBy : null,
                    Gain = o.score - Baseline(o.main, o.ranged), Rating = input.Option(o.c, w)?.Rating ?? w.Overall, Before = 0f, Gap = SlotOf(o.ranged)
                });
            }
            return result;
        }

        /// <summary>
        /// Dual Wield: the off hand of a colonist who keeps what they hold (a one-handed weapon they may use), both hands
        /// whole, nothing there yet: a weapon of the same kind (guns with guns, blades with blades: both hands fire or swing
        /// together) that Dual Wield allows there, not two-handed, not on an outfit stand (its job takes it from the ground),
        /// when both hands together (its penalties in, PickOption.Pair) add <see cref="OffHandAdds"/> or more. Greedily the
        /// most worth, by how much each fights.
        /// </summary>
        internal static List<PickSuggestion> FillOffHands(PickInput input, List<PickColonist> keeping, List<PickWeapon> free, HashSet<string> taken,
            HashSet<string> skip = null)
        {
            var result = new List<PickSuggestion>();
            if (!input.Rules.DualWield) return result;
            var options = new List<(PickColonist c, PickWeapon w, float gain, float pair, float alone)>();
            foreach (var c in keeping)
            {
                if (c.OffHand != null || c.OneHand || (skip != null && skip.Contains(c.Key))) continue;
                var main = Main(input, c);
                if (main == null || main.TwoHanded) continue;
                var mo = input.Option(c, main);
                if (mo == null) continue;
                foreach (var w in WeaponPickMath.Heads(free, taken))
                {
                    if (w.HandOnly || !w.CanOffHand || w.TwoHanded || w.Ranged != main.Ranged) continue;
                    var o = input.Option(c, w);
                    if (o == null || o.Cant != null || !o.Pair.HasValue || !Usable(input, c, w)) continue;
                    float gain = o.Pair.Value - mo.Rating;
                    if (gain < OffHandAdds - 1e-4f) continue;
                    options.Add((c, w, gain, o.Pair.Value, mo.Rating));
                }
            }
            var done = new HashSet<string>();
            foreach (var (c, first, gain, pair, alone) in options.OrderByDescending(o => o.c.Fights * o.gain).ThenBy(o => o.c.Key, StringComparer.Ordinal).ThenBy(o => o.w.Key, StringComparer.Ordinal))
            {
                var w = done.Contains(c.Key) ? null : WeaponPickMath.Untaken(first, free, taken);
                if (w == null) continue;
                done.Add(c.Key);
                taken.Add(w.Key);
                result.Add(new PickSuggestion { Kind = PickKind.OffHand, Colonist = c.Key, Weapon = w.Key, Instead = c.Holds, Gain = gain, Rating = pair, Before = alone, Gap = KitSlot.OffHand });
            }
            return result;
        }

        /// <summary>
        /// Before anyone's upgrade: the weapons the empty slots of those who hold something (and those with only a pack) would
        /// take as things stand, kept from everyone else's hand. <paramref name="kindBound"/>: those given a sidearm of the kind
        /// they lack (their main hand's kind must stay); <paramref name="handBound"/>: those whose hand the kit fills or whose
        /// off hand it fills (their hand stays as the kit leaves it this time).
        /// </summary>
        internal static HashSet<string> Reserve(PickInput input, List<PickColonist> keeping, List<PickWeapon> free, out HashSet<string> kindBound, out HashSet<string> handBound)
        {
            var taken = new HashSet<string>();
            var holds = keeping.ToDictionary(c => c.Key, c => Main(input, c));
            var kinds = FillKinds(input, keeping, free, taken, holds);
            var offs = FillOffHands(input, keeping, free, taken);
            kindBound = new HashSet<string>(kinds.Where(s => s.Kind != PickKind.Primary).Select(s => s.Colonist));
            handBound = new HashSet<string>(kinds.Where(s => s.Kind == PickKind.Primary).Concat(offs).Select(s => s.Colonist));
            return new HashSet<string>(kinds.Concat(offs).Where(s => input.Weapon(s.Weapon)?.Stored == true).Select(s => s.Weapon));
        }

        /// <summary>
        /// The empty slots the colony's choice (<paramref name="assigned"/>) leaves empty that a forbidden weapon in storage
        /// would fill: what a colonist may ask for (they never take a forbidden weapon themselves). The same rules as the
        /// choice, from forbidden weapons only, no weapon to two.
        /// </summary>
        public static List<PickSuggestion> ForbiddenFills(PickInput input, List<PickSuggestion> assigned)
        {
            var result = new List<PickSuggestion>();
            var forbidden = input.Weapons.Where(w => w.Stored && w.Forbidden && w.Rating != null).OrderBy(w => w.Key, StringComparer.Ordinal).ToList();
            if (forbidden.Count == 0) return result;
            var fighters = input.Colonists.Where(c => c.CanFight && !c.NotNow).OrderBy(c => c.Key, StringComparer.Ordinal).ToList();
            var taken = new HashSet<string>();
            var inHand = new Dictionary<string, PickWeapon>();
            foreach (var s in assigned)
                if (s.Kind == PickKind.Primary || s.Kind == PickKind.Swap || (s.Kind == PickKind.Spare && s.Gap.HasValue && Main(input, input.Colonist(s.Colonist)) == null))
                    inHand[s.Colonist] = input.Weapon(s.Weapon);
            var skip = new HashSet<(string, KitSlot)>();
            foreach (var s in assigned.Where(s => s.Gap.HasValue)) skip.Add((s.Colonist, s.Gap.Value));
            foreach (var k in inHand.Keys) skip.Add((k, KitSlot.Hand));
            // Nothing in hand and nothing given: the best forbidden weapon sensible for them, the front line first.
            var hands = new List<(PickColonist c, PickWeapon w, float value)>();
            foreach (var c in fighters)
            {
                if (inHand.ContainsKey(c.Key) || !HandEmpty(input, c)) continue;
                foreach (var w in forbidden)
                    if (WeaponPickMath.Score(input, c, w) is float sc && sc >= Floor - 1e-4f)
                        hands.Add((c, w, c.Fights * sc));
            }
            foreach (var (c, w, _) in hands.OrderByDescending(h => h.value).ThenBy(h => h.c.Key, StringComparer.Ordinal).ThenBy(h => h.w.Key, StringComparer.Ordinal))
            {
                if (inHand.ContainsKey(c.Key) || taken.Contains(w.Key)) continue;
                taken.Add(w.Key);
                inHand[c.Key] = w;
                skip.Add((c.Key, KitSlot.Hand));
                skip.Add((c.Key, SlotOf(w.Ranged)));
                result.Add(new PickSuggestion
                {
                    Kind = PickKind.Primary, Colonist = c.Key, Weapon = w.Key, Gain = (WeaponPickMath.Score(input, c, w) ?? 0f) - WeaponPickMath.Unarmed,
                    Rating = input.Option(c, w)?.Rating ?? w.Overall, Gap = input.Rules.Sidearms ? SlotOf(w.Ranged) : KitSlot.Hand
                });
            }
            var holds = fighters.ToDictionary(c => c.Key, c => inHand.TryGetValue(c.Key, out var w) ? w : Main(input, c));
            result.AddRange(FillKinds(input, fighters, forbidden, taken, holds, skip, true));
            var keeping = fighters.Where(c => !assigned.Any(s => s.Colonist == c.Key && (s.Kind == PickKind.Primary || s.Kind == PickKind.Swap))).ToList();
            result.AddRange(FillOffHands(input, keeping, forbidden, taken, new HashSet<string>(assigned.Where(s => s.Gap == KitSlot.OffHand).Select(s => s.Colonist))));
            return result;
        }

        // ---------------------------------------------------------------- what was looked at

        /// <summary>
        /// What was looked at for an empty slot of a colonist's kit and why each was left out (the quiet line's tooltip): the
        /// weapons of its kind in storage (forbidden ones too; alike ones once), in their own pack, and other colonists'
        /// spares; the best for them first, at most <paramref name="max"/>. <paramref name="assigned"/>: the colony's choice,
        /// for a weapon it gives to someone else.
        /// </summary>
        public static List<KitLook> Looked(PickInput input, PickColonist c, KitSlot slot, List<PickSuggestion> assigned, int max = 6)
        {
            var list = new List<(KitLook look, float score)>();
            if (c == null) return new List<KitLook>();
            var main = Main(input, c);
            bool offHand = slot == KitSlot.OffHand;
            bool? kind = slot == KitSlot.Ranged ? true : slot == KitSlot.Melee ? false : offHand ? main?.Ranged : null;
            bool sidearm = !offHand && input.Rules.Sidearms && main != null;
            var givenTo = new Dictionary<string, string>();
            foreach (var s in assigned ?? new List<PickSuggestion>())
                if (s.Kind != PickKind.Swap && !givenTo.ContainsKey(s.Weapon))
                    givenTo[s.Weapon] = s.Colonist;
            var seen = new HashSet<string>();
            var cands = new List<(PickWeapon w, bool own, string owner)>();
            foreach (var w in input.Weapons.Where(x => x.Stored).OrderBy(x => x.Forbidden).ThenBy(x => x.Key, StringComparer.Ordinal)) cands.Add((w, false, null));
            if (input.Rules.Sidearms && !offHand)
            {
                foreach (var k in c.Carries)
                    if (c.Unremembered.Contains(k) && input.Weapon(k) is PickWeapon own)
                        cands.Add((own, true, null));
                foreach (var g in input.Colonists.Where(x => x.Key != c.Key).OrderBy(x => x.Key, StringComparer.Ordinal))
                    foreach (var w in Spares(input, g))
                        cands.Add((w, false, g.Key));
            }
            float alone = main != null ? input.Option(c, main)?.Rating ?? main.Overall : 0f;
            foreach (var (w, own, owner) in cands)
            {
                if (w?.Rating == null || (kind.HasValue && w.Ranged != kind.Value)) continue;
                // Alike weapons in storage once: the group, forbidden or not; anything else by itself.
                string seenKey = w.Stored ? WeaponPickMath.GroupOf(w) + (w.Forbidden ? "/forbidden" : "") : "#" + w.Key;
                if (!seen.Add(seenKey)) continue;
                var look = new KitLook { Weapon = w.Key, Own = own, Owner = owner };
                var o = input.Option(c, w);
                var score = WeaponPickMath.Score(input, c, w);
                if (w.Forbidden) look.Why = KitLeftOut.Forbidden;
                else if (o == null || o.Cant != null || !score.HasValue)
                {
                    look.Why = KitLeftOut.Cant;
                    look.Text = o?.Cant ?? "";
                }
                else if (offHand)
                {
                    if (!w.CanOffHand) look.Why = KitLeftOut.NotOffHand;
                    else if (w.TwoHanded) look.Why = KitLeftOut.TwoHanded;
                    else if (w.HandOnly) look.Why = KitLeftOut.OnStand;
                    else if (!o.Pair.HasValue || o.Pair.Value - alone < OffHandAdds - 1e-4f)
                    {
                        look.Why = KitLeftOut.AddsLittle;
                        look.Value = o.Pair.HasValue ? o.Pair.Value - alone : 0f;
                        look.Limit = OffHandAdds;
                    }
                }
                else if (sidearm && w.HandOnly) look.Why = KitLeftOut.OnStand;
                else if (sidearm && w.Ranged && w.OneUse) look.Why = KitLeftOut.OneUse;
                else if (sidearm && !FitsSidearm(input, w))
                {
                    look.Why = KitLeftOut.TooHeavy;
                    look.Value = w.Mass;
                    look.Limit = w.Ranged ? input.Rules.SidearmRangedMass : input.Rules.SidearmMeleeMass;
                }
                else if (sidearm && w.Stored && o.SidearmCant != null)
                {
                    look.Why = KitLeftOut.SidearmRefused;
                    look.Text = o.SidearmCant;
                }
                if (look.Why == KitLeftOut.Fits && score.HasValue)
                {
                    if (score.Value < Floor - 1e-4f)
                    {
                        look.Why = KitLeftOut.TooWeak;
                        look.Value = score.Value;
                        look.Limit = Floor;
                    }
                    else if (!Sensible(score.Value, w, main))
                    {
                        look.Why = KitLeftOut.NoBetterThanBash;
                        look.Value = score.Value;
                        look.Limit = Bash(main);
                    }
                }
                if (look.Why == KitLeftOut.Fits)
                {
                    // Every one of the group (in storage) given to someone else, or this very one.
                    var group = w.Stored ? input.Weapons.Where(x => x.Stored && !x.Forbidden && WeaponPickMath.GroupOf(x) == WeaponPickMath.GroupOf(w)).ToList() : new List<PickWeapon> { w };
                    var others = group.Select(x => givenTo.TryGetValue(x.Key, out var who) ? who : null).ToList();
                    if (others.All(x => x != null && x != c.Key))
                    {
                        look.Why = KitLeftOut.Given;
                        look.Text = others.First();
                    }
                }
                list.Add((look, score ?? 0f));
            }
            return list.OrderBy(x => x.look.Why == KitLeftOut.Fits ? 0 : 1).ThenByDescending(x => x.score).ThenBy(x => x.look.Weapon, StringComparer.Ordinal)
                .Take(max).Select(x => x.look).ToList();
        }
    }
}
