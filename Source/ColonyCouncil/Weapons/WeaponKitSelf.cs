using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Colonists fill an empty slot of their kit themselves ("Colonists fill an empty weapon slot themselves", on; the
    /// player's words, 9 November 2026: "the pawns should always have a ranged and melee weapon ... and suggest/equip them
    /// accordingly"). At their hourly look (WeaponPickBook's clock, a tenth of the colony each turn), a colonist in their
    /// free time at home and safe (WeaponAsks.FreeAndSafe: not drafted, downed or in a mental state, no threat near) whose
    /// kit has an empty slot the colony's choice fills from storage goes and takes it, by a real job: the game's Equip into
    /// an empty hand, Simple Sidearms' own EquipSecondary as a sidearm, Dual Wield's own DW_EquipOffhand into the off hand;
    /// a weapon already in their pack is made a sidearm at once. Only empty slots: never in place of a weapon they hold.
    /// Never a forbidden weapon (the choice leaves those out; the colonist may ask), never one that would bond a persona
    /// weapon (the game's warning is the player's), never one their faith despises or the game refuses them. At most once a
    /// day each. As they pick it up, one line through Speech (Rimfolk_Pool_WeaponKitTake, every age). The game's apparel
    /// policy covers apparel only, so it has no say over weapons. With the setting off the slot is only suggested, and
    /// asked for.
    /// </summary>
    public static class WeaponKitSelf
    {
        public const string Pool = "Rimfolk_Pool_WeaponKitTake";

        /// <summary>At most once in this many ticks each (a day).</summary>
        public const int EachTicks = 60000;

        public static bool On => WeaponLive.On && (ColonyCouncilMod.Settings?.weaponKitSelf ?? true);

        /// <summary>
        /// Whether a colonist can fill this slot themselves: an empty slot of their kit from storage (into an empty hand only,
        /// as a sidearm, into the off hand) or a weapon in their own pack, that nothing stops now (WeaponPick.WhyNot: a path,
        /// burning, reserved, Simple Sidearms' refusal, an outfit stand for a sidearm), and not a persona weapon that would bond.
        /// </summary>
        public static bool CanFetch(WeaponPick.Picks picks, PickSuggestion s, Pawn p)
        {
            if (picks == null || s?.Gap == null || p == null) return false;
            switch (s.Kind)
            {
                case PickKind.Keep:
                    break;
                case PickKind.Primary:
                    if (p.equipment?.Primary != null) return false;
                    goto case PickKind.Sidearm;
                case PickKind.Sidearm:
                case PickKind.OffHand:
                    if (picks.input.Weapon(s.Weapon)?.Stored != true || WeaponPick.FetchJob(picks, s) == null) return false;
                    break;
                default:
                    return false;
            }
            return WeaponPick.WhyNot(picks, s) == null && WeaponPick.Confirm(picks, s).NullOrEmpty();
        }

        /// <summary>
        /// The hourly look for one colonist (the pass's slice): in their free time, safe, not today already, an empty slot they
        /// can fill themselves (the hand first, then a kind, then the off hand); off they go. True when they set off.
        /// </summary>
        public static bool Look(Pawn p, WeaponPick.Picks picks, int now)
        {
            if (!On || picks == null || p == null || !WeaponAsks.FreeAndSafe(p, false)) return false;
            var book = WeaponPickBook.Instance;
            if (book == null || book.IsNotNow(p) || !book.MayFetch(p, now)) return false;
            var s = picks.Of(p).Where(x => x.Gap.HasValue && WeaponPick.Stands(picks, x) && CanFetch(picks, x, p))
                .OrderBy(x => x.Kind == PickKind.Primary ? 0 : x.Gap == KitSlot.OffHand ? 2 : 1).ThenByDescending(x => x.Gain).FirstOrDefault();
            if (s == null) return false;
            var t = picks.Thing(s.Weapon);
            try
            {
                if (s.Kind == PickKind.Keep)
                {
                    // Already on them: Simple Sidearms is told it is one of their sidearms.
                    if (!WeaponMods.Remember(p, t)) return false;
                    book.Fetched(p, now);
                    Say(p, s, t);
                    WeaponPick.Dirty();
                    return true;
                }
                var job = WeaponPick.FetchJob(picks, s);
                if (job == null) return false;
                p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                if (p.CurJob != job) return false;
                book.Fetched(p, now);
                var said = s;
                // The line as they pick it up: when the job is done, the weapon in their hands or pack.
                p.jobs.curDriver?.AddFinishAction(c =>
                {
                    WeaponPick.Dirty();
                    if (c == JobCondition.Succeeded) Say(p, said, t);
                });
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a colonist filling an empty weapon slot themselves", e);
                return false;
            }
        }

        /// <summary>
        /// Their line as they take it (Rimfolk_Pool_WeaponKitTake): tagged for how it is carried (hand, sidearm, offhand,
        /// keep: one already in their pack) and the weapon's kind (ranged, melee); {WEAPON} its label. Only a line for how it is
        /// carried is said; none, nothing said.
        /// </summary>
        public static void Say(Pawn p, PickSuggestion s, Thing t)
        {
            if (p == null || t == null || !p.Spawned || p.health?.capacities == null || !p.health.capacities.CapableOf(PawnCapacityDefOf.Talking)) return;
            string line = Line(p, s, t);
            if (!line.NullOrEmpty()) Speech.Say(p, line);
        }

        public static string Line(Pawn p, PickSuggestion s, Thing t)
        {
            var pool = DefDatabase<FlavorPoolDef>.GetNamedSilentFail(Pool);
            if (pool?.lines == null) return null;
            var tags = Flavor.TagsFor(p);
            string how = Carried(s.Kind);
            tags.Add(how);
            tags.Add(t.def.IsRangedWeapon ? "ranged" : "melee");
            if (!Flavor.AnyFits(pool.lines.AllLines(), tags)) return null;
            string raw = pool.lines.Pick(ToneUtility.ToneFor(p), tags, p);
            if (raw.NullOrEmpty()) return null;
            return raw.Formatted(p.Named("PAWN"), t.LabelNoCount.Named("WEAPON")).Resolve().CapitalizeFirst();
        }

        /// <summary>How a weapon is carried, as the lines are tagged: hand, sidearm, offhand, keep.</summary>
        public static string Carried(PickKind kind) => kind == PickKind.Primary ? "hand" : kind == PickKind.OffHand ? "offhand" : kind == PickKind.Keep ? "keep" : "sidearm";
    }
}
