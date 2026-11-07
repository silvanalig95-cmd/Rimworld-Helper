using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Sacred animals, with Ideology (the player's pick of 7 November 2026): the kinds the colony's faith holds sacred are
    /// its venerated animals, the game's own (Ideo.VeneratedAnimals). The game already makes hunting, slaughtering and
    /// eating one wrong for those who hold it so, and mourns a tame one that dies. Rimfolk adds what it leaves out:
    /// <list type="bullet">
    /// <item>One that falls on the colony's land that wasn't the colony's (a wild one, a neighbour's, a manhunter brought
    /// down): those whose own faith holds it sacred grieve it (-2 for 3 days, three at most), with a word at the body and
    /// a line in the news.</item>
    /// <item>A hunt marked on one: a believer who sees it says they'll have no part of it (the game's own rules do the rest).</item>
    /// <item>Its day: the council may be asked to keep a day for the sacred animals (Rimfolk_Dilemma_SacredDay, a custom,
    /// spring to autumn with one of them on the land): food carried out to one where it is and everyone gathered round it
    /// for the afternoon. Kept, it goes on the colony's calendar, its question comes round on that date each year, and
    /// three years running it is a tradition, as every custom does (Calendar/CalendarCustoms.cs).</item>
    /// <item>The council's own cull never marks one (Animals/AnimalDilemmas.cs), as its hunts never take one (PlayOut/PlayCarry.cs).</item>
    /// </list>
    /// Looked at once an hour; nothing saved. Setting: "Sacred animals" (What's on, with the animals).
    /// </summary>
    public static class SacredAnimals
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public static bool On => ModsConfig.IdeologyActive && S != null && S.sacredAnimals && Current.Game != null;

        /// <summary>The kinds the colony's own faith holds sacred (none without Ideology or a faith).</summary>
        public static List<ThingDef> Kinds()
        {
            var ideo = DecreeWork.ColonyFaith;
            return ideo?.VeneratedAnimals ?? new List<ThingDef>();
        }

        /// <summary>The colony's own faith holds this kind sacred.</summary>
        public static bool SacredToColony(ThingDef race) => race != null && ModsConfig.IdeologyActive && (DecreeWork.ColonyFaith?.IsVeneratedAnimal(race) ?? false);

        /// <summary>Any faith of the colony's people holds this animal sacred (the game's own rule, whatever Rimfolk's setting).</summary>
        public static bool HeldSacred(Pawn a)
        {
            if (a == null || !ModsConfig.IdeologyActive) return false;
            var ideos = Faction.OfPlayerSilentFail?.ideos;
            if (ideos == null) return false;
            foreach (var ideo in ideos.AllIdeos)
                if (ideo != null && ideo.IsVeneratedAnimal(a))
                    return true;
            return false;
        }

        /// <summary>Their own faith holds this kind sacred.</summary>
        public static bool Believer(Pawn p, ThingDef race) => p?.Ideo != null && race != null && p.Ideo.IsVeneratedAnimal(race);

        /// <summary>The sacred animals on this map now that the colony could gather round: seen, up, calm, not hostile, reachable ground.</summary>
        public static IEnumerable<Pawn> Here(Map map)
        {
            if (map == null || !ModsConfig.IdeologyActive) yield break;
            var kinds = Kinds();
            if (kinds.Count == 0) yield break;
            foreach (var a in map.mapPawns.AllPawnsSpawned)
            {
                if (a?.RaceProps == null || !a.RaceProps.Animal || a.Dead || a.Downed || a.InMentalState || !kinds.Contains(a.def)) continue;
                if (map.fogGrid.IsFogged(a.Position) || a.HostileTo(Faction.OfPlayer)) continue;
                yield return a;
            }
        }

        // ===================== Once an hour =====================

        private static readonly HashSet<int> noted = new HashSet<int>();
        private static readonly HashSet<int> huntsNoted = new HashSet<int>();
        private static readonly Dictionary<Map, int> lastWord = new Dictionary<Map, int>();
        private static Game notedGame;

        public static void Hourly(int now)
        {
            if (!On) return;
            if (notedGame != Current.Game)
            {
                notedGame = Current.Game;
                noted.Clear();
                huntsNoted.Clear();
                lastWord.Clear();
            }
            if (Kinds().Count == 0 && !AnyBeliever()) return;
            foreach (var map in Find.Maps)
            {
                if (map == null || !map.IsPlayerHome) continue;
                try
                {
                    Fallen(map, now);
                    Hunts(map, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("sacred animals: the hour", e);
                }
            }
            if (noted.Count > 2000) noted.Clear();
            if (huntsNoted.Count > 2000) huntsNoted.Clear();
        }

        /// <summary>Someone of the colony holds an animal sacred (a faith other than the colony's may).</summary>
        private static bool AnyBeliever()
        {
            foreach (var p in PawnsFinder.AllMaps_FreeColonistsSpawned)
                if (p.Ideo != null && !p.Ideo.VeneratedAnimals.NullOrEmpty())
                    return true;
            return false;
        }

        private static bool WordDue(Map map, int now) =>
            !lastWord.TryGetValue(map, out int last) || last > now || now - last >= AnimalPowersMath.SacredLineGap;

        /// <summary>A sacred animal that wasn't the colony's fell on its land within the last two hours: its believers grieve.</summary>
        private static void Fallen(Map map, int now)
        {
            var corpses = map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse);
            if (corpses.Count == 0) return;
            var thought = DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_Sacred_Fell");
            bool told = false;
            foreach (var t in corpses.ToList())
            {
                if (!(t is Corpse c) || c.Destroyed || !c.Spawned) continue;
                var x = c.InnerPawn;
                if (x?.RaceProps == null || !x.RaceProps.Animal || c.timeOfDeath < 0 || now - c.timeOfDeath > AnimalPowersMath.SacredFreshTicks) continue;
                if (!noted.Add(c.thingIDNumber)) continue;
                bool tame = x.Faction == Faction.OfPlayer;
                var mourners = map.mapPawns.FreeColonistsSpawned
                    .Where(p => People.HasInnerLife(p) && !p.DevelopmentalStage.Baby() && AnimalPowersMath.Mourns(Believer(p, x.def), tame)).ToList();
                if (mourners.Count == 0) continue;
                string kind = x.KindLabel;
                using (Causes.Push("CC_Sacred_Cause_Fell".Translate(kind.Named("KIND")).Resolve()))
                    foreach (var p in mourners)
                        CouncilGameComponent.GiveThought(p, thought);
                if (told) continue;
                told = true;
                News.Post("CC_Sacred_Fell".Translate(x.Named("PET"), TextUtil.JoinAnd(mourners.Select(p => p.LabelShort).ToList()).Named("WHO")).Resolve(), new LookTargets(c),
                    NewsKind.Land, MessageTypeDefOf.NegativeEvent, Importance.Flavor);
                if (!Speech.On || map != Find.CurrentMap || !WordDue(map, now)) continue;
                var who = mourners.Where(p => Together.Able(p) && p.Position.InHorDistOf(c.Position, 25f)).OrderBy(p => p.Position.DistanceToSquared(c.Position)).FirstOrDefault();
                if (who == null) continue;
                string line = Interplay.Line("Rimfolk_Pool_Sacred_Fell", who, null, kind.Named("KIND"));
                // apart: said over the body, to whoever hears it.
                if (!line.NullOrEmpty() && Speech.Say(who, line)) lastWord[map] = now;
            }
        }

        /// <summary>A hunt marked on a sacred animal: a believer in view says they'll have no part of it.</summary>
        private static void Hunts(Map map, int now)
        {
            if (!Speech.On || map != Find.CurrentMap || !WordDue(map, now)) return;
            foreach (var d in map.designationManager.SpawnedDesignationsOfDef(DesignationDefOf.Hunt))
            {
                if (!(d.target.Thing is Pawn a) || a.Dead || !huntsNoted.Add(a.thingIDNumber)) continue;
                var who = map.mapPawns.FreeColonistsSpawned.Where(p => Together.Able(p) && People.HasInnerLife(p) && Believer(p, a.def) && AnimalPowers.InView(map, p.Position))
                    .FirstOrDefault();
                if (who == null) continue;
                string line = Interplay.Line("Rimfolk_Pool_Sacred_Hunt", who, null, a.KindLabel.Named("KIND"));
                // apart: said to nobody in particular, about a mark on the map.
                if (!line.NullOrEmpty() && Speech.Say(who, line))
                {
                    lastWord[map] = now;
                    return;
                }
            }
        }
    }

    /// <summary>
    /// The sacred animals' day, a custom (Ideology): the colony's faith holds a kind sacred and one of them is on the land;
    /// asked of someone whose faith holds it so. X is the animal (the colony's own first), {THING} its kind.
    /// </summary>
    public class DilemmaWorker_SacredDay : DilemmaWorker_Custom
    {
        public override bool CanHappenNow(Map map) => SacredAnimals.On && base.CanHappenNow(map) && SacredAnimals.Here(map).Any();

        protected override bool Extra(DilemmaContext ctx)
        {
            if (!base.Extra(ctx) || ctx.a == null) return false;
            var animal = SacredAnimals.Here(ctx.map).Where(x => SacredAnimals.Believer(ctx.a, x.def) && ctx.a.CanReach(x, Verse.AI.PathEndMode.Touch, Danger.Some))
                .OrderByDescending(x => x.Faction == Faction.OfPlayer).ThenBy(x => x.Position.DistanceToSquared(ctx.a.Position)).FirstOrDefault();
            if (animal == null) return false;
            ctx.stranger = animal;
            ctx.thing = animal.def;
            return true;
        }

        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            base.AddTags(ctx, tags);
            tags.Add(ctx.stranger?.Faction == Faction.OfPlayer ? "tame" : "wild");
        }
    }
}
