using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    // A hamlet's life (docs/design/stage1-region.md, item 6, "Its life", "Fealty"): its fortune as any town's; at harvest
    // a carter brings produce to the gate; in a hard season it asks for food, a doctor or tools, and refused twice in one
    // the tie is broken; after 60 days at 40 or more with no ask refused in 60 days it asks to swear fealty (Faction
    // Territories' vassal with its vassalage on, else the tie sworn); its people turned on the colony before that, its
    // families choose; its second year's end begins the town that grows from it (World/Region/GrownTown.cs).
    public static partial class Hamlets
    {
        /// <summary>The hamlet's day.</summary>
        private static void Life(Map home, HamletHome h, int now)
        {
            var t = TownOf(h);
            var rec = h.town >= 0 ? WorldLedger.TownRecord(h.town) : null;
            if (t == null || t.Faction != h.people || h.people == null || h.people.defeated)
            {
                End(home, h, rec != null && rec.kind == TownKind.Ruin ? HamletEnd.Failed : HamletEnd.Gone, null);
                return;
            }
            if (rec != null && rec.kind != TownKind.Hamlet && !h.grown)
            {
                // Grown into a town (World/Region/GrownTown.cs) after its second year: the ledger keeps it as one from now on.
                if (h.secondYear && rec.kind == TownKind.Town) h.grown = true;
                else
                {
                    rec.kind = TownKind.Hamlet;
                    WorldLedger.TownChanged();
                }
            }
            if (t.HasName) h.name = t.Name;
            // Off: no new life for it; it stands as any town of theirs, under the colony's protection still.
            if (!On) return;
            if (h.people.HostileTo(Faction.OfPlayer))
            {
                if (!h.hostileAsked)
                {
                    // The question's day is kept where the oath's would be: no oath is asked while their people are hostile.
                    h.hostileAsked = true;
                    h.oathAt = now;
                    HamletAsk.AskHostile(home, h);
                }
                // Never answered: its families stay with their people.
                else if (now - h.oathAt >= (long)HamletMath.AnswerDays * Day) End(home, h, HamletEnd.Stayed, null);
                return;
            }
            h.hostileAsked = false;
            float p = rec != null && rec.Seeded ? rec.prosperity : -1f;
            h.highSince = HamletMath.HighSince(p, h.highSince, now);
            if (!h.secondYear && HamletMath.SecondYearOver(h.founded, now)) SecondYear(home, h, t);
            // The region's hard year near it (a plague, a famine, a fire) or its good one (a bumper harvest): B11.
            var hard = RegionYear(h, false, out var hardWhere);
            if (HamletMath.HarvestDue(HarvestTime(home), h.harvestAt, h.founded, now, p)) Harvest(home, h, t, p, now, RegionYear(h, true, out _) != null);
            else if (Tell.CanAsk && HamletMath.HelpDue(p, h.helpAt, h.founded, now, hard != null)) AskHelp(home, h, now, hard, hardWhere);
            else if (h.stage == HamletStage.Standing && Tell.CanAsk && HamletMath.FealtyDue(h.highSince, HamletMath.LastRefused(h.refused), h.oathAt, now))
            {
                h.oathAt = now;
                HamletAsk.AskOath(home, h);
            }
        }

        // ---------------- Its second year ----------------

        /// <summary>
        /// Its second year is over: the town that grows from it, 60 to 180 days from now (World/Region/GrownTown.cs), with its
        /// founder and what the colony did in its first years (food sent, other help, help refused).
        /// </summary>
        private static void SecondYear(Map home, HamletHome h, Settlement t)
        {
            h.secondYear = true;
            var flags = new List<string>();
            if ((h.firstYear & 1) != 0) flags.Add("fed");
            if ((h.firstYear & 2) != 0) flags.Add("gift");
            if ((h.firstYear & 4) != 0) flags.Add("refused");
            var leader = h.leader != null && !h.leader.Dead && !h.leader.Discarded ? h.leader : null;
            Safe("its second year", () => GrownTowns.Note(home, t, leader, flags, HamletWords.TaleOf(h), "hamlet"));
        }

        // ---------------- The harvest ----------------

        /// <summary>The colony's harvest time: autumn at the home, or Septober where the seasons never turn.</summary>
        private static bool HarvestTime(Map home)
        {
            var s = GenLocalDate.Season(home);
            if (s == Season.Fall) return true;
            if (s != Season.PermanentSummer && s != Season.PermanentWinter) return false;
            return GenDate.Quadrum(Find.TickManager.TicksAbs, Find.WorldGrid.LongLatOf(home.Tile).x) == Quadrum.Septober;
        }

        /// <summary>
        /// A carter brings the first of the hamlet's harvest to the colony's gate: carried in by their own people, or left there;
        /// half again as much in a bumper year near.
        /// </summary>
        private static void Harvest(Map home, HamletHome h, Settlement t, float prosperity, int now, bool bumper)
        {
            h.harvestAt = now;
            var def = new[] { "RawPotatoes", "RawCorn", "RawRice" }.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).FirstOrDefault(d => d != null);
            if (def == null) return;
            // Sworn, the council sets how much it brings (Diplomacy/Vassals.cs: light, usual, heavy).
            int n = Vassals.HamletProduce(home, h, HamletMath.Harvest(HamletMath.Produce(prosperity), bumper));
            var gate = PlaySpots.GateCell(home, null);
            var things = Couriers.Make(def, n);
            bool carried = Couriers.Deliver(home, things, h.people, gate);
            if (!carried) Loot.Deliver(things, home, gate, false);
            try
            {
                h.people.TryAffectGoodwillWith(Faction.OfPlayer, HamletMath.HarvestGoodwill, true, false, GoodwillWhy.Harvest);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("hamlets: the harvest's goodwill", e);
            }
            FactionMemory.Note(h.people, HamletMath.DeedHarvest, t.Name);
            HistoryOf(HamletMath.KindHarvest, home, h, HamletWords.HistoryHarvest(h, n, def));
            HamletWords.TellHarvest(home, h, n, def, carried, gate, bumper);
        }

        // ---------------- A hard season ----------------

        /// <summary>
        /// A hard season: the hamlet asks for food, a doctor or tools; with the region's hard year near it, for what that year
        /// brings (a doctor in a plague, food in a famine, tools after a fire), and its letter names it.
        /// </summary>
        private static void AskHelp(Map home, HamletHome h, int now, WorldSituation hard, string where)
        {
            var need = HamletMath.NeedFor(HamletMath.RegionNeed(hard?.def?.defName), Rand.Value, h.lastNeed);
            h.lastNeed = (int)need;
            h.helpAt = now;
            h.hardEvent = hard?.def?.defName;
            h.hardWhere = hard != null ? where ?? "" : null;
            h.wordUntil = -1;
            HamletAsk.AskHelp(home, h, need);
        }

        // ---------------- The region's years (docs/content-update-proposals.md, B11) ----------------

        /// <summary>
        /// The nearest running world event within reach of the hamlet (HamletMath.RegionTiles from its town; one with no town
        /// of its own by its people's nearest town) that brings a need (<paramref name="good"/> false), or a bumper harvest
        /// (true); null for none. <paramref name="where"/>: the place's name, empty when it is at the hamlet itself or has none.
        /// </summary>
        internal static WorldSituation RegionYear(HamletHome h, bool good, out string where)
        {
            where = null;
            var ws = WorldSituations.Instance;
            var at = TileOf(h);
            if (ws == null || !at.Valid || !(ColonyCouncilMod.Settings?.worldSituations ?? true)) return null;
            WorldSituation best = null;
            float bestTiles = float.MaxValue;
            foreach (var s in ws.Running)
            {
                string def = s?.def?.defName;
                if (def == null || (good ? def != HamletMath.Bumper : HamletMath.RegionNeed(def) == null)) continue;
                float tiles = TilesTo(at, s, out var town);
                if (!HamletMath.Reaches(tiles) || tiles >= bestTiles) continue;
                best = s;
                bestTiles = tiles;
                where = town != null && town.ID == h.town ? "" : s.ctx?.placeName ?? "";
            }
            return best;
        }

        /// <summary>Tiles from the hamlet to where a world event is: its town, its tile, or its people's nearest town; -1 for none.</summary>
        private static float TilesTo(PlanetTile at, WorldSituation s, out Settlement town)
        {
            town = Refs.Town(s.place);
            var there = town != null ? town.Tile : s.place.kind == RefKind.Tile ? RiseFall.TileOf(s.place) : PlanetTile.Invalid;
            if (there.Valid) return there.Layer == at.Layer ? Find.WorldGrid.ApproxDistanceInTiles(at, there) : -1f;
            float best = -1f;
            var people = s.ctx?.faction;
            if (people == null) return best;
            foreach (var o in Find.WorldObjects.Settlements)
            {
                if (o == null || o.Destroyed || o.Faction != people || o.Tile.Layer != at.Layer) continue;
                float d = Find.WorldGrid.ApproxDistanceInTiles(at, o.Tile);
                if (best < 0f || d < best)
                {
                    best = d;
                    town = o;
                }
            }
            return best;
        }

        /// <summary>Any hamlet whose carter has a word to say at the gate (the region's hour has something to do).</summary>
        internal static bool AnyWord()
        {
            int now = Now;
            foreach (var (_, h) in All())
                if (h.wordUntil > now)
                    return true;
            return false;
        }

        /// <summary>
        /// The region's hour, while help sent in the region's hard year waits at the gate: their courier there, come for it,
        /// says what is happening at home, once ("The fever from Ashford has reached our lower farms.").
        /// </summary>
        internal static void CarterWord(int now)
        {
            foreach (var (home, h) in All())
            {
                if (h.wordUntil <= now || h.people == null || h.hardEvent.NullOrEmpty()) continue;
                var need = HamletMath.RegionNeed(h.hardEvent);
                if (need == null)
                {
                    h.wordUntil = -1;
                    continue;
                }
                var carter = CourierTracker.Instance?.trips?.Where(t => t != null && !t.settled && t.map == home && t.faction == h.people && t.state == CourierState.Working)
                    .SelectMany(t => t.people).FirstOrDefault(p => p != null && p.Spawned && p.Map == home && !p.Dead && !p.Downed);
                if (carter == null) continue;
                h.wordUntil = -1;
                string word = HamletWords.CarterWord(h, need.Value);
                Speech.Say(carter, word);
            }
        }

        /// <summary>What the colony carries out for a need, by its own means: raw food, medicine, and tools as its age has them (timber, iron, components).</summary>
        internal static (string what, int count) Help(HamletNeed need)
        {
            switch (need)
            {
                case HamletNeed.Food: return ("rawfood", 30);
                case HamletNeed.Doctor: return ("medicine", 5);
                default:
                    switch (Ages.Ours)
                    {
                        case WorldAge.Tribal: return ("wood", 40);
                        case WorldAge.Medieval: return ("Steel", 25);
                        default: return ("ComponentIndustrial", 4);
                    }
            }
        }

        /// <summary>Help sent: carried out to their carter at the gate; their fortune lifted, their people remember it.</summary>
        internal static void Helped(Map home, HamletHome h, HamletNeed need)
        {
            if (h == null || !HamletMath.Stands(h.stage)) return;
            var (what, count) = Help(need);
            var ctx = new DilemmaContext { map = home, faction = h.people };
            if (need == HamletNeed.Doctor) ctx.a = VarietyUtil.Best(home, SkillDefOf.Medicine);
            string label = HamletWords.HelpLabel(h, need);
            // apart: one of the colony carries the goods out to the gate, where their carter waits to take them up the road.
            new DilemmaEffect_CarryOut { what = what, count = count, steward = false, who = need == HamletNeed.Doctor && ctx.a != null ? PlayWho.A : PlayWho.None, to = PlaySpot.Gate, label = label }
                .Apply(ctx);
            h.firstYear |= need == HamletNeed.Food ? 1 : 2;
            // In the region's hard year, their courier who comes to the gate for it says what is happening at home.
            if (!h.hardEvent.NullOrEmpty() && HamletMath.RegionNeed(h.hardEvent) == need) h.wordUntil = Now + HamletMath.WordDays * Day;
            var rec = WorldLedger.TownRecord(h.town);
            if (rec != null && rec.Seeded && RegionBook.FortunesOn)
            {
                int now = Now;
                rec.prosperity = RegionMath.Clamp(rec.prosperity + HamletMath.HelpLift);
                rec.movedAt = now;
                rec.cause = "CC_Hamlet_Cause_Helped".Translate().Resolve();
                rec.causeDir = 1;
                rec.causeAt = now;
                rec.lowSince = RegionMath.LowSince(rec.prosperity, rec.lowSince, now);
                WorldLedger.TownChanged();
            }
            FactionMemory.Note(h.people, HamletMath.DeedHelped, h.name);
            HistoryOf(HamletMath.KindHelped, home, h, HamletWords.HistoryHelped(h, need), ctx.a);
        }

        /// <summary>Why the colony can't send it now (too little in the stores), or null.</summary>
        internal static string CantHelp(Map home, HamletNeed need)
        {
            if (home == null) return "";
            var (what, count) = Help(need);
            var def = PlayCarry.DefOf(what, null);
            int have = PlayCarry.Have(home, what, def);
            return have < count ? "CC_Dilemma_NotEnough".Translate(count, PlayCarry.Label(what, def)).ToString() : null;
        }

        /// <summary>Help refused: their people remember; twice in a season and the tie breaks.</summary>
        internal static void Refused(Map home, HamletHome h, HamletNeed need)
        {
            if (h == null || !HamletMath.Stands(h.stage)) return;
            int now = Now;
            HamletMath.NoteRefused(h.refused, now);
            h.firstYear |= 4;
            FactionMemory.Note(h.people, HamletMath.DeedRefused, h.name);
            HistoryOf(HamletMath.KindRefused, home, h, HamletWords.HistoryRefused(h, need));
            if (HamletMath.Broken(h.refused, now)) End(home, h, HamletEnd.Broken, null);
            else HamletWords.TellRefused(home, h);
        }

        // ---------------- Fealty ----------------

        /// <summary>
        /// They swear fealty: with Faction Territories and its vassalage on, its own vassalisation (no goodwill price: they
        /// asked), and the mod keeps the hamlet as the colony's vassal from here; else the tie, sworn.
        /// </summary>
        internal static void Swear(Map home, HamletHome h)
        {
            if (h == null || h.stage != HamletStage.Standing) return;
            var t = TownOf(h);
            if (t == null) return;
            h.oathAt = Now;
            string name = t.Name;
            if (Vassalise(t))
            {
                h.name = name;
                End(home, h, HamletEnd.Vassal, null);
                return;
            }
            h.stage = HamletStage.Sworn;
            var tie = TieOf(h);
            if (tie != null && tie.Live)
            {
                tie.terms.key = "oath";
                WorldLedger.Touch(tie);
            }
            HistoryOf(HamletMath.KindSworn, home, h, HamletWords.HistorySworn(h, home));
            HamletWords.TellSworn(home, h, t);
        }

        /// <summary>Faction Territories' own vassalisation of the town, by name and guarded: false without the mod, with its vassalage off, or when it fails.</summary>
        internal static bool Vassalise(Settlement t)
        {
            if (t == null || !Territories.Active) return false;
            try
            {
                var type = GenTypes.GetTypeInAnyAssembly(Territories.VassaliseUtility);
                if (type == null) return false;
                var on = AccessTools.Method(type, "IsVassalageEnabled", Type.EmptyTypes);
                if (on != null && on.ReturnType == typeof(bool) && !(bool)on.Invoke(null, null)) return false;
                var run = AccessTools.Method(type, "ExecuteVassalisation", new[] { typeof(Settlement) });
                if (run == null || run.ReturnType != typeof(bool)) return false;
                return (bool)run.Invoke(null, new object[] { t });
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("hamlets: Faction Territories' vassal", e);
                return false;
            }
        }

        // ---------------- Their people turned on the colony ----------------

        /// <summary>Its families come to the colony: they walk in and join it, and the hamlet is given up when it can be (a ruin), else a few stay on.</summary>
        internal static void Join(Map home, HamletHome h)
        {
            if (h == null || !HamletMath.Stands(h.stage)) return;
            var t = TownOf(h);
            var joined = new List<Pawn>();
            var kind = Arrivals.PersonKind(h.people) ?? Arrivals.JoinerKind();
            var houses = h.town >= 0 ? WorldLedger.HousesOf(h.town) : new List<LedgerHouse>();
            int n = Rand.RangeInclusive(2, 4);
            for (int i = 0; i < n; i++)
            {
                try
                {
                    var house = houses.Count > 0 ? houses[i % houses.Count] : null;
                    string last = house != null && !house.kin ? house.name : null;
                    var p = Arrivals.Make(kind, h.people, new FloatRange(18f, 55f), null, last, false, joined.FirstOrDefault());
                    if (p != null && Arrivals.Join(p, home)) joined.Add(p);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("hamlets: its families come", e);
                }
            }
            bool ruin = false;
            if (t != null && TradingPosts.SafeToClose(t)) ruin = RiseFall.MakeRuin(t, WorldLedger.TownRecord(t.ID), false) != null;
            string extra = HamletWords.Joined(h, joined, ruin);
            End(home, h, HamletEnd.Joined, extra);
        }
    }
}
