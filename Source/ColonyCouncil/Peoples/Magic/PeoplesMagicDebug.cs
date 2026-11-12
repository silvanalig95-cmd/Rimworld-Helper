using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: magic among the peoples ("Rimfolk", "Peoples' magic: ..."): the casters on the map and what they could
    /// do now; one caster of a people's class here now as a visitor; a spell in peace now from the casters here (the rules
    /// but the hour's roll and once a visit); what each people is known for, as its card and letters say it.
    /// </summary>
    public static class PeoplesMagicDebug
    {
        private static void Say(string text) => Messages.Message("Peoples' magic: " + text, MessageTypeDefOf.SilentInput, false);

        private static IEnumerable<Pawn> Casters(Map map) =>
            map?.mapPawns?.AllHumanlikeSpawned.Where(p => PeoplesMagic.TheirCraft(p) != null).ToList() ?? new List<Pawn>();

        [DebugAction("Rimfolk", "Peoples' magic: the casters on this map (log)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugCasters()
        {
            if (!PeoplesMagic.Loaded)
            {
                Say("A RimWorld of Magic isn't loaded.");
                return;
            }
            var map = Find.CurrentMap;
            var book = PeoplesMagicBook.Instance;
            var sb = new StringBuilder("[Rimfolk] Peoples' magic: the peoples' casters on this map (the mod lets those not of the colony cast: " + RomSpells.AiMayCast + "):");
            sb.AppendLine();
            foreach (var p in Casters(map))
            {
                string trait = PeoplesMagic.TheirCraft(p);
                sb.Append("    " + p.LabelShort + " of " + p.Faction.Name + ", " + PeoplesMagic.WordWithArticle(trait) + (p.HostileTo(Faction.OfPlayer) ? " (hostile)" : ""));
                sb.Append("; wears " + string.Join(", ", p.apparel?.WornApparel.Select(a => a.LabelNoCount) ?? Enumerable.Empty<string>()));
                if (p.equipment?.Primary != null) sb.Append("; carries " + p.equipment.Primary.LabelNoCount);
                var held = RomSpells.Abilities(p);
                sb.Append("; abilities " + (held.Count == 0 ? "none yet" : string.Join(", ", held)));
                var kept = held.Where(PeoplesMagicMath.IsKeptOut).ToList();
                if (kept.Count > 0) sb.Append("; kept out " + string.Join(", ", kept));
                if (PeoplesMagicMath.Helps(trait) && !p.HostileTo(Faction.OfPlayer))
                {
                    bool may = PeoplesMagicWatch.MayNow(book, p, map, out string why);
                    sb.Append("; may cast in peace now: " + (may ? "yes" : "no, " + why));
                }
                sb.AppendLine();
            }
            Log.Message(sb.ToString().TrimEnd());
            Say("listed in the log.");
        }

        [DebugAction("Rimfolk", "Peoples' magic: a caster of theirs here now, as a visitor (testing)...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugCaster()
        {
            if (!PeoplesMagic.Loaded)
            {
                Say("A RimWorld of Magic isn't loaded.");
                return;
            }
            var map = Find.CurrentMap;
            var at = UI.MouseCell();
            var options = new List<FloatMenuOption>();
            foreach (var f in Find.FactionManager.AllFactionsListForReading.Where(x => Peoples.IsOurs(x) && !x.defeated && !x.HostileTo(Faction.OfPlayer)))
            {
                var people = f;
                var kinds = DefDatabase<PawnKindDef>.AllDefsListForReading.Where(k => k.defaultFactionDef == people.def && k.GetModExtension<PeopleKindExtension>()?.gifts != null).ToList();
                foreach (var kind in kinds)
                    foreach (var g in kind.GetModExtension<PeopleKindExtension>().gifts.Where(g => g != null && !g.trait.NullOrEmpty()))
                    {
                        var k = kind;
                        string trait = g.trait;
                        options.Add(new FloatMenuOption(people.Name + ": " + k.label + ", " + PeoplesMagic.Word(trait, false), () => Spawn(people, k, trait, map, at)));
                    }
            }
            if (options.Count == 0) Say("none of Rimfolk's peoples at peace with the colony has a class to give.");
            else Find.WindowStack.Add(new FloatMenu(options));
        }

        private static void Spawn(Faction people, PawnKindDef kind, string trait, Map map, IntVec3 at)
        {
            var p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, people, PawnGenerationContext.NonPlayer, map.Tile, forceGenerateNewPawn: true));
            if (p == null)
            {
                Say("no one could be made.");
                return;
            }
            if (PeoplesMagic.Craft(p) == null)
            {
                if (!PeopleGifts.GiveClass(p, trait))
                {
                    Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.Discard);
                    Say("the class couldn't be given (switched off in A RimWorld of Magic's settings, the people kept from it there, or the pawn's backstory forbids it).");
                    return;
                }
                PeoplesMagic.Dress(p, Peoples.AgeOf(people));
            }
            if (!CellFinder.TryFindRandomCellNear(at, map, 6, c => c.Standable(map) && !c.Fogged(map), out var cell)) cell = at;
            GenSpawn.Spawn(p, cell, map);
            LordMaker.MakeNewLord(people, new LordJob_VisitOnErrand(people, at, GenDate.TicksPerDay / 2), map, new List<Pawn> { p });
            Say(p.LabelShort + ", " + PeoplesMagic.WordWithArticle(PeoplesMagic.Craft(p)) + " of " + people.Name + ", here as a visitor for half a day (wears "
                + string.Join(", ", p.apparel?.WornApparel.Select(a => a.LabelNoCount) ?? Enumerable.Empty<string>()) + ").");
        }

        [DebugAction("Rimfolk", "Peoples' magic: a spell in peace now (testing)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugPeace()
        {
            if (!PeoplesMagic.Loaded)
            {
                Say("A RimWorld of Magic isn't loaded.");
                return;
            }
            var map = Find.CurrentMap;
            var book = PeoplesMagicBook.Instance;
            var sb = new StringBuilder("[Rimfolk] Peoples' magic: a spell in peace now (no hour's roll, and once a visit not counted):");
            sb.AppendLine();
            int begun = 0;
            foreach (var p in Casters(map).Where(p => !p.HostileTo(Faction.OfPlayer)))
            {
                string trait = PeoplesMagic.TheirCraft(p);
                if (!PeoplesMagicMath.Helps(trait))
                {
                    sb.AppendLine("    " + p.LabelShort + " (" + PeoplesMagic.Word(trait, false) + "): no spell for peace in this class.");
                    continue;
                }
                if (PeoplesMagicWatch.Waiting(p))
                {
                    sb.AppendLine("    " + p.LabelShort + ": a spell asked already, waiting for it to be cast.");
                    continue;
                }
                bool may = PeoplesMagicWatch.MayNow(null, p, map, out string why);
                if (!may)
                {
                    sb.AppendLine("    " + p.LabelShort + ": may not now, " + why + ".");
                    continue;
                }
                if (PeoplesMagicWatch.TryCast(p, trait, Find.TickManager.TicksGame, out string not))
                {
                    begun++;
                    sb.AppendLine("    " + p.LabelShort + ": casting now.");
                }
                else sb.AppendLine("    " + p.LabelShort + ": nothing to cast, " + not + ".");
            }
            Log.Message(sb.ToString().TrimEnd());
            Say(begun > 0 ? begun + " casting now (the rest in the log)." : "no one cast (why in the log).");
        }

        [DebugAction("Rimfolk", "Peoples' magic: what each people is known for (log)", allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugKnown()
        {
            var sb = new StringBuilder("[Rimfolk] Peoples' magic: what each of Rimfolk's peoples in this world is known for (its card, and {AMONG} in its letters):");
            sb.AppendLine();
            foreach (var f in Find.FactionManager.AllFactionsListForReading.Where(x => Peoples.IsOurs(x) && !x.defeated))
            {
                var lines = PeoplesMagic.CardLines(f);
                sb.AppendLine("    " + f.Name + (PeoplesMagicMath.BuiltOnMagic(Peoples.CallingOf(f)) ? " (built on magic)" : "") + ": "
                              + (lines == null ? "nothing (no class switched on for them, or A RimWorld of Magic isn't loaded)" : string.Join(" ", lines)));
            }
            Log.Message(sb.ToString().TrimEnd());
            Say("listed in the log.");
        }
    }
}
