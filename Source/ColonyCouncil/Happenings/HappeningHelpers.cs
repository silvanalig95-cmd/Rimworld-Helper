using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>People who come to the colony: who they are, where they appear, how they leave or stay.</summary>
    public static class Arrivals
    {
        /// <summary>Factions of the colony's own era are much likelier (a medieval colony meets medieval folk).</summary>
        public static float TechWeight(Faction f, TechLevel tech) => Mathf.Pow(0.35f, Mathf.Abs((int)f.def.techLevel - (int)tech));

        /// <summary>A people that isn't hostile and sends visitors: settlers, villagers, traders, tribes.</summary>
        public static Faction Friendly(Func<Faction, bool> pred = null)
        {
            var tech = Avail.EffectiveTech;
            return Find.FactionManager.AllFactionsListForReading
                .Where(f => !f.IsPlayer && !f.Hidden && !f.defeated && !f.temporary && f.def.humanlikeFaction && f.def.basicMemberKind != null
                            && !f.HostileTo(Faction.OfPlayer) && (pred == null || pred(f)))
                .RandomElementByWeightWithFallback(f => TechWeight(f, tech));
        }

        /// <summary>What a newcomer is generated as: dressed and armed for the colony's era.</summary>
        public static PawnKindDef JoinerKind()
        {
            if (Avail.IsPreIndustrial)
            {
                var tech = Avail.EffectiveTech;
                var f = Find.FactionManager.AllFactionsListForReading
                    .Where(x => !x.IsPlayer && x.def.humanlikeFaction && x.def.basicMemberKind != null && x.def.techLevel >= TechLevel.Neolithic && x.def.techLevel <= TechLevel.Medieval)
                    .OrderBy(x => Mathf.Abs((int)x.def.techLevel - (int)tech)).FirstOrDefault();
                if (f != null) return f.def.basicMemberKind;
            }
            return Faction.OfPlayer?.def?.basicMemberKind ?? PawnKindDefOf.Colonist;
        }

        public static Pawn Make(PawnKindDef kind, Faction faction, FloatRange? age = null, Gender? gender = null, string lastName = null, bool child = false)
        {
            var req = new PawnGenerationRequest(kind, faction, PawnGenerationContext.NonPlayer,
                forceGenerateNewPawn: true, canGeneratePawnRelations: false, colonistRelationChanceFactor: 0f,
                fixedGender: gender, fixedLastName: lastName, biologicalAgeRange: age,
                developmentalStages: child ? DevelopmentalStage.Child : DevelopmentalStage.Adult,
                dontGiveWeapon: child || Avail.IsPreIndustrial && (faction == null || faction.def.techLevel > TechLevel.Medieval));
            return PawnGenerator.GeneratePawn(req);
        }

        /// <summary>Someone the colony hasn't answered about yet: kept in the world until then.</summary>
        public static void Hold(Pawn p)
        {
            if (p == null || p.Spawned || p.Dead) return;
            if (!Find.WorldPawns.Contains(p)) Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.KeepForever);
            else Find.WorldPawns.ForcefullyKeptPawns.Add(p);
        }

        /// <summary>Not wanted: back to the wide world (they may turn up again, as people do).</summary>
        public static void Release(Pawn p)
        {
            if (p == null) return;
            Find.WorldPawns.ForcefullyKeptPawns.Remove(p);
        }

        /// <summary>Made for an event that couldn't happen after all: gone without a trace.</summary>
        public static void Discard(Pawn p)
        {
            if (p == null || p.Spawned || p.Dead || p.Discarded) return;
            Find.WorldPawns.ForcefullyKeptPawns.Remove(p);
            if (Find.WorldPawns.Contains(p)) Find.WorldPawns.RemoveAndDiscardPawnViaGC(p);
            else Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.Discard);
        }

        public static bool TryEntryCell(Map map, out IntVec3 cell) =>
            RCellFinder.TryFindRandomPawnEntryCell(out cell, map, CellFinder.EdgeRoadChance_Friendly, false, null);

        /// <summary>Puts someone from the world on the map: at its edge, or near a spot.</summary>
        public static bool Spawn(Pawn p, Map map, IntVec3? near = null)
        {
            if (p == null || p.Dead || p.Discarded || map == null) return false;
            if (p.Spawned) return true;
            IntVec3 cell;
            if (near.HasValue && near.Value.IsValid)
            {
                if (!CellFinder.TryFindRandomCellNear(near.Value, map, 4, c => c.Standable(map) && !c.Fogged(map), out cell)) cell = near.Value;
            }
            else if (!TryEntryCell(map, out cell)) return false;
            Find.WorldPawns.ForcefullyKeptPawns.Remove(p);
            if (Find.WorldPawns.Contains(p)) Find.WorldPawns.RemovePawn(p);
            GenSpawn.Spawn(p, cell, map);
            return true;
        }

        /// <summary>Visitors walk in from the edge and spend some hours at a spot (outside the colony, or at a grave).</summary>
        public static Lord Visit(List<Pawn> pawns, Map map, Faction f, IntVec3? chill, float hours)
        {
            if (pawns.NullOrEmpty() || f == null || !TryEntryCell(map, out var entry)) return null;
            foreach (var p in pawns)
                if (!Spawn(p, map, entry)) return null;
            IntVec3 spot = chill ?? IntVec3.Invalid;
            if (!spot.IsValid && !RCellFinder.TryFindRandomSpotJustOutsideColony(pawns[0], out spot)) spot = entry;
            return LordMaker.MakeNewLord(f, new LordJob_VisitColony(f, spot, Mathf.RoundToInt(hours * GenDate.TicksPerHour)), map, pawns);
        }

        /// <summary>Visitors (or a waiting stranger) leave now, on foot.</summary>
        public static void SendAway(IEnumerable<Pawn> pawns)
        {
            var list = pawns.Where(p => p != null && p.Spawned && !p.Dead && !p.Downed && p.Faction != Faction.OfPlayer && !p.IsPrisoner && p.RaceProps.Humanlike).Distinct().ToList();
            foreach (var p in list) p.GetLord()?.RemovePawn(p);
            foreach (var g in list.GroupBy(p => new { p.Map, p.Faction }))
                LordMaker.MakeNewLord(g.Key.Faction, new LordJob_ExitMapBest(LocomotionUrgency.Walk, false, true), g.Key.Map, g.ToList());
            foreach (var p in pawns.Where(p => p != null && !p.Spawned)) Release(p);
        }

        /// <summary>Joins the colony: walks in from the edge if they were waiting out in the world.</summary>
        public static bool Join(Pawn x, Map map, IntVec3? near = null)
        {
            if (x == null || x.Dead || x.Discarded || map == null) return false;
            if (!x.Spawned && !Spawn(x, map, near)) return false;
            Find.WorldPawns.ForcefullyKeptPawns.Remove(x);
            x.GetLord()?.RemovePawn(x);
            if (x.Faction != Faction.OfPlayer)
            {
                if (x.guest != null && x.guest.HostFaction != null) x.guest.SetGuestStatus(null);
                x.SetFaction(Faction.OfPlayer);
            }
            return true;
        }

        /// <summary>Text above someone's head for a few seconds.</summary>
        public static void Say(Pawn p, string text)
        {
            if (p == null || !p.Spawned || text.NullOrEmpty() || !ColonyCouncilMod.Settings.speechText) return;
            MoteMaker.ThrowText(p.DrawPos + new Vector3(0f, 0f, 0.6f), p.Map, text, 4f);
        }
    }

    /// <summary>Fights the events bring, sent through the game's own raid incident.</summary>
    public static class Raids
    {
        public static bool CanRaid(Faction f) =>
            f != null && !f.IsPlayer && !f.defeated && !f.Hidden && !f.temporary && f.def.humanlikeFaction && f.HostileTo(Faction.OfPlayer)
            && f.def.pawnGroupMakers != null && f.def.pawnGroupMakers.Any(m => m.kindDef == PawnGroupKindDefOf.Combat);

        /// <summary>A hostile people that can send fighters, of the colony's own era by preference.</summary>
        public static Faction Enemy(Func<Faction, bool> pred = null)
        {
            var tech = Avail.EffectiveTech;
            return Find.FactionManager.AllFactionsListForReading.Where(f => CanRaid(f) && (pred == null || pred(f)))
                .RandomElementByWeightWithFallback(f => Arrivals.TechWeight(f, tech));
        }

        /// <summary>Sends them now, with the event's own letter above the game's usual raid text.</summary>
        public static bool Send(Map map, Faction f, float points, string label, string text, Pawn leader = null)
        {
            if (map == null || !CanRaid(f)) return false;
            float min = 0f;
            try
            {
                min = f.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Combat, null);
            }
            catch
            {
            }
            var parms = new IncidentParms
            {
                target = map, faction = f, points = Mathf.Max(points, min * 1.1f, 60f), forced = true,
                raidStrategy = RaidStrategyDefOf.ImmediateAttack, raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn
            };
            if (!label.NullOrEmpty()) parms.customLetterLabel = NoBraces(label);
            if (!text.NullOrEmpty()) parms.customLetterText = NoBraces(text) + "\n\n{BASETEXT}";
            var before = new HashSet<Lord>(map.lordManager.lords);
            bool ok = IncidentDefOf.RaidEnemy.Worker.TryExecute(parms);
            if (!ok)
            {
                // That way in or that plan doesn't suit these people: let the game choose.
                parms.raidStrategy = null;
                parms.raidArrivalMode = null;
                ok = IncidentDefOf.RaidEnemy.Worker.TryExecute(parms);
            }
            // The storyteller should know a big fight just happened, so it doesn't send its own right on top of it.
            if (ok)
            {
                try
                {
                    // (A forced incident isn't recorded, so this goes in as an ordinary one.)
                    var record = parms.ShallowCopy();
                    record.forced = false;
                    map.storyState.Notify_IncidentFired(new FiringIncident(IncidentDefOf.RaidEnemy, null, record));
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("event raid record", e);
                }
            }
            if (ok && leader != null)
            {
                try
                {
                    Lead(map, f, leader, before);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("event raid leader", e);
                }
            }
            return ok;
        }

        private static string NoBraces(string s) => s.Replace('{', '(').Replace('}', ')');

        /// <summary>Someone special (a nemesis) walks in with the raiders who just arrived.</summary>
        private static void Lead(Map map, Faction f, Pawn leader, HashSet<Lord> before)
        {
            Find.WorldPawns.ForcefullyKeptPawns.Remove(leader);
            if (leader.Dead || leader.Discarded || leader.Spawned || leader.Faction != f || leader.IsPrisoner || People.Kidnapped(leader)
                || leader.GetCaravan() != null || QuestUtility.IsReservedByQuestOrQuestBeingGenerated(leader)) return;
            var lord = map.lordManager.lords.FirstOrDefault(l => !before.Contains(l) && l.faction == f && l.ownedPawns.Count > 0);
            if (lord == null) return;
            var anchor = lord.ownedPawns.RandomElement();
            if (!CellFinder.TryFindRandomCellNear(anchor.Position, map, 5, c => c.Standable(map) && c.GetFirstPawn(map) == null, out var cell)) cell = anchor.Position;
            if (Find.WorldPawns.Contains(leader)) Find.WorldPawns.RemovePawn(leader);
            GenSpawn.Spawn(leader, cell, map);
            if (lord.CanAddPawn(leader)) lord.AddPawn(leader);
        }
    }

    /// <summary>Finds, gifts and inheritances: things of the colony's own era, never something it couldn't know.</summary>
    public static class Loot
    {
        private static List<ThingDef> cache;
        private static TechLevel cacheTech = TechLevel.Undefined;

        private static TechLevel Tech
        {
            get
            {
                var t = Avail.EffectiveTech;
                if (Avail.IsPreIndustrial && t > TechLevel.Medieval) t = TechLevel.Medieval;
                return t < TechLevel.Neolithic ? TechLevel.Neolithic : t;
            }
        }

        private static List<ThingDef> Candidates()
        {
            var tech = Tech;
            if (cache != null && cacheTech == tech) return cache;
            cacheTech = tech;
            cache = DefDatabase<ThingDef>.AllDefsListForReading.Where(d => Fits(d, tech)).ToList();
            return cache;
        }

        private static bool Fits(ThingDef d, TechLevel tech)
        {
            try
            {
                if (d.category != ThingCategory.Item || d.tradeability == Tradeability.None || !d.PlayerAcquirable || d.destroyOnDrop) return false;
                if (d.IsCorpse || d.isUnfinishedThing || d.IsEgg || d.IsMeat || d.IsDrug || d.techLevel > tech) return false;
                if (d.BaseMarketValue < 1f || d.BaseMarketValue > 2500f) return false;
                if (d.HasComp(typeof(CompRottable)) || d.HasComp(typeof(CompBladelinkWeapon)) || d.HasComp(typeof(CompHatcher))) return false;
                if (d.thingClass == typeof(MinifiedThing) || d.thingClass == typeof(Pawn)) return false;
                if (d.IsWeapon && d.weaponTags.NullOrEmpty()) return false;
                return d.IsStuff || d.IsMedicine || d.IsWeapon || d.IsApparel || d == ThingDefOf.ComponentIndustrial;
            }
            catch
            {
                return false;
            }
        }

        public static void ClearCache() => cache = null;

        /// <summary>Things worth about <paramref name="value"/> silver: some silver, some materials or medicine, maybe a fine piece.</summary>
        public static List<Thing> Make(float value, bool fancy = false)
        {
            var list = new List<Thing>();
            float left = value;
            int silver = Mathf.RoundToInt(value * Rand.Range(0.15f, 0.35f) / 5f) * 5;
            if (silver > 0)
            {
                var s = ThingMaker.MakeThing(ThingDefOf.Silver);
                s.stackCount = silver;
                list.Add(s);
                left -= silver;
            }
            var pool = Candidates();
            var tech = Tech;
            for (int tries = 0; left > 15f && tries < 12 && list.Count < 5; tries++)
            {
                float budget = left;
                var d = pool.Where(x => x.BaseMarketValue <= budget).RandomElementByWeightWithFallback(x => Weight(x, fancy));
                if (d == null) break;
                ThingDef stuff = null;
                if (d.MadeFromStuff && !GenStuff.TryRandomStuffByCommonalityFor(d, out stuff, tech)) continue;
                var t = ThingMaker.MakeThing(d, stuff);
                t.TryGetComp<CompQuality>()?.SetQuality(fancy ? QualityUtility.GenerateQualitySuper() : QualityUtility.GenerateQualityReward(), ArtGenerationContext.Outsider);
                float unit = t.MarketValue;
                if (unit <= 0f || unit > left * 1.25f) continue;
                if (d.stackLimit > 1) t.stackCount = Mathf.Clamp(Mathf.FloorToInt(left * Rand.Range(0.4f, 0.9f) / unit), 1, d.stackLimit);
                left -= unit * t.stackCount;
                list.Add(t);
            }
            return list;
        }

        private static float Weight(ThingDef d, bool fancy)
        {
            if (d.IsWeapon || d.IsApparel) return fancy ? 3f : 0.6f;
            if (d.IsMedicine) return 2f;
            if (d.IsStuff) return d.BaseMarketValue >= 5f ? 2.5f : 1.5f;
            return 1f;
        }

        /// <summary>Delivered: carried in and left near the spot, or (an industrial colony) dropped by pod.</summary>
        public static void Deliver(List<Thing> things, Map map, IntVec3 near, bool pod)
        {
            if (things.NullOrEmpty() || map == null) return;
            if (pod && !Avail.IsPreIndustrial)
            {
                IntVec3 cell = near.IsValid ? near : DropCellFinder.TradeDropSpot(map);
                if (!DropCellFinder.TryFindDropSpotNear(cell, map, out var spot, false, false)) spot = DropCellFinder.TradeDropSpot(map);
                DropPodUtility.DropThingsNear(spot, map, things, 110, false, false, false, false);
                return;
            }
            foreach (var t in things) GenPlace.TryPlaceThing(t, near.IsValid ? near : DropCellFinder.TradeDropSpot(map), map, ThingPlaceMode.Near);
        }

        public static string Describe(List<Thing> things) => TextUtil.JoinAnd(things.Select(t => t.LabelCap.ToString()).ToList());
    }

    /// <summary>What a colonist used to be, read from their backstory titles (works for modded backstories too).</summary>
    public enum Past
    {
        Soldier,
        Outlaw,
        Noble,
        Healer,
        Scholar,
        Farmer,
        Trader,
        Artisan
    }

    public static class Pasts
    {
        private static readonly Dictionary<Past, Regex> Words = new Dictionary<Past, Regex>
        {
            { Past.Soldier, Words_("soldier|mercenar(y|ies)|merc|guard|guardsman|marine|knight|squire|veteran|militia\\w*|trooper|legionnaire|legionary|warrior|sentry|officer|sergeant|commando|gunner|sniper|janissary|cataphract|man-at-arms|footman|archer|bodyguard|enforcer|war\\w*|fighter|recruit|cadet|infantry\\w*|cavalry\\w*|scout|ranger") },
            { Past.Outlaw, Words_("thief|thieves|pirate|smuggler|bandit|assassin|convict|prisoner|gang\\w*|outlaw|con artist|conman|con man|raider|murderer|cutpurse|pickpocket|criminal|burglar|poacher|mugger|hitman|hit man|fugitive|deserter|cultist|slaver|scavenger|swindler|thug|brigand|highwayman|cutthroat|rogue|drug \\w+|dealer|fence") },
            { Past.Noble, Words_("noble\\w*|lord|lady|prince|princess|heir|heiress|aristocrat|courtier|baron\\w*|duke|duchess|royal\\w*|count|countess|socialite|dilettante|scion|knight-errant") },
            { Past.Healer, Words_("doctor|surgeon|healer|medic|nurse|herbalist|physician|apothecary|paramedic|midwife") },
            { Past.Scholar, Words_("scholar|scientist|researcher|professor|student|monk|scribe|teacher|philosopher|archivist|librarian|engineer|academic|tutor|historian|alchemist|astronomer") },
            { Past.Farmer, Words_("farmer|rancher|herder|shepherd|farmhand|farm \\w+|gardener|peasant|fisher\\w*|serf|villager|homesteader|beekeeper") },
            { Past.Trader, Words_("trader|merchant|shopkeeper|peddler|salesman|saleswoman|broker|gambler|financier|banker|caravaneer|moneylender|innkeeper|tavern\\w*") },
            { Past.Artisan, Words_("artist|sculptor|painter|musician|bard|poet|performer|actor|actress|singer|smith|blacksmith|carpenter|tailor|craftsman|craftswoman|jeweler|mechanic|potter|weaver|minstrel|dancer|entertainer") }
        };

        private static Regex Words_(string alternatives) => new Regex(@"\b(" + alternatives + @")\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>The backstory titles and categories, in one string.</summary>
        public static string Text(Pawn p)
        {
            var st = p?.story;
            if (st == null) return "";
            var parts = new List<string>();
            foreach (var b in new[] { st.Childhood, st.Adulthood })
            {
                if (b == null) continue;
                parts.Add(b.TitleFor(p.gender));
                if (b.spawnCategories != null) parts.AddRange(b.spawnCategories);
            }
            return string.Join(" | ", parts);
        }

        public static bool Has(Pawn p, Past k) => Words[k].IsMatch(Text(p));

        public static List<Past> Of(Pawn p)
        {
            string text = Text(p);
            return Words.Where(kv => kv.Value.IsMatch(text)).Select(kv => kv.Key).ToList();
        }

        /// <summary>Their most recent backstory title ("mercenary"), for the texts.</summary>
        public static string Title(Pawn p)
        {
            var b = p?.story?.Adulthood ?? p?.story?.Childhood;
            return b == null ? "" : b.TitleShortFor(p.gender).ToLowerInvariant();
        }

        public static string Tag(Past k) => k.ToString().ToLowerInvariant();

        /// <summary>A rough life of violence: bloodlust or psychopathy, or a past as a fighter or an outlaw.</summary>
        public static bool Violent(Pawn p)
        {
            var t = p?.story?.traits;
            if (t != null && (t.HasTrait(TraitDefOf.Bloodlust) || t.HasTrait(TraitDefOf.Psychopath))) return true;
            return Has(p, Past.Outlaw);
        }
    }
}
