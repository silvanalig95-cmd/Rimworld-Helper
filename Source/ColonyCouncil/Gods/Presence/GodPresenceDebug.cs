using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: the gods' presence (Debug actions, "Rimfolk", "Gods: ..."). Make any person or animal on the map a god's
    /// of any look, shown or in disguise; reveal one, hide one again or unmake it; bring an animal of a look's kinds; play
    /// each kind of moment of each look at a cell (with the god standing there, if one does); see what each look finds
    /// in this game (its robes, motes, sounds, tracks and animals) in the log.
    /// </summary>
    public static class GodPresenceDebug
    {
        private static void Say(string text) => Messages.Message("Gods: " + text, MessageTypeDefOf.SilentInput, false);

        private static void Menu(IEnumerable<FloatMenuOption> options)
        {
            var list = options.ToList();
            if (list.Count == 0) list.Add(new FloatMenuOption("(nothing here)", null));
            Find.WindowStack.Add(new FloatMenu(list));
        }

        private static IEnumerable<GodLookDef> Looks => GodLooks.All.OrderBy(l => Array.IndexOf(GodPresenceMath.Domains, l.Domain)).ThenBy(l => l.minTech);

        private static string LookLabel(GodLookDef l) => l.defName.Replace("Rimfolk_GodLook_", "") + " (" + l.sampleName + ")";

        private static void PickLook(Action<GodLookDef> then) => Menu(Looks.Select(l => new FloatMenuOption(LookLabel(l), () => then(l))));

        /// <summary>The pawn under the mouse, for a click of the dev tool.</summary>
        private static Pawn PawnAtMouse()
        {
            var map = Find.CurrentMap;
            var c = UI.MouseCell();
            return map == null || !c.InBounds(map) ? null : c.GetThingList(map).OfType<Pawn>().FirstOrDefault();
        }

        private static void MakeAt(GodLookDef look, bool disguised)
        {
            DebugTools.curTool = new DebugTool("make a " + (disguised ? "disguised " : "") + "god of " + look.Domain, () =>
            {
                var p = PawnAtMouse();
                if (p == null) return;
                Say(GodAvatar.Make(p, look, look.sampleName, disguised) ? p.LabelShort + " is " + look.sampleName + (disguised ? ", in disguise." : ".") : "couldn't make " + p.LabelShort + " a god's.");
            });
        }

        [DebugAction("Rimfolk", "Gods: make a person or animal a god's, shown...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugMakeShown() => PickLook(l => MakeAt(l, false));

        [DebugAction("Rimfolk", "Gods: make a person or animal a god's, in disguise...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugMakeDisguised() => PickLook(l => MakeAt(l, true));

        private static IEnumerable<HediffComp_GodAvatar> Here => GodAvatar.All.Where(c => c.Pawn != null && c.Pawn.Spawned && c.Pawn.Map == Find.CurrentMap).ToList();

        private static string GodLabel(HediffComp_GodAvatar c) => c.Pawn.LabelShort + ": " + c.Name + (c.disguised ? " (in disguise)" : "");

        [DebugAction("Rimfolk", "Gods: reveal a god in disguise...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugReveal()
        {
            // apart: a god in disguise shows itself to everyone on the map; nobody confides anything to anyone.
            Menu(Here.Where(c => c.disguised).Select(c => new FloatMenuOption(GodLabel(c), () => Say(GodAvatar.Reveal(c.Pawn) ? c.Name + " shows itself." : "it couldn't."))));
        }

        [DebugAction("Rimfolk", "Gods: a god goes back into disguise...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugDisguise() =>
            Menu(Here.Where(c => !c.disguised).Select(c => new FloatMenuOption(GodLabel(c), () =>
            {
                GodAvatar.Disguise(c.Pawn);
                Say(c.Pawn.LabelShort + " looks like anyone again.");
            })));

        [DebugAction("Rimfolk", "Gods: unmake a god...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugUnmake() =>
            Menu(Here.Select(c => new FloatMenuOption(GodLabel(c), () =>
            {
                var p = c.Pawn;
                GodAvatar.Unmake(p);
                Say(p.LabelShort + " is no god's now.");
            })));

        [DebugAction("Rimfolk", "Gods: bring an animal of a look, shown...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugAnimal() =>
            PickLook(l => Menu(l.AnimalKinds.Select(k => new FloatMenuOption(k.LabelCap + " (" + k.defName + ")", () =>
            {
                var map = Find.CurrentMap;
                var kind = DefDatabase<PawnKindDef>.AllDefs.FirstOrDefault(pk => pk.race == k);
                if (kind == null)
                {
                    Say("no kind of " + k.label + " to make.");
                    return;
                }
                var a = PawnGenerator.GeneratePawn(kind);
                var at = CellFinder.RandomClosewalkCellNear(Find.CameraDriver.MapPosition, map, 6);
                GenSpawn.Spawn(a, at, map);
                Say(GodAvatar.Make(a, l, l.sampleName, false) ? "a " + k.label + ", " + l.sampleName + "." : "couldn't make it a god's.");
            }))));

        [DebugAction("Rimfolk", "Gods: a moment of a look here...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugMoment() =>
            PickLook(l => Menu(((PresenceKind[])Enum.GetValues(typeof(PresenceKind))).SelectMany(k => new[] { false, true }.Select(veiled => new FloatMenuOption(k + (veiled ? ", veiled" : ""), () =>
            {
                DebugTools.curTool = new DebugTool(k + " of " + l.Domain, () =>
                {
                    var map = Find.CurrentMap;
                    var cell = UI.MouseCell();
                    var god = PawnAtMouse();
                    var c = GodAvatar.Of(god);
                    string name = c?.Name ?? l.sampleName;
                    GodPresence.Manifest(l, name, k == PresenceKind.Omen ? null : "CC_GodP_DevEpithet".Translate().Resolve(), map, cell, k, c != null ? god : null, null,
                        k == PresenceKind.Omen ? "CC_GodP_DevOmen".Translate(name.Named("NAME")).Resolve() : null, veiled);
                    DebugTools.curTool = null;
                });
            })))));

        [DebugAction("Rimfolk", "Gods: end the moments going on", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugStop()
        {
            GodPresence.StopAll();
            Say("no moment goes on now.");
        }

        [DebugAction("Rimfolk", "Gods: the gods here (log)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugList()
        {
            var lines = Here.Select(c => "  " + GodLabel(c) + ", look " + c.look?.defName + ", size " + c.DrawSize.ToString("0.00")).ToList();
            Log.Message("[Rimfolk] Gods here:\n" + (lines.Count > 0 ? string.Join("\n", lines) : "  (none)"));
            Say(lines.Count + " here; the list is in the log.");
        }

        [DebugAction("Rimfolk", "Gods: what each look finds in this game (log)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugLooks()
        {
            var sb = new StringBuilder("[Rimfolk] The gods' looks in this game:\n");
            foreach (var l in Looks)
            {
                sb.AppendLine(l.defName + " (" + l.minTech + " to " + l.maxTech + ")");
                sb.AppendLine("  robes: " + string.Join(", ", l.BodyApparel.Select(d => d.defName)) + "; head: " + string.Join(", ", l.HeadApparel.Select(d => d.defName)));
                sb.AppendLine("  motes: " + string.Join(", ", l.Motes.Select(d => d.defName)) + "; flash: " + string.Join(", ", l.Flashes.Select(d => d.defName)) + "; circle: "
                              + string.Join(", ", l.Circles.Select(d => d.defName)));
                foreach (PresenceKind k in Enum.GetValues(typeof(PresenceKind)))
                    sb.AppendLine("  " + k + ": " + string.Join(" + ", GodSounds.StingFor(l, k).Select(s => s.defName)));
                sb.AppendLine("  hum: " + (l.hum.FirstOrDefault(GodSounds.SoundExists) ?? "none") + "; tracks: " + string.Join(", ", l.tracks.Where(GodSounds.SongExists))
                              + "; own tracks: " + GodSounds.OwnTracks(l.Domain).Count + "; dark: " + string.Join(", ", l.darkTracks.Where(GodSounds.SongExists)));
                sb.AppendLine("  animals: " + string.Join(", ", l.AnimalKinds.Select(d => d.defName)));
            }
            Log.Message(sb.ToString());
            Say("the looks are in the log.");
        }
    }
}
