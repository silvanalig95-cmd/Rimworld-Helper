using System;
using System.Linq;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: each god's own happenings now ("Rimfolk", "Gods: &lt;god&gt;'s &lt;happening&gt; now"), one for each, on the map
    /// being looked at, past every gap and chance (what it needs on the map it still needs: a dryad, a console, a turret in a
    /// fight); and what each has under way (log).
    /// </summary>
    public static class OwnHappeningsDebug
    {
        private static void Say(string text) => Messages.Message("Gods: " + text, MessageTypeDefOf.SilentInput, false);

        private static Map Map => Find.CurrentMap;

        private static Pawn AnyColonist => Map?.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => GodOmens.Awake(p)) ?? Map?.mapPawns.FreeColonistsSpawned.FirstOrDefault();

        /// <summary>Runs one happening now, every gap and chance giving way; says what came of it.</summary>
        private static void Now(OwnHappening h, Func<OwnHappeningsBook, God, Map, int, bool> fire, string none)
        {
            var b = OwnHappenings.Book;
            var g = OwnHappenings.GodOf(h);
            var map = Map;
            if (b == null || map == null)
            {
                Say("the gods are off, or no game.");
                return;
            }
            if (g == null)
            {
                Say("no " + OwnHappeningsMath.GodOf(h) + " in this pantheon.");
                return;
            }
            OwnHappenings.forced = h;
            bool done;
            try
            {
                done = fire(b, g, map, Find.TickManager.TicksGame);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods' own happenings: a dev action", e);
                done = false;
            }
            finally
            {
                OwnHappenings.forced = null;
            }
            Say(done ? Gods.Name(g) + "'s " + OwnHappeningsMath.Name(h).ToLowerInvariant() + ": under way." : none);
        }

        // ================= Hollin =================

        [DebugAction("Rimfolk", "Gods: Hollin's roots through the floor now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void HollinRoots() => Now(OwnHappening.HollinRoots, (b, g, m, n) => OwnHollin.Roots(b, m, n, true), "no room of the colony's for the roots.");

        [DebugAction("Rimfolk", "Gods: Hollin's dryads' word now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void HollinDryads() => Now(OwnHappening.HollinDryads, (b, g, m, n) => OwnHollin.Word(b, g, m, n),
            "the dryads need an anima or gauranlen tree, a dryad, someone outdoors, and something real to point at (trees marked to cut, frost ahead, a bare field, blight).");

        [DebugAction("Rimfolk", "Gods: Hollin's green walk now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void HollinWalk() => Now(OwnHappening.HollinWalk, (b, g, m, n) =>
        {
            if (!GodRising.Rise(m, g)) return false;
            return b.walkLeft > 0;
        }, "she couldn't rise (it must be day, with open fertile ground), or her walk is already under way.");

        [DebugAction("Rimfolk", "Gods: Hollin's old tree spared now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void HollinOldTree() => Now(OwnHappening.HollinOldTree, (b, g, m, n) => OwnHollin.Ring(b, g, m, b.oldTree != null && b.oldTree.Spawned && b.oldTree.Map == m ? b.oldTree : OwnHollin.Oldest(m), n),
            "no tree near the colony, or no room round it.");

        // ================= Smoulder =================

        [DebugAction("Rimfolk", "Gods: Smoulder's bargain at the shaft now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SmoulderBargain() => Now(OwnHappening.SmoulderBargain, (b, g, m, n) =>
        {
            b.bargainStage = 0;
            return OwnSmoulder.Offer(b, m, Math.Max(OwnHappeningsMath.BargainMinHeld, OwnSmoulder.Silver(m)), n) && OwnSmoulder.Ask(b, true);
        }, "no one to dream it, or the card couldn't come.");

        [DebugAction("Rimfolk", "Gods: Smoulder's vein after the bargain now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SmoulderVein() => Now(OwnHappening.SmoulderBargain, (b, g, m, n) =>
        {
            if (b.bargainStage != 4)
            {
                b.bargainMap = m;
                b.shaft = OwnSmoulder.Shaft(m, AnyColonist);
                b.promised = 200;
                b.thrown = 200;
            }
            return OwnSmoulder.Vein(b, n);
        }, "no rock near the shaft for a vein.");

        [DebugAction("Rimfolk", "Gods: Smoulder's proper name on a sculpture now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SmoulderName() => Now(OwnHappening.SmoulderName, (b, g, m, n) =>
        {
            var art = m.listerThings.AllThings.FirstOrDefault(t => t.Spawned && t.def.IsArt && t.TryGetComp<CompArt>() != null)?.TryGetComp<CompArt>();
            var by = AnyColonist;
            if (by == null || !OwnSmoulder.Name(b, g, m, art, by, n)) return false;
            b.nameDue = n;
            return true;
        }, "no colonist (and no sculpture on the map to name, which is fine: the seam still comes).");

        [DebugAction("Rimfolk", "Gods: Smoulder's heat from below now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SmoulderHeat() => Now(OwnHappening.SmoulderHeat, (b, g, m, n) =>
        {
            var at = GodOmens.UnderMountain(m);
            if (!at.IsValid) return false;
            OwnSmoulder.Heat(b, g, m, at, AnyColonist, n);
            return true;
        }, "nowhere under thick rock on this map.");

        [DebugAction("Rimfolk", "Gods: Smoulder's stretching dream now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SmoulderStretch() => Now(OwnHappening.SmoulderStretch, (b, g, m, n) => OwnSmoulder.Stretch(b, m, n, true), "no miners (Mining 6, or a passion for it).");

        [DebugAction("Rimfolk", "Gods: Smoulder's warm dream's burn now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SmoulderBurn() => Now(OwnHappening.SmoulderBurn, (b, g, m, n) => OwnSmoulder.Burn(b, OwnSmoulder.Miners(m).FirstOrDefault() ?? AnyColonist, n, true), "no colonist.");

        // ================= Bellwether =================

        [DebugAction("Rimfolk", "Gods: Bellwether's road through the colony now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void BellwetherRoad() => Now(OwnHappening.BellwetherRoad, (b, g, m, n) =>
        {
            var s = GodsOnMapBook.Instance?.beast;
            if (s?.a == null || GodShapes.LineTag(s.God) != OwnBellwether.Tag)
            {
                var mb = GodsOnMapBook.Instance;
                if (mb == null || mb.beast != null || SacredAnimal.Bring(mb, m, g, null) == null) return false;
                s = mb.beast;
            }
            b.roadBeast = null;
            if (!OwnBellwether.Road(s, s.a, n)) return false;
            b.roadStage = 2;
            return OwnBellwether.Road(s, s.a, n);
        }, "the white beast couldn't come, or another sacred beast is on the land.");

        [DebugAction("Rimfolk", "Gods: Bellwether's horn child foretells rain now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void BellwetherHorn() => Now(OwnHappening.BellwetherHorn, (b, g, m, n) =>
        {
            var p = b.horned.FirstOrDefault(x => x.Spawned && x.Map == m) ?? m.mapPawns.FreeColonistsSpawned.Where(x => x.DevelopmentalStage.Child()).FirstOrDefault() ?? AnyColonist;
            if (p == null) return false;
            OwnBellwether.Touched(p);
            OwnBellwether.Foretell(b, g, p, false, n);
            return true;
        }, "no one to say it.");

        [DebugAction("Rimfolk", "Gods: Bellwether's wounded thrumbo at the edge now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void BellwetherHurt() => Now(OwnHappening.BellwetherHurt, (b, g, m, n) => b.hurt == null && OwnBellwether.Hurt(b, m, n), "a wounded thrumbo is already here, or none could come.");

        [DebugAction("Rimfolk", "Gods: Bellwether's last of its kind dream now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void BellwetherLast() => Now(OwnHappening.BellwetherLast, (b, g, m, n) => OwnBellwether.Dream(b, m, n, true), "no one to dream it.");

        // ================= Morel =================

        [DebugAction("Rimfolk", "Gods: Morel's mourner at the graveyard now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void MorelMourner() => Now(OwnHappening.MorelMourner, (b, g, m, n) => OwnMorel.Mourn(b, m, n), "no graves on this map, or no room at their edge.");

        [DebugAction("Rimfolk", "Gods: Morel's mourner gone at dawn now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void MorelMournerGone() => Now(OwnHappening.MorelMourner, (b, g, m, n) =>
        {
            if (b.mournerUntil < 0) return false;
            b.mournerUntil = n;
            OwnMorel.Hourly(b, n);
            return true;
        }, "no mourner standing (bring one first).");

        [DebugAction("Rimfolk", "Gods: Morel's ring of mushrooms now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void MorelRing() => Now(OwnHappening.MorelRing, (b, g, m, n) => OwnMorel.RingNow(b, m, n), "no ground for a ring.");

        [DebugAction("Rimfolk", "Gods: Morel's white threads before the freezer fails now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void MorelThreads() => Now(OwnHappening.MorelThreads, (b, g, m, n) =>
        {
            b.warnDue = -1;
            return OwnHappenings.Forewarn(g, m, GodWrath.Breakdown);
        }, "no working cooler on this map.");

        [DebugAction("Rimfolk", "Gods: Morel's breath in the ring now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void MorelBreath() => Now(OwnHappening.MorelBreath, (b, g, m, n) =>
        {
            var p = AnyColonist;
            if (p == null) return false;
            OwnMorel.Heard(b, g, p, n);
            return true;
        }, "no colonist.");

        // ================= Ennu =================

        [DebugAction("Rimfolk", "Gods: Ennu's small test now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void EnnuTest() => Now(OwnHappening.EnnuTest, (b, g, m, n) =>
        {
            var s = GodsOnMapBook.Instance?.stranger;
            if (s?.x == null || s.phase != StrangerPhase.Staying) return false;
            b.testOf = s.x;
            b.testStage = 0;
            return OwnEnnu.Ask(b, s, s.x, n);
        }, "no stranger staying (\"Gods: a stranger at the gate now...\" and give them a bed first), or nothing to ask.");

        [DebugAction("Rimfolk", "Gods: Ennu's tale by the fire now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void EnnuTales() => Now(OwnHappening.EnnuTales, (b, g, m, n) =>
        {
            var s = GodsOnMapBook.Instance?.stranger;
            var listener = s?.x == null ? null : Together.Company(s.x) ?? AnyColonist;
            return s?.x != null && listener != null && OwnEnnu.TellTale(b, s.x, listener, s.God ?? g, n);
        }, "no stranger staying, or no story left to tell.");

        [DebugAction("Rimfolk", "Gods: Ennu's bowl set out now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void EnnuBowl() => Now(OwnHappening.EnnuBowl, (b, g, m, n) => b.bowlStage == 0 && OwnEnnu.Bowl(b, g, m, n), "needs a cook, a dining table with nothing on it and a meal in storage.");

        [DebugAction("Rimfolk", "Gods: Ennu's bowl found empty now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void EnnuBowlMorning() => Now(OwnHappening.EnnuBowl, (b, g, m, n) => b.bowlStage == 2 && OwnEnnu.Morning(b, n), "no bowl set down on a table yet.");

        [DebugAction("Rimfolk", "Gods: Ennu's kindest sought out now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void EnnuKindest() => Now(OwnHappening.EnnuKindest, (b, g, m, n) =>
        {
            var k = b.kindest != null && b.kindest.Spawned && b.kindest.Map == m ? b.kindest : AnyColonist;
            var v = m.mapPawns.AllPawnsSpawned.FirstOrDefault(x => x.RaceProps?.Humanlike == true && x.Faction != null && !x.Faction.IsPlayer && !x.HostileTo(Faction.OfPlayer) && !x.Downed);
            if (k == null || v == null) return false;
            b.kindest = k;
            b.kindestUntil = n + GenDate.TicksPerDay * 15;
            return OwnEnnu.Seek(b, v, k, n);
        }, "needs a visitor on the map.");

        // ================= Pip =================

        [DebugAction("Rimfolk", "Gods: Pip's machine that won't stop now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void PipWontStop() => Now(OwnHappening.PipWontStop, (b, g, m, n) =>
        {
            var comp = m.listerBuildings.allBuildingsColonist.Select(t => t.TryGetComp<CompFlickable>()).FirstOrDefault(c => c != null && c.parent.GetComp<CompPowerTrader>() != null && c.SwitchIsOn);
            if (comp == null || b.wontStop != null) return false;
            comp.SwitchIsOn = false;
            return OwnPip.WontStop(b, g, comp, n);
        }, "no switched-on machine of the colony's (or one already running on).");

        [DebugAction("Rimfolk", "Gods: Pip's message to the stars now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void PipStars() => Now(OwnHappening.PipStars, (b, g, m, n) => OwnPip.Night(b, m, n) && OwnPip.Morning(b, m, n), "no powered comms console.");

        [DebugAction("Rimfolk", "Gods: Pip's turret holds its fire (the next crossing)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void PipTurret() => Now(OwnHappening.PipTurret, (b, g, m, n) =>
        {
            b.Came(OwnHappening.PipTurret, -999999);
            OwnPip.Watching = true;
            return m.listerBuildings.allBuildingsColonist.Any(t => t is Building_TurretGun);
        }, "no turret of the colony's.");

        [DebugAction("Rimfolk", "Gods: Pip's sulking lights before a short circuit now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void PipSulk() => Now(OwnHappening.PipSulk, (b, g, m, n) =>
        {
            b.warnDue = -1;
            return OwnHappenings.Forewarn(g, m, GodWrath.ShortCircuit);
        }, "no lit powered light.");

        // ================= Cirrus =================

        [DebugAction("Rimfolk", "Gods: Cirrus's star for the watchers now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void CirrusStar() => Now(OwnHappening.CirrusStar, (b, g, m, n) =>
        {
            b.festivalKept = n;
            b.watcher ??= AnyColonist;
            return GodHealth.Bless(g, m, GodBlessing.Meteor);
        }, "the meteorite couldn't fall here.");

        [DebugAction("Rimfolk", "Gods: Cirrus's wrong-way star dream now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void CirrusFall() => Now(OwnHappening.CirrusFall, (b, g, m, n) =>
        {
            b.fallStage = 0;
            OwnCirrus.WrongStar(m, AnyColonist);
            if (b.fallStage != 1 || !OwnCirrus.Dream(b, n, true)) return false;
            b.fallDue = n + 250;
            return true;
        }, "no open ground, or no one to dream.");

        [DebugAction("Rimfolk", "Gods: Cirrus's aurora on the coldest night now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void CirrusAurora() => Now(OwnHappening.CirrusAurora, (b, g, m, n) => OwnCirrus.Light(b, g, m, m.mapPawns.FreeColonistsSpawned.ToList(), n), "an aurora is already in the sky.");

        [DebugAction("Rimfolk", "Gods: Cirrus's dry thunder now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void CirrusThunder() => Now(OwnHappening.CirrusThunder, (b, g, m, n) => OwnCirrus.Thunder(b, m, n), "the thunder is already rolling.");

        // ================= The log =================

        [DebugAction("Rimfolk", "Gods: each god's own happenings (log)", allowedGameStates = AllowedGameStates.Playing)]
        private static void Log()
        {
            var b = OwnHappenings.Book;
            if (b == null)
            {
                Say("the gods are off.");
                return;
            }
            int now = Find.TickManager.TicksGame;
            var lines = OwnHappeningsMath.All.Select(h => "  " + h + ": " + (b.Last(h) < -900000 ? "never" : GodPace.DaysSince(b.Last(h), now) + " days ago")).ToList();
            lines.Add("  under way: roots " + b.rootsLeft + ", walk " + b.walkLeft + ", bargain stage " + b.bargainStage + ", road stage " + b.roadStage + ", hurt " + (b.hurt?.LabelShort ?? "none")
                      + ", mourner " + (b.mournerUntil >= 0) + ", ring " + b.ringAt + ", warning " + b.warnWrath + " " + b.warnDue + ", test " + b.test + " " + b.testStage + ", bowl " + b.bowlStage
                      + ", stars " + b.starsStage + ", fall " + b.fallStage + ", thunder " + b.thunderLeft + ", horn children " + b.horned.Count + ", sky watched " + b.watches.Count);
            Verse.Log.Message("[Rimfolk] Each god's own happenings:\n" + string.Join("\n", lines));
            Say("logged.");
        }
    }
}
