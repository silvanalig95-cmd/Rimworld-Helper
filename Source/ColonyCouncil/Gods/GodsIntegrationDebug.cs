using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: the gods' integrations, part 1 (docs/content-update-proposals.md, C1 to C4, C16, C17): a festival's custom
    /// now and its next part, the believers behind a rite, a god's beast at home greeted, fed and walked, its death, the beast
    /// that stays taken for a god's and its question, a legendary on the land, and each of Rimfolk's tales for the colony's
    /// art. Kept out of Gods/OnMap as the other dev actions are: the map's own code speaks only through Speech.Say and the
    /// teller.
    /// </summary>
    public static class GodsIntegrationDebug
    {
        private static void Say(string text) => Messages.Message("Gods: " + text, MessageTypeDefOf.SilentInput, false);

        [DebugAction("Rimfolk", "Gods: a festival kept now, its custom at once...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugFestival()
        {
            var map = Find.CurrentMap;
            var list = OwnFeastsMath.Gods.Select(OwnHappenings.God).Where(g => g != null)
                .Select(g => new FloatMenuOption(Festivals.Name(g), () => Say(OwnFeasts.DevStart(g, map)))).ToList();
            if (list.Count == 0) list.Add(new FloatMenuOption("(none of the eight in this game)", null));
            Find.WindowStack.Add(new FloatMenu(list));
        }

        [DebugAction("Rimfolk", "Gods: the festival's next part now (meal, story, lamps, dawn, breakfast)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugFestivalNext() => Say(OwnFeasts.DevNext(Find.CurrentMap));

        [DebugAction("Rimfolk", "Gods: a festival's rite now, the believers behind it...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugRite()
        {
            var map = Find.CurrentMap;
            var list = Gods.All.Where(g => g != null && Shrines.Of(map, g).Any())
                .Select(g => new FloatMenuOption(Gods.FullName(g), () =>
                {
                    var shrine = Shrines.Of(map, g).Select(s => s.thing).FirstOrDefault(t => t != null && t.Spawned);
                    var lead = Worship.Priest(map) ?? map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => !p.DevelopmentalStage.Juvenile() && p.CanReach(shrine, PathEndMode.Touch, Danger.Some));
                    if (shrine == null || lead == null)
                    {
                        Say("no place of this god's, or nobody to lead the rite.");
                        return;
                    }
                    int ticks = 1800;
                    if (!WorshipJobs.Start(lead, WorshipJobs.At(WorshipJobs.RiteJob, shrine, ticks)))
                    {
                        Say(lead.LabelShort + " couldn't start the rite.");
                        return;
                    }
                    OwnFeasts.Gather(lead, shrine, g, ticks + 600);
                    Say(lead.LabelShort + " leads the rite; those who hold " + Gods.Name(g) + " and are free stand behind.");
                })).ToList();
            if (list.Count == 0) list.Add(new FloatMenuOption("(no god has a place here)", null));
            Find.WindowStack.Add(new FloatMenu(list));
        }

        [DebugAction("Rimfolk", "Gods: a god's beast at home: greeted, given a bite, walked to its place...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugBeast()
        {
            var map = Find.CurrentMap;
            var b = GodBeastsBook.Instance;
            var beasts = GodBeasts.AtHome(map);
            if (b == null || beasts.Count == 0)
            {
                Say("none of the colony's animals here is of a god's kind (Gods/SacredKinds.cs; tame one, e.g. a thunderox or a dryad).");
                return;
            }
            b.greeted.Clear();
            Find.WindowStack.Add(new FloatMenu(new System.Collections.Generic.List<FloatMenuOption>
            {
                new FloatMenuOption("greeted by whoever is near", () =>
                {
                    int was = b.greeted.Count;
                    GodBeasts.Greet(b, map, beasts);
                    Say(b.greeted.Count > was ? "greeted." : "nobody near who holds the god they take it for (in sight, within ten cells).");
                }),
                new FloatMenuOption("given a bite by a believer", () =>
                {
                    b.EndWalk();
                    Say(GodBeasts.Bite(b, map, beasts, null, Find.TickManager.TicksGame) ? b.by.LabelShort + " takes a bite to " + b.beast.LabelShort + "." : "nobody who holds the god they take it for is free.");
                }),
                new FloatMenuOption("its god's festival: fed first, walked to the place", () =>
                {
                    var g = SacredKinds.BeastOf(beasts[0].def);
                    GodBeasts.Festival(g, map);
                    Say(b.stage != 0 ? b.by.LabelShort + " feeds " + b.beast.LabelShort + " and walks it to " + Gods.Name(g) + "'s place." : "nobody who holds " + Gods.Name(g) + " free, or no place of it.");
                }),
                new FloatMenuOption("grieved as if it had died (nothing dies)", () =>
                {
                    GodBeasts.Ours(beasts[0], map);
                    Say("those who hold the god they take " + beasts[0].LabelShort + " for grieve; the book has a believer's verse.");
                })
            }));
        }

        [DebugAction("Rimfolk", "Gods: the beast that stays here taken for a god's now, and its question", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugResident()
        {
            var map = Find.CurrentMap;
            var comp = CouncilGameComponent.Instance;
            var rb = comp?.BeastOf(map);
            if (rb?.animal == null || !rb.Here)
            {
                Say("no beast that stays here (\"Rimfolk: check myself\" says why; a wild animal of a god's kind three days on the land becomes one).");
                return;
            }
            string said = GodBeasts.Resident(rb, rb.animal, map);
            if (said == null)
            {
                Say(rb.animal.LabelShort + " is of no god's kind or shape, or nobody here can name a god for it.");
                return;
            }
            bool asked = (DilemmaTracker.For(map) ?? comp.dilemmas).TryFire(true, d => d.defName == ResidentBeast.SacredDilemma);
            Say(said + (asked ? " The council is asked." : " The question couldn't be asked just now."));
        }

        [DebugAction("Rimfolk", "Gods: a legendary on the land now, taken for a god's...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugLegend()
        {
            var map = Find.CurrentMap;
            var b = GodBeastsBook.Instance;
            var list = GodBeastsMath.Legends.Keys.Select(n => DefDatabase<PawnKindDef>.AllDefsListForReading.FirstOrDefault(k => k.race?.defName == n)).Where(k => k != null)
                .Select(k => new FloatMenuOption(k.LabelCap + " (" + GodBeastsMath.LegendGod(k.race.defName) + ")", () =>
                {
                    if (!RCellFinder.TryFindRandomPawnEntryCell(out var cell, map, CellFinder.EdgeRoadChance_Animal))
                    {
                        Say("no way onto the land.");
                        return;
                    }
                    var p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(k, null, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true));
                    GenSpawn.Spawn(p, cell, map);
                    var like = Gods.ByKey(GodBeastsMath.LegendGod(k.race.defName));
                    Say(GodBeasts.Taken(b, map, p, like, Find.TickManager.TicksGame) ? "a believer says whose they take it for." : "nobody here who believes such things (or no believer up and about).");
                })).ToList();
            if (list.Count == 0) list.Add(new FloatMenuOption("(PokéWorld isn't loaded)", null));
            Find.WindowStack.Add(new FloatMenu(list));
        }

        [DebugAction("Rimfolk", "Art: each of Rimfolk's tales now (for a sculpture to show)", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugTales(Pawn p)
        {
            if (p == null || !p.IsColonist) return;
            var beast = p.Map?.mapPawns.SpawnedColonyAnimals.FirstOrDefault() ?? p;
            ArtTales.force = true;
            int n = 0;
            try
            {
                foreach (var kind in ArtTalesMath.Kinds)
                {
                    var g = Gods.All.FirstOrDefault(x => x != null && x.kind != GodKind.Dead);
                    string god = kind == ArtTales.Festival || kind == ArtTales.FestivalBeast ? g == null ? null : Festivals.Name(g) : ArtTales.GodWords(g, p.Map);
                    string what = kind == ArtTales.LineHeld ? "the north line" : kind == ArtTales.OfferingTaken ? "a carved figure" : null;
                    if (ArtTales.Record(kind, p, beast, god, what) != null) n++;
                }
            }
            finally
            {
                ArtTales.force = false;
            }
            Say(n + " tales of " + p.LabelShort + "'s recorded; a sculpture made now may show one of them.");
        }
    }
}
