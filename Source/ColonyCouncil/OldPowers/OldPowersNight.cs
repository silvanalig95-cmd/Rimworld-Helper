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
    /// The old powers' strange nights (OldPowers.cs):
    /// <list type="bullet">
    /// <item>Everything faced one way: at midnight every one and every beast awake on the land stops and turns to face one
    /// way, their shadows running out that way, a few breaths long; the night after one of the colony died they all face
    /// where the dead lie, and at dawn someone goes and stands by them; else they face the edge of the land, and at dawn the
    /// first one up walks out that way and finds an old worn thing of the colony's age lying there.</item>
    /// <item>The humming stone: under a thunderstorm or a psychic sky a stone of the colony's (a chunk lying out, else the
    /// rock face nearest the middle) hums from dusk till five in the morning; someone lays a hand on it, the beasts keep
    /// away, a mage who touches it is filled, and those who sleep near it dream of it.</item>
    /// <item>The casters burning bright: under A RimWorld of Magic's mana storm or surge, a psychic bloom, or an aurora, the
    /// colony's mages (their mana filled), psycasters (half their psyfocus) or those sensitive enough light up where they
    /// stand; one may be burned by the light running out of their hands (the shared budget's rate).</item>
    /// <item>The dead on the road: at dusk after a death of the colony, or on a night with no moon while the void is awake
    /// or the dead lie unburied, the dead walk a road across the land past the colony: with Anomaly, through the shared
    /// harm budget, the game's own shamblers walking through; else a line of pale figures that passes and is gone.</item>
    /// <item>Another sky: on a clear night in the deep of summer or winter, or under an aurora, the sky is another sky till
    /// five in the morning (its own colours, its stars wrong); those out of doors stop and look up.</item>
    /// <item>The pale wolves: under the full moon on a clear night (in winter or on snow the likelier), a wolf howls far off,
    /// and a pack of five to eight pale wolves (A RimWorld of Magic's spirit wolves where it is loaded) runs once round the
    /// colony and off the land; the beasts near stand and watch, those out of doors stop and look; at dawn someone walks
    /// out to where they ran and finds no tracks.</item>
    /// <item>Eyes in the dark: on a foggy night, or a night with no moon in autumn or winter, pairs of eyes catch the light
    /// at the edge of the dark round the colony, low to the ground, and don't move; those out of doors stop and look; after
    /// a while the one handiest in a fight walks out to the nearest pair, and as they come close every pair goes out, with
    /// nothing there. Unwalked to, they go out at first light.</item>
    /// </list>
    /// </summary>
    public static class OldPowersNight
    {
        private static int Now => OldPowers.Now;

        private static readonly Color Shadow = new Color(0.12f, 0.1f, 0.16f), Hum = new Color(0.62f, 0.78f, 1f), Bright = new Color(0.78f, 0.62f, 1f),
            Pale = new Color(0.82f, 0.86f, 0.9f), Stars = new Color(0.7f, 1f, 0.86f);

        // ================= Everything faced one way =================

        public static GodPaceMath.Trigger OneWayCall(Map map)
        {
            if (map.mapPawns.FreeColonistsSpawned.All(p => !OldPowers.Awake(p))) return GodPaceMath.Trigger.None;
            return OldPowersMath.OneWayTrigger(OldPowers.HoursSinceDeath(map, out _), OldPowers.Moonless, OwnHappenings.Clear(map));
        }

        public static bool OneWay(OldPowersBook b, Map map)
        {
            var mid = OwnHappenings.Middle(map);
            float hours = OldPowers.HoursSinceDeath(map, out var dead);
            bool toDead = dead != null && dead.Spawned && !float.IsNaN(hours) && hours <= OldPowersMath.DeadFacedHours;
            IntVec3 target;
            if (toDead) target = dead.Position;
            else if (!CellFinder.TryFindRandomEdgeCellWith(c => c.Standable(map) && !c.Fogged(map) && map.reachability.CanReachColony(c), map, 0f, out target)) return false;
            float angle = Angle(mid, target);
            int colonists = 0;
            var stood = new List<Pawn>();
            foreach (var p in map.mapPawns.AllPawnsSpawned.ToList())
            {
                if (p.Dead || p.Downed || !p.Awake() || p.InMentalState || p.Drafted || p.HostileTo(Faction.OfPlayer)) continue;
                if (p.RaceProps.Humanlike ? !OwnHappenings.Yields(p) : p.RaceProps.IsMechanoid) continue;
                // The same way for all, or all toward the dead.
                var toward = toDead ? target : p.Position + (target - mid);
                if (!OwnHappenings.Stand(p, toward, OldPowersMath.FacedTicks)) continue;
                float a = toDead ? Angle(p.Position, target) : angle;
                for (int i = 0; i < 2; i++)
                    OldPowers.Drift(map, p.DrawPos, Shadow, a + Rand.Range(-6f, 6f), Rand.Range(0.5f, 0.9f), Rand.Range(0.7f, 1f), 2.4f, "Mote_Shadow", "Mote_ShadowCloud");
                if (p.IsFreeColonist) colonists++;
                stood.Add(p);
            }
            if (colonists == 0) return false;
            OldPowers.Sound(map, mid, "TM_VibrationLow", "Psycast_Skip_Pulse", "PsychicPulseGlobal");
            var witness = stood.Where(p => p.IsFreeColonist && OwnHappenings.Ours(p)).RandomElementWithFallback();
            if (witness != null) OldPowers.Say(witness, toDead ? "oneway,dead" : "oneway,turn");
            foreach (var p in stood.Where(p => p.IsFreeColonist)) OldPowers.Memory(p, "Rimfolk_Thought_OldP_OneWay");
            string who = OldPowers.T(stood.Any(p => p.RaceProps.Animal) ? "OneWay_WhoBeasts" : "OneWay_WhoPeople", stood.Count(p => p.RaceProps.Humanlike).ToString().Named("COUNT"));
            string text = OldPowers.T("OneWay_Text", OldPowers.Toward(mid, target).Named("POINT"), who.Named("WHO"));
            var deadPawn = (dead as Corpse)?.InnerPawn ?? (dead as Building_Grave)?.Corpse?.InnerPawn;
            if (toDead) text += " " + OldPowers.T("OneWay_Dead", (deadPawn?.LabelShort ?? "?").Named("DEAD"));
            else text += " " + OldPowers.T("OneWay_Edge");
            OldPowers.Card(map, "OneWay", text, new LookTargets(stood.Where(p => p.IsFreeColonist)), LetterDefOf.NeutralEvent);
            var play = OldPowers.Play(b, OldPower.OneWay, map, 12f, 2500);
            play.cell = target;
            play.strong = toDead;
            play.thing = dead;
            play.note = deadPawn?.LabelShort;
            play.stage = 1;
            play.form = toDead ? "dead" : "edge";
            b.Came(OldPower.OneWay, Now, play.form);
            return true;
        }

        /// <summary>The angle from one cell to another as the motes take it (0 up the screen, clockwise in degrees).</summary>
        private static float Angle(IntVec3 from, IntVec3 to)
        {
            float a = Mathf.Atan2(to.x - from.x, to.z - from.z) * Mathf.Rad2Deg;
            return a < 0f ? a + 360f : a;
        }

        internal static void OneWayStep(OldPowersBook b, OldPowerPlay p, int now)
        {
            var map = p.map;
            int hour = GenLocalDate.HourOfDay(map);
            switch (p.stage)
            {
                case 1:
                {
                    // Waiting for the dawn; then someone goes the way everything faced.
                    if (hour < 6 || hour >= 10)
                    {
                        OldPowers.Next(b, p, 1250);
                        return;
                    }
                    var go = OldPowers.FreeNear(map, p.cell);
                    if (go == null)
                    {
                        if (hour >= 9) OldPowers.End(b);
                        else OldPowers.Next(b, p, 1250);
                        return;
                    }
                    if (!p.strong)
                    {
                        var old = OwnMurmur.OldThing();
                        if (old == null || !GenPlace.TryPlaceThing(old, p.cell, map, ThingPlaceMode.Near, out var placed))
                        {
                            OldPowers.End(b);
                            return;
                        }
                        p.thing = placed;
                        p.cell = placed.Position;
                    }
                    var at = OwnHappenings.Beside(map, p.thing != null && p.thing.Spawned ? p.thing.Position : p.cell, go);
                    if (!at.IsValid || !OwnHappenings.GoTo(go, at, GenDate.TicksPerHour * 2))
                    {
                        OldPowers.End(b);
                        return;
                    }
                    p.b = go;
                    p.stage = 2;
                    p.count = 0;
                    OldPowers.Next(b, p, 250);
                    return;
                }
                case 2:
                {
                    var go = p.b;
                    if (go == null || go.Dead || !go.Spawned)
                    {
                        OldPowers.End(b);
                        return;
                    }
                    var spot = p.thing != null && p.thing.Spawned ? p.thing.Position : p.cell;
                    if (!go.Position.InHorDistOf(spot, 3f) && p.count++ < 20)
                    {
                        OldPowers.Next(b, p, 250);
                        return;
                    }
                    OwnHappenings.Stand(go, spot, 600);
                    if (p.strong)
                    {
                        OldPowers.Say(go, "oneway,grave", (p.note ?? "").Named("DEAD"));
                        OldPowers.Minor(map, OldPowers.T("OneWay_DawnDead", go.LabelShort.Named("WALKER"), (p.note ?? "?").Named("DEAD")), new LookTargets(go));
                    }
                    else
                    {
                        string thing = p.thing?.LabelNoCount ?? "?";
                        OldPowers.Say(go, "oneway,found", thing.Named("THING"));
                        OldPowers.Minor(map, OldPowers.T("OneWay_DawnFound", go.LabelShort.Named("WALKER"), thing.Named("THING")), new LookTargets(p.thing));
                    }
                    OldPowers.End(b);
                    return;
                }
            }
        }

        // ================= The humming stone =================

        /// <summary>A psychic sky or a sky of power (the game's, Vanilla Events Expanded's, Vanilla Psycasts Expanded's, the Archon's, A RimWorld of Magic's).</summary>
        private static readonly string[] PsychicSkies =
        {
            "PsychicDrone", "PsychicSoothe", "PsychicRain", "VEE_PsychicHum", "VEE_PsychicStimulation", "VEE_PsychicBloom", "VEE_PsychicOverdrive",
            "VPE_PsychicFlashstorm", "VREA_PsychicStorm", "ManaSurge", "TM_ManaStorm"
        };

        /// <summary>A sky of power that fills a caster (A RimWorld of Magic's storm and surge, Vanilla Events Expanded's bloom, stimulation and overdrive).</summary>
        private static readonly string[] ManaSkies = { "ManaSurge", "TM_ManaStorm", "VEE_PsychicBloom", "VEE_PsychicStimulation", "VEE_PsychicOverdrive" };

        private static bool Storm(Map map) => map.weatherManager?.curWeather?.defName?.IndexOf("Thunder", StringComparison.OrdinalIgnoreCase) >= 0;

        public static GodPaceMath.Trigger StoneCall(Map map)
        {
            var why = OldPowersMath.StoneTrigger(OldPowers.AnyCondition(map, PsychicSkies), Storm(map));
            return why != GodPaceMath.Trigger.None && StoneOf(map, out _) != null ? why : GodPaceMath.Trigger.None;
        }

        /// <summary>The colony's stone: a chunk lying out of doors near the middle, else the rock face nearest it (its cell); null for none.</summary>
        internal static Thing StoneOf(Map map, out IntVec3 cell)
        {
            cell = IntVec3.Invalid;
            var mid = OwnHappenings.Middle(map);
            var chunk = map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver)
                .Where(t => t.Spawned && t.def.thingCategories != null && t.def.thingCategories.Contains(ThingCategoryDefOf.StoneChunks) && !t.Position.Roofed(map)
                            && !t.Position.Fogged(map) && t.Position.InHorDistOf(mid, 40f))
                .OrderBy(t => t.Position.DistanceToSquared(mid)).FirstOrDefault();
            if (chunk != null)
            {
                cell = chunk.Position;
                return chunk;
            }
            foreach (var c in GenRadial.RadialCellsAround(mid, 30f, false))
            {
                if (!c.InBounds(map) || c.Fogged(map)) continue;
                var e = c.GetEdifice(map);
                if (e == null || e.def.building == null || !e.def.building.isNaturalRock) continue;
                if (!GenAdj.CellsAdjacentCardinal(c, Rot4.North, IntVec2.One).Any(n => n.InBounds(map) && n.Standable(map) && !n.Roofed(map))) continue;
                cell = c;
                return e;
            }
            return null;
        }

        public static bool Stone(OldPowersBook b, Map map, bool strong)
        {
            var stone = StoneOf(map, out var cell);
            if (stone == null) return false;
            int hour = GenLocalDate.HourOfDay(map);
            int left = (OldPowersMath.StoneStopsAt + 24 - hour) % 24;
            var p = OldPowers.Play(b, OldPower.Stone, map, Math.Max(2, left), 60);
            p.thing = stone;
            p.cell = cell;
            p.strong = strong;
            p.stage = 1;
            p.form = stone.def.category == ThingCategory.Item ? "chunk" : "face";
            OldPowers.Card(map, "Stone", OldPowers.T("Stone_Text", Name(stone).Named("STONE"), (strong ? OldPowers.T("Stone_Psychic") : OldPowers.T("Stone_Storm")).Named("WHY")),
                new LookTargets(stone), LetterDefOf.NeutralEvent);
            b.Came(OldPower.Stone, Now, p.form);
            return true;
        }

        private static string Name(Thing stone) => stone.def.category == ThingCategory.Item ? stone.LabelNoCount : OldPowers.T("Stone_Face", stone.def.label.Named("ROCK"));

        internal static void StoneStep(OldPowersBook b, OldPowerPlay p, int now)
        {
            var map = p.map;
            var stone = p.thing;
            if (stone == null || stone.Destroyed || GenLocalDate.HourOfDay(map) == OldPowersMath.StoneStopsAt)
            {
                OldPowers.Minor(map, OldPowers.T("Stone_Quiet"), stone != null && stone.Spawned ? new LookTargets(stone) : LookTargets.Invalid);
                OldPowers.End(b);
                return;
            }
            var at = stone.Spawned ? stone.Position : p.cell;
            OldPowers.Motes(map, at.ToVector3Shifted(), Hum, 3, 0.5f, "Mote_PsiCurrent", "Mote_Psi", "PsycastPsychicEffect");
            OldPowers.Sound(map, at, "TM_VibrationLow", "AnimaTreeProgress", "PsychicSootheGlobal");
            // The beasts keep away from it.
            foreach (var a in map.mapPawns.AllPawnsSpawned.Where(x => x.RaceProps.Animal && !x.Dead && !x.Downed && x.Awake() && x.Position.InHorDistOf(at, 6f) && OwnHappenings.Yields(x)).Take(4).ToList())
            {
                var away = a.Position + (a.Position - at);
                OwnHappenings.Lead(a, CellFinder.StandableCellNear(away.ClampInsideMap(map), map, 4f), 600);
            }
            // Someone lays a hand on it once, a little after it begins.
            if (p.b == null && p.count++ >= 1)
            {
                var hand = OldPowers.FreeNear(map, at);
                var near = hand == null ? IntVec3.Invalid : OwnHappenings.Beside(map, at, hand);
                if (near.IsValid && OwnHappenings.GoTo(hand, near, 1500)) p.b = hand;
            }
            else if (p.b != null && p.stage == 1 && p.b.Spawned && p.b.Position.InHorDistOf(at, 2f))
            {
                p.stage = 2;
                OwnHappenings.Stand(p.b, at, 900);
                OldPowers.Say(p.b, "stone,touch");
                OldPowers.Memory(p.b, "Rimfolk_Thought_OldP_Stone");
                if (CasterOf(p.b) == OldPowersMath.Caster.Mage && FillMana(p.b)) OldPowers.Motes(map, p.b.DrawPos, Bright, 5, 0.5f, "Mote_ManaPuff", "Mote_Psi_Arcane");
            }
            // Those who sleep near it dream of it.
            foreach (var s in map.mapPawns.FreeColonistsSpawned.Where(x => !x.Awake() && x.Position.InHorDistOf(at, OldPowersMath.DreamReach)))
                OldPowers.Memory(s, "Rimfolk_Thought_OldP_StoneDream");
            OldPowers.Next(b, p, OldPowersMath.HumEvery);
        }

        // ================= The casters burning bright =================

        /// <summary>What one of the colony is to a sky full of power: a mage (A RimWorld of Magic), a psycaster, one sensitive enough, or nothing.</summary>
        internal static OldPowersMath.Caster CasterOf(Pawn p)
        {
            if (p == null) return OldPowersMath.Caster.None;
            string craft = PeoplesMagic.Craft(p);
            bool mage = craft != null && !PeoplesGearMath.Might(craft);
            bool psy = ModsConfig.RoyaltyActive && p.HasPsylink;
            float sens = p.GetStatValue(StatDefOf.PsychicSensitivity);
            return OldPowersMath.CasterOf(mage, psy, sens);
        }

        /// <summary>A RimWorld of Magic's mana, filled (its need, by name); false without it.</summary>
        internal static bool FillMana(Pawn p)
        {
            var needs = p?.needs?.AllNeeds;
            if (needs == null) return false;
            foreach (var n in needs)
                if (n != null && n.GetType().FullName == "TorannMagic.Need_Mana")
                {
                    n.CurLevel = n.MaxLevel;
                    return true;
                }
            return false;
        }

        private static List<Pawn> Casters(Map map) => map.mapPawns.FreeColonistsSpawned.Where(p => OwnHappenings.Ours(p) && CasterOf(p) != OldPowersMath.Caster.None).ToList();

        public static GodPaceMath.Trigger ChargedCall(Map map) =>
            OldPowersMath.ChargedTrigger(OldPowers.AnyCondition(map, ManaSkies), OldPowers.AnyCondition(map, "Aurora"), Casters(map).Count);

        public static bool Charged(OldPowersBook b, Map map, bool strong)
        {
            var casters = Casters(map);
            if (casters.Count == 0) return false;
            var names = new List<string>();
            foreach (var c in casters)
            {
                var kind = CasterOf(c);
                OldPowers.Motes(map, c.DrawPos, Bright, 8, 0.5f, kind == OldPowersMath.Caster.Mage ? new[] { "Mote_ManaPuff", "Mote_ArcaneWaves", "Mote_Psi_Arcane" } : new[] { "Mote_Psi", "PsycastPsychicEffect" });
                if (kind == OldPowersMath.Caster.Mage) FillMana(c);
                else if (kind == OldPowersMath.Caster.Psycaster) c.psychicEntropy?.OffsetPsyfocusDirectly(OldPowersMath.PsyfocusGain);
                if (OldPowers.Awake(c) && OwnHappenings.Yields(c)) OwnHappenings.Stand(c, c.Position + IntVec3.North * 4, 900);
                OldPowers.Memory(c, "Rimfolk_Thought_OldP_Charged");
                names.Add(OldPowers.T("Charged_" + kind, c.LabelShort.Named("NAME")));
            }
            OldPowers.Sound(map, casters[0].Position, "TM_Powerup", "MeditationGainPsyfocus", "PsychicPulseGlobal");
            foreach (var c in casters.Where(OldPowers.Awake).Take(2)) OldPowers.Say(c, "charged," + CasterOf(c).ToString().ToLowerInvariant());
            // The light running out of one of them too fast: a burn across the hands, at the shared budget's rate.
            string burned = null;
            var hurt = casters.Where(OldPowers.Awake).RandomElementWithFallback();
            if (hurt != null && Harm.Roll(hurt, HarmKinds.Uncanny, OldPowersMath.BurnChance)
                && Harm.Injure(hurt, HurtKind.Burn, HurtWhere.Hand, OldPowersMath.BurnLow, OldPowersMath.BurnHigh, HarmKinds.Uncanny) != null)
            {
                burned = OldPowers.T("Charged_Burned", hurt.LabelShort.Named("NAME"));
                OldPowers.Say(hurt, "charged,burned");
            }
            string text = OldPowers.T("Charged_Text", (strong ? OldPowers.T("Charged_Power") : OldPowers.T("Charged_Aurora")).Named("SKY"),
                string.Join("\n", names.Select(n => "- " + n)).Named("NAMES"));
            if (burned != null) text += "\n\n" + burned;
            OldPowers.Card(map, "Charged", text, new LookTargets(casters), burned != null ? LetterDefOf.NegativeEvent : LetterDefOf.PositiveEvent);
            b.Came(OldPower.Charged, Now, strong ? "power" : "aurora");
            return true;
        }

        internal static void ChargedStep(OldPowersBook b, OldPowerPlay p, int now) => OldPowers.End(b);

        // ================= The dead on the road =================

        private static bool VoidAwake => ModsConfig.AnomalyActive && Find.Anomaly != null && Find.Anomaly.Level > 0;

        public static GodPaceMath.Trigger DeadRoadCall(Map map)
        {
            if (map.mapPawns.FreeColonistsSpawned.All(p => !OldPowers.Awake(p))) return GodPaceMath.Trigger.None;
            return OldPowersMath.DeadRoadTrigger(OldPowers.HoursSinceDeath(map, out _), OldPowers.Moonless, VoidAwake, OldPowers.Unburied(map));
        }

        /// <summary>The road: from one edge to the other, passing the colony a little way off its middle.</summary>
        private static bool Road(Map map, out IntVec3 from, out IntVec3 to)
        {
            var mid = OwnHappenings.Middle(map).ToVector3Shifted();
            float a = Rand.Range(0f, 360f) * Mathf.Deg2Rad;
            var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            var side = new Vector3(-dir.z, 0f, dir.x) * Rand.Range(18f, 30f) * (Rand.Bool ? 1f : -1f);
            float reach = Mathf.Max(map.Size.x, map.Size.z);
            from = (mid + side - dir * reach).ToIntVec3().ClampInsideMap(map);
            to = (mid + side + dir * reach).ToIntVec3().ClampInsideMap(map);
            return from.IsValid && to.IsValid && from != to;
        }

        public static bool DeadRoad(OldPowersBook b, Map map, bool strong)
        {
            if (!Road(map, out var from, out var to)) return false;
            string why = strong ? OldPowers.T("DeadRoad_AfterDeath") : OldPowers.T("DeadRoad_NoMoon");
            // With Anomaly, through the shared harm budget: the game's own shamblers walking the road through.
            if (ModsConfig.AnomalyActive && Harm.On && Harm.Roll(HarmKinds.Uncanny, OldPowersMath.DeadWalkChance) && Shamblers(map, from, why))
            {
                b.Came(OldPower.DeadRoad, Now, "walkers");
                return true;
            }
            var p = OldPowers.Play(b, OldPower.DeadRoad, map, 3f, 60);
            p.cell = from;
            p.cell2 = to;
            p.stage = 1;
            p.count = 0;
            p.form = "sight";
            OldPowers.Card(map, "DeadRoad", OldPowers.T("DeadRoad_Text", why.Named("WHY"), OldPowers.Toward(OwnHappenings.Middle(map), from).Named("FROM")),
                new LookTargets(from, map), LetterDefOf.NeutralEvent);
            b.Came(OldPower.DeadRoad, Now, p.form);
            return true;
        }

        /// <summary>The game's small shambler swarm, at a small share of the threat points, its letter in the old powers' words above its own.</summary>
        private static bool Shamblers(Map map, IntVec3 from, string why)
        {
            var def = DefDatabase<IncidentDef>.GetNamedSilentFail("SmallShamblerSwarm") ?? DefDatabase<IncidentDef>.GetNamedSilentFail("ShamblerSwarm");
            if (def == null) return false;
            var parms = new IncidentParms
            {
                target = map, forced = true, spawnCenter = from, points = OldPowersMath.DeadRoadPoints(StorytellerUtility.DefaultThreatPointsNow(map)),
                customLetterLabel = OldPowers.T("DeadRoad_Title"),
                customLetterText = OldPowers.T("DeadRoad_Walkers", why.Named("WHY")) + "\n\n{BASETEXT}"
            };
            try
            {
                return def.Worker.CanFireNow(parms) && def.Worker.TryExecute(parms);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the old powers: the dead on the road", e);
                return false;
            }
        }

        internal static void DeadRoadStep(OldPowersBook b, OldPowerPlay p, int now)
        {
            var map = p.map;
            if (p.count > OldPowersMath.RoadSteps)
            {
                OldPowers.Minor(map, OldPowers.T("DeadRoad_Gone"), LookTargets.Invalid);
                OldPowers.End(b);
                return;
            }
            float t = p.count / (float)OldPowersMath.RoadSteps;
            var at = Vector3.Lerp(p.cell.ToVector3Shifted(), p.cell2.ToVector3Shifted(), t);
            float angle = Angle(p.cell, p.cell2);
            for (int i = 0; i < 3; i++)
                OldPowers.Drift(map, at + new Vector3(Rand.Range(-1.2f, 1.2f), 0f, Rand.Range(-1.2f, 1.2f)), Pale, angle, 0.55f, Rand.Range(0.8f, 1.1f), 3f, "Mote_Ghost");
            // Whoever of the colony sees them pass stops and watches, once.
            var cell = at.ToIntVec3();
            foreach (var w in map.mapPawns.FreeColonistsSpawned.Where(x => OwnHappenings.Ours(x) && OldPowers.Awake(x) && x.Position.InHorDistOf(cell, 35f) && OwnHappenings.Yields(x)
                                                                          && GenSight.LineOfSight(x.Position, cell, map, true)).ToList())
            {
                var def = DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_Thought_OldP_DeadRoad");
                if (def != null && w.needs?.mood?.thoughts?.memories?.GetFirstMemoryOfDef(def) != null) continue;
                OwnHappenings.Stand(w, cell, 900);
                OldPowers.Memory(w, "Rimfolk_Thought_OldP_DeadRoad");
                if (p.b == null)
                {
                    p.b = w;
                    OldPowers.Say(w, "deadroad,sight");
                }
            }
            if (p.count == 0) OldPowers.Sound(map, cell, "TM_WindLowSD", "TM_AirWooshLow", "PsychicSootheGlobal");
            p.count++;
            OldPowers.Next(b, p, OldPowersMath.RoadStepTicks);
        }

        // ================= Another sky =================

        public const string OtherSkyDef = "Rimfolk_OldP_OtherSky";

        public static GodPaceMath.Trigger OtherSkyCall(Map map)
        {
            if (map.gameConditionManager.ConditionIsActive(DefDatabase<GameConditionDef>.GetNamedSilentFail(OtherSkyDef))) return GodPaceMath.Trigger.None;
            return OldPowersMath.OtherSkyTrigger(OldPowers.AnyCondition(map, "Aurora"), OldPowers.Quarter(map), OldPowers.DayOfSeason(map), OwnHappenings.Clear(map));
        }

        public static bool OtherSky(OldPowersBook b, Map map)
        {
            var def = DefDatabase<GameConditionDef>.GetNamedSilentFail(OtherSkyDef);
            if (def == null || map.gameConditionManager.ConditionIsActive(def)) return false;
            int hour = GenLocalDate.HourOfDay(map);
            int hours = Math.Max(2, (OldPowersMath.OtherSkyEndsAt + 24 - hour) % 24);
            int ticks = hours * GenDate.TicksPerHour;
            var cond = GameConditionMaker.MakeCondition(def, ticks);
            map.gameConditionManager.RegisterCondition(cond);
            var p = OldPowers.Play(b, OldPower.OtherSky, map, hours, 600);
            p.stage = 1;
            p.form = "age" + OldPowers.Age;
            OldPowers.Card(map, "OtherSky", OldPowers.Aged("OtherSky_Text"), LookTargets.Invalid, LetterDefOf.NeutralEvent);
            b.Came(OldPower.OtherSky, Now, p.form);
            return true;
        }

        internal static void OtherSkyStep(OldPowersBook b, OldPowerPlay p, int now)
        {
            var map = p.map;
            if (!map.gameConditionManager.ConditionIsActive(DefDatabase<GameConditionDef>.GetNamedSilentFail(OtherSkyDef)))
            {
                OldPowers.Minor(map, OldPowers.T("OtherSky_Gone"), LookTargets.Invalid);
                OldPowers.End(b);
                return;
            }
            // Its stars, wrong, over the colony.
            var mid = OwnHappenings.Middle(map);
            for (int i = 0; i < 6; i++)
            {
                var c = CellFinder.RandomClosewalkCellNear(mid, map, 30);
                if (!c.Roofed(map)) OldPowers.Motes(map, c.ToVector3Shifted() + new Vector3(0f, 0f, 2f), Stars, 1, 0.2f, "Mote_Twinkle", "Mote_BlueTwinkle");
            }
            // Those out of doors stop and look up, once, and say so.
            var thought = DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_Thought_OldP_OtherSky");
            int said = 0;
            foreach (var w in map.mapPawns.FreeColonistsSpawned.Where(x => OwnHappenings.Ours(x) && OldPowers.Awake(x) && !x.Position.Roofed(map) && OwnHappenings.Yields(x)).ToList())
            {
                if (thought != null && w.needs?.mood?.thoughts?.memories?.GetFirstMemoryOfDef(thought) != null) continue;
                OwnHappenings.Stand(w, w.Position + IntVec3.North * 5, 900);
                OldPowers.Memory(w, "Rimfolk_Thought_OldP_OtherSky");
                if (said++ < 2) OldPowers.Say(w, "othersky");
            }
            OldPowers.Next(b, p, 600);
        }

        /// <summary>The other sky ended with what was under way (the dev tools, a load): its condition goes too.</summary>
        internal static void EndSky(OldPowerPlay p)
        {
            var def = DefDatabase<GameConditionDef>.GetNamedSilentFail(OtherSkyDef);
            var c = def == null ? null : p?.map?.gameConditionManager?.GetActiveCondition(def);
            if (c != null && !c.Expired) c.End();
        }

        // ================= The pale wolves =================

        /// <summary>A RimWorld of Magic's spirit wolf, drawn running north, east, south and west (OldPowersMath.PackFacing's order).</summary>
        private static readonly string[] WolfMotes = { "Mote_SpiritWolf_North", "Mote_SpiritWolf_East", "Mote_SpiritWolf_South", "Mote_SpiritWolf_West" };

        private static readonly Color Wolf = new Color(0.86f, 0.9f, 0.96f), Glint = new Color(0.86f, 0.92f, 0.55f);

        private static bool AnyoneAwake(Map map) => map.mapPawns.FreeColonistsSpawned.Any(p => OwnHappenings.Ours(p) && OldPowers.Awake(p));

        /// <summary>Winter, or snow lying at the colony's middle.</summary>
        private static bool WinterOrSnow(Map map) => OldPowers.Quarter(map) == StoryWaysMath.Winter || map.snowGrid.GetDepth(OwnHappenings.Middle(map)) > 0.2f;

        public static GodPaceMath.Trigger PackCall(Map map) =>
            OldPowersMath.PackTrigger(OldPowers.FullMoon, OwnHappenings.Clear(map), WinterOrSnow(map), AnyoneAwake(map));

        /// <summary>A point of the pack's ring at a step, inside the land.</summary>
        private static Vector3 RingAt(OldPowerPlay p, float step)
        {
            var (x, z) = OldPowersMath.PackAt(step, p.angle, OldPowersMath.PackRadius);
            var at = p.cell.ToVector3Shifted() + new Vector3(x, 0f, z);
            at.x = Mathf.Clamp(at.x, 1f, p.map.Size.x - 2f);
            at.z = Mathf.Clamp(at.z, 1f, p.map.Size.z - 2f);
            return at;
        }

        public static bool SpiritPack(OldPowersBook b, Map map, bool strong)
        {
            if (!AnyoneAwake(map)) return false;
            var mid = OwnHappenings.Middle(map);
            var p = OldPowers.Play(b, OldPower.SpiritPack, map, 12f, 120);
            p.cell = mid;
            p.angle = Rand.Range(0f, 360f);
            p.value = OldPowersMath.PackCount(Rand.Value);
            p.count = 0;
            p.stage = 1;
            p.strong = strong;
            p.form = strong ? "winter" : "moon";
            var from = RingAt(p, 0f).ToIntVec3();
            OldPowers.Sound(map, from, "TM_WolfHowl", "Pawn_Warg_Call", "Pawn_Dog_Call");
            OldPowers.Card(map, "SpiritPack", OldPowers.T("SpiritPack_Text", p.value.ToString().Named("COUNT"), OldPowers.Toward(mid, from).Named("POINT")),
                new LookTargets(mid, map), LetterDefOf.NeutralEvent);
            b.Came(OldPower.SpiritPack, Now, p.form);
            return true;
        }

        /// <summary>One wolf of the pack at a point, running a way (degrees from north), upright, its drawing by the way it runs.</summary>
        private static void WolfAt(Map map, Vector3 at, int facing, float angle)
        {
            if (!at.ShouldSpawnMotesAt(map)) return;
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(WolfMotes[((facing % 4) + 4) % 4]);
            if (def?.mote == null)
            {
                OldPowers.Drift(map, at, Wolf, angle, OldPowersMath.PackRadius * 0.5f, 0.6f, 0.3f, "Mote_Ghost");
                return;
            }
            try
            {
                if (map.moteCounter.SaturatedLowPriority) return;
                if (!(ThingMaker.MakeThing(def) is Mote m)) return;
                m.Scale = 1.1f;
                m.exactPosition = at;
                m.exactRotation = 0f;
                m.rotationRate = 0f;
                m.solidTimeOverride = 0.3f;
                if (m is MoteThrown thrown) thrown.SetVelocity(angle, OldPowersMath.PackRadius * 0.5f);
                GenSpawn.Spawn(m, at.ToIntVec3(), map);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the old powers: a pale wolf", e);
            }
        }

        internal static void SpiritPackStep(OldPowersBook b, OldPowerPlay p, int now)
        {
            var map = p.map;
            switch (p.stage)
            {
                case 1:
                {
                    // Once round the colony, the wolves one behind another.
                    if (p.count <= OldPowersMath.PackSteps)
                    {
                        for (int i = 0; i < p.value; i++)
                        {
                            float step = p.count - i * 0.45f;
                            if (step < 0f) continue;
                            float run = (p.angle + 360f * step / OldPowersMath.PackSteps + 90f) % 360f;
                            WolfAt(map, RingAt(p, step), OldPowersMath.PackFacing(step, p.angle), run);
                        }
                        if (p.count % 6 == 0) Watch(p, RingAt(p, p.count).ToIntVec3());
                        p.count++;
                        OldPowers.Next(b, p, OldPowersMath.PackStepTicks);
                        return;
                    }
                    // Then off the land, straight out from the colony.
                    var last = RingAt(p, OldPowersMath.PackSteps).ToIntVec3();
                    p.cell2 = (p.cell + (last - p.cell) * 4).ClampInsideMap(map);
                    p.stage = 2;
                    p.count = 0;
                    OldPowers.Next(b, p, OldPowersMath.PackStepTicks);
                    return;
                }
                case 2:
                {
                    if (p.count <= OldPowersMath.PackOffSteps)
                    {
                        var start = RingAt(p, OldPowersMath.PackSteps);
                        var at = Vector3.Lerp(start, p.cell2.ToVector3Shifted(), p.count / (float)OldPowersMath.PackOffSteps);
                        float run = Angle(start.ToIntVec3(), p.cell2);
                        for (int i = 0; i < p.value; i++)
                            WolfAt(map, at + new Vector3(Rand.Range(-1.2f, 1.2f), 0f, Rand.Range(-1.2f, 1.2f)), Mathf.RoundToInt(run / 90f) % 4, run);
                        p.count++;
                        OldPowers.Next(b, p, OldPowersMath.PackStepTicks);
                        return;
                    }
                    OldPowers.Minor(map, OldPowers.T("SpiritPack_Gone", OldPowers.Toward(p.cell, p.cell2).Named("POINT")), new LookTargets(p.cell, map));
                    p.stage = 3;
                    p.count = 0;
                    OldPowers.Next(b, p, 1250);
                    return;
                }
                case 3:
                {
                    // At dawn someone walks out to where they ran and looks at the ground: there are no tracks.
                    int hour = GenLocalDate.HourOfDay(map);
                    if (hour < 6 || hour >= 10)
                    {
                        OldPowers.Next(b, p, 1250);
                        return;
                    }
                    var ring = OldPowers.OpenNear(map, RingAt(p, Rand.Range(0, OldPowersMath.PackSteps)).ToIntVec3(), 4);
                    var go = ring.IsValid ? OldPowers.FreeNear(map, ring) : null;
                    if (go == null || !OwnHappenings.GoTo(go, ring, GenDate.TicksPerHour))
                    {
                        if (hour >= 9) OldPowers.End(b);
                        else OldPowers.Next(b, p, 1250);
                        return;
                    }
                    p.b = go;
                    p.cell2 = ring;
                    p.stage = 4;
                    OldPowers.Next(b, p, 250);
                    return;
                }
                case 4:
                {
                    var go = p.b;
                    if (go == null || go.Dead || !go.Spawned)
                    {
                        OldPowers.End(b);
                        return;
                    }
                    if (!go.Position.InHorDistOf(p.cell2, 2f) && p.count++ < 16)
                    {
                        OldPowers.Next(b, p, 250);
                        return;
                    }
                    OwnHappenings.Stand(go, p.cell2, 500);
                    bool snow = map.snowGrid.GetDepth(p.cell2) > 0.2f;
                    OldPowers.Say(go, snow ? "spiritpack,tracks,snow" : "spiritpack,tracks");
                    OldPowers.Minor(map, OldPowers.T(snow ? "SpiritPack_NoTracksSnow" : "SpiritPack_NoTracks", go.LabelShort.Named("WALKER")), new LookTargets(go));
                    OldPowers.End(b);
                    return;
                }
            }
        }

        /// <summary>The beasts near the pack stand and watch it go by; those of the colony out of doors who see it stop and watch, once.</summary>
        private static void Watch(OldPowerPlay p, IntVec3 at)
        {
            var map = p.map;
            foreach (var a in map.mapPawns.AllPawnsSpawned.Where(x => x.RaceProps.Animal && !x.Dead && !x.Downed && x.Awake() && !x.InMentalState
                                                                       && x.Position.InHorDistOf(at, OldPowersMath.PackSeenBy) && OwnHappenings.Yields(x)).Take(8).ToList())
                OwnHappenings.Stand(a, at, 300);
            var thought = DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_Thought_OldP_SpiritPack");
            foreach (var w in map.mapPawns.FreeColonistsSpawned.Where(x => OwnHappenings.Ours(x) && OldPowers.Awake(x) && !x.Position.Roofed(map) && x.Position.InHorDistOf(at, 40f)
                                                                          && GenSight.LineOfSight(x.Position, at, map, true)).ToList())
            {
                if (thought != null && w.needs?.mood?.thoughts?.memories?.GetFirstMemoryOfDef(thought) != null) continue;
                if (OwnHappenings.Yields(w)) OwnHappenings.Stand(w, at, 600);
                OldPowers.Memory(w, "Rimfolk_Thought_OldP_SpiritPack");
                if (p.b == null)
                {
                    p.b = w;
                    OldPowers.Say(w, "spiritpack,seen");
                }
            }
        }

        // ================= Eyes in the dark =================

        private static bool Fog(Map map) => map.weatherManager?.curWeather?.defName?.IndexOf("Fog", StringComparison.OrdinalIgnoreCase) >= 0;

        public static GodPaceMath.Trigger WatchedCall(Map map) =>
            OldPowersMath.WatchedTrigger(Fog(map), OldPowers.Moonless, OldPowers.Quarter(map), AnyoneAwake(map));

        /// <summary>A dark, open place at the edge of the colony's light, near a point.</summary>
        private static IntVec3 DarkNear(Map map, IntVec3 at)
        {
            if (!at.InBounds(map)) return IntVec3.Invalid;
            foreach (var c in GenRadial.RadialCellsAround(at, 3f, true))
            {
                if (!c.InBounds(map) || c.Fogged(map) || c.Roofed(map) || !c.Standable(map) || c.GetEdifice(map) != null) continue;
                if (map.glowGrid.GroundGlowAt(c) >= OldPowersMath.EyesDarkBelow) continue;
                return c;
            }
            return IntVec3.Invalid;
        }

        public static bool Watched(OldPowersBook b, Map map)
        {
            if (!AnyoneAwake(map)) return false;
            var mid = OwnHappenings.Middle(map);
            int n = OldPowersMath.EyesCount(Rand.Value);
            float start = Rand.Range(0f, 360f);
            var spots = new List<IntVec3>();
            for (int i = 0; i < n; i++)
            {
                float a = (start + i * 360f / n + Rand.Range(-15f, 15f)) * Mathf.Deg2Rad;
                float r = Rand.Range(OldPowersMath.EyesFrom, OldPowersMath.EyesTo);
                var c = DarkNear(map, (mid.ToVector3Shifted() + new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r)).ToIntVec3());
                if (c.IsValid && !spots.Contains(c)) spots.Add(c);
            }
            if (spots.Count < 2) return false;
            var p = OldPowers.Play(b, OldPower.Watched, map, OldPowersMath.EyesHours + 1f, 60);
            p.cells = spots;
            p.cell = mid;
            p.stage = 1;
            p.count = 0;
            p.strong = Fog(map);
            p.form = p.strong ? "fog" : "dark";
            OldPowers.Card(map, "Watched", OldPowers.T("Watched_Text", spots.Count.ToString().Named("COUNT"), (p.strong ? OldPowers.T("Watched_Fog") : OldPowers.T("Watched_Dark")).Named("WHEN")),
                new LookTargets(spots.Select(c => new TargetInfo(c, map))), LetterDefOf.NeutralEvent);
            b.Came(OldPower.Watched, Now, p.form);
            return true;
        }

        /// <summary>A pair of eyes catching the light for a few seconds: two small glints side by side, low to the ground.</summary>
        private static void Eyes(Map map, IntVec3 c)
        {
            var at = c.ToVector3Shifted() + new Vector3(0f, 0f, -0.2f);
            var def = (Def)DefDatabase<ThingDef>.GetNamedSilentFail("Mote_Twinkle_grayscale") ?? GodFx.MoteDef;
            foreach (float dx in new[] { -0.14f, 0.14f })
                GodFx.Throw(def, at + new Vector3(dx, 0f, 0f), map, 0.16f, Glint, 0f, 0f, 0f, 3.6f);
        }

        internal static void WatchedStep(OldPowersBook b, OldPowerPlay p, int now)
        {
            var map = p.map;
            p.cells.RemoveAll(c => !c.InBounds(map));
            if (p.cells.Count == 0)
            {
                OldPowers.End(b);
                return;
            }
            // Daylight, or their hours over: they go out.
            if (now > p.until - GenDate.TicksPerHour || GenCelestial.CurCelestialSunGlow(map) > 0.4f)
            {
                OldPowers.Minor(map, OldPowers.T("Watched_Out"), LookTargets.Invalid);
                OldPowers.End(b);
                return;
            }
            foreach (var c in p.cells) Eyes(map, c);
            switch (p.stage)
            {
                case 1:
                {
                    // Those of the colony out of doors who can see a pair stop and look at it, once.
                    var thought = DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_Thought_OldP_Watched");
                    int said = 0;
                    foreach (var w in map.mapPawns.FreeColonistsSpawned.Where(x => OwnHappenings.Ours(x) && OldPowers.Awake(x) && !x.Position.Roofed(map)).ToList())
                    {
                        if (thought != null && w.needs?.mood?.thoughts?.memories?.GetFirstMemoryOfDef(thought) != null) continue;
                        var seen = p.cells.Where(c => w.Position.InHorDistOf(c, 32f) && GenSight.LineOfSight(w.Position, c, map, true)).OrderBy(c => c.DistanceToSquared(w.Position)).ToList();
                        if (seen.Count == 0) continue;
                        if (OwnHappenings.Yields(w)) OwnHappenings.Stand(w, seen[0], 600);
                        OldPowers.Memory(w, "Rimfolk_Thought_OldP_Watched");
                        if (said++ < 2) OldPowers.Say(w, "watched,eyes");
                    }
                    // After a while the one handiest in a fight, awake and free, walks out to the nearest pair.
                    if (++p.count >= OldPowersMath.EyesWalkAfter && p.b == null)
                    {
                        var go = Bravest(map, p.cells);
                        var spot = go == null ? IntVec3.Invalid : p.cells.OrderBy(c => c.DistanceToSquared(go.Position)).First();
                        if (go != null && OwnHappenings.GoTo(go, spot, GenDate.TicksPerHour))
                        {
                            p.b = go;
                            p.cell2 = spot;
                            p.stage = 2;
                            p.count = 0;
                            OldPowers.Say(go, "watched,walk");
                        }
                    }
                    OldPowers.Next(b, p, OldPowersMath.EyesEvery);
                    return;
                }
                case 2:
                {
                    var go = p.b;
                    bool gone = go == null || go.Dead || !go.Spawned || go.Downed;
                    if (!gone && !go.Position.InHorDistOf(p.cell2, OldPowersMath.EyesGoOutAt) && p.count++ < 30)
                    {
                        if (go.CurJobDef != JobDefOf.Goto) OwnHappenings.GoTo(go, p.cell2, GenDate.TicksPerHour);
                        OldPowers.Next(b, p, 120);
                        return;
                    }
                    if (gone || !go.Position.InHorDistOf(p.cell2, OldPowersMath.EyesGoOutAt))
                    {
                        // They didn't get there: the eyes stay, and nobody else is sent.
                        p.stage = 3;
                        OldPowers.Next(b, p, OldPowersMath.EyesEvery);
                        return;
                    }
                    // As they come close every pair goes out at once; there is nothing there.
                    p.cells.Clear();
                    OwnHappenings.Stand(go, p.cell2, 600);
                    OldPowers.Sound(map, p.cell2, "TM_WindLowSD", "TM_AirWooshLow");
                    OldPowers.Say(go, "watched,nothing");
                    OldPowers.Minor(map, OldPowers.T("Watched_Nothing", go.LabelShort.Named("WALKER")), new LookTargets(go));
                    OldPowers.End(b);
                    return;
                }
                default:
                    // The eyes stay till their hours are over.
                    OldPowers.Next(b, p, OldPowersMath.EyesEvery);
                    return;
            }
        }

        /// <summary>The one of the colony awake and free to go who is handiest in a fight (shooting or melee), the nearer first among equals; never a child.</summary>
        private static Pawn Bravest(Map map, List<IntVec3> spots) =>
            map.mapPawns.FreeColonistsSpawned.Where(x => OwnHappenings.Ours(x) && OldPowers.Awake(x) && OwnHappenings.Yields(x) && x.DevelopmentalStage.Adult()
                                                         && spots.Any(c => x.CanReach(c, PathEndMode.OnCell, Danger.Some)))
                .OrderByDescending(x => Math.Max(x.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0, x.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0))
                .ThenBy(x => spots.Min(c => c.DistanceToSquared(x.Position))).FirstOrDefault();
    }

    /// <summary>
    /// Another sky (OldPowersNight.OtherSky): the night's sky in colours of its own, violet over and green-white in the
    /// shadows, faded in and out; stargazing three times as likely and the gladder for it. Only at night: by day it is the
    /// sky as it is.
    /// </summary>
    public class GameCondition_OtherSky : GameCondition
    {
        private static readonly Color Sky = new Color(0.42f, 0.3f, 0.72f), Shade = new Color(0.32f, 0.62f, 0.52f), Overlay = new Color(0.62f, 0.52f, 0.9f);

        public override float SkyGazeChanceFactor(Map map) => 3f;

        public override float SkyGazeJoyGainFactor(Map map) => 1.5f;

        private float Night(Map map) => Mathf.InverseLerp(0.3f, 0.08f, GenCelestial.CurCelestialSunGlow(map));

        public override float SkyTargetLerpFactor(Map map)
        {
            try
            {
                return 0.85f * Night(map) * GameConditionUtility.LerpInOutValue(this, OldPowersMath.OtherSkyFade);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the old powers: another sky's blend", e);
                return 0f;
            }
        }

        public override SkyTarget? SkyTarget(Map map)
        {
            try
            {
                var cur = map.skyManager.CurSky;
                var colors = new SkyColorSet(Sky, Shade, Overlay, 1.15f);
                return new SkyTarget(Mathf.Max(cur.glow, 0.12f), colors, cur.lightsourceShineSize, cur.lightsourceShineIntensity);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the old powers: another sky", e);
                return null;
            }
        }
    }
}
