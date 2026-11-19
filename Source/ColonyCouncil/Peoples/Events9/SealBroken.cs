using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    // ===================== 91. The seal broken (the Sunken Vault) =====================

    /// <summary>
    /// The seal broken (item 91): insects (the game's hive insects and Vanilla Factions Expanded - Insectoids 2's) or mechs have got into the
    /// Sunken Vault's lower level, and a surface-runner brings the overseer's call for the colony's fighters. Answered, a caravan of two or
    /// more of the colony's grown people sets out for the vault's town within five days (the call waits while it is on the road); there
    /// the fight is the game's own, on the caravan's battle map beside the town (the calls to arms' shape), against what came up at the
    /// home's share of threat points, with a few of the vault's security beside the colony's people. Won (nothing of what came up left on its
    /// feet), the colony gets a share of the vault's spacer stores set down by its people there, the vault +15, and remembers its town
    /// defended. Or the colony sends components and medicine for the vault's security instead. Kept in the round's book (SealRecord).
    /// </summary>
    public static class SealBroken
    {
        public const string EventDef = "Rimfolk_Evt_SealBroken";

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>The hive's insects that may come up (Insectoids 2's of the same weight among the game's own), the strongest left out.</summary>
        public static readonly string[] InsectKinds =
        {
            "Megascarab", "Spelopede", "Megaspider", "VFEI2_Megapede", "VFEI2_Hellbeetle", "VFEI2_Megawasp", "VFEI2_Acidspitter", "VFEI2_Venomite", "VFEI2_RoyalSpelopede",
            "VFEI2_RoyalMegascarab"
        };

        /// <summary>The vault's town nearest this home, within reach.</summary>
        public static Settlement Town(Faction f, Map home)
        {
            if (f == null || home == null) return null;
            Settlement best = null;
            float bestD = PeopleEvents9Math.SealTiles;
            foreach (var s in Find.WorldObjects.Settlements)
            {
                if (s == null || s.Destroyed || s.Faction != f || s.Tile.Layer != home.Tile.Layer) continue;
                float d = Find.WorldGrid.ApproxDistanceInTiles(s.Tile, home.Tile);
                if (d > bestD) continue;
                best = s;
                bestD = d;
            }
            return best;
        }

        /// <summary>The town the call came from (kept on the event's context by its world id, the note).</summary>
        public static Settlement TownOf(HappeningContext ctx) =>
            int.TryParse(ctx?.note ?? "", out int id) ? Find.WorldObjects.Settlements.FirstOrDefault(s => s != null && s.ID == id && !s.Destroyed) : null;

        public static SealRecord Open(Faction f) => PeopleEvents9Book.Instance?.seals.FirstOrDefault(r => r.Open && (f == null || r.faction == f));

        /// <summary>What came up: insects at their chance when there are any, else mechs.</summary>
        public static bool Insects(float roll) => Faction.OfInsects != null && InsectKinds.Any(n => DefDatabase<PawnKindDef>.GetNamedSilentFail(n) != null)
                                                  && (Faction.OfMechanoids == null || roll < PeopleEvents9Math.InsectChance);

        /// <summary>The colony answered: the call kept until a caravan comes or five days are out; the caravan's window opened.</summary>
        public static void Answer(HappeningContext ctx)
        {
            var town = TownOf(ctx) ?? Town(ctx?.faction, ctx?.map);
            var b = PeopleEvents9Book.Instance;
            if (b == null || town == null || ctx.faction == null) return;
            b.AddSeal(new SealRecord { faction = ctx.faction, town = town, home = ctx.map, insects = ctx.variant > 0, due = Now + PeopleEvents9Math.SealDays * GenDate.TicksPerDay });
            if (ctx.map != null && !Find.WindowStack.IsOpen<Dialog_FormCaravan>()) Find.WindowStack.Add(new Dialog_FormCaravan(ctx.map));
        }

        /// <summary>The hour: each call waiting for a caravan, or its fight under way.</summary>
        public static void Watch(int now)
        {
            var b = PeopleEvents9Book.Instance;
            if (b == null) return;
            foreach (var r in b.seals.Where(x => x.Open).ToList())
            {
                if (r.stage == "fight")
                {
                    FightHour(r);
                    continue;
                }
                if (r.town == null || r.town.Destroyed || r.town.Faction != r.faction || r.faction.HostileTo(Faction.OfPlayer))
                {
                    r.stage = "done";
                    continue;
                }
                var c = CallsToArms.Answering(r.town);
                if (c != null)
                {
                    if (Find.WorldGrid.ApproxDistanceInTiles(c.Tile, r.town.Tile) <= 1.01f) Fight(r, c);
                    else if (r.due < now + GenDate.TicksPerDay) r.due = now + GenDate.TicksPerDay;
                    continue;
                }
                if (now <= r.due) continue;
                // Nobody came by the day it waited for.
                r.stage = "done";
                PeopleEvents9.Goodwill(r.faction, -3, GoodwillWhy.DealBroken);
                Tell.Message(NewsKind.World, Importance.Notable, r.home, "CC_PEv9_SealLapsed".Translate(r.town.Name.Named("TOWN"), r.faction.Name.Named("FACTION")).Resolve(), LookTargets.Invalid,
                    MessageTypeDefOf.NegativeEvent);
            }
        }

        /// <summary>The caravan at the town: what came up, on the caravan's battle map beside it, the vault's security with the colony's people.</summary>
        public static void Fight(SealRecord r, Caravan c)
        {
            var f = r.faction;
            try
            {
                if (c.Tile == r.town.Tile && !MoveBeside(c, r.town)) return;
                if (!CaravanIncidentUtility.CanFireIncidentWhichWantsToGenerateMapAt(c.Tile)) return;
                float points = StorytellerUtility.DefaultThreatPointsNow(r.home ?? Homes.Seat);
                var enemies = r.insects ? MakeInsects(points * PeopleEvents9Math.SealPoints, c.Tile) : MakeMechs(points * PeopleEvents9Math.SealPoints, c.Tile);
                if (enemies.Count == 0)
                {
                    r.stage = "done";
                    return;
                }
                var allies = MakeSecurity(f, points * PeopleEvents9Math.SecurityPoints, c.Tile);
                var map = CaravanIncidentUtility.SetupCaravanAttackMap(c, enemies, false);
                if (map == null)
                {
                    foreach (var p in allies) Arrivals.Discard(p);
                    return;
                }
                var enemy = enemies[0].Faction;
                LordMaker.MakeNewLord(enemy, new LordJob_AssaultColony(enemy, false, false, false, false, false), map, enemies);
                var anchor = map.mapPawns.FreeColonistsSpawned.FirstOrDefault();
                if (anchor != null && allies.Count > 0)
                {
                    foreach (var p in allies) GenSpawn.Spawn(p, CellFinder.RandomSpawnCellForPawnNear(anchor.Position, map), map);
                    LordMaker.MakeNewLord(f, new LordJob_AssistColony(f, anchor.Position), map, allies);
                }
                else foreach (var p in allies) Arrivals.Discard(p);
                Find.TickManager.Notify_GeneratedPotentiallyHostileMap();
                r.stage = "fight";
                r.fight = map;
                r.enemies = enemies.ToList();
                Tell.Letter(NewsKind.World, Importance.Important, map, "CC_PEv9_SealFightLabel".Translate(r.town.Name.Named("TOWN")).Resolve(),
                    (r.insects ? "CC_PEv9_SealFightInsects" : "CC_PEv9_SealFightMechs").Translate(r.town.Name.Named("TOWN"), f.Name.Named("FACTION"), allies.Count.Named("COUNT")).Resolve(),
                    LetterDefOf.ThreatBig, new LookTargets(enemies.Cast<Thing>()), enemy);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 9: the fight at the vault", e);
                r.stage = "done";
            }
        }

        /// <summary>The fight's hour: won (none of what came up left on its feet, some of the colony's still standing), or left before it was.</summary>
        private static void FightHour(SealRecord r)
        {
            var map = r.fight;
            if (map == null || !Find.Maps.Contains(map))
            {
                r.stage = "done";
                r.fight = null;
                if (r.home != null)
                    Tell.Message(NewsKind.World, Importance.Notable, r.home, "CC_PEv9_SealLeft".Translate((r.town?.Name ?? "?").Named("TOWN")).Resolve(), LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
                return;
            }
            int theirs = r.enemies.Count(p => p != null && !p.Dead && !p.Downed && p.Spawned && p.Map == map);
            var ours = map.mapPawns.FreeColonistsSpawned.Where(p => !p.Downed).ToList();
            if (!PeopleEvents9Math.Won(theirs, ours.Count)) return;
            r.stage = "done";
            Won(r, map, ours.First());
        }

        /// <summary>Won: the vault's share of its stores set down by the colony's people there, its thanks, its memory of the town defended.</summary>
        private static void Won(SealRecord r, Map map, Pawn by)
        {
            var f = r.faction;
            var things = new List<Thing>();
            foreach (var (def, count) in HappeningWorker_VaultBarter.Crate(f))
            {
                var t = ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
                t.stackCount = Mathf.Min(def.stackLimit, Mathf.Max(1, count * 2));
                things.Add(t);
            }
            var comps = ThingMaker.MakeThing(ThingDefOf.ComponentSpacer);
            comps.stackCount = PeopleEvents9Math.SealComponents;
            things.Add(comps);
            foreach (var t in things)
                if (GenPlace.TryPlaceThing(t, by.Position, map, ThingPlaceMode.Near)) t.SetForbidden(false, false);
            string what = TextUtil.JoinAnd(things.Select(t => t.LabelCap.ToString()).ToList());
            PeopleEvents9.Goodwill(f, PeopleEvents9Math.SealGoodwill, GoodwillWhy.Helped);
            FactionMemory.Note(f, "townDefended", r.town?.Name ?? f.Name);
            Tell.Letter(NewsKind.World, Importance.Important, map, "CC_PEv9_SealWonLabel".Translate().Resolve(),
                "CC_PEv9_SealWon".Translate((r.town?.Name ?? "?").Named("TOWN"), f.Name.Named("FACTION"), what.Named("GOODS")).Resolve(), LetterDefOf.PositiveEvent, new LookTargets(things.Where(t => t.Spawned)), f);
        }

        private static List<Pawn> MakeInsects(float points, PlanetTile tile)
        {
            var list = new List<Pawn>();
            var kinds = InsectKinds.Select(n => DefDatabase<PawnKindDef>.GetNamedSilentFail(n)).Where(k => k?.race != null && k.combatPower > 0f).ToList();
            if (kinds.Count == 0 || Faction.OfInsects == null) return list;
            float left = Mathf.Max(points, kinds.Min(k => k.combatPower) * 2f);
            for (int i = 0; i < 30 && left > 0f; i++)
            {
                var k = kinds.Where(x => x.combatPower <= left).RandomElementWithFallback();
                if (k == null) break;
                try
                {
                    list.Add(PawnGenerator.GeneratePawn(new PawnGenerationRequest(k, Faction.OfInsects, PawnGenerationContext.NonPlayer, tile, forceGenerateNewPawn: true)));
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events 9: the vault's insects", e);
                    break;
                }
                left -= k.combatPower;
            }
            return list;
        }

        private static List<Pawn> MakeMechs(float points, PlanetTile tile)
        {
            var list = new List<Pawn>();
            var f = Faction.OfMechanoids;
            if (f?.def?.pawnGroupMakers == null || !f.def.pawnGroupMakers.Any(m => m.kindDef == PawnGroupKindDefOf.Combat)) return list;
            try
            {
                float min = f.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Combat);
                list.AddRange(PawnGroupMakerUtility.GeneratePawns(new PawnGroupMakerParms { groupKind = PawnGroupKindDefOf.Combat, faction = f, points = Mathf.Max(points, min * 1.05f), tile = tile }));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 9: the vault's mechs", e);
            }
            return list;
        }

        /// <summary>The vault's security, as their people fight, worth these points; four at most.</summary>
        private static List<Pawn> MakeSecurity(Faction f, float points, PlanetTile tile)
        {
            var list = new List<Pawn>();
            if (f?.def?.pawnGroupMakers == null || !f.def.pawnGroupMakers.Any(m => m.kindDef == PawnGroupKindDefOf.Combat)) return list;
            try
            {
                float min = f.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Combat);
                list.AddRange(PawnGroupMakerUtility.GeneratePawns(new PawnGroupMakerParms
                    { groupKind = PawnGroupKindDefOf.Combat, faction = f, points = Mathf.Max(points, min * 1.05f), tile = tile, generateFightersOnly = true }));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 9: the vault's security", e);
            }
            foreach (var extra in list.Skip(PeopleEvents9Math.SecurityMax).ToList()) Arrivals.Discard(extra);
            return list.Take(PeopleEvents9Math.SecurityMax).ToList();
        }

        /// <summary>The caravan steps off the town's own tile to the open ground beside it, where the fight's map is made.</summary>
        private static bool MoveBeside(Caravan c, Settlement town)
        {
            var neighbours = new List<PlanetTile>();
            Find.WorldGrid.GetTileNeighbors(town.Tile, neighbours);
            foreach (var n in neighbours.InRandomOrder())
            {
                if (!n.Valid || Find.WorldObjects.AnyWorldObjectAt(n) || Find.World.Impassable(n)) continue;
                c.pather?.StopDead();
                c.Tile = n;
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// A surface-runner of {FACTION} ({X}) at the gate with the overseer's call: {TALE} got into the vault's lower level at {PLACE}, and the
    /// vault asks for the colony's fighters to come and fight beside its security.
    /// </summary>
    public class HappeningWorker_SealBroken : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            var f = PeoplesD.Friendly(PeoplesMath.Vaultfolk);
            return f != null && SealBroken.Open(null) == null && SealBroken.Town(f, map) != null && IncidentWorker_Happening.BattlesAllowed(map) && PeoplesG.Grown(map).Count() >= 3
                   && (Faction.OfInsects != null || Faction.OfMechanoids != null);
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Vaultfolk);
            var town = SealBroken.Town(ctx.faction, ctx.map);
            if (ctx.faction == null || town == null) return false;
            ctx.note = town.ID.ToString();
            ctx.placeName = town.Name;
            bool insects = SealBroken.Insects(Rand.Value);
            ctx.variant = insects ? 1 : 0;
            ctx.tale = (insects ? "CC_PEv9_SealInsects" : "CC_PEv9_SealMechs").Translate().Resolve();
            float wf = LivingEvents.WealthFactor(ctx.map);
            ctx.count = PeopleEvents9Math.AskComponents(wf);
            ctx.thing = ThingDefOf.MedicineIndustrial;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_SunkenVault_Scout"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Shooting);
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            tags.Add(ctx.variant > 0 ? "insects" : "mechs");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option != 0) return null;
            var town = SealBroken.TownOf(ctx);
            return town == null || town.Destroyed || town.Faction != ctx.faction ? "CC_PEv9_SealTownGone".Translate().Resolve() : null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option == 0 ? "come" : option == 1 ? "supplies" : "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>
    /// The colony's answer to the vault's call: <see cref="fight"/>, its fighters set out for the town (SealBroken.Answer); else components and
    /// medicine for the vault's security, carried out to the runner (the supply that holds the vault's age).
    /// </summary>
    public class DilemmaEffect_SealBroken : DilemmaEffect
    {
        public bool fight = true;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return;
            if (fight)
            {
                sheet.Play("CC_PEv9_Impact_SealGo".Translate(hc.placeName ?? "?", PeopleEvents9Math.SealDays).Resolve(), "CC_PEv9_Impact_SealGoBrief".Translate().Resolve());
                sheet.Add(ImpactKind.Health, -1, "CC_PEv9_Impact_SealFight".Translate(hc.tale ?? "?").Resolve(), "CC_PEv9_Impact_SealFightBrief".Translate().Resolve(), "pev9sealfight");
                sheet.Add(ImpactKind.Goods, 1, "CC_PEv9_Impact_SealStores".Translate().Resolve(), "CC_PEv9_Impact_SealStoresBrief".Translate().Resolve(), "pev9sealstores");
                if (hc.faction != null) sheet.Add(ImpactKind.Goodwill, 1, "CC_PEv9_Impact_SealThanks".Translate(hc.faction.Name, PeopleEvents9Math.SealGoodwill).Resolve(), null, "pev9sealthanks");
            }
            else
            {
                sheet.Add(ImpactKind.Other, 1, "CC_PeoplesG_Impact_Supply".Translate(hc.faction?.Name ?? "?").Resolve(), "CC_PeoplesG_Impact_SupplyBrief".Translate().Resolve(), "vaultsupply");
                if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents9Math.SuppliesGoodwill);
            }
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc?.faction == null) return;
            if (fight)
            {
                SealBroken.Answer(hc);
                Arrivals.SendAway(PeoplesG.Party(hc));
                return;
            }
            VaultSupply.Noted(hc.faction, VaultSupply.Worth(hc.count, 0) + PeopleEvents9Math.SealMedicine * (ThingDefOf.MedicineIndustrial?.BaseMarketValue ?? 18f),
                "CC_PeopleAge_Gift_components".Translate().Resolve());
            PeopleEvents9.Goodwill(hc.faction, PeopleEvents9Math.SuppliesGoodwill, GoodwillWhy.Gift);
        }
    }
}
