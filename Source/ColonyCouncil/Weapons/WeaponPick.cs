using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// The best weapon for each colonist in the game (docs/design/weapons.md, step 3, point 8): a home map's colonists and the
    /// weapons the colony has there (what they hold and carry, what lies in storage they may take: in a stockpile or on a
    /// shelf, not forbidden, and on an outfit stand), each colonist's own rating of each (kept an hour, worked out ahead a
    /// tenth of the colony at a time by <see cref="WeaponPickBook"/>), what stops them, their faith's view, Dual Wield's pair;
    /// the colony's choice made by <see cref="WeaponPickMath.Assign"/> at most once an hour (sooner when what they hold or
    /// what lies in storage changes), each suggestion's reasons from the colonist's own rating's steps; and what a button
    /// does: a real job, walked out on the map (the game's Equip, Simple Sidearms' own, Dual Wield's own, a trade between
    /// two). Nothing is worked out per frame: the drawing reads the kept choice and checks it still stands.
    /// </summary>
    public static class WeaponPick
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>"Weapon suggestions": the gear tab's line, who should carry a weapon on its card, the colony's view.</summary>
        public static bool On => WeaponLive.On && (S?.weaponSuggestions ?? true);

        /// <summary>The kinds of gun and of blade from storage the choice weighs at most, the best by their own rating.</summary>
        public const int TopKinds = 16;

        public sealed class Picks
        {
            public Map map;
            public int tick = -1, signature;
            public float made, sigAt;
            public PickInput input = new PickInput();
            public List<PickSuggestion> list = new List<PickSuggestion>();
            public readonly Dictionary<string, Thing> things = new Dictionary<string, Thing>();
            public readonly Dictionary<string, Pawn> pawns = new Dictionary<string, Pawn>();
            public readonly Dictionary<string, WeaponReport> reports = new Dictionary<string, WeaponReport>();
            public readonly Dictionary<string, string> places = new Dictionary<string, string>();
            /// <summary>The kinds the choice rates, for working colonists' ratings out ahead.</summary>
            public readonly HashSet<WeaponReport> kinds = new HashSet<WeaponReport>();
            /// <summary>Weapons lying in storage the colony could take, all kinds.</summary>
            public int stored;

            public IEnumerable<PickSuggestion> Of(Pawn p) => p == null ? Enumerable.Empty<PickSuggestion>() : list.Where(s => s.Colonist == Key(p));

            public Pawn Pawn(string key) => key != null && pawns.TryGetValue(key, out var p) ? p : null;

            public Thing Thing(string key) => key != null && things.TryGetValue(key, out var t) ? t : null;

            public WeaponReport Report(string key) => key != null && reports.TryGetValue(key, out var r) ? r : null;

            public string Place(string key) => key != null && places.TryGetValue(key, out var p) ? p : "";
        }

        private static readonly Dictionary<int, Picks> byMap = new Dictionary<int, Picks>();
        private static readonly Dictionary<(int, WeaponReport), (int tick, float rating)> ratings = new Dictionary<(int, WeaponReport), (int, float)>();
        private static readonly Dictionary<(int, WeaponReport, WeaponReport), (int tick, float? pair)> pairs = new Dictionary<(int, WeaponReport, WeaponReport), (int, float?)>();
        private static bool dirty;

        /// <summary>Forgets every choice and rating kept (a new game).</summary>
        public static void Reset()
        {
            byMap.Clear();
            ratings.Clear();
            pairs.Clear();
        }

        /// <summary>Something the choice reads changed by the player's hand (an order given, "not now"): made again at the next look.</summary>
        public static void Dirty() => dirty = true;

        public static string Key(Pawn p) => "p" + p.thingIDNumber;

        public static string Key(Thing t) => "w" + t.thingIDNumber;

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        // ---------------------------------------------------------------- who and what

        /// <summary>The colonists on a home map the choice is for: grown people of the colony, not slaves or guests on a quest.</summary>
        public static IEnumerable<Pawn> Colonists(Map map)
        {
            if (map?.mapPawns == null) yield break;
            foreach (var p in map.mapPawns.FreeColonistsSpawned)
            {
                if (p == null || p.Dead || !p.RaceProps.Humanlike || p.IsSlave || p.IsQuestLodger() || !p.DevelopmentalStage.Adult()) continue;
                if (p.equipment == null) continue;
                yield return p;
            }
        }

        /// <summary>The choice kept for a map, as it is (null before the first).</summary>
        public static Picks Current(Map map) => map != null && byMap.TryGetValue(map.uniqueID, out var p) ? p : null;

        /// <summary>The colony's choice on a home map: kept, made again once an hour, or sooner when what it reads has changed.</summary>
        public static Picks For(Map map)
        {
            if (map == null || !Homes.IsHome(map) || (!On && !WeaponAsks.On)) return null;
            var p = Current(map);
            if (p == null || Stale(p))
            {
                try
                {
                    p = Build(map);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("the best weapon for each colonist", e);
                    p = new Picks { map = map, tick = Now, made = Time.realtimeSinceStartup };
                }
                byMap[map.uniqueID] = p;
                dirty = false;
            }
            return p;
        }

        private static bool Stale(Picks p)
        {
            int now = Now;
            if (now - p.tick >= GenDate.TicksPerHour || now < p.tick) return true;
            float real = Time.realtimeSinceStartup;
            if (dirty && real - p.made > 0.5f) return true;
            if (real - p.sigAt < 1f) return false;
            p.sigAt = real;
            return Signature(p.map) != p.signature && real - p.made > 2f;
        }

        /// <summary>What the colonists hold and carry and how many weapons there are on the map, in one number: when it changes, the choice is made again.</summary>
        private static int Signature(Map map)
        {
            unchecked
            {
                int h = 17;
                foreach (var p in Colonists(map))
                {
                    h = h * 31 + p.thingIDNumber;
                    h = h * 31 + (p.equipment.Primary?.thingIDNumber ?? 0);
                    h = h * 31 + (p.inventory?.innerContainer?.Count ?? 0);
                }
                h = h * 31 + map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon).Count;
                h = h * 31 + (WeaponPickBook.Instance?.notNow.Count ?? 0);
                return h;
            }
        }

        // ---------------------------------------------------------------- the choice

        private static Picks Build(Map map)
        {
            var picks = new Picks { map = map, tick = Now, made = Time.realtimeSinceStartup };
            picks.sigAt = picks.made;
            picks.signature = Signature(map);
            var input = picks.input;
            input.Rules.Sidearms = WeaponMods.SidearmsLoaded;
            input.Rules.DualWield = WeaponMods.DualWieldLoaded;
            var book = WeaponPickBook.Instance;
            input.Colony = book?.Situations() ?? new Dictionary<string, float>();
            var colonists = Colonists(map).ToList();
            var dual = input.Rules.DualWield ? WeaponMods.DualRules() : null;

            PickWeapon Add(Thing t, WeaponReport r)
            {
                string key = Key(t);
                var w = input.Weapon(key);
                if (w != null) return w;
                w = new PickWeapon
                {
                    Key = key, Label = t.LabelNoCount, Group = GroupOf(t, r),
                    Rating = r.Rating, Mass = r.Mass, OneUse = r.Ranged?.OneUse ?? false,
                    HuntingOk = r.Rating.Kind == "ranged" && r.Ranged != null && r.Ranged.HarmsHealth && r.Ranged.ExplosionRadius <= 0f,
                    CanOffHand = WeaponMods.CanBeOffHand(t.def) ?? false, TwoHanded = WeaponMods.IsTwoHand(t.def) ?? false
                };
                input.Weapons.Add(w);
                picks.things[key] = t;
                picks.reports[key] = r;
                picks.kinds.Add(r);
                return w;
            }

            // What they hold and carry.
            foreach (var p in colonists)
            {
                var c = Colonist(p, book);
                input.Colonists.Add(c);
                picks.pawns[c.Key] = p;
                var primary = p.equipment.Primary;
                var pr = primary != null ? WeaponLive.Of(primary) : null;
                if (pr != null)
                {
                    Add(primary, pr).Holder = c.Key;
                    c.Holds = Key(primary);
                }
                foreach (var t in WeaponMods.Carried(p))
                {
                    var r = WeaponLive.Of(t);
                    if (r == null) continue;
                    Add(t, r).CarriedBy = c.Key;
                    c.Carries.Add(Key(t));
                }
                var off = WeaponMods.OffHand(p);
                if (off != null) c.OffHand = Key(off);
                if (p.equipment.bondedWeapon is ThingWithComps bonded && !bonded.Destroyed)
                {
                    c.Bonded = Key(bonded);
                    c.BondJealous = Jealous(bonded);
                }
            }
            // What lies in storage: the best kinds of each, as many of each as there are colonists.
            var stored = new List<(Thing t, WeaponReport r, bool stand)>();
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon))
            {
                if (t == null || !t.Spawned || !WeaponLive.Rateable(t.def) || t.IsForbidden(Faction.OfPlayer) || !t.IsInAnyStorage() || t.IsBurning()) continue;
                var r = WeaponLive.Of(t);
                if (r != null) stored.Add((t, r, false));
            }
            foreach (var stand in map.listerBuildings.AllBuildingsColonistOfClass<Building_OutfitStand>())
            {
                var t = stand?.HeldWeapon;
                if (t == null || !stand.Spawned || stand.IsForbidden(Faction.OfPlayer) || !WeaponLive.Rateable(t.def)) continue;
                var r = WeaponLive.Of(t);
                if (r != null) stored.Add((t, r, true));
            }
            picks.stored = stored.Count;
            int perKind = Math.Max(1, colonists.Count);
            foreach (var kind in stored.GroupBy(x => x.r.Rating.Kind))
                foreach (var g in kind.GroupBy(x => x.r).OrderByDescending(g => g.Key.Rating.Overall).Take(TopKinds))
                    foreach (var (t, r, stand) in g.Take(perKind))
                    {
                        var w = Add(t, r);
                        w.Stored = true;
                        w.HandOnly = stand;
                        picks.places[w.Key] = PlaceOf(t);
                    }
            // Each colonist with each weapon; alike weapons (one group: kind, stuff, quality, coding) are weighed once each.
            foreach (var c in input.Colonists)
            {
                var p = picks.Pawn(c.Key);
                var mainReport = picks.Report(c.Holds);
                var alike = new Dictionary<string, PickOption>();
                foreach (var w in input.Weapons)
                {
                    var t = picks.Thing(w.Key);
                    var r = picks.Report(w.Key);
                    string gk = w.Group + (w.Stored ? "/stored" : "/" + w.Key);
                    if (alike.TryGetValue(gk, out var same))
                    {
                        input.Options[(c.Key, w.Key)] = new PickOption { Cant = same.Cant, Rating = same.Rating, Noble = same.Noble, Pair = same.Pair };
                        continue;
                    }
                    var o = new PickOption { Cant = CantFor(p, t, r) };
                    if (o.Cant == null)
                    {
                        o.Rating = Rate(p, r);
                        o.Noble = p.Ideo != null && p.Ideo.GetDispositionForWeapon(t.def) == IdeoWeaponDisposition.Noble;
                        if (dual != null && w.Stored && w.CanOffHand && !w.TwoHanded && mainReport != null && mainReport.Rating.Kind == r.Rating.Kind && c.OffHand == null)
                            o.Pair = Pair(p, mainReport, r, dual);
                    }
                    input.Options[(c.Key, w.Key)] = o;
                    alike[gk] = o;
                }
            }
            // The choice; a weapon a colonist can't walk to is taken out for them, and the choice made again.
            for (int round = 0; round < 3; round++)
            {
                picks.list = WeaponPickMath.Assign(input);
                bool again = false;
                foreach (var s in picks.list)
                {
                    if (s.Kind == PickKind.Swap) continue;
                    var p = picks.Pawn(s.Colonist);
                    var t = picks.Thing(s.Weapon);
                    var target = Where(t);
                    if (p == null || t == null || !target.IsValid || p.CanReach(target, PathEndMode.ClosestTouch, Danger.Deadly)) continue;
                    input.Options[(s.Colonist, s.Weapon)].Cant = "NoPath".Translate().CapitalizeFirst();
                    again = true;
                }
                if (!again) break;
            }
            // Each suggestion's reasons, with the steps of the colonist's own rating (their skill, their traits).
            foreach (var s in picks.list)
            {
                var p = picks.Pawn(s.Colonist);
                var r = picks.Report(s.Weapon);
                var fc = p != null && r != null ? WeaponLive.ForColonist(p, r) : null;
                s.Why = WeaponPickMath.Reasons(input, s, fc?.Why);
            }
            return picks;
        }

        /// <summary>Alike weapons, one choice: one kind, stuff and quality, coded to the same one or none; one with traits of its own is its own.</summary>
        private static string GroupOf(Thing t, WeaponReport r)
        {
            if (WeaponLive.HasOwnTraits(t)) return Key(t);
            string quality = t.TryGetQuality(out var q) ? q.ToString() : "";
            var coded = (t as ThingWithComps)?.GetComp<CompBiocodable>();
            string code = coded != null && coded.Biocoded ? "/coded:" + (coded.CodedPawn?.thingIDNumber.ToString() ?? coded.CodedPawnLabel ?? "?") : "";
            return t.def.defName + "/" + (t.Stuff?.defName ?? "") + "/" + quality + code;
        }

        /// <summary>A colonist as the choice sees them: skills and passions, a brawler, their work (a hunter, a doctor, a crafter), set aside or not.</summary>
        private static PickColonist Colonist(Pawn p, WeaponPickBook book)
        {
            var shooting = p.skills?.GetSkill(SkillDefOf.Shooting);
            var melee = p.skills?.GetSkill(SkillDefOf.Melee);
            return new PickColonist
            {
                Key = Key(p), Name = p.LabelShort, CanFight = !p.WorkTagIsDisabled(WorkTags.Violent), CanShoot = !p.WorkTagIsDisabled(WorkTags.Shooting),
                Shooting = shooting?.Level ?? 0, Melee = melee?.Level ?? 0,
                ShootingPassion = shooting != null ? People.PassionLevel(shooting) : 0, MeleePassion = melee != null ? People.PassionLevel(melee) : 0,
                Brawler = p.story?.traits?.HasTrait(TraitDefOf.Brawler) ?? false, Hunter = Hunts(p), Worker = WorkerOf(p),
                NotNow = book?.IsNotNow(p) ?? false
            };
        }

        private static bool Works(Pawn p, WorkTypeDef w) =>
            w != null && p.workSettings != null && p.workSettings.EverWork && !p.WorkTypeIsDisabled(w) && p.workSettings.WorkIsActive(w);

        private static bool Hunts(Pawn p) => Works(p, WorkTypeDefOf.Hunting);

        /// <summary>A doctor or a crafter: doctoring or making things is among their work, at a skill of 8 or more and above their fighting.</summary>
        private static string WorkerOf(Pawn p)
        {
            if (p.skills == null) return null;
            int fight = Math.Max(p.skills.GetSkill(SkillDefOf.Shooting)?.Level ?? 0, p.skills.GetSkill(SkillDefOf.Melee)?.Level ?? 0);
            string best = null;
            int bestPrio = int.MaxValue;
            void Look(WorkTypeDef w, string role, SkillDef skill)
            {
                if (!Works(p, w)) return;
                int level = p.skills.GetSkill(skill)?.Level ?? 0;
                if (level < 8 || level <= fight) return;
                int prio = p.workSettings.GetPriority(w);
                if (prio > 0 && prio < bestPrio)
                {
                    bestPrio = prio;
                    best = role;
                }
            }
            Look(WorkTypeDefOf.Doctor, "doctor", SkillDefOf.Medicine);
            foreach (var name in new[] { "Crafting", "Smithing", "Tailoring" })
                Look(DefDatabase<WorkTypeDef>.GetNamedSilentFail(name), "crafter", SkillDefOf.Crafting);
            return best;
        }

        /// <summary>A persona weapon whose persona is jealous of other weapons (Royalty's jealous trait: the bonded one's mood sours when they wield another).</summary>
        private static bool Jealous(ThingWithComps t)
        {
            var traits = t.GetComp<CompBladelinkWeapon>()?.TraitsListForReading;
            return traits != null && traits.Any(x => x?.workerClass != null && typeof(WeaponTraitWorker_Jealous).IsAssignableFrom(x.workerClass));
        }

        /// <summary>
        /// Why a colonist can't or mustn't carry a weapon, or null: incapable of violence, coded or bonded to someone else, a
        /// heavy weapon they can't carry (WeaponLive.CantUse); what the game itself refuses (another persona weapon when they
        /// are bonded, a role's rule); a gun when they can't shoot; a weapon their faith despises.
        /// </summary>
        private static string CantFor(Pawn p, Thing t, WeaponReport r)
        {
            if (p == null || t == null || r == null) return "?";
            string cant = WeaponLive.CantUse(p, t, r);
            if (cant != null) return cant;
            if (!EquipmentUtility.CanEquip(t, p, out var why, true)) return why.NullOrEmpty() ? "?" : why.CapitalizeFirst();
            if (r.Rating.Kind == "ranged" && p.WorkTagIsDisabled(WorkTags.Shooting)) return "CC_WeaponPick_CantShoot".Translate(p.Named("PAWN")).Resolve();
            if (p.Ideo != null && p.Ideo.GetDispositionForWeapon(t.def) == IdeoWeaponDisposition.Despised)
                return "CC_WeaponPick_Despised".Translate(p.Named("PAWN"), p.Ideo.name.Named("IDEO")).Resolve();
            return null;
        }

        /// <summary>A colonist's own rating of a weapon (WeaponForMath: their skill, traits and body), kept an hour.</summary>
        public static float Rate(Pawn p, WeaponReport r)
        {
            int now = Now;
            var key = (p.thingIDNumber, r);
            if (ratings.TryGetValue(key, out var kept) && now - kept.tick < GenDate.TicksPerHour && now >= kept.tick) return kept.rating;
            if (ratings.Count > 6000) ratings.Clear();
            float v;
            try
            {
                v = WeaponForMath.RateFor(r, WeaponForMath.Fighter(WeaponLive.Fighter(p) ?? new FighterFacts())).Overall;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a colonist's rating of a weapon, for the suggestions", e);
                v = r.Rating.Overall;
            }
            ratings[key] = (now, v);
            return v;
        }

        /// <summary>Dual Wield: both hands together with these two weapons, its penalties in, kept an hour.</summary>
        private static float? Pair(Pawn p, WeaponReport main, WeaponReport off, WeaponForMath.DualWieldRules dual)
        {
            int now = Now;
            var key = (p.thingIDNumber, main, off);
            if (pairs.TryGetValue(key, out var kept) && now - kept.tick < GenDate.TicksPerHour && now >= kept.tick) return kept.pair;
            if (pairs.Count > 2000) pairs.Clear();
            float? v = null;
            try
            {
                var f = WeaponForMath.Fighter(WeaponLive.Fighter(p) ?? new FighterFacts());
                var m = WeaponForMath.RateInHand(main, f, dual, false, Skill(p, main));
                var o = WeaponForMath.RateInHand(off, f, dual, true, Skill(p, off));
                v = WeaponForMath.PairTogether(m, o);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a pair in both hands, for the suggestions", e);
            }
            pairs[key] = (now, v);
            return v;
        }

        private static int Skill(Pawn p, WeaponReport r) =>
            (r.Rating.Kind == "ranged" ? p.skills?.GetSkill(SkillDefOf.Shooting)?.Level : p.skills?.GetSkill(SkillDefOf.Melee)?.Level) ?? 8;

        /// <summary>A colonist's ratings of the kinds the colony's choice weighs, worked out ahead (the pass's slice), so the choice reads kept ones.</summary>
        public static void Warm(Picks picks, Pawn p)
        {
            if (picks == null || p == null || p.WorkTagIsDisabled(WorkTags.Violent)) return;
            foreach (var r in picks.kinds) Rate(p, r);
        }

        // ---------------------------------------------------------------- where it is

        /// <summary>Where a weapon is to be fetched from: the weapon, or the outfit stand holding it.</summary>
        public static LocalTargetInfo Where(Thing t)
        {
            if (t == null || t.Destroyed) return LocalTargetInfo.Invalid;
            if (t.Spawned) return t;
            if (t.ParentHolder is Building_OutfitStand stand && stand.Spawned) return stand;
            return LocalTargetInfo.Invalid;
        }

        /// <summary>Where it lies, in words that match the map: "in the stockpile", "on the shelf", "on the outfit stand".</summary>
        public static string PlaceOf(Thing t)
        {
            try
            {
                if (t?.ParentHolder is Building_OutfitStand stand) return "CC_WeaponPick_On".Translate(stand.def.label.Named("LABEL")).Resolve();
                var slot = t != null && t.Spawned ? t.Position.GetSlotGroup(t.Map) : null;
                if (slot?.parent is Building b)
                {
                    string label = b.def.label ?? "";
                    bool on = label.Contains("shelf") || label.Contains("rack") || label.Contains("pallet") || label.Contains("table") || label.Contains("stand");
                    return (on ? "CC_WeaponPick_On" : "CC_WeaponPick_In").Translate(label.Named("LABEL")).Resolve();
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("where a weapon lies, for the suggestions", e);
            }
            return "CC_WeaponPick_InStockpile".Translate().Resolve();
        }

        /// <summary>
        /// Whether a suggestion still stands as the map is now: the colonist here and up, a weapon from storage still lying
        /// there (not forbidden, not taken), not already theirs; a trade's two still holding what they held.
        /// </summary>
        public static bool Stands(Picks picks, PickSuggestion s)
        {
            if (picks == null || s == null) return false;
            var p = picks.Pawn(s.Colonist);
            var t = picks.Thing(s.Weapon);
            if (p == null || t == null || p.Dead || p.Destroyed || !p.Spawned || p.Map != picks.map || t.Destroyed) return false;
            if (s.Kind == PickKind.Swap)
            {
                var other = picks.Pawn(s.Other);
                var mine = picks.Thing(s.Instead);
                return other != null && other.Spawned && !other.Dead && other.Map == picks.map && other.equipment?.Primary == t && p.equipment.Primary == mine;
            }
            if (p.equipment.Primary == t || WeaponMods.OffHand(p) == t) return false;
            if (s.Kind != PickKind.Primary && p.inventory?.innerContainer != null && p.inventory.innerContainer.Contains(t)) return false;
            var w = picks.input.Weapon(s.Weapon);
            if (w == null) return false;
            if (w.Stored)
            {
                if (t.ParentHolder is Building_OutfitStand stand) return stand.Spawned && stand.Map == picks.map && !stand.IsForbidden(Faction.OfPlayer);
                return t.Spawned && t.Map == picks.map && !t.IsForbidden(Faction.OfPlayer) && t.IsInAnyStorage();
            }
            // One of their own sidearms, to take in hand.
            return w.CarriedBy == s.Colonist && p.inventory?.innerContainer != null && p.inventory.innerContainer.Contains(t);
        }

        /// <summary>A colonist's suggestions that still stand, the weapon in hand first.</summary>
        public static List<PickSuggestion> Standing(Pawn p)
        {
            if (p == null || !p.Spawned || !On) return new List<PickSuggestion>();
            var picks = For(p.Map);
            return picks == null ? new List<PickSuggestion>() : picks.Of(p).Where(s => Stands(picks, s)).OrderBy(s => s.Kind == PickKind.Swap ? 0 : (int)s.Kind).ToList();
        }

        // ---------------------------------------------------------------- words

        private static string Gain(float g) => WeaponPickMath.GainText(g);

        private static string F1(float v) => WeaponForMath.RatingText(v);

        /// <summary>The weapon's label as the line names it.</summary>
        public static string Label(Picks picks, string key) => picks.Thing(key)?.LabelNoCount ?? picks.input.Weapon(key)?.Label ?? "?";

        /// <summary>The head of a suggestion: "Better for Ana: charge rifle (+1.4)", "A sidearm for Ana: knife (+1.1)", a trade.</summary>
        public static string Head(Picks picks, PickSuggestion s)
        {
            var p = picks.Pawn(s.Colonist);
            string weapon = Label(picks, s.Weapon);
            switch (s.Kind)
            {
                case PickKind.Sidearm:
                    return "CC_WeaponPick_Head_Sidearm".Translate(p.Named("PAWN"), weapon.Named("WEAPON"), Gain(s.Gain).Named("GAIN")).Resolve();
                case PickKind.OffHand:
                    return "CC_WeaponPick_Head_OffHand".Translate(p.Named("PAWN"), weapon.Named("WEAPON"), Gain(s.Gain).Named("GAIN")).Resolve();
                case PickKind.Swap:
                    var other = picks.Pawn(s.Other);
                    return "CC_WeaponPick_Head_Swap".Translate(p.Named("PAWN"), other.Named("OTHER"), weapon.Named("WEAPON"), Gain(s.Gain).Named("GAIN")).Resolve();
                default:
                    return "CC_WeaponPick_Head_Primary".Translate(p.Named("PAWN"), weapon.Named("WEAPON"), Gain(s.Gain).Named("GAIN")).Resolve();
            }
        }

        /// <summary>One reason or cost in words.</summary>
        public static string Words(Picks picks, PickSuggestion s, PickReason r)
        {
            var p = picks.Pawn(s.Colonist);
            var w = picks.input.Weapon(s.Weapon);
            bool ranged = w?.Ranged ?? true;
            switch (r.Kind)
            {
                case PickWhy.SkillHigh:
                    return ("CC_WeaponPick_Why_SkillHigh_" + (ranged ? "Ranged" : "Melee")).Translate(p.Named("PAWN"), r.Level.Named("LEVEL")).Resolve();
                case PickWhy.SkillLow:
                    return ("CC_WeaponPick_Why_SkillLow_" + (ranged ? "Ranged" : "Melee")).Translate(p.Named("PAWN"), r.Level.Named("LEVEL")).Resolve();
                case PickWhy.Trait:
                    return "CC_WeaponPick_Why_Trait".Translate(p.Named("PAWN"), r.Text.Named("TRAIT"), WeaponForMath.Delta(r.Value).Named("DELTA")).Resolve();
                case PickWhy.Situation:
                    return ("CC_WeaponPick_Why_" + SituationWord(r.Text)).Translate(p.Named("PAWN")).Resolve();
                case PickWhy.MechsEmp:
                    return "CC_WeaponPick_Why_MechsEmp".Translate(p.Named("PAWN")).Resolve();
                case PickWhy.ArmourPierce:
                    return "CC_WeaponPick_Why_ArmourPierce".Translate(p.Named("PAWN")).Resolve();
                case PickWhy.Hunting:
                    return "CC_WeaponPick_Why_Hunting".Translate(p.Named("PAWN")).Resolve();
                case PickWhy.Passion:
                    return "CC_WeaponPick_Why_Passion".Translate(p.Named("PAWN"), (ranged ? SkillDefOf.Shooting : SkillDefOf.Melee).LabelCap.Named("SKILL")).Resolve();
                case PickWhy.Noble:
                    return "CC_WeaponPick_Why_Noble".Translate(p.Named("PAWN")).Resolve();
                case PickWhy.Worker:
                    return ("CC_WeaponPick_Why_Worker_" + (r.Text == "doctor" ? "Doctor" : "Crafter")).Translate(p.Named("PAWN")).Resolve();
                case PickWhy.Special:
                    return "CC_WeaponPick_Why_Special".Translate(r.Text.Named("SPECIAL")).Resolve();
                case PickWhy.Unarmed:
                    return "CC_WeaponPick_Why_Unarmed".Translate(p.Named("PAWN")).Resolve();
                case PickWhy.Sidearm:
                    return "CC_WeaponPick_Why_Sidearm".Translate(p.Named("PAWN"), (r.Text.NullOrEmpty() ? "?" : r.Text).Named("HELD")).Resolve();
                case PickWhy.OffHand:
                    return "CC_WeaponPick_Why_OffHand".Translate(F1(r.Value).Named("RATING")).Resolve();
                case PickWhy.Swap:
                    var other = picks.Pawn(s.Other);
                    return "CC_WeaponPick_Why_Swap".Translate(other.Named("OTHER"), Gain(r.Value).Named("GAIN")).Resolve();
                case PickWhy.Bonded:
                    return "CC_WeaponPick_Cost_Bonded".Translate(p.Named("PAWN"), r.Text.Named("BONDED")).Resolve();
                case PickWhy.Jealous:
                    return "CC_WeaponPick_Cost_Jealous".Translate(p.Named("PAWN")).Resolve();
                case PickWhy.NoHunting:
                    return "CC_WeaponPick_Cost_NoHunting".Translate(p.Named("PAWN")).Resolve();
                default:
                    return "CC_WeaponPick_Why_Better".Translate(p.Named("PAWN"), F1(s.Rating).Named("RATING"), (s.Before > 0f ? F1(s.Before) : "CC_WeaponPick_Nothing".Translate().Resolve()).Named("BEFORE")).Resolve();
            }
        }

        /// <summary>A situation's key in the colony's fights as the keys name it.</summary>
        public static string SituationWord(string key)
        {
            switch (key)
            {
                case "mechanoids": return "Mechs";
                case "heavily armoured": return "Armour";
                case "crowds": return "Crowds";
                case "siege or long duel": return "Siege";
                case "holding a choke": return "Choke";
                default: return "Better";
            }
        }

        /// <summary>The reasons in one line: "her Shooting 14 makes its accuracy count, and it pierces the armour your last raids wore".</summary>
        public static string Why(Picks picks, PickSuggestion s)
        {
            var parts = s.Reasons.Select(r => Words(picks, s, r)).Where(x => !x.NullOrEmpty()).ToList();
            string why = parts.Count == 0 ? "" : parts.Count == 1 ? parts[0] : "CC_WeaponPick_And".Translate(string.Join(", ", parts.Take(parts.Count - 1)).Named("A"), parts.Last().Named("B")).Resolve();
            return why;
        }

        /// <summary>What it costs, as a sentence ("Though her bonded zeusblade would go unused."), or empty.</summary>
        public static string Costs(Picks picks, PickSuggestion s)
        {
            var parts = s.Costs.Select(r => Words(picks, s, r)).Where(x => !x.NullOrEmpty()).ToList();
            if (parts.Count == 0) return "";
            string joined = parts.Count == 1 ? parts[0] : "CC_WeaponPick_And".Translate(string.Join(", ", parts.Take(parts.Count - 1)).Named("A"), parts.Last().Named("B")).Resolve();
            return "CC_WeaponPick_Costs".Translate(joined.Named("COST")).Resolve();
        }

        /// <summary>The whole line: "Charge rifle: +1.4 for Ana: her Shooting 14 makes its accuracy count, and ..." and what it costs.</summary>
        public static string Full(Picks picks, PickSuggestion s)
        {
            var p = picks.Pawn(s.Colonist);
            string line = "CC_WeaponPick_Full".Translate(Label(picks, s.Weapon).CapitalizeFirst().Named("WEAPON"), Gain(s.Gain).Named("GAIN"), p.Named("PAWN"),
                Why(picks, s).Named("WHY")).Resolve();
            string costs = Costs(picks, s);
            return costs.NullOrEmpty() ? line : line + " " + costs;
        }

        // ---------------------------------------------------------------- acting on it

        private static JobDef sidearmJob, offHandJob, swapJob;
        private static bool jobsLooked;

        private static void LookJobs()
        {
            if (jobsLooked) return;
            jobsLooked = true;
            if (WeaponMods.SidearmsLoaded) sidearmJob = DefDatabase<JobDef>.GetNamedSilentFail("EquipSecondary");
            if (WeaponMods.DualWieldLoaded) offHandJob = DefDatabase<JobDef>.GetNamedSilentFail("DW_EquipOffhand");
            swapJob = DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_SwapWeapons");
        }

        /// <summary>Simple Sidearms' own job to take a weapon as a sidearm, when it is loaded (else no such button).</summary>
        public static bool CanTakeSidearms
        {
            get
            {
                LookJobs();
                return sidearmJob != null;
            }
        }

        private static readonly MethodInfo CanPickupSidearm = GenTypes.GetTypeInAnyAssembly("PeteTimesSix.SimpleSidearms.Utilities.StatCalculator") is Type sc
            ? AccessTools.Method(sc, "CanPickupSidearmInstance") : null;

        private static readonly MethodInfo EquipFromInventory = GenTypes.GetTypeInAnyAssembly("PeteTimesSix.SimpleSidearms.Utilities.WeaponAssingment") is Type wa
            ? AccessTools.Method(wa, "equipSpecificWeaponFromInventory") : null;

        /// <summary>Simple Sidearms' own check: whether this colonist may pick this weapon up as a sidearm now (its weights and counts), with its reason.</summary>
        private static string SidearmWhyNot(Thing t, Pawn p)
        {
            if (CanPickupSidearm == null || !(t is ThingWithComps tc)) return null;
            try
            {
                var args = new object[] { tc, p, null };
                if (CanPickupSidearm.Invoke(null, args) is bool ok && !ok) return (args[2] as string).NullOrEmpty() ? "?" : ((string)args[2]).CapitalizeFirst();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("asking Simple Sidearms whether a sidearm fits", e);
            }
            return null;
        }

        /// <summary>A colonist is on their way to it already (the job a button gave them, under way).</summary>
        public static bool OnTheWay(Picks picks, PickSuggestion s)
        {
            var p = picks?.Pawn(s.Colonist);
            var job = p?.CurJob;
            if (job == null) return false;
            var t = picks.Thing(s.Weapon);
            if (s.Kind == PickKind.Swap) return job.def == swapJob && job.targetB.Thing == t;
            return (job.def == JobDefOf.Equip || job.def == sidearmJob || job.def == offHandJob) && job.targetA.Thing == t;
        }

        /// <summary>
        /// Why a button can't be used now, or null: the suggestion no longer stands, the colonist is down or not themselves,
        /// can't use their hands, can't walk there, it burns or someone else has it reserved; Simple Sidearms' own refusal
        /// for a sidearm; for a trade, the other not able to.
        /// </summary>
        public static string WhyNot(Picks picks, PickSuggestion s, bool asSidearm = false)
        {
            LookJobs();
            if (!Stands(picks, s)) return "CC_WeaponPick_Gone".Translate().Resolve();
            var p = picks.Pawn(s.Colonist);
            var t = picks.Thing(s.Weapon);
            if (p.Downed || p.InMentalState) return "CC_WeaponPick_Busy".Translate(p.Named("PAWN")).Resolve();
            if (!p.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)) return "Incapable".Translate().CapitalizeFirst();
            if (s.Kind == PickKind.Swap)
            {
                if (swapJob == null) return "?";
                var other = picks.Pawn(s.Other);
                if (other.Downed || other.InMentalState || other.Drafted || !other.Awake()) return "CC_WeaponPick_Busy".Translate(other.Named("PAWN")).Resolve();
                if (!p.CanReach(other, PathEndMode.Touch, Danger.Deadly)) return "NoPath".Translate().CapitalizeFirst();
                return null;
            }
            var w = picks.input.Weapon(s.Weapon);
            if (w.Stored)
            {
                var where = Where(t);
                if (!p.CanReach(where, PathEndMode.ClosestTouch, Danger.Deadly)) return "NoPath".Translate().CapitalizeFirst();
                if (t.Spawned && t.IsBurning()) return "BurningLower".Translate().CapitalizeFirst();
                if (t.Spawned && !p.CanReserve(t)) return "CC_WeaponPick_Reserved".Translate().Resolve();
            }
            bool sidearm = s.Kind == PickKind.Sidearm || asSidearm;
            if (sidearm)
            {
                if (sidearmJob == null) return "CC_WeaponPick_NoSidearms".Translate().Resolve();
                if (!w.Stored || w.HandOnly) return "CC_WeaponPick_FromGround".Translate().Resolve();
                return SidearmWhyNot(t, p);
            }
            if (s.Kind == PickKind.OffHand && offHandJob == null) return "?";
            return null;
        }

        /// <summary>A persona weapon that would bond: the game's own warning, to be confirmed first (none for anything else).</summary>
        public static string Confirm(Picks picks, PickSuggestion s)
        {
            var t = picks.Thing(s.Weapon);
            var p = picks.Pawn(s.Colonist);
            return t == null || p == null ? null : EquipmentUtility.GetPersonaWeaponConfirmationText(t, p);
        }

        /// <summary>
        /// Gives the order: the colonist walks to the weapon and takes it (the game's Equip; Simple Sidearms' own job as a
        /// sidearm; Dual Wield's own for the off hand), takes a sidearm they carry in hand (Simple Sidearms' own switch), or
        /// walks to the other to trade. <paramref name="queue"/>: after what they were already told to do (equip all).
        /// </summary>
        public static bool Do(Picks picks, PickSuggestion s, bool asSidearm = false, bool queue = false)
        {
            if (WhyNot(picks, s, asSidearm) != null) return false;
            var p = picks.Pawn(s.Colonist);
            var t = picks.Thing(s.Weapon);
            var w = picks.input.Weapon(s.Weapon);
            Job job = null;
            try
            {
                if (s.Kind == PickKind.Swap) job = JobMaker.MakeJob(swapJob, picks.Pawn(s.Other), t, picks.Thing(s.Instead));
                else if (s.Kind == PickKind.Sidearm || asSidearm) job = JobMaker.MakeJob(sidearmJob, t);
                else if (s.Kind == PickKind.OffHand) job = JobMaker.MakeJob(offHandJob, t);
                else if (!w.Stored)
                {
                    // One of their own sidearms, taken in hand: Simple Sidearms' own switch, else put down and picked up.
                    if (EquipFromInventory != null && t is ThingWithComps tc)
                    {
                        EquipFromInventory.Invoke(null, new object[] { p, tc, false, false });
                        Dirty();
                        return true;
                    }
                    if (!p.inventory.innerContainer.TryDrop(t, p.Position, p.Map, ThingPlaceMode.Near, out var dropped) || dropped == null) return false;
                    t = dropped;
                    job = JobMaker.MakeJob(JobDefOf.Equip, t);
                }
                else job = JobMaker.MakeJob(JobDefOf.Equip, t);
                if (t.Spawned) t.SetForbidden(false, false);
                bool given = p.jobs.TryTakeOrderedJob(job, JobTag.Misc, queue);
                if (given && Where(t).IsValid) FleckMaker.Static(Where(t).Cell.ToVector3Shifted(), p.Map, FleckDefOf.FeedbackEquip);
                Dirty();
                return given;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("giving the order for a suggested weapon", e);
                return false;
            }
        }

        /// <summary>Do, after the game's own warning for a persona weapon that would bond, when there is one.</summary>
        public static void DoConfirmed(Picks picks, PickSuggestion s, bool asSidearm = false)
        {
            string confirm = Confirm(picks, s);
            if (confirm.NullOrEmpty()) Do(picks, s, asSidearm);
            else Find.WindowStack.Add(new Dialog_MessageBox(confirm, "Yes".Translate(), () => Do(picks, s, asSidearm), "No".Translate()));
        }

        /// <summary>
        /// "Equip all suggested": every suggestion that stands and can be done now is ordered, each colonist's after what they
        /// were already told (never one that would bond a persona weapon: that one asks first, from its own button). Only
        /// orders; nothing is done by itself later. Returns how many were ordered.
        /// </summary>
        public static int DoAll(Picks picks)
        {
            if (picks == null) return 0;
            int n = 0;
            var told = new HashSet<string>();
            foreach (var s in picks.list.OrderBy(s => (int)s.Kind))
            {
                if (!Stands(picks, s) || OnTheWay(picks, s) || WhyNot(picks, s) != null || !Confirm(picks, s).NullOrEmpty()) continue;
                // A trade is given to one of the two, once.
                if (s.Kind == PickKind.Swap && told.Contains(s.Other)) continue;
                if (Do(picks, s, false, told.Contains(s.Colonist)))
                {
                    told.Add(s.Colonist);
                    n++;
                }
            }
            return n;
        }

        /// <summary>The colonists who can carry a weapon best (its card, "Who should carry this"), from the choice on the map in view.</summary>
        public static List<(Pawn p, float rating, float gain)> WhoShouldCarry(Thing thing, ThingDef def, ThingDef stuff, int max = 3)
        {
            var list = new List<(Pawn, float, float)>();
            var map = Find.CurrentMap;
            var picks = On ? For(map) : null;
            if (picks == null) return list;
            var input = picks.input;
            PickWeapon w = thing != null ? input.Weapon(Key(thing)) : null;
            WeaponReport r = thing != null ? WeaponLive.Of(thing) : WeaponLive.Of(def, stuff);
            if (r == null) return list;
            bool added = false;
            if (w == null)
            {
                // Not one the colony's choice weighs (elsewhere, a trader's, a kind from the research tab): weighed here, not kept.
                w = new PickWeapon { Key = "card", Label = r.Label, Rating = r.Rating, Mass = r.Mass, OneUse = r.Ranged?.OneUse ?? false,
                    HuntingOk = r.Rating.Kind == "ranged" && r.Ranged != null && r.Ranged.HarmsHealth && r.Ranged.ExplosionRadius <= 0f };
                input.Weapons.Add(w);
                added = true;
                foreach (var c in input.Colonists)
                {
                    var p = picks.Pawn(c.Key);
                    var o = new PickOption { Cant = thing != null ? CantFor(p, thing, r) : CantForKind(p, def, r) };
                    if (o.Cant == null)
                    {
                        o.Rating = Rate(p, r);
                        o.Noble = p.Ideo != null && p.Ideo.GetDispositionForWeapon(def ?? thing.def) == IdeoWeaponDisposition.Noble;
                    }
                    input.Options[(c.Key, w.Key)] = o;
                }
            }
            try
            {
                foreach (var (c, score, gain) in WeaponPickMath.WhoShouldCarry(input, w, max))
                {
                    var p = picks.Pawn(c.Key);
                    if (p != null) list.Add((p, input.Option(c, w)?.Rating ?? score, gain));
                }
            }
            finally
            {
                if (added)
                {
                    input.Weapons.Remove(w);
                    foreach (var c in input.Colonists) input.Options.Remove((c.Key, w.Key));
                }
            }
            return list;
        }

        /// <summary>What stops a colonist from carrying a weapon's kind (no weapon in hand to read its coding).</summary>
        private static string CantForKind(Pawn p, ThingDef def, WeaponReport r)
        {
            if (p.WorkTagIsDisabled(WorkTags.Violent)) return "CC_Weapon_CantFight".Translate(p.Named("PAWN")).Resolve();
            if (WeaponLive.Heavy(r) && WeaponLive.HeavyCarriedBy(p, r) == null) return "CC_Weapon_TooHeavyFor".Translate(p.Named("PAWN")).Resolve();
            if (r.Rating.Kind == "ranged" && p.WorkTagIsDisabled(WorkTags.Shooting)) return "CC_WeaponPick_CantShoot".Translate(p.Named("PAWN")).Resolve();
            if (p.Ideo != null && def != null && p.Ideo.GetDispositionForWeapon(def) == IdeoWeaponDisposition.Despised)
                return "CC_WeaponPick_Despised".Translate(p.Named("PAWN"), p.Ideo.name.Named("IDEO")).Resolve();
            return null;
        }
    }
}
