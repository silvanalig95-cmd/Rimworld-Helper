using System;
using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // The content round's fourth part, its world events (docs/content-round-4-council-world.md, R4W-1 to R4W-6; Defs/
    // WorldSituationDefs/Rimfolk_WS_Round4.xml): the warm springs, the gift road, the timber rafts, the beacon fires, the arms
    // sold off and the missing caravan. Each in its people's age's form (SituationWorker_Variety's tags), their state in the
    // world event's own flags; the pure rules are World/Variety/Round4CouncilWorldMath.cs. Their answers use the frame's own
    // effects: the visitors at the gate and on the road, the far errand, a search, a bonfire, goods carried out, set down and
    // paid for at the gate.

    // ===================== R4W-1. The warm springs =====================

    /// <summary>
    /// The warm springs (R4W-1): warm water at a friendly people's town, and the aching of the region go to bathe. Their
    /// runner's question is put to the one at home with an ache that warm water eases (Rimfolk's own ailments: a strained back,
    /// a stiff neck, sore hands, chilblains, a limp, a sprained ankle; {ITEM} the ache), else the most worn, else the eldest
    /// from fifty ([ache], [worn] or [old]); sent, they go to the town and home again (the far errand), the ache eased and the
    /// strain with it.
    /// </summary>
    public class SituationWorker_R4WSprings : SituationWorker_Variety
    {
        /// <summary>The ache warm water eases that someone has, or null.</summary>
        public static Hediff Ache(Pawn p) =>
            p?.health?.hediffSet?.hediffs?.FirstOrDefault(h => h?.def != null && h.Visible && Round4CouncilWorldMath.Aches.Contains(h.def.defName));

        public override bool CastBeat(WorldSituation s, SituationBeat beat, SituationContext ctx, Map map)
        {
            if (beat.key != "send") return base.CastBeat(s, beat, ctx, map);
            var pool = IncidentWorker_Happening.Colonists(map).Where(p => !p.DevelopmentalStage.Juvenile() && !p.Downed && !p.InMentalState && !FarErrands.Away(p)).ToList();
            var a = pool.Where(p => Ache(p) != null).InRandomOrder().FirstOrDefault()
                    ?? pool.Where(p => Round4CouncilWorldMath.SpringsFor(false, Heart.StrainOf(p))).OrderByDescending(Heart.StrainOf).FirstOrDefault()
                    ?? pool.Where(p => p.RaceProps.Humanlike && Flavor.HumanAge(p) >= Round4CouncilWorldMath.SpringsOldFrom).OrderByDescending(Flavor.HumanAge).FirstOrDefault();
            if (a == null) return false;
            ctx.a = a;
            var ache = Ache(a);
            ctx.item = ache?.def?.label ?? "";
            s.Set(ache != null ? "ache" : Round4CouncilWorldMath.SpringsFor(false, Heart.StrainOf(a)) ? "worn" : "old");
            return true;
        }
    }

    // ===================== R4W-2. The gift road =====================

    /// <summary>
    /// The gift road (R4W-2): a friendly people sends a gift from settlement to settlement after a good year, each passing a
    /// gift of its own on. Their bearer sets theirs down at the gate whatever the colony answers; the colony passes on food or
    /// silver, or keeps it and sends nothing on. All in the def; the class gives it its people's age.
    /// </summary>
    public class SituationWorker_R4WGiftRoad : SituationWorker_Variety
    {
    }

    // ===================== R4W-3. The timber rafts =====================

    /// <summary>
    /// The timber rafts (R4W-3): a friendly people floats timber down the river to its town. Only with a river on the colony's
    /// map. The raftsmen camp a night at the colony's water; their foreman sells logs cheap at the gate. Whether a raft breaks up
    /// on the rocks is settled at the start ([willBreak]); when it does, some of its logs wash up on the colony's own bank, told
    /// only where there is a bank to wash up on.
    /// </summary>
    public class SituationWorker_R4WRafts : SituationWorker_Variety
    {
        /// <summary>A river runs across the colony's map (looked over every third cell).</summary>
        public static bool River(Map map)
        {
            if (map == null) return false;
            try
            {
                for (int x = 0; x < map.Size.x; x += 3)
                    for (int z = 0; z < map.Size.z; z += 3)
                        if (new IntVec3(x, 0, z).GetTerrain(map)?.IsRiver == true) return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 4 world events: the river", e);
            }
            return false;
        }

        /// <summary>A cell on the colony's bank where logs can wash up: standable, beside the river, the nearest to the colony.</summary>
        public static IntVec3 Bank(Map map)
        {
            if (map == null) return IntVec3.Invalid;
            var center = CampSite.ColonyCenter(map);
            if (!center.IsValid) center = map.Center;
            foreach (var c in GenRadial.RadialCellsAround(center, 60f, true))
            {
                if (!c.InBounds(map) || !c.Standable(map) || c.Fogged(map) || c.GetTerrain(map)?.IsRiver == true || c.GetFirstBuilding(map) != null) continue;
                for (int i = 0; i < 4; i++)
                {
                    var n = c + GenAdj.CardinalDirections[i];
                    if (n.InBounds(map) && n.GetTerrain(map)?.IsRiver == true) return c;
                }
            }
            return IntVec3.Invalid;
        }

        protected override bool CanHappen(Map map) => River(map);

        public override bool Cast(WorldSituation s, Map map)
        {
            if (!base.Cast(s, map)) return false;
            if (Rand.Chance(Round4CouncilWorldMath.BreakChance)) s.Set("willBreak");
            return true;
        }

        public override bool PrepareBeat(WorldSituation s, SituationBeat beat, SituationContext ctx)
        {
            if (beat.key != "sell") return true;
            ctx.thing = ThingDefOf.WoodLog;
            ctx.count = Round4CouncilWorldMath.RaftPrice(Round4CouncilWorldMath.RaftLogs, ThingDefOf.WoodLog.BaseMarketValue);
            return true;
        }

        public override SituationEnding TimeoutEnding(WorldSituation s)
        {
            // A raft broken on the rocks is told only where its logs can wash up on the colony's bank.
            if (s.Has("willBreak") && Bank(SituationEffectUtil.MapOf(s.ctx)).IsValid) return def.Ending("broke") ?? base.TimeoutEnding(s);
            return def.Ending("reached") ?? base.TimeoutEnding(s);
        }
    }

    /// <summary>The logs of a raft broken on the rocks, washed up on the colony's bank (R4W-3): the game's own logs, where they lie.</summary>
    public class WorldEffect_R4WDriftLogs : WorldEffect
    {
        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
        }

        public override void Apply(DilemmaContext ctx)
        {
            var map = SituationEffectUtil.MapOf(ctx);
            if (map == null || !Gate(ctx)) return;
            try
            {
                var at = SituationWorker_R4WRafts.Bank(map);
                if (!at.IsValid) return;
                int n = Round4CouncilWorldMath.DriftLogs(Rand.Value);
                Thing first = null;
                while (n > 0)
                {
                    var t = ThingMaker.MakeThing(ThingDefOf.WoodLog);
                    t.stackCount = Mathf.Min(n, ThingDefOf.WoodLog.stackLimit);
                    n -= t.stackCount;
                    GenPlace.TryPlaceThing(t, at, map, ThingPlaceMode.Near);
                    first = first ?? t;
                }
                if (first != null)
                    Tell.Message(NewsKind.Land, Importance.Notable, map, "CC_R4W_Rafts_LogsAshore".Translate().Resolve(), new LookTargets(first), MessageTypeDefOf.PositiveEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 4 world events: the logs ashore", e);
            }
        }
    }

    // ===================== R4W-4. The beacon fires =====================

    /// <summary>
    /// The beacon fires (R4W-4): raiders are out (a people hostile to the colony where there is one: {ENEMY}, [enemy]), and a
    /// friendly people keeps a chain of beacons on the hills. Kept, someone of the colony lays a fire in the open each evening
    /// for three evenings and sits by it into the night (the bonfire, with its own small risk named on the answer).
    /// </summary>
    public class SituationWorker_R4WBeacons : SituationWorker_Variety
    {
        protected override void AddTags(WorldSituation s, HashSet<string> tags)
        {
            base.AddTags(s, tags);
            if (s?.ctx?.enemy != null) tags.Add("enemy");
        }
    }

    // ===================== R4W-5. The arms sold off =====================

    /// <summary>
    /// The arms sold off (R4W-5): a people at peace made a hard peace and sells its armoury rather than hand it over. Their
    /// seller's weapons ({THING}) are of the people's age, read from every weapon of the game and the player's active mods (of
    /// that tech level, tradeable, of a fair price, never a persona's), the game's own by name after; {COUNT} the price of three.
    /// </summary>
    public class SituationWorker_R4WArms : SituationWorker_Variety
    {
        /// <summary>The game's own, when no weapon of the age fits: a spear, a longsword, a bolt-action rifle, a charge rifle.</summary>
        public static readonly Dictionary<int, string> Fallback = new Dictionary<int, string>
        {
            [2] = "MeleeWeapon_Spear", [3] = "MeleeWeapon_LongSword", [4] = "Gun_BoltActionRifle", [5] = "Gun_ChargeRifle"
        };

        /// <summary>What one is worth as it would be made: of its usual stuff when it takes one.</summary>
        public static float Value(ThingDef d)
        {
            if (d == null) return 0f;
            try
            {
                return d.MadeFromStuff ? d.GetStatValueAbstract(StatDefOf.MarketValue, GenStuff.DefaultStuffFor(d)) : d.BaseMarketValue;
            }
            catch
            {
                return d.BaseMarketValue;
            }
        }

        public static bool Fits(ThingDef d, int tech) =>
            d != null && d.IsWeapon && d.category == ThingCategory.Item && (int)d.techLevel == tech && d.equipmentType == EquipmentType.Primary && d.tradeability.TraderCanSell()
            && !d.destroyOnDrop && !d.HasComp(typeof(CompBladelinkWeapon)) && (d.weaponTags == null || !d.weaponTags.Any(t => t.IndexOf("Unique", StringComparison.OrdinalIgnoreCase) >= 0))
            && Round4CouncilWorldMath.ArmsFair(Value(d));

        /// <summary>The weapon the cart carries for a people of this age (the same for the event, by its id).</summary>
        public static ThingDef Weapon(string ageTag, int seed)
        {
            int tech = Round4CouncilWorldMath.ArmsTech(ageTag);
            var all = DefDatabase<ThingDef>.AllDefsListForReading.Where(d => Fits(d, tech)).OrderBy(d => d.defName).ToList();
            if (all.Count > 0) return all[Mathf.Abs(seed) % all.Count];
            return Fallback.TryGetValue(tech, out var n) ? DefDatabase<ThingDef>.GetNamedSilentFail(n) : null;
        }

        public override bool PrepareBeat(WorldSituation s, SituationBeat beat, SituationContext ctx)
        {
            if (beat.key != "seller") return true;
            string age = s.ctx.faction != null ? Ages.Tag(Ages.Of(s.ctx.faction)) : "medieval";
            var w = Weapon(age, Gen.HashCombineInt(s.id, 4049));
            if (w == null) return false;
            ctx.thing = w;
            ctx.count = Round4CouncilWorldMath.ArmsPrice(Value(w), Round4CouncilWorldMath.ArmsCount);
            return true;
        }
    }

    // ===================== R4W-6. The missing caravan =====================

    /// <summary>
    /// The missing caravan (R4W-6): a friendly people's caravan never arrived. Whether it is found is settled at the start, out
    /// of sight (Round4CouncilWorldMath.Found), and shown by what follows: found, it passes the colony on the road home with its
    /// beasts; lost, nobody comes.
    /// </summary>
    public class SituationWorker_R4WMissing : SituationWorker_Variety
    {
        public override bool Cast(WorldSituation s, Map map)
        {
            if (!base.Cast(s, map)) return false;
            s.Set(Round4CouncilWorldMath.Found(Rand.Value) ? "willBeFound" : "willBeLost");
            return true;
        }
    }

    // ===================== Dev mode =====================

    /// <summary>Dev mode for the round's world events (Debug actions, "Rimfolk"): each new world event started now.</summary>
    public static class R4WDebug
    {
        private static Map Map => Find.CurrentMap ?? HappeningTracker.HomeMap();

        private static void Say(string text) => Messages.Message("Round 4: " + text, MessageTypeDefOf.SilentInput, false);

        /// <summary>The world events of this round.</summary>
        public static readonly string[] WorldEvents =
        {
            "Rimfolk_WS_WarmSprings", "Rimfolk_WS_GiftRoad", "Rimfolk_WS_Rafts", "Rimfolk_WS_Beacons", "Rimfolk_WS_ArmsSold", "Rimfolk_WS_MissingCaravan"
        };

        [DebugAction("Rimfolk", "Round 4: start one of the new world events now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Start()
        {
            var inst = WorldSituations.Instance;
            var map = Map;
            if (inst == null || map == null) return;
            var options = new List<FloatMenuOption>();
            foreach (string n in WorldEvents)
            {
                var def = DefDatabase<WorldSituationDef>.GetNamedSilentFail(n);
                if (def == null) continue;
                bool can;
                try
                {
                    can = def.Worker.CanStart(map);
                }
                catch
                {
                    can = false;
                }
                options.Add(new FloatMenuOption(def.LabelCap + (can ? "" : " (can't now: its people, the land or its season)"), () =>
                {
                    var s = inst.TryStart(map, x => x == def, true);
                    Say(s != null ? def.label + " started." : "couldn't start " + def.label + " now (nobody fits, or one of its kind is running).");
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}
