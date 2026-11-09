using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Colonists sometimes ask for a better weapon (docs/design/weapons.md, point 9; "Colonists ask for better weapons"): when
    /// the colony's choice gives one a clearly better weapon lying in storage (twice the gear tab's gain), they now and then
    /// say so in one sentence in their own voice (Rimfolk_Pool_WeaponAsk: the weapon by name, where it lies, and why, by
    /// the line's tag; every age, every tone), in their free time at home, never in danger, drafted, downed or not
    /// themselves; at most once in five days each and once a day in the colony, the same weapon not again for fifteen days,
    /// never while the player has set their suggestions aside. The weapon is checked to lie where the line says at that
    /// moment. A quiet entry stays in the inbox (How people are) while the suggestion stands, opening their gear tab.
    /// </summary>
    public static class WeaponAsks
    {
        public const string Pool = "Rimfolk_Pool_WeaponAsk";

        public static bool On => WeaponLive.On && (ColonyCouncilMod.Settings?.weaponAsks ?? true);

        /// <summary>In their free time at home and safe: up, awake, able to talk, not drafted or in a mental state, no threat on the map or near them, in the home area, at recreation or idle.</summary>
        public static bool FreeAndSafe(Pawn p)
        {
            if (p == null || !p.Spawned || p.Dead || p.Downed || p.Drafted || p.InMentalState || !p.Awake()) return false;
            var map = p.Map;
            if (!Homes.IsHome(map) || !map.areaManager.Home[p.Position]) return false;
            if (p.health?.capacities == null || !p.health.capacities.CapableOf(PawnCapacityDefOf.Talking)) return false;
            if (p.mindState?.enemyTarget != null || GenHostility.AnyHostileActiveThreatToPlayer(map) || PawnUtility.EnemiesAreNearby(p, 9, true)) return false;
            bool joyHour = p.timetable?.CurrentAssignment == TimeAssignmentDefOf.Joy;
            bool joyJob = p.CurJob?.def?.joyKind != null;
            bool idle = p.mindState != null && p.mindState.IsIdle;
            return joyHour || joyJob || idle;
        }

        /// <summary>The hourly look for one colonist (the pass's slice): a clearly better weapon lying in storage, the waits over, the chance; then their line.</summary>
        public static void Look(Pawn p, WeaponPick.Picks picks, int now)
        {
            if (!On || picks == null || !FreeAndSafe(p)) return;
            var book = WeaponPickBook.Instance;
            if (book == null) return;
            var s = picks.Of(p).Where(x => WeaponAskMath.Askable(x) && picks.input.Weapon(x.Weapon)?.Stored == true && WeaponPick.Stands(picks, x))
                .OrderByDescending(x => x.Gain).FirstOrDefault();
            if (s == null) return;
            var t = picks.Thing(s.Weapon);
            var last = book.AskOf(p);
            if (!WeaponAskMath.MayAsk(now, last?.tick ?? -1, book.lastAsk, last?.weapon ?? -1, t.thingIDNumber, book.NotNowUntil(p))) return;
            if (!Rand.Chance(WeaponAskMath.Chance)) return;
            // Where it lies now, as the line says it.
            string place = WeaponPick.PlaceOf(t);
            string line = Line(p, s, t, place);
            if (line.NullOrEmpty()) return;
            Speech.Say(p, line);
            book.Asked(p, t, place, s.Gain);
        }

        /// <summary>Their line: tagged for why (mechanoids, armoured, crowds, siege, choke, skill, trait, hunting, passion, noble, worker, special, sidearm, offhand, better) and the weapon's kind; {WEAPON} its label, {PLACE} where it lies.</summary>
        public static string Line(Pawn p, PickSuggestion s, Thing t, string place)
        {
            var pool = DefDatabase<FlavorPoolDef>.GetNamedSilentFail(Pool);
            if (pool?.lines == null) return null;
            var tags = Flavor.TagsFor(p);
            string why = WeaponPickMath.AskTag(s);
            tags.Add(why);
            tags.Add(t.def.IsRangedWeapon ? "ranged" : "melee");
            // Only a line for this reason (its words must be true); else one that only says it is better.
            if (!Flavor.AnyFits(pool.lines.AllLines(), tags))
            {
                tags.Remove(why);
                tags.Add("better");
                if (!Flavor.AnyFits(pool.lines.AllLines(), tags)) return null;
            }
            string raw = pool.lines.Pick(ToneUtility.ToneFor(p), tags, p);
            if (raw.NullOrEmpty()) return null;
            return raw.Formatted(p.Named("PAWN"), t.LabelNoCount.Named("WEAPON"), place.Named("PLACE")).Resolve().CapitalizeFirst();
        }

        // ---------------- The inbox ----------------

        /// <summary>
        /// How people are: a quiet row for each colonist who asked lately (three days) while their suggestion still stands and
        /// isn't set aside: "Ana would rather carry the charge rifle in the stockpile (+1.4)", its reasons under it, a click
        /// opening their gear tab, its act the colony's view on the Defenses page. Counted nowhere: it waits on no one.
        /// </summary>
        public static void InboxRows(List<WeekRow> rows)
        {
            var book = WeaponPickBook.Instance;
            if (book == null || !On || book.asks.Count == 0) return;
            int now = Find.TickManager.TicksGame;
            foreach (var a in book.asks)
            {
                if (!WeaponAskMath.Shows(now, a.tick, 0)) continue;
                var p = WeaponPick.Colonists(Find.CurrentMap).FirstOrDefault(x => x.thingIDNumber == a.pawn)
                        ?? Homes.All.SelectMany(WeaponPick.Colonists).FirstOrDefault(x => x.thingIDNumber == a.pawn);
                if (p == null || book.IsNotNow(p)) continue;
                var picks = WeaponPick.For(p.Map);
                var s = picks?.Of(p).FirstOrDefault(x => picks.Thing(x.Weapon)?.thingIDNumber == a.weapon && x.Kind != PickKind.Swap);
                if (s == null || !WeaponPick.Stands(picks, s)) continue;
                var who = p;
                rows.Add(new WeekRow
                {
                    pawn = p,
                    text = "CC_WeaponAsk_Inbox".Translate(WeekModel.Name(p).Named("PAWN"), WeaponPick.Label(picks, s.Weapon).Named("WEAPON"), picks.Place(s.Weapon).Named("PLACE"),
                        WeaponPickMath.GainText(s.Gain).Named("GAIN")).Resolve(),
                    sub = WeaponPick.Why(picks, s).CapitalizeFirst(),
                    tip = WeekModel.Tip(WeaponPick.Full(picks, s), "CC_WeaponAsk_InboxClick".Translate(p.Named("PAWN")).Resolve()),
                    click = WeekModel.Inspect(p, typeof(ITab_Pawn_Gear)),
                    act = "CC_WeaponAsk_InboxAct".Translate(),
                    actTip = "CC_WeaponAsk_InboxAct_Tip".Translate(),
                    actRun = () => WeaponPickPanel.Show(who.Map),
                    waits = false,
                    order = -a.tick
                });
            }
        }
    }
}
