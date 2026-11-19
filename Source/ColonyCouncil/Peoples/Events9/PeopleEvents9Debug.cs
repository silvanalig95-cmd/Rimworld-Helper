using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: the peoples' own events, round 9 ("Rimfolk", "Peoples' events 9: ..."). What the record keeps; one of their camps here now
    /// (the mech yard, the listening ring, the surface scouts), whatever its own trigger; a listener for a mind in a long break now; a dream
    /// for the linked now; the vault's fight now (the colony's caravan at the vault's town); the console's next evening now; a camp of
    /// theirs turns now. The drawn events are fired with the game's own incident tool (Rimfolk_Evt_LatticeLinked and the rest).
    /// </summary>
    public static class PeopleEvents9Debug
    {
        private static void Say(string text) => Messages.Message("Peoples' events 9: " + text, MessageTypeDefOf.SilentInput, false);

        [DebugAction("Rimfolk", "Peoples' events 9: what the record keeps (log)", allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugState()
        {
            var b = PeopleEvents9Book.Instance;
            if (b == null)
            {
                Say("no record in this game.");
                return;
            }
            var sb = new StringBuilder("[Rimfolk] Peoples' events 9: ");
            sb.AppendLine($"last listener {b.lastShout}; breaks rolled {string.Join(", ", b.shouts)}; the void's word {b.foretold ?? "none"} by {b.foretoldBy ?? "-"} until {b.foretoldUntil}.");
            foreach (var r in b.seals)
                sb.AppendLine($"  seal of {r.faction?.Name ?? "?"} at {r.town?.Name ?? "?"}: {r.stage}, {(r.insects ? "insects" : "mechs")}, due {r.due}, enemies {r.enemies.Count}.");
            var kinds = new[] { FeralMech.Kind, VoidListens.Kind, MindShouts.Kind, ConsoleTalk.Kind };
            var visits = CouncilGameComponent.Instance?.happenings?.visits?.Where(v => v != null && !v.done && kinds.Contains(v.kind)).ToList();
            if (visits != null)
                foreach (var v in visits)
                    sb.AppendLine($"  visit {v.kind}: {v.x?.LabelShort ?? "-"}, {v.a?.LabelShort ?? "-"}, stake {v.stake}, due {v.due}, until {v.until}, {v.text}.");
            foreach (var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists)
                if (LatticeLinked.LinkOf(p) is Hediff_LatticeLink h)
                    sb.AppendLine($"  {p.LabelShort} linked into {h.lattice?.Name ?? "?"} ({(h.whole ? "whole" : "for work")}).");
            var map = Find.CurrentMap;
            if (map != null)
            {
                var vault = PeoplesD.Friendly(PeoplesMath.Vaultfolk);
                sb.AppendLine($"  here: mechanitors to link {LatticeLinked.Takers(map).Count()}; feral kind {FeralMech.KindFor(map)?.defName ?? "none"}; hum taker {HumDeepened.Pick(map)?.LabelShort ?? "none"}; "
                              + $"shaken {VoidListens.Pick(map)?.LabelShort ?? "none"}, void's next {VoidListens.Next(map)?.defName ?? "none"}; long break {MindShouts.For(map)?.LabelShort ?? "none"}; "
                              + $"vault town {(vault != null ? SealBroken.Town(vault, map)?.Name ?? "none in reach" : "no Vault")}; part taker {LastPart.Takers(map).FirstOrDefault()?.LabelShort ?? "none"}; "
                              + $"radio {VoiceForColony.Radio(map)?.LabelShort ?? "none"}, talker {ConsoleTalk.Talker(map)?.LabelShort ?? "none"}.");
            }
            Log.Message(sb.ToString());
            Say("listed in the log.");
        }

        [DebugAction("Rimfolk", "Peoples' events 9: one of their camps here now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugCamp()
        {
            var map = Find.CurrentMap;
            var options = new List<FloatMenuOption>();
            foreach (string n in new[] { "Rimfolk_Camp_MechYard", "Rimfolk_Camp_ListeningRing", "Rimfolk_Camp_SurfaceScouts" })
            {
                var kind = DefDatabase<CampKindDef>.GetNamedSilentFail(n);
                if (kind == null) continue;
                options.Add(new FloatMenuOption(kind.label, () =>
                {
                    var f = Arrivals.Friendly(x => kind.FitsFaction(x));
                    if (f == null || !CampTracker.Enabled)
                    {
                        Say("no camp: " + (f == null ? "no people of that calling at peace" : "camps are off"));
                        return;
                    }
                    var ctx = new HappeningContext { map = map, faction = f };
                    Camp c = n == "Rimfolk_Camp_MechYard" ? MechYard.Start(ctx, false) : n == "Rimfolk_Camp_ListeningRing" ? ListeningRing.Start(ctx) : SurfaceScouts.Start(ctx);
                    Say(c != null ? kind.label + " of " + f.Name + " on the way." : "no camp: no spot for it here.");
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Peoples' events 9: a listener for a mind in a long break now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugShout()
        {
            var map = Find.CurrentMap;
            var f = PeoplesD.Friendly(PeoplesMath.Listeners);
            if (f == null)
            {
                Say("no Choir at peace.");
                return;
            }
            if (MindShouts.For(map) == null)
            {
                Say("nobody of the colony has been three hours in a break that turns on nobody (start one with the game's own tools and wait).");
                return;
            }
            Say(PeopleEvents2.Fire(MindShouts.EventDef, map, f) ? "the listener is at the gate." : "the listener couldn't come.");
        }

        [DebugAction("Rimfolk", "Peoples' events 9: the linked dream now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugDream()
        {
            int n = 0;
            foreach (var p in IncidentWorker_Happening.Colonists(Find.CurrentMap))
                if (LatticeLinked.LinkOf(p) is Hediff_LatticeLink h && h.whole)
                {
                    PeopleEvents9.Thought(p, Rand.Bool ? "Rimfolk_Thought_LatticeDreamGood" : "Rimfolk_Thought_LatticeDreamBad");
                    n++;
                }
            Say(n > 0 ? n + " linked dreamed another's dream." : "nobody here has the whole link.");
        }

        [DebugAction("Rimfolk", "Peoples' events 9: the vault's fight now (a caravan at the town)", allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugSeal()
        {
            var r = SealBroken.Open(null);
            if (r == null || r.stage != "called")
            {
                Say("no call of the vault's waiting (fire Rimfolk_Evt_SealBroken and answer it).");
                return;
            }
            var c = CallsToArms.Answering(r.town);
            if (c == null)
            {
                Say("no caravan of two or more of the colony's grown people on the way to " + (r.town?.Name ?? "the town") + ".");
                return;
            }
            if (Find.WorldGrid.ApproxDistanceInTiles(c.Tile, r.town.Tile) > 1.01f)
            {
                Say("the caravan is still on the road to " + r.town.Name + "; the fight starts when it gets there.");
                return;
            }
            SealBroken.Fight(r, c);
            Say(r.stage == "fight" ? "the fight is on." : "the fight couldn't be made here.");
        }

        [DebugAction("Rimfolk", "Peoples' events 9: the console's next evening now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugConsole()
        {
            var v = ConsoleTalk.Open();
            if (v?.a == null)
            {
                Say("no talk with the overseer under way (fire Rimfolk_Evt_ConsoleTalk).");
                return;
            }
            ConsoleTalk.Talked(v.a);
            Say("an evening's talk counted (" + v.due + " so far).");
        }

        [DebugAction("Rimfolk", "Peoples' events 9: a camp of theirs turns now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugTurn()
        {
            var map = Find.CurrentMap;
            var yard = MechYard.On(map);
            var ring = ListeningRing.On(map);
            var scouts = SurfaceScouts.On(map);
            var options = new List<FloatMenuOption>();
            if (MechYard.StateOf(yard) is MechYardState ys)
            {
                options.Add(new FloatMenuOption("mech yard: a mech goes feral (at its chance)", () =>
                {
                    ys.feralRolled = false;
                    MechYard.Feral(yard, ys);
                    Say(ys.feral ? "a mech has gone feral." : "nothing came of it (the harm budget, fights off, or the chance).");
                }));
                options.Add(new FloatMenuOption("mech yard: a band comes for their mechs (at its chance)", () =>
                {
                    ys.bandRolled = false;
                    MechYard.Band(yard, ys);
                    Say(ys.band ? "a band is coming." : "nothing came of it (no people at war with the Lattice, fights off, or the chance).");
                }));
            }
            if (ListeningRing.StateOf(ring) is ListeningRingState rs)
            {
                options.Add(new FloatMenuOption("listening ring: their song turns strange (at once)", () =>
                {
                    rs.strangeRolled = true;
                    ListeningRing.Strange(ring, rs);
                    Say("their song turned strange; they are leaving.");
                }));
                options.Add(new FloatMenuOption("listening ring: tonight's song felt now", () =>
                {
                    rs.sangDay = GenDate.DaysPassed;
                    ListeningRing.Felt(ring, rs);
                    Say("the evening's song is felt at home.");
                }));
            }
            if (SurfaceScouts.StateOf(scouts) is ScoutState ss)
            {
                options.Add(new FloatMenuOption("surface scouts: one is sky-sick (at its chance)", () =>
                {
                    ss.skyRolled = false;
                    SurfaceScouts.SkySick(scouts, ss);
                    Say(ss.sick != null ? ss.sick.LabelShort + " keeps to the shelter." : "nothing came of it (the chance, or no bed).");
                }));
                options.Add(new FloatMenuOption("surface scouts: one is lost on the land (at its chance)", () =>
                {
                    ss.lostRolled = false;
                    SurfaceScouts.Lose(scouts, ss);
                    Say(ss.lost != null ? ss.lost.LabelShort + " is out on the land." : "nothing came of it (the weather, the chance, or no place).");
                }));
                options.Add(new FloatMenuOption("surface scouts: one walks up to a beast (at its chance)", () =>
                {
                    ss.maulRolled = false;
                    SurfaceScouts.Maul(scouts, ss);
                    Say(ss.mauler != null ? "a scout is walking up to the " + ss.mauler.LabelShort + "." : "nothing came of it (no beast near, the harm budget, or the chance).");
                }));
            }
            if (options.Count == 0)
            {
                Say("none of their camps here.");
                return;
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}
