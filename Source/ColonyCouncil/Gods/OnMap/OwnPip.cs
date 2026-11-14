using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Pip, the Hum, on the map (docs/gods-next-proposals.md, C 6): machines, power, turrets. A colony with no machines never
    /// meets any of it, so a tribal colony's Pip does nothing here.
    /// <list type="bullet">
    /// <item>The machine that would not stop: one of the colony switches a machine off, and it keeps running an hour to show
    /// it can, then stops on its own with a shudder of sparks.</item>
    /// <item>Its message to the stars ("calls to the parents"): the comms console comes on by itself in the small hours, and in
    /// the morning a half-written message to the stars is on its screen; with an ancient terminal on the land, that hums back.</item>
    /// <item>The turret that held fire: in a fight one of the colony's turrets holds its fire for a breath as a colonist
    /// crosses its line, and the colonist says so.</item>
    /// <item>Sulking, with a warning: the day before its short circuit (its anger, the game's own event), the lights flicker
    /// slow and sad.</item>
    /// <item>Mended overnight (its kindness, GodHealth), shown: the machine's light winks as it comes back.</item>
    /// </list>
    /// </summary>
    public static class OwnPip
    {
        public const string Key = "Pip";
        public const string TurretThought = "Rimfolk_Thought_GodOwnTurretHeld";
        public const int Messages = 6;

        private static readonly string[] Terminals = { "AncientTerminal", "AncientTerminal_Worshipful", "VFEPD_AncientTerminal" };

        private static God It => OwnHappenings.God(Key);

        private static int Now => OwnHappenings.Now;

        // ================= The machine that would not stop =================

        /// <summary>A switch flicked (Patch_OwnFlicked): switched off, the machine may run on an hour.</summary>
        internal static void Flicked(CompFlickable comp)
        {
            var b = OwnHappenings.Book;
            var t = comp?.parent;
            var g = It;
            if (b == null || g == null || t == null || !t.Spawned || comp.SwitchIsOn || b.wontStop != null || t.Faction != Faction.OfPlayer || t.GetComp<CompPowerTrader>() == null) return;
            var map = t.Map;
            if (!Homes.IsHome(map) || !OwnHappenings.Acts(g, map)) return;
            int now = Now;
            if (OwnHappenings.forced != OwnHappening.PipWontStop
                && !Rand.Chance(OwnHappeningsMath.FlickChance(OwnHappenings.DaysSince(b, OwnHappening.PipWontStop, now), true, GodNear.Of(g, map))))
                return;
            WontStop(b, g, comp, now);
        }

        internal static bool WontStop(OwnHappeningsBook b, God g, CompFlickable comp, int now)
        {
            var t = comp.parent;
            var map = t.Map;
            try
            {
                comp.SwitchIsOn = true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a machine that won't stop", e);
                return false;
            }
            b.wontStop = t;
            b.wontStopUntil = now + OwnHappeningsMath.WontStopTicks;
            b.Due(b.wontStopUntil);
            OwnHappenings.Came(b, OwnHappening.PipWontStop, now);
            for (int i = 0; i < 3; i++) FleckMaker.ThrowMicroSparks(t.TrueCenter(), map);
            var by = map.mapPawns.FreeColonistsSpawned.Where(p => p.Position.InHorDistOf(t.Position, 3f)).OrderBy(p => p.Position.DistanceToSquared(t.Position)).FirstOrDefault()
                     ?? OwnHappenings.Witness(map, t.Position, 12f);
            OwnHappenings.Say(by, "wontstop", g, null, t.LabelNoCount.Named("THING"));
            OwnHappenings.Card(g, map, "WontStop", false, new LookTargets(t), null, Importance.Notable, t.LabelNoCount.Named("THING"));
            OwnHappenings.Saw(g, map, t.Position, GodActKind.Act, by, "CC_GodO_Saw_WontStop", t.LabelNoCount.Named("THING"));
            return true;
        }

        /// <summary>Its hour over: it stops on its own, with a shudder of sparks.</summary>
        private static void WontStopStep(OwnHappeningsBook b, int now)
        {
            var t = b.wontStop;
            if (t == null) return;
            if (now < b.wontStopUntil)
            {
                b.Due(b.wontStopUntil);
                return;
            }
            b.wontStop = null;
            if (t.Destroyed || !t.Spawned) return;
            var comp = t.TryGetComp<CompFlickable>();
            try
            {
                if (comp != null) comp.SwitchIsOn = false;
                for (int i = 0; i < 4; i++) FleckMaker.ThrowMicroSparks(t.TrueCenter(), t.Map);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a machine stopping on its own", e);
            }
            OwnHappenings.Say(OwnHappenings.Witness(t.Map, t.Position, 12f), "stopped", It, null, t.LabelNoCount.Named("THING"));
        }

        // ================= Its message to the stars =================

        private static Building Console(Map map) =>
            map.listerBuildings.allBuildingsColonist.FirstOrDefault(c => c is Building_CommsConsole && c.Spawned && (c.GetComp<CompPowerTrader>()?.PowerOn ?? true));

        private static Thing Terminal(Map map)
        {
            foreach (var n in Terminals)
            {
                var def = DefDatabase<ThingDef>.GetNamedSilentFail(n);
                var t = def == null ? null : map.listerThings.ThingsOfDef(def).FirstOrDefault(x => x.Spawned && !x.Position.Fogged(map));
                if (t != null) return t;
            }
            return null;
        }

        /// <summary>In the small hours: the console comes on by itself (its sparks and glow), and an old terminal on the land hums back.</summary>
        internal static bool Night(OwnHappeningsBook b, Map map, int now)
        {
            var g = It;
            var console = Console(map);
            if (g == null || console == null) return false;
            b.starsConsole = console;
            b.starsMsg = Rand.Range(0, Messages);
            b.starsStage = 2;
            try
            {
                for (int i = 0; i < 5; i++) FleckMaker.ThrowMicroSparks(console.TrueCenter(), map);
                FleckMaker.ThrowLightningGlow(console.TrueCenter(), map, 1.2f);
                OwnHappenings.Motes(map, console.TrueCenter(), OwnHappenings.PsiCurrent, new Color(0.6f, 1f, 0.75f), 4);
                GodSounds.Play(GodSounds.Sound("CommsWindow_Open"), map, console.Position)?.End();
                var term = Terminal(map);
                if (term != null)
                {
                    for (int i = 0; i < 4; i++) FleckMaker.ThrowMicroSparks(term.TrueCenter(), map);
                    FleckMaker.ThrowLightningGlow(term.TrueCenter(), map, 1f);
                    GodSounds.Play(GodSounds.Sound("Power_OnSmall"), map, term.Position)?.End();
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the console in the night", e);
            }
            var awake = OwnHappenings.Witness(map, console.Position, 15f);
            OwnHappenings.Say(awake, "starsnight", g);
            OwnHappenings.Saw(g, map, console.Position, GodActKind.Omen, awake, "CC_GodO_Saw_StarsNight");
            return true;
        }

        /// <summary>In the morning: the half-written message on its screen, read where it stands and by whoever comes to it.</summary>
        internal static bool Morning(OwnHappeningsBook b, Map map, int now)
        {
            var g = It;
            var console = b.starsConsole;
            b.starsStage = 0;
            if (g == null || console == null || console.Destroyed || !console.Spawned) return false;
            string message = ("CC_GodO_StarsMessage_" + Mathf.Clamp(b.starsMsg, 0, Messages - 1)).Translate().Resolve();
            bool term = Terminal(map) != null;
            try
            {
                // The screen still lit with it: its words are on the card and in the reader's mouth.
                FleckMaker.ThrowLightningGlow(console.TrueCenter(), map, 0.8f);
                OwnHappenings.Motes(map, console.TrueCenter(), OwnHappenings.PsiCurrent, new Color(0.6f, 1f, 0.75f), 3, 0.4f);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the message on the screen", e);
            }
            OwnHappenings.Card(g, map, term ? "StarsTerminal" : "Stars", true, new LookTargets(console), LetterDefOf.NeutralEvent, Importance.Notable, message.Named("MESSAGE"));
            var seer = OwnHappenings.Found(b, map, console.Position, "starsmorning", g, message);
            OwnHappenings.Saw(g, map, console.Position, GodActKind.Omen, seer, "CC_GodO_Saw_Stars", message.Named("MESSAGE"));
            return true;
        }

        // ================= The turret that held fire =================

        /// <summary>Pip may hold a turret's fire now (looked at each hour): it acts at a home, its gap is past, the colony has turrets.</summary>
        internal static bool Watching;

        private static readonly AccessTools.FieldRef<Building_TurretGun, int> Cooldown = AccessTools.FieldRefAccess<Building_TurretGun, int>("burstCooldownTicksLeft");
        private static readonly AccessTools.FieldRef<Building_TurretGun, int> Warmup = AccessTools.FieldRefAccess<Building_TurretGun, int>("burstWarmupTicksLeft");

        /// <summary>A turret of the colony's about to fire (Patch_OwnTurretHeld): one of the colony across its line, and it holds a breath. True when it held.</summary>
        internal static bool HoldFire(Building_TurretGun turret)
        {
            if (turret == null || !turret.Spawned || turret.Faction != Faction.OfPlayer) return false;
            var target = turret.CurrentTarget;
            if (!target.IsValid || !target.HasThing) return false;
            var map = turret.Map;
            var from = turret.DrawPos;
            var to = target.Thing.DrawPos;
            foreach (var p in map.mapPawns.FreeColonistsSpawned)
            {
                if (p == target.Thing || p.Downed || !p.Position.InHorDistOf(turret.Position, 40f)) continue;
                if (!OwnHappeningsMath.CrossesLine(from.x, from.z, to.x, to.z, p.DrawPos.x, p.DrawPos.z)) continue;
                if (OwnHappenings.forced != OwnHappening.PipTurret && !Rand.Chance(OwnHappeningsMath.TurretChance)) return false;
                Held(turret, p);
                return true;
            }
            return false;
        }

        /// <summary>It held: a breath without a shot, the colonist says so, and the colony's book keeps it.</summary>
        internal static void Held(Building_TurretGun turret, Pawn p)
        {
            var b = OwnHappenings.Book;
            var g = It;
            Watching = false;
            try
            {
                Cooldown(turret) = Math.Max(Cooldown(turret), OwnHappeningsMath.HoldTicks);
                Warmup(turret) = 0;
                FleckMaker.ThrowMicroSparks(turret.TrueCenter(), turret.Map);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a turret holding its fire", e);
            }
            if (b == null || g == null) return;
            int now = Now;
            OwnHappenings.Came(b, OwnHappening.PipTurret, now, null, p.ThingID);
            OwnHappenings.Say(p, "turret", g);
            OwnHappenings.Memory(p, TurretThought);
            OwnHappenings.Saw(g, turret.Map, p.Position, GodActKind.Blessing, p, "CC_GodO_Saw_Turret", turret.LabelNoCount.Named("THING"));
        }

        // ================= Sulking, with a warning =================

        /// <summary>Its anger forewarned (OwnHappenings.Forewarn): the colony's lights flicker slow and sad. False with no lights.</summary>
        internal static bool Sulk(OwnHappeningsBook b, God g, Map map)
        {
            var lights = GodOmens.Powered(map, true).OrderBy(_ => Rand.Value).Take(6).ToList();
            int n = lights.Sum(l => GodOmens.Play(map, l, GenDate.TicksPerHour * 6, false, false));
            if (n == 0) return false;
            var at = lights[0].Position;
            b.warnAt = at;
            OwnHappenings.Came(b, OwnHappening.PipSulk, Now);
            OwnHappenings.Card(g, map, "Sulk", false, new LookTargets(lights.Select(l => new GlobalTargetInfo(l))));
            var seer = OwnHappenings.Found(b, map, at, "sulk", g);
            OwnHappenings.Saw(g, map, at, GodActKind.Omen, seer, "CC_GodO_Saw_Sulk");
            return true;
        }

        // ================= Mended overnight, shown =================

        /// <summary>Its kindness mended a machine (GodHealth.Bless): the machine's light winks as it comes back (sparks where it has none).</summary>
        public static void Wink(Thing mended)
        {
            if (mended == null || !mended.Spawned) return;
            try
            {
                if (mended is ThingWithComps tw && tw.GetComp<CompGlower>()?.Glows == true) GodOmens.Play(mended.Map, tw, 400, true, false);
                else
                    for (int i = 0; i < 3; i++)
                        FleckMaker.ThrowMicroSparks(mended.TrueCenter(), mended.Map);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a mended machine winking", e);
            }
        }

        // ================= The day, the hour, the steps =================

        internal static void Daily(OwnHappeningsBook b, int now)
        {
            var g = It;
            if (g == null || b.starsStage != 0) return;
            foreach (var map in OwnHappenings.HomeMaps)
            {
                if (!OwnHappenings.Acts(g, map)) continue;
                var why = OwnHappeningsMath.StarsTrigger(Console(map) != null, GodPace.DaysSince(b.consoleUsed, now), Terminal(map) != null, GodNear.Of(g, map));
                if (!OwnHappenings.Due(b, OwnHappening.PipStars, g, map, why, now)) continue;
                b.starsConsole = Console(map);
                b.starsStage = 1;
                OwnHappenings.Came(b, OwnHappening.PipStars, now);
                return;
            }
        }

        internal static void Hourly(OwnHappeningsBook b, int now)
        {
            var g = It;
            Watching = false;
            if (g == null) return;
            foreach (var map in OwnHappenings.HomeMaps)
            {
                if (!OwnHappenings.Acts(g, map)) continue;
                if (map.mapPawns.FreeColonistsSpawned.Any(p => p.CurJobDef?.defName == "UseCommsConsole")) b.consoleUsed = now;
                if (OwnHappeningsMath.Free(OwnHappening.PipTurret, OwnHappenings.DaysSince(b, OwnHappening.PipTurret, now)) && map.listerBuildings.allBuildingsColonist.Any(t => t is Building_TurretGun))
                    Watching = true;
            }
            var console = b.starsConsole;
            if (b.starsStage > 0 && (console == null || console.Destroyed || !console.Spawned))
            {
                b.starsStage = 0;
                return;
            }
            if (b.starsStage == 1)
            {
                int h = OwnHappenings.Hour(console.Map);
                if (h >= OwnHappeningsMath.NightFrom && h <= OwnHappeningsMath.NightTo && OwnHappenings.Quiet(console.Map)) Night(b, console.Map, now);
                else if (now - b.Last(OwnHappening.PipStars) > GenDate.TicksPerDay * 2) b.starsStage = 0;
            }
            else if (b.starsStage == 2)
            {
                int h = OwnHappenings.Hour(console.Map);
                if (h >= OwnHappeningsMath.MorningFrom && h <= OwnHappeningsMath.MorningTo) Morning(b, console.Map, now);
            }
        }

        internal static void Step(OwnHappeningsBook b, int now) => WontStopStep(b, now);
    }
}
