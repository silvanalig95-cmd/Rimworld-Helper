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
    /// <para>
    /// A trade is proposed the same way (Rimfolk_Pool_WeaponTrade), to the other once the two are together
    /// (<see cref="Together.When"/>): the one who gains asks for the other's weapon, the one who gives up some offers theirs,
    /// two who both gain propose it; both weapons still in their hands as the words say. Nobody gives up their own keepsake
    /// (a lucky weapon, an heirloom) of their own accord; the other may still propose the trade, its line warning of it.
    /// </para>
    /// <para>
    /// An empty slot of their kit (WeaponKitMath) is asked for under the same waits and chance, whatever its gain, tagged
    /// "kit" ("I've nothing for close work, can I take the knife in the stockpile?"): only when they don't fill it themselves
    /// (WeaponKitSelf off, or it is something they can't take on their own: a forbidden weapon, one on an outfit stand for a
    /// sidearm, one in place of what they hold, a persona weapon that would bond). A forbidden one's inbox row allows it.
    /// </para>
    /// </summary>
    public static class WeaponAsks
    {
        public const string Pool = "Rimfolk_Pool_WeaponAsk", TradePool = "Rimfolk_Pool_WeaponTrade";

        /// <summary>How long a proposed trade waits for its two to be together before it is let go.</summary>
        public const float TradeWaitHours = 4f;

        public static bool On => WeaponLive.On && (ColonyCouncilMod.Settings?.weaponAsks ?? true);

        /// <summary>Safe at home: up, awake, able to talk (unless <paramref name="talk"/> is false), not drafted or in a mental state, no threat on the map or near them, in the home area.</summary>
        public static bool Safe(Pawn p, bool talk = true)
        {
            if (p == null || !p.Spawned || p.Dead || p.Downed || p.Drafted || p.InMentalState || !p.Awake()) return false;
            var map = p.Map;
            if (!Homes.IsHome(map) || !map.areaManager.Home[p.Position]) return false;
            if (talk && (p.health?.capacities == null || !p.health.capacities.CapableOf(PawnCapacityDefOf.Talking))) return false;
            return p.mindState?.enemyTarget == null && !GenHostility.AnyHostileActiveThreatToPlayer(map) && !PawnUtility.EnemiesAreNearby(p, 9, true);
        }

        /// <summary>In their free time at home and safe: <see cref="Safe"/>, and at recreation or idle.</summary>
        public static bool FreeAndSafe(Pawn p, bool talk = true)
        {
            if (!Safe(p, talk)) return false;
            bool joyHour = p.timetable?.CurrentAssignment == TimeAssignmentDefOf.Joy;
            bool joyJob = p.CurJob?.def?.joyKind != null;
            bool idle = p.mindState != null && p.mindState.IsIdle;
            return joyHour || joyJob || idle;
        }

        /// <summary>The hourly look for one colonist (the pass's slice): a clearly better weapon lying in storage, the waits over, the chance; then their line. Else a trade to propose.</summary>
        public static void Look(Pawn p, WeaponPick.Picks picks, int now)
        {
            if (!On || picks == null || !FreeAndSafe(p)) return;
            var book = WeaponPickBook.Instance;
            if (book == null) return;
            string me = WeaponPick.Key(p);
            // An empty slot first (one they don't fill themselves, or a forbidden weapon for it), then a clearly better weapon.
            var s = picks.Of(p).Where(x => WeaponAskMath.Askable(x) && picks.input.Weapon(x.Weapon)?.Stored == true && WeaponPick.Stands(picks, x)
                                           && WeaponAskMath.MaySpeakFor(x, WeaponPickMath.GivesUpKeepsake(x, me)) && (!x.Gap.HasValue || !WeaponKitSelf.On || !WeaponKitSelf.CanFetch(picks, x, p)))
                .Concat(picks.kitAsks.Where(x => x.Colonist == me && StandsForbidden(picks, x)))
                .OrderBy(x => x.Gap.HasValue ? 0 : 1).ThenByDescending(x => x.Gain).FirstOrDefault();
            if (s == null)
            {
                ProposeTrade(p, picks, now, book);
                return;
            }
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

        /// <summary>
        /// A forbidden weapon that would fill an empty slot (WeaponKitMath.ForbiddenFills) still lies in storage and the slot is
        /// still empty: the colonist here and up, the weapon there, still forbidden (allowed, the colony's choice takes it up).
        /// </summary>
        public static bool StandsForbidden(WeaponPick.Picks picks, PickSuggestion s)
        {
            var p = picks?.Pawn(s?.Colonist);
            var t = picks?.Thing(s?.Weapon);
            if (p == null || t == null || p.Dead || !p.Spawned || p.Map != picks.map || t.Destroyed) return false;
            if (s.Kind == PickKind.Primary && p.equipment?.Primary != null) return false;
            var where = t.ParentHolder is Building_OutfitStand stand ? (Thing)stand : t;
            return where.Spawned && where.Map == picks.map && where.IsForbidden(Faction.OfPlayer) && (t.ParentHolder is Building_OutfitStand || t.IsInAnyStorage());
        }

        /// <summary>
        /// Their line: tagged for why (mechanoids, armoured, crowds, siege, choke, skill, trait, hunting, passion, noble, worker,
        /// special, sidearm, offhand, better; kit for an empty slot) and the weapon's kind (an empty off hand: offhand); {WEAPON}
        /// its label, {PLACE} where it lies.
        /// </summary>
        public static string Line(Pawn p, PickSuggestion s, Thing t, string place)
        {
            var pool = DefDatabase<FlavorPoolDef>.GetNamedSilentFail(Pool);
            if (pool?.lines == null) return null;
            var tags = Flavor.TagsFor(p);
            string why = WeaponPickMath.AskTag(s);
            tags.Add(why);
            tags.Add(why == "kit" && s.Gap == KitSlot.OffHand ? "offhand" : t.def.IsRangedWeapon ? "ranged" : "melee");
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

        // ---------------- Trades they propose ----------------

        /// <summary>
        /// A trade the colonist proposes: their best trade that stands in the colony's choice where one of the two gains clearly
        /// (WeaponAskMath.TradeAskable), the asks' own waits (theirs, the colony's, the same weapon's, neither set aside) and
        /// chance; then said to the other once the two are together, up to <see cref="TradeWaitHours"/> later.
        /// </summary>
        private static void ProposeTrade(Pawn p, WeaponPick.Picks picks, int now, WeaponPickBook book)
        {
            string me = WeaponPick.Key(p);
            var s = picks.Of(p).Where(x => WeaponAskMath.TradeAskable(x) && WeaponPick.Stands(picks, x) && WeaponAskMath.MaySpeakFor(x, WeaponPickMath.GivesUpKeepsake(x, me)))
                .OrderByDescending(x => x.Worth).FirstOrDefault();
            if (s == null) return;
            var other = picks.Pawn(s.Other);
            Thing theirs = picks.Thing(s.Weapon), mine = picks.Thing(s.Instead);
            if (other == null || theirs == null || mine == null || book.IsNotNow(other)) return;
            var last = book.AskOf(p);
            if (!WeaponAskMath.MayAsk(now, last?.tick ?? -1, book.lastAsk, last?.weapon ?? -1, theirs.thingIDNumber, book.NotNowUntil(p))) return;
            if (!Rand.Chance(WeaponAskMath.Chance)) return;
            var said = s;
            Together.When("weapon-trade:" + p.thingIDNumber + ":" + other.thingIDNumber, p, other, () => SayTrade(p, other, said, mine, theirs), TradeWaitHours);
        }

        /// <summary>The trade said, once the two are together: both safe, both still holding what the words name; then kept for the inbox.</summary>
        private static void SayTrade(Pawn p, Pawn other, PickSuggestion s, Thing mine, Thing theirs)
        {
            var book = WeaponPickBook.Instance;
            if (book == null || !On || !Safe(p) || !Together.Can(p, other)) return;
            if (p.equipment?.Primary != mine || other.equipment?.Primary != theirs || mine.Destroyed || theirs.Destroyed) return;
            string line = TradeLine(p, other, s, mine, theirs);
            if (line.NullOrEmpty()) return;
            Speech.Say(p, line, other);
            book.Asked(p, theirs, "", s.Gain, other);
        }

        /// <summary>
        /// The words of a trade: tagged for the speaker's side ("taker" asks for the other's weapon, "giver" offers theirs,
        /// "even" proposes it), the kind of the weapon the line speaks of (the one they want, or the one they give), "lowskill"
        /// when a giver's own skill with it is 5 or less, "rarely" when a giver rarely fights; {OTHER} the other, {MINE} and
        /// {THEIRS} the two weapons by kind.
        /// </summary>
        public static string TradeLine(Pawn p, Pawn other, PickSuggestion s, Thing mine, Thing theirs)
        {
            var pool = DefDatabase<FlavorPoolDef>.GetNamedSilentFail(TradePool);
            if (pool?.lines == null) return null;
            var tags = Flavor.TagsFor(p);
            string role = WeaponAskMath.TradeRole(s);
            tags.Add(role);
            var spoken = role == "taker" ? theirs : mine;
            bool ranged = spoken.def.IsRangedWeapon;
            tags.Add(ranged ? "ranged" : "melee");
            if (role == "giver")
            {
                int skill = (ranged ? p.skills?.GetSkill(SkillDefOf.Shooting)?.Level : p.skills?.GetSkill(SkillDefOf.Melee)?.Level) ?? 0;
                if (skill <= 5) tags.Add("lowskill");
                var c = WeaponPick.Current(p.Map)?.input.Colonist(WeaponPick.Key(p));
                if (c != null && c.Fights <= WeaponPickMath.RarelyFightsAt + 1e-4f) tags.Add("rarely");
            }
            if (!Flavor.AnyFits(pool.lines.AllLines(), tags)) return null;
            string raw = pool.lines.Pick(ToneUtility.ToneFor(p), tags, p);
            if (raw.NullOrEmpty()) return null;
            return raw.Formatted(p.Named("PAWN"), other.Named("OTHER"), mine.def.label.Named("MINE"), theirs.def.label.Named("THEIRS")).Resolve().CapitalizeFirst();
        }

        // ---------------- The inbox ----------------

        /// <summary>
        /// How people are: a quiet row for each colonist who asked lately (three days) while their suggestion still stands and
        /// isn't set aside: "Ana would rather carry the charge rifle in the stockpile (+1.4)", its reasons under it, a click
        /// opening their gear tab, its act the colony's view on the Defenses page; a trade they proposed: "Ana proposed a trade
        /// to Bautista: her revolver for his charge rifle (+1.6)", its act the trade's own order. Counted nowhere: it waits on
        /// no one.
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
                bool trade = a.other >= 0;
                var s = picks?.Of(p).FirstOrDefault(x => picks.Thing(x.Weapon)?.thingIDNumber == a.weapon && (x.Kind == PickKind.Swap) == trade
                                                         && (!trade || picks.Pawn(x.Other)?.thingIDNumber == a.other));
                // A forbidden weapon asked for to fill an empty slot: its row allows it.
                var forbidden = s == null && !trade ? picks?.kitAsks.FirstOrDefault(x => x.Colonist == WeaponPick.Key(p) && picks.Thing(x.Weapon)?.thingIDNumber == a.weapon) : null;
                if (forbidden != null && StandsForbidden(picks, forbidden))
                {
                    var t = picks.Thing(forbidden.Weapon);
                    rows.Add(new WeekRow
                    {
                        pawn = p, sub = WeaponPick.Why(picks, forbidden).CapitalizeFirst(),
                        text = "CC_WeaponAsk_InboxForbidden".Translate(WeekModel.Name(p).Named("PAWN"), WeaponPick.Label(picks, forbidden.Weapon).Named("WEAPON"),
                            picks.Place(forbidden.Weapon).Named("PLACE")).Resolve(),
                        tip = WeekModel.Tip(WeaponPick.Head(picks, forbidden), "CC_WeaponAsk_InboxClick".Translate(p.Named("PAWN")).Resolve()),
                        click = WeekModel.Inspect(p, typeof(ITab_Pawn_Gear)), waits = false, order = -a.tick,
                        act = "CC_WeaponAsk_InboxAllow".Translate(), actTip = "CC_WeaponAsk_InboxAllow_Tip".Translate(t.LabelNoCount.Named("WEAPON"), p.Named("PAWN")).Resolve(),
                        actRun = () =>
                        {
                            var where = t.ParentHolder is Building_OutfitStand stand ? (Thing)stand : t;
                            if (where.Spawned) where.SetForbidden(false, false);
                            WeaponPick.Dirty();
                            WeaponPickGearTab.Refresh();
                        }
                    });
                    continue;
                }
                if (s == null || !WeaponPick.Stands(picks, s)) continue;
                var who = p;
                var row = new WeekRow
                {
                    pawn = p,
                    sub = WeaponPick.Why(picks, s).CapitalizeFirst(),
                    tip = WeekModel.Tip(WeaponPick.Full(picks, s), "CC_WeaponAsk_InboxClick".Translate(p.Named("PAWN")).Resolve()),
                    click = WeekModel.Inspect(p, typeof(ITab_Pawn_Gear)),
                    waits = false,
                    order = -a.tick
                };
                if (trade)
                {
                    var other = picks.Pawn(s.Other);
                    row.text = "CC_WeaponAsk_InboxTrade".Translate(WeekModel.Name(p).Named("PAWN"), other.Named("OTHER"), WeaponPick.Label(picks, s.Instead).Named("MINE"),
                        WeaponPick.Label(picks, s.Weapon).Named("THEIRS"), WeaponPickMath.GainText(s.Gain).Named("GAIN")).Resolve();
                    var picksNow = picks;
                    var trading = s;
                    row.act = "CC_WeaponPick_Act_Swap".Translate();
                    row.actTip = "CC_WeaponPick_Act_Swap_Tip".Translate(p.Named("PAWN")).Resolve();
                    row.actRun = () =>
                    {
                        WeaponPick.DoConfirmed(picksNow, trading);
                        WeaponPickGearTab.Refresh();
                    };
                }
                else
                {
                    row.text = "CC_WeaponAsk_Inbox".Translate(WeekModel.Name(p).Named("PAWN"), WeaponPick.Label(picks, s.Weapon).Named("WEAPON"), picks.Place(s.Weapon).Named("PLACE"),
                        WeaponPickMath.GainText(s.Gain).Named("GAIN")).Resolve();
                    row.act = "CC_WeaponAsk_InboxAct".Translate();
                    row.actTip = "CC_WeaponAsk_InboxAct_Tip".Translate();
                    row.actRun = () => WeaponPickPanel.Show(who.Map);
                }
                rows.Add(row);
            }
        }
    }
}
